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

        WireTextFields();
    }

    private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        int mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))     mods |= NativeMethods.MOD_ALT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))   mods |= NativeMethods.MOD_SHIFT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        if (mods == 0) return; // Требуем хотя бы один модификатор

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        string text = "";
        if ((mods & NativeMethods.MOD_CONTROL) != 0) text += "Ctrl + ";
        if ((mods & NativeMethods.MOD_ALT) != 0)     text += "Alt + ";
        if ((mods & NativeMethods.MOD_SHIFT) != 0)   text += "Shift + ";
        if ((mods & NativeMethods.MOD_WIN) != 0)     text += "Win + ";
        text += key.ToString();

        TxtHotkey.Text = text;

        SaveSetting(s =>
        {
            s.HotkeyModifiers = mods;
            s.HotkeyVirtualKey = vk;
            s.HotkeyText = text;
        });

        // Перерегистрируем хоткей
        if (System.Windows.Application.Current is App app)
        {
            app.Host.ApplyHotkey();
        }
    }

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = "Нажмите комбинацию...";
    }

    private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = _settings.HotkeyText;
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
        // Убираем визуальный «кадр прошлого открытия»: сначала показываем окно прозрачным,
        // затем перестраиваем фигуру в новой точке и только после этого возвращаем непрозрачность.
        bool wasVisible = IsVisible;
        Opacity = 0;
        if (!wasVisible)
            Show();

        _figureConfig = FigureConfigService.Load();

        if (!_settings.SpawnAtCursor)
        {
            var primary = WF.Screen.PrimaryScreen!;
            screenPos = new System.Drawing.Point(
                primary.Bounds.Left + primary.Bounds.Width  / 2,
                primary.Bounds.Top  + primary.Bounds.Height / 2);
        }

        PlaceWindowAt(screenPos);

        ResetAllModes();
        _figure.Build(_anchorCx, _anchorCy, _figureConfig, OnCircleActivated);
        PositionInputCard();

        UpdateLayout();
        Opacity = 1;
        Activate();
    }

    public void ShowForSettings()
    {
        var s      = WF.Screen.PrimaryScreen!;
        var center = new System.Drawing.Point(
            s.Bounds.Left + s.Bounds.Width  / 2,
            s.Bounds.Top  + s.Bounds.Height / 2);

        ShowAtCursor(center);

        _settingsMode              = true;
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
    }

    public void HidePopup()
    {
        ExitModalMode(instant: true);
        _figure.CollapseAllGroupsImmediate();
        _drag.Reset();
        _settingsMode        = false;
        _selectedChildPath.Clear();

        DimOverlay.Visibility        = Visibility.Collapsed;
        SettingsToolbar.Visibility   = Visibility.Collapsed;
        ConfirmClosePanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility     = Visibility.Collapsed;
        Hide();
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

        double figBottom = _figureConfig.Circles.Count > 0
            ? _figureConfig.Circles.Max(c => c.OffsetY + c.Radius)
            : 40;

        Canvas.SetLeft(InputCard, Math.Clamp(_anchorCx - cardW / 2, pad, Width  - cardW - pad));
        Canvas.SetTop(InputCard,  Math.Clamp(_anchorCy + figBottom + 28, pad, Height - cardH - pad));
    }

    // ── Browse dialog ─────────────────────────────────────────────────────────

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
                Title  = "Выберите исполняемый файл",
                Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
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
