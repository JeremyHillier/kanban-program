using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageProjectsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageProjectsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProjectTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _viewModel.AddProject(name);
        NewProjectTextBox.Clear();
        NewProjectTextBox.Focus();
    }

    private void ProjectName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: ProjectViewModel project } textBox) return;

        // Deferred via BeginInvoke: RenameProject re-sorts the list (Remove+Insert, not Move), which
        // tears down this very TextBox's row while its own LostFocus event is still dispatching —
        // the same WPF deadlock documented on the board's quick-edit popups.
        var newName = textBox.Text;
        Dispatcher.BeginInvoke(new Action(() => _viewModel.RenameProject(project, newName)), DispatcherPriority.Background);
    }

    private void Active_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: ProjectViewModel project } checkBox) return;

        _viewModel.SetProjectActive(project, checkBox.IsChecked == true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProjectViewModel project }) return;

        if (_viewModel.ConfirmDelete)
        {
            var count = _viewModel.CountTasksUsingProject(project);
            var impact = count == 0 ? "No tasks currently use it." : $"{count} task{(count == 1 ? "" : "s")} currently use it — they'll show as having no project.";
            var result = MessageBox.Show(this, $"Delete project \"{project.Name}\"?\n\n{impact}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as the rename above — removing the row tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => _viewModel.DeleteProject(project)), DispatcherPriority.Background);
    }
}
