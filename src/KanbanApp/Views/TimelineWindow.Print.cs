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

// Printing the Timeline: the same layout rules (TimelineLayout) drawn by hand onto pages.
public partial class TimelineWindow
{
    // The preview offers the paper and orientation (remembered), and lays the pages out again for them.
    private void Print_Click(object sender, RoutedEventArgs e) =>
        new ReportPreviewWindow(BuildPrintDocument, _viewModel.TimelinePageSetup, _viewModel.SaveTimelinePageSetup) { Owner = this, Title = "Timeline Preview" }.ShowDialog();

    // Builds a print-friendly, paginated rendering of exactly what's currently on screen (same
    // range, zoom level, and Include Done setting) via GetVisibleData, on the paper chosen in the
    // preview, with print-specific column widths (recomputed to exactly fill the page width for
    // however many units are showing) rather than the on-screen pixel widths, and paginates by row,
    // repeating the title/date-header band on every page.
    private FixedDocument BuildPrintDocument(PageSetup setup)
    {
        var pageWidth = setup.PageWidth;
        var pageHeight = setup.PageHeight;
        const double margin = 40;
        const double headerBandHeight = 54;
        const double dateHeaderHeight = 30;
        const double projectColWidth = 110;
        const double rowMinHeight = 20;
        const double lineHeight = 11;
        const double chipPadding = 4;
        const double chipGap = 2;

        const double barHeight = 16;

        var (rows, unitStarts, rangeEnd) = GetVisibleData();
        var unitsToShow = unitStarts.Count;

        var contentWidth = pageWidth - 2 * margin;
        var unitColWidth = (contentWidth - projectColWidth) / unitsToShow;
        var bodyTop = margin + headerBandHeight + dateHeaderHeight + 10;
        var bottomLimit = pageHeight - margin;

        var regularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        FormattedText MakeText(string s, Typeface tf, double size, Brush brush) =>
            new(s, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

        // A title can hold line breaks (it comes from a multi-line box), so each line of it is wrapped on its
        // own and the chip is made tall enough for all of them. Blank lines are left out of a chip.
        List<string> WrapWords(string text, Typeface tf, double size, double maxWidth) =>
            TextWrap.Lines(text, s => MakeText(s, tf, size, Brushes.Black).Width, maxWidth).Where(l => l.Length > 0).ToList();

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

        var subLines = Enumerable.Range(0, unitsToShow)
            .Select(w => TimelineLayout.SubDivisions(Zoom, unitStarts[w], w + 1 < unitsToShow ? unitStarts[w + 1] : rangeEnd)).ToList();

        void DrawColumnLines(double top, double height)
        {
            void VLine(double x, double thickness = 0.5)
            {
                var line = new Line { X1 = x, Y1 = top, X2 = x, Y2 = top + height, Stroke = columnLineBrush, StrokeThickness = thickness };
                canvas.Children.Add(line);
            }
            VLine(margin + projectColWidth);
            for (var w = 0; w < unitsToShow; w++)
            {
                var left = margin + projectColWidth + w * unitColWidth;
                if (w > 0) VLine(left, 0.9); // between weeks, months or days: a little heavier
                foreach (var fraction in subLines[w])
                    VLine(left + fraction * unitColWidth, 0.3); // each day of a week, each Monday of a month
            }
        }

        // Today, if it is showing: a thin blue line down the row.
        void DrawToday(double top, double height)
        {
            if (DateTime.Today < unitStarts[0] || DateTime.Today >= rangeEnd) return;

            var x = margin + projectColWidth + (TimelineLayout.Offset(DateTime.Today, unitStarts, rangeEnd) + TimelineLayout.EndOffset(DateTime.Today, unitStarts, rangeEnd)) / 2 * unitColWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = top, X2 = x, Y2 = top + height, Stroke = TodayBrush, StrokeThickness = 1 });
        }

