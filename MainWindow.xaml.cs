using System.Globalization;
using System.Windows;
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
        if (DataContext is not MainViewModel viewModel || !viewModel.ShowDueReminders) return;

        var dueCards = viewModel.GetDueReminders();
        if (dueCards.Count == 0) return;

        // Deferred: showing the modal reminder dialog synchronously here would block before the
        // splash screen (still on screen at this point) gets a chance to close.
        Dispatcher.BeginInvoke(() => ShowReminders(dueCards, viewModel), DispatcherPriority.ApplicationIdle);
    }

    private void ShowReminders(List<CardViewModel> dueCards, MainViewModel viewModel)
    {
        var dialog = new ReminderWindow(dueCards, card => EditCard(card, viewModel), card => MarkCardDone(card, viewModel),
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
                forceEditOnComplete: dialog.ForceEditOnComplete);
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
                forceEditOnComplete: dialog.ForceEditOnComplete);
        }
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
                }
                break;
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
        var dialog = new HelpWindow { Owner = this };
        dialog.ShowDialog();
    }

    private void ReportBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var dialog = new ReportBuilderWindow(viewModel) { Owner = this };
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

    private void SortByProject_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByProject();
    }

    private void SortByDueDate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByDueDate();
    }

    private void SortByWho_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByWho();
    }

    private void SortByPriority_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SortByPriority();
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

        if (viewModel.ConfirmDelete)
        {
            var result = MessageBox.Show(this, $"Delete \"{card.Title}\"?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        viewModel.DeleteCardCommand.Execute(card);
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

        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var flag in available)
        {
            var menuItem = new System.Windows.Controls.MenuItem { Header = flag.Name };
            menuItem.Click += (_, _) => viewModel.AddFlagToCard(card, flag);
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = element;
        menu.IsOpen = true;
    }

    private void PriorityBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var priority in new[] { "High", "Medium", "Normal", "Low" })
        {
            var menuItem = new System.Windows.Controls.MenuItem { Header = priority, IsChecked = card.Priority == priority };
            // Deferred via BeginInvoke: mutating the card collection (ApplySort) synchronously from inside
            // a MenuItem.Click handler tears down the popup's PlacementTarget while it's still closing,
            // which deadlocks WPF's layout engine. Running it after the menu has actually closed avoids that.
            menuItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => viewModel.SetCardPriority(card, priority)), DispatcherPriority.Background);
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = element;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void WhoDisplay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CardViewModel card } element || DataContext is not MainViewModel viewModel) return;

        var menu = new System.Windows.Controls.ContextMenu();
        var unassignedItem = new System.Windows.Controls.MenuItem { Header = "Unassigned", IsChecked = card.WhoId is null };
        unassignedItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => viewModel.SetCardWho(card, null)), DispatcherPriority.Background);
        menu.Items.Add(unassignedItem);

        foreach (var person in viewModel.People.Where(p => p.IsActive))
        {
            var menuItem = new System.Windows.Controls.MenuItem { Header = person.Name, IsChecked = card.WhoId == person.Id };
            menuItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => viewModel.SetCardWho(card, person)), DispatcherPriority.Background);
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = element;
        menu.IsOpen = true;
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
        var clearButton = new System.Windows.Controls.Button
        {
            Content = "Clear Due Date",
            Margin = new Thickness(8, 0, 8, 8),
            Padding = new Thickness(4)
        };

        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(datePicker);
        panel.Children.Add(clearButton);

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = element,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = panel
            }
        };

        // Deferred via BeginInvoke: see the comment in PriorityBadge_MouseLeftButtonDown — mutating the
        // card collection while this popup is still closing deadlocks WPF's layout engine.
        datePicker.SelectedDateChanged += (_, _) =>
        {
            var newDate = datePicker.SelectedDate;
            popup.IsOpen = false;
            Dispatcher.BeginInvoke(new Action(() => viewModel.SetCardDueDate(card, newDate)), DispatcherPriority.Background);
        };
        clearButton.Click += (_, _) =>
        {
            popup.IsOpen = false;
            Dispatcher.BeginInvoke(new Action(() => viewModel.SetCardDueDate(card, null)), DispatcherPriority.Background);
        };
        panel.PreviewKeyDown += (_, keyArgs) =>
        {
            if (keyArgs.Key != Key.Escape) return;
            popup.IsOpen = false;
            keyArgs.Handled = true;
        };
        popup.Opened += (_, _) => datePicker.Focus();

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

        viewModel.MoveCardCommand.Execute((card, targetColumn));
        MaybePromptCompletionNote(card, targetColumn, viewModel);
    }

    private void Column_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(CardViewModel)) is CardViewModel card &&
            sender is FrameworkElement { DataContext: ColumnViewModel column } &&
            DataContext is MainViewModel viewModel)
        {
            viewModel.MoveCardCommand.Execute((card, column));
            MaybePromptCompletionNote(card, column, viewModel);
        }
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
}
