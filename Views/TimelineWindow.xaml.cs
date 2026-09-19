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

public partial class TimelineWindow : Window
{
    // Week view: 12 weekly columns, paged 4 weeks (28 days) at a time. Day view: 21 daily columns
    // (three weeks), paged 1 week (7 days) at a time. Both step sizes are multiples of 7, so
    // _windowStart stays Monday-aligned regardless of which view is active or how much the user has
    // paged - switching views mid-navigation never needs to re-snap the range.
    private const int WeekViewUnits = 12;
    private const int DayViewUnits = 21;
    private const int WeekViewStepDays = 28;
    private const int DayViewStepDays = 7;

    private readonly MainViewModel _viewModel;
    private DateTime _windowStart;
    private bool _initializing = true;

    // Reuses the exact same priority palette as the main board's priority badge (see
    // PriorityToBrushConverter) rather than defining a second one here, so "what color means High"
    // stays consistent across the whole app.
    private static readonly PriorityToBrushConverter PriorityBrushConverter = new();

    private static Brush GetPriorityBrush(string priority) =>
        (Brush)PriorityBrushConverter.Convert(priority, typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture)!;

    private static Color GetPriorityColor(string priority) => ((SolidColorBrush)GetPriorityBrush(priority)).Color;

    private static Color LightenColor(Color color, double whiteAmount) => Color.FromRgb(
        (byte)(color.R + (255 - color.R) * whiteAmount),
        (byte)(color.G + (255 - color.G) * whiteAmount),
        (byte)(color.B + (255 - color.B) * whiteAmount));

