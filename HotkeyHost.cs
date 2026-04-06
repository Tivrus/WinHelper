using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using TransparentHotkeyUtility.Native;
using TransparentHotkeyUtility.UI;

namespace TransparentHotkeyUtility;

/// <summary>
/// Управляет регистрацией хоткея, системным треем и жизненным циклом PopupWindow.
/// Использует скрытое WPF-окно для получения WM_HOTKEY.
/// </summary>
internal sealed class HotkeyHost : IDisposable
{
    private const int HotkeyId = 1;
    private const int VK_OEM_1 = 0xBA; // Ctrl+Alt+;

    private readonly IntPtr      _hwnd;
    private readonly HwndSource  _source;
    private readonly Window      _msgWin;
    private readonly PopupWindow _popup;
    private readonly NotifyIcon  _tray;

    public HotkeyHost()
    {
        _popup = new PopupWindow();

        // Скрытое окно только для приёма WM_HOTKEY — никогда не показывается
        _msgWin = new Window
        {
            Width         = 0,
            Height        = 0,
            WindowStyle   = WindowStyle.None,
            ShowInTaskbar = false,
        };
        var winHelper = new WindowInteropHelper(_msgWin);
        winHelper.EnsureHandle();
        _hwnd   = winHelper.Handle;
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);

        bool ok = NativeMethods.RegisterHotKey(
            _hwnd, HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
            VK_OEM_1);

        if (!ok)
            System.Windows.MessageBox.Show(
                "Не удалось зарегистрировать Ctrl+Alt+;\n" +
                "Возможно, хоткей занят другой программой.",
                "gitHelper",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);

        // ── Системный трей ────────────────────────────────────────────────────
        _tray = new NotifyIcon
        {
            Icon    = SystemIcons.Application,
            Text    = "gitHelper  (Ctrl+Alt+;)",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть / Закрыть", null, (_, _) => TogglePopup());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход",             null, (_, _) => System.Windows.Application.Current.Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => TogglePopup();
    }

    // ── WM_HOTKEY ─────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            TogglePopup();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void TogglePopup()
    {
        if (_popup.IsVisible)
            _popup.HidePopup();
        else
            _popup.ShowAtCursor(Cursor.Position);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _source.Dispose();
        _tray.Dispose();
    }
}
