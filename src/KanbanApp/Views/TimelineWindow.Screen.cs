using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Converters;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Drawing the Timeline on screen: the date header, a row per project, and each task as a box or
// an arrow laid out by TimelineLayout.
public partial class TimelineWindow
{
    // Rebuilds both grids from scratch on every navigation/toggle rather than trying to update them
    // in place - the row/column count changes with the data (only projects with a due task in the
    // visible window get a row), so an incremental update would need the same "figure out which
    // rows/columns are needed" logic anyway. The header row lives in HeaderGrid (its own frozen
    // ScrollViewer) and the project rows live in TimelineGrid (the scrollable body) - both get
    // identical column definitions built by AddColumns so their cells stay lined up.
    private void BuildGrid()
    {
        var brush = (Brush)FindResource("PrimaryTextBrush");
        var secondaryBrush = (Brush)FindResource("SecondaryTextBrush");
        var borderBrush = (Brush)FindResource("CardBorderBrush");
        var panelBrush = (Brush)FindResource("PanelBackgroundBrush");
        var alternateRowBrush = (Brush)FindResource("AlternateRowBrush");

        var stepLabel = IsDayView ? "1 Week" : "4 Weeks";
        PrevButton.Content = $"◀ {stepLabel}";
        NextButton.Content = $"{stepLabel} ▶";

        var (rowProjects, byProject, unitsToShow, unitDays, rangeEnd) = GetVisibleData();
        RangeLabel.Text = $"{_windowStart:MMM d} – {rangeEnd.AddDays(-1):MMM d, yyyy}";

        HeaderGrid.Children.Clear();
        HeaderGrid.RowDefinitions.Clear();
        HeaderGrid.ColumnDefinitions.Clear();

        TimelineGrid.Children.Clear();
        TimelineGrid.RowDefinitions.Clear();
        TimelineGrid.ColumnDefinitions.Clear();

        const double projectColWidth = 150;
        var unitColWidth = IsDayView ? 90 : 150;

        void AddColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(projectColWidth) });
            for (var w = 0; w < unitsToShow; w++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitColWidth) });
            }
        }
        AddColumns(HeaderGrid);
        AddColumns(TimelineGrid);

        HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddCell(Grid grid, int row, int col, UIElement content, Brush? background = null)
        {
            var border = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = background,
                Child = content
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        AddCell(HeaderGrid, 0, 0, new TextBlock
        {
            Text = "Projects", FontWeight = FontWeights.Bold, Foreground = brush,
            Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center
        }, panelBrush);

        for (var w = 0; w < unitsToShow; w++)
        {
            var unitStart = _windowStart.AddDays(w * unitDays);
            var headerText = IsDayView ? unitStart.ToString("ddd\nd-MMM") : unitStart.ToString("d-MMM");
            AddCell(HeaderGrid, 0, w + 1, new TextBlock
            {
                Text = headerText, FontWeight = FontWeights.Bold, Foreground = brush,
                Margin = new Thickness(4, 6, 4, 6), VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            }, panelBrush);
        }

        if (rowProjects.Count == 0)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });
            AddCell(TimelineGrid, 0, 0, new TextBlock
            {
                Text = "No tasks with a due or start date in this range.", Foreground = secondaryBrush,
                FontStyle = FontStyles.Italic, Margin = new Thickness(6)
            });
            for (var w = 0; w < unitsToShow; w++) AddCell(TimelineGrid, 0, w + 1, new Border());
            return;
        }

        for (var r = 0; r < rowProjects.Count; r++)
        {
            TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 50 });

            // Alternating shading, matching the app's existing AlternateRowBrush convention.
            var rowBackground = r % 2 == 1 ? alternateRowBrush : null;

            var projectName = rowProjects[r];
            AddCell(TimelineGrid, r, 0, new TextBlock
            {
                Text = projectName, FontWeight = FontWeights.SemiBold, Foreground = brush,
                Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            }, rowBackground);

            // The row's date cells are just the background and grid lines now; the tasks sit in a
            // lane grid laid over them (same column widths, one auto-height row per lane), because
            // an arrow has to run across several columns.
            for (var w = 0; w < unitsToShow; w++) AddCell(TimelineGrid, r, w + 1, new Border(), rowBackground);

            var items = TimelineLayout.Place(byProject[projectName], _windowStart, unitDays, unitsToShow);
            var laneGrid = new Grid { Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            for (var w = 0; w < unitsToShow; w++) laneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitColWidth) });
            for (var lane = 0; lane <= items.Max(i => i.Lane); lane++) laneGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(laneGrid, r);
            Grid.SetColumn(laneGrid, 1);
            Grid.SetColumnSpan(laneGrid, unitsToShow);
            TimelineGrid.Children.Add(laneGrid);

            foreach (var item in items)
            {
                var task = item.Card;
                if (item.HasArrow)
                {
                    var arrow = BuildArrow(item, unitColWidth, brush);
                    Grid.SetRow(arrow, item.Lane);
                    Grid.SetColumn(arrow, item.ArrowFirstUnit);
                    Grid.SetColumnSpan(arrow, item.ArrowLastUnit - item.ArrowFirstUnit + 1);
                    laneGrid.Children.Add(arrow);
                }

                var parts = new List<string> { task.Title };
                if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                parts.Add(TimelineLayout.DateLabel(task));

                var priorityBrush = GetPriorityBrush(task.Priority);
                var tooltip = $"{task.Title}\nPriority: {task.Priority}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}"
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
                block.Margin = new Thickness(3, 0, 3, 3);
                block.Cursor = Cursors.Hand;
                block.ToolTip = tooltip;
                block.VerticalAlignment = VerticalAlignment.Top;
                block.MouseLeftButtonDown += (_, args) =>
                {
                    if (args.ClickCount != 2) return;
                    OpenCardForEdit(task);
                };
                Grid.SetRow(block, item.Lane);
                Grid.SetColumn(block, item.BoxUnit);
                laneGrid.Children.Add(block);
            }
        }
    }

    // The arrow from a task's box (at its start date) to its due date: a line and a head whose tip
    // stops in the middle of the due column - or at the right edge when the due date is beyond it.
    // Drawn in the text colour, so it is black on the light theme and light on the dark one. It
    // sits level with the first line of text in the box.
    private static FrameworkElement BuildArrow(TimelineItem item, double unitColWidth, Brush stroke)
    {
        const double arrowTop = 6;
        var panel = new DockPanel
        {
            Height = 10, VerticalAlignment = VerticalAlignment.Top, LastChildFill = true, IsHitTestVisible = false,
            Margin = new Thickness(0, arrowTop, item.DueAfterWindow ? 0 : unitColWidth / 2, 0)
        };

        var head = new Polygon { Points = [new Point(0, 0), new Point(9, 5), new Point(0, 10)], Fill = stroke, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(head, Dock.Right);
        panel.Children.Add(head);

        panel.Children.Add(new Rectangle { Height = 2, Fill = stroke, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
}
