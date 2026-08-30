using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp;

public partial class App : Application
{
    private static Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 1 && e.Args[0] == "--seed-data-folder")
        {
            SeedDataFolder(e.Args[1]);
            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(true, $"KanbanTaskBoard-{AppChannel.Name}", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show($"{AppChannel.DisplayName} is already running.", AppChannel.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Must run before any window loads - it works by class handler, so it only affects windows
        // whose Loaded fires after this point.
        Theming.DialogCopyright.Register();

        var db = new DatabaseService();
        CleanUpOldDbFileAfterMove(db.DbPath);

        DispatcherUnhandledException += (_, ex) =>
        {
            LogCrash(db.DbPath, ex.Exception);
            MessageBox.Show(
                "Something went wrong, but the app will stay open. Details were written to crash.log next to your data file.\n\n" + ex.Exception.Message,
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        var showSplash = db.GetSetting("ShowSplash") != "False";
        var splashDelayMs = int.TryParse(db.GetSetting("SplashDelayMs"), out var delay) ? delay : 1800;
        var startFullScreen = db.GetSetting("StartFullScreen") == "True";

        if (!showSplash)
        {
            var main = new MainWindow(db);
            MainWindow = main;
            main.Show();
            if (startFullScreen) main.WindowState = WindowState.Maximized;
            return;
        }

        var splash = new SplashWindow();
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(splashDelayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var main = new MainWindow(db);
            MainWindow = main;
            main.Show();
            if (startFullScreen) main.WindowState = WindowState.Maximized;

            splash.Close();
        };
        timer.Start();
    }

    private static void LogCrash(string dbPath, Exception ex)
    {
        try
        {
            var logPath = Path.Combine(Path.GetDirectoryName(dbPath) ?? Path.GetTempPath(), "crash.log");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n");
        }
        catch
        {
            // Logging is best-effort; never let a failure here mask the original error.
        }
    }

    private static void SeedDataFolder(string dataFolder)
    {
        if (AppConfig.ConfigFileExists()) return; // Never touch an existing configuration (e.g. on an upgrade).

        new AppConfig { DbPath = Path.Combine(dataFolder, "kanban.db") }.Save();
    }

    private static void CleanUpOldDbFileAfterMove(string currentDbPath)
    {
        var config = AppConfig.Load();
        if (string.IsNullOrEmpty(config.PendingCleanupPath)) return;

        if (string.Equals(Path.GetFullPath(config.PendingCleanupPath), Path.GetFullPath(currentDbPath), StringComparison.OrdinalIgnoreCase))
        {
            // Safety guard: never delete the file that's actively in use.
            config.PendingCleanupPath = null;
            config.Save();
            return;
        }

        try
        {
            if (File.Exists(config.PendingCleanupPath))
            {
                File.Delete(config.PendingCleanupPath);
            }

            var oldAttachmentsDir = DatabaseService.GetAttachmentsDir(config.PendingCleanupPath);
            var currentAttachmentsDir = DatabaseService.GetAttachmentsDir(currentDbPath);
            if (!string.Equals(Path.GetFullPath(oldAttachmentsDir), Path.GetFullPath(currentAttachmentsDir), StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(oldAttachmentsDir))
            {
                Directory.Delete(oldAttachmentsDir, recursive: true);
            }

            config.PendingCleanupPath = null;
            config.Save();
        }
        catch
        {
            // Leave PendingCleanupPath set so cleanup is retried on the next startup.
        }
    }
}
