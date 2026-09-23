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
        $"{r.Card.Id}{r.Title}{r.ColumnId}{r.ProjectId}{r.GoalId}{r.WhoId}{r.Priority}{r.DueDate:yyyy-MM-dd}{r.IsImported}"));

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

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = new List<ImportedRowEditViewModel>();

        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.Title))
            {
                MessageBox.Show(this, "Task details cannot be blank. Fix or remove the row before saving.",
                    "Missing Task Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
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

        foreach (var row in toRemove)
        {
            _rows.Remove(row);
        }

        EmptyStateText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Re-baseline so closing straight after a save doesn't claim there's unsaved work. Only
        // reached once every row has been written - the blank-title path returns before this.
        _savedSignature = BuildSignature();

        MessageBox.Show(this, "Changes saved.", "Imported Tasks", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
