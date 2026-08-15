using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    private readonly MainViewModel _viewModel;

    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }
    public GoalViewModel? SelectedGoal { get; private set; }
    public List<FlagViewModel> SelectedFlags { get; private set; } = [];
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public string? Who { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurrencePattern { get; private set; }

    public AddTaskWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        CategoryComboBox.ItemsSource = viewModel.Columns;
        ProjectComboBox.ItemsSource = viewModel.Projects;
        GoalComboBox.ItemsSource = viewModel.Goals;
        RebuildFlagCheckboxes();

        DetailsTextBox.Focus();
    }

    public AddTaskWindow(MainViewModel viewModel, CardViewModel cardToEdit, ColumnViewModel currentColumn) : this(viewModel)
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

        var assignedIds = cardToEdit.Flags.Select(f => f.Id).ToHashSet();
        foreach (var checkBox in FlagsPanel.Children.OfType<CheckBox>())
        {
            if (checkBox.Tag is FlagViewModel flag && assignedIds.Contains(flag.Id))
            {
                checkBox.IsChecked = true;
            }
        }
    }

    private void RebuildFlagCheckboxes(int? autoCheckFlagId = null)
    {
        var checkedIds = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => ((FlagViewModel)cb.Tag).Id)
            .ToHashSet();

        FlagsPanel.Children.Clear();
        foreach (var flag in _viewModel.Flags)
        {
            FlagsPanel.Children.Add(new CheckBox
            {
                Content = flag.Name,
                Tag = flag,
                IsChecked = checkedIds.Contains(flag.Id) || flag.Id == autoCheckFlagId,
                Margin = new Thickness(0, 0, 16, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            });
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Project", "Project name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddProject(dialog.Value);
        ProjectComboBox.SelectedItem = _viewModel.Projects.LastOrDefault();
    }

    private void NewGoal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Goal", "Goal name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddGoal(dialog.Value);
        GoalComboBox.SelectedItem = _viewModel.Goals.LastOrDefault();
    }

    private void NewFlag_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Flag", "Flag name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddFlag(dialog.Value);
        RebuildFlagCheckboxes(_viewModel.Flags.LastOrDefault()?.Id);
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

        SelectedFlags = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (FlagViewModel)cb.Tag)
            .ToList();

        DialogResult = true;
        Close();
    }
}
