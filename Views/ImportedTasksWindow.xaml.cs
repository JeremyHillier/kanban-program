using System.Collections.ObjectModel;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ImportedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<ImportedRowEditViewModel> _rows;

    public ImportedTasksWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _rows = new ObservableCollection<ImportedRowEditViewModel>(
            viewModel.GetImportedCards().Select(c => new ImportedRowEditViewModel(c, viewModel)));
        RowsList.ItemsSource = _rows;
        EmptyStateText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

            _viewModel.EditCard(row.Card, row.Title, column, project, row.Priority, row.DueDate, row.Who,
                row.Card.IsRecurring, row.Card.RecurrencePattern, goal, row.Card.Flags, row.Card.SubTasks, row.Card.Notes,
                attachments: row.Card.Attachments);

            _viewModel.SetCardImported(row.Card, row.IsImported);
            if (!row.IsImported) toRemove.Add(row);
        }

        foreach (var row in toRemove)
        {
            _rows.Remove(row);
        }

        EmptyStateText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MessageBox.Show(this, "Changes saved.", "Imported Tasks", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
