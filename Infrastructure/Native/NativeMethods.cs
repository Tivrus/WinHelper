using System.Runtime.InteropServices;

namespace TransparentHotkeyUtility.Infrastructure.Native;

internal static class NativeMethods
{
    internal const int WM_HOTKEY = 0x0312;

    internal const int MOD_ALT     = 0x0001;
    internal const int MOD_CONTROL = 0x0002;
    internal const int MOD_SHIFT   = 0x0004;
    internal const int MOD_WIN     = 0x0008;

    // Low-level hooks
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WH_MOUSE_LL    = 14;

    internal const int WM_KEYDOWN    = 0x0100;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_MBUTTONDOWN = 0x0207;
    internal const int WM_XBUTTONDOWN = 0x020B;

    internal const int XBUTTON1 = 0x0001;
    internal const int XBUTTON2 = 0x0002;

    /// <summary>Левая кнопка мыши.</summary>
    internal const int MouseBtnLeft   = 1;
    /// <summary>Правая кнопка мыши.</summary>
    internal const int MouseBtnRight  = 2;
    /// <summary>Средняя кнопка (колёсико).</summary>
    internal const int MouseBtnMiddle = 3;
    /// <summary>Боковая кнопка (Mouse4 / XButton1).</summary>
    internal const int MouseBtnX1     = 4;
    /// <summary>Боковая кнопка (Mouse5 / XButton2).</summary>
    internal const int MouseBtnX2     = 5;

    internal const int HC_ACTION = 0;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    internal const int VK_SHIFT   = 0x10;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU    = 0x12; // Alt
    internal const int VK_LWIN    = 0x5B;
    internal const int VK_RWIN    = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public int    vkCode;
        public int    scanCode;
        public int    flags;
        public int    time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        public POINT  pt;
        public int    mouseData;
        public int    flags;
        public int    time;
        public IntPtr dwExtraInfo;
    }

    internal static int GetCurrentModifiers()
    {
        int mods = 0;
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= MOD_CONTROL;
        if ((GetAsyncKeyState(VK_MENU)    & 0x8000) != 0) mods |= MOD_ALT;
        if ((GetAsyncKeyState(VK_SHIFT)   & 0x8000) != 0) mods |= MOD_SHIFT;
        if ((GetAsyncKeyState(VK_LWIN)    & 0x8000) != 0 ||
            (GetAsyncKeyState(VK_RWIN)    & 0x8000) != 0) mods |= MOD_WIN;
        return mods;
    }

    internal static string FormatModifiers(int mods)
    {
        string text = "";
        if ((mods & MOD_CONTROL) != 0) text += "Ctrl + ";
        if ((mods & MOD_ALT)     != 0) text += "Alt + ";
        if ((mods & MOD_SHIFT)   != 0) text += "Shift + ";
        if ((mods & MOD_WIN)     != 0) text += "Win + ";
        return text;
    }

    internal static string MouseButtonName(int button) => button switch
    {
        MouseBtnLeft   => "Mouse Left",
        MouseBtnRight  => "Mouse Right",
        MouseBtnMiddle => "Mouse Middle",
        MouseBtnX1     => "Mouse4",
        MouseBtnX2     => "Mouse5",
        _              => $"Mouse{button}"
    };
}
