using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.ViewModels;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace KanbanApp.Services;

public static class ReportService
{
    private static bool _fontResolverRegistered;

    private static void EnsureFontResolverRegistered()
    {
        if (_fontResolverRegistered) return;
        GlobalFontSettings.FontResolver = new SegoeUiFontResolver();
        _fontResolverRegistered = true;
    }

    private class SegoeUiFontResolver : IFontResolver
    {
        private static readonly string FontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        public byte[] GetFont(string faceName)
        {
            var fileName = faceName switch
            {
                "SegoeUI#Bold" => "segoeuib.ttf",
                "SegoeUI#Italic" => "segoeuii.ttf",
                "SegoeUI#BoldItalic" => "segoeuiz.ttf",
                _ => "segoeui.ttf"
            };
            return File.ReadAllBytes(Path.Combine(FontsDir, fileName));
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var faceName = (isBold, isItalic) switch
            {
                (true, true) => "SegoeUI#BoldItalic",
                (true, false) => "SegoeUI#Bold",
                (false, true) => "SegoeUI#Italic",
                _ => "SegoeUI#Regular"
            };
            return new FontResolverInfo(faceName);
        }
    }

    public static List<ReportRow> BuildRows(
        IEnumerable<ColumnViewModel> columns,
        HashSet<string> includeColumns,
        string projectFilter, string priorityFilter, string whoFilter, string goalFilter, string flagFilter, string dueFilter,
        IEnumerable<(CardViewModel Card, string ColumnName)>? archivedCards = null)
    {
        var rows = new List<ReportRow>();

        foreach (var column in columns)
        {
            if (!includeColumns.Contains(column.Name)) continue;

            foreach (var card in column.Cards)
            {
                if (!Matches(card, projectFilter, priorityFilter, whoFilter, goalFilter, flagFilter, dueFilter)) continue;

                rows.Add(BuildRow(card, column.DisplayName, isArchived: false));
            }
        }

        if (archivedCards is not null)
        {
            foreach (var (card, columnName) in archivedCards)
            {
                if (!Matches(card, projectFilter, priorityFilter, whoFilter, goalFilter, flagFilter, dueFilter)) continue;

                rows.Add(BuildRow(card, columnName, isArchived: true));
            }
        }

        return rows;
    }

    private static ReportRow BuildRow(CardViewModel card, string columnName, bool isArchived) => new()
    {
        Title = card.Title,
        ColumnName = columnName,
        ProjectName = card.ProjectName,
        Priority = card.Priority,
        DueDate = card.DueDate,
        Who = card.WhoId is null ? null : card.WhoName,
        GoalName = card.GoalName,
        Flags = card.Flags.Select(f => f.Name).ToList(),
        SubTasks = card.SubTasks.Select(s => (s.Title, s.IsDone)).ToList(),
        Notes = card.Notes,
        IsArchived = isArchived
    };

    private static bool Matches(CardViewModel card, string projectFilter, string priorityFilter, string whoFilter,
        string goalFilter, string flagFilter, string dueFilter)
    {
        if (projectFilter != "All" && card.ProjectName != projectFilter) return false;
        if (priorityFilter != "All" && card.Priority != priorityFilter) return false;

        if (whoFilter == "Unassigned")
        {
            if (card.WhoId is not null) return false;
        }
        else if (whoFilter != "All" && card.WhoName != whoFilter) return false;

        if (goalFilter == "Unassigned")
        {
            if (card.GoalId is not null) return false;
        }
        else if (goalFilter != "All" && card.GoalName != goalFilter) return false;

        if (flagFilter == "Unassigned")
        {
            if (card.Flags.Count > 0) return false;
        }
        else if (flagFilter != "All" && card.Flags.All(f => f.Name != flagFilter)) return false;

        if (dueFilter != "All")
        {
            var today = DateTime.Today;
            var matchesDue = dueFilter switch
            {
                "Today" => card.DueDate is not null && card.DueDate.Value.Date <= today,
                "Tomorrow" => card.DueDate?.Date == today.AddDays(1),
                "Within a Week" => card.DueDate is not null && card.DueDate.Value.Date >= today && card.DueDate.Value.Date <= today.AddDays(7),
                "No Due Date" => card.DueDate is null,
                _ => true
            };
            if (!matchesDue) return false;
        }

        return true;
    }

