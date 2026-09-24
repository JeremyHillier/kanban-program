using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// What the board shows unasked: What's New after an update, the due-task reminders at start-up,
// and the time alerts while it runs.
public partial class MainWindow
{
    private void ShowWhatsNew(MainViewModel viewModel)
    {
        var dialog = new WhatsNewWindow(viewModel) { Owner = this };
        dialog.ShowDialog();

        // Recorded even if the user turns the screen off from inside it, so switching it back on
        // later doesn't immediately re-show notes they've already read.
        viewModel.MarkWhatsNewSeen();
    }

    private void ShowReminders(List<CardViewModel> dueCards, MainViewModel viewModel)
    {
        var dialog = new ReminderWindow(dueCards, viewModel.Columns, card => EditCard(card, viewModel), card => MarkCardDone(card, viewModel),
            card => viewModel.GetDueReminders().Contains(card)) { Owner = this };
        dialog.ShowDialog();
    }

    private void DueTimeTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.RefreshIfDayChanged();

        var newlyDue = _timeAlerts.TakeCardsToAlert(viewModel.GetCardsPastDueTime(), DateTime.Now);

        // Still recorded as announced while alerts are switched off, so turning them back on later
        // doesn't dump every time that passed in the meantime.
        if (newlyDue.Count == 0 || !viewModel.ShowTimeAlerts) return;

        ShowTimeAlert(newlyDue, viewModel);
    }

    // Non-modal and not owned by the board, so it still surfaces when the board is minimised or
    // behind another app; the Topmost flip brings it to the front once without pinning it there.
    private void ShowTimeAlert(List<CardViewModel> dueCards, MainViewModel viewModel)
    {
        var alert = new ReminderWindow(dueCards, viewModel.Columns, card => EditCard(card, viewModel), card => MarkCardDone(card, viewModel),
            card => viewModel.GetCardsPastDueTime().Contains(card), isTimeAlert: true,
            onSnooze: (cards, duration) => _timeAlerts.Snooze(cards, DateTime.Now + duration))
        {
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };

        System.Media.SystemSounds.Exclamation.Play();
        alert.Show();
        alert.Activate();
        alert.Topmost = false;
    }

    private void MarkCardDone(CardViewModel card, MainViewModel viewModel)
    {
        var doneColumn = viewModel.Columns.FirstOrDefault(c => c.Name == "Done");
        if (doneColumn is null) return;

        viewModel.MoveCardCommand.Execute((card, doneColumn));
        MaybePromptCompletionNote(card, doneColumn, viewModel);
    }

    private void Reminders_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dueCards = viewModel.GetDueReminders();
        if (dueCards.Count == 0)
        {
            MessageBox.Show(this, "No overdue or due-today tasks.", "Task Reminders", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowReminders(dueCards, viewModel);
    }
}
