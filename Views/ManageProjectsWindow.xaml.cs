using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageProjectsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageProjectsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
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

        _viewModel.RenameProject(project, textBox.Text);
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
            var result = MessageBox.Show(this, $"Delete project \"{project.Name}\"? {impact} This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        _viewModel.DeleteProject(project);
    }
}
