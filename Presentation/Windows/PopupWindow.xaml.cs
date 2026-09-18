using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TransparentHotkeyUtility.Infrastructure;
using TransparentHotkeyUtility.Infrastructure.Native;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation;
using TransparentHotkeyUtility.Presentation.Figure;
using TransparentHotkeyUtility.Presentation.Forms;
using TransparentHotkeyUtility.Services;
using System.Windows.Controls.Primitives;

using Key            = System.Windows.Input.Key;
using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
using Color          = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes        = System.Windows.Media.Brushes;
using Cursors        = System.Windows.Input.Cursors;
using TextBox        = System.Windows.Controls.TextBox;
using CheckBox       = System.Windows.Controls.CheckBox;
using Button         = System.Windows.Controls.Button;
using WF             = System.Windows.Forms;

namespace TransparentHotkeyUtility.Presentation.Windows;

public partial class PopupWindow : Window
{
    // ── Core state ────────────────────────────────────────────────────────────

    private readonly AppSettings               _settings;
    private readonly FigureManager             _figure;
    private readonly SettingsDragController    _drag;

    private FigureConfig   _figureConfig        = new();
    private bool           _suppressDeactivated;
    private double         _anchorCx, _anchorCy;
    private int            _currentPageIndex;

    /// <summary>Кружки текущей страницы.</summary>
    private List<CircleConfig> CurrentCircles =>
        _figureConfig.Pages.Count > 0
            ? _figureConfig.Pages[Math.Clamp(_currentPageIndex, 0, _figureConfig.Pages.Count - 1)].Circles
            : _figureConfig.Circles;

    // ── Settings mode state ───────────────────────────────────────────────────

    private bool _settingsMode;
    private bool _suppressCircleTypeChange;
    private readonly List<int> _selectedChildPath = new();

    // ── Modal mode state ──────────────────────────────────────────────────────

    private CircleConfig?  _activeModalConfig;
    /// <summary>Группы-предки (сверху вниз): поля каждой идут в аргументах перед полями <see cref="_activeModalConfig"/>.</summary>
    private List<CircleConfig>? _activeModalGroups;
    private FigureNode? _activeModalNode;
    private readonly Dictionary<FormFieldConfig, FrameworkElement> _dynamicControls = new();

    private sealed record SettingsSnap(string Folder, string Repo);
    private readonly Stack<SettingsSnap> _undoStack = new();
    private readonly Stack<SettingsSnap> _redoStack = new();
    private SettingsSnap _settingsSnap = new(string.Empty, string.Empty);

    // ── Constructor ───────────────────────────────────────────────────────────

    public PopupWindow()
    {
        InitializeComponent();

        _settings = SettingsService.Load();
        TxtFolder.Text = _settings.FolderPath;
        TxtRepo.Text   = _settings.RepoUrl;

        _figure = new FigureManager(LineLayer, CircleLayer);
        _drag   = new SettingsDragController(MainCanvas, () => _settingsMode);

        ChkSpawnAtCursor.IsChecked  = _settings.SpawnAtCursor;
        ChkSpawnAtCursor.Checked   += (_, _) => SaveSetting(s => s.SpawnAtCursor = true);
        ChkSpawnAtCursor.Unchecked += (_, _) => SaveSetting(s => s.SpawnAtCursor = false);

        TxtHotkey.Text = _settings.HotkeyText;

        MainCanvas.MouseMove  += MainCanvas_MouseMove;
        MainCanvas.MouseLeave += (_, _) => _figure.ResetMagnet(instant: false);
        MainCanvas.MouseWheel += MainCanvas_MouseWheel;

        WireTextFields();
    }

