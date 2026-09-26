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

        // Unlike the report, a chip's text is wrapped as one paragraph, and an empty last line is dropped.
        List<string> WrapWords(string text, Typeface tf, double size, double maxWidth)
        {
            var lines = TextWrap.Words(text, s => MakeText(s, tf, size, Brushes.Black).Width, maxWidth);
            if (lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
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
                parts.Add(TimelineLayout.DateLabel(task));

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
                    // Same arrow as on screen: from the box to the middle of the due column, or
                    // to the right edge when the due date is beyond it. Level with the first line
                    // of text.
                    var arrowY = laneTop + chipPadding + lineHeight / 2;
                    var x1 = UnitLeft(item.ArrowFirstUnit);
                    var x2 = UnitLeft(item.ArrowLastUnit + 1) - (item.DueAfterWindow ? 1 : unitColWidth / 2);

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
