using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public string? Who { get; private set; }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns, IEnumerable<ProjectViewModel> projects)
    {
        InitializeComponent();
        CategoryComboBox.ItemsSource = columns;
        ProjectComboBox.ItemsSource = projects;
        DetailsTextBox.Focus();
    }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns, IEnumerable<ProjectViewModel> projects,
        CardViewModel cardToEdit, ColumnViewModel currentColumn) : this(columns, projects)
    {
        Title = "Edit Task";
        SubmitButton.Content = "Save";

        DetailsTextBox.Text = cardToEdit.Title;
        CategoryComboBox.SelectedItem = currentColumn;
        ProjectComboBox.SelectedItem = ProjectComboBox.Items.OfType<ProjectViewModel>()
            .FirstOrDefault(p => p.Id == cardToEdit.ProjectId);

        foreach (var item in PriorityComboBox.Items.OfType<ComboBoxItem>())
        {
            if ((string)item.Content == cardToEdit.Priority)
            {
                PriorityComboBox.SelectedItem = item;
                break;
            }
        }

        DueDatePicker.SelectedDate = cardToEdit.DueDate;
        WhoTextBox.Text = cardToEdit.Who ?? string.Empty;
    }

    private void ClearDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = null;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var details = DetailsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(details) ||
            CategoryComboBox.SelectedItem is not ColumnViewModel column ||
            ProjectComboBox.SelectedItem is not ProjectViewModel project ||
            PriorityComboBox.SelectedItem is not ComboBoxItem priorityItem)
        {
            return;
        }

        TaskDetails = details;
        SelectedColumn = column;
        SelectedProject = project;
        SelectedPriority = (string)priorityItem.Content;
        SelectedDueDate = DueDatePicker.SelectedDate;
        Who = string.IsNullOrWhiteSpace(WhoTextBox.Text) ? null : WhoTextBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
