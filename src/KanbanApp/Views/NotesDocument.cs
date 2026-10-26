using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using KanbanApp.Services;

namespace KanbanApp.Views;

// Turns the Notes editor's document into the notes model (RichNotes) and back. Reading keeps only paragraphs, tables,
// bold, italic, underline and left/centre/right alignment - whatever else a pasted document carries (fonts, colours,
// lists, pictures) is reduced to its text - and building makes the same plain-looking document every time.
internal static class NotesDocument
{
    private static readonly Brush TableLine = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98));

    // --- Reading the document ---------------------------------------------------------------------------

    public static List<RichBlock> Read(FlowDocument document)
    {
        var blocks = new List<RichBlock>();
        ReadBlocks(document.Blocks, blocks);
        return blocks;
    }

    private static void ReadBlocks(IEnumerable<Block> source, List<RichBlock> into)
    {
        foreach (var block in source)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    into.Add(RichBlock.Of(ReadParagraph(paragraph)));
                    break;
                case Table table:
                    into.Add(RichBlock.Of(new RichTable(table.RowGroups.SelectMany(g => g.Rows).Select(row =>
                        (IReadOnlyList<RichParagraph>)row.Cells.Select(ReadCell).ToList()).ToList())));
                    break;
                case Section section:
                    ReadBlocks(section.Blocks, into);
                    break;
                case List list:
                    foreach (var item in list.ListItems) ReadBlocks(item.Blocks, into);
                    break;
                // A picture or other embedded control has no text.
            }
        }
    }

    // A cell holds one paragraph in the model: its blocks' text, one line each, and the first block's alignment.
    private static RichParagraph ReadCell(TableCell cell)
    {
        var lines = new List<RichBlock>();
        ReadBlocks(cell.Blocks, lines);
        var paragraphs = lines.Select(b => b.Paragraph).OfType<RichParagraph>().ToList();
        if (paragraphs.Count == 0) return new RichParagraph(NotesAlign.Left, []);

        var runs = new List<RichRun>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (i > 0) runs.Add(new RichRun("\n"));
            runs.AddRange(paragraphs[i].Runs);
        }
        return new RichParagraph(paragraphs[0].Align, Merge(runs));
    }

    private static RichParagraph ReadParagraph(Paragraph paragraph)
    {
        var runs = new List<RichRun>();
        ReadInlines(paragraph.Inlines, runs);
        var align = paragraph.TextAlignment switch { TextAlignment.Center => NotesAlign.Center, TextAlignment.Right => NotesAlign.Right, _ => NotesAlign.Left };
        return new RichParagraph(align, Merge(runs));
    }

    private static void ReadInlines(IEnumerable<Inline> inlines, List<RichRun> into)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when run.Text.Length > 0:
                    into.Add(new RichRun(run.Text,
                        run.FontWeight.ToOpenTypeWeight() >= 600,
                        run.FontStyle == FontStyles.Italic || run.FontStyle == FontStyles.Oblique,
                        run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline)));
                    break;
                case LineBreak:
                    into.Add(new RichRun("\n"));
                    break;
                case Span span: // Bold, Italic, Underline, Hyperlink and the like: their runs say how they look
                    ReadInlines(span.Inlines, into);
                    break;
            }
        }
    }

    // Neighbouring runs that look the same become one.
    private static List<RichRun> Merge(List<RichRun> runs)
    {
        var merged = new List<RichRun>();
        foreach (var run in runs)
        {
            if (merged.Count > 0 && merged[^1] is { } last && last.Bold == run.Bold && last.Italic == run.Italic && last.Underline == run.Underline)
                merged[^1] = last with { Text = last.Text + run.Text };
            else
                merged.Add(run);
        }
        return merged;
    }

    // --- Building the document --------------------------------------------------------------------------

    public static void Fill(FlowDocument document, IReadOnlyList<RichBlock> blocks)
    {
        document.Blocks.Clear();
        foreach (var block in blocks)
        {
            if (block.Paragraph is { } paragraph) document.Blocks.Add(Build(paragraph));
            else if (block.Table is { } table) document.Blocks.Add(Build(table));
        }
        if (document.Blocks.Count == 0 || document.Blocks.LastBlock is Table) document.Blocks.Add(new Paragraph()); // somewhere to type after a table
    }

    public static Paragraph Build(RichParagraph source)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0), TextAlignment = ToWpf(source.Align) };
        foreach (var run in source.Runs)
        {
            var parts = run.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) paragraph.Inlines.Add(new LineBreak());
                if (parts[i].Length == 0) continue;

                var element = new Run(parts[i]);
                if (run.Bold) element.FontWeight = FontWeights.Bold;
                if (run.Italic) element.FontStyle = FontStyles.Italic;
                if (run.Underline) element.TextDecorations = TextDecorations.Underline;
                paragraph.Inlines.Add(element);
            }
        }
        return paragraph;
    }

    public static Table Build(RichTable source)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 2, 0, 2), BorderBrush = TableLine, BorderThickness = new Thickness(1, 1, 0, 0) };
        var columns = Math.Max(1, source.Rows.Count == 0 ? 1 : source.Rows.Max(r => r.Count));
        for (var c = 0; c < columns; c++) table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();
        foreach (var row in source.Rows)
        {
            var tableRow = new TableRow();
            for (var c = 0; c < columns; c++) tableRow.Cells.Add(NewCell(c < row.Count ? row[c] : new RichParagraph(NotesAlign.Left, [])));
            group.Rows.Add(tableRow);
        }
        table.RowGroups.Add(group);
        return table;
    }

    public static TableCell NewCell(RichParagraph content) =>
        new(Build(content)) { BorderBrush = TableLine, BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(5, 2, 5, 2) };

    private static TextAlignment ToWpf(NotesAlign align) => align switch { NotesAlign.Center => TextAlignment.Center, NotesAlign.Right => TextAlignment.Right, _ => TextAlignment.Left };
}
