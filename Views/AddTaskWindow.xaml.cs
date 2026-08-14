using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }
    public GoalViewModel? SelectedGoal { get; private set; }
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public string? Who { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurrencePattern { get; private set; }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns, IEnumerable<ProjectViewModel> projects, IEnumerable<GoalViewModel> goals)
    {
        InitializeComponent();
        CategoryComboBox.ItemsSource = columns;
        ProjectComboBox.ItemsSource = projects;
        GoalComboBox.ItemsSource = goals;
        DetailsTextBox.Focus();
    }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns, IEnumerable<ProjectViewModel> projects, IEnumerable<GoalViewModel> goals,
        CardViewModel cardToEdit, ColumnViewModel currentColumn) : this(columns, projects, goals)
    {
        Title = "Edit Task";
        SubmitButton.Content = "Save";

        DetailsTextBox.Text = cardToEdit.Title;
        CategoryComboBox.SelectedItem = currentColumn;
        ProjectComboBox.SelectedItem = ProjectComboBox.Items.OfType<ProjectViewModel>()
            .FirstOrDefault(p => p.Id == cardToEdit.ProjectId);
        GoalComboBox.SelectedItem = GoalComboBox.Items.OfType<GoalViewModel>()
            .FirstOrDefault(g => g.Id == cardToEdit.GoalId);

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

        RecurringCheckBox.IsChecked = cardToEdit.IsRecurring;
        RecurrenceComboBox.Visibility = cardToEdit.IsRecurring ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in RecurrenceComboBox.Items.OfType<ComboBoxItem>())
        {
            if ((string)item.Content == cardToEdit.RecurrencePattern)
            {
                RecurrenceComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void ClearDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = null;
    }

    private void RecurringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RecurrenceComboBox.Visibility = RecurringCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
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
        SelectedGoal = GoalComboBox.SelectedItem as GoalViewModel;
        SelectedPriority = (string)priorityItem.Content;
        SelectedDueDate = DueDatePicker.SelectedDate;
        Who = string.IsNullOrWhiteSpace(WhoTextBox.Text) ? null : WhoTextBox.Text.Trim();

        IsRecurring = RecurringCheckBox.IsChecked == true;
        RecurrencePattern = IsRecurring && RecurrenceComboBox.SelectedItem is ComboBoxItem recurrenceItem
            ? (string)recurrenceItem.Content
            : null;

        DialogResult = true;
        Close();
    }
}
