using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using TransparentHotkeyUtility.Infrastructure.Native;
using TransparentHotkeyUtility.Presentation.Windows;

namespace TransparentHotkeyUtility;

/// <summary>
/// Управляет регистрацией хоткея, системным треем и жизненным циклом PopupWindow.
/// Использует скрытое WPF-окно для получения WM_HOTKEY.
/// </summary>
internal sealed class HotkeyHost : IDisposable
{
    private const int HotkeyId = 1;

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

        ApplyHotkey();

        // ── Системный трей ────────────────────────────────────────────────────
        _tray = new NotifyIcon
        {
            Icon    = SystemIcons.Application,
            Text    = "gitHelper",
            Visible = true,
        };
        UpdateTrayText();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть / Закрыть", null, (_, _) => TogglePopup());
        menu.Items.Add("Настройки",         null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход",             null, (_, _) => System.Windows.Application.Current.Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => TogglePopup();
    }

    public bool ApplyHotkey()
    {
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);

        var settings = Services.SettingsService.Load();
        int mods = settings.HotkeyModifiers;
        int vk = settings.HotkeyVirtualKey;

        // Fallback, если ничего не задано
        if (mods == 0 && vk == 0)
        {
            mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
            vk = 0xBA; // VK_OEM_1 (;)
        }

        bool ok = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, mods, vk);

        if (!ok)
            System.Windows.MessageBox.Show(
                $"Не удалось зарегистрировать хоткей ({settings.HotkeyText}).\n" +
                "Возможно, он занят другой программой.",
                "gitHelper",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        
        UpdateTrayText();
        return ok;
    }

    private void UpdateTrayText()
    {
        if (_tray != null)
        {
            var settings = Services.SettingsService.Load();
            var txt = settings.HotkeyText;
            if (string.IsNullOrWhiteSpace(txt)) txt = "Ctrl+Alt+;";
            _tray.Text = $"gitHelper  ({txt})";
        }
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

    private void OpenSettings()
    {
        if (_popup.IsVisible)
        {
            _popup.Activate();
            return;
        }

        _popup.ShowForSettings();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _source.Dispose();
        _tray.Dispose();
    }
}
