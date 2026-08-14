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

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProjectViewModel project }) return;

        _viewModel.DeleteProject(project);
    }
}
