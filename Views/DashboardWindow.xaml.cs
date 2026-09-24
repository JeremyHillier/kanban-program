using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using static KanbanApp.Views.DashboardCharts;

namespace KanbanApp.Views;

// The Dashboard: headline tiles, then chart cards two to a row. The numbers come from
// DashboardData; the colours from DashboardPalette (checked for colour-blind separation, with
// their own dark-mode steps); the drawing from DashboardCharts.
public partial class DashboardWindow : Window
{
    private readonly DashboardPalette _palette;

    public DashboardWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _palette = new DashboardPalette(viewModel.IsDarkMode);

        var cards = viewModel.Columns
            .SelectMany(c => c.Cards.Select(card => new DashboardData.BoardCard(card, c.Name, c.DisplayName)))
            .ToList();
        var columns = viewModel.Columns.Select(c => new DashboardData.Status(c.Name, c.DisplayName)).ToList();
        var data = DashboardData.Build(cards, columns, viewModel.GetArchivedCompletionDates(), DateTime.Today,
            CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

        AsOfText.Text = $"As of {DateTime.Now:MMM d, yyyy h:mm tt}";
        BuildTiles(data);
        BuildCharts(data);
    }

    private void BuildTiles(DashboardData data)
    {
        AddTile(data.Open, "Open", "Tasks not in Done");
        AddTile(data.Overdue, "Overdue", "Open tasks past their due date", data.Overdue > 0 ? _palette.DueDates[0] : null);
        AddTile(data.DueToday, "Due Today", "Open tasks due today", data.DueToday > 0 ? _palette.DueDates[1] : null);
        AddTile(data.DueThisWeek, "Due This Week", "Open tasks due today or in the next 7 days");
        AddTile(data.WaitingOn, "Waiting On", "Open tasks waiting on someone or something");
        AddTile(data.DoneLast7Days, "Done in 7 Days", "Tasks completed in the last 7 days, including any since archived");
        AddTile(data.InDone, "In Done", "Tasks in the Done column");
    }

