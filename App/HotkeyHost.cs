using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using TransparentHotkeyUtility.Infrastructure.Native;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation.Windows;
using TransparentHotkeyUtility.Services;

namespace TransparentHotkeyUtility;

/// <summary>
/// Управляет триггером открытия меню (хоткей / одиночная клавиша / кнопка мыши),
/// системным треем и жизненным циклом PopupWindow.
/// </summary>
internal sealed class HotkeyHost : IDisposable
{
    private const int HotkeyId = 1;
    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMilliseconds(250);

    private readonly IntPtr      _hwnd;
    private readonly HwndSource  _source;
    private readonly Window      _msgWin;
    private readonly PopupWindow _popup;
    private readonly NotifyIcon  _tray;

    // Делегаты держим в полях, иначе GC их соберёт и хук сломается.
    private NativeMethods.LowLevelProc? _keyboardProc;
    private NativeMethods.LowLevelProc? _mouseProc;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook    = IntPtr.Zero;

    private HotkeyInputKind _kind;
    private int _mods;
    private int _vk;
    private int _mouseButton;
    private DateTime _lastTriggerUtc = DateTime.MinValue;

    /// <summary>
    /// Пока true — глобальный триггер не срабатывает (идёт захват в настройках).
    /// </summary>
    public bool SuppressTrigger { get; set; }

    /// <summary>Колбэк захвата кнопки мыши в настройках (button, modifiers).</summary>
    private Action<int, int>? _onMouseCaptured;
    private bool _captureMouseHookOwned;

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
        // На случай вызова из обработчика захвата — сбрасываем callback до переустановки хуков
        _onMouseCaptured       = null;
        _captureMouseHookOwned = false;
        SuppressTrigger        = false;

        ClearBindings();

        var settings = SettingsService.Load();
        _kind        = settings.HotkeyKind;
        _mods        = settings.HotkeyModifiers;
        _vk          = settings.HotkeyVirtualKey;
        _mouseButton = settings.HotkeyMouseButton;

        // Fallback, если ничего не задано
        if (_kind == HotkeyInputKind.Keyboard && _mods == 0 && _vk == 0)
        {
            _mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
            _vk   = 0xBA; // VK_OEM_1 (;)
        }

        if (_kind == HotkeyInputKind.Mouse && _mouseButton == 0)
        {
            _kind = HotkeyInputKind.Keyboard;
            _mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
            _vk   = 0xBA;
        }

        bool ok;
        if (_kind == HotkeyInputKind.Mouse)
        {
            ok = InstallMouseHook();
        }
        else if (_mods == 0)
        {
            // Одиночная клавиша без модификаторов — через LL-хук
            ok = InstallKeyboardHook();
        }
        else
        {
            ok = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, _mods, _vk);
        }

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

    /// <summary>
    /// Включает режим захвата кнопки мыши (в т.ч. Mouse4/Mouse5) для поля настроек.
    /// </summary>
    public void BeginMouseCapture(Action<int, int> onMouseCaptured)
    {
        SuppressTrigger   = true;
        _onMouseCaptured  = onMouseCaptured;
        if (_mouseHook == IntPtr.Zero)
        {
            _captureMouseHookOwned = InstallMouseHook();
        }
    }

    /// <summary>
    /// Выключает режим захвата и восстанавливает рабочий триггер.
    /// </summary>
    public void EndMouseCapture()
    {
        bool hadCapture = _onMouseCaptured != null || _captureMouseHookOwned;
        _onMouseCaptured       = null;
        _captureMouseHookOwned = false;
        SuppressTrigger        = false;
        if (hadCapture)
            ApplyHotkey();
    }

    private void ClearBindings()
    {
        _captureMouseHookOwned = false;
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        _keyboardProc = null;

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _mouseProc = null;
    }

    private bool InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardProc,
            NativeMethods.GetModuleHandle(null),
            0);
        return _keyboardHook != IntPtr.Zero;
    }

    private bool InstallMouseHook()
    {
        _mouseProc = MouseHookCallback;
        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _mouseProc,
            NativeMethods.GetModuleHandle(null),
            0);
        return _mouseHook != IntPtr.Zero;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION && !SuppressTrigger)
        {
            int msg = wParam.ToInt32();
            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                // flags bit0 = extended, bit4 = injected, bit7 = transition (1 = key up) — KEYDOWN only
                int currentMods = NativeMethods.GetCurrentModifiers();
                // Модификаторы сами по себе не триггерят
                if (info.vkCode is NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL
                    or NativeMethods.VK_MENU or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN)
                    return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                if (info.vkCode == _vk && currentMods == _mods)
                {
                    if (TryFireTrigger())
                        return (IntPtr)1; // поглощаем
                }
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION)
        {
            int msg = wParam.ToInt32();
            int button = msg switch
            {
                NativeMethods.WM_LBUTTONDOWN => NativeMethods.MouseBtnLeft,
                NativeMethods.WM_RBUTTONDOWN => NativeMethods.MouseBtnRight,
                NativeMethods.WM_MBUTTONDOWN => NativeMethods.MouseBtnMiddle,
                NativeMethods.WM_XBUTTONDOWN => ResolveXButton(lParam),
                _ => 0
            };

            if (button != 0 && _onMouseCaptured != null)
            {
                // В режиме захвата не берём Left — это клик по полю настроек / UI
                if (button != NativeMethods.MouseBtnLeft)
                {
                    int mods = NativeMethods.GetCurrentModifiers();
                    var cb = _onMouseCaptured;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => cb(button, mods));
                    return (IntPtr)1;
                }
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            if (!SuppressTrigger && button != 0 && button == _mouseButton)
            {
                int currentMods = NativeMethods.GetCurrentModifiers();
                if (currentMods == _mods && TryFireTrigger())
                    return (IntPtr)1;
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static int ResolveXButton(IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
        int xBtn = (info.mouseData >> 16) & 0xFFFF;
        return xBtn switch
        {
            NativeMethods.XBUTTON1 => NativeMethods.MouseBtnX1,
            NativeMethods.XBUTTON2 => NativeMethods.MouseBtnX2,
            _ => 0
        };
    }

    private bool TryFireTrigger()
    {
        var now = DateTime.UtcNow;
        if (now - _lastTriggerUtc < TriggerCooldown)
            return false;
        _lastTriggerUtc = now;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return false;

        dispatcher.BeginInvoke(TogglePopup);
        return true;
    }

    private void UpdateTrayText()
    {
        if (_tray == null) return;
        var settings = SettingsService.Load();
        var txt = settings.HotkeyText;
        if (string.IsNullOrWhiteSpace(txt)) txt = "Ctrl+Alt+;";
        _tray.Text = $"gitHelper  ({txt})";
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
        ClearBindings();
        _source.Dispose();
        _tray.Dispose();
    }
}
