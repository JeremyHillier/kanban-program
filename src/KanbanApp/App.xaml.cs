using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        // Nothing ends the app until it says so. A message or the splash may be the first window up,
        // and WPF would otherwise make it the main window and close the app with it.
        // OnMainWindowClose is set once the board is showing.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Tooltips stay up long enough to read a few lines; WPF's own 5 seconds hides the longer ones
        // mid-sentence. One value for every control, so none needs its own.
        ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(20000));

        if (e.Args.Length > 1 && e.Args[0] == "--seed-data-folder")
        {
            SeedDataFolder(e.Args[1]);
            Shutdown();
            return;
        }

        // A task link clicked in Outlook or an email: Windows starts this copy with the link as its argument.
        var taskLink = e.Args.FirstOrDefault(TaskLinks.IsLink);

        _instanceMutex = new Mutex(true, $"KanbanTaskBoard-{AppChannel.Name}", out var createdNew);
        if (!createdNew)
        {
            if (taskLink is not null && TaskLinkForwarder.TrySend(taskLink))
            {
                Shutdown(); // the open copy has it
                return;
            }

            Dialogs.Tell(null, "Already Open", $"{AppChannel.DisplayName} is already open.\n\nLook for it on the taskbar.");
            Shutdown();
            return;
        }

        // Must run before any window loads - it works by class handler, so it only affects windows
        // whose Loaded fires after this point.
        Theming.DialogCopyright.Register();
        Theming.WindowFit.Register(); // every window stays inside its screen, buttons and all
        Views.HelpKey.Register(); // F1 opens Help from any window, at the section about it

        TaskLinks.Register(); // so a link to a task opens this copy

        var db = new DatabaseService();
        if (db.IsFromNewerApp && !ConfirmOpenNewerTaskFile(db))
        {
            Shutdown();
            return;
        }

        GiveEveryTaskAnId(db);

        if (db.ForeignKeyUpgradeProblem is { } upgradeProblem)
        {
            Dialogs.Tell(null, "Task File Not Updated", upgradeProblem, DialogTone.Warning);
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
            ShowBoard(db, startFullScreen, taskLink);
            return;
        }

        var splash = new SplashWindow();
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(splashDelayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            ShowBoard(db, startFullScreen, taskLink);

            splash.Close();
        };
        timer.Start();
    }

    // The board becomes the main window, and closing it from now on ends the app.
    private void ShowBoard(DatabaseService db, bool startFullScreen, string? taskLink)
    {
        var main = new MainWindow(db);
        MainWindow = main;
        main.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        if (startFullScreen) main.WindowState = WindowState.Maximized;
        OpenLinkWhenReady(main, taskLink);
    }

    // The link that started this copy opens its task once the board is up and idle.
    private static void OpenLinkWhenReady(MainWindow main, string? taskLink)
    {
        if (taskLink is null) return;
        main.Dispatcher.BeginInvoke(() => main.OpenTaskLink(taskLink), DispatcherPriority.ApplicationIdle);
    }

    // A task file from before every task had a permanent ID is given them all, once, after a backup
    // (the ID is what a link to a task is made of). With no backup the IDs are simply made as they are
    // needed, so nothing is changed that could not be put back.
    private static void GiveEveryTaskAnId(DatabaseService db)
    {
        try
        {
            if (db.CountTasksWithoutId() == 0) return;

            BackupService.CreateBackupBeforeUpdate(db.DbPath);
            db.EnsureEveryTaskHasAnId();
        }
        catch
        {
            // Not worth stopping the app for: a task gets its ID the first time one is asked of it.
        }
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
