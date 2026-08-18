using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ReminderWindow : Window
{
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush DueTodayBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));

    private readonly Action<CardViewModel> _onOpenTask;
    private readonly Action<CardViewModel> _onMarkDone;
    private readonly Func<CardViewModel, bool> _isStillDue;
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

    public ReminderWindow(List<CardViewModel> dueCards, IEnumerable<ColumnViewModel> columns, Action<CardViewModel> onOpenTask,
        Action<CardViewModel> onMarkDone, Func<CardViewModel, bool> isStillDue)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _onOpenTask = onOpenTask;
        _onMarkDone = onMarkDone;
        _isStillDue = isStillDue;
        _columns = columns.ToList();

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
        return new ReminderRow
        {
            Title = card.Title,
            ProjectName = card.ProjectName,
            WhoName = card.WhoName,
            Priority = card.Priority,
            CategoryName = _columns.FirstOrDefault(c => c.Cards.Contains(card))?.DisplayName ?? string.Empty,
            DueLabel = isOverdue ? $"Overdue since {card.DueDate:MMM d, yyyy}" : "Due today",
            DueLabelBrush = isOverdue ? OverdueBrush : DueTodayBrush
        };
    }

    private void UpdateIntro()
    {
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

        _onMarkDone(card);
        _rowsToCards.Remove(row);
        _rows.Remove(row);
        UpdateIntro();
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
