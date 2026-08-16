using System.IO;
using System.Windows;
using System.Windows.Documents;
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
        string projectFilter, string priorityFilter, string whoFilter, string goalFilter, string flagFilter, string dueFilter)
    {
        var rows = new List<ReportRow>();

        foreach (var column in columns)
        {
            if (!includeColumns.Contains(column.Name)) continue;

            foreach (var card in column.Cards)
            {
                if (!Matches(card, projectFilter, priorityFilter, whoFilter, goalFilter, flagFilter, dueFilter)) continue;

                rows.Add(new ReportRow
                {
                    Title = card.Title,
                    ColumnName = column.Name,
                    ProjectName = card.ProjectName,
                    Priority = card.Priority,
                    DueDate = card.DueDate,
                    Who = card.WhoId is null ? null : card.WhoName,
                    GoalName = card.GoalName,
                    Flags = card.Flags.Select(f => f.Name).ToList(),
                    SubTasks = card.SubTasks.Select(s => (s.Title, s.IsDone)).ToList(),
                    Notes = card.Notes
                });
            }
        }

        return rows;
    }

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
            $"Status: {row.ColumnName}",
            $"Project: {row.ProjectName}",
            $"Priority: {row.Priority}"
        };

        if (row.DueDate is not null) parts.Add($"Due {row.DueDate:MMM d, yyyy}");
        parts.Add(string.IsNullOrWhiteSpace(row.Who) ? "Unassigned" : $"Who: {row.Who}");
        if (row.GoalName != "No Goal") parts.Add($"Goal: {row.GoalName}");
        if (row.Flags.Count > 0) parts.Add($"Flags: {string.Join(", ", row.Flags)}");

        return parts;
    }

    private static readonly SolidColorBrush BandAccentBrush = new(Color.FromRgb(0x1E, 0x3A, 0x5F));
    private static readonly SolidColorBrush BandEvenBrush = Brushes.White;
    private static readonly SolidColorBrush BandOddBrush = new(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly SolidColorBrush BandGroupedOddBrush = new(Color.FromRgb(0xE3, 0xF2, 0xFD));

    private static SolidColorBrush RowBandBrush(int rowIndex, bool isGrouped) =>
        rowIndex % 2 == 0 ? BandEvenBrush : (isGrouped ? BandGroupedOddBrush : BandOddBrush);

    public static FlowDocument BuildFlowDocument(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity
        };

        doc.Blocks.Add(new Paragraph(new Run(title))
        {
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Background = BandAccentBrush,
            Padding = new Thickness(40, 16, 40, 4),
            Margin = new Thickness(0)
        });
        doc.Blocks.Add(new Paragraph(new Run($"Generated {DateTime.Now:MMM d, yyyy h:mm tt}  —  {rows.Count} task{(rows.Count == 1 ? "" : "s")}"))
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xCB, 0xDA)),
            Background = BandAccentBrush,
            Padding = new Thickness(40, 0, 40, 16),
            Margin = new Thickness(0, 0, 0, 18)
        });

        if (rows.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("No tasks match the selected filters.")) { FontStyle = FontStyles.Italic, Margin = new Thickness(40, 0, 40, 0) });
            return doc;
        }

        var isGrouped = groupBy != "None" && !string.IsNullOrEmpty(groupBy);

        foreach (var group in GroupRows(rows, groupBy))
        {
            if (isGrouped)
            {
                doc.Blocks.Add(new Paragraph(new Run(group.Key))
                {
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(40, 14, 40, 6),
                    Foreground = BandAccentBrush
                });
            }

            var rowIndex = 0;
            foreach (var row in group)
            {
                var band = RowBandBrush(rowIndex, isGrouped);
                rowIndex++;

                var hasSubTasks = includeSubTasks && row.SubTasks.Count > 0;
                var hasNotes = includeNotes && !string.IsNullOrWhiteSpace(row.Notes);
                var isLastBlock = !hasSubTasks && !hasNotes;

                var titlePara = new Paragraph
                {
                    Background = band,
                    Padding = new Thickness(40, 6, 40, isLastBlock ? 6 : 0),
                    Margin = new Thickness(0)
                };
                titlePara.Inlines.Add(new Run(row.Title) { FontWeight = FontWeights.Bold, FontSize = 13 });
                doc.Blocks.Add(titlePara);

                doc.Blocks.Add(new Paragraph(new Run(string.Join("   •   ", BuildMetaParts(row))))
                {
                    FontSize = 10,
                    Foreground = Brushes.DimGray,
                    Background = band,
                    Padding = new Thickness(40, 1, 40, (hasSubTasks || hasNotes) ? 0 : 6),
                    Margin = new Thickness(0)
                });

                if (hasSubTasks)
                {
                    var list = new List
                    {
                        Background = band,
                        Padding = new Thickness(56, 2, 40, hasNotes ? 0 : 6),
                        Margin = new Thickness(0),
                        MarkerStyle = TextMarkerStyle.None
                    };
                    foreach (var (subTitle, isDone) in row.SubTasks)
                    {
                        list.ListItems.Add(new ListItem(new Paragraph(new Run($"{(isDone ? "☑" : "☐")} {subTitle}")) { FontSize = 10, Margin = new Thickness(0) }));
                    }
                    doc.Blocks.Add(list);
                }

                if (hasNotes)
                {
                    doc.Blocks.Add(new Paragraph(new Run($"Notes: {row.Notes}"))
                    {
                        FontSize = 10,
                        FontStyle = FontStyles.Italic,
                        Foreground = Brushes.DimGray,
                        Background = band,
                        Padding = new Thickness(56, 2, 40, 6),
                        Margin = new Thickness(0)
                    });
                }
            }
        }

        return doc;
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

        var isGrouped = groupBy != "None" && !string.IsNullOrEmpty(groupBy);

        foreach (var group in GroupRows(rows, groupBy))
        {
            if (isGrouped)
            {
                NewPageIfNeeded(26);
                gfx.DrawString(group.Key, groupFont, accentBrush, new XPoint(margin, y));
                y += 24;
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
