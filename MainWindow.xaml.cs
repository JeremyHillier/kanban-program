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

    public MainWindow(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        DataContext = new MainViewModel(db);

        RestoreWindowBounds();
        Closing += (_, _) =>
        {
            SaveWindowBounds();
            (DataContext as MainViewModel)?.SaveLastViewState();
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

        if (sender is FrameworkElement { DataContext: CardViewModel card } element)
        {
            DragDrop.DoDragDrop(element, card, DragDropEffects.Move);
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

    private void ColumnHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column } || DataContext is not MainViewModel viewModel) return;

        OpenAddTaskDialog(viewModel, column);
        e.Handled = true;
    }

    private void OpenAddTaskDialog(MainViewModel viewModel, ColumnViewModel? initialColumn)
    {
        var dialog = new AddTaskWindow(viewModel) { Owner = this };
        if (initialColumn is not null) dialog.PreselectColumn(initialColumn);

        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            viewModel.AddCard(dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedWho, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl);
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
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedWho, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl);
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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
            clearViewModel.ClearFilters();
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

    private void DueToday_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.DueFilter = "Today";
    }

    private void DueTomorrow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.DueFilter = "Tomorrow";
    }

    private void DueWithinWeek_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.DueFilter = "Within a Week";
    }

    private void DueNone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.DueFilter = "No Due Date";
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

        // A recurring task that hasn't completed yet (so hasn't already spawned its next occurrence)
        // gets a real three-way choice instead of the plain confirm - deleting it is how you'd "skip"
        // today's instance, and whether the series should keep going is a decision worth asking for
        // every time, not something the ConfirmDelete setting should be able to skip past.
        var offerRecurrenceChoice = card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern) && !card.NextOccurrenceSpawned;
        var spawnNext = false;

        if (offerRecurrenceChoice)
        {
            var result = MessageBox.Show(this,
                $"\"{card.Title}\" is a recurring task.\n\n" +
                "Yes — delete this occurrence, but keep the series going (create the next occurrence now)\n" +
                "No — delete this occurrence and end the recurring series\n" +
                "Cancel — don't delete",
                "Delete Recurring Task", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

            if (result == MessageBoxResult.Cancel) return;
            spawnNext = result == MessageBoxResult.Yes;
        }
        else if (viewModel.ConfirmDelete)
        {
            var result = MessageBox.Show(this, $"Delete \"{card.Title}\"?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as QuickMove_Click — removing the card tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => viewModel.DeleteCard(card, spawnNext)), DispatcherPriority.Background);
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

        OutlookEmailHelper.ComposeCardEmail(this, card, card.WhoEmail!, viewModel);
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
        items.AddRange(viewModel.People.Where(p => p.IsActive).Select(p => (p.Name, card.WhoId == p.Id, (PersonViewModel?)p)));
        ShowQuickEditMenu(element, items, who => viewModel.SetCardWho(card, who));
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

        // Deferred via BeginInvoke: moving the card to another column removes it from this button's
        // own ItemsControl, tearing down the container mid-Click-dispatch — the same deadlock
        // documented on the Priority/Who/Project/Due Date quick-edits above.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            viewModel.MoveCardCommand.Execute((card, targetColumn));
            MaybePromptCompletionNote(card, targetColumn, viewModel);
        }), DispatcherPriority.Background);
    }

    private void Column_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(CardViewModel)) is CardViewModel card &&
            sender is FrameworkElement { DataContext: ColumnViewModel column } &&
            DataContext is MainViewModel viewModel)
        {
            // Deferred via BeginInvoke: same defensive reasoning as QuickMove_Click — this handler
            // still runs nested inside DoDragDrop's own message loop (Drop fires before DoDragDrop
            // returns to Card_MouseMove), so mutating the collection here immediately carries the
            // same class of risk as mutating it from inside a Click handler.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                viewModel.MoveCardCommand.Execute((card, column));
                MaybePromptCompletionNote(card, column, viewModel);
            }), DispatcherPriority.Background);
        }
    }

    // Positional drag-to-reorder within a column, always available - a card dragged over its own
    // current column reorders it (and switches the board into manual sort mode, see
    // MainViewModel.ReorderCardWithinColumn). A drag into a different column keeps using
    // Column_Drop's existing append-style move, so this deliberately returns false (leaving the
    // event unhandled, to bubble up to Column_Drop) for every other case.
    private bool TryGetManualReorderContext(object sender, DragEventArgs e,
        out System.Windows.Controls.ItemsControl itemsControl, out ColumnViewModel column, out CardViewModel draggedCard)
    {
        itemsControl = null!;
        column = null!;
        draggedCard = null!;

        if (sender is not System.Windows.Controls.ScrollViewer { Content: System.Windows.Controls.Grid grid } || grid.DataContext is not ColumnViewModel col) return false;
        if (e.Data.GetData(typeof(CardViewModel)) is not CardViewModel card) return false;
        if (!col.Cards.Contains(card)) return false;

        var ic = grid.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
        if (ic is null) return false;

        itemsControl = ic;
        column = col;
        draggedCard = card;
        return true;
    }

    private void CardsScrollViewer_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCard)) return;

        e.Handled = true;
        e.Effects = DragDropEffects.Move;

        var (_, indicatorY) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCard);
        column.DropIndicatorY = indicatorY;
        column.IsDropIndicatorVisible = true;
    }

    private void CardsScrollViewer_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.ScrollViewer { Content: System.Windows.Controls.Grid grid } scrollViewer || grid.DataContext is not ColumnViewModel column) return;

        // Same spurious-DragLeave guard as the sub-task drag indicator (AddTaskWindow.xaml.cs):
        // only actually hide once the mouse has genuinely left the ScrollViewer's bounds, not just
        // crossed onto a child card that isn't itself drop-enabled.
        var position = e.GetPosition(scrollViewer);
        if (position.X >= 0 && position.X <= scrollViewer.ActualWidth &&
            position.Y >= 0 && position.Y <= scrollViewer.ActualHeight)
        {
            return;
        }

        column.IsDropIndicatorVisible = false;
    }

    private void CardsScrollViewer_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCard)) return;

        e.Handled = true;
        column.IsDropIndicatorVisible = false;
        if (DataContext is not MainViewModel viewModel) return;

        var (newIndex, _) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCard);

        // Deferred via BeginInvoke: same reasoning as Column_Drop/QuickMove_Click above - this
        // handler still runs nested inside DoDragDrop's own message loop, so mutating the Cards
        // collection here immediately carries the same class of risk as mutating it from inside a
        // Click handler.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            viewModel.ReorderCardWithinColumn(draggedCard, column, newIndex);
        }), DispatcherPriority.Background);
    }

    // Returns both where a drop would land (Index, in "before removal" Cards-count space - matching
    // MainViewModel.ReorderCardWithinColumn's own before/after index adjustment) and the Y position
    // (relative to itemsControl) for the insertion-line indicator, so DragOver and Drop always agree.
    private static (int Index, double IndicatorY) GetCardDropTarget(System.Windows.Controls.ItemsControl itemsControl, ColumnViewModel column, Point positionInItemsControl, CardViewModel draggedCard)
    {
        for (var i = 0; i < column.Cards.Count; i++)
        {
            if (ReferenceEquals(column.Cards[i], draggedCard)) continue;
            if (itemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;

            var top = container.TranslatePoint(new Point(0, 0), itemsControl).Y;
            if (positionInItemsControl.Y < top + container.ActualHeight / 2)
            {
                return (i, top);
            }
        }

        if (column.Cards.Count > 0 && itemsControl.ItemContainerGenerator.ContainerFromIndex(column.Cards.Count - 1) is FrameworkElement lastContainer)
        {
            var bottom = lastContainer.TranslatePoint(new Point(0, 0), itemsControl).Y + lastContainer.ActualHeight;
            return (column.Cards.Count, bottom);
        }

        return (0, 0);
    }

    private void MaybePromptCompletionNote(CardViewModel card, ColumnViewModel targetColumn, MainViewModel viewModel)
    {
        if (targetColumn.Name != "Done") return;

        if (card.ForceEditOnComplete)
        {
            EditCard(card, viewModel);
            return;
        }

        if (!viewModel.AddNoteOnComplete) return;

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
