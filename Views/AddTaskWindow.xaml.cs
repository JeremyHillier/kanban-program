using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanApp.Models;
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
    public List<SubTaskViewModel> SelectedSubTasks { get; private set; } = [];
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public string? Who { get; private set; }
    public string? Notes { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurrencePattern { get; private set; }

    public AddTaskWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        CategoryComboBox.ItemsSource = viewModel.Columns;
        RebuildProjectItems();
        RebuildGoalItems();
        RebuildFlagCheckboxes();
        UpdateSubTaskProgressLabel();

        DetailsTextBox.Focus();
    }

    public void PreselectColumn(ColumnViewModel column)
    {
        CategoryComboBox.SelectedItem = column;
    }

    public void FocusNotesField()
    {
        Loaded += (_, _) =>
        {
            NotesTextBox.Focus();
            NotesTextBox.CaretIndex = NotesTextBox.Text.Length;
        };
    }

    public AddTaskWindow(MainViewModel viewModel, CardViewModel cardToEdit, ColumnViewModel currentColumn) : this(viewModel)
    {
        Title = "Edit Task";
        SubmitButton.Content = "Save";

        DetailsTextBox.Text = cardToEdit.Title;
        CategoryComboBox.SelectedItem = currentColumn;
        RebuildProjectItems(_viewModel.Projects.FirstOrDefault(p => p.Id == cardToEdit.ProjectId));
        RebuildGoalItems(_viewModel.Goals.FirstOrDefault(g => g.Id == cardToEdit.GoalId));

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
        NotesTextBox.Text = cardToEdit.Notes ?? string.Empty;

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

        RebuildFlagCheckboxes(forceCheckedIds: cardToEdit.Flags.Select(f => f.Id));

        foreach (var subTask in cardToEdit.SubTasks)
        {
            AddSubTaskRow(subTask.Title, subTask.IsDone);
        }
        UpdateSubTaskProgressLabel();
    }

    private void RebuildProjectItems(ProjectViewModel? autoSelect = null)
    {
        var items = _viewModel.Projects.Where(p => p.IsActive).ToList();
        if (autoSelect is not null && !items.Any(p => p.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        ProjectComboBox.ItemsSource = items;
        ProjectComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(p => p.Id == autoSelect.Id);
    }

    private void RebuildGoalItems(GoalViewModel? autoSelect = null)
    {
        var items = _viewModel.Goals.Where(g => g.IsActive).ToList();
        if (autoSelect is not null && !items.Any(g => g.Id == autoSelect.Id))
        {
            items.Insert(0, autoSelect);
        }

        GoalComboBox.ItemsSource = items;
        GoalComboBox.SelectedItem = autoSelect is null
            ? null
            : items.FirstOrDefault(g => g.Id == autoSelect.Id);
    }

    private void RebuildFlagCheckboxes(int? autoCheckFlagId = null, IEnumerable<int>? forceCheckedIds = null)
    {
        var checkedIds = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => ((FlagViewModel)cb.Tag).Id)
            .ToHashSet();

        if (forceCheckedIds is not null)
        {
            checkedIds.UnionWith(forceCheckedIds);
        }

        FlagsPanel.Children.Clear();
        foreach (var flag in _viewModel.Flags.Where(f => f.IsActive || checkedIds.Contains(f.Id)))
        {
            FlagsPanel.Children.Add(new CheckBox
            {
                Content = flag.Name,
                Tag = flag,
                IsChecked = checkedIds.Contains(flag.Id) || flag.Id == autoCheckFlagId,
                Margin = new Thickness(0, 0, 16, 8),
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            });
        }
    }

    private void AddSubTaskRow(string title = "", bool isDone = false)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox
        {
            IsChecked = isDone,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        checkBox.Checked += (_, _) => UpdateSubTaskProgressLabel();
        checkBox.Unchecked += (_, _) => UpdateSubTaskProgressLabel();
        Grid.SetColumn(checkBox, 0);

        var textBox = new TextBox
        {
            Text = title,
            Padding = new Thickness(6),
            Background = (Brush)FindResource("InputBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)FindResource("CardBorderBrush")
        };
        Grid.SetColumn(textBox, 1);

        var deleteButton = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(6, 0, 0, 0),
            Background = (Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            ToolTip = "Remove sub-task"
        };
        Grid.SetColumn(deleteButton, 2);
        deleteButton.Click += (_, _) =>
        {
            SubTasksPanel.Children.Remove(row);
            UpdateSubTaskProgressLabel();
        };

        row.Children.Add(checkBox);
        row.Children.Add(textBox);
        row.Children.Add(deleteButton);
        SubTasksPanel.Children.Add(row);

        if (string.IsNullOrEmpty(title))
        {
            textBox.Focus();
        }

        UpdateSubTaskProgressLabel();
    }

    private void UpdateSubTaskProgressLabel()
    {
        var checkBoxes = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => (CheckBox)row.Children[0])
            .ToList();

        if (checkBoxes.Count == 0)
        {
            SubTaskProgressLabel.Text = string.Empty;
            return;
        }

        var done = checkBoxes.Count(cb => cb.IsChecked == true);
        SubTaskProgressLabel.Text = $"{done}/{checkBoxes.Count} ({done * 100 / checkBoxes.Count}%)";
    }

    private void AddSubTaskRow_Click(object sender, RoutedEventArgs e)
    {
        AddSubTaskRow();
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Project", "Project name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddProject(dialog.Value);
        RebuildProjectItems(_viewModel.Projects.LastOrDefault());
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        ProjectComboBox.SelectedIndex = -1;
    }

    private void NewGoal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("New Goal", "Goal name") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _viewModel.AddGoal(dialog.Value);
        RebuildGoalItems(_viewModel.Goals.LastOrDefault());
    }

    private void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        GoalComboBox.SelectedIndex = -1;
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
            PriorityComboBox.SelectedItem is not ComboBoxItem priorityItem)
        {
            return;
        }

        TaskDetails = details;
        SelectedColumn = column;
        SelectedProject = ProjectComboBox.SelectedItem as ProjectViewModel;
        SelectedGoal = GoalComboBox.SelectedItem as GoalViewModel;
        SelectedPriority = (string)priorityItem.Content;
        SelectedDueDate = DueDatePicker.SelectedDate;
        Who = string.IsNullOrWhiteSpace(WhoTextBox.Text) ? null : WhoTextBox.Text.Trim();
        Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();

        IsRecurring = RecurringCheckBox.IsChecked == true;
        RecurrencePattern = IsRecurring && RecurrenceComboBox.SelectedItem is ComboBoxItem recurrenceItem
            ? (string)recurrenceItem.Content
            : null;

        SelectedFlags = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (FlagViewModel)cb.Tag)
            .ToList();

        SelectedSubTasks = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => new
            {
                Title = ((TextBox)row.Children[1]).Text.Trim(),
                IsDone = ((CheckBox)row.Children[0]).IsChecked == true
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = s.IsDone }))
            .ToList();

        DialogResult = true;
        Close();
    }
}