    private static List<IGrouping<string, ReportRow>> GroupRows(List<ReportRow> rows, string groupBy) => groupBy switch
    {
        "Status" => rows.GroupBy(r => r.ColumnName).ToList(),
        "Project" => rows.GroupBy(r => r.ProjectName).OrderBy(g => g.Key).ToList(),
        "Priority" => rows.GroupBy(r => r.Priority).OrderBy(g => g.Key).ToList(),
        "Who" => rows.GroupBy(r => string.IsNullOrWhiteSpace(r.Who) ? "Unassigned" : r.Who).OrderBy(g => g.Key).ToList(),
        "Goal" => rows.GroupBy(r => r.GoalName).OrderBy(g => g.Key).ToList(),
        _ => rows.GroupBy(_ => string.Empty).ToList()
    };

    private static List<string> BuildMetaParts(ReportRow row)
    {
        var parts = new List<string>
        {
            $"Status: {row.ColumnName}{(row.IsArchived ? " (Archived)" : "")}",
            $"Project: {row.ProjectName}",
            $"Priority: {row.Priority}"
        };

        if (row.DueDate is not null) parts.Add($"Due {row.DueDate:MMM d, yyyy}");
        parts.Add(string.IsNullOrWhiteSpace(row.Who) ? "Unassigned" : $"Who: {row.Who}");
        if (row.GoalName != "No Goal") parts.Add($"Goal: {row.GoalName}");
        if (row.Flags.Count > 0) parts.Add($"Flags: {string.Join(", ", row.Flags)}");
        if (row.SubTasks.Count > 0)
        {
            var done = row.SubTasks.Count(s => s.IsDone);
            parts.Add($"Sub-tasks: {done}/{row.SubTasks.Count}");
        }

        return parts;
    }

