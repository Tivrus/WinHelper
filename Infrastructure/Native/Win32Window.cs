using System.Windows.Forms;

namespace TransparentHotkeyUtility.Infrastructure.Native;

/// <summary>
/// Минимальная обёртка над Win32 HWND для передачи в диалоги WinForms
/// как владелец окна.
/// </summary>
internal sealed class Win32Window(IntPtr handle) : IWin32Window
{
    public IntPtr Handle { get; } = handle;
}
