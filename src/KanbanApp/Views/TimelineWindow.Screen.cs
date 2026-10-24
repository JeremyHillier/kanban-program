using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Drawing the Timeline on screen: the date header, a row per project or goal, its Summary bar, and
// each task as a box or an arrow laid out by TimelineLayout.
public partial class TimelineWindow
{
    // Rebuilds both grids from scratch on every navigation/toggle rather than trying to update them
    // in place - the row/column count changes with the data, so an incremental update would need the
    // same "figure out which rows/columns are needed" logic anyway. The header row lives in HeaderGrid
    // (its own frozen ScrollViewer) and the rows live in TimelineGrid (the scrollable body) - both get
    // identical column definitions built by AddColumns so their cells stay lined up.
    private void BuildGrid()
    {
        var brush = (Brush)FindResource("PrimaryTextBrush");
        var secondaryBrush = (Brush)FindResource("SecondaryTextBrush");
        var borderBrush = (Brush)FindResource("CardBorderBrush");
        var panelBrush = (Brush)FindResource("PanelBackgroundBrush");
        var alternateRowBrush = (Brush)FindResource("AlternateRowBrush");

        var stepLabel = TimelineScale.StepLabel(Zoom, _months);
        FiltersButton.Content = _filter.IsEmpty ? "Filters..." : $"Filters ({_filter.Count})...";
        FiltersButton.ToolTip = _filter.IsEmpty ? "Show only some projects, goals, people, priorities or flags" : $"Showing only: {_filter.Describe(_viewModel.People, _viewModel.Flags, _viewModel.Projects, _viewModel.Goals)}";
        PrevButton.Content = $"◀ {stepLabel}";
        NextButton.Content = $"{stepLabel} ▶";

        var (rows, unitStarts, rangeEnd) = GetVisibleData();
        var unitsToShow = unitStarts.Count;
        RangeLabel.Text = Zoom == TimelineZoom.Month
            ? $"{_windowStart:MMMM yyyy} – {rangeEnd.AddDays(-1):MMMM yyyy}"
            : $"{_windowStart:MMM d} – {rangeEnd.AddDays(-1):MMM d, yyyy}";

        HeaderGrid.Children.Clear();
        HeaderGrid.RowDefinitions.Clear();
        HeaderGrid.ColumnDefinitions.Clear();

        TimelineGrid.Children.Clear();
        TimelineGrid.RowDefinitions.Clear();
        TimelineGrid.ColumnDefinitions.Clear();

        const double nameColWidth = 170;
        // Month columns narrow as more months are shown, so a year or two still fits a wide screen.
        var unitColWidth = Zoom switch { TimelineZoom.Day => 90, TimelineZoom.Month => _months <= 6 ? 180 : _months <= 12 ? 120 : 90, _ => 150 };

        void AddColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(nameColWidth) });
            for (var w = 0; w < unitsToShow; w++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitColWidth) });
            }
        }
        AddColumns(HeaderGrid);
        AddColumns(TimelineGrid);

        HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // The line between columns (weeks, months, days) is a little heavier than the light lines inside them.
        void AddCell(Grid grid, int row, int col, UIElement content, Brush? background = null)
        {
            var border = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, col == 0 ? 1 : 1.75, 1),
                Background = background,
                Child = content
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        // The light lines inside a column: each day of a week, each Monday of a month.
        var subLines = Enumerable.Range(0, unitsToShow)
            .Select(w => TimelineLayout.SubDivisions(Zoom, unitStarts[w], w + 1 < unitsToShow ? unitStarts[w + 1] : rangeEnd))
            .ToList();
        var subLineBrush = new SolidColorBrush(((SolidColorBrush)borderBrush).Color) { Opacity = 0.45 };
        UIElement SubLines(int w)
        {
            var lines = new Grid();
            foreach (var fraction in subLines[w])
            {
                lines.Children.Add(new Rectangle
                {
                    Width = 1, Fill = subLineBrush, HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(fraction * unitColWidth - 0.5, 0, 0, 0)
                });
            }
            return lines;
        }

        // Where today falls, if it is showing: a thin blue line down every row, so the plan reads against now.
        var todayAt = DateTime.Today >= unitStarts[0] && DateTime.Today < rangeEnd
            ? TimelineLayout.Offset(DateTime.Today, unitStarts, rangeEnd) + 0.5 * (TimelineLayout.EndOffset(DateTime.Today, unitStarts, rangeEnd) - TimelineLayout.Offset(DateTime.Today, unitStarts, rangeEnd))
            : (double?)null;

        AddCell(HeaderGrid, 0, 0, new TextBlock
        {
            Text = GroupHeading, FontWeight = FontWeights.Bold, Foreground = brush,
            Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center
        }, panelBrush);

        for (var w = 0; w < unitsToShow; w++)
        {
            AddCell(HeaderGrid, 0, w + 1, new TextBlock
            {
                Text = UnitLabel(unitStarts[w], print: false), FontWeight = FontWeights.Bold, Foreground = brush,
                Margin = new Thickness(4, 6, 4, 6), VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            }, panelBrush);
        }

        // A second header row naming the light lines: the days of each week (M T W ...) in Week view, the date of
        // each Monday in Month view when there is room for it. Day view has no inner lines, so no second row.
        if (Zoom != TimelineZoom.Day && (Zoom == TimelineZoom.Week || unitColWidth >= 100))
        {
            HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddCell(HeaderGrid, 1, 0, new TextBlock
            {
                Text = Zoom == TimelineZoom.Week ? "Days" : "Weeks from", FontSize = 10, Foreground = secondaryBrush, Margin = new Thickness(6, 2, 6, 2)
            }, panelBrush);
            for (var w = 0; w < unitsToShow; w++)
            {
                var labels = new Grid { Height = 16 };
                var unitStart = unitStarts[w];
                var unitEnd = w + 1 < unitsToShow ? unitStarts[w + 1] : rangeEnd;
                var length = (unitEnd - unitStart).TotalDays;
                for (var d = 0; d < length; d++)
                {
                    var day = unitStart.AddDays(d);
                    if (Zoom == TimelineZoom.Month && (day.DayOfWeek != DayOfWeek.Monday || d / length * unitColWidth > unitColWidth - 16)) continue; // no room for a late Monday

                    labels.Children.Add(new TextBlock
                    {
                        Text = Zoom == TimelineZoom.Week ? day.ToString("ddd")[..1] : day.Day.ToString(), FontSize = 9.5, Foreground = secondaryBrush,
                        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(d / length * unitColWidth + (Zoom == TimelineZoom.Week ? unitColWidth / length / 2 - 3 : 2), 0, 0, 0),
                        ToolTip = day.ToString("dddd, MMM d, yyyy")
                    });
                }
                var cell = new Grid();
                cell.Children.Add(SubLines(w));
                cell.Children.Add(labels);
                AddCell(HeaderGrid, 1, w + 1, cell, panelBrush);
            }
        }

        if (rows.Count == 0)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });
            AddCell(TimelineGrid, 0, 0, new TextBlock
            {
                Text = EmptyText, Foreground = secondaryBrush,
                FontStyle = FontStyles.Italic, Margin = new Thickness(6), TextWrapping = TextWrapping.Wrap
            });
            for (var w = 0; w < unitsToShow; w++) AddCell(TimelineGrid, 0, w + 1, SubLines(w));
            return;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = row.Summary is not null && !row.IsExpanded ? 44 : 50 });

            // Alternating shading, matching the app's existing AlternateRowBrush convention.
            var rowBackground = r % 2 == 1 ? alternateRowBrush : null;

            AddCell(TimelineGrid, r, 0, BuildNameCell(row, brush, secondaryBrush), rowBackground);

            // The row's date cells are just the background and grid lines; the bar and the tasks sit
            // in a lane grid laid over them (same column widths, one auto-height row per lane),
            // because a bar or an arrow has to run across several columns.
            for (var w = 0; w < unitsToShow; w++) AddCell(TimelineGrid, r, w + 1, SubLines(w), rowBackground);

            var laneGrid = new Grid { Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            for (var w = 0; w < unitsToShow; w++) laneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitColWidth) });
            for (var lane = 0; lane < row.Lanes; lane++)
                laneGrid.RowDefinitions.Add(new RowDefinition { Height = row.HasMilestoneLane && lane == row.Lanes - 1 ? new GridLength(18) : GridLength.Auto });
            Grid.SetRow(laneGrid, r);
            Grid.SetColumn(laneGrid, 1);
            Grid.SetColumnSpan(laneGrid, unitsToShow);
            TimelineGrid.Children.Add(laneGrid);

            if (row.Summary is { } summary)
            {
                var bar = BuildSummaryBar(row, summary, unitColWidth);
                Grid.SetRow(bar, 0);
                Grid.SetColumn(bar, summary.FirstUnit);
                Grid.SetColumnSpan(bar, summary.LastUnit - summary.FirstUnit + 1);
                laneGrid.Children.Add(bar);
            }

            if (row.Target is { } target && TimelineLayout.TargetPosition(target, unitStarts, rangeEnd) is { } at)
            {
                var marker = BuildTargetMarker(row, target, at.Fraction, unitColWidth);
                Grid.SetRow(marker, 0);
                Grid.SetRowSpan(marker, row.Lanes);
                Grid.SetColumn(marker, at.Unit);
                laneGrid.Children.Add(marker);

                var leadIn = BuildTargetLeadIn(at.Fraction, unitColWidth);
                Grid.SetRow(leadIn, 0);
                Grid.SetColumn(leadIn, 0);
                Grid.SetColumnSpan(leadIn, at.Unit + 1);
                laneGrid.Children.Insert(0, leadIn);
            }
            else if (row.Target is { } later && later.Date >= rangeEnd)
            {
                // The target is beyond the right edge: the line runs right across, ending in an arrowhead.
                var leadIn = BuildTargetLeadIn(1, unitColWidth, toEdge: true);
                Grid.SetRow(leadIn, 0);
                Grid.SetColumn(leadIn, 0);
                Grid.SetColumnSpan(leadIn, unitsToShow);
                laneGrid.Children.Insert(0, leadIn);

                var head = new TextBlock
                {
                    Text = "▶", Foreground = TargetBrush, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, TargetLineY - 8, 0, 0), Cursor = Cursors.Help,
                    ToolTip = $"{row.Name}\nTarget: {later:MMM d, yyyy} (later than the dates showing)"
                };
                Grid.SetRow(head, 0);
                Grid.SetColumn(head, unitsToShow - 1);
                laneGrid.Children.Add(head);
            }

            foreach (var milestone in row.Milestones ?? [])
            {
                if (TimelineLayout.TargetPosition(milestone.Date, unitStarts, rangeEnd) is not { } where) continue;

                var line = BuildMilestoneMarker(milestone, where.Fraction, unitColWidth);
                Grid.SetRow(line, 0);
                Grid.SetRowSpan(line, row.Lanes);
                Grid.SetColumn(line, where.Unit);
                Grid.SetColumnSpan(line, unitStarts.Count - where.Unit); // room for the label to run on past its own column
                laneGrid.Children.Add(line);
            }

            foreach (var item in row.Items)
            {
                var task = item.Card;
                var lane = row.FirstTaskLane + item.Lane;
                if (item.HasLine)
                {
                    var dateLine = BuildDateLine(item, brush, unitColWidth);
                    Grid.SetRow(dateLine, lane);
                    Grid.SetColumn(dateLine, item.FirstUnit);
                    Grid.SetColumnSpan(dateLine, item.LastUnit - item.FirstUnit + 1);
                    laneGrid.Children.Add(dateLine);
                }

                var parts = new List<string> { task.Title };
                if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                parts.Add(TimelineLayout.DateLabel(task));

                var priorityBrush = GetPriorityBrush(task.Priority);
                var otherGroup = IsByGoal ? $"Project: {task.ProjectName}\n" : (string.IsNullOrEmpty(task.GoalDisplay) ? "" : $"{task.GoalDisplay}\n");
                var tooltip = $"{task.Title}\n{otherGroup}Priority: {task.Priority}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}"
                    + $"{(task.StartDate is not null ? $"Start: {task.StartDate:MMM d, yyyy}\n" : "")}"
                    + $"{(task.DueDate is not null ? $"Due: {task.DueDate:MMM d, yyyy}{(task.DueDateTime is { } dueAt ? $" at {dueAt:h:mm tt}" : "")}\n" : "")}"
                    + $"{(task.IsWaiting ? $"{task.WaitingOnDisplay}\n" : "")}\nDouble-click to open";

                // Started before the left edge: the box can't sit at its start date, so it is drawn
                // hollow and dashed in the first column showing, with the arrow running on from it.
                FrameworkElement block = item.StartsBeforeWindow
                    ? new Grid
                    {
                        Children =
                        {
                            new Rectangle { Stroke = priorityBrush, StrokeThickness = 1.5, StrokeDashArray = [3, 2], RadiusX = 3, RadiusY = 3, Fill = panelBrush },
                            new TextBlock { Text = string.Join(" - ", parts), Foreground = brush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5, 3, 5, 3) }
                        }
                    }
                    : new Border
                    {
                        Background = priorityBrush,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5, 3, 5, 3),
                        Child = new TextBlock { Text = string.Join(" - ", parts), Foreground = Brushes.White, FontSize = 11, TextWrapping = TextWrapping.Wrap }
                    };
                block.Margin = new Thickness(3, 0, 3, item.HasLine ? DateLineRoom : 3); // room under the box for its date line
                block.Cursor = Cursors.Hand;
                block.ToolTip = tooltip;
                block.VerticalAlignment = VerticalAlignment.Top;
                block.MouseLeftButtonDown += (_, args) =>
                {
                    if (args.ClickCount != 2) return;
                    OpenCardForEdit(task);
                };
                Grid.SetRow(block, lane);
                Grid.SetColumn(block, item.BoxUnit);
                laneGrid.Children.Add(block);
            }
        }

        // Today: one thin blue line down every row, in front, so where things stand reads at a glance.
        if (todayAt is { } now)
        {
            var todayLine = new Rectangle
            {
                Width = 2, Fill = TodayBrush, Opacity = 0.75, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(now * unitColWidth - 1, 0, 0, 0), ToolTip = $"Today, {DateTime.Today:MMM d, yyyy}"
            };
            Grid.SetRow(todayLine, 0);
            Grid.SetRowSpan(todayLine, rows.Count);
            Grid.SetColumn(todayLine, 1);
            Grid.SetColumnSpan(todayLine, unitsToShow);
            TimelineGrid.Children.Add(todayLine);
        }
    }

    // The row's name; in Summary view with a triangle that opens or closes its tasks, and the counts
    // under it (overdue in red).
    private FrameworkElement BuildNameCell(TimelineRow row, Brush brush, Brush secondaryBrush)
    {
        var panel = new StackPanel { Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, ContextMenu = BuildRowMenu(row.Name) };

        // A goal's target date, and (Summary) how far its open tasks run past it.
        void AddTargetLines(TimelineSummary? summary)
        {
            if (row.Target is not { } target) return;

            var line = new TextBlock { FontSize = 11, Foreground = secondaryBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(row.Summary is null ? 0 : 12, 2, 0, 0) };
            line.Inlines.Add(new Run("◆ ") { Foreground = TargetBrush });
            line.Inlines.Add(new Run($"Target {target:MMM d, yyyy}"));
            panel.Children.Add(line);

            if (summary?.OverrunText is { Length: > 0 } overrun)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = overrun, FontSize = 11, Foreground = OverdueBrush, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12, 1, 0, 0)
                });
            }
        }

        if (row.Summary is not { } summary)
        {
            panel.Children.Add(new TextBlock { Text = row.Name, FontWeight = FontWeights.SemiBold, Foreground = brush, TextWrapping = TextWrapping.Wrap });
            AddTargetLines(null);
            return panel;
        }

        panel.Children.Add(new TextBlock
        {
            Text = $"{(row.IsExpanded ? "▾" : "▸")} {row.Name}", FontWeight = FontWeights.SemiBold, Foreground = brush, TextWrapping = TextWrapping.Wrap
        });

        var counts = new TextBlock { FontSize = 11, Foreground = secondaryBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 0, 0) };
        counts.Inlines.Add(new Run(summary.OpenText));
        if (summary.Overdue > 0)
        {
            counts.Inlines.Add(new Run(" · "));
            counts.Inlines.Add(new Run(summary.OverdueText) { Foreground = OverdueBrush, FontWeight = FontWeights.SemiBold });
        }
        if (summary.Undated > 0) counts.Inlines.Add(new Run($" · {summary.UndatedText}"));
        panel.Children.Add(counts);
        AddTargetLines(summary);

        panel.Background = Brushes.Transparent; // so the whole cell takes the click, not just the text
        panel.Cursor = Cursors.Hand;
        panel.ToolTip = row.IsExpanded ? "Hide its tasks" : "Show its tasks";
        panel.MouseLeftButtonUp += (_, _) => ToggleRow(row.Name);
        return panel;
    }

    // Milestones are a teal line, apart from the purple or blue bar, the orange target and the red overrun.
    private static readonly Brush TodayBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
    private static readonly Brush MilestoneBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x89, 0x7B));

    // A goal's milestone: a vertical line down the row at its day within the month column, with what it is
    // written beside it at the foot of the row.
    private static FrameworkElement BuildMilestoneMarker(Milestone milestone, double fraction, double unitColWidth)
    {
        var x = Math.Clamp(fraction * unitColWidth, 1, unitColWidth - 1);
        var marker = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(x - 1, 0, 0, 0), Cursor = Cursors.Help };
        marker.ToolTip = $"{milestone.Description}\n{milestone.Date:MMM d, yyyy}";
        marker.Children.Add(new Rectangle { Width = 2, Fill = MilestoneBrush, HorizontalAlignment = HorizontalAlignment.Left });
        marker.Children.Add(new TextBlock
        {
            Text = milestone.Description, Foreground = MilestoneBrush, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 150,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 3)
        });
        return marker;
    }

    // The bar's colour; red from the goal's target date onward when its open tasks run past it (a hard
    // stop in a gradient, so it is one bar with two colours).
    private Brush BarBrush(TimelineSummary summary)
    {
        var color = SummaryBarColor;
        if (summary.OverrunFrom is not { } from) return new SolidColorBrush(color);
        if (from <= 0) return OverrunBarBrush;

        return new LinearGradientBrush(
        [
            new GradientStop(color, 0), new GradientStop(color, from), new GradientStop(OverrunColor, from), new GradientStop(OverrunColor, 1)
        ], new Point(0, 0), new Point(1, 0));
    }

    private static readonly Color OverrunColor = Color.FromRgb(0xC6, 0x28, 0x28);
    private static readonly Brush OverrunBarBrush = new SolidColorBrush(OverrunColor);
    private static readonly Brush TargetBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00));

    // How far down the row the target's line runs: just under a Summary bar, so the line is not hidden by it.
    private const double TargetLineY = 26;

    // The dashed line from the left edge of the dates to the diamond, behind the bar and the tasks.
    private static FrameworkElement BuildTargetLeadIn(double fraction, double unitColWidth, bool toEdge = false) => new Line
    {
        X1 = 0, X2 = 1, Y1 = 0, Y2 = 0, Stretch = Stretch.Fill, Stroke = TargetBrush, StrokeThickness = 1.5, StrokeDashArray = [4, 3],
        VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, TargetLineY - 0.75, toEdge ? 6 : Math.Max(0, unitColWidth - Math.Clamp(fraction * unitColWidth, 1, unitColWidth - 1)), 0),
        IsHitTestVisible = false
    };

    // The target date on a goal's row: a diamond on that line and a thin line down the row below it, at the day
    // within its month column.
    private static FrameworkElement BuildTargetMarker(TimelineRow row, DateTime target, double fraction, double unitColWidth)
    {
        var x = Math.Clamp(fraction * unitColWidth, 1, unitColWidth - 1);
        var marker = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(x - 6, 0, 0, 0), Width = 12 };
        marker.Children.Add(new Rectangle { Width = 1.5, Fill = TargetBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, TargetLineY, 0, 0), IsHitTestVisible = false });
        marker.Children.Add(new TextBlock
        {
            Text = "◆", Foreground = TargetBrush, FontSize = 14, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, TargetLineY - 9, 0, 0), Cursor = Cursors.Help,
            ToolTip = $"{row.Name}\nTarget: {target:MMM d, yyyy}"
        });
        return marker;
    }

    // A Summary bar: the dates its open tasks run between, square-ended with a ◀ or ▶ where it runs past
    // the edge of what is showing. Clicking it opens or closes the row's tasks.
    // The bar spans whole columns in the grid; its margins pull its ends in to the start of its first day and the end
    // of its last, keeping it at least 8 wide so a one-day bar can still be seen and clicked.
    private static Thickness BarMargin(TimelineSummary summary, double unitColWidth)
    {
        var span = (summary.LastUnit - summary.FirstUnit + 1) * unitColWidth;
        var left = summary.StartsBeforeWindow ? 0 : Math.Max(0, (summary.StartAt - summary.FirstUnit) * unitColWidth);
        var right = summary.EndsAfterWindow ? 0 : Math.Max(0, (summary.LastUnit + 1 - summary.EndAt) * unitColWidth);
        if (span - left - right < 8) right = Math.Max(0, span - left - 8);
        return new Thickness(left, 0, right, 4);
    }

    private FrameworkElement BuildSummaryBar(TimelineRow row, TimelineSummary summary, double unitColWidth)
    {
        var text = $"{(summary.StartsBeforeWindow ? "◀ " : "")}{summary.DatesText}{(summary.EndsAfterWindow ? " ▶" : "")}";
        var bar = new Border
        {
            Background = BarBrush(summary),
            CornerRadius = new CornerRadius(summary.StartsBeforeWindow ? 0 : 4, summary.EndsAfterWindow ? 0 : 4, summary.EndsAfterWindow ? 0 : 4, summary.StartsBeforeWindow ? 0 : 4),
            Padding = new Thickness(6, 4, 6, 4),
            // To the day: it starts where its first day starts and ends where its last day ends, inside the columns it spans.
            Margin = BarMargin(summary, unitColWidth),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
            ToolTip = $"{row.Name}\n{summary.DatesText}\n{summary.CountsText}\n\n{(row.IsExpanded ? "Click to hide its tasks" : "Click to show its tasks")}"
        };
        bar.ContextMenu = BuildRowMenu(row.Name);
        bar.MouseLeftButtonUp += (_, _) => ToggleRow(row.Name);
        return bar;
    }

    // A task's date line, under its box: from the start of its start day to the end of its due day, so
    // it shows the real dates in Week and Month view too, not just the columns. A short upright tick marks
    // the start and an arrowhead against a bar marks the due date. Starting before the left edge, it has no
    // tick; due after the right edge, it runs off that edge with no bar. Drawn in the text colour.
    private const double DateLineRoom = 13; // the box's bottom margin when it has a line under it

    private static FrameworkElement BuildDateLine(TimelineItem item, Brush stroke, double unitColWidth)
    {
        var columns = item.LastUnit - item.FirstUnit + 1;
        var left = item.StartsBeforeWindow ? 0 : Math.Max(0, (item.StartAt - item.FirstUnit) * unitColWidth);
        var right = item.DueAfterWindow ? 0 : Math.Max(0, (item.LastUnit + 1 - item.EndAt) * unitColWidth);
        // Never shorter than its tick and head: a one-day line in Month view would otherwise vanish.
        if (columns * unitColWidth - left - right < 12) right = Math.Max(0, columns * unitColWidth - left - 12);

        var panel = new DockPanel
        {
            Height = 9, VerticalAlignment = VerticalAlignment.Bottom, LastChildFill = true, IsHitTestVisible = false,
            Margin = new Thickness(left, 0, right, 2)
        };

        if (!item.StartsBeforeWindow)
        {
            var tick = new Rectangle { Width = 2, Height = 9, Fill = stroke };
            DockPanel.SetDock(tick, Dock.Left);
            panel.Children.Add(tick);
        }

        if (!item.DueAfterWindow)
        {
            var bar = new Rectangle { Width = 2, Height = 9, Fill = stroke };
            DockPanel.SetDock(bar, Dock.Right);
            panel.Children.Add(bar);
        }

        var head = new Polygon { Points = [new Point(0, 0), new Point(7, 4), new Point(0, 8)], Fill = stroke, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(head, Dock.Right);
        panel.Children.Add(head);

        panel.Children.Add(new Rectangle { Height = 2, Fill = stroke, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
}