    private void MainCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_settingsMode || _activeModalConfig is not null) return;
        _figure.UpdateMagnet(e.GetPosition(MainCanvas));
    }

    private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_settingsMode || _activeModalConfig is not null) return;
        if (_figureConfig.Pages.Count <= 1) return;

        int delta = e.Delta > 0 ? -1 : +1;
        SwitchPage(delta);
        e.Handled = true;
    }

    /// <summary>Переключает страницу фигуры (колёсико / кнопки в настройках).</summary>
    private void SwitchPage(int delta)
    {
        int count = _figureConfig.Pages.Count;
        if (count <= 1) return;

        _currentPageIndex = ((_currentPageIndex + delta) % count + count) % count;
        RebuildCurrentPage();
        PersistCurrentPage();
        if (_settingsMode)
            UpdateSettingsPageBar();
    }

    /// <summary>Восстанавливает индекс страницы по сохранённому Id.</summary>
    private static int ResolvePageIndex(string? pageId, FigureConfig config)
    {
        if (config.Pages.Count == 0) return 0;
        if (!string.IsNullOrEmpty(pageId))
        {
            for (int i = 0; i < config.Pages.Count; i++)
            {
                if (config.Pages[i].Id == pageId)
                    return i;
            }
        }
        return 0;
    }

    /// <summary>Сохраняет Id текущей страницы в settings.json.</summary>
    private void PersistCurrentPage()
    {
        if (_figureConfig.Pages.Count == 0) return;
        var pageId = _figureConfig.Pages[_currentPageIndex].Id;
        if (_settings.LastPageId == pageId) return;
        SaveSetting(s => s.LastPageId = pageId);
    }

    /// <summary>Перестраивает фигуру для текущей страницы.</summary>
    private void RebuildCurrentPage()
    {
        _figure.Clear();
        _figure.Build(_anchorCx, _anchorCy, CurrentCircles, OnCircleActivated);
        UpdatePageIndicator();
        PositionInputCard();

        if (_settingsMode)
        {
            _drag.EnableForTree(_figure.RootNodes, SelectConfig);
            ApplySettingsVisualMode(null);
            SelectConfig(null);
        }
    }

    private void UpdatePageIndicator()
    {
        int count = _figureConfig.Pages.Count;
        if (count <= 1)
        {
            PageIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var name = _figureConfig.Pages[_currentPageIndex].Name;
        PageIndicatorText.Text = $"{_currentPageIndex + 1} / {count}  ·  {name}";
        PageIndicator.Visibility = Visibility.Visible;
    }

    // ── Управление страницами (настройки) ─────────────────────────────────────

    private void BtnPagePrev_Click(object sender, RoutedEventArgs e) => SwitchPage(-1);
    private void BtnPageNext_Click(object sender, RoutedEventArgs e) => SwitchPage(+1);

    private void BtnAddPage_Click(object sender, RoutedEventArgs e)
    {
        var page = new FigurePage { Name = $"Страница {_figureConfig.Pages.Count + 1}" };
        _figureConfig.Pages.Add(page);
        _currentPageIndex = _figureConfig.Pages.Count - 1;
        RebuildCurrentPage();
        PersistCurrentPage();
        UpdateSettingsPageBar();
    }

    private void BtnDeletePage_Click(object sender, RoutedEventArgs e)
    {
        if (_figureConfig.Pages.Count <= 1) return;
        _figureConfig.Pages.RemoveAt(_currentPageIndex);
        _currentPageIndex = Math.Clamp(_currentPageIndex, 0, _figureConfig.Pages.Count - 1);
        RebuildCurrentPage();
        PersistCurrentPage();
        UpdateSettingsPageBar();
    }

    private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        int mods = ReadKeyboardModifiers();
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        string text = NativeMethods.FormatModifiers(mods) + key;
        ApplyHotkeyBinding(HotkeyInputKind.Keyboard, mods, vk, mouseButton: 0, text);
    }

    private void TxtHotkey_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Первый клик только фокусирует поле — захват со следующего нажатия.
        // Mouse4/Mouse5 надёжнее ловятся через LL-хук (BeginMouseCapture).
        if (!TxtHotkey.IsKeyboardFocusWithin)
            return;

        int button = e.ChangedButton switch
        {
            MouseButton.Left   => NativeMethods.MouseBtnLeft,
            MouseButton.Right  => NativeMethods.MouseBtnRight,
            MouseButton.Middle => NativeMethods.MouseBtnMiddle,
            _ => 0
        };
        if (button == 0) return;

        e.Handled = true;

        int mods = ReadKeyboardModifiers();
        string text = NativeMethods.FormatModifiers(mods) + NativeMethods.MouseButtonName(button);
        ApplyHotkeyBinding(HotkeyInputKind.Mouse, mods, vk: 0, button, text);
    }

    private static int ReadKeyboardModifiers()
    {
        int mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))     mods |= NativeMethods.MOD_ALT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))   mods |= NativeMethods.MOD_SHIFT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;
        return mods;
    }

    private void ApplyHotkeyBinding(HotkeyInputKind kind, int mods, int vk, int mouseButton, string text)
    {
        TxtHotkey.Text = text;

        SaveSetting(s =>
        {
            s.HotkeyKind        = kind;
            s.HotkeyModifiers   = mods;
            s.HotkeyVirtualKey  = vk;
            s.HotkeyMouseButton = mouseButton;
            s.HotkeyText        = text;
        });

        if (System.Windows.Application.Current is App app)
            app.Host.ApplyHotkey();
    }

    private void OnHotkeyMouseCaptured(int button, int mods)
    {
        if (!TxtHotkey.IsKeyboardFocusWithin) return;

        string text = NativeMethods.FormatModifiers(mods) + NativeMethods.MouseButtonName(button);
        ApplyHotkeyBinding(HotkeyInputKind.Mouse, mods, vk: 0, button, text);
        // Снимаем фокус, чтобы не ловить повторные нажатия в режиме захвата
        Keyboard.ClearFocus();
    }

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = "Клавиша или кнопка мыши...";
        if (System.Windows.Application.Current is App app)
            app.Host.BeginMouseCapture(OnHotkeyMouseCaptured);
    }

    private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = _settings.HotkeyText;
        if (System.Windows.Application.Current is App app)
            app.Host.EndMouseCapture();
    }

    private void WireTextFields()
    {
        TxtFolder.LostFocus += (_, _) =>
        {
            if (_settingsMode) PushSettingsSnapshot();
            else SaveSetting(s => s.FolderPath = TxtFolder.Text.Trim());
        };
        TxtRepo.LostFocus += (_, _) =>
        {
            if (_settingsMode) PushSettingsSnapshot();
            else SaveSetting(s => s.RepoUrl = TxtRepo.Text.Trim());
        };
        Deactivated += (_, _) => { if (!_suppressDeactivated) HidePopup(); };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowAtCursor(System.Drawing.Point screenPos)
    {
        // Сразу прячем и очищаем старую фигуру, чтобы не мелькал прошлый кадр.
        Opacity = 0;
        _figure.Clear();

        if (!_settings.SpawnAtCursor)
        {
            var primary = WF.Screen.PrimaryScreen!;
            screenPos = new System.Drawing.Point(
                primary.Bounds.Left + primary.Bounds.Width  / 2,
                primary.Bounds.Top  + primary.Bounds.Height / 2);
        }

        // Позиционируем окно ДО показа / перестройки
        PlaceWindowAt(screenPos);

        _figureConfig = FigureConfigService.Load();
        _currentPageIndex = ResolvePageIndex(_settings.LastPageId, _figureConfig);
        ResetAllModes();
        _figure.MagnetInteractive = true;
        _figure.SetSettingsMode(false);
        _figure.Build(_anchorCx, _anchorCy, CurrentCircles, OnCircleActivated);
        UpdatePageIndicator();
        PositionInputCard();
        UpdateLayout();

        // Показываем уже готовое окно в новой позиции — без кадра на старом месте.
        // Visibility=Visible (вместо Show) не пересоздаёт HWND и рендерит сразу в нужной точке.
        if (!IsVisible)
            Visibility = Visibility.Visible;

        // Opacity=1 только после кадра с уже готовой раскладкой
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (!IsVisible) return;
            Opacity = 1;
            Activate();
        });
    }

    public void ShowForSettings()
    {
        var s      = WF.Screen.PrimaryScreen!;
        var center = new System.Drawing.Point(
            s.Bounds.Left + s.Bounds.Width  / 2,
            s.Bounds.Top  + s.Bounds.Height / 2);

        ShowAtCursor(center);

        _settingsMode              = true;
        _figure.SetSettingsMode(true);
        _figure.MagnetInteractive  = false;
        _figure.ResetMagnet();
        DimOverlay.Visibility      = Visibility.Visible;
        SettingsToolbar.Visibility = Visibility.Visible;

        _settingsSnap = new SettingsSnap(_settings.FolderPath, _settings.RepoUrl);
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoButtons();

        BtnDeleteCircle.IsEnabled   = false;
        SettingsPanel.Visibility    = Visibility.Visible;
        CirclePropsPanel.Visibility = Visibility.Collapsed;
        SettingsHintText.Visibility = Visibility.Visible;

        _drag.EnableForTree(_figure.RootNodes, SelectConfig);
        ApplySettingsVisualMode(null);
        UpdateSettingsPageBar();
    }

    private void UpdateSettingsPageBar()
    {
        int count = _figureConfig.Pages.Count;
        if (count == 0) return;
        _currentPageIndex = Math.Clamp(_currentPageIndex, 0, count - 1);
        TxtPageName.Text = $"{_currentPageIndex + 1}/{count} · {_figureConfig.Pages[_currentPageIndex].Name}";
        BtnDeletePage.IsEnabled = count > 1;
    }

    public void HidePopup()
    {
        PersistCurrentPage();
        ExitModalMode(instant: true);
        _figure.CollapseAllGroupsImmediate();
        _figure.ResetMagnet();
        _figure.Clear();
        _drag.Reset();
        _settingsMode        = false;
        _figure.SetSettingsMode(false);
        _figure.MagnetInteractive = true;
        _selectedChildPath.Clear();

        DimOverlay.Visibility        = Visibility.Collapsed;
        SettingsToolbar.Visibility   = Visibility.Collapsed;
        ConfirmClosePanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility     = Visibility.Collapsed;
        PageIndicator.Visibility     = Visibility.Collapsed;
        Opacity = 0;
        Visibility = Visibility.Collapsed;
    }

    // ── Window placement ──────────────────────────────────────────────────────

    private void PlaceWindowAt(System.Drawing.Point screenPos)
    {
        var screen   = WF.Screen.FromPoint(screenPos);
        var b        = screen.Bounds;
        var (sx, sy) = WpfUtils.GetDpiScale();

        Left   = b.Left  / sx;
        Top    = b.Top   / sy;
        Width  = b.Width / sx;
        Height = b.Height / sy;

        _anchorCx = (screenPos.X - b.Left) / sx;
        _anchorCy = (screenPos.Y - b.Top)  / sy;
    }

    // ── Settings toolbar handlers ─────────────────────────────────────────────

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (!_settingsMode) { HidePopup(); return; }

        if (ConfirmClosePanel.Visibility == Visibility.Visible)
        {
            ConfirmClosePanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (HasUnsavedSettingsChanges())
            ConfirmClosePanel.Visibility = Visibility.Visible;
        else
            HidePopup();
    }

    private void BtnConfirmSaveYes_Click(object sender, RoutedEventArgs e)
    {
        CommitSettingsFields();
        FigureConfigService.Save(_figureConfig);
        HidePopup();
    }

    private void BtnConfirmSaveNo_Click(object sender, RoutedEventArgs e) => HidePopup();

    private void BtnSettingsSave_Click(object sender, RoutedEventArgs e)
    {
        PushSettingsSnapshot();
        CommitSettingsFields();
        FigureConfigService.Save(_figureConfig);
    }

    private void BtnSettingsUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(_settingsSnap);
        _settingsSnap  = _undoStack.Pop();
        TxtFolder.Text = _settingsSnap.Folder;
        TxtRepo.Text   = _settingsSnap.Repo;
        UpdateUndoRedoButtons();
    }

    private void BtnSettingsRedo_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(_settingsSnap);
        _settingsSnap  = _redoStack.Pop();
        TxtFolder.Text = _settingsSnap.Folder;
        TxtRepo.Text   = _settingsSnap.Repo;
        UpdateUndoRedoButtons();
    }

    // ── Settings helpers ──────────────────────────────────────────────────────

    private void CommitSettingsFields()
    {
        SaveSetting(s =>
        {
            s.FolderPath = TxtFolder.Text.Trim();
            s.RepoUrl    = TxtRepo.Text.Trim();
        });
        _settingsSnap = new SettingsSnap(_settings.FolderPath, _settings.RepoUrl);
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void PushSettingsSnapshot()
    {
        var snap = new SettingsSnap(TxtFolder.Text.Trim(), TxtRepo.Text.Trim());
        if (snap == _settingsSnap) return;
        _undoStack.Push(_settingsSnap);
        _settingsSnap = snap;
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        BtnSettingsUndo.IsEnabled = _undoStack.Count > 0;
        BtnSettingsRedo.IsEnabled = _redoStack.Count > 0;
    }

    private bool HasUnsavedSettingsChanges() =>
        TxtFolder.Text.Trim() != _settings.FolderPath
        || TxtRepo.Text.Trim() != _settings.RepoUrl;

    // ── Модальная форма и карточка ввода: см. PopupWindow.CircleSession.cs ──

    private void ResetAllModes()
    {
        MainFieldsPanel.Visibility   = Visibility.Collapsed;
        DynamicFormPanel.Visibility  = Visibility.Collapsed;

        ClearMainError();
        SyncInputCardVisibility();
    }

    /// <summary>
    /// Карточка ввода скрыта, пока не открыт ни один режим (пустая рамка не показывается).
    /// </summary>
    private void SyncInputCardVisibility()
    {
        bool any =
            MainFieldsPanel.Visibility == Visibility.Visible
            || DynamicFormPanel.Visibility == Visibility.Visible;
        InputCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Error display ─────────────────────────────────────────────────────────

    private void ShowMainError(string text)
    {
        TxtMainError.Text       = text;
        TxtMainError.Visibility = Visibility.Visible;
    }

    private void ClearMainError()
    {
        TxtMainError.Text       = string.Empty;
        TxtMainError.Visibility = Visibility.Collapsed;
    }

    private void MainFields_TextChanged(object sender, TextChangedEventArgs e) => ClearMainError();

    // ── Input card positioning ────────────────────────────────────────────────

    private void PositionInputCard()
    {
        if (InputCard.Visibility != Visibility.Visible)
            return;

        const double cardW = 360;
        const double pad   = 24;

        InputCard.UpdateLayout();
        double cardH = InputCard.ActualHeight > 1 ? InputCard.ActualHeight + 12 : 200;

        double figBottom = CurrentCircles.Count > 0
            ? CurrentCircles.Max(c => c.OffsetY + c.Radius)
            : 40;

        Canvas.SetLeft(InputCard, Math.Clamp(_anchorCx - cardW / 2, pad, Width  - cardW - pad));
        Canvas.SetTop(InputCard,  Math.Clamp(_anchorCy + figBottom + 28, pad, Height - cardH - pad));
    }

    // ── Browse dialog ─────────────────────────────────────────────────────────

    /// <summary>
    /// Выполняет действие (обычно — показ WinForms-диалога), не давая окну
    /// скрыться по событию Deactivated, и возвращает фокус после.
    /// </summary>
    internal void SuppressDeactivatedWhile(Action action)
    {
        _suppressDeactivated = true;
        try
        {
            action();
        }
        finally
        {
            _suppressDeactivated = false;
            Activate();
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        _suppressDeactivated = true;
        try
        {
            using var dlg = new WF.FolderBrowserDialog
            {
                Description            = "Выберите папку проекта",
                UseDescriptionForTitle = true,
            };
            if (!string.IsNullOrWhiteSpace(TxtFolder.Text) && Directory.Exists(TxtFolder.Text))
                dlg.InitialDirectory = TxtFolder.Text;

            var owner = new Win32Window(new WindowInteropHelper(this).Handle);
            if (dlg.ShowDialog(owner) == WF.DialogResult.OK)
            {
                TxtFolder.Text = dlg.SelectedPath;
                SaveSetting(s => s.FolderPath = dlg.SelectedPath);
                ClearMainError();
            }
        }
        finally
        {
            _suppressDeactivated = false;
            Activate();
        }
    }

    private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
    {
        _suppressDeactivated = true;
        try
        {
            using var dlg = new WF.OpenFileDialog
            {
                Title  = "Выберите .exe или скрипт AutoHotkey v2",
                Filter = "Программы и скрипты|*.exe;*.ahk|Исполняемые (*.exe)|*.exe|AutoHotkey v2 (*.ahk)|*.ahk|Все файлы (*.*)|*.*",
            };
            if (!string.IsNullOrWhiteSpace(PropExePath.Text))
            {
                var dir = Path.GetDirectoryName(PropExePath.Text);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
            }

            var owner = new Win32Window(new WindowInteropHelper(this).Handle);
            if (dlg.ShowDialog(owner) == WF.DialogResult.OK)
            {
                PropExePath.Text = dlg.FileName;
                ApplyCircleProps();
            }
        }
        finally
        {
            _suppressDeactivated = false;
            Activate();
        }
    }

    // Детали редактора полей/кружков вынесены в PopupWindow.SettingsEditor.cs

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SaveSetting(Action<AppSettings> mutate)
    {
        mutate(_settings);
        SettingsService.Save(_settings);
    }

    private static void TryPresetDialogColor(WF.ColorDialog dlg, string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch { }
    }
}
