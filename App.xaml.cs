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
}
