using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KanbanApp.Services;

// The statistics report on paper. Unlike the task list, it is laid out once - in points, on an A4
// page - into a list of texts, boxes and lines per page, and that one layout is drawn twice: on
// screen and printed (a FixedDocument), and as a PDF. So the two always match.
public static partial class ReportService
{
    private static readonly Color StatsAccent = Color.FromRgb(0x1E, 0x3A, 0x5F);
    private static readonly Color StatsBand = Color.FromRgb(0xC0, 0xCB, 0xDA);
    private static readonly Color StatsTile = Color.FromRgb(0xEE, 0xF2, 0xF7);
    private static readonly Color StatsStripe = Color.FromRgb(0xF2, 0xF2, 0xF2);
    private static readonly Color StatsBar = Color.FromRgb(0x2A, 0x78, 0xD6);
    private static readonly Color StatsMuted = Color.FromRgb(0x69, 0x69, 0x69);
    private static readonly Color StatsRule = Color.FromRgb(0xD3, 0xD3, 0xD3);

    // The charts' colours, told apart by people with colour blindness: blue for what was finished
    // (and on time), a pale blue for what was added beside it, orange for late, grey for still open.
    private static readonly Color ChartFinished = Color.FromRgb(0x2A, 0x78, 0xD6);
    private static readonly Color ChartAdded = Color.FromRgb(0xA8, 0xCB, 0xF0);
    private static readonly Color ChartLate = Color.FromRgb(0xE0, 0x73, 0x2F);
    private static readonly Color ChartOpen = Color.FromRgb(0xA6, 0xA6, 0xA6);
    private static readonly Color ChartGrid = Color.FromRgb(0xE4, 0xE4, 0xE4);

    public static FixedDocument BuildStatisticsDocument(string title, TaskStatisticsResult result, SavedStatisticsSections sections, string breakdown,
        string overTime, bool isLandscape, string? parameterSummary)
    {
        var pages = LayOutStatistics(title, result, sections, breakdown, overTime, isLandscape, parameterSummary);
        const double dipsPerPoint = 96.0 / 72.0;

        var fixedDoc = new FixedDocument();
        foreach (var page in pages)
        {
            var canvas = new Canvas { Width = page.Width, Height = page.Height, Background = Brushes.White, RenderTransform = new ScaleTransform(dipsPerPoint, dipsPerPoint) };
            foreach (var item in page.Items) DrawOnCanvas(canvas, item);

            var fixedPage = new FixedPage { Width = page.Width * dipsPerPoint, Height = page.Height * dipsPerPoint };
            fixedPage.Children.Add(canvas);
            var content = new PageContent();
            ((IAddChild)content).AddChild(fixedPage);
            fixedDoc.Pages.Add(content);
        }
        return fixedDoc;
    }

    public static void SaveStatisticsPdf(string title, TaskStatisticsResult result, SavedStatisticsSections sections, string breakdown,
        string overTime, bool isLandscape, string? parameterSummary, string filePath)
    {
        EnsureFontResolverRegistered();
        var doc = new PdfDocument();
        foreach (var layout in LayOutStatistics(title, result, sections, breakdown, overTime, isLandscape, parameterSummary))
        {
            var page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            using var gfx = XGraphics.FromPdfPage(page);
            foreach (var item in layout.Items) DrawOnPdf(gfx, item);
        }
        doc.Save(filePath);
    }

    // Which sections a statistics report shows.
    public sealed record SavedStatisticsSections(bool Summary, bool Breakdown, bool OverTime, bool ColumnTimes, bool Charts = true);

    // ----- Layout -----

    private abstract record StatsItem;
    private sealed record StatsText(double X, double Top, string Text, double Size, bool Bold, bool Italic, Color Color, bool AlignRight = false, bool Centre = false) : StatsItem;
    private sealed record StatsBox(double X, double Top, double Width, double Height, Color Fill) : StatsItem;
    private sealed record StatsLine(double X1, double Y1, double X2, double Y2, Color Stroke, double Thickness) : StatsItem;
    private sealed record StatsPage(double Width, double Height, List<StatsItem> Items);

