using System.IO;
using System.Windows;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var db = new DatabaseService();
        CleanUpOldDbFileAfterMove(db.DbPath);

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
