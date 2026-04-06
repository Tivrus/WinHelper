using System.Windows;
using Application    = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs    = System.Windows.ExitEventArgs;

namespace TransparentHotkeyUtility;

public partial class App : Application
{
    private HotkeyHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = new HotkeyHost();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