    private static string BuildStatusSummary(List<ReportRow> rows)
    {
        var counts = rows.GroupBy(r => r.IsArchived ? "Archived" : r.ColumnName)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}");
        return string.Join("   •   ", counts);
    }

    private static readonly SolidColorBrush BandAccentBrush = new(Color.FromRgb(0x1E, 0x3A, 0x5F));
    private static readonly SolidColorBrush BandEvenBrush = Brushes.White;
    private static readonly SolidColorBrush BandOddBrush = new(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly SolidColorBrush BandGroupedOddBrush = new(Color.FromRgb(0xE3, 0xF2, 0xFD));

    private static SolidColorBrush RowBandBrush(int rowIndex, bool isGrouped) =>
        rowIndex % 2 == 0 ? BandEvenBrush : (isGrouped ? BandGroupedOddBrush : BandOddBrush);

    public static FixedDocument BuildFixedDocument(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks)
    {
        const double pageWidth = 793.92;
        const double pageHeight = 1122.24;
        const double margin = 40;
        const double contentWidth = pageWidth - 2 * margin;
        const double topContentY = 40;
        const double bottomLimitY = pageHeight - 40;

        var regularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var italicTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        FormattedText MakeText(string s, Typeface tf, double size, Brush brush) =>
            new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

        List<string> WrapLine(string text, Typeface tf, double size, double maxWidth)
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
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

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

        const double bandHeight = 70;
        var titleBand = new System.Windows.Shapes.Rectangle { Width = pageWidth, Height = bandHeight, Fill = BandAccentBrush };
        Canvas.SetLeft(titleBand, 0);
        Canvas.SetTop(titleBand, 0);
        canvas.Children.Add(titleBand);
        AddText(canvas, title, margin, 18, boldTypeface, 20, Brushes.White);
        AddText(canvas, $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}  —  {rows.Count} task{(rows.Count == 1 ? "" : "s")}",
            margin, 46, regularTypeface, 10, new SolidColorBrush(Color.FromRgb(0xC0, 0xCB, 0xDA)));
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

                    var lines = new List<(string Text, Typeface Typeface, double Size, Brush Brush, double XOffset, double LineHeight)>
                    {
                        (row.Title, boldTypeface, 13, Brushes.Black, 0, 18)
                    };

                    var metaLine = string.Join("   •   ", BuildMetaParts(row));
                    foreach (var line in WrapLine(metaLine, regularTypeface, 10, contentWidth - 16))
                    {
                        lines.Add((line, regularTypeface, 10, Brushes.DimGray, 0, 14));
                    }

                    if (includeSubTasks && row.SubTasks.Count > 0)
                    {
                        foreach (var (subTitle, isDone) in row.SubTasks)
                        {
                            lines.Add(($"{(isDone ? "☑" : "☐")} {subTitle}", regularTypeface, 10, Brushes.Black, 16, 14));
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

    public static void SavePdf(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks, string filePath)
    {
        EnsureFontResolverRegistered();

        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);

        const double margin = 40;
        double y = margin;
        double width = page.Width.Point - 2 * margin;
        double pageWidth = page.Width.Point;

        var titleFont = new XFont("Segoe UI", 20, XFontStyleEx.Bold);
        var subtitleFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
        var groupFont = new XFont("Segoe UI", 13, XFontStyleEx.Bold);
        var rowTitleFont = new XFont("Segoe UI", 11, XFontStyleEx.Bold);
        var metaFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
        var subTaskFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
        var noteFont = new XFont("Segoe UI", 9, XFontStyleEx.Italic);

        var accentBrush = new XSolidBrush(XColor.FromArgb(0x1E, 0x3A, 0x5F));
        var subtitleBrush = new XSolidBrush(XColor.FromArgb(0xC0, 0xCB, 0xDA));
        var bandEvenBrush = XBrushes.White;
        var bandOddBrush = new XSolidBrush(XColor.FromArgb(0xF2, 0xF2, 0xF2));
        var bandGroupedOddBrush = new XSolidBrush(XColor.FromArgb(0xE3, 0xF2, 0xFD));

        void NewPageIfNeeded(double neededHeight)
        {
            if (y + neededHeight <= page.Height.Point - margin) return;
            page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            y = margin;
        }

        const double bandHeight = 70;
        gfx.DrawRectangle(accentBrush, 0, 0, pageWidth, bandHeight);
        gfx.DrawString(title, titleFont, XBrushes.White, new XPoint(margin, 30));
        gfx.DrawString($"Generated {DateTime.Now:MMM d, yyyy h:mm tt}  —  {rows.Count} task{(rows.Count == 1 ? "" : "s")}",
            subtitleFont, subtitleBrush, new XPoint(margin, 56));
        y = bandHeight + 24;

        if (rows.Count == 0)
        {
            gfx.DrawString("No tasks match the selected filters.", metaFont, XBrushes.Black, new XPoint(margin, y));
            doc.Save(filePath);
            return;
        }

        var metaBrush = new XSolidBrush(XColor.FromArgb(0x69, 0x69, 0x69));
        foreach (var line in WrapText(gfx, BuildStatusSummary(rows), metaFont, width))
        {
            gfx.DrawString(line, metaFont, metaBrush, new XPoint(margin, y));
            y += 13;
        }
        y += 10;

        var isGrouped = groupBy != "None" && !string.IsNullOrEmpty(groupBy);

        foreach (var group in GroupRows(rows, groupBy))
        {
            if (isGrouped)
            {
                NewPageIfNeeded(30);
                gfx.DrawString($"{group.Key} ({group.Count()})", groupFont, accentBrush, new XPoint(margin, y));
                y += 20;
                gfx.DrawLine(new XPen(XColor.FromArgb(0x1E, 0x3A, 0x5F), 1.2), new XPoint(margin - 8, y), new XPoint(pageWidth - margin + 8, y));
                y += 12;
            }

            var rowIndex = 0;
            foreach (var row in group)
            {
                var band = rowIndex % 2 == 0 ? bandEvenBrush : (isGrouped ? bandGroupedOddBrush : bandOddBrush);
                rowIndex++;

                var lines = new List<(string Text, XFont Font, XBrush Brush, double XOffset, double LineHeight)>
                {
                    (row.Title, rowTitleFont, XBrushes.Black, 0, 18)
                };

                var metaLine = string.Join("   •   ", BuildMetaParts(row));
                foreach (var line in WrapText(gfx, metaLine, metaFont, width - 16))
                {
                    lines.Add((line, metaFont, XBrushes.DimGray, 0, 13));
                }

                if (includeSubTasks && row.SubTasks.Count > 0)
                {
                    foreach (var (subTitle, isDone) in row.SubTasks)
                    {
                        lines.Add(($"{(isDone ? "[x]" : "[ ]")} {subTitle}", subTaskFont, XBrushes.Black, 16, 13));
                    }
                }

                if (includeNotes && !string.IsNullOrWhiteSpace(row.Notes))
                {
                    foreach (var line in WrapText(gfx, $"Notes: {row.Notes}", noteFont, width - 32))
                    {
                        lines.Add((line, noteFont, XBrushes.DimGray, 16, 13));
                    }
                }

                var rowHeight = lines.Sum(l => l.LineHeight) + 10;
                NewPageIfNeeded(rowHeight + 8);

                gfx.DrawRectangle(band, margin - 8, y - 4, width + 16, rowHeight);

                foreach (var line in lines)
                {
                    gfx.DrawString(line.Text, line.Font, line.Brush, new XPoint(margin + line.XOffset, y));
                    y += line.LineHeight;
                }

                y += 10;
            }
        }

        doc.Save(filePath);
    }

    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }
}
