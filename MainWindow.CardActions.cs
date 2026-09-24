using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// What a card's own controls do - the quick edits on its project, priority, who and due date,
// its move, delete and done buttons - plus adding and editing tasks and the Waiting On prompts.
public partial class MainWindow
{
    private void ColumnHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column } || DataContext is not MainViewModel viewModel) return;

        OpenAddTaskDialog(viewModel, column);
        e.Handled = true;
    }

    private void OpenAddTaskDialog(MainViewModel viewModel, ColumnViewModel? initialColumn, KanbanApp.Models.TaskTemplate? template = null)
    {
        var dialog = new AddTaskWindow(viewModel) { Owner = this };
        if (initialColumn is not null) dialog.PreselectColumn(initialColumn);
        if (template is not null) dialog.StartFromTemplate(template);

        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            viewModel.AddCard(dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedWho, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl, dueTime: dialog.SelectedDueTime,
                startDate: dialog.SelectedStartDate, waitingOn: dialog.WaitingOn, people: dialog.SelectedPeople, recurrencesLeft: dialog.RecurrencesLeft);
        }
    }

    private void EditCard(CardViewModel card, MainViewModel viewModel, bool focusNotes = false)
    {
        var currentColumn = viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (currentColumn is null) return;

        var dialog = new AddTaskWindow(viewModel, card, currentColumn) { Owner = this };
        if (focusNotes) dialog.FocusNotesField();
        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            viewModel.EditCard(card, dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedPeople, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl, dueTime: dialog.SelectedDueTime,
                startDate: dialog.SelectedStartDate, waitingOn: dialog.WaitingOn, recurrencesLeft: dialog.RecurrencesLeft);
        }
    }

    private void WaitingOnDisplay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() => PromptWaitingOn([card], viewModel)), DispatcherPriority.Background);
    }

    // Asks who or what the task (or group of tasks) is waiting on. The box starts with the current
    // answer when they all share one; saving it empty clears it.
    private void PromptWaitingOn(IReadOnlyList<CardViewModel> cards, MainViewModel viewModel)
    {
        if (cards.Count == 0) return;

        var shared = cards.Select(c => c.WaitingOn).Distinct().Count() == 1 ? cards[0].WaitingOn : null;
        var question = cards.Count == 1 ? "Who or what is this task waiting on?" : $"Who or what are these {cards.Count} tasks waiting on?";
        var dialog = new PromptWindow("Waiting On", question, shared, "Save", viewModel.WaitingOnSuggestions, viewModel.ForgetWaitingOnSuggestion,
            owner => { new ManageWaitingOnWindow(viewModel) { Owner = owner }.ShowDialog(); return viewModel.WaitingOnSuggestions; }) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        viewModel.SetCardsWaitingOn(cards, dialog.Value);
    }

    // Moving a task into the Waiting column is the natural moment to say what it's waiting on, so
    // ask - once, for however many were moved, and only for tasks that don't already say. Cancel
    // just leaves it blank.
    private void MaybePromptWaitingOn(IEnumerable<CardViewModel> moved, ColumnViewModel targetColumn, MainViewModel viewModel)
    {
        if (targetColumn.Name != "Waiting") return;
        PromptWaitingOn(moved.Where(c => !c.IsWaiting).ToList(), viewModel);
    }

    private void DeleteQuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } || DataContext is not MainViewModel viewModel) return;
        DeleteCardWithConfirm(card, viewModel);
    }

    // Shared by the card's X button and its right-click Delete.
    private void DeleteCardWithConfirm(CardViewModel card, MainViewModel viewModel)
    {
        // A recurring task that hasn't completed yet (so hasn't already spawned its next occurrence)
        // gets a real three-way choice instead of the plain confirm - deleting it is how you'd "skip"
        // today's instance, and whether the series should keep going is a decision worth asking for
        // every time, not something the ConfirmDelete setting should be able to skip past.
        var offerRecurrenceChoice = card.HasNextOccurrence;
        var spawnNext = false;

        if (offerRecurrenceChoice)
        {
            var dialog = new DeleteRecurringTaskWindow(card.Title) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SpawnNext is null) return;
            spawnNext = dialog.SpawnNext.Value;
        }
        else if (viewModel.ConfirmDelete)
        {
            var result = MessageBox.Show(this, $"Delete \"{card.Title}\"?\n\nYou can take this back with Undo (Ctrl+Z).",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as QuickMove_Click — removing the card tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => viewModel.DeleteCard(card, spawnNext)), DispatcherPriority.Background);
    }

    // Deleting a whole selection always asks, whatever the ConfirmDelete setting says: it is one
    // question for many tasks. If any of them would still create a next occurrence, the
    // recurring-task choice is asked once for the group instead.
    private void DeleteCardsWithConfirm(List<CardViewModel> cards, MainViewModel viewModel)
    {
        var recurring = cards.Count(c => c.HasNextOccurrence);
        var spawnNext = false;

        if (recurring > 0)
        {
            var dialog = new DeleteRecurringTaskWindow(cards.Count, recurring) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SpawnNext is null) return;
            spawnNext = dialog.SpawnNext.Value;
        }
        else
        {
            var result = MessageBox.Show(this, $"Delete {cards.Count} tasks?\n\nYou can take this back with Undo (Ctrl+Z).",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
        }

        Dispatcher.BeginInvoke(new Action(() => viewModel.DeleteCards(cards, spawnNext)), DispatcherPriority.Background);
    }

    // Shared by every card quick-edit popup below (Flags/Priority/Who/Project): builds a
    // ContextMenu with one MenuItem per option and opens it anchored to placementTarget.
    // Each selection is deferred via BeginInvoke - mutating the card collection (e.g. ApplySort)
    // synchronously from inside a MenuItem.Click handler tears down the popup's PlacementTarget
    // while it's still closing, which deadlocks WPF's layout engine; running it after the menu has
    // actually closed avoids that. Always deferring (even where the specific onSelect action
    // doesn't currently touch the collection, like AddFlagToCard) keeps this helper safe regardless
    // of what a future onSelect ends up doing.
    private void ShowQuickEditMenu<T>(FrameworkElement placementTarget, IEnumerable<(string Header, bool IsChecked, T Value)> items, Action<T> onSelect)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var (header, isChecked, value) in items)
        {
            var menuItem = new System.Windows.Controls.MenuItem { Header = header, IsChecked = isChecked };
            menuItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => onSelect(value)), DispatcherPriority.Background);
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }

    private void AddFlagQuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var available = viewModel.Flags
            .Where(f => f.IsActive && card.Flags.All(cf => cf.Id != f.Id))
            .OrderBy(f => f.Name)
            .ToList();

        if (available.Count == 0)
        {
            MessageBox.Show(this, "This task already has every available flag, or no flags have been created yet.",
                "No Flags to Add", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var items = available.Select(flag => (Header: flag.Name, IsChecked: false, Value: flag));
        ShowQuickEditMenu(element, items, flag => viewModel.AddFlagToCard(card, flag));
    }

    private void EmailCardQuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } || !card.CanEmailCard) return;
        if (DataContext is not MainViewModel viewModel) return;

        OutlookEmailHelper.ComposeCardEmail(this, card, OutlookEmailHelper.JoinRecipients(card.PeopleEmails), viewModel);
    }

    private void PriorityBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var items = new[] { "High", "Medium", "Normal", "Low" }
            .Select(priority => (Header: priority, IsChecked: card.Priority == priority, Value: priority));
        ShowQuickEditMenu(element, items, priority => viewModel.SetCardPriority(card, priority));
        e.Handled = true;
    }

    private void WhoDisplay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var items = new List<(string Header, bool IsChecked, PersonViewModel? Value)> { ("Unassigned", card.WhoId is null, null) };
        items.AddRange(viewModel.People.Where(p => p.IsActive).Select(p =>
            (card.People.Count > 1 && card.WhoId == p.Id ? $"{p.Name}  (lead)" : p.Name, card.IsAssignedTo(p.Id), (PersonViewModel?)p)));
        ShowQuickEditMenu(element, items, who => { if (who is null) viewModel.SetCardWho(card, null); else viewModel.ToggleCardPerson(card, who); });
        e.Handled = true;
    }

    private void ProjectName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var items = viewModel.Projects.Where(p => p.IsActive)
            .Select(project => (Header: project.Name, IsChecked: card.ProjectId == project.Id, Value: project));
        ShowQuickEditMenu(element, items, project => viewModel.SetCardProject(card, project));
        e.Handled = true;
    }

    private void DueDateDisplay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var datePicker = new System.Windows.Controls.DatePicker
        {
            SelectedDate = card.DueDate,
            Width = 160,
            Margin = new Thickness(8, 8, 8, 4)
        };
        CalendarWheelSupport.Attach(datePicker);
        var clearButton = new System.Windows.Controls.Button
        {
            Content = "Clear Due Date",
            Margin = new Thickness(8, 0, 8, 8),
            Padding = new Thickness(4)
        };

        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(datePicker);
        panel.Children.Add(clearButton);

        // StaysOpen="False" (the default for a transient popup) is what actually causes the freeze
        // reported when picking a date from the calendar, not the collection-mutation timing the
        // earlier BeginInvoke fixes addressed: DatePicker's own calendar dropdown is itself a nested
        // Popup, and WPF's automatic "click outside closes it" logic on an outer StaysOpen=False
        // Popup fires synchronously while that nested popup is still tearing down, racing two popup
        // closes against each other. Typing a date never opens that nested popup, so it never hit
        // this. ContextMenu (used by Priority/Who/Project) has its own correct handling of nested
        // popups and isn't affected. Fix: StaysOpen="True" so WPF's racy auto-dismiss never engages,
        // and close it ourselves only in response to an explicit action (date picked, Clear clicked,
        // Escape, or a genuine outside click - detected via the Window's PreviewMouseDown, which a
        // click inside this popup or its nested calendar popup never reaches, since popups are
        // separate top-level windows that don't route input through their owner's event handlers).
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = element,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = true,
            AllowsTransparency = true,
            Child = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = panel
            }
        };

        MouseButtonEventHandler onOutsideClick = null!;
        onOutsideClick = (_, _) => ClosePopup();

        void ClosePopup()
        {
            PreviewMouseDown -= onOutsideClick;
            Deactivated -= OnDeactivatedClosePopup;
            popup.IsOpen = false;

            // DatePicker's calendar dropdown sets Win32 mouse capture on its own native popup window
            // while open. If that window is destroyed (which IsOpen=false above does, for both the
            // calendar popup and ours) without capture being released first, Windows can leave the
            // capture "phantom" - pointing at a window that no longer exists - which silently
            // swallows all further mouse input app-wide until something forces the OS to reset it
            // (dragging the title bar does, via its own native modal move loop; that's the exact
            // "only moving the window unfreezes it" symptom this was causing). Mouse.Capture(null) is
            // the managed-WPF release; NativeMethods.ReleaseCapture() is the Win32-level one, needed
            // in case the capture was set by native code below WPF that the managed call can't reach.
            Mouse.Capture(null);
            NativeMethods.ReleaseCapture();
            Keyboard.Focus(this);
        }

        void OnDeactivatedClosePopup(object? _, EventArgs __) => ClosePopup();

        // Deferred via BeginInvoke: mutating the card collection while this popup is still closing
        // deadlocks WPF's layout engine (see PriorityBadge_MouseLeftButtonDown for the same pattern).
        datePicker.SelectedDateChanged += (_, _) =>
        {
            var newDate = datePicker.SelectedDate;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClosePopup();
                viewModel.SetCardDueDate(card, newDate);
            }), DispatcherPriority.Background);
        };
        clearButton.Click += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClosePopup();
                viewModel.SetCardDueDate(card, null);
            }), DispatcherPriority.Background);
        };
        panel.PreviewKeyDown += (_, keyArgs) =>
        {
            if (keyArgs.Key != Key.Escape) return;
            ClosePopup();
            keyArgs.Handled = true;
        };
        popup.Opened += (_, _) =>
        {
            datePicker.Focus();
            PreviewMouseDown += onOutsideClick;
            Deactivated += OnDeactivatedClosePopup;
        };

        popup.IsOpen = true;
        e.Handled = true;
    }

    private void SubTaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox { DataContext: SubTaskViewModel subTask, Tag: CardViewModel card } checkBox) return;
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.SetSubTaskDone(card, subTask, checkBox.IsChecked == true);
    }

    private void QuickMove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element ||
            element.Tag is not string targetColumnName ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var targetColumn = viewModel.Columns.FirstOrDefault(c => c.Name == targetColumnName);
        if (targetColumn is null) return;

        MoveCardDeferred(card, targetColumn, viewModel);
    }

    // Shared by the card's quick-move buttons and its right-click Move To.
    // Deferred via BeginInvoke: moving the card to another column removes it from the clicked
    // button's own ItemsControl, tearing down the container mid-Click-dispatch — the same deadlock
    // documented on the Priority/Who/Project/Due Date quick-edits above.
    private void MoveCardDeferred(CardViewModel card, ColumnViewModel targetColumn, MainViewModel viewModel)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            viewModel.MoveCardCommand.Execute((card, targetColumn));
            MaybePromptCompletionNote(card, targetColumn, viewModel);
            MaybePromptWaitingOn([card], targetColumn, viewModel);
        }), DispatcherPriority.Background);
    }

    private void MaybePromptCompletionNote(CardViewModel card, ColumnViewModel targetColumn, MainViewModel viewModel, bool askForNote = true)
    {
        if (targetColumn.Name != "Done") return;

        if (card.ForceEditOnComplete)
        {
            EditCard(card, viewModel);
            return;
        }

        if (!askForNote || !viewModel.AddNoteOnComplete) return;

        var result = MessageBox.Show(this, $"Add a completion note to \"{card.Title}\"?\n\nYou can jot down any final details before it's marked Done.",
            "Task Complete", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (result != MessageBoxResult.Yes) return;

        EditCard(card, viewModel, focusNotes: true);
    }
}
