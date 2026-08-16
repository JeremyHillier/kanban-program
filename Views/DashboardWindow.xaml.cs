using System.Windows;
using System.Windows.Media;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class DashboardWindow : Window
{
    private static readonly Brush HighBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F));
    private static readonly Brush MediumBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x3E));
    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush TodayBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));
    private static readonly Brush ThisWeekBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xC9, 0x3E));
    private static readonly Brush Next30Brush = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5));
    private static readonly Brush LaterBrush = new SolidColorBrush(Color.FromRgb(0x7E, 0x57, 0xC2));
    private static readonly Brush NoDueDateBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    private static readonly Brush[] CategoryPalette =
    [
        new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)), // blue
        new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A)), // teal
        new SolidColorBrush(Color.FromRgb(0x7E, 0x57, 0xC2)), // purple
        new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)), // green
        new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)), // orange
        new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)), // red
        new SolidColorBrush(Color.FromRgb(0xEC, 0x40, 0x7A)), // pink
        new SolidColorBrush(Color.FromRgb(0x8D, 0x6E, 0x63)), // brown
    ];

    private class BarItem
    {
        public required string Label { get; init; }
        public int Count { get; init; }
        public double BarHeight { get; init; }
        public required Brush Brush { get; init; }
    }

    public DashboardWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        var allCards = viewModel.Columns.SelectMany(c => c.Cards).ToList();
        var openCards = viewModel.Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).ToList();
        var doneColumn = viewModel.Columns.FirstOrDefault(c => c.Name == "Done");

        OpenTile.Text = openCards.Count.ToString();
        InDoneTile.Text = (doneColumn?.Cards.Count ?? 0).ToString();

        var today = DateTime.Today;
        OverdueTile.Text = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date < today).ToString();
        DueTodayTile.Text = openCards.Count(c => c.DueDate?.Date == today).ToString();
        DueThisWeekTile.Text = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date >= today && c.DueDate.Value.Date <= today.AddDays(7)).ToString();

        StatusChart.ItemsSource = BuildBars(viewModel.Columns.Select(c => (c.Name, c.Cards.Count, c.Background)));

        PriorityChart.ItemsSource = BuildBars(
        [
            ("High", allCards.Count(c => c.Priority == "High"), HighBrush),
            ("Medium", allCards.Count(c => c.Priority == "Medium"), MediumBrush),
            ("Normal", allCards.Count(c => c.Priority == "Normal"), NormalBrush)
        ]);

        var overdue = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date < today);
        var dueToday = openCards.Count(c => c.DueDate?.Date == today);
        var thisWeek = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date > today && c.DueDate.Value.Date <= today.AddDays(7));
        var next30 = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date > today.AddDays(7) && c.DueDate.Value.Date <= today.AddDays(30));
        var later = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date > today.AddDays(30));
        var noDueDate = openCards.Count(c => c.DueDate is null);

        TimelineChart.ItemsSource = BuildBars(
        [
            ("Overdue", overdue, OverdueBrush),
            ("Today", dueToday, TodayBrush),
            ("This Week", thisWeek, ThisWeekBrush),
            ("Next 30 Days", next30, Next30Brush),
            ("Later", later, LaterBrush),
            ("No Due Date", noDueDate, NoDueDateBrush)
        ]);

        var projectGroups = allCards.GroupBy(c => c.ProjectName)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select((g, i) => (g.Key, g.Count(), CategoryPalette[i % CategoryPalette.Length]));
        ProjectChart.ItemsSource = BuildBars(projectGroups);

        var whoGroups = allCards.GroupBy(c => c.WhoName)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select((g, i) => (g.Key, g.Count(), CategoryPalette[i % CategoryPalette.Length]));
        WhoChart.ItemsSource = BuildBars(whoGroups);
    }

    private static List<BarItem> BuildBars(IEnumerable<(string Label, int Count, Brush Brush)> data, double maxHeight = 140)
    {
        var list = data.ToList();
        var maxCount = list.Count == 0 ? 0 : list.Max(d => d.Count);

        return list.Select(d => new BarItem
        {
            Label = d.Label,
            Count = d.Count,
            Brush = d.Brush,
            BarHeight = maxCount == 0 || d.Count == 0 ? 0 : Math.Max(4, d.Count / (double)maxCount * maxHeight)
        }).ToList();
    }
}