    public TimelineWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowStart = MondayOf(DateTime.Today);
        _initializing = false;
        BuildGrid();
    }

    private bool IsDayView => DayViewRadio.IsChecked == true;
    private int UnitDays => IsDayView ? 1 : 7;
    private int UnitsToShow => IsDayView ? DayViewUnits : WeekViewUnits;
    private int StepDays => IsDayView ? DayViewStepDays : WeekViewStepDays;

    private static DateTime MondayOf(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow(_viewModel, "HelpSection_Timeline") { Owner = this }.ShowDialog();
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(-StepDays);
        BuildGrid();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = _windowStart.AddDays(StepDays);
        BuildGrid();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _windowStart = MondayOf(DateTime.Today);
        BuildGrid();
    }

    private void IncludeDoneCheckBox_Changed(object sender, RoutedEventArgs e) => BuildGrid();

    // The header row lives in its own ScrollViewer (frozen vertically, no scrollbar of its own) so
    // it stays visible while the body scrolls; this keeps its horizontal offset locked to the
    // body's so the header columns stay lined up with the body's as the user scrolls sideways.
    private void BodyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0) HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
    }

    private void ZoomLevel_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        BuildGrid();
    }

    // Mirrors MainWindow's own EditCard - opens the same task dialog the board uses, then saves
    // through the same MainViewModel.EditCard call so the change is identical either way. Rebuilds
    // the grid afterward regardless of Save/Cancel, since that's cheap and picks up anything that
    // moved the task out of view (a new due date, project, or column).
    private void OpenCardForEdit(CardViewModel card)
    {
        var currentColumn = _viewModel.Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (currentColumn is null) return;

        var dialog = new AddTaskWindow(_viewModel, card, currentColumn) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedColumn is not null)
        {
            _viewModel.EditCard(card, dialog.TaskDetails, dialog.SelectedColumn, dialog.SelectedProject,
                dialog.SelectedPriority, dialog.SelectedDueDate, dialog.SelectedWho, dialog.IsRecurring, dialog.RecurrencePattern,
                dialog.SelectedGoal, dialog.SelectedFlags, dialog.SelectedSubTasks, dialog.Notes, attachments: dialog.SelectedAttachments,
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl, dueTime: dialog.SelectedDueTime,
                startDate: dialog.SelectedStartDate);
        }

        BuildGrid();
    }

    // Shared by BuildGrid and BuildPrintDocument so both work from identical data: which projects
    // get a row, which cards land in each, and the current unit/day math for the visible window.
    private (List<string> RowProjects, Dictionary<string, List<CardViewModel>> ByProject, int UnitsToShow, int UnitDays, DateTime RangeEnd) GetVisibleData()
    {
        var unitDays = UnitDays;
        var unitsToShow = UnitsToShow;
        var rangeEnd = _windowStart.AddDays(unitsToShow * unitDays);

        var includeDone = IncludeDoneCheckBox.IsChecked == true;
        var cards = _viewModel.Columns
            .Where(c => includeDone || c.Name != "Done")
            .SelectMany(c => c.Cards)
            .Where(c => TimelineLayout.IsInWindow(c, _windowStart, rangeEnd))
            .ToList();

        var byProject = cards.GroupBy(c => c.ProjectName).ToDictionary(g => g.Key, g => g.ToList());

        var rowProjects = _viewModel.Projects.Select(p => p.Name).Where(byProject.ContainsKey).ToList();
        if (byProject.ContainsKey("No Project")) rowProjects.Add("No Project");

        return (rowProjects, byProject, unitsToShow, unitDays, rangeEnd);
    }

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
                parts.Add(item.DueAfterWindow ? $"due {task.DueDate:MMM d}" : TimelineLayout.DateLabel(task));

                var priorityBrush = GetPriorityBrush(task.Priority);
                var tooltip = $"{task.Title}\nPriority: {task.Priority}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}"
                    + $"{(task.StartDate is not null ? $"Start: {task.StartDate:MMM d, yyyy}\n" : "")}"
                    + $"{(task.DueDate is not null ? $"Due: {task.DueDate:MMM d, yyyy}\n" : "")}\nDouble-click to open";

                // Due past the right edge: the box can't sit at its due date, so it is drawn
                // hollow and dashed at the left of what's showing, with the arrow running on from it.
                FrameworkElement block = item.DueAfterWindow
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

    // The arrow from a task's start date to its box: a dot where it starts (left off when the start
    // is before the visible range, so the line simply comes in from the edge), a line, and a head
    // pointing at the box - or off the right edge when the due date is beyond it. Drawn in the text
    // colour, so it is black on the light theme and light on the dark one. It sits level with the
    // first line of text in the box.
    private static FrameworkElement BuildArrow(TimelineItem item, double unitColWidth, Brush stroke)
    {
        const double arrowTop = 6;
        var panel = new DockPanel
        {
            Height = 10, VerticalAlignment = VerticalAlignment.Top, LastChildFill = true, IsHitTestVisible = false,
            // Starts in the middle of the start column (not applicable when it comes in from the
            // edge, or when it starts at the dashed box's right-hand side).
            Margin = new Thickness(item.StartsBeforeWindow || item.DueAfterWindow ? 0 : unitColWidth / 2, arrowTop, 0, 0)
        };

        if (!item.StartsBeforeWindow && !item.DueAfterWindow)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Fill = stroke, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dot, Dock.Left);
            panel.Children.Add(dot);
        }

        var head = new Polygon { Points = [new Point(0, 0), new Point(9, 5), new Point(0, 10)], Fill = stroke, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(head, Dock.Right);
        panel.Children.Add(head);

        panel.Children.Add(new Rectangle { Height = 2, Fill = stroke, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private void Print_Click(object sender, RoutedEventArgs e) =>
        new ReportPreviewWindow(BuildPrintDocument()) { Owner = this }.ShowDialog();

    // Builds a print-friendly, paginated rendering of exactly what's currently on screen (same
    // range, zoom level, and Include Done setting) via GetVisibleData. Reuses ReportPreviewWindow's
    // DocumentViewer for preview and its built-in Print button, the same as Report Builder, rather
    // than any separate print plumbing. Landscape A4 with print-specific column widths (recomputed
    // to exactly fill the page width for however many units are showing) rather than the on-screen
    // pixel widths, and paginates by project row, repeating the title/date-header band on every page.
    private FixedDocument BuildPrintDocument()
    {
        const double pageWidth = 1122.24;
        const double pageHeight = 793.92;
        const double margin = 40;
        const double headerBandHeight = 54;
        const double dateHeaderHeight = 30;
        const double projectColWidth = 110;
        const double rowMinHeight = 20;
        const double lineHeight = 11;
        const double chipPadding = 4;
        const double chipGap = 2;

        var (rowProjects, byProject, unitsToShow, unitDays, rangeEnd) = GetVisibleData();

        var contentWidth = pageWidth - 2 * margin;
        var unitColWidth = (contentWidth - projectColWidth) / unitsToShow;
        var bodyTop = margin + headerBandHeight + dateHeaderHeight + 10;
        var bottomLimit = pageHeight - margin;

        var regularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        FormattedText MakeText(string s, Typeface tf, double size, Brush brush) =>
            new(s, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

        List<string> WrapWords(string text, Typeface tf, double size, double maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (MakeText(candidate, tf, size, Brushes.Black).Width > maxWidth && current.Length > 0)
                {
                    lines.Add(current);
                    current = word;
                }
                else current = candidate;
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        void AddText(Canvas targetCanvas, string text, double x, double top, Typeface tf, double size, Brush brush,
            TextAlignment align = TextAlignment.Left)
        {
            var width = MakeText(text, tf, size, brush).Width;
            var drawX = align switch { TextAlignment.Center => x - width / 2, TextAlignment.Right => x - width, _ => x };
            var block = new TextBlock
            {
                Text = text, FontFamily = tf.FontFamily, FontSize = size, FontWeight = tf.Weight, FontStyle = tf.Style, Foreground = brush
            };
            Canvas.SetLeft(block, drawX);
            Canvas.SetTop(block, top);
            targetCanvas.Children.Add(block);
        }

        var accentBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));
        var subtitleBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xCB, 0xDA));
        var bandOddBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
        var columnLineBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));

        var canvases = new List<Canvas>();
        Canvas canvas = null!;
        double y = 0;

        void DrawColumnLines(double top, double height)
        {
            void VLine(double x)
            {
                var line = new Line { X1 = x, Y1 = top, X2 = x, Y2 = top + height, Stroke = columnLineBrush, StrokeThickness = 0.5 };
                canvas.Children.Add(line);
            }
            VLine(margin + projectColWidth);
            for (var w = 1; w < unitsToShow; w++)
                VLine(margin + projectColWidth + w * unitColWidth);
        }

        void DrawHeaderBand()
        {
            var band = new Rectangle { Width = pageWidth, Height = headerBandHeight, Fill = accentBrush };
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, 0);
            canvas.Children.Add(band);
            AddText(canvas, "Timeline", margin, 10, boldTypeface, 18, Brushes.White);
            AddText(canvas,
                $"{_windowStart:MMM d} – {rangeEnd.AddDays(-1):MMM d, yyyy}   •   {(IsDayView ? "Day view" : "Week view")}   •   Generated {DateTime.Now:MMM d, yyyy}",
                margin, 32, regularTypeface, 10, subtitleBrush);

            var headerY = headerBandHeight + 4;
            AddText(canvas, "Projects", margin + 4, headerY + 6, boldTypeface, 10, accentBrush);
            for (var w = 0; w < unitsToShow; w++)
            {
                var unitStart = _windowStart.AddDays(w * unitDays);
                var label = IsDayView ? unitStart.ToString("ddd d-MMM") : unitStart.ToString("d-MMM");
                var colCenterX = margin + projectColWidth + w * unitColWidth + unitColWidth / 2;
                AddText(canvas, label, colCenterX, headerY + 6, boldTypeface, 8, accentBrush, TextAlignment.Center);
            }
            var headerRule = new Line
            {
                X1 = margin, Y1 = headerY + dateHeaderHeight, X2 = pageWidth - margin, Y2 = headerY + dateHeaderHeight,
                Stroke = accentBrush, StrokeThickness = 1
            };
            canvas.Children.Add(headerRule);
        }

        void NewPage()
        {
            canvas = new Canvas { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            canvases.Add(canvas);
            DrawHeaderBand();
            y = bodyTop;
        }

        NewPage();

        if (rowProjects.Count == 0)
        {
            AddText(canvas, "No tasks with a due or start date in this range.", margin, y, regularTypeface, 11, Brushes.Gray);
        }

        for (var r = 0; r < rowProjects.Count; r++)
        {
            var projectName = rowProjects[r];
            var items = TimelineLayout.Place(byProject[projectName], _windowStart, unitDays, unitsToShow);

            // Wrap every box's text and work out each lane's height (its tallest box) before
            // anything is drawn, so the row height is known and a row that doesn't fit can start a
            // new page (with the header redrawn) before any of it is committed to the current one.
            var boxLines = new Dictionary<TimelineItem, List<string>>();
            var laneHeights = new double[items.Max(i => i.Lane) + 1];
            foreach (var item in items)
            {
                var task = item.Card;
                var parts = new List<string> { task.Title };
                if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                parts.Add(item.DueAfterWindow ? $"due {task.DueDate:MMM d}" : TimelineLayout.DateLabel(task));

                var lines = WrapWords(string.Join(" - ", parts), regularTypeface, 7.5, unitColWidth - 2 * chipPadding - 4);
                boxLines[item] = lines;
                laneHeights[item.Lane] = Math.Max(laneHeights[item.Lane], lines.Count * lineHeight + 2 * chipPadding + chipGap);
            }
            var rowHeight = Math.Max(rowMinHeight, laneHeights.Sum()) + 4;

            if (y + rowHeight > bottomLimit) NewPage();

            var rowBand = new Rectangle { Width = contentWidth, Height = rowHeight, Fill = r % 2 == 1 ? bandOddBrush : Brushes.White };
            Canvas.SetLeft(rowBand, margin);
            Canvas.SetTop(rowBand, y);
            canvas.Children.Add(rowBand);
            DrawColumnLines(y, rowHeight);

            var rowTop = y;
            var labelY = rowTop + 4;
            foreach (var line in WrapWords(projectName, boldTypeface, 9, projectColWidth - 8))
            {
                AddText(canvas, line, margin + 4, labelY, boldTypeface, 9, Brushes.Black);
                labelY += lineHeight;
            }

            double UnitLeft(int unit) => margin + projectColWidth + unit * unitColWidth;

            foreach (var item in items)
            {
                var laneTop = rowTop + 2 + laneHeights.Take(item.Lane).Sum();
                var lines = boxLines[item];
                var priorityColor = GetPriorityColor(item.Card.Priority);

                if (item.HasArrow)
                {
                    // Same arrow as on screen: dot at the start (unless it comes in from the left
                    // edge or starts at the dashed box), line, head pointing at the box or off the
                    // right edge. Level with the first line of text.
                    var arrowY = laneTop + chipPadding + lineHeight / 2;
                    var hasDot = !item.StartsBeforeWindow && !item.DueAfterWindow;
                    var x1 = UnitLeft(item.ArrowFirstUnit) + (hasDot ? unitColWidth / 2 : 0);
                    var x2 = UnitLeft(item.ArrowLastUnit + 1) - 1;

                    canvas.Children.Add(new Line { X1 = x1, Y1 = arrowY, X2 = x2 - 5, Y2 = arrowY, Stroke = Brushes.Black, StrokeThickness = 1.25 });
                    canvas.Children.Add(new Polygon
                    {
                        Points = [new Point(x2 - 6, arrowY - 3.5), new Point(x2, arrowY), new Point(x2 - 6, arrowY + 3.5)], Fill = Brushes.Black
                    });
                    if (hasDot)
                    {
                        var dot = new Ellipse { Width = 5, Height = 5, Fill = Brushes.Black };
                        Canvas.SetLeft(dot, x1 - 2.5);
                        Canvas.SetTop(dot, arrowY - 2.5);
                        canvas.Children.Add(dot);
                    }
                }

                var chipHeight = lines.Count * lineHeight + 2 * chipPadding;
                var chipX = UnitLeft(item.BoxUnit) + 1;
                var chip = new Rectangle
                {
                    Width = unitColWidth - 2, Height = chipHeight, RadiusX = 2, RadiusY = 2,
                    Stroke = new SolidColorBrush(priorityColor), StrokeThickness = 0.75,
                    // Dashed and unfilled when the due date is past the right edge - see TimelineItem.
                    Fill = item.DueAfterWindow ? Brushes.White : new SolidColorBrush(LightenColor(priorityColor, 0.85))
                };
                if (item.DueAfterWindow) chip.StrokeDashArray = [3, 2];
                Canvas.SetLeft(chip, chipX);
                Canvas.SetTop(chip, laneTop);
                canvas.Children.Add(chip);

                var textY = laneTop + chipPadding;
                foreach (var line in lines)
                {
                    AddText(canvas, line, chipX + 3, textY, regularTypeface, 7.5, Brushes.Black);
                    textY += lineHeight;
                }
            }

            y = rowTop + rowHeight;
        }

        var fixedDoc = new FixedDocument();
        for (var i = 0; i < canvases.Count; i++)
        {
            var footerText = $"Page {i + 1} of {canvases.Count}";
            var footerBrush = Brushes.Gray;
            var footerWidth = MakeText(footerText, regularTypeface, 9, footerBrush).Width;
            AddText(canvases[i], footerText, (pageWidth - footerWidth) / 2, pageHeight - 26, regularTypeface, 9, footerBrush);

            var fixedPage = new FixedPage { Width = pageWidth, Height = pageHeight };
            fixedPage.Children.Add(canvases[i]);
            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDoc.Pages.Add(pageContent);
        }

        return fixedDoc;
    }
}
