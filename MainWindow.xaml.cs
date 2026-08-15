using System.Windows;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DatabaseService());
    }

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (sender is FrameworkElement { DataContext: CardViewModel card } && DataContext is MainViewModel viewModel)
            {
                EditCard(card, viewModel);
            }
            e.Handled = true;
            return;
        }

        _dragStartPoint = e.GetPosition(null);
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(null);
        var diff = _dragStartPoint - currentPosition;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: CardViewModel card } element)
        {
            DragDrop.DoDragDrop(element, card, DragDropEffects.Move);
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new AddTaskWindow(viewModel.Columns, viewModel.Projects, viewModel.Goals) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            viewModel.AddCard(dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.Who, dialog.IsRecurring, dialog.RecurrencePattern, dialog.SelectedGoal);
        }
    }

    private void EditCard(CardViewModel card, MainViewModel viewModel)
    {
        var currentColumn = viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (currentColumn is null) return;

        var dialog = new AddTaskWindow(viewModel.Columns, viewModel.Projects, viewModel.Goals, card, currentColumn) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            viewModel.EditCard(card, dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.Who, dialog.IsRecurring, dialog.RecurrencePattern, dialog.SelectedGoal);
        }
    }

    private void ManageProjects_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageProjectsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ManageGoals_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageGoalsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new SettingsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ArchiveDone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ArchiveDoneTasks();
    }

    private void ViewArchived_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ArchivedTasksWindow(viewModel.GetArchivedCards()) { Owner = this };
        dialog.ShowDialog();
    }

    private void SortByProject_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByProject();
    }

    private void SortByDueDate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByDueDate();
    }

    private void DueFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && DataContext is MainViewModel viewModel)
        {
            viewModel.DueFilter = tag;
        }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        DueTodayRadio.IsChecked = false;
        DueTomorrowRadio.IsChecked = false;
        DueWithinWeekRadio.IsChecked = false;
        DueNoDateRadio.IsChecked = false;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearFilters();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ToggleTheme();
    }

    private void EditQuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CardViewModel card } && DataContext is MainViewModel viewModel)
        {
            EditCard(card, viewModel);
        }
    }

    private void QuickMove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element ||
            element.Tag is not string targetColumnName ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var targetColumn = viewModel.Columns.FirstOrDefault(c => c.Name == targetColumnName);
        if (targetColumn is null) return;

        viewModel.MoveCardCommand.Execute((card, targetColumn));
    }

    private void Column_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(CardViewModel)) is CardViewModel card &&
            sender is FrameworkElement { DataContext: ColumnViewModel column } &&
            DataContext is MainViewModel viewModel)
        {
            viewModel.MoveCardCommand.Execute((card, column));
        }
    }
}