        void DrawHeaderBand()
        {
            var band = new Rectangle { Width = pageWidth, Height = headerBandHeight, Fill = accentBrush };
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, 0);
            canvas.Children.Add(band);
            AddText(canvas, "Timeline", margin, 10, boldTypeface, 18, Brushes.White);
            AddText(canvas,
                $"{_windowStart:MMM d} – {rangeEnd.AddDays(-1):MMM d, yyyy}   •   {TimelineScale.ViewName(Zoom)}, {(IsSummary ? "summary" : "detail")}, by {(IsByGoal ? "goal" : "project")}   •   Generated {DateTime.Now:MMM d, yyyy}",
                margin, 32, regularTypeface, 10, subtitleBrush);

            var headerY = headerBandHeight + 4;
            AddText(canvas, GroupHeading, margin + 4, headerY + 6, boldTypeface, 10, accentBrush);
            for (var w = 0; w < unitsToShow; w++)
            {
                // A heading that does not fit its column goes on two shorter lines (month over year, or day
                // over weekday), the year only where it starts or changes.
                var unitStart = unitStarts[w];
                var label = UnitLabel(unitStart, print: true);
                string? second = null;
                if (MakeText(label, boldTypeface, 8, accentBrush).Width > unitColWidth - 4)
                {
                    (label, second) = Zoom switch
                    {
                        TimelineZoom.Month => (unitStart.ToString("MMM"), w == 0 || unitStart.Month == 1 ? unitStart.ToString("yyyy") : null),
                        TimelineZoom.Day => (unitStart.ToString("ddd"), unitStart.ToString("d-MMM")),
                        _ => (unitStart.ToString("d"), unitStart.ToString("MMM"))
                    };
                }
                var colCenterX = margin + projectColWidth + w * unitColWidth + unitColWidth / 2;
                AddText(canvas, label, colCenterX, second is null ? headerY + 6 : headerY + 2, boldTypeface, 8, accentBrush, TextAlignment.Center);
                if (second is not null) AddText(canvas, second, colCenterX, headerY + 14, regularTypeface, 7, accentBrush, TextAlignment.Center);
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

        if (rows.Count == 0)
        {
            AddText(canvas, EmptyText, margin, y, regularTypeface, 11, Brushes.Gray);
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var projectName = row.Name;
            var items = row.Items;

            // Wrap every box's text and work out each lane's height (its tallest box) before
            // anything is drawn, so the row height is known and a row that doesn't fit can start a
            // new page (with the header redrawn) before any of it is committed to the current one.
            // A Summary bar takes the first lane.
            var boxLines = new Dictionary<TimelineItem, List<string>>();
            var laneHeights = new double[Math.Max(1, row.Lanes)];
            if (row.Summary is not null) laneHeights[0] = barHeight + chipGap;
            foreach (var item in items)
            {
                var task = item.Card;
                var parts = new List<string> { task.Title };
                if (!string.IsNullOrWhiteSpace(task.WhoName) && task.WhoName != "Unassigned") parts.Add(task.WhoName);
                parts.Add(TimelineLayout.DateLabel(task));

                var lines = WrapWords(string.Join(" - ", parts), regularTypeface, 7.5, unitColWidth - 2 * chipPadding - 4);
                boxLines[item] = lines;
                laneHeights[row.FirstTaskLane + item.Lane] = Math.Max(laneHeights[row.FirstTaskLane + item.Lane], lines.Count * lineHeight + 2 * chipPadding + chipGap);
            }
            // The name column: the name, and in Summary view the counts under it.
            var nameLines = WrapWords(projectName, boldTypeface, 9, projectColWidth - 8);
            var countLines = row.Summary is { } counted ? WrapWords(counted.CountsText, regularTypeface, 7.5, projectColWidth - 8) : [];
            var targetLines = new List<string>();
            if (row.Target is { } goalTarget) targetLines.Add($"Target {goalTarget:MMM d, yyyy}");
            if (row.Summary?.OverrunText is { Length: > 0 } overrunText) targetLines.Add(overrunText);
            var nameHeight = (nameLines.Count + countLines.Count + targetLines.Count) * lineHeight + 4;
            var rowHeight = Math.Max(Math.Max(rowMinHeight, nameHeight), laneHeights.Sum()) + 4;

            if (y + rowHeight > bottomLimit) NewPage();

            var rowBand = new Rectangle { Width = contentWidth, Height = rowHeight, Fill = r % 2 == 1 ? bandOddBrush : Brushes.White };
            Canvas.SetLeft(rowBand, margin);
            Canvas.SetTop(rowBand, y);
            canvas.Children.Add(rowBand);
            DrawColumnLines(y, rowHeight);

            var rowTop = y;
            var labelY = rowTop + 4;
            foreach (var line in nameLines)
            {
                AddText(canvas, line, margin + 4, labelY, boldTypeface, 9, Brushes.Black);
                labelY += lineHeight;
            }
            foreach (var line in countLines)
            {
                AddText(canvas, line, margin + 4, labelY, regularTypeface, 7.5, Brushes.DimGray);
                labelY += lineHeight;
            }
            foreach (var line in targetLines)
            {
                var isOverrun = line.StartsWith("ends ", StringComparison.Ordinal);
                AddText(canvas, line, margin + 4, labelY, isOverrun ? boldTypeface : regularTypeface, 7.5, isOverrun ? new SolidColorBrush(OverrunColor) : Brushes.DimGray);
                labelY += lineHeight;
            }

            double UnitLeft(int unit) => margin + projectColWidth + unit * unitColWidth;

            // The Summary bar: square-ended where it runs past the edge, with its dates in white.
            if (row.Summary is { } summary)
            {
                // To the day, as on screen: from the start of its first day to the end of its last, never under 6 wide.
                var barLeft = summary.StartsBeforeWindow ? UnitLeft(0) : UnitLeft(0) + summary.StartAt * unitColWidth + 0.5;
                var barRight = Math.Max(barLeft + 6, summary.EndsAfterWindow ? UnitLeft(unitsToShow) : UnitLeft(0) + summary.EndAt * unitColWidth - 0.5);
                var barRect = new Rectangle
                {
                    Width = barRight - barLeft, Height = barHeight, Fill = BarBrush(summary),
                    RadiusX = summary.StartsBeforeWindow || summary.EndsAfterWindow ? 0 : 3,
                    RadiusY = summary.StartsBeforeWindow || summary.EndsAfterWindow ? 0 : 3
                };
                Canvas.SetLeft(barRect, barLeft);
                Canvas.SetTop(barRect, rowTop + 2);
                canvas.Children.Add(barRect);

                var barText = $"{(summary.StartsBeforeWindow ? "◀ " : "")}{summary.DatesText}{(summary.EndsAfterWindow ? " ▶" : "")}";
                var fitted = WrapWords(barText, boldTypeface, 7.5, Math.Max(10, barRight - barLeft - 8)).FirstOrDefault() ?? string.Empty;
                AddText(canvas, fitted, barLeft + 4, rowTop + 4, boldTypeface, 7.5, Brushes.White);
            }

            // A goal's target date: a dashed line from the left edge of the dates to a diamond, and a thin
            // line down the row from it, at the day within its month. It sits just under a Summary bar.
            if (row.Target is { } target && TimelineLayout.TargetPosition(target, unitStarts, rangeEnd) is { } at)
            {
                var targetX = UnitLeft(at.Unit) + Math.Clamp(at.Fraction * unitColWidth, 1, unitColWidth - 1);
                var targetY = rowTop + 2 + barHeight + 4;
                canvas.Children.Add(new Line
                {
                    X1 = UnitLeft(0), Y1 = targetY, X2 = targetX, Y2 = targetY, Stroke = TargetBrush, StrokeThickness = 1, StrokeDashArray = [4, 3]
                });
                canvas.Children.Add(new Line { X1 = targetX, Y1 = targetY, X2 = targetX, Y2 = rowTop + rowHeight, Stroke = TargetBrush, StrokeThickness = 1 });
                canvas.Children.Add(new Polygon
                {
                    Points = [new Point(targetX, targetY - 4.5), new Point(targetX + 4.5, targetY), new Point(targetX, targetY + 4.5), new Point(targetX - 4.5, targetY)],
                    Fill = TargetBrush
                });
            }
            else if (row.Target is { } later && later.Date >= rangeEnd)
            {
                // Beyond the right edge: the line runs right across to an arrowhead.
                var edgeX = UnitLeft(unitsToShow) - 1;
                var targetY = rowTop + 2 + barHeight + 4;
                canvas.Children.Add(new Line
                {
                    X1 = UnitLeft(0), Y1 = targetY, X2 = edgeX - 5, Y2 = targetY, Stroke = TargetBrush, StrokeThickness = 1, StrokeDashArray = [4, 3]
                });
                canvas.Children.Add(new Polygon
                {
                    Points = [new Point(edgeX - 6, targetY - 3.5), new Point(edgeX, targetY), new Point(edgeX - 6, targetY + 3.5)], Fill = TargetBrush
                });
            }

            // A goal's milestones: a teal line down the row, with what it is written beside it at the foot.
            foreach (var milestone in row.Milestones ?? [])
            {
                if (TimelineLayout.TargetPosition(milestone.Date, unitStarts, rangeEnd) is not { } where) continue;

                var milestoneX = UnitLeft(where.Unit) + Math.Clamp(where.Fraction * unitColWidth, 1, unitColWidth - 1);
                // It starts under a Summary bar rather than crossing its dates.
                canvas.Children.Add(new Line { X1 = milestoneX, Y1 = row.Summary is null ? rowTop : rowTop + 2 + barHeight, X2 = milestoneX, Y2 = rowTop + rowHeight, Stroke = MilestoneBrush, StrokeThickness = 1.5 });
                var text = WrapWords(milestone.Description, boldTypeface, 7, 90).FirstOrDefault() ?? milestone.Description;
                AddText(canvas, text, milestoneX + 3, rowTop + rowHeight - 11, boldTypeface, 7, MilestoneBrush);
            }

            foreach (var item in items)
            {
                var laneTop = rowTop + 2 + laneHeights.Take(row.FirstTaskLane + item.Lane).Sum();
                var lines = boxLines[item];
                var priorityColor = GetPriorityColor(item.Card.Priority);

                if (item.HasArrow)
                {
                    // Same arrow as on screen: from the box to the right-hand edge of the due
                    // column, stopping against a short upright bar, or off the right edge with no
                    // bar when the due date is beyond it. Level with the first line of text.
                    var arrowY = laneTop + chipPadding + lineHeight / 2;
                    var x1 = UnitLeft(item.ArrowFirstUnit);
                    // The head stops at the end of the due day, as on screen.
                    var x2 = Math.Max(x1 + 10, item.DueAfterWindow ? UnitLeft(unitsToShow) - 1 : UnitLeft(0) + item.EndAt * unitColWidth - 1);

                    if (!item.DueAfterWindow)
                    {
                        canvas.Children.Add(new Line { X1 = x2 + 0.75, Y1 = arrowY - 4.5, X2 = x2 + 0.75, Y2 = arrowY + 4.5, Stroke = Brushes.Black, StrokeThickness = 1.25 });
                    }
                    canvas.Children.Add(new Line { X1 = x1, Y1 = arrowY, X2 = x2 - 5, Y2 = arrowY, Stroke = Brushes.Black, StrokeThickness = 1.25 });
                    canvas.Children.Add(new Polygon
                    {
                        Points = [new Point(x2 - 6, arrowY - 3.5), new Point(x2, arrowY), new Point(x2 - 6, arrowY + 3.5)], Fill = Brushes.Black
                    });
                }

                var chipHeight = lines.Count * lineHeight + 2 * chipPadding;
                var chipX = UnitLeft(item.BoxUnit) + 1;
                var chip = new Rectangle
                {
                    Width = unitColWidth - 2, Height = chipHeight, RadiusX = 2, RadiusY = 2,
                    Stroke = new SolidColorBrush(priorityColor), StrokeThickness = 0.75,
                    // Dashed and unfilled when the start is before the left edge - see TimelineItem.
                    Fill = item.StartsBeforeWindow ? Brushes.White : new SolidColorBrush(LightenColor(priorityColor, 0.85))
                };
                if (item.StartsBeforeWindow) chip.StrokeDashArray = [3, 2];
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

            DrawToday(rowTop, rowHeight);
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
