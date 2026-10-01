using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace KanbanApp.Views;

// Shows a tooltip's text with its keyboard shortcuts in bold ("Create a new task (**Ctrl+N**)"), so
// the keys stand out from the description. Every plain-text tooltip in the app goes through it, by
// way of the ToolTip style in App.xaml; a tooltip that is already laid out is left alone.
public static partial class ShortcutText
{
    // A key combination (Ctrl+N, Ctrl+Shift+P, Alt+Up, Shift+Delete, Ctrl+click), with an optional
    // range after it (Alt+0 - Alt+9, Alt+0-9); a function key (F2); Esc; "Delete key"; "press Enter".
    // Only capitals or digits count as a single key, so "Alt+{slot}" in a sentence isn't taken for one.
    private const string Key = @"(?:F\d{1,2}|Up|Down|Left|Right|Delete|Del|Enter|Esc|Tab|Space|Home|End|click|[A-Z0-9])";
    private const string Combo = @"(?:(?:Ctrl|Alt|Shift|Windows)\+)+" + Key;

    [GeneratedRegex(@"(?<![\w+])" + Combo + @"(?:(?:\s*-\s*|\s+to\s+)" + Combo + @"|-\d)?(?![\w+])"
        + @"|(?<![\w+])F\d{1,2}(?![\w+])"
        + @"|\bEsc\b"
        + @"|\bDelete(?= key\b)"
        + @"|(?<=\bpress )(?:Enter|Tab)\b")]
    private static partial Regex ShortcutPattern();

    // The text in order, each piece marked as a shortcut or not.
    internal static List<(string Text, bool IsShortcut)> Split(string text)
    {
        var pieces = new List<(string, bool)>();
        var at = 0;
        foreach (Match match in ShortcutPattern().Matches(text))
        {
            if (match.Index > at) pieces.Add((text[at..match.Index], false));
            pieces.Add((match.Value, true));
            at = match.Index + match.Length;
        }
        if (at < text.Length) pieces.Add((text[at..], false));
        return pieces;
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(ShortcutText), new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(TextBlock element) => (string?)element.GetValue(TextProperty);
    public static void SetText(TextBlock element, string? value) => element.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block) return;

        block.Inlines.Clear();
        foreach (var (text, isShortcut) in Split(e.NewValue as string ?? string.Empty))
        {
            var run = new Run(text);
            if (isShortcut) run.FontWeight = FontWeights.Bold;
            block.Inlines.Add(run);
        }
    }
}

// Picks the bold-shortcut layout for a tooltip whose content is plain text. Anything else (a tooltip
// built from its own controls) gets no template from here, so WPF shows it exactly as before.
public sealed class ToolTipTextSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is string ? TextTemplate : null;
}
