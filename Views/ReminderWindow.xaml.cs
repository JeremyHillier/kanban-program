using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ReminderWindow : Window
{
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush DueTodayBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));

    private readonly Action<CardViewModel> _onOpenTask;
    private readonly Dictionary<ReminderRow, CardViewModel> _rowsToCards = [];

    private class ReminderRow
    {
        public required string Title { get; init; }
        public required string ProjectName { get; init; }
        public required string DueLabel { get; init; }
        public required Brush DueLabelBrush { get; init; }
    }

    public ReminderWindow(List<CardViewModel> dueCards, Action<CardViewModel> onOpenTask)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _onOpenTask = onOpenTask;

        var overdueCount = dueCards.Count(c => c.DueDate!.Value.Date < DateTime.Today);
        var todayCount = dueCards.Count - overdueCount;
        IntroText.Text = BuildIntro(overdueCount, todayCount);

        var rows = new List<ReminderRow>();
        foreach (var card in dueCards)
        {
            var isOverdue = card.DueDate!.Value.Date < DateTime.Today;
            var row = new ReminderRow
            {
                Title = card.Title,
                ProjectName = card.ProjectName,
                DueLabel = isOverdue ? $"Overdue since {card.DueDate:MMM d, yyyy}" : "Due today",
                DueLabelBrush = isOverdue ? OverdueBrush : DueTodayBrush
            };
            rows.Add(row);
            _rowsToCards[row] = card;
        }

        ReminderList.ItemsSource = rows;
        ReminderList.MouseDoubleClick += ReminderList_MouseDoubleClick;
    }

    private static string BuildIntro(int overdueCount, int todayCount)
    {
        var parts = new List<string>();
        if (overdueCount > 0) parts.Add($"{overdueCount} overdue task{(overdueCount == 1 ? "" : "s")}");
        if (todayCount > 0) parts.Add($"{todayCount} task{(todayCount == 1 ? "" : "s")} due today");

        return parts.Count == 0
            ? "No overdue or due-today tasks."
            : $"You have {string.Join(" and ", parts)}. Double-click a task to open it.";
    }

    private void ReminderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ReminderList.SelectedItem is not ReminderRow row || !_rowsToCards.TryGetValue(row, out var card)) return;

        Close();
        _onOpenTask(card);
    }
}
