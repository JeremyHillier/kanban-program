using System.Windows;
using System.Windows.Input;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// Reviewing Quick Add tasks: the Review (N) button and Alt+Q open the list; the card's Review tag and its right-click
// menu go straight to the task. Review opens the tasks one after another in the full task window with its review strip
// (AddTaskWindow.QuickReview.cs); each step saves the task first, and Reviewed takes it off the list.
public partial class MainWindow
{
    private void ReviewQuickEntries_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) OpenQuickEntries(viewModel);
    }

    private void ReviewTag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(() => ReviewQuickEntries(viewModel, card), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OpenQuickEntries(MainViewModel viewModel)
    {
        var list = new QuickEntriesWindow(viewModel) { Owner = this };
        if (list.ShowDialog() == true && list.StartFrom is { } start) ReviewQuickEntries(viewModel, start);
    }

    private void ReviewQuickEntries(MainViewModel viewModel, CardViewModel start)
    {
        var current = start;
        while (true)
        {
            var queue = viewModel.QuickEntriesToReview();
            var index = queue.IndexOf(current);
            var column = viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(current));
            if (index < 0 || column is null) return;

            var dialog = new AddTaskWindow(viewModel, current, column) { Owner = this };
            dialog.ShowReviewStrip(queue, index);
            if (dialog.ShowDialog() != true) return;

            dialog.SaveTo(viewModel, current);
            if (dialog.MarkDoneRequested && viewModel.DoneColumn is { } done)
            {
                MoveCardDeferred(current, done, viewModel);
                return;
            }

            CardViewModel? next = dialog.ReviewStep switch
            {
                ReviewStep.Previous => index > 0 ? queue[index - 1] : null,
                ReviewStep.Next => index < queue.Count - 1 ? queue[index + 1] : null,
                ReviewStep.Jump => dialog.ReviewJumpTo,
                ReviewStep.Reviewed => index < queue.Count - 1 ? queue[index + 1] : index > 0 ? queue[0] : null,
                _ => null // Save & Close
            };
            if (dialog.ReviewStep == ReviewStep.Reviewed) viewModel.MarkReviewed(current);

            if (next is null)
            {
                if (dialog.ReviewStep == ReviewStep.Reviewed && !viewModel.HasQuickEntriesToReview) ShowStatusMessage("Every quick entry has been reviewed");
                return;
            }
            current = next;
        }
    }
}
