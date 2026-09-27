using System.Windows;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The automatic check for a newer version: at most once a day, in the background after the board
// is up, and silent unless there is something to offer. See Services/UpdateChecker for what is sent.
public partial class MainWindow
{
    private async void CheckForUpdatesOnStartup(MainViewModel viewModel)
    {
        if (!viewModel.IsUpdateCheckDue(DateTime.Now)) return;

        var update = await UpdateChecker.FetchLatestAsync(viewModel.AppVersion.TrimStart('v'));
        if (update is null || !IsLoaded) return; // couldn't find out: try again next start, say nothing

        viewModel.RecordUpdateCheck(DateTime.Now);
        if (viewModel.ShouldOfferUpdate(update)) ShowUpdateWhenQuiet(viewModel, update);
    }

    // What's New, the reminder list or a task may be open by the time the answer comes back. The
    // notice waits its turn rather than landing on top of them.
    private void ShowUpdateWhenQuiet(MainViewModel viewModel, UpdateInfo update)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            if (!IsLoaded) { timer.Stop(); return; }
            if (OwnedWindows.Count > 0 || WindowState == WindowState.Minimized) return;

            timer.Stop();
            new UpdateAvailableWindow(viewModel, update) { Owner = this }.ShowDialog();
        };
        timer.Start();
    }
}
