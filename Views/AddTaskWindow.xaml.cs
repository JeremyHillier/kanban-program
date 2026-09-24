using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<string> _sessionPastedFilePaths = [];
    private CardViewModel? _cardToEdit;
    private Point _subTaskDragStartPoint;
    private Grid? _subTaskDragCandidate;

    // What the form looked like once it finished being populated. Captured on Loaded rather than at
    // the end of the constructor because callers still set things up afterwards (PreselectColumn),
    // and those programmatic changes must not count as the user having typed something.
    private string? _openingSignature;

    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }
    public ProjectViewModel? SelectedProject { get; private set; }
    public GoalViewModel? SelectedGoal { get; private set; }
    public List<FlagViewModel> SelectedFlags { get; private set; } = [];
    public List<SubTaskViewModel> SelectedSubTasks { get; private set; } = [];
    public List<AttachmentViewModel> SelectedAttachments { get; private set; } = [];
    public string SelectedPriority { get; private set; } = "Normal";
    public DateTime? SelectedDueDate { get; private set; }
    public string? SelectedDueTime { get; private set; }
    public DateTime? SelectedStartDate { get; private set; }
    // Everyone the task is assigned to, lead first. SelectedWho is the lead.
    public List<PersonViewModel> SelectedPeople { get; private set; } = [];
    public PersonViewModel? SelectedWho => SelectedPeople.FirstOrDefault();
    public string? Notes { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? WaitingOn { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurrencePattern { get; private set; }
    public int? RecurrencesLeft { get; private set; } // times in all, counting this one; null has no end
    public bool ForceEditOnComplete { get; private set; }

    public AddTaskWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        // Constrains the scrollable form area directly, rather than relying on the Grid row it
        // sits in to propagate the window's MaxHeight down during a SizeToContent="Height" measure
        // pass (it doesn't, reliably) - this is what actually makes the ScrollViewer's own scrollbar
        // activate once content (e.g. a long sub-task list) overflows, instead of the window just
        // growing past the screen's bottom edge and taking the Cancel/Add Task buttons with it.
        FormScrollViewer.MaxHeight = SystemParameters.WorkArea.Height * 0.75;

        // Base spell-check language is en-CA (set in XAML); this custom dictionary adds American
        // spelling variants (color, favorite, traveled, ...) on top so both are accepted instead of
        // one dialect flagging the other as misspelled.
        var americanVariantsDictionary = new Uri("pack://application:,,,/Assets/AmericanSpellingVariants.lex", UriKind.Absolute);
        SpellCheck.GetCustomDictionaries(DetailsTextBox).Add(americanVariantsDictionary);
        SpellCheck.GetCustomDictionaries(NotesTextBox).Add(americanVariantsDictionary);

        _viewModel = viewModel;
        CalendarWheelSupport.Attach(DueDatePicker);
        CalendarWheelSupport.Attach(StartDatePicker);
        CategoryComboBox.ItemsSource = viewModel.Columns;
        RebuildProjectItems();
        RebuildGoalItems();
        RebuildWhoItems();
        RebuildFlagCheckboxes();
        InitializeTemplates();
        InitializeWhoTypeAhead();
        _waitingOnSuggestions = TextBoxSuggestions.Attach(WaitingOnTextBox, viewModel.WaitingOnSuggestions, viewModel.ForgetWaitingOnSuggestion);
        UpdateSubTaskProgressLabel();

        ProjectComboBox.Focus();

        Loaded += (_, _) => _openingSignature = BuildSignature();
    }

    // Everything the form would save, rendered as one comparable string. Built from the same
    // controls Add_Click reads, so a field can't be added to the save path and silently left out of
    // the unsaved-changes check.
    private string BuildSignature()
    {
        var flags = string.Join("|", FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => ((FlagViewModel)cb.Tag).Id));

        var subTasks = string.Join("|", SubTasksPanel.Children.OfType<Grid>()
            .Select(row => $"{((TextBox)row.Children[2]).Text}{((CheckBox)row.Children[1]).IsChecked == true}"));

        var attachments = string.Join("|", AttachmentsPanel.Children.OfType<Grid>()
            .Select(row => ((AttachmentViewModel)row.Tag).FilePath));

        return string.Join("",
            DetailsTextBox.Text,
            (ProjectComboBox.SelectedItem as ProjectViewModel)?.Id.ToString() ?? "-",
            (CategoryComboBox.SelectedItem as ColumnViewModel)?.Id.ToString() ?? "-",
            (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? "-",
            DueDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "-",
            StartDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "-",
            CurrentDueTime() ?? DueTimeTextBox.Text.Trim(),
            string.Join(",", _selectedPeople.Select(p => p.Id)),
            (GoalComboBox.SelectedItem as GoalViewModel)?.Id.ToString() ?? "-",
            RecurringCheckBox.IsChecked == true,
            (RecurrenceComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? "-",
            RecurrenceCountTextBox.Text.Trim(),
            ForceEditOnCompleteCheckBox.IsChecked == true,
            NotesTextBox.Text,
            WebsiteUrlTextBox.Text,
            WaitingOnTextBox.Text,
            flags, subTasks, attachments);
    }

    private bool HasUnsavedChanges() => _openingSignature is not null && BuildSignature() != _openingSignature;

    // Covers every way of abandoning the dialog - Escape (the Cancel button is IsCancel), the Cancel
    // button itself, and the title bar's X. DialogResult is true only when Add/Save actually ran, so
    // a successful save never prompts.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || DialogResult == true || !HasUnsavedChanges()) return;

        if (!UnsavedChangesGuard.ConfirmDiscard(this))
        {
            e.Cancel = true;
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow(_viewModel, "HelpSection_AddEditTask") { Owner = this }.ShowDialog();
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

        _cardToEdit = cardToEdit;
        RefreshTemplatePanel();

        TaskStampPanel.Visibility = Visibility.Visible;
        UpdatedStampText.Text = cardToEdit.UpdatedFullDisplay;
        if (cardToEdit.CompletedFullDisplay is { } completed)
        {
            CompletedStampText.Text = completed;
            CompletedStampText.Visibility = Visibility.Visible;
        }

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
        StartDatePicker.SelectedDate = cardToEdit.StartDate;
        DueTimeTextBox.Text = DueTimeParser.Format(cardToEdit.DueTime);
        SetSelectedPeople(cardToEdit.People);
        NotesTextBox.Text = cardToEdit.Notes ?? string.Empty;
        WebsiteUrlTextBox.Text = cardToEdit.WebsiteUrl ?? string.Empty;
        WaitingOnTextBox.Text = cardToEdit.WaitingOn ?? string.Empty;
        ForceEditOnCompleteCheckBox.IsChecked = cardToEdit.ForceEditOnComplete;

        RecurringCheckBox.IsChecked = cardToEdit.IsRecurring;
        RecurrenceComboBox.Visibility = cardToEdit.IsRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceCountTextBox.Text = cardToEdit.IsRecurring ? cardToEdit.RecurrencesLeft?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
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

        foreach (var attachment in cardToEdit.Attachments)
        {
            AddAttachmentRow(attachment);
        }
    }

    private void ClearWaitingOn_Click(object sender, RoutedEventArgs e) => WaitingOnTextBox.Text = string.Empty;

    private TextBoxSuggestions? _waitingOnSuggestions;

    // Opens the Waiting On list. Apart from the case noted below, what is in the box is left alone.
    private void ManageWaitingOn_Click(object sender, RoutedEventArgs e)
    {
        var before = _cardToEdit?.WaitingOn;
        new ManageWaitingOnWindow(_viewModel) { Owner = this }.ShowDialog();

        // Renaming or deleting there can reword this very task on the board. If the box still holds
        // the old wording, follow it - otherwise saving would put the old wording straight back.
        if (_cardToEdit is not null && _cardToEdit.WaitingOn != before && WaitingOnTextBox.Text.Trim() == (before ?? string.Empty))
        {
            WaitingOnTextBox.Text = _cardToEdit.WaitingOn ?? string.Empty;
        }

        if (_waitingOnSuggestions is null) _waitingOnSuggestions = TextBoxSuggestions.Attach(WaitingOnTextBox, _viewModel.WaitingOnSuggestions, _viewModel.ForgetWaitingOnSuggestion);
        else _waitingOnSuggestions.Replace(_viewModel.WaitingOnSuggestions);
    }

    // Opens whatever is currently typed, not the card's saved value, so a link pasted in during
    // this edit can be tried out before saving.
    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open(WebsiteUrlTextBox.Text, this);
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

        if (ProjectComboBox.SelectedItem is not ProjectViewModel project)
        {
            MessageBox.Show(this, "Please select a Project before saving.", "Project Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dueTime = CurrentDueTime();
        if (dueTime is null && !string.IsNullOrWhiteSpace(DueTimeTextBox.Text))
        {
            MessageBox.Show(this, "The time isn't recognised. Enter it like 2:30 PM or 14:30, or leave it blank.",
                "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
            DueTimeTextBox.Focus();
            return;
        }
        if (dueTime is not null && DueDatePicker.SelectedDate is null)
        {
            MessageBox.Show(this, "A time needs a due date to go with it. Pick a due date, or clear the time.",
                "Due Date Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            DueDatePicker.Focus();
            return;
        }
        if (StartDatePicker.SelectedDate is { } startDate && DueDatePicker.SelectedDate is { } dueDate && startDate.Date > dueDate.Date)
        {
            MessageBox.Show(this, "The start date is after the due date. Move one of them, or clear the start date.",
                "Start Date After Due Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            StartDatePicker.Focus();
            return;
        }
        if (!string.IsNullOrWhiteSpace(WebsiteUrlTextBox.Text) && !UrlLauncher.TryNormalize(WebsiteUrlTextBox.Text, out _))
        {
            MessageBox.Show(this, UrlLauncher.AllowedLinksMessage + "\n\nFix the Website field, or clear it.",
                "Website Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
            WebsiteUrlTextBox.Focus();
            WebsiteUrlTextBox.SelectAll();
            return;
        }

        if (!TryReadRecurrenceCount(out var recurrencesLeft))
        {
            MessageBox.Show(this, "Enter how many times this task should happen in all, from 1 to 999, or leave the box empty to keep it repeating with no end.",
                "Number of Times", MessageBoxButton.OK, MessageBoxImage.Warning);
            RecurrenceCountTextBox.Focus();
            RecurrenceCountTextBox.SelectAll();
            return;
        }

        TaskDetails = details;
        SelectedDueTime = dueTime;
        SelectedColumn = column;
        SelectedProject = project;
        SelectedGoal = GoalComboBox.SelectedItem as GoalViewModel;
        SelectedPriority = (string)priorityItem.Content;
        SelectedDueDate = DueDatePicker.SelectedDate;
        SelectedStartDate = StartDatePicker.SelectedDate;
        SelectedPeople = [.. _selectedPeople];
        Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();
        WebsiteUrl = string.IsNullOrWhiteSpace(WebsiteUrlTextBox.Text) ? null : WebsiteUrlTextBox.Text.Trim();
        WaitingOn = string.IsNullOrWhiteSpace(WaitingOnTextBox.Text) ? null : WaitingOnTextBox.Text.Trim();

        IsRecurring = RecurringCheckBox.IsChecked == true;
        RecurrencePattern = IsRecurring && RecurrenceComboBox.SelectedItem is ComboBoxItem recurrenceItem
            ? (string)recurrenceItem.Content
            : null;
        RecurrencesLeft = IsRecurring ? recurrencesLeft : null;
        ForceEditOnComplete = ForceEditOnCompleteCheckBox.IsChecked == true;

        SelectedFlags = FlagsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (FlagViewModel)cb.Tag)
            .ToList();

        SelectedSubTasks = SubTasksPanel.Children.OfType<Grid>()
            .Select(row => new
            {
                Title = ((TextBox)row.Children[2]).Text.Trim(),
                IsDone = ((CheckBox)row.Children[1]).IsChecked == true
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = s.IsDone }))
            .ToList();

        SelectedAttachments = AttachmentsPanel.Children.OfType<Grid>()
            .Select(row => (AttachmentViewModel)row.Tag)
            .ToList();

        DialogResult = true;
        Close();
    }
}
