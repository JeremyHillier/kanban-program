using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns, IEnumerable<ProjectViewModel> projects)
    {
        InitializeComponent();
        CategoryComboBox.ItemsSource = columns;
        ProjectComboBox.ItemsSource = projects;
        DetailsTextBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var details = DetailsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(details) ||
            CategoryComboBox.SelectedItem is not ColumnViewModel column ||
            ProjectComboBox.SelectedItem is not ProjectViewModel project)
        {
            return;
        }

        TaskDetails = details;
        SelectedColumn = column;
        SelectedProject = project;
        DialogResult = true;
        Close();
    }
}