    // The number stays in the text colour; a small dot beside the label flags one that needs attention.
    private void AddTile(int value, string label, string toolTip, Brush? flag = null)
    {
        var number = new TextBlock { Text = value.ToString(), FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        number.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");

        // One wrapping line of text (with the dot inside it), so a narrow window wraps the label
        // rather than cutting it off.
        var caption = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        if (flag is not null)
        {
            caption.Inlines.Add(new System.Windows.Documents.InlineUIContainer(
                new Ellipse { Width = 8, Height = 8, Fill = flag, Margin = new Thickness(0, 0, 5, 1) }) { BaselineAlignment = BaselineAlignment.Center });
        }
        caption.Inlines.Add(new System.Windows.Documents.Run(label));

        var tile = new Border
        {
            CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(8, 10, 8, 10),
            Margin = new Thickness(TilesPanel.Children.Count == 0 ? 0 : 5, 0, TilesPanel.Children.Count == 6 ? 0 : 5, 0),
            ToolTip = toolTip,
            Child = new StackPanel { Children = { number, caption } }
        };
        tile.SetResourceReference(Border.BackgroundProperty, "PanelBackgroundBrush");
        tile.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        TilesPanel.Children.Add(tile);
    }

    private void BuildCharts(DashboardData data)
    {
        // Open work: every column, each split by priority.
        var priorityBrushes = data.Priorities.Select(p => (Name: p, Brush: _palette.Priority(p))).ToList();
        var byPriority = data.StatusByPriority.Select(s => new Bar(s.Label,
            s.Counts.Select((n, i) => new Segment(priorityBrushes[i].Name, n, priorityBrushes[i].Brush)).ToList())).ToList();
        var statusCard = Card("Tasks by Status and Priority", "Every task on the board, by column",
            Columns(byPriority), Legend(priorityBrushes));

        var dueBrushes = _palette.DueDates;
        var dueCard = Card("When Open Tasks Are Due", $"{data.Open} open tasks",
            Columns(data.DueDates.Select((b, i) => new Bar(b.Label, [new Segment(b.Label, b.Count, dueBrushes[i])])).ToList(), barWidth: 32));

        // Throughput: completions per week. Only the latest week and the busiest carry numbers; hover for the rest.
        var weeks = data.CompletedPerWeek;
        var busiest = weeks.Count == 0 ? -1 : weeks.Select((w, i) => (w.Count, i)).Max().i;
        var weekBars = weeks.Select((w, i) => new Bar($"{w.Start:MMM}\n{w.Start:%d}",[new Segment("Completed", w.Count, _palette.Series)],
            $"{(i == weeks.Count - 1 ? "This week, so far" : $"Week of {w.Start:MMM d}")}: {w.Count} completed")).ToList();
        var total = weeks.Sum(w => w.Count);
        var average = weeks.Count > 1 ? weeks.Take(weeks.Count - 1).Average(w => w.Count) : 0; // the current week isn't over
        var throughputCard = Card("Completed per Week",
            $"Last {DashboardData.WeeksShown} weeks: {total} completed, about {average:0.#} a week. The last bar is this week so far.",
            Columns(weekBars, barWidth: 18, labelEvery: 2, showTotal: i => i == weeks.Count - 1 || (i == busiest && weeks[i].Count > 0)));

        var ageBrushes = _palette.LastUpdated;
        var ageCard = Card("Open Tasks by Last Update", "How long since each open task was last changed",
            Columns(data.LastUpdated.Select((b, i) => new Bar(b.Label, [new Segment(b.Label, b.Count, ageBrushes[i])])).ToList(), barWidth: 40));

        // Projects and people as horizontal bars - long names read properly and nothing wraps.
        var statusBrushes = data.Statuses.Select(s => (Name: s.DisplayName, Brush: _palette.Status(s.Name))).ToList();
        var projectBars = data.ProjectsByStatus.Select(p => new Bar(p.Label,
            p.Counts.Select((n, i) => new Segment(statusBrushes[i].Name, n, statusBrushes[i].Brush)).ToList())).ToList();
        var projectCard = Card("Tasks by Project",
            data.FoldedProjectCount > 0 ? $"Every task on the board. The {data.FoldedProjectCount} smallest projects are grouped as Other." : "Every task on the board, by column",
            projectBars.Count == 0 ? Empty() : Rows(projectBars), Legend(statusBrushes));

        var openBrushes = data.OpenStatuses.Select(s => (Name: s.DisplayName, Brush: _palette.Status(s.Name))).ToList();
        var peopleBars = data.PeopleByStatus.Select(p => new Bar(p.Label,
            p.Counts.Select((n, i) => new Segment(openBrushes[i].Name, n, openBrushes[i].Brush)).ToList())).ToList();
        var peopleCard = Card("Open Tasks by Person", "A task shared by several people counts once for each of them",
            peopleBars.Count == 0 ? Empty() : Rows(peopleBars), Legend(openBrushes));

        AddRow(statusCard, dueCard);
        AddRow(throughputCard, ageCard);
        AddRow(projectCard);
        AddRow(peopleCard);
    }

    private static TextBlock Empty()
    {
        var text = new TextBlock { Text = "No tasks yet.", FontSize = 12 };
        text.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        return text;
    }

    // Two cards side by side, or one across the full width.
    private void AddRow(FrameworkElement left, FrameworkElement? right = null)
    {
        var row = ChartsGrid.RowDefinitions.Count;
        ChartsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(left, row);
        if (right is null) Grid.SetColumnSpan(left, 3);
        ChartsGrid.Children.Add(left);

        if (right is null) return;
        right.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(right, row);
        Grid.SetColumn(right, 2);
        ChartsGrid.Children.Add(right);
    }
}