    private sealed record StatsColumn(string Header, double Share, bool AlignRight);

    private static readonly Typeface StatsRegular = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface StatsBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    // Widths for laying out, in points (FormattedText scales with the size, so the units carry over).
    private static double MeasureStats(string text, double size, bool bold) =>
        new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? StatsBold : StatsRegular, size, Brushes.Black, 1.0).WidthIncludingTrailingWhitespace;

    private static string FitStats(string text, double size, bool bold, double width)
    {
        if (MeasureStats(text, size, bold) <= width) return text;
        while (text.Length > 1 && MeasureStats(text + "…", size, bold) > width) text = text[..^1];
        return text.TrimEnd() + "…";
    }

    private static List<StatsPage> LayOutStatistics(string title, TaskStatisticsResult result, SavedStatisticsSections sections, string breakdown,
        string overTime, bool isLandscape, string? parameterSummary)
    {
        const double a4Short = 595.28, a4Long = 841.89, margin = 40, bottomGap = 48;
        var pageWidth = isLandscape ? a4Long : a4Short;
        var pageHeight = isLandscape ? a4Short : a4Long;
        var contentWidth = pageWidth - 2 * margin;

        var pages = new List<StatsPage>();
        List<StatsItem> items = null!;
        double y = 0;

        void NewPage()
        {
            items = [];
            pages.Add(new StatsPage(pageWidth, pageHeight, items));
            y = 44;
        }

        bool Fits(double height) => y + height <= pageHeight - bottomGap;
        void EnsureSpace(double height) { if (!Fits(height)) NewPage(); }

        // The title band, the same as the task list's: title and when it was made on the left, what
        // the report covers on the right.
        NewPage();
        var paramLines = string.IsNullOrEmpty(parameterSummary)
            ? []
            : TextWrap.Lines(parameterSummary, s => MeasureStats(s, 11, false), contentWidth * 0.55);
        var bandHeight = Math.Max(70, 44 + paramLines.Count * 15 + 12);
        items.Add(new StatsBox(0, 0, pageWidth, bandHeight, StatsAccent));
        // A long title steps down in size to fit beside what the report covers, and is only shortened
        // if it still doesn't fit at 14.
        var titleWidth = contentWidth * 0.42;
        var titleSize = 20.0;
        while (titleSize > 14 && MeasureStats(title, titleSize, true) > titleWidth) titleSize--;
        items.Add(new StatsText(margin, 12 + (20 - titleSize) / 2, FitStats(title, titleSize, true, titleWidth), titleSize, true, false, Colors.White));
        items.Add(new StatsText(margin, 44, $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}", 10, false, false, StatsBand));
        var paramY = 44.0;
        foreach (var line in paramLines)
        {
            items.Add(new StatsText(pageWidth - margin, paramY, line, 11, false, false, StatsBand, AlignRight: true));
            paramY += 15;
        }
        y = bandHeight + 22;

        void Heading(string text, double keepWithNext)
        {
            EnsureSpace(34 + keepWithNext);
            items.Add(new StatsText(margin, y, text, 14, true, false, StatsAccent));
            y += 21;
            items.Add(new StatsLine(margin, y, margin + contentWidth, y, StatsAccent, 1));
            y += 10;
        }

        void Note(string text)
        {
            foreach (var line in TextWrap.Lines(text, s => MeasureStats(s, 9, false), contentWidth))
            {
                EnsureSpace(13);
                items.Add(new StatsText(margin, y, line, 9, false, true, StatsMuted));
                y += 13;
            }
            y += 4;
        }

        // Rows of big figures: the number, what it is, and a line of detail.
        void Figures(IReadOnlyList<(string Value, string Label, string? Detail)> figures)
        {
            const int perRow = 3;
            const double gap = 10, height = 58;
            var width = (contentWidth - gap * (perRow - 1)) / perRow;
            for (var i = 0; i < figures.Count; i += perRow)
            {
                EnsureSpace(height + gap);
                for (var j = 0; j < perRow && i + j < figures.Count; j++)
                {
                    var (value, label, detail) = figures[i + j];
                    var x = margin + j * (width + gap);
                    items.Add(new StatsBox(x, y, width, height, StatsTile));
                    items.Add(new StatsText(x + 10, y + 6, value, 20, true, false, StatsAccent));
                    items.Add(new StatsText(x + 10, y + 32, FitStats(label, 9, true, width - 20), 9, true, false, Colors.Black));
                    if (detail is not null) items.Add(new StatsText(x + 10, y + 44, FitStats(detail, 8, false, width - 20), 8, false, false, StatsMuted));
                }
                y += height + gap;
            }
            y += 4;
        }

        // A table with a dark header row (repeated on a new page) and banded rows. A bar column, when
        // given, draws each row's value as a bar scaled to the largest.
        void Table(IReadOnlyList<StatsColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows, int barColumn = -1, IReadOnlyList<int>? barValues = null)
        {
            const double headerHeight = 18, rowHeight = 16, size = 9;
            var xs = new List<double>();
            var x0 = margin;
            foreach (var column in columns) { xs.Add(x0); x0 += column.Share * contentWidth; }
            var barMax = barValues is { Count: > 0 } ? Math.Max(1, barValues.Max()) : 1;

            void Header()
            {
                items.Add(new StatsBox(margin, y, contentWidth, headerHeight, StatsAccent));
                for (var c = 0; c < columns.Count; c++)
                {
                    var width = columns[c].Share * contentWidth;
                    var text = FitStats(columns[c].Header, size, true, width - 10);
                    items.Add(columns[c].AlignRight
                        ? new StatsText(xs[c] + width - 5, y + 3, text, size, true, false, Colors.White, AlignRight: true)
                        : new StatsText(xs[c] + 5, y + 3, text, size, true, false, Colors.White));
                }
                y += headerHeight;
            }

            EnsureSpace(headerHeight + rowHeight * Math.Min(rows.Count, 3));
            Header();
            for (var r = 0; r < rows.Count; r++)
            {
                if (!Fits(rowHeight)) { NewPage(); Header(); }
                if (r % 2 == 1) items.Add(new StatsBox(margin, y, contentWidth, rowHeight, StatsStripe));
                for (var c = 0; c < columns.Count; c++)
                {
                    var width = columns[c].Share * contentWidth;
                    if (c == barColumn && barValues is not null)
                    {
                        var length = (width - 10) * barValues[r] / barMax;
                        if (length > 0) items.Add(new StatsBox(xs[c] + 5, y + 4, Math.Max(length, 1.5), rowHeight - 8, StatsBar));
                        continue;
                    }
                    var text = FitStats(rows[r][c], size, false, width - 10);
                    items.Add(columns[c].AlignRight
                        ? new StatsText(xs[c] + width - 5, y + 2, text, size, false, false, Colors.Black, AlignRight: true)
                        : new StatsText(xs[c] + 5, y + 2, text, size, false, false, Colors.Black));
                }
                y += rowHeight;
            }
            y += 14;
        }

        // ----- Charts. Each is drawn under a short title, with a line saying how to read it. -----

        void ChartTitle(string text, double keepWithNext)
        {
            EnsureSpace(18 + keepWithNext);
            items.Add(new StatsText(margin, y, text, 10.5, true, false, Colors.Black));
            y += 16;
        }

        void Legend(IReadOnlyList<(string Label, Color Color)> entries)
        {
            var x = margin;
            foreach (var (label, color) in entries)
            {
                items.Add(new StatsBox(x, y + 2, 9, 9, color));
                items.Add(new StatsText(x + 13, y, label, 8.5, false, false, StatsMuted));
                x += 13 + MeasureStats(label, 8.5, false) + 16;
            }
            y += 15;
        }

        // Up to five gridlines at a round step, so the scale reads at a glance.
        static (double Top, double Step) Scale(double max)
        {
            if (max <= 0) return (1, 1);
            var rough = max / 4;
            var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            var step = new[] { 1, 2, 5, 10 }.Select(m => m * power).First(st => st >= rough);
            step = Math.Max(step, 1);
            return (Math.Ceiling(max / step) * step, step);
        }

        // Upright bars, one or more side by side for each label, against a scale on the left.
        void ColumnChart(IReadOnlyList<(string Label, IReadOnlyList<int> Values)> groups, IReadOnlyList<Color> colors)
        {
            const double height = 120, axisWidth = 30, labelSize = 7.5;
            EnsureSpace(height + 30);
            y += 8; // room for the top figure on the scale
            var (top, step) = Scale(groups.SelectMany(g => g.Values).DefaultIfEmpty(0).Max());
            var plotLeft = margin + axisWidth;
            var plotWidth = contentWidth - axisWidth;
            var bottom = y + height;

            for (var v = 0.0; v <= top + step / 2; v += step)
            {
                var gy = bottom - v / top * height;
                items.Add(new StatsLine(plotLeft, gy, plotLeft + plotWidth, gy, v == 0 ? StatsMuted : ChartGrid, v == 0 ? 0.8 : 0.5));
                items.Add(new StatsText(plotLeft - 5, gy - 5, v.ToString("0", CultureInfo.CurrentCulture), labelSize, false, false, StatsMuted, AlignRight: true));
            }

            var slot = plotWidth / Math.Max(groups.Count, 1);
            var series = Math.Max(colors.Count, 1);
            var barWidth = Math.Min(16, slot * 0.72 / series);
            var every = (int)Math.Ceiling(groups.Count / Math.Max(1.0, Math.Floor(plotWidth / 46))); // labels at least ~46pt apart
            for (var i = 0; i < groups.Count; i++)
            {
                var centre = plotLeft + slot * (i + 0.5);
                var left = centre - barWidth * series / 2;
                for (var k = 0; k < groups[i].Values.Count; k++)
                {
                    var h = groups[i].Values[k] / top * height;
                    if (h > 0) items.Add(new StatsBox(left + k * barWidth, bottom - h, Math.Max(barWidth - 1, 1), h, colors[k % colors.Count]));
                }
                if (i % every == 0) items.Add(new StatsText(centre, bottom + 3, groups[i].Label, labelSize, false, false, StatsMuted, Centre: true));
            }
            y = bottom + 17;
        }

        // Sideways bars, one per label, each split into its parts, with the figure at the end.
        void BarChart(IReadOnlyList<(string Label, IReadOnlyList<(double Value, Color Color)> Parts, string Figure)> bars)
        {
            const double rowHeight = 15, barHeight = 9, size = 8.5;
            var labelWidth = contentWidth * 0.28;
            var barLeft = margin + labelWidth;
            var barSpace = contentWidth - labelWidth - 46;
            var max = Math.Max(bars.Select(b => b.Parts.Sum(p => p.Value)).DefaultIfEmpty(0).Max(), 1e-9);
            var axisTop = y;

            foreach (var (label, parts, figure) in bars)
            {
                if (!Fits(rowHeight))
                {
                    items.Add(new StatsLine(barLeft, axisTop, barLeft, y, StatsMuted, 0.8));
                    NewPage();
                    axisTop = y;
                }
                items.Add(new StatsText(margin, y + 1, FitStats(label, size, false, labelWidth - 8), size, false, false, Colors.Black));
                var x = barLeft;
                foreach (var (value, color) in parts)
                {
                    var w = barSpace * value / max;
                    if (w > 0) items.Add(new StatsBox(x, y + (rowHeight - barHeight) / 2, Math.Max(w, 1.5), barHeight, color));
                    x += w;
                }
                items.Add(new StatsText(x + 5, y + 1, figure, size, false, false, StatsMuted));
                y += rowHeight;
            }
            items.Add(new StatsLine(barLeft, axisTop, barLeft, y, StatsMuted, 0.8));
            y += 6;
        }

        // One bar the full width, split into shares of a whole, each labelled inside when it fits.
        void ShareBar(IReadOnlyList<(string Label, int Count, Color Color)> parts)
        {
            const double height = 18;
            EnsureSpace(height + 22);
            var total = Math.Max(parts.Sum(p => p.Count), 1);
            var x = margin;
            foreach (var (_, count, color) in parts)
            {
                var w = contentWidth * count / total;
                if (w <= 0) continue;
                items.Add(new StatsBox(x, y, w, height, color));
                var share = $"{Math.Round(count * 100.0 / total):0}%";
                if (MeasureStats(share, 9, true) + 8 < w) items.Add(new StatsText(x + w / 2, y + 3, share, 9, true, false, Colors.White, Centre: true));
                x += w;
            }
            y += height + 5;
            Legend(parts.Select(p => ($"{p.Label} ({p.Count})", p.Color)).ToList());
        }

        var s = result.Summary;
        var period = StatisticsPeriods.Describe(result.From, result.To);
        var charts = sections.Charts;

        if (result.TaskCount == 0)
        {
            Note("No tasks match the selected filters.");
        }
        else
        {
            if (sections.Summary)
            {
                Heading($"Summary, {period}", 70);
                Figures(
                [
                    (s.Added.ToString(CultureInfo.CurrentCulture), "Added", "put on the board in the period"),
                    (s.Finished.ToString(CultureInfo.CurrentCulture), "Finished", "reached Done in the period"),
                    (TaskStatistics.OnTimeShare(s.FinishedOnTime, s.FinishedWithDueDate), "Finished on time",
                        s.FinishedWithDueDate == 0 ? "none finished had a due date" : $"{s.FinishedOnTime} of {s.FinishedWithDueDate} with a due date"),
                    (TaskStatistics.Days(s.TypicalDaysToFinish), "Typical days to finish", "added to Done, middle value"),
                    (s.OpenNow.ToString(CultureInfo.CurrentCulture), "Open now", "on the board, not in Done"),
                    (s.OverdueNow.ToString(CultureInfo.CurrentCulture), "Overdue now", "open, past the due date")
                ]);

                if (charts && s.FinishedWithDueDate > 0)
                {
                    ChartTitle("Finished on time", 40);
                    ShareBar([("On time", s.FinishedOnTime, ChartFinished), ("Late", s.FinishedWithDueDate - s.FinishedOnTime, ChartLate)]);
                    Note("Of the tasks finished in the period that had a due date: blue were finished on or before it, orange after it.");
                }

                if (charts && result.FinishTimes.Any(f => f.Tasks > 0))
                {
                    ChartTitle("How long finished tasks took", 140);
                    ColumnChart(result.FinishTimes.Select(f => (f.Label, (IReadOnlyList<int>)[f.Tasks])).ToList(), [ChartFinished]);
                    Note("Each bar counts the tasks finished in the period that took that long from being added to reaching Done. A tall bar on the left means most work is quick; bars on the right are the long-running tasks the typical figure doesn't show.");
                }
            }

            if (sections.Breakdown && breakdown != "None")
            {
                var label = breakdown switch { "Who" => "Person", _ => breakdown };
                Heading($"By {label.ToLowerInvariant()}", charts && result.Groups.Count > 0 ? 60 + 15 * Math.Min(result.Groups.Count, 15) : 40); // with its chart, if there is one
                if (breakdown == "Who") Note("A shared task counts under each of its people.");
                if (result.Groups.Count == 0) Note("Nothing to show for this period.");
                else
                {
                    if (charts)
                    {
                        const int most = 15;
                        var shown = result.Groups.OrderByDescending(g => g.Finished + g.OpenNow).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).Take(most).ToList();
                        ChartTitle($"Finished and open now, by {label.ToLowerInvariant()}", 21 + 15 * shown.Count); // kept on one page
                        Legend([("Finished in the period", ChartFinished), ("Open now", ChartOpen)]);
                        BarChart(shown.Select(g => (g.Name, (IReadOnlyList<(double, Color)>)[(g.Finished, ChartFinished), (g.OpenNow, ChartOpen)],
                            $"{g.Finished} + {g.OpenNow}")).ToList());
                        Note((result.Groups.Count > most ? $"The {most} with the most tasks; the table below lists all {result.Groups.Count}. " : "")
                            + "A long blue part is where work got done; a long grey part is where it is waiting to be done.");
                    }

                    Table(
                    [
                        new(label, 0.32, false), new("Added", 0.10, true), new("Finished", 0.11, true), new("On time", 0.11, true),
                        new("Typical days", 0.14, true), new("Open now", 0.11, true), new("Overdue", 0.11, true)
                    ],
                    result.Groups.Select(g => (IReadOnlyList<string>)
                    [
                        g.Name, g.Added.ToString(CultureInfo.CurrentCulture), g.Finished.ToString(CultureInfo.CurrentCulture),
                        TaskStatistics.OnTimeShare(g.FinishedOnTime, g.FinishedWithDueDate), TaskStatistics.Days(g.TypicalDaysToFinish),
                        g.OpenNow.ToString(CultureInfo.CurrentCulture), g.OverdueNow.ToString(CultureInfo.CurrentCulture)
                    ]).ToList());
                }
            }

            if (sections.OverTime)
            {
                Heading(overTime == "Month" ? "Month by month" : "Week by week", charts ? 160 : 40);
                if (charts)
                {
                    var spansYears = result.From.Year != result.To.Year;
                    string Short(DateTime start) => overTime == "Month"
                        ? start.ToString(spansYears ? "MMM yy" : "MMM", CultureInfo.CurrentCulture)
                        : start.ToString(spansYears ? "MMM d yy" : "MMM d", CultureInfo.CurrentCulture);
                    Legend([("Added", ChartAdded), ("Finished", ChartFinished)]);
                    ColumnChart(result.Buckets.Select(b => (Short(b.Start), (IReadOnlyList<int>)[b.Added, b.Finished])).ToList(), [ChartAdded, ChartFinished]);
                    Note($"Each pair is one {(overTime == "Month" ? "month" : "week, from its Monday")}: tasks added, then tasks finished. Where the blue bar is the taller, more was finished than came in and the backlog shrank.");
                    Table(
                    [
                        new(overTime == "Month" ? "Month" : "Week", 0.40, false), new("Added", 0.20, true), new("Finished", 0.20, true), new("Backlog change", 0.20, true)
                    ],
                    result.Buckets.Select(b => (IReadOnlyList<string>)
                        [b.Label, b.Added.ToString(CultureInfo.CurrentCulture), b.Finished.ToString(CultureInfo.CurrentCulture),
                         (b.Added - b.Finished).ToString("+0;-0;0", CultureInfo.CurrentCulture)]).ToList());
                }
                else
                {
                    Table(
                    [
                        new(overTime == "Month" ? "Month" : "Week", 0.30, false), new("Added", 0.12, true), new("Finished", 0.12, true), new("", 0.46, false)
                    ],
                    result.Buckets.Select(b => (IReadOnlyList<string>)
                        [b.Label, b.Added.ToString(CultureInfo.CurrentCulture), b.Finished.ToString(CultureInfo.CurrentCulture), ""]).ToList(),
                    barColumn: 3, barValues: result.Buckets.Select(b => b.Finished).ToList());
                }
            }

            if (sections.ColumnTimes)
            {
                Heading("Time in each column", 40);
                Note("For the tasks finished in this period: how long they spent in each column on the way to Done. Typical is the middle value.");
                if (result.ColumnTimes.Count == 0) Note("No tasks were finished in this period.");
                else
                {
                    if (charts)
                    {
                        ChartTitle("Typical days in each column", 20 + 15 * result.ColumnTimes.Count);
                        BarChart(result.ColumnTimes.Select(c => (c.Column, (IReadOnlyList<(double, Color)>)[(c.TypicalDays, ChartFinished)],
                            c.TypicalDays < 1 ? "under 1 day" : $"{TaskStatistics.Days(c.TypicalDays)} days")).ToList());
                        Note("The longest bar is where tasks wait longest on their way to Done.");
                    }

                    Table(
                    [
                        new("Column", 0.34, false), new("Tasks", 0.16, true), new("Typical days", 0.25, true), new("Longest days", 0.25, true)
                    ],
                    result.ColumnTimes.Select(c => (IReadOnlyList<string>)
                    [
                        c.Column, c.Tasks.ToString(CultureInfo.CurrentCulture), TaskStatistics.Days(c.TypicalDays), TaskStatistics.Days(c.LongestDays)
                    ]).ToList());
                }
            }
        }

        // The running header on later pages and the page numbers, as on the task list.
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            if (i > 0)
            {
                page.Items.Add(new StatsText(margin, 12, FitStats(title, 9, false, contentWidth * 0.6), 9, false, false, Colors.Gray));
                page.Items.Add(new StatsText(pageWidth - margin, 13, $"Generated {DateTime.Now:MMM d, yyyy}", 8, false, false, Colors.Gray, AlignRight: true));
                page.Items.Add(new StatsLine(margin, 28, pageWidth - margin, 28, StatsRule, 0.75));
            }
            page.Items.Add(new StatsLine(margin, pageHeight - 34, pageWidth - margin, pageHeight - 34, StatsRule, 0.75));
            page.Items.Add(new StatsText(pageWidth / 2, pageHeight - 28, $"Page {i + 1} of {pages.Count}", 9, false, false, Colors.Gray, Centre: true));
        }

        return pages;
    }

    // ----- Drawing -----

    private static void DrawOnCanvas(Canvas canvas, StatsItem item)
    {
        switch (item)
        {
            case StatsBox box:
                var rect = new System.Windows.Shapes.Rectangle { Width = box.Width, Height = box.Height, Fill = new SolidColorBrush(box.Fill) };
                Canvas.SetLeft(rect, box.X);
                Canvas.SetTop(rect, box.Top);
                canvas.Children.Add(rect);
                break;
            case StatsLine line:
                canvas.Children.Add(new System.Windows.Shapes.Line { X1 = line.X1, Y1 = line.Y1, X2 = line.X2, Y2 = line.Y2, Stroke = new SolidColorBrush(line.Stroke), StrokeThickness = line.Thickness });
                break;
            case StatsText text:
                var block = new TextBlock
                {
                    Text = text.Text, FontFamily = StatsRegular.FontFamily, FontSize = text.Size,
                    FontWeight = text.Bold ? FontWeights.Bold : FontWeights.Normal, FontStyle = text.Italic ? FontStyles.Italic : FontStyles.Normal,
                    Foreground = new SolidColorBrush(text.Color)
                };
                var width = text.AlignRight || text.Centre ? MeasureStats(text.Text, text.Size, text.Bold) : 0;
                Canvas.SetLeft(block, text.AlignRight ? text.X - width : text.Centre ? text.X - width / 2 : text.X);
                Canvas.SetTop(block, text.Top);
                canvas.Children.Add(block);
                break;
        }
    }

    private static void DrawOnPdf(XGraphics gfx, StatsItem item)
    {
        static XColor X(Color c) => XColor.FromArgb(c.A, c.R, c.G, c.B);

        switch (item)
        {
            case StatsBox box:
                gfx.DrawRectangle(new XSolidBrush(X(box.Fill)), box.X, box.Top, box.Width, box.Height);
                break;
            case StatsLine line:
                gfx.DrawLine(new XPen(X(line.Stroke), line.Thickness), line.X1, line.Y1, line.X2, line.Y2);
                break;
            case StatsText text:
                var style = text.Bold && text.Italic ? XFontStyleEx.BoldItalic : text.Bold ? XFontStyleEx.Bold : text.Italic ? XFontStyleEx.Italic : XFontStyleEx.Regular;
                var font = new XFont(PdfFontResolver.FamilyName, text.Size, style);
                var width = text.AlignRight || text.Centre ? gfx.MeasureString(text.Text, font).Width : 0;
                var x = text.AlignRight ? text.X - width : text.Centre ? text.X - width / 2 : text.X;
                gfx.DrawString(text.Text, font, new XSolidBrush(X(text.Color)), x, text.Top, XStringFormats.TopLeft);
                break;
        }
    }
}
