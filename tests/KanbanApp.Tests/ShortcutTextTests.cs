using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Keyboard shortcuts in tooltips are shown in bold: which words count as a shortcut, and that the
// tooltip text itself is never changed.
[Collection(WpfCollection.Name)]
public sealed class ShortcutTextTests(WpfDispatcherFixture wpf)
{
    private static string[] Bold(string text) => ShortcutText.Split(text).Where(p => p.IsShortcut).Select(p => p.Text).ToArray();

    [Theory]
    [InlineData("Create a new task (Ctrl+N). Right-click to start from a template, or to manage templates (Alt+M).", "Ctrl+N|Alt+M")]
    [InlineData("Run a saved report view in two clicks: preview, print or PDF (Ctrl+Shift+P)", "Ctrl+Shift+P")]
    [InlineData("Cards picked with Ctrl+click or Shift+click. Drag any of them to move them all together. Esc clears the selection.", "Ctrl+click|Shift+click|Esc")]
    [InlineData("Rename it, on the list and on every task that has it (F2)", "F2")]
    [InlineData("Make it a higher priority (Alt+Up)", "Alt+Up")]
    [InlineData("Take this answer off the list (Delete key)", "Delete")]
    [InlineData("Start typing, or press the Down arrow, to pick an earlier answer. Shift+Delete on a highlighted one forgets it.", "Shift+Delete")]
    [InlineData("Set the board's filters how you like, then click here to save the combination to Alt+0 - Alt+9.", "Alt+0 - Alt+9")]
    [InlineData("Saved custom filters:\nAlt+1: Highs — Priority: High\nAlt+2: Sam", "Alt+1|Alt+2")]
    [InlineData("Undo: Move \"Order parts\" (Ctrl+Z)", "Ctrl+Z")]
    [InlineData("Type the task and press Enter.", "Enter")]
    [InlineData("Quick add (Ctrl+Alt+N)", "Ctrl+Alt+N")]
    public void TheShortcutsAreBold(string tooltip, string expected) =>
        Assert.Equal(expected.Split('|'), Bold(tooltip));

    [Theory]
    [InlineData("Delete")]                                           // a button's name, not a key
    [InlineData("Enter a name for the new project")]                 // "enter" as a word
    [InlineData("Show only cards whose title, project, who, or notes contain this text")]
    [InlineData("Sam's F150 order, or the 2+2 plan")]                 // look-alikes
    [InlineData("Saved to Alt+{slot} when you click Save")]
    [InlineData("Escalate it, or Escape the plan")]
    public void OrdinaryWords_StayPlain(string tooltip) =>
        Assert.Empty(Bold(tooltip));

    [Fact]
    public void TheTextItself_IsUnchanged()
    {
        const string text = "Cards picked with Ctrl+click or Shift+click.\nEsc clears the selection (Alt+0-9).";
        Assert.Equal(text, string.Concat(ShortcutText.Split(text).Select(p => p.Text)));
        Assert.Equal(["Ctrl+click", "Shift+click", "Esc", "Alt+0-9"], Bold(text));
    }

    [Fact]
    public void TheTooltip_ShowsTheKeysInBold() => wpf.Run(() =>
    {
        var block = new TextBlock();
        ShortcutText.SetText(block, "Close the application (Ctrl+Q)");

        var runs = block.Inlines.OfType<Run>().ToList();
        Assert.Equal("Close the application (Ctrl+Q)", string.Concat(runs.Select(r => r.Text)));
        Assert.Equal(["Ctrl+Q"], runs.Where(r => r.FontWeight == FontWeights.Bold).Select(r => r.Text));
    });

    [Fact]
    public void OnlyPlainTextTooltips_GetTheBoldLayout() => wpf.Run(() =>
    {
        var template = new DataTemplate();
        var selector = new ToolTipTextSelector { TextTemplate = template };

        Assert.Same(template, selector.SelectTemplate("Close (Ctrl+Q)", new DependencyObject()));
        Assert.Null(selector.SelectTemplate(new TextBlock(), new DependencyObject()));
    });

    // Every "Ctrl+", "Alt+" or "Shift+" written in a tooltip in the app's windows comes out bold, so a
    // new tooltip worded a new way shows up here rather than slipping through plain.
    [Fact]
    public void EveryShortcutInTheWindowsTooltips_IsBold()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var tooltips = Directory.EnumerateFiles(Path.Combine(dir.FullName, "src"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "ToolTip=\"([^\"]*)\"").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)))
            .Where(t => Regex.IsMatch(t, @"(Ctrl|Alt|Shift)\+"))
            .ToList();
        Assert.NotEmpty(tooltips);

        var missed = tooltips.Where(t =>
        {
            var bold = string.Join(" ", Bold(t));
            return Regex.Matches(t, @"(?:(?:Ctrl|Alt|Shift)\+)+\w+").Any(m => !bold.Contains(m.Value));
        }).ToList();
        Assert.Empty(missed);
    }
}
