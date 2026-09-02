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

    // unionFilters, when non-empty, REPLACES the six discrete filter params entirely for matching
    // purposes: a card is included if it matches ANY one of the selected saved custom filters (an
    // OR/union across filters, not an AND on top of the dropdowns above) - each captured slot's own
    // Due/DueFrom/DueTo is used as-is, independent of dueRangeFrom/dueRangeTo/includeNoDueDate below
    // (those are specific to this report's own due-date-range fields, not to the saved filters).
    public static List<ReportRow> BuildRows(
        IEnumerable<ColumnViewModel> columns,
        HashSet<string> includeColumns,
        string projectFilter, string priorityFilter, string whoFilter, string goalFilter, string flagFilter, string dueFilter,
        DateTime? dueRangeFrom, DateTime? dueRangeTo, bool includeNoDueDate,
        List<CustomFilter>? unionFilters,
        string sortLevel1, string sortLevel2, string sortLevel3,
        ReportArchiveScope archiveScope = ReportArchiveScope.BoardOnly,
        IEnumerable<(CardViewModel Card, string ColumnName)>? archivedCards = null,
        DateTime? archivedFrom = null, DateTime? archivedTo = null)
    {
        bool CardMatches(CardViewModel card) => unionFilters is { Count: > 0 }
            ? unionFilters.Any(f => Matches(card, f))
            : Matches(card, projectFilter, priorityFilter, whoFilter, goalFilter, flagFilter, dueFilter, dueRangeFrom, dueRangeTo, includeNoDueDate);

        var columnList = columns.ToList();
        var categoryOrder = columnList.Select(c => c.DisplayName).ToList();

        var rows = new List<ReportRow>();

        if (archiveScope != ReportArchiveScope.ArchivedOnly)
        {
            foreach (var column in columnList)
            {
                if (!includeColumns.Contains(column.Name)) continue;

                foreach (var card in column.Cards)
                {
                    if (!CardMatches(card)) continue;

                    rows.Add(BuildRow(card, column.DisplayName, isArchived: false));
                }
            }
        }

        if (archiveScope != ReportArchiveScope.BoardOnly && archivedCards is not null)
        {
            foreach (var (card, columnName) in archivedCards)
            {
                if (!CardMatches(card)) continue;
                if (archivedFrom is not null && (card.ArchivedAt is null || card.ArchivedAt.Value.Date < archivedFrom.Value.Date)) continue;
                if (archivedTo is not null && (card.ArchivedAt is null || card.ArchivedAt.Value.Date > archivedTo.Value.Date)) continue;

                rows.Add(BuildRow(card, columnName, isArchived: true));
            }
        }

        // Sorted here (not in BuildFixedDocument/SavePdf) so both rendering paths get the same order
        // for free: LINQ's GroupBy preserves input order within each group, so pre-sorting the flat
        // list before it's grouped downstream naturally orders rows within whatever group they land in.
        return SortRows(rows, sortLevel1, sortLevel2, sortLevel3, categoryOrder);
    }

    public static List<SubTaskSummaryRow> BuildSubTaskSummary(List<ReportRow> rows) =>
        rows.SelectMany(r => r.SubTasks.Select(st => (ParentTitle: r.Title, SubTaskTitle: st.Title, st.IsDone)))
            .GroupBy(x => (x.ParentTitle, x.SubTaskTitle))
            .Select(g => new SubTaskSummaryRow
            {
                ParentTitle = g.Key.ParentTitle,
                SubTaskTitle = g.Key.SubTaskTitle,
                CompletedCount = g.Count(x => x.IsDone),
                TotalCount = g.Count()
            })
            .OrderBy(s => s.ParentTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SubTaskTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
        ArchivedAt = card.ArchivedAt,
        IsArchived = isArchived
    };

    private static bool Matches(CardViewModel card, string projectFilter, string priorityFilter, string whoFilter,
        string goalFilter, string flagFilter, string dueFilter, DateTime? dueRangeFrom = null, DateTime? dueRangeTo = null, bool includeNoDueDate = false)
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

        // A due-date range (set on the report itself, not part of a saved custom filter) takes
        // priority over the preset Due bucket when both happen to be present.
        if (dueRangeFrom is not null || dueRangeTo is not null)
        {
            var inRange = card.DueDate is not null
                && (dueRangeFrom is null || card.DueDate.Value.Date >= dueRangeFrom.Value.Date)
                && (dueRangeTo is null || card.DueDate.Value.Date <= dueRangeTo.Value.Date);
            var noDueDateOk = includeNoDueDate && card.DueDate is null;
            if (!inRange && !noDueDateOk) return false;
        }
        else if (dueFilter != "All")
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

    private static bool Matches(CardViewModel card, CustomFilter filter) =>
        Matches(card, filter.Project, filter.Priority, filter.Who, filter.Goal, filter.Flag, filter.Due,
            ParseDate(filter.DueFrom), ParseDate(filter.DueTo));

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static int PriorityRank(string priority) => priority switch
    {
        "High" => 0,
        "Medium" => 1,
        "Normal" => 2,
        "Low" => 3,
        _ => 2
    };

    // Ranks by the board's own column order (categoryOrder, taken from the Columns passed into
    // BuildRows - already in SortOrder) rather than alphabetically, so the default To Do/In
    // Progress/On Hold/Waiting/Done ordering falls out for free. Archived rows carry whatever
    // column they were archived from, but are always ranked last, as their own "Archived" tier.
    private static int CategoryRank(ReportRow row, List<string> categoryOrder)
    {
        if (row.IsArchived) return categoryOrder.Count + 1;
        var index = categoryOrder.IndexOf(row.ColumnName);
        return index < 0 ? categoryOrder.Count : index;
    }

    // Sorts the flat row list by up to three keys before it's grouped downstream ("None" entries are
    // skipped). Category maps to the same ColumnName field GroupBy calls "Status" - the Add/Edit Task
    // dialog calls the column-selection field "Category", so this reuses that naming for consistency
    // in the UI even though the underlying model field is ColumnName.
    private static List<ReportRow> SortRows(List<ReportRow> rows, string sortLevel1, string sortLevel2, string sortLevel3, List<string> categoryOrder)
    {
        var keys = new[] { sortLevel1, sortLevel2, sortLevel3 }.Where(k => !string.IsNullOrEmpty(k) && k != "None").ToList();
        if (keys.Count == 0) return rows;

        var ordered = ApplyOrderBy(rows, keys[0], categoryOrder);
        for (var i = 1; i < keys.Count; i++)
        {
            ordered = ApplyThenBy(ordered, keys[i], categoryOrder);
        }
        return ordered.ToList();
    }

    private static IOrderedEnumerable<ReportRow> ApplyOrderBy(IEnumerable<ReportRow> rows, string key, List<string> categoryOrder) => key switch
    {
        "Category" => rows.OrderBy(r => CategoryRank(r, categoryOrder)),
        "Priority" => rows.OrderBy(r => PriorityRank(r.Priority)),
        "Who" => rows.OrderBy(r => string.IsNullOrWhiteSpace(r.Who) ? "Unassigned" : r.Who, StringComparer.OrdinalIgnoreCase),
        "Due Date" => rows.OrderBy(r => r.DueDate ?? DateTime.MaxValue),
        "Project" => rows.OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        "Goal" => rows.OrderBy(r => r.GoalName, StringComparer.OrdinalIgnoreCase),
        _ => rows.OrderBy(_ => 0)
    };

    private static IOrderedEnumerable<ReportRow> ApplyThenBy(IOrderedEnumerable<ReportRow> rows, string key, List<string> categoryOrder) => key switch
    {
        "Category" => rows.ThenBy(r => CategoryRank(r, categoryOrder)),
        "Priority" => rows.ThenBy(r => PriorityRank(r.Priority)),
        "Who" => rows.ThenBy(r => string.IsNullOrWhiteSpace(r.Who) ? "Unassigned" : r.Who, StringComparer.OrdinalIgnoreCase),
        "Due Date" => rows.ThenBy(r => r.DueDate ?? DateTime.MaxValue),
        "Project" => rows.ThenBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        "Goal" => rows.ThenBy(r => r.GoalName, StringComparer.OrdinalIgnoreCase),
        _ => rows
    };

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

    public static FixedDocument BuildFixedDocument(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks, bool includeSubTaskSummary = false, bool isLandscape = false)
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

        List<string> WrapLine(string text, Typeface tf, double size, double maxWidth)
        {
            // Split on embedded newlines first (Title and Notes both come from multi-line text
            // boxes) - each rendered line is one TextBlock at a fixed height, so an unhandled
            // newline inside a single "line" made that TextBlock render two physical lines in a
            // space budgeted for one, overlapping whatever was rendered next.
            var lines = new List<string>();
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                var words = paragraph.Split(' ');
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
                lines.Add(current);
            }
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

                    var lines = new List<(string Text, Typeface Typeface, double Size, Brush Brush, double XOffset, double LineHeight)>();
                    foreach (var titleLine in WrapLine(row.Title, boldTypeface, 13, contentWidth - 16))
                    {
                        lines.Add((titleLine, boldTypeface, 13, Brushes.Black, 0, 18));
                    }

                    var metaLine = string.Join("   •   ", BuildMetaParts(row));
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

    public static void SavePdf(string title, List<ReportRow> rows, string groupBy, bool includeNotes, bool includeSubTasks, string filePath, bool includeSubTaskSummary = false, bool isLandscape = false)
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
            page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
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

                var lines = new List<(string Text, XFont Font, XBrush Brush, double XOffset, double LineHeight)>();
                foreach (var titleLine in WrapText(gfx, row.Title, rowTitleFont, width - 16))
                {
                    lines.Add((titleLine, rowTitleFont, XBrushes.Black, 0, 18));
                }

                var metaLine = string.Join("   •   ", BuildMetaParts(row));
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
                gfx.DrawString("Sub-task Completion Summary", new XFont("Segoe UI", 16, XFontStyleEx.Bold), accentBrush, new XPoint(margin, y));
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

    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        // See the matching comment on the Preview path's local WrapLine - same fix, same reason:
        // paragraphs must be split on embedded newlines before word-wrapping each one.
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = paragraph.Split(' ');
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

            lines.Add(current);
        }

        return lines;
    }
}
