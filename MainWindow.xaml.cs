using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;
    private readonly DatabaseService _db;

    private readonly TimeAlertTracker _timeAlerts = new();
    private readonly DispatcherTimer _dueTimeTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    public MainWindow(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        var mainViewModel = new MainViewModel(db);
        DataContext = mainViewModel;

        // Anything whose time had already passed before the app opened is left to the startup
        // reminder list rather than also popping a time alert on the first tick.
        _timeAlerts.MarkAnnounced(mainViewModel.GetCardsPastDueTime());
        _dueTimeTimer.Tick += DueTimeTimer_Tick;
        _dueTimeTimer.Start();

        RestoreWindowBounds();
        Closing += (_, _) =>
        {
            _dueTimeTimer.Stop();
            SaveWindowBounds();
            var viewModel = DataContext as MainViewModel;
            viewModel?.SaveLastViewState();

            if (viewModel is { AutoBackupEnabled: true })
            {
                BackupService.CreateBackup(_db.DbPath, viewModel.BackupRetentionCount);
            }
        };
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        // Both of these are deferred for the same reason: showing a modal dialog synchronously here
        // would block before the splash screen (still on screen at this point) gets a chance to
        // close. Queued in order, so What's New is dealt with before the reminder list appears.
        if (viewModel.ShouldShowWhatsNewOnStartup())
        {
            Dispatcher.BeginInvoke(() => ShowWhatsNew(viewModel), DispatcherPriority.ApplicationIdle);
        }

        CheckForUpdatesOnStartup(viewModel);

        if (!viewModel.ShowDueReminders) return;

        var dueCards = viewModel.GetDueReminders();
        if (dueCards.Count == 0) return;

        Dispatcher.BeginInvoke(() => ShowReminders(dueCards, viewModel), DispatcherPriority.ApplicationIdle);
    }

    private void ShowWhatsNew(MainViewModel viewModel)
    {
        var dialog = new WhatsNewWindow(viewModel) { Owner = this };
        dialog.ShowDialog();

        // Recorded even if the user turns the screen off from inside it, so switching it back on
        // later doesn't immediately re-show notes they've already read.
        viewModel.MarkWhatsNewSeen();
    }

    private void ShowReminders(List<CardViewModel> dueCards, MainViewModel viewModel)
    {
        var dialog = new ReminderWindow(dueCards, viewModel.Columns, card => EditCard(card, viewModel), card => MarkCardDone(card, viewModel),
            card => viewModel.GetDueReminders().Contains(card)) { Owner = this };
        dialog.ShowDialog();
    }

    private void DueTimeTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.RefreshIfDayChanged();

        var newlyDue = _timeAlerts.TakeCardsToAlert(viewModel.GetCardsPastDueTime(), DateTime.Now);

        // Still recorded as announced while alerts are switched off, so turning them back on later
        // doesn't dump every time that passed in the meantime.
        if (newlyDue.Count == 0 || !viewModel.ShowTimeAlerts) return;

        ShowTimeAlert(newlyDue, viewModel);
    }

    // Non-modal and not owned by the board, so it still surfaces when the board is minimised or
    // behind another app; the Topmost flip brings it to the front once without pinning it there.
    private void ShowTimeAlert(List<CardViewModel> dueCards, MainViewModel viewModel)
    {
        var alert = new ReminderWindow(dueCards, viewModel.Columns, card => EditCard(card, viewModel), card => MarkCardDone(card, viewModel),
            card => viewModel.GetCardsPastDueTime().Contains(card), isTimeAlert: true,
            onSnooze: (cards, duration) => _timeAlerts.Snooze(cards, DateTime.Now + duration))
        {
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };

        System.Media.SystemSounds.Exclamation.Play();
        alert.Show();
        alert.Activate();
        alert.Topmost = false;
    }

    private void MarkCardDone(CardViewModel card, MainViewModel viewModel)
    {
        var doneColumn = viewModel.Columns.FirstOrDefault(c => c.Name == "Done");
        if (doneColumn is null) return;

        viewModel.MoveCardCommand.Execute((card, doneColumn));
        MaybePromptCompletionNote(card, doneColumn, viewModel);
    }

    private void Reminders_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dueCards = viewModel.GetDueReminders();
        if (dueCards.Count == 0)
        {
            MessageBox.Show(this, "No overdue or due-today tasks.", "Task Reminders", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowReminders(dueCards, viewModel);
    }

    private void RestoreWindowBounds()
    {
        var width = double.TryParse(_db.GetSetting("WindowWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : Width;
        var height = double.TryParse(_db.GetSetting("WindowHeight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : Height;
        Width = Math.Clamp(width, MinWidth, SystemParameters.VirtualScreenWidth);
        Height = Math.Clamp(height, MinHeight, SystemParameters.VirtualScreenHeight);

        var hasLeft = double.TryParse(_db.GetSetting("WindowLeft"), NumberStyles.Float, CultureInfo.InvariantCulture, out var left);
        var hasTop = double.TryParse(_db.GetSetting("WindowTop"), NumberStyles.Float, CultureInfo.InvariantCulture, out var top);
        if (hasLeft && hasTop &&
            left + Width > SystemParameters.VirtualScreenLeft && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
            top + 50 > SystemParameters.VirtualScreenTop && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private void SaveWindowBounds()
    {
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        _db.SetSetting("WindowWidth", bounds.Width.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowHeight", bounds.Height.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowLeft", bounds.Left.ToString(CultureInfo.InvariantCulture));
        _db.SetSetting("WindowTop", bounds.Top.ToString(CultureInfo.InvariantCulture));
    }

    // Set when a plain click lands on a card that's part of a multi-selection: the selection is
    // only cleared if the button comes back up without a drag, since the press may be the start of
    // dragging the whole group.
    private bool _clearSelectionOnMouseUp;

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (sender is FrameworkElement { DataContext: CardViewModel card } && DataContext is MainViewModel viewModel)
            {
                EditCard(card, viewModel);
            }
            e.Handled = true;
            return;
        }

        _dragStartPoint = e.GetPosition(null);
        _clearSelectionOnMouseUp = false;

        // Clicks on a card's own buttons (quick-move, flag, email, delete) act on that card only and
        // leave the selection alone.
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (sender is not FrameworkElement { DataContext: CardViewModel clicked } || DataContext is not MainViewModel board) return;

        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                board.ToggleCardSelection(clicked);
                e.Handled = true;
                break;
            case ModifierKeys.Shift:
                board.SelectCardRange(clicked);
                e.Handled = true;
                break;
            case ModifierKeys.None when clicked.IsSelected:
                _clearSelectionOnMouseUp = true;
                break;
            case ModifierKeys.None when board.SelectedCardCount > 0:
                board.ClearCardSelection();
                break;
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_clearSelectionOnMouseUp) return;
        _clearSelectionOnMouseUp = false;
        if (DataContext is MainViewModel board) board.ClearCardSelection();
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var current = element; current is not null; current = current is System.Windows.Media.Visual
                 ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                 : LogicalTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase) return true;
            if (current is System.Windows.Controls.ContentPresenter { Content: CardViewModel }) return false; // reached the card
        }
        return false;
    }

    // What a card drag carries: the grabbed card (the single-card format every handler already
    // understands), plus, when it's part of a multi-selection, the whole group in board order.
    private const string CardGroupFormat = "KanbanApp.CardGroup";

    internal static DataObject CreateCardDragData(CardViewModel grabbed, IReadOnlyList<CardViewModel> selected)
    {
        var data = new DataObject(typeof(CardViewModel), grabbed);
        if (selected.Count > 1 && selected.Contains(grabbed)) data.SetData(CardGroupFormat, selected.ToList());
        return data;
    }

    internal static List<CardViewModel> GetDraggedCards(IDataObject data)
    {
        if (data.GetDataPresent(CardGroupFormat) && data.GetData(CardGroupFormat) is List<CardViewModel> group) return group;
        return data.GetData(typeof(CardViewModel)) is CardViewModel card ? [card] : [];
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(null);
        var diff = _dragStartPoint - currentPosition;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: CardViewModel card } element && DataContext is MainViewModel viewModel)
        {
            // A drag, not a click, so the mouse-up mustn't clear the selection being dragged.
            _clearSelectionOnMouseUp = false;

            // Dragging a card that isn't selected drags just that card, and drops the selection so
            // what's highlighted always matches what the next group drag would carry.
            if (!card.IsSelected && viewModel.SelectedCardCount > 0) viewModel.ClearCardSelection();

            DragDrop.DoDragDrop(element, CreateCardDragData(card, viewModel.SelectedCards), DragDropEffects.Move);

            // DoDragDrop blocks until the drag ends, however it ends (drop, Esc-cancel, focus loss).
            // Clearing every column's insertion-line indicator here, unconditionally, guarantees none
            // are left stuck visible even when a DragLeave/Drop never fired for whichever column last
            // showed one - e.g. a drop landing on Column_Drop's cross-column move instead of the
            // card area's own manual-reorder Drop, which was the only path resetting it before.
            foreach (var col in viewModel.Columns)
            {
                col.IsDropIndicatorVisible = false;
            }
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        var canDrop = OutlookDragDropHelper.HasDroppableFiles(e.Data);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = canDrop;
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (!OutlookDragDropHelper.HasDroppableFiles(e.Data)) return;
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: CardViewModel card } || DataContext is not MainViewModel viewModel) return;

        List<(string FilePath, string DisplayName, bool WasSaved)> files;
        try
        {
            files = OutlookDragDropHelper.ExtractDroppedFiles(e.Data, viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't read the dropped item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var file in files)
        {
            viewModel.AddAttachmentToCard(card, file.FilePath, file.DisplayName);
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        OpenAddTaskDialog(viewModel, null);
    }

    // Right-click New Task: start straight from a template, or get to Manage Templates. Built on
    // each open so it always lists the templates as they are now.
    private void NewTaskButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        AddMenuItem(menu, "_New Task", () => OpenAddTaskDialog(viewModel, null), gesture: "Ctrl+N");
        menu.Items.Add(new System.Windows.Controls.Separator());

        if (viewModel.TaskTemplates.Count == 0)
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "No templates yet", IsEnabled = false });
        }
        foreach (var template in viewModel.TaskTemplates)
        {
            AddMenuItem(menu, $"From template: {MenuText(template.Name)}", () => OpenAddTaskDialog(viewModel, null, template));
        }

        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "_Manage Templates...", () => ManageTemplates(viewModel), gesture: "Alt+M");
        menu.IsOpen = true;
    }

    private void ManageTemplates(MainViewModel viewModel) => new ManageTemplatesWindow(viewModel) { Owner = this }.ShowDialog();

    private void ManageWaitingOn(MainViewModel viewModel) => new ManageWaitingOnWindow(viewModel) { Owner = this }.ShowDialog();

    // Right-click the Waiting On button: the filter it normally applies, or the list behind the suggestions.
    private void WaitingOnButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        AddMenuItem(menu, "_Show Waiting Tasks", () => WaitingOn_Click(button, new RoutedEventArgs()));
        AddMenuItem(menu, "_Manage Waiting On List...", () => ManageWaitingOn(viewModel));
        menu.IsOpen = true;
    }

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
                startDate: dialog.SelectedStartDate, waitingOn: dialog.WaitingOn, people: dialog.SelectedPeople);
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
                startDate: dialog.SelectedStartDate, waitingOn: dialog.WaitingOn);
        }
    }

    private void ManageCustomFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageCustomFiltersWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    // Rebuilt fresh on every hover rather than cached, so a slot saved or renamed a moment ago
    // (via the Manage Custom Filters dialog, or Alt+0-9 capture) always shows up-to-date without
    // needing an explicit refresh hook.
    private void CustomFiltersButton_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var defined = viewModel.CustomFilters
            .Select((filter, slot) => (filter, slot))
            .Where(x => x.filter.IsDefined)
            .ToList();

        CustomFiltersButton.ToolTip = defined.Count == 0
            ? "No custom filters saved yet. Set the board's filters how you like, then click here to save the combination to Alt+0 - Alt+9."
            : "Saved custom filters:\n" + string.Join("\n", defined.Select(x => $"Alt+{x.slot}: {x.filter.Name} — {x.filter.Summary}"));
    }

    private void ManageProjects_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageProjectsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ManageGoals_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageGoalsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ManageFlags_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageFlagsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ManageWho_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ManageWhoWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    // The coloured label boxes beside the Project / Who / Goal / Flag filters double as shortcuts
    // to the matching Manage dialog, the same ones the big Manage buttons open.
    private void ManageProjectsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageProjects_Click(sender, e);

    private void ManageWhoLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageWho_Click(sender, e);

    private void ManageGoalsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageGoals_Click(sender, e);

    private void ManageFlagsLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ManageFlags_Click(sender, e);

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private DispatcherTimer? _statusMessageTimer;

    private void WaitingOn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly(MainViewModel.WaitingOnFilter);
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

    private void HideFuture_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ToggleHideFutureTasks();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastAction();

    // Deferred like every other board change made from a click, and says what it took back, since
    // the card that changed may be scrolled out of view or in another column.
    private void UndoLastAction()
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.CanUndo) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (viewModel.Undo() is not { } undone) return;

            ShowStatusMessage($"Undid: {undone}");
        }), DispatcherPriority.Background);
    }

    // The short line at the foot of the board ("Undid: ...", "Added: ..."), gone after a few seconds.
    private void ShowStatusMessage(string message)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.StatusMessage = message;
        _statusMessageTimer?.Stop();
        _statusMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _statusMessageTimer.Tick += (timer, _) =>
        {
            ((DispatcherTimer)timer!).Stop();
            viewModel.StatusMessage = string.Empty;
        };
        _statusMessageTimer.Start();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handled at the Window level (tunneling PreviewKeyDown, fires before any focused control's
        // own Escape handling) so it's a single, reliable "reset the view" regardless of which
        // filter control happens to have focus — a focused ComboBox's own "just close the dropdown"
        // Escape behavior otherwise leaves other filters untouched, which read as ESC only clearing
        // some of them.
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && DataContext is MainViewModel clearViewModel)
        {
            // Two steps: a multi-card selection is cleared first, then the next Esc clears filters.
            if (clearViewModel.SelectedCardCount > 0) clearViewModel.ClearCardSelection();
            else clearViewModel.ClearFilters();
            e.Handled = true;
            return;
        }

        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                switch (e.Key)
                {
                    case Key.Q:
                        Close();
                        e.Handled = true;
                        break;
                    case Key.P:
                        ReportBuilder_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.Z:
                        // A text box keeps its own Ctrl+Z for typing.
                        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) break;
                        UndoLastAction();
                        e.Handled = true;
                        break;
                    case Key.N:
                        AddTask_Click(sender, e);
                        e.Handled = true;
                        break;
                }
                break;

            case ModifierKeys.Alt:
                switch (e.Key == Key.System ? e.SystemKey : e.Key)
                {
                    case Key.A:
                        ArchiveDone_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.P:
                        ManageProjects_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.G:
                        ManageGoals_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.F:
                        ManageFlags_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.W:
                        ManageWho_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.R:
                        Reminders_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.S:
                        Settings_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.H:
                        Help_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.L:
                        Timeline_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.M:
                        if (DataContext is MainViewModel templatesViewModel) ManageTemplates(templatesViewModel);
                        e.Handled = true;
                        break;
                    case Key.T:
                        if (DataContext is MainViewModel todayViewModel) todayViewModel.ShowTodayOnly();
                        e.Handled = true;
                        break;
                    default:
                        ApplyCustomFilterShortcut(e);
                        break;
                }
                break;
        }
    }

    // Alt+0 - Alt+9 apply that slot's saved filter. Both the number row (D0-D9) and the numeric
    // keypad (NumPad0-9) map to the same slot. An unassigned slot is left unhandled so the key does
    // nothing at all, rather than appearing to work and silently clearing the board's filters.
    private void ApplyCustomFilterShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        var slot = key switch
        {
            >= Key.D0 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => -1
        };

        if (slot < 0 || DataContext is not MainViewModel viewModel) return;

        if (viewModel.ApplyCustomFilter(slot))
        {
            e.Handled = true;
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new SettingsWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new HelpWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ReportBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ReportBuilderWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Timeline_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new TimelineWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ArchiveDone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        if (viewModel.ConfirmArchive)
        {
            var result = MessageBox.Show(this, "Archive all tasks in the Done column?\n\nThey'll be removed from the board but not deleted.",
                "Confirm Archive", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        viewModel.ArchiveDoneTasks();
    }

    private DispatcherTimer? _viewArchivedClickTimer;

    private void ViewArchived_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            _viewArchivedClickTimer?.Stop();
            OpenDeletedTasks();
            return;
        }

        _viewArchivedClickTimer?.Stop();
        _viewArchivedClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _viewArchivedClickTimer.Tick += (_, _) =>
        {
            _viewArchivedClickTimer!.Stop();
            OpenArchivedTasks();
        };
        _viewArchivedClickTimer.Start();
    }

    private void OpenArchivedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ArchivedTasksWindow(viewModel, viewModel.GetArchivedCards()) { Owner = this };
        dialog.ShowDialog();
    }

    private void OpenDeletedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new DeletedTasksWindow(viewModel, viewModel.GetDeletedCards()) { Owner = this };
        dialog.ShowDialog();
    }

    private DispatcherTimer? _importTasksClickTimer;

    private void ImportTasks_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            _importTasksClickTimer?.Stop();
            OpenImportedTasks();
            return;
        }

        _importTasksClickTimer?.Stop();
        _importTasksClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _importTasksClickTimer.Tick += (_, _) =>
        {
            _importTasksClickTimer!.Stop();
            OpenImportTasks();
        };
        _importTasksClickTimer.Start();
    }

    private void OpenImportTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ImportTasksWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void OpenImportedTasks()
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ImportedTasksWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new DashboardWindow(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    // FilterOptionViewModel.IsSelected is bound TwoWay to each ListBoxItem, so the Ctrl/Shift-click
    // selection itself is already applied to the view model by the time this fires - it only needs
    // to trigger the actual re-filter pass.
    private void ProjectFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void PriorityFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void WhoFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void SortByProject_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Project, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByDueDate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.DueDate, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByWho_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Who, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByPriority_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Priority, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    // Each of these clears every other filter before applying its own - see
    // MainViewModel.ShowDueFilterOnly for why they deliberately aren't cumulative.
    private void DueToday_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Today");
    }

    private void DueTomorrow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Tomorrow");
    }

    private void DueWithinWeek_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Within a Week");
    }

    private void DueNone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("No Due Date");
    }

    // The grip sits right under the list it resizes and moves with it, so each DragDelta is the
    // distance moved since the previous one and can simply be added on.
    private void FilterResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement grip) return;

        if ((string)grip.Tag == "Project")
        {
            viewModel.ProjectFilterListHeight += e.VerticalChange;
        }
        else
        {
            viewModel.PriorityWhoFilterListHeight += e.VerticalChange;
        }
    }

    private void FilterResizeGrip_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        (DataContext as MainViewModel)?.SaveFilterListHeights();
    }

    private void FilterResizeGrip_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement grip) return;

        if ((string)grip.Tag == "Project")
        {
            viewModel.ProjectFilterListHeight = MainViewModel.DefaultFilterListHeight;
        }
        else
        {
            viewModel.PriorityWhoFilterListHeight = MainViewModel.DefaultFilterListHeight;
        }
        viewModel.SaveFilterListHeights();
        e.Handled = true;
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearFilters();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ToggleTheme();
    }

    private void ToggleCardSize_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ToggleCardSize();
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
        var offerRecurrenceChoice = card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern) && !card.NextOccurrenceSpawned;
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
        var recurring = cards.Count(c => c.IsRecurring && !string.IsNullOrWhiteSpace(c.RecurrencePattern) && !c.NextOccurrenceSpawned);
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

    // Every right-click menu action runs after the menu has closed - see ShowQuickEditMenu for why.
    private System.Windows.Controls.MenuItem AddMenuItem(System.Windows.Controls.ItemsControl parent, string header, Action action,
        bool isEnabled = true, bool isChecked = false, string? gesture = null)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header, IsEnabled = isEnabled, IsChecked = isChecked, InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        parent.Items.Add(item);
        return item;
    }

    private static System.Windows.Controls.MenuItem AddSubmenu(System.Windows.Controls.ContextMenu menu, string header)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        menu.Items.Add(item);
        return item;
    }

    // A person or list name is data, not a caption: without this a name containing an underscore
    // would lose it to WPF's access-key handling.
    private static string MenuText(string name) => name.Replace("_", "__");

    // The card's right-click menu. Built fresh on every open so it reflects the card as it is now
    // (its column, priority, assignee, remaining flags, whether it has a website or an email).
    // Every entry reuses the path its button or quick-edit already takes, so a right-click Delete
    // or Move behaves exactly like the X or quick-move button, prompts included.
    //
    // Right-clicking a card that is part of a multi-card selection opens the selection's menu
    // instead. Right-clicking a card outside the selection drops the selection first (as dragging
    // one does), so what's highlighted always matches what a menu action would touch.
    private void Card_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;
        e.Handled = true;

        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = element,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
        };

        if (card.IsSelected && viewModel.SelectedCardCount > 1)
        {
            BuildSelectionMenu(menu, viewModel.SelectedCards, viewModel);
        }
        else
        {
            if (!card.IsSelected && viewModel.SelectedCardCount > 0) viewModel.ClearCardSelection();
            BuildCardMenu(menu, card, viewModel);
        }

        menu.IsOpen = true;
    }

    private void BuildCardMenu(System.Windows.Controls.ContextMenu menu, CardViewModel card, MainViewModel viewModel)
    {
        var currentColumn = viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        void Separator() => menu.Items.Add(new System.Windows.Controls.Separator());

        AddMenuItem(menu, "_Edit Task...", () => EditCard(card, viewModel), gesture: "Double-click");
        AddMenuItem(menu, "_Copy as Text", () => CopyToClipboard(CardTextFormatter.Format(card, currentColumn?.DisplayName ?? string.Empty)));
        AddMenuItem(menu, "Copy _Title", () => CopyToClipboard(card.Title));
        AddMenuItem(menu, "D_uplicate", () => viewModel.DuplicateCard(card));
        AddMenuItem(menu, "Save as Temp_late...", () => TemplatePrompts.SaveAs(this, viewModel, MainViewModel.TemplateFromCard(card)));
        Separator();

        var moveTo = AddSubmenu(menu, "_Move To");
        foreach (var column in viewModel.Columns)
        {
            var isCurrent = column == currentColumn;
            AddMenuItem(moveTo, MenuText(column.DisplayName), () => MoveCardDeferred(card, column, viewModel), isEnabled: !isCurrent, isChecked: isCurrent);
        }

        var priority = AddSubmenu(menu, "_Priority");
        foreach (var level in new[] { "High", "Medium", "Normal", "Low" })
        {
            AddMenuItem(priority, level, () => viewModel.SetCardPriority(card, level), isChecked: card.Priority == level);
        }

        var assign = AddSubmenu(menu, "_Assign To");
        AddMenuItem(assign, "Unassigned", () => viewModel.SetCardWho(card, null), isChecked: card.WhoId is null);
        foreach (var person in viewModel.People.Where(p => p.IsActive))
        {
            // Ticks the person on or off, leaving the others: a task can have several people.
            var label = card.People.Count > 1 && card.WhoId == person.Id ? $"{person.Name}  (lead)" : person.Name;
            AddMenuItem(assign, MenuText(label), () => viewModel.ToggleCardPerson(card, person), isChecked: card.IsAssignedTo(person.Id));
        }

        var project = AddSubmenu(menu, "P_roject");
        foreach (var option in viewModel.Projects.Where(p => p.IsActive))
        {
            AddMenuItem(project, MenuText(option.Name), () => viewModel.SetCardProject(card, option), isChecked: card.ProjectId == option.Id);
        }

        var waiting = AddSubmenu(menu, "Waitin_g On");
        AddMenuItem(waiting, card.IsWaiting ? "Change..." : "Set...", () => PromptWaitingOn([card], viewModel));
        AddMenuItem(waiting, "Clear", () => viewModel.SetCardWaitingOn(card, null), isEnabled: card.IsWaiting);
        waiting.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(waiting, "Manage List...", () => ManageWaitingOn(viewModel));

        var availableFlags = viewModel.Flags
            .Where(f => f.IsActive && card.Flags.All(cf => cf.Id != f.Id))
            .OrderBy(f => f.Name)
            .ToList();
        var addFlag = AddSubmenu(menu, "Add _Flag");
        addFlag.IsEnabled = availableFlags.Count > 0;
        foreach (var flag in availableFlags)
        {
            AddMenuItem(addFlag, MenuText(flag.Name), () => viewModel.AddFlagToCard(card, flag));
        }

        Separator();
        AddMenuItem(menu, "Open _Website", () => UrlLauncher.Open(card.WebsiteUrl, this), isEnabled: !string.IsNullOrWhiteSpace(card.WebsiteUrl));
        AddMenuItem(menu, "E_mail Task...", () => OutlookEmailHelper.ComposeCardEmail(this, card, OutlookEmailHelper.JoinRecipients(card.PeopleEmails), viewModel), isEnabled: card.CanEmailCard);
        Separator();
        AddMenuItem(menu, "_Delete...", () => DeleteCardWithConfirm(card, viewModel));
    }

    // The same menu for a whole selection. One-card-only entries (Edit, Open Website, Email) are
    // left out. A tick means every selected card already has that value; choosing an entry changes
    // only the cards that differ. The cards are captured when the menu opens, so the action applies
    // to what was highlighted at that moment.
    private void BuildSelectionMenu(System.Windows.Controls.ContextMenu menu, List<CardViewModel> cards, MainViewModel viewModel)
    {
        void Separator() => menu.Items.Add(new System.Windows.Controls.Separator());
        ColumnViewModel? ColumnOf(CardViewModel card) => viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));

        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = $"{cards.Count} tasks selected", IsEnabled = false, FontWeight = FontWeights.Bold });
        Separator();

        const string divider = "\r\n----------------------------------------\r\n\r\n";
        AddMenuItem(menu, "_Copy All as Text", () => CopyToClipboard(string.Join(divider,
            cards.Select(c => CardTextFormatter.Format(c, ColumnOf(c)?.DisplayName ?? string.Empty)))));
        AddMenuItem(menu, "Copy _Titles", () => CopyToClipboard(string.Join("\r\n", cards.Select(c => c.Title))));
        AddMenuItem(menu, "D_uplicate All", () => viewModel.DuplicateCards(cards));
        Separator();

        var moveTo = AddSubmenu(menu, "_Move All To");
        foreach (var column in viewModel.Columns)
        {
            var allHere = cards.All(column.Cards.Contains);
            AddMenuItem(moveTo, MenuText(column.DisplayName), () =>
            {
                // As with dragging a group: no "add a note?" per card, but a task set to force an
                // edit on completion still opens.
                var movedCards = viewModel.MoveCards(cards, column);
                foreach (var moved in movedCards) MaybePromptCompletionNote(moved, column, viewModel, askForNote: false);
                MaybePromptWaitingOn(movedCards, column, viewModel);
            }, isEnabled: !allHere, isChecked: allHere);
        }

        var priority = AddSubmenu(menu, "_Priority");
        foreach (var level in new[] { "High", "Medium", "Normal", "Low" })
        {
            AddMenuItem(priority, level, () => viewModel.SetCardsPriority(cards, level), isChecked: cards.All(c => c.Priority == level));
        }

        var assign = AddSubmenu(menu, "_Assign To");
        AddMenuItem(assign, "Unassigned", () => viewModel.SetCardsWho(cards, null), isChecked: cards.All(c => c.WhoId is null));
        foreach (var person in viewModel.People.Where(p => p.IsActive))
        {
            // Ticked only when every task has them; clicking adds them to the ones that do not,
            // or takes them off all of them when they are already on every one.
            var onAll = cards.All(c => c.IsAssignedTo(person.Id));
            AddMenuItem(assign, MenuText(person.Name), () => { if (onAll) viewModel.RemovePersonFromCards(cards, person); else viewModel.AddPersonToCards(cards, person); }, isChecked: onAll);
        }

        var project = AddSubmenu(menu, "P_roject");
        foreach (var option in viewModel.Projects.Where(p => p.IsActive))
        {
            AddMenuItem(project, MenuText(option.Name), () => viewModel.SetCardsProject(cards, option), isChecked: cards.All(c => c.ProjectId == option.Id));
        }

        var waiting = AddSubmenu(menu, "Waitin_g On");
        AddMenuItem(waiting, "Set...", () => PromptWaitingOn(cards, viewModel));
        AddMenuItem(waiting, "Clear", () => viewModel.SetCardsWaitingOn(cards, null), isEnabled: cards.Any(c => c.IsWaiting));
        waiting.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(waiting, "Manage List...", () => ManageWaitingOn(viewModel));

        // A flag is offered while at least one selected card lacks it.
        var availableFlags = viewModel.Flags
            .Where(f => f.IsActive && cards.Any(c => c.Flags.All(cf => cf.Id != f.Id)))
            .OrderBy(f => f.Name)
            .ToList();
        var addFlag = AddSubmenu(menu, "Add _Flag");
        addFlag.IsEnabled = availableFlags.Count > 0;
        foreach (var flag in availableFlags)
        {
            AddMenuItem(addFlag, MenuText(flag.Name), () => viewModel.AddFlagToCards(cards, flag));
        }

        Separator();
        AddMenuItem(menu, "Clear _Selection", viewModel.ClearCardSelection, gesture: "Esc");
        Separator();
        AddMenuItem(menu, $"_Delete {cards.Count} Tasks...", () => DeleteCardsWithConfirm(cards, viewModel));
    }

    private void CopyToClipboard(string text)
    {
        // Another program (a clipboard manager, a remote-desktop session) can briefly hold the
        // clipboard open, which makes SetText throw. Say so rather than letting it reach the crash
        // handler, so the user knows to just try again.
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            MessageBox.Show(this, "The clipboard is being used by another program. Please try again.",
                "Couldn't Copy", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

    // Shared by every XAML-declared DatePicker in this window (the due-date range filter's From/To
    // pickers) - the board's own due-date quick-edit popup above builds its DatePicker in code and
    // wires CalendarWheelSupport.Attach directly instead, since it has no XAML element to hang a
    // Loaded handler off of.
    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        CalendarWheelSupport.Attach((System.Windows.Controls.DatePicker)sender);
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

    private void Column_Drop(object sender, DragEventArgs e)
    {
        var dragged = GetDraggedCards(e.Data);
        if (dragged.Count > 0 &&
            sender is FrameworkElement { DataContext: ColumnViewModel column } &&
            DataContext is MainViewModel viewModel)
        {
            // Deferred via BeginInvoke: same defensive reasoning as QuickMove_Click — this handler
            // still runs nested inside DoDragDrop's own message loop (Drop fires before DoDragDrop
            // returns to Card_MouseMove), so mutating the collection here immediately carries the
            // same class of risk as mutating it from inside a Click handler.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var moved = viewModel.MoveCards(dragged, column);

                // One card gets the usual completion prompt. A group moved into Done skips the
                // optional "add a note?" question rather than asking once per card, but a task set
                // to force an edit on completion still opens, since that is the task's own setting.
                var askForNote = dragged.Count == 1;
                foreach (var card in moved) MaybePromptCompletionNote(card, column, viewModel, askForNote);
                MaybePromptWaitingOn(moved, column, viewModel);
            }), DispatcherPriority.Background);
        }
    }

    // Positional drag-to-reorder within a column, always available - a card (or a selected group
    // of cards) dragged over its own current column is reordered there, and the board switches into
    // manual sort mode (see MainViewModel.ReorderCardsWithinColumn). A drag that includes any card
    // from a different column keeps using Column_Drop's move, so this deliberately returns false
    // (leaving the event unhandled, to bubble up to Column_Drop) for every other case.
    // The sender is the Grid wrapping each column's card list and its insertion line.
    private static bool TryGetManualReorderContext(object sender, DragEventArgs e,
        out System.Windows.Controls.ItemsControl itemsControl, out ColumnViewModel column, out List<CardViewModel> draggedCards)
    {
        itemsControl = null!;
        column = null!;
        draggedCards = null!;

        if (sender is not System.Windows.Controls.Grid { DataContext: ColumnViewModel col } grid) return false;
        var cards = GetDraggedCards(e.Data);
        if (cards.Count == 0 || !cards.All(col.Cards.Contains)) return false;

        var ic = grid.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
        if (ic is null) return false;

        itemsControl = ic;
        column = col;
        draggedCards = cards;
        return true;
    }

    private void CardsArea_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCards)) return;

        e.Handled = true;
        e.Effects = DragDropEffects.Move;

        var (_, indicatorY) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCards);
        column.DropIndicatorY = indicatorY;
        column.IsDropIndicatorVisible = true;
    }

    private void CardsArea_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.Grid { DataContext: ColumnViewModel column } area) return;

        // Same spurious-DragLeave guard as the sub-task drag indicator (AddTaskWindow.xaml.cs):
        // only actually hide once the mouse has genuinely left the card area's bounds, not just
        // crossed onto a child card that isn't itself drop-enabled.
        var position = e.GetPosition(area);
        if (position.X >= 0 && position.X <= area.ActualWidth &&
            position.Y >= 0 && position.Y <= area.ActualHeight)
        {
            return;
        }

        column.IsDropIndicatorVisible = false;
    }

    private void CardsArea_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCards)) return;

        e.Handled = true;
        column.IsDropIndicatorVisible = false;
        if (DataContext is not MainViewModel viewModel) return;

        var (newIndex, _) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCards);

        // Deferred via BeginInvoke: same reasoning as Column_Drop/QuickMove_Click above - this
        // handler still runs nested inside DoDragDrop's own message loop, so mutating the Cards
        // collection here immediately carries the same class of risk as mutating it from inside a
        // Click handler.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            viewModel.ReorderCardsWithinColumn(draggedCards, column, newIndex);
        }), DispatcherPriority.Background);
    }

    // Returns both where a drop would land (Index, in "before anything moves" Cards-count space -
    // matching MainViewModel.ReorderCardsWithinColumn) and the Y position (relative to itemsControl,
    // i.e. on screen) for the insertion-line indicator, so DragOver and Drop always agree. The
    // dragged cards themselves are never a target.
    //
    // The list is virtualized and filtered, so this walks the list's own items (the visible cards)
    // and only the ones that currently have an on-screen element, then maps the matched card back
    // to its position in the full column. Cards without an element are all either above or below
    // the visible area, and the mouse is inside it, so skipping them can't change the answer.
    internal static (int Index, double IndicatorY) GetCardDropTarget(System.Windows.Controls.ItemsControl itemsControl, ColumnViewModel column,
        Point positionInItemsControl, IReadOnlyCollection<CardViewModel> draggedCards)
    {
        var items = itemsControl.Items;
        var generator = itemsControl.ItemContainerGenerator;
        CardViewModel? lastCard = null;
        var lastIndex = -1;
        var lastBottom = 0.0;

        for (var i = 0; i < items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not FrameworkElement container) continue;

            var card = (CardViewModel)items[i];
            var top = container.TranslatePoint(new Point(0, 0), itemsControl).Y;
            if (!draggedCards.Contains(card) && positionInItemsControl.Y < top + container.ActualHeight / 2)
            {
                return (column.Cards.IndexOf(card), top);
            }

            lastCard = card;
            lastIndex = i;
            lastBottom = top + container.ActualHeight;
        }

        if (lastCard is null) return (0, 0);

        // Below every card that has an element: after the last visible card in the list means the
        // very end of the column (past any hidden ones too, as before); otherwise straight after
        // the last card on screen.
        return lastIndex == items.Count - 1
            ? (column.Cards.Count, lastBottom)
            : (column.Cards.IndexOf(lastCard) + 1, lastBottom);
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

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
    }
}
