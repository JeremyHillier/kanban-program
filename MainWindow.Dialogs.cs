using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The windows the buttons open: managing lists and templates, settings, help, reports,
// the timeline, archived and deleted tasks, and importing.
public partial class MainWindow
{
    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        OpenAddTaskDialog(viewModel, null);
    }

    // Right-click New Task: start straight from a template, or get to Manage Templates. Built on
    // each open so it always lists the templates as they are now.
    private void NewTaskButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        AddMenuItem(menu, "_New Task", () => OpenAddTaskDialog(viewModel, null), gesture: "Ctrl+N");
        menu.Items.Add(new System.Windows.Controls.Separator());

        if (viewModel.TaskTemplates.Count == 0)
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "No templates yet", IsEnabled = false });
        }
        foreach (var template in viewModel.TaskTemplates)
        {
            AddMenuItem(menu, $"From template: {MenuText(template.Name)}", () => OpenAddTaskDialog(viewModel, null, template));
        }

        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "_Manage Templates...", () => ManageTemplates(viewModel), gesture: "Alt+M");
        menu.IsOpen = true;
    }

    private void ManageTemplates(MainViewModel viewModel) => new ManageTemplatesWindow(viewModel) { Owner = this }.ShowDialog();

    private void ManageWaitingOn(MainViewModel viewModel) => new ManageWaitingOnWindow(viewModel) { Owner = this }.ShowDialog();

    // Right-click the Waiting On button: the filter it normally applies, or the list behind the suggestions.
    private void WaitingOnButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        AddMenuItem(menu, "_Show Waiting Tasks", () => WaitingOn_Click(button, new RoutedEventArgs()));
        AddMenuItem(menu, "_Manage Waiting On List...", () => ManageWaitingOn(viewModel));
        menu.IsOpen = true;
    }

    private void ManageCustomFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageCustomFiltersWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
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

    private void ManageFlags_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageFlagsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ManageWho_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageWhoWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    // The coloured label boxes beside the Project / Who / Goal / Flag filters double as shortcuts
    // to the matching Manage dialog, the same ones the big Manage buttons open.
    private void ManageProjectsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageProjects_Click(sender, e);

    private void ManageWhoLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageWho_Click(sender, e);

    private void ManageGoalsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageGoals_Click(sender, e);

    private void ManageFlagsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageFlags_Click(sender, e);

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new SettingsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new HelpWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void QuickReport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        new QuickReportWindow(viewModel) { Owner = this }.ShowDialog();
    }

    private void ReportBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ReportBuilderWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Timeline_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new TimelineWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ArchiveDone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        if (viewModel.ConfirmArchive)
        {
            var result = MessageBox.Show(this, "Archive all tasks in the Done column?\n\nThey'll be removed from the board but not deleted.",
                "Confirm Archive", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        viewModel.ArchiveDoneTasks();
    }

    private DispatcherTimer? _viewArchivedClickTimer;

    private void ViewArchived_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            _viewArchivedClickTimer?.Stop();
            OpenDeletedTasks();
            return;
        }

        _viewArchivedClickTimer?.Stop();
        _viewArchivedClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _viewArchivedClickTimer.Tick += (_, _) =>
        {
            _viewArchivedClickTimer!.Stop();
            OpenArchivedTasks();
        };
        _viewArchivedClickTimer.Start();
    }

    private void OpenArchivedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ArchivedTasksWindow(viewModel, viewModel.GetArchivedCards()) { Owner = this };
        dialog.ShowDialog();
    }

    private void OpenDeletedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new DeletedTasksWindow(viewModel, viewModel.GetDeletedCards()) { Owner = this };
        dialog.ShowDialog();
    }

    private DispatcherTimer? _importTasksClickTimer;

    private void ImportTasks_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            _importTasksClickTimer?.Stop();
            OpenImportedTasks();
            return;
        }

        _importTasksClickTimer?.Stop();
        _importTasksClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _importTasksClickTimer.Tick += (_, _) =>
        {
            _importTasksClickTimer!.Stop();
            OpenImportTasks();
        };
        _importTasksClickTimer.Start();
    }

    private void OpenImportTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ImportTasksWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void OpenImportedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ImportedTasksWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
