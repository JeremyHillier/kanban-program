using KanbanApp.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace KanbanApp.Services;

// The report as a PDF file, drawn with PdfSharp to the same layout as the preview.
public static partial class ReportService
{
    private static bool _fontResolverRegistered;

    private static void EnsureFontResolverRegistered()
    {
        if (_fontResolverRegistered) return;
        GlobalFontSettings.FontResolver = new PdfFontResolver();
        _fontResolverRegistered = true;
    }

    public static void SavePdf(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks, string filePath, bool includeSubTaskSummary = false, bool isLandscape = false, string? parameterSummary = null)
    {
        EnsureFontResolverRegistered();

        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
        var gfx = XGraphics.FromPdfPage(page);

        const double margin = 40;
        double y = margin;
        double width = page.Width.Point - 2 * margin;
        double pageWidth = page.Width.Point;

        var titleFont = new XFont(PdfFontResolver.FamilyName, 20, XFontStyleEx.Bold);
        var subtitleFont = new XFont(PdfFontResolver.FamilyName, 10, XFontStyleEx.Regular);
        var groupFont = new XFont(PdfFontResolver.FamilyName, 13, XFontStyleEx.Bold);
        var rowTitleFont = new XFont(PdfFontResolver.FamilyName, 11, XFontStyleEx.Bold);
        var metaFont = new XFont(PdfFontResolver.FamilyName, 9, XFontStyleEx.Regular);
        var subTaskFont = new XFont(PdfFontResolver.FamilyName, 9, XFontStyleEx.Regular);
        var noteFont = new XFont(PdfFontResolver.FamilyName, 9, XFontStyleEx.Italic);

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
            page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            gfx = XGraphics.FromPdfPage(page);
            y = margin;
        }

        var paramFont = new XFont(PdfFontResolver.FamilyName, 12, XFontStyleEx.Regular);
        const double paramLineHeight = 16;
        const double bandMinHeight = 70;

        // Same "second column in the band, right-aligned, level with Generated" layout as the
        // WPF preview path - see BuildFixedDocument's matching comment.
        var paramLines = string.IsNullOrEmpty(parameterSummary)
            ? []
            : WrapText(gfx, parameterSummary, paramFont, width * 0.55);
        var bandHeight = Math.Max(bandMinHeight, 46 + paramLines.Count * paramLineHeight + 14);

        gfx.DrawRectangle(accentBrush, 0, 0, pageWidth, bandHeight);
        gfx.DrawString(title, titleFont, XBrushes.White, new XPoint(margin, 30));
        gfx.DrawString($"Generated {DateTime.Now:MMM d, yyyy h:mm tt}  —  {rows.Count} task{(rows.Count == 1 ? "" : "s")}",
            subtitleFont, subtitleBrush, new XPoint(margin, 56));

        var paramY = 56.0;
        foreach (var line in paramLines)
        {
            var lineWidth = gfx.MeasureString(line, paramFont).Width;
            gfx.DrawString(line, paramFont, subtitleBrush, new XPoint(pageWidth - margin - lineWidth, paramY));
            paramY += paramLineHeight;
        }

        y = bandHeight + 24;

        var metaBrush = new XSolidBrush(XColor.FromArgb(0x69, 0x69, 0x69));

        if (rows.Count == 0)
        {
            gfx.DrawString("No tasks match the selected filters.", metaFont, XBrushes.Black, new XPoint(margin, y));
            doc.Save(filePath);
            return;
        }

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

                var lines = new List<(string Text, XFont Font, XBrush Brush, double XOffset, double LineHeight)>();
                foreach (var titleLine in WrapText(gfx, row.Title, rowTitleFont, width - 16))
                {
                    lines.Add((titleLine, rowTitleFont, XBrushes.Black, 0, 18));
                }

                var metaLine = string.Join("   •   ", BuildMetaParts(row, groupBy == "Who" ? group.Key : null));
                foreach (var line in WrapText(gfx, metaLine, metaFont, width - 16))
                {
                    lines.Add((line, metaFont, XBrushes.DimGray, 0, 13));
                }

                if (includeSubTasks && row.SubTasks.Count > 0)
                {
                    foreach (var (subTitle, isDone) in row.SubTasks)
                    {
                        var subTaskText = $"{(isDone ? "[x]" : "[ ]")} {subTitle}";
                        foreach (var subLine in WrapText(gfx, subTaskText, subTaskFont, width - 32))
                        {
                            lines.Add((subLine, subTaskFont, XBrushes.Black, 16, 13));
                        }
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

        if (includeSubTaskSummary)
        {
            var summary = BuildSubTaskSummary(rows);
            if (summary.Count > 0)
            {
                NewPageIfNeeded(50);
                y += 6;
                gfx.DrawLine(new XPen(XColor.FromArgb(0x1E, 0x3A, 0x5F), 1.2), new XPoint(margin - 8, y), new XPoint(pageWidth - margin + 8, y));
                y += 16;
                gfx.DrawString("Sub-task Completion Summary", new XFont(PdfFontResolver.FamilyName, 16, XFontStyleEx.Bold), accentBrush, new XPoint(margin, y));
                y += 26;

                string? lastParent = null;
                foreach (var s in summary)
                {
                    NewPageIfNeeded(18);
                    if (s.ParentTitle != lastParent)
                    {
                        if (lastParent is not null) y += 6;
                        gfx.DrawString(s.ParentTitle, rowTitleFont, XBrushes.Black, new XPoint(margin, y));
                        y += 18;
                        lastParent = s.ParentTitle;
                    }
                    var pct = s.TotalCount == 0 ? 0 : s.CompletedCount * 100 / s.TotalCount;
                    gfx.DrawString($"{s.SubTaskTitle}: {s.CompletedCount}/{s.TotalCount} completed ({pct}%)",
                        metaFont, metaBrush, new XPoint(margin + 16, y));
                    y += 16;
                }
            }
        }

        doc.Save(filePath);
    }

    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth) =>
        TextWrap.Lines(text, s => gfx.MeasureString(s, font).Width, maxWidth);
}
