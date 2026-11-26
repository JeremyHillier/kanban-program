using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ImportedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<ImportedRowEditViewModel> _rows;

    // The grid's contents as last saved (or as first loaded), so Close can tell whether there are
    // edits to lose. Unlike the task dialog this window stays open after saving, so it's re-taken
    // on every successful save rather than only once.
    private string _savedSignature;

    public ImportedTasksWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width * 0.95);
        _viewModel = viewModel;
        DataContext = viewModel;

        _rows = new ObservableCollection<ImportedRowEditViewModel>(
            viewModel.GetImportedCards().Select(c => new ImportedRowEditViewModel(c, viewModel)));
        RowsList.ItemsSource = _rows;
        EmptyStateText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _savedSignature = BuildSignature();
    }

    private string BuildSignature() => string.Join("|", _rows.Select(r =>
        $"{r.Card.Id}{r.Title}{r.ColumnId}{r.ProjectId}{r.GoalId}{r.WhoId}{r.Priority}{r.DueDate:yyyy-MM-dd}{r.IsImported}{r.IsDeleting}"));

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || BuildSignature() == _savedSignature) return;

        if (!UnsavedChangesGuard.ConfirmDiscard(this))
        {
            e.Cancel = true;
        }
    }

    private void DatePicker_Loaded(object sender, RoutedEventArgs e) => CalendarWheelSupport.Attach((DatePicker)sender);

    // The rows lose width to the vertical scrollbar whenever there are enough of them to scroll, so
    // the header gives up the same width on its right to keep its columns over the right cells.
    private void RowsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollBarWidth = Math.Max(0, RowsScrollViewer.ActualWidth - RowsScrollViewer.ViewportWidth);
        HeaderGrid.Margin = new Thickness(1, 10, 1 + scrollBarWidth, 4);
    }

    // Delete on a row marks it (and Keep unmarks it); nothing is deleted until the list is saved.
    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ImportedRowEditViewModel row) row.IsDeleting = !row.IsDeleting;
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        if (SaveRows() is { } deleted)
            Dialogs.Tell(this, "Imported Tasks", deleted == 0 ? "Changes saved."
                : $"Changes saved. {deleted} task{(deleted == 1 ? " was" : "s were")} deleted and can be found in Deleted Tasks.");
    }

    // Writes the list to the board: the edits, the Imported ticks, and the tasks marked Delete (one Undo step, to Deleted Tasks).
    // Returns how many were deleted, or null when a row with no task details stopped the save.
    internal int? SaveRows()
    {
        var toRemove = new List<ImportedRowEditViewModel>();
        var deleting = _rows.Where(r => r.IsDeleting).ToList();

        foreach (var row in _rows.Except(deleting))
        {
            if (string.IsNullOrWhiteSpace(row.Title))
            {
                Dialogs.Tell(this, "Missing Task Details", "A row has no task details, so nothing was saved.\n\nFill it in, or click Delete on its row, then save again.", DialogTone.Warning);
                return null;
            }

            var column = _viewModel.Columns.FirstOrDefault(c => c.Id == row.ColumnId);
            if (column is null) continue;

            var project = row.ProjectId is null ? null : _viewModel.Projects.FirstOrDefault(p => p.Id == row.ProjectId);
            var goal = row.GoalId is null ? null : _viewModel.Goals.FirstOrDefault(g => g.Id == row.GoalId);
            // This screen edits one person per row - the lead. Left alone, everyone stays. Changed,
            // the new person takes the lead's place and the others stay; cleared, nobody is assigned.
            var who = row.WhoId is null ? null : _viewModel.People.FirstOrDefault(p => p.Id == row.WhoId);
            IReadOnlyList<PersonViewModel> people = row.WhoId == row.Card.WhoId ? row.Card.People
                : who is null ? []
                : [who, .. row.Card.People.Skip(1).Where(p => p.Id != who.Id)];

            _viewModel.EditCard(row.Card, row.Title, column, project, row.Priority, row.DueDate, people,
                row.Card.IsRecurring, row.Card.RecurrencePattern, goal, row.Card.Flags, row.Card.SubTasks, row.Card.Notes,
                attachments: row.Card.Attachments, forceEditOnComplete: row.Card.ForceEditOnComplete,
                websiteUrl: row.Card.WebsiteUrl, dueTime: row.Card.DueTime, startDate: row.Card.StartDate,
                waitingOn: row.Card.WaitingOn, recurrencesLeft: row.Card.RecurrencesLeft);

            _viewModel.SetCardImported(row.Card, row.IsImported);
            if (!row.IsImported) toRemove.Add(row);
        }

        if (deleting.Count > 0) _viewModel.DeleteCards(deleting.Select(r => r.Card));

        foreach (var row in toRemove.Concat(deleting))
        {
            _rows.Remove(row);
        }

        EmptyStateText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Re-baseline so closing straight after a save doesn't claim there's unsaved work. Only
        // reached once every row has been written - the blank-title path returns before this.
        _savedSignature = BuildSignature();
        return deleting.Count;
    }
}
