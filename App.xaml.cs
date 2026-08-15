using System.Windows;
using System.Windows.Threading;
using KanbanApp.Views;

namespace KanbanApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            splash.Close();
        };
        timer.Start();
    }
}
