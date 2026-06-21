using System.Runtime.InteropServices;

namespace TransparentHotkeyUtility.Infrastructure.Native;

internal static class NativeMethods
{
    internal const int WM_HOTKEY  = 0x0312;
    internal const int MOD_ALT    = 0x0001;
    internal const int MOD_CONTROL = 0x0002;
    internal const int MOD_SHIFT   = 0x0004;
    internal const int MOD_WIN     = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
