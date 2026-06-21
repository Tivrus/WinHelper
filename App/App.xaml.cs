using System.Windows;
using Application    = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs    = System.Windows.ExitEventArgs;
using TransparentHotkeyUtility.Services;

namespace TransparentHotkeyUtility;

public partial class App : Application
{
    private HotkeyHost? _host;

    internal HotkeyHost Host => _host!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FigureConfigService.EnsureConfigFileExists();
        _host = new HotkeyHost();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
