using System.Windows;
using Application = System.Windows.Application;

namespace CatPetStation.App;

/// <summary>
/// CatPetStation lives in the system tray: there is no main window, only pet
/// windows and the tray menu. Closing every pet keeps the app running until
/// the user picks Exit.
/// </summary>
public partial class App : Application
{
    private PetHost? _host;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = new PetHost();
        _tray = new TrayIcon(_host);
        _host.RestoreOrSpawnDefault();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
