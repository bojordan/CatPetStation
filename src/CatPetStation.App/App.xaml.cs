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

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _host = new PetHost();
        _tray = new TrayIcon(_host);
        _host.RestoreOrSpawnDefault();
    }

    /// <summary>
    /// A pet app should degrade, not detonate: unexpected UI-thread exceptions
    /// are written to a crash log the user can attach to a bug report, and the
    /// app keeps running.
    /// </summary>
    private void OnDispatcherUnhandledException(
        object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppSettings.DataDirectory);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppSettings.DataDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must never cause a second failure.
        }
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
