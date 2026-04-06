using System.Runtime.InteropServices;

namespace TransparentHotkeyUtility.Native;

internal static class NativeMethods
{
    internal const int WM_HOTKEY  = 0x0312;
    internal const int MOD_ALT    = 0x0001;
    internal const int MOD_CONTROL = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
