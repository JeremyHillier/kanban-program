using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Task templates on the task screen: starting a new task from one (the Template row at the top,
// new tasks only) and saving whatever is currently filled in as one (Save as Template, always).
public partial class AddTaskWindow
{
    private void InitializeTemplates()
    {
        TemplateComboBox.ItemsSource = _viewModel.TaskTemplates;
        RefreshTemplatePanel();
    }

    // Always shown for a new task, even with no templates yet - the row is how people find out
    // templates exist, and Manage lives on it. With none, the list is greyed and says how to make
    // one. Never shown when editing: a template would overwrite the task.
    private void RefreshTemplatePanel()
    {
        TemplatePanel.Visibility = _cardToEdit is null ? Visibility.Visible : Visibility.Collapsed;

        var hasTemplates = _viewModel.TaskTemplates.Count > 0;
        TemplateComboBox.IsEnabled = hasTemplates;
        TemplateHintText.Text = hasTemplates
            ? "Start from a template..."
            : "No templates yet - fill in a task below, then click Save as Template";
        UpdateTemplateHint();
    }

    private void UpdateTemplateHint() =>
        TemplateHintText.Visibility = TemplateComboBox.SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;

    // Opens the dialog already filled in from a template (the board's right-click on New Task).
    // Called before the window shows, so the filled-in form is what "unchanged" means on closing.
    public void StartFromTemplate(TaskTemplate template) => TemplateComboBox.SelectedItem = template;

    private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateComboBox.SelectedItem is TaskTemplate template) ApplyTemplate(template);
        UpdateTemplateHint();
    }

    // Fills the form from a template, replacing what was there. A project, person, goal or flag
    // the template refers to that no longer exists (or has been retired) is simply left unset.
    private void ApplyTemplate(TaskTemplate template)
    {
        DetailsTextBox.Text = template.Title;
        RebuildProjectItems(_viewModel.Projects.FirstOrDefault(p => p.Id == template.ProjectId && p.IsActive));
        RebuildGoalItems(_viewModel.Goals.FirstOrDefault(g => g.Id == template.GoalId && g.IsActive));
        SetSelectedPeople(template.AllPeopleIds.Select(id => _viewModel.People.FirstOrDefault(p => p.Id == id && p.IsActive)).OfType<PersonViewModel>());

        PriorityComboBox.SelectedItem = PriorityComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == template.Priority)
            ?? PriorityComboBox.SelectedItem;

        var due = template.DueDateFromToday;
        var start = template.StartDateFromToday;
        if (due is not null && start > due) start = due;
        DueDatePicker.SelectedDate = due;
        StartDatePicker.SelectedDate = start;
        DueTimeTextBox.Text = due is null ? string.Empty : DueTimeParser.Format(template.DueTime);

        NotesTextBox.Text = template.Notes ?? string.Empty;
        WebsiteUrlTextBox.Text = template.WebsiteUrl ?? string.Empty;
        ForceEditOnCompleteCheckBox.IsChecked = template.ForceEditOnComplete;

        RecurringCheckBox.IsChecked = template.IsRecurring;
        RecurrenceComboBox.Visibility = template.IsRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceCountTextBox.Text = template.IsRecurring ? template.RecurrenceCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        if (template.IsRecurring)
        {
            RecurrenceComboBox.SelectedItem = RecurrenceComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == template.RecurrencePattern)
                ?? RecurrenceComboBox.SelectedItem;
        }

        RebuildFlagCheckboxes(forceCheckedIds: template.FlagIds);

        SubTasksPanel.Children.Clear();
        foreach (var title in template.SubTasks) AddSubTaskRow(title);
        UpdateSubTaskProgressLabel();
    }

    // What is on the form right now, as a template. Reads the same controls Add_Click does, but
    // without its validation: a half-filled form is a perfectly good template.
    private TaskTemplate BuildTemplateFromForm()
    {
        var isRecurring = RecurringCheckBox.IsChecked == true;
        var recurrenceCount = TryReadRecurrenceCount(out var count) ? count : null; // a half-typed number just leaves it open-ended
        return new TaskTemplate
        {
            Title = DetailsTextBox.Text.Trim(),
            ProjectId = (ProjectComboBox.SelectedItem as ProjectViewModel)?.Id,
            Priority = (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Normal",
            WhoId = _selectedPeople.FirstOrDefault()?.Id,
            PeopleIds = _selectedPeople.Select(p => p.Id).ToList(),
            GoalId = (GoalComboBox.SelectedItem as GoalViewModel)?.Id,
            FlagIds = FlagsPanel.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true).Select(cb => ((FlagViewModel)cb.Tag).Id).ToList(),
            SubTasks = SubTasksPanel.Children.OfType<Grid>().Select(row => ((TextBox)row.Children[2]).Text.Trim()).Where(t => t.Length > 0).ToList(),
            Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim(),
            IsRecurring = isRecurring,
            RecurrencePattern = isRecurring ? (RecurrenceComboBox.SelectedItem as ComboBoxItem)?.Content as string : null,
            RecurrenceCount = isRecurring ? recurrenceCount : null,
            ForceEditOnComplete = ForceEditOnCompleteCheckBox.IsChecked == true,
            WebsiteUrl = string.IsNullOrWhiteSpace(WebsiteUrlTextBox.Text) ? null : WebsiteUrlTextBox.Text.Trim(),
            DueTime = DueDatePicker.SelectedDate is null ? null : CurrentDueTime(),
            DueInDays = TaskTemplate.DaysFromToday(DueDatePicker.SelectedDate),
            StartInDays = TaskTemplate.DaysFromToday(StartDatePicker.SelectedDate)
        };
    }

    private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplatePrompts.SaveAs(this, _viewModel, BuildTemplateFromForm()) is null) return;
        RefreshTemplatePanel();
    }

    private void ManageTemplates_Click(object sender, RoutedEventArgs e)
    {
        TemplateComboBox.SelectedItem = null;
        new ManageTemplatesWindow(_viewModel) { Owner = this }.ShowDialog();
        RefreshTemplatePanel();
    }
}

// Asking for a template's name, shared by the task screen and the board's right-click menu.
internal static class TemplatePrompts
{
    // Returns the saved template, or null if the user backed out. An existing name is only
    // replaced after a yes.
    public static TaskTemplate? SaveAs(Window owner, MainViewModel viewModel, TaskTemplate template)
    {
        var dialog = new PromptWindow("Save as Template", "Template name", template.Title, "Save") { Owner = owner };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return null;

        if (viewModel.FindTaskTemplate(dialog.Value) is { } existing)
        {
            if (!Dialogs.Confirm(owner, DialogMessage.AskDanger("Save as Template",
                    $"Replace the template \"{existing.Name}\"?\n\nThere is already a template with that name. Tasks already made from it are not affected.",
                    "Replace It")))
                return null;
        }

        return viewModel.SaveTaskTemplate(dialog.Value, template);
    }
}
