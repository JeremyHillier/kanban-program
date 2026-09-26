using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.Services;

// The report on screen and on paper: Preview and Print draw it page by page as a FixedDocument.
public static partial class ReportService
{
    private static readonly SolidColorBrush BandAccentBrush = new(Color.FromRgb(0x1E, 0x3A, 0x5F));
    private static readonly SolidColorBrush BandEvenBrush = Brushes.White;
    private static readonly SolidColorBrush BandOddBrush = new(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly SolidColorBrush BandGroupedOddBrush = new(Color.FromRgb(0xE3, 0xF2, 0xFD));

    private static SolidColorBrush RowBandBrush(int rowIndex, bool isGrouped) =>
        rowIndex % 2 == 0 ? BandEvenBrush : (isGrouped ? BandGroupedOddBrush : BandOddBrush);

    public static FixedDocument BuildFixedDocument(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks, bool includeSubTaskSummary = false, bool isLandscape = false, string? parameterSummary = null)
    {
        const double a4Width = 793.92;
        const double a4Height = 1122.24;
        var pageWidth = isLandscape ? a4Height : a4Width;
        var pageHeight = isLandscape ? a4Width : a4Height;
        const double margin = 40;
        var contentWidth = pageWidth - 2 * margin;
        const double topContentY = 40;
        var bottomLimitY = pageHeight - 40;

        var regularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var italicTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        FormattedText MakeText(string s, Typeface tf, double size, Brush brush) =>
            new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

        List<string> WrapLine(string text, Typeface tf, double size, double maxWidth) =>
            TextWrap.Lines(text, s => MakeText(s, tf, size, Brushes.Black).Width, maxWidth);

        void AddText(Canvas targetCanvas, string text, double x, double top, Typeface tf, double size, Brush brush)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = tf.FontFamily,
                FontSize = size,
                FontWeight = tf.Weight,
                FontStyle = tf.Style,
                Foreground = brush
            };
            Canvas.SetLeft(block, x);
            Canvas.SetTop(block, top);
            targetCanvas.Children.Add(block);
        }

        // Right edge is pinned at rightX; the line's own measured width determines where it starts.
        void AddTextRight(Canvas targetCanvas, string text, double rightX, double top, Typeface tf, double size, Brush brush)
        {
            var width = MakeText(text, tf, size, brush).Width;
            AddText(targetCanvas, text, rightX - width, top, tf, size, brush);
        }

        var canvases = new List<Canvas>();
        Canvas canvas = null!;
        double y = 0;

