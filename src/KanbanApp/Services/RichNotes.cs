
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KanbanApp.Services;

// A task's Notes with simple formatting: paragraphs and tables, with bold, italic, underline and left/centre/right
// alignment. Nothing else is kept - no fonts, sizes or colours, nothing that could run - so what a pasted document
// brings along is reduced to this. The same text without the formatting (ToPlainText) is what the rest of the app
// reads: it stays in the task's ordinary Notes, and the formatting is stored beside it only when there is some.
public enum NotesAlign { Left, Center, Right }

public sealed record RichRun(string Text, bool Bold = false, bool Italic = false, bool Underline = false)
{
    public bool IsPlain => !Bold && !Italic && !Underline;
}

// A paragraph, or the paragraph inside a table cell. A line break inside it is "\n" in a run's text.
public sealed record RichParagraph(NotesAlign Align, IReadOnlyList<RichRun> Runs)
{
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

// ColumnWeights: how the width is shared between the columns (only the proportions matter); null shares it equally.
public sealed record RichTable(IReadOnlyList<IReadOnlyList<RichParagraph>> Rows, IReadOnlyList<double>? ColumnWeights = null);

// Exactly one of the two is set.
public sealed record RichBlock(RichParagraph? Paragraph, RichTable? Table)
{
    public static RichBlock Of(RichParagraph paragraph) => new(paragraph, null);
    public static RichBlock Of(RichTable table) => new(null, table);
}

public static class RichNotes
{
    // The plain text of the notes: a line per paragraph, a table as a line per row with its cells separated by tabs.
    // Lines end "\r\n", like text typed in an ordinary box.
    public static string ToPlainText(IReadOnlyList<RichBlock> blocks)
    {
        var lines = new List<string>();
        foreach (var block in blocks)
        {
            if (block.Paragraph is { } paragraph) lines.AddRange(paragraph.Text.Replace("\r", "").Split('\n'));
            else if (block.Table is { } table)
                lines.AddRange(table.Rows.Select(row => string.Join('\t', row.Select(cell => cell.Text.Replace("\r", "").Replace('\n', ' ')))));
        }
        return string.Join("\r\n", lines);
    }

    // Plain text as paragraphs, one per line.
    public static List<RichBlock> FromPlainText(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => RichBlock.Of(new RichParagraph(NotesAlign.Left, line.Length == 0 ? [] : [new RichRun(line)]))).ToList();

    // True when there is nothing a plain box could not show: no table, no formatted run, no alignment.
    public static bool IsPlain(IReadOnlyList<RichBlock> blocks) =>
        blocks.All(b => b.Table is null && b.Paragraph is { Align: NotesAlign.Left } p && p.Runs.All(r => r.IsPlain));

    // --- Storage: JSON, read back defensively (it is only ever data) ------------------------------------

    private sealed record StoredRun(string T, bool B = false, bool I = false, bool U = false);
    private sealed record StoredParagraph(string? A, List<StoredRun> R);
    private sealed record StoredBlock(StoredParagraph? P, List<List<StoredParagraph>>? T, List<double>? W = null);

    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    public static string Serialize(IReadOnlyList<RichBlock> blocks) => JsonSerializer.Serialize(blocks.Select(block => new StoredBlock(
        block.Paragraph is { } p ? Store(p) : null,
        block.Table?.Rows.Select(row => row.Select(Store).ToList()).ToList(),
        block.Table?.ColumnWeights?.Select(w => Math.Round(w, 3)).ToList())).ToList(), Options);

    private static StoredParagraph Store(RichParagraph p) => new(p.Align == NotesAlign.Left ? null : p.Align.ToString(), p.Runs.Select(r => new StoredRun(r.Text, r.Bold, r.Italic, r.Underline)).ToList());

    // Null for anything that is not what Serialize wrote.
    public static List<RichBlock>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredBlock>>(json, Options);
            return stored?.Select(block => block.T is { } rows
                ? RichBlock.Of(new RichTable(rows.Select(row => (IReadOnlyList<RichParagraph>)row.Select(Load).ToList()).ToList(), GoodWeights(block.W, rows.Max(r => r.Count))))
                : RichBlock.Of(Load(block.P ?? new StoredParagraph(null, [])))).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Saved weights are used only when there is one positive weight for each column.
    private static List<double>? GoodWeights(List<double>? weights, int columns) =>
        weights is { Count: > 0 } && weights.Count == columns && weights.All(w => w > 0 && double.IsFinite(w)) ? weights : null;

    private static RichParagraph Load(StoredParagraph p) =>
        new(Enum.TryParse<NotesAlign>(p.A, out var align) ? align : NotesAlign.Left,
            (p.R ?? []).Where(r => r.T is not null).Select(r => new RichRun(r.T, r.B, r.I, r.U)).ToList());

    // A table of empty cells, for Insert Table.
    public static RichTable EmptyTable(int rows, int columns) => new(Enumerable.Range(0, rows)
        .Select(_ => (IReadOnlyList<RichParagraph>)Enumerable.Range(0, columns).Select(_ => new RichParagraph(NotesAlign.Left, [])).ToList()).ToList());
}
