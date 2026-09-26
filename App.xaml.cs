using System.IO;
using System.Windows;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp;

public partial class App : Application
{
    // Never read, and deliberately so: this holds the single-instance mutex alive for the life of
    // the process. Dropping it because it "looks unused" would let the GC collect the mutex and
    // silently break the guard that stops two copies writing to the same database.
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
            Dialogs.Tell(null, "Already Open", $"{AppChannel.DisplayName} is already open.\n\nLook for it on the taskbar.");
            Shutdown();
            return;
        }

        // Must run before any window loads - it works by class handler, so it only affects windows
        // whose Loaded fires after this point.
        Theming.DialogCopyright.Register();

        var db = new DatabaseService();
        if (db.IsFromNewerApp && !ConfirmOpenNewerTaskFile(db))
        {
            Shutdown();
            return;
        }

        CleanUpOldDbFileAfterMove(db.DbPath);

        DispatcherUnhandledException += (_, ex) =>
        {
            LogCrash(db.DbPath, ex.Exception);
            ex.Handled = true;
            const string text = "Something went wrong, but the app is still open and your tasks are safe.\n\n"
                                + "Would you like to email a problem report? You see the email before anything is sent.";
            bool report;
            try
            {
                report = Dialogs.Confirm(null, new DialogMessage("Something Went Wrong", text)
                {
                    Tone = DialogTone.Warning, Detail = ex.Exception.Message, Yes = "Email a Report", No = "Not Now",
                });
            }
            catch (Exception)
            {
                // The app's own window may be what is failing: the plain Windows message box still asks.
                report = MessageBox.Show(text + "\n\n" + ex.Exception.Message, "Something Went Wrong",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            if (!report) return;

            try
            {
                ProblemReport.Compose(MainWindow is { IsLoaded: true } owner ? owner : null, db.DbPath);
            }
            catch (Exception reportEx)
            {
                // Never let the report itself raise another unexpected-error prompt.
                LogCrash(db.DbPath, reportEx);
            }
        };

        var showSplash = db.GetFlag("ShowSplash", true);
        var splashDelayMs = db.GetInt("SplashDelayMs", 1800);
        var startFullScreen = db.GetFlag("StartFullScreen", false);

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

    // A newer copy of the app has used this task file (another PC, or a downgrade). This copy
    // doesn't know about whatever that one stores, so it says so before anything is edited. The
    // safe answer, closing, is the default.
    private static bool ConfirmOpenNewerTaskFile(DatabaseService db) =>
        Dialogs.Confirm(null, DialogMessage.AskDanger("Newer Task File",
            NewerTaskFileMessage(db.NewerAppVersion, DatabaseService.RunningAppVersion), "Open It Anyway", "Close the App"));

    internal static string NewerTaskFileMessage(string? newerVersion, string thisVersion) =>
        $"This task file was last used by a newer version of the app{(string.IsNullOrWhiteSpace(newerVersion) ? "" : $" ({newerVersion} or later)")}.\n\n" +
        $"This PC is running version {thisVersion}. If you carry on, tasks you change here can lose details that only the newer version knows about.\n\n" +
        "It is safer to close now and update this PC first, from the download page. Open the task file anyway?";

    private static void LogCrash(string dbPath, Exception ex)
    {
        try
        {
            File.AppendAllText(ProblemReport.CrashLogPath(dbPath), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n");
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
