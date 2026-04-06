using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Services;

using WF = System.Windows.Forms;

namespace TransparentHotkeyUtility.UI;

public partial class PopupWindow : Window
{
    private readonly AppSettings _settings;
    private          FigureConfig _figureConfig = new();

    // Guard against Deactivated firing while a WinForms dialog is open
    private bool _suppressDeactivated;

    public PopupWindow()
    {
        InitializeComponent();

        _settings = SettingsService.Load();

        TxtFolder.Text = _settings.FolderPath;
        TxtRepo.Text   = _settings.RepoUrl;

        TxtFolder.LostFocus += (_, _) => SaveSetting(s => s.FolderPath = TxtFolder.Text.Trim());
        TxtRepo.LostFocus   += (_, _) => SaveSetting(s => s.RepoUrl    = TxtRepo.Text.Trim());

        Deactivated += (_, _) => { if (!_suppressDeactivated) HidePopup(); };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowAtCursor(System.Drawing.Point screenPos)
    {
        // Reload config on each show so edits to figure.json apply without restart
        _figureConfig = FigureConfigService.Load();

        var screen   = WF.Screen.FromPoint(screenPos);
        var b        = screen.Bounds;
        var (sx, sy) = GetDpiScale();

        // Screen.Bounds / Cursor.Position are in physical pixels;
        // WPF Window.Left/Top/Width/Height are in logical units (DIPs).
        Left   = b.Left   / sx;
        Top    = b.Top    / sy;
        Width  = b.Width  / sx;
        Height = b.Height / sy;

        double cx = (screenPos.X - b.Left) / sx;
        double cy = (screenPos.Y - b.Top)  / sy;

        BuildFigure(cx, cy);
        PositionInputCard(cx, cy);

        Show();
        Activate();
    }

    public void HidePopup() => Hide();

    // ── Figure construction ───────────────────────────────────────────────────

    private void BuildFigure(double cx, double cy)
    {
        LineLayer.Children.Clear();
        CircleLayer.Children.Clear();

        var circles = _figureConfig.Circles;
        if (circles.Count == 0) return;

        var lineBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 160, 110, 255));

        foreach (var cfg in circles)
        {
            LineLayer.Children.Add(new Line
            {
                X1              = cx,
                Y1              = cy,
                X2              = cx + cfg.OffsetX,
                Y2              = cy + cfg.OffsetY,
                Stroke          = lineBrush,
                StrokeThickness = 1.0,
            });
        }

        foreach (var cfg in circles)
        {
            var element = CircleElementFactory.Create(cfg);
            Canvas.SetLeft(element, cx + cfg.OffsetX - cfg.Radius);
            Canvas.SetTop(element,  cy + cfg.OffsetY - cfg.Radius);
            CircleLayer.Children.Add(element);
        }
    }

    // ── Input card ───────────────────────────────────────────────────────────

    private void PositionInputCard(double cx, double cy)
    {
        const double cardW = 360;
        const double cardH = 170;
        const double pad   = 24;

        double figBottom = _figureConfig.Circles.Count > 0
            ? _figureConfig.Circles.Max(c => c.OffsetY + c.Radius)
            : 40;

        double x = Math.Clamp(cx - cardW / 2, pad, Width  - cardW - pad);
        double y = Math.Clamp(cy + figBottom + 28, pad, Height - cardH - pad);

        Canvas.SetLeft(InputCard, x);
        Canvas.SetTop(InputCard,  y);
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
            }
        }
        finally
        {
            _suppressDeactivated = false;
            Activate();
        }
    }

    // ── Mouse: close when clicking outside figure and card ───────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        if (!IsDescendantOf(src, CircleLayer) && !IsDescendantOf(src, InputCard))
            HidePopup();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SaveSetting(Action<AppSettings> mutate)
    {
        mutate(_settings);
        SettingsService.Save(_settings);
    }

    /// <summary>DPI scale factors relative to the base 96 DPI.</summary>
    private static (double x, double y) GetDpiScale()
    {
        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return (g.DpiX / 96.0, g.DpiY / 96.0);
    }

    private static bool IsDescendantOf(DependencyObject? child, DependencyObject root)
    {
        while (child is not null)
        {
            if (ReferenceEquals(child, root)) return true;
            child = VisualTreeHelper.GetParent(child);
        }
        return false;
    }

    // Thin wrapper so WinForms dialogs can accept a WPF window as owner
    private sealed class Win32Window(IntPtr handle) : WF.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
