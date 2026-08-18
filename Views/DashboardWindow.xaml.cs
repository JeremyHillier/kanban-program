using System.Windows;
using System.Windows.Media;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class DashboardWindow : Window
{
    private static readonly Brush HighBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly Brush MediumBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
    private static readonly Brush LowBrush = new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6));

    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly Brush TodayBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
    private static readonly Brush ThisWeekBrush = new SolidColorBrush(Color.FromRgb(0xFD, 0xD8, 0x35));
    private static readonly Brush Next30Brush = new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6));
    private static readonly Brush LaterBrush = new SolidColorBrush(Color.FromRgb(0x7E, 0x57, 0xC2));
    private static readonly Brush NoDueDateBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C));

    private static readonly Brush[] CategoryPalette =
    [
        new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)), // blue
        new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A)), // teal
        new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC)), // purple
        new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)), // green
        new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00)), // orange
        new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)), // red
        new SolidColorBrush(Color.FromRgb(0xEC, 0x40, 0x7A)), // pink
        new SolidColorBrush(Color.FromRgb(0x8D, 0x6E, 0x63)), // brown
    ];

    // Fixed, vivid per-status colors for chart use — deliberately independent of the pastel
    // kanban column background colors, which are far too low-contrast (near-black in dark mode)
    // to double as chart fill colors.
    private static readonly Dictionary<string, Brush> StatusPalette = new()
    {
        ["To Do"] = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
        ["In Progress"] = new SolidColorBrush(Color.FromRgb(0xFD, 0xD8, 0x35)),
        ["On Hold"] = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00)),
        ["Waiting"] = new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC)),
        ["Done"] = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
    };
    private static readonly Brush FallbackStatusBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    private static readonly (string Name, Brush Brush)[] PriorityLegend =
    [
        ("High", HighBrush), ("Medium", MediumBrush), ("Normal", NormalBrush), ("Low", LowBrush)
    ];

    private class BarItem
    {
        public required string Label { get; init; }
        public int Count { get; init; }
        public double BarHeight { get; init; }
        public required Brush Brush { get; init; }
    }

    private class SegmentItem
    {
        public double SegmentHeight { get; init; }
        public required Brush Brush { get; init; }
    }

    private class StackedBarItem
    {
        public required string Label { get; init; }
        public int Total { get; init; }
        public required List<SegmentItem> Segments { get; init; }
    }

    public DashboardWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        var cardsWithColumn = viewModel.Columns.SelectMany(c => c.Cards.Select(card => (Card: card, ColumnName: c.Name, ColumnDisplayName: c.DisplayName))).ToList();
        var allCards = cardsWithColumn.Select(x => x.Card).ToList();
        var openCards = cardsWithColumn.Where(x => x.ColumnName != "Done").Select(x => x.Card).ToList();
        var doneColumn = viewModel.Columns.FirstOrDefault(c => c.Name == "Done");

        OpenTile.Text = openCards.Count.ToString();
        InDoneTile.Text = (doneColumn?.Cards.Count ?? 0).ToString();

        var today = DateTime.Today;
        OverdueTile.Text = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date < today).ToString();
        DueTodayTile.Text = openCards.Count(c => c.DueDate?.Date == today).ToString();
        DueThisWeekTile.Text = openCards.Count(c => c.DueDate is not null && c.DueDate.Value.Date >= today && c.DueDate.Value.Date <= today.AddDays(7)).ToString();

        // Status Distribution + Priority Mix combined: one stacked bar per status, segments by priority.
        StatusByPriorityChart.ItemsSource = BuildStackedBars(
            viewModel.Columns.Select(c => c.DisplayName),
            status => PriorityLegend.Select(p => (p.Name, cardsWithColumn.Count(x => x.ColumnDisplayName == status && x.Card.Priority == p.Name), p.Brush)));

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

        // By Project + Status combined: one stacked bar per project, segments by status.
        var projectNames = allCards.GroupBy(c => c.ProjectName)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .ToList();
        var statusOrder = viewModel.Columns.Select(c => (c.Name, c.DisplayName)).ToList();
        ProjectByStatusChart.ItemsSource = BuildStackedBars(
            projectNames,
            project => statusOrder.Select(status =>
                (status.DisplayName, cardsWithColumn.Count(x => x.Card.ProjectName == project && x.ColumnName == status.Name),
                 StatusPalette.GetValueOrDefault(status.Name, FallbackStatusBrush))));

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

    private static List<StackedBarItem> BuildStackedBars(IEnumerable<string> labels,
        Func<string, IEnumerable<(string Name, int Count, Brush Brush)>> segmentSelector, double maxHeight = 140)
    {
        var rows = labels.Select(l => (Label: l, Segments: segmentSelector(l).ToList())).ToList();
        var maxTotal = rows.Count == 0 ? 0 : rows.Max(r => r.Segments.Sum(s => s.Count));

        return rows.Select(r => new StackedBarItem
        {
            Label = r.Label,
            Total = r.Segments.Sum(s => s.Count),
            Segments = r.Segments
                .Where(s => s.Count > 0)
                .Select(s => new SegmentItem
                {
                    Brush = s.Brush,
                    SegmentHeight = maxTotal == 0 ? 0 : Math.Max(2, s.Count / (double)maxTotal * maxHeight)
                }).ToList()
        }).ToList();
    }
}
