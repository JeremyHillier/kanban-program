namespace KanbanApp.Services;

// Word-wrapping for everything the app draws by hand: the report preview, the report PDF and the printed
// Timeline. Each caller passes its own way of measuring text (WPF FormattedText, or PdfSharp's
// MeasureString), so the rules are the same wherever a report is drawn.
internal static class TextWrap
{
    // The text wrapped line by line. Titles and Notes come from multi-line boxes, and each drawn line is one
    // line high, so a line break left inside a "line" would draw two lines in the space of one, on top of
    // whatever comes next. Each paragraph is wrapped on its own; an empty one stays as an empty line.
    public static List<string> Lines(string text, Func<string, double> width, double maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            lines.AddRange(Words(paragraph, width, maxWidth));
        return lines;
    }

    // One paragraph wrapped at its spaces. A word wider than the line keeps a line to itself rather than
    // being cut. Always at least one line: "" gives one empty line.
    public static List<string> Words(string paragraph, Func<string, double> width, double maxWidth)
    {
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in paragraph.Split(' '))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (width(candidate) > maxWidth && current.Length > 0)
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
        return lines;
    }
}
