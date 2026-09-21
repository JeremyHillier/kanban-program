using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ReminderWindow : Window
{
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush DueTodayBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));

    private readonly Action<CardViewModel> _onOpenTask;
    private readonly Action<CardViewModel> _onMarkDone;
    private readonly Func<CardViewModel, bool> _isStillDue;
    private readonly bool _isTimeAlert;
    private readonly Action<List<CardViewModel>, TimeSpan>? _onSnooze;
    private readonly List<ColumnViewModel> _columns;
    private readonly Dictionary<ReminderRow, CardViewModel> _rowsToCards = [];
    private readonly ObservableCollection<ReminderRow> _rows = [];

    private class ReminderRow
    {
        public required string Title { get; init; }
        public required string ProjectName { get; init; }
        public required string WhoName { get; init; }
        public required string Priority { get; init; }
        public required string CategoryName { get; init; }
        public required string DueLabel { get; init; }
        public required Brush DueLabelBrush { get; init; }
    }

    // isTimeAlert: the same list, raised mid-session by the due-time timer for tasks whose time has
    // just arrived, rather than the startup/on-demand overdue-and-due-today roundup.
    // onSnooze: given only for time alerts; receives the tasks still listed and the chosen duration.
    public ReminderWindow(List<CardViewModel> dueCards, IEnumerable<ColumnViewModel> columns, Action<CardViewModel> onOpenTask,
        Action<CardViewModel> onMarkDone, Func<CardViewModel, bool> isStillDue, bool isTimeAlert = false,
        Action<List<CardViewModel>, TimeSpan>? onSnooze = null)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _onOpenTask = onOpenTask;
        _onMarkDone = onMarkDone;
        _isStillDue = isStillDue;
        _isTimeAlert = isTimeAlert;
        _onSnooze = onSnooze;
        _columns = columns.ToList();
        if (isTimeAlert) Title = "Task Due Now";

        if (onSnooze is not null)
        {
            SnoozeButton.Visibility = Visibility.Visible;
            var menu = new ContextMenu { PlacementTarget = SnoozeButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
            foreach (var (label, duration) in TimeAlertTracker.SnoozeOptions)
            {
                var item = new MenuItem { Header = label, Tag = duration };
                item.Click += SnoozeOption_Click;
                menu.Items.Add(item);
            }
            SnoozeButton.ContextMenu = menu;
        }

        foreach (var card in dueCards)
        {
            var row = BuildRow(card);
            _rows.Add(row);
            _rowsToCards[row] = card;
        }

        UpdateIntro();
        ReminderList.ItemsSource = _rows;
        ReminderList.MouseDoubleClick += ReminderList_MouseDoubleClick;
    }

    private ReminderRow BuildRow(CardViewModel card)
    {
        var isOverdue = card.DueDate!.Value.Date < DateTime.Today;
        var dueTodayLabel = card.DueDateTime is { } dueAt ? $"Due today at {dueAt:h:mm tt}" : "Due today";
        return new ReminderRow
        {
            Title = card.Title,
            ProjectName = card.ProjectName,
            WhoName = card.WhoName,
            Priority = card.Priority,
            CategoryName = _columns.FirstOrDefault(c => c.Cards.Contains(card))?.DisplayName ?? string.Empty,
            DueLabel = isOverdue ? $"Overdue since {card.DueDateTime?.ToString("MMM d, yyyy h:mm tt") ?? card.DueDate.Value.ToString("MMM d, yyyy")}" : dueTodayLabel,
            DueLabelBrush = isOverdue || _isTimeAlert ? OverdueBrush : DueTodayBrush
        };
    }

    private void UpdateIntro()
    {
        if (_isTimeAlert)
        {
            SnoozeButton.IsEnabled = _rows.Count > 0;
            IntroText.Text = _rows.Count == 0
                ? "All caught up."
                : $"{(_rows.Count == 1 ? "This task's" : $"These {_rows.Count} tasks'")} due time has arrived. Check a task off to mark it Done, double-click to open it, or Snooze to be reminded again later.";
            return;
        }

        var overdueCount = _rows.Count(r => ReferenceEquals(r.DueLabelBrush, OverdueBrush));
        var todayCount = _rows.Count - overdueCount;

        var parts = new List<string>();
        if (overdueCount > 0) parts.Add($"{overdueCount} overdue task{(overdueCount == 1 ? "" : "s")}");
        if (todayCount > 0) parts.Add($"{todayCount} task{(todayCount == 1 ? "" : "s")} due today");

        IntroText.Text = parts.Count == 0
            ? "All caught up."
            : $"You have {string.Join(" and ", parts)}. Check a task off to mark it Done, or double-click to open it.";
    }

    private void ReminderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<CheckBox>(source) is not null) return;
        if (ReminderList.SelectedItem is not ReminderRow row || !_rowsToCards.TryGetValue(row, out var card)) return;

        // Deliberately left open: the user may want to review or act on other reminders after this one.
        _onOpenTask(card);
        RefreshRow(row, card);
    }

    private void RefreshRow(ReminderRow row, CardViewModel card)
    {
        if (!_isStillDue(card))
        {
            _rowsToCards.Remove(row);
            _rows.Remove(row);
            UpdateIntro();
            return;
        }

        var index = _rows.IndexOf(row);
        if (index < 0) return; // Row already gone (e.g. removed via the checkbox) — nothing to refresh.

        var updatedRow = BuildRow(card);
        _rowsToCards.Remove(row);
        _rows[index] = updatedRow;
        _rowsToCards[updatedRow] = card;
        UpdateIntro();
    }

    private void MarkDoneCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ReminderRow row } || !_rowsToCards.TryGetValue(row, out var card)) return;

        // Deferred via BeginInvoke: removing the row tears down this checkbox's own container
        // mid-Checked-dispatch — the same WPF deadlock documented on the board's quick-edit popups.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _onMarkDone(card);
            _rowsToCards.Remove(row);
            _rows.Remove(row);
            UpdateIntro();
        }), DispatcherPriority.Background);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Snooze_Click(object sender, RoutedEventArgs e) => SnoozeButton.ContextMenu!.IsOpen = true;

    private void SnoozeOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: TimeSpan duration } || _onSnooze is null) return;

        var cards = _rows.Select(row => _rowsToCards[row]).ToList();

        // Deferred via BeginInvoke: closing the window from inside the menu's own Click, while that
        // menu is still closing, is the same re-entrancy the board's quick-edit menus steer around.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _onSnooze(cards, duration);
            Close();
        }), DispatcherPriority.Background);
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match) return match;
            // Text inside a TextBlock (a Run) is not a Visual, and GetParent throws on it.
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
