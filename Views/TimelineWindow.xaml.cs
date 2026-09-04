using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
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
                forceEditOnComplete: dialog.ForceEditOnComplete, websiteUrl: dialog.WebsiteUrl);
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
            .Where(c => c.DueDate is not null && c.DueDate.Value.Date >= _windowStart && c.DueDate.Value.Date < rangeEnd)
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
        var cardBrush = (Brush)FindResource("CardBackgroundBrush");
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
                Text = "No tasks with a due date in this range.", Foreground = secondaryBrush,
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

            var tasksByUnit = byProject[projectName]
                .GroupBy(c => (c.DueDate!.Value.Date - _windowStart).Days / unitDays)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueDate).ToList());

            for (var w = 0; w < unitsToShow; w++)
            {
                var cellPanel = new StackPanel { Margin = new Thickness(3) };
                if (tasksByUnit.TryGetValue(w, out var tasks))
                {
                    foreach (var task in tasks)
                    {
                        var parts = new List<string> { task.Title };
                        if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                        parts.Add(task.DueDate!.Value.ToString("MMM d"));

                        var block = new Border
                        {
                            Background = cardBrush,
                            BorderBrush = borderBrush,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 3, 5, 3),
                            Margin = new Thickness(0, 0, 0, 3),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock
                            {
                                Text = string.Join(" - ", parts), Foreground = brush,
                                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                                ToolTip = $"{task.Title}\n{(task.WhoName != "Unassigned" ? $"Who: {task.WhoName}\n" : "")}Due: {task.DueDate:MMM d, yyyy}\n\nDouble-click to open"
                            }
                        };
                        block.MouseLeftButtonDown += (_, args) =>
                        {
                            if (args.ClickCount != 2) return;
                            OpenCardForEdit(task);
                        };
                        cellPanel.Children.Add(block);
                    }
                }
                AddCell(TimelineGrid, r, w + 1, cellPanel, rowBackground);
            }
        }
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
        var chipBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD));
        var chipBorderBrush = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9));
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
            AddText(canvas, "No tasks with a due date in this range.", margin, y, regularTypeface, 11, Brushes.Gray);
        }

        for (var r = 0; r < rowProjects.Count; r++)
        {
            var projectName = rowProjects[r];
            var tasksByUnit = byProject[projectName]
                .GroupBy(c => (c.DueDate!.Value.Date - _windowStart).Days / unitDays)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueDate).ToList());

            // Precompute each column's wrapped lines so the row height (the tallest column) is
            // known before anything is drawn, and so a row that doesn't fit can trigger a new page
            // (with the header redrawn) before any of its content is committed to the current one.
            var cellLines = new Dictionary<int, List<List<string>>>();
            var rowHeight = rowMinHeight;
            for (var w = 0; w < unitsToShow; w++)
            {
                if (!tasksByUnit.TryGetValue(w, out var tasks)) continue;
                var linesPerTask = new List<List<string>>();
                foreach (var task in tasks)
                {
                    var parts = new List<string> { task.Title };
                    if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                    parts.Add(task.DueDate!.Value.ToString("MMM d"));
                    linesPerTask.Add(WrapWords(string.Join(" - ", parts), regularTypeface, 7.5, unitColWidth - 2 * chipPadding - 4));
                }
                cellLines[w] = linesPerTask;
                var cellHeight = linesPerTask.Sum(lines => lines.Count * lineHeight + 2 * chipPadding + chipGap);
                rowHeight = Math.Max(rowHeight, cellHeight);
            }
            rowHeight += 4;

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

            for (var w = 0; w < unitsToShow; w++)
            {
                if (!cellLines.TryGetValue(w, out var linesPerTask)) continue;
                var chipY = rowTop + 2;
                var chipX = margin + projectColWidth + w * unitColWidth + 1;
                var chipWidth = unitColWidth - 2;

                foreach (var lines in linesPerTask)
                {
                    var chipHeight = lines.Count * lineHeight + 2 * chipPadding;
                    var chip = new Rectangle
                    {
                        Width = chipWidth, Height = chipHeight, Fill = chipBrush,
                        Stroke = chipBorderBrush, StrokeThickness = 0.75, RadiusX = 2, RadiusY = 2
                    };
                    Canvas.SetLeft(chip, chipX);
                    Canvas.SetTop(chip, chipY);
                    canvas.Children.Add(chip);

                    var textY = chipY + chipPadding;
                    foreach (var line in lines)
                    {
                        AddText(canvas, line, chipX + 3, textY, regularTypeface, 7.5, Brushes.Black);
                        textY += lineHeight;
                    }
                    chipY += chipHeight + chipGap;
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