        void NewPage()
        {
            canvas = new Canvas { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            canvases.Add(canvas);
            y = topContentY;
        }

        void EnsureSpace(double neededHeight)
        {
            if (y + neededHeight <= bottomLimitY) return;
            NewPage();
        }

        NewPage();

        var subtitleBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xCB, 0xDA));
        const double paramFontSize = 12;
        const double paramLineHeight = 16;
        const double bandMinHeight = 70;

        // Parameters sits to the right of Title/Generated, right-aligned, its first line level with
        // Generated - so it reads as a second column in the band rather than a separate block below
        // it. Wrapped to a bit over half the content width to keep clear of a long title on the left;
        // the band grows past its 70px minimum if that produces more lines than Title/Generated need.
        var paramLines = string.IsNullOrEmpty(parameterSummary)
            ? []
            : WrapLine(parameterSummary, regularTypeface, paramFontSize, contentWidth * 0.55);
        var bandHeight = Math.Max(bandMinHeight, 46 + paramLines.Count * paramLineHeight + 14);

        var titleBand = new System.Windows.Shapes.Rectangle { Width = pageWidth, Height = bandHeight, Fill = BandAccentBrush };
        Canvas.SetLeft(titleBand, 0);
        Canvas.SetTop(titleBand, 0);
        canvas.Children.Add(titleBand);
        AddText(canvas, title, margin, 18, boldTypeface, 20, Brushes.White);
        AddText(canvas, $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}  —  {rows.Count} task{(rows.Count == 1 ? "" : "s")}",
            margin, 46, regularTypeface, 10, subtitleBrush);

        var paramY = 46.0;
        foreach (var line in paramLines)
        {
            AddTextRight(canvas, line, pageWidth - margin, paramY, regularTypeface, paramFontSize, subtitleBrush);
            paramY += paramLineHeight;
        }

        y = bandHeight + 24;

        if (rows.Count == 0)
        {
            AddText(canvas, "No tasks match the selected filters.", margin, y, italicTypeface, 11, Brushes.Black);
        }
        else
        {
            var statusSummary = BuildStatusSummary(rows);
            foreach (var line in WrapLine(statusSummary, regularTypeface, 10, contentWidth))
            {
                AddText(canvas, line, margin, y, regularTypeface, 10, Brushes.DimGray);
                y += 14;
            }
            y += 10;

            var isGrouped = groupBy != "None" && !string.IsNullOrEmpty(groupBy);

            foreach (var group in GroupRows(rows, groupBy))
            {
                if (isGrouped)
                {
                    EnsureSpace(30);
                    AddText(canvas, $"{group.Key} ({group.Count()})", margin, y, boldTypeface, 14, BandAccentBrush);
                    y += 22;
                    var divider = new System.Windows.Shapes.Line { X1 = margin - 8, Y1 = y, X2 = pageWidth - margin + 8, Y2 = y, Stroke = BandAccentBrush, StrokeThickness = 1.2 };
                    canvas.Children.Add(divider);
                    y += 12;
                }

                var rowIndex = 0;
                foreach (var row in group)
                {
                    var band = RowBandBrush(rowIndex, isGrouped);
                    rowIndex++;

                    var lines = new List<(string Text, Typeface Typeface, double Size, Brush Brush, double XOffset, double LineHeight)>();
                    foreach (var titleLine in WrapLine(row.Title, boldTypeface, 13, contentWidth - 16))
                    {
                        lines.Add((titleLine, boldTypeface, 13, Brushes.Black, 0, 18));
                    }

                    var metaLine = string.Join("   •   ", BuildMetaParts(row, groupBy == "Who" ? group.Key : null));
                    foreach (var line in WrapLine(metaLine, regularTypeface, 10, contentWidth - 16))
                    {
                        lines.Add((line, regularTypeface, 10, Brushes.DimGray, 0, 14));
                    }

                    if (includeSubTasks && row.SubTasks.Count > 0)
                    {
                        foreach (var (subTitle, isDone) in row.SubTasks)
                        {
                            var subTaskText = $"{(isDone ? "☑" : "☐")} {subTitle}";
                            foreach (var subLine in WrapLine(subTaskText, regularTypeface, 10, contentWidth - 32))
                            {
                                lines.Add((subLine, regularTypeface, 10, Brushes.Black, 16, 14));
                            }
                        }
                    }

                    if (includeNotes && !string.IsNullOrWhiteSpace(row.Notes))
                    {
                        foreach (var line in WrapLine($"Notes: {row.Notes}", italicTypeface, 10, contentWidth - 32))
                        {
                            lines.Add((line, italicTypeface, 10, Brushes.DimGray, 16, 14));
                        }
                    }

                    var rowHeight = lines.Sum(l => l.LineHeight) + 10;
                    EnsureSpace(rowHeight + 8);

                    var rowBand = new System.Windows.Shapes.Rectangle { Width = contentWidth + 16, Height = rowHeight, Fill = band };
                    Canvas.SetLeft(rowBand, margin - 8);
                    Canvas.SetTop(rowBand, y - 4);
                    canvas.Children.Add(rowBand);

                    foreach (var line in lines)
                    {
                        AddText(canvas, line.Text, margin + line.XOffset, y, line.Typeface, line.Size, line.Brush);
                        y += line.LineHeight;
                    }

                    y += 10;
                }
            }
        }

        if (includeSubTaskSummary)
        {
            var summary = BuildSubTaskSummary(rows);
            if (summary.Count > 0)
            {
                EnsureSpace(50);
                y += 6;
                var divider = new System.Windows.Shapes.Line { X1 = margin - 8, Y1 = y, X2 = pageWidth - margin + 8, Y2 = y, Stroke = BandAccentBrush, StrokeThickness = 1.2 };
                canvas.Children.Add(divider);
                y += 16;
                AddText(canvas, "Sub-task Completion Summary", margin, y, boldTypeface, 16, BandAccentBrush);
                y += 26;

                string? lastParent = null;
                foreach (var s in summary)
                {
                    EnsureSpace(20);
                    if (s.ParentTitle != lastParent)
                    {
                        if (lastParent is not null) y += 6;
                        AddText(canvas, s.ParentTitle, margin, y, boldTypeface, 12, Brushes.Black);
                        y += 18;
                        lastParent = s.ParentTitle;
                    }
                    var pct = s.TotalCount == 0 ? 0 : s.CompletedCount * 100 / s.TotalCount;
                    AddText(canvas, $"{s.SubTaskTitle}: {s.CompletedCount}/{s.TotalCount} completed ({pct}%)",
                        margin + 16, y, regularTypeface, 11, Brushes.DimGray);
                    y += 16;
                }
            }
        }

        var fixedDoc = new FixedDocument();
        for (var i = 0; i < canvases.Count; i++)
        {
            var pageCanvas = canvases[i];

            if (i > 0)
            {
                AddText(pageCanvas, title, margin, 10, regularTypeface, 9, Brushes.Gray);
                var generated = $"Generated {DateTime.Now:MMM d, yyyy}";
                var generatedWidth = MakeText(generated, regularTypeface, 8, Brushes.LightGray).Width;
                AddText(pageCanvas, generated, pageWidth - margin - generatedWidth, 12, regularTypeface, 8, Brushes.LightGray);
                var headerRule = new System.Windows.Shapes.Line { X1 = margin, Y1 = 28, X2 = pageWidth - margin, Y2 = 28, Stroke = Brushes.LightGray, StrokeThickness = 0.75 };
                pageCanvas.Children.Add(headerRule);
            }

            var footerRule = new System.Windows.Shapes.Line { X1 = margin, Y1 = pageHeight - 34, X2 = pageWidth - margin, Y2 = pageHeight - 34, Stroke = Brushes.LightGray, StrokeThickness = 0.75 };
            pageCanvas.Children.Add(footerRule);
            var footerText = $"Page {i + 1} of {canvases.Count}";
            var footerWidth = MakeText(footerText, regularTypeface, 9, Brushes.Gray).Width;
            AddText(pageCanvas, footerText, (pageWidth - footerWidth) / 2, pageHeight - 26, regularTypeface, 9, Brushes.Gray);

            var fixedPage = new FixedPage { Width = pageWidth, Height = pageHeight };
            fixedPage.Children.Add(pageCanvas);
            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDoc.Pages.Add(pageContent);
        }

        return fixedDoc;
    }
}
