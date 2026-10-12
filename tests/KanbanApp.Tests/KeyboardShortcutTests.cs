using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Every shortcut the board handles is listed in Help, so there is one complete list to read.
public class KeyboardShortcutTests
{
    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "KanbanApp");
    }

    [Fact]
    public void EveryBoardShortcutLetter_IsInTheHelpList()
    {
        var root = SourceRoot();
        var keyboard = File.ReadAllText(Path.Combine(root, "MainWindow.Keyboard.cs"));
        var help = File.ReadAllText(Path.Combine(root, "Views", "HelpWindow.xaml"));

        var letters = Regex.Matches(keyboard, @"case Key\.([A-Z]):").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(letters);

        var missing = letters.Where(letter => !Regex.IsMatch(help, $@"(Ctrl|Alt)\+(Shift\+)?{letter}\b")).ToList();
        Assert.True(missing.Count == 0, "Shortcut letters handled on the board but not listed under Keyboard shortcuts in Help: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheHelpList_NamesEveryKeyTheWindowsBind()
    {
        var help = File.ReadAllText(Path.Combine(SourceRoot(), "Views", "HelpWindow.xaml"));
        foreach (var key in new[] { "F1", "F2", "Ctrl+S", "Ctrl+F", "Ctrl+C", "Delete", "Esc", "Enter", "Home", "Alt+Up", "Shift+Delete", "underlined letter" })
        {
            Assert.Contains(key, help);
        }
    }
}

[Collection(WpfCollection.Name)]
public sealed class AccessKeyTests(WpfDispatcherFixture wpf)
{
    [Fact]
    public void AMarkedLabel_LosesItsUnderscore_AndUnderlinesTheLetter() => wpf.Run(() =>
    {
        var button = new Button { Content = "Make De_fault" };
        var window = new Window { Content = new StackPanel { Children = { button } } };

        AccessKeys.Bind(window);

        var block = Assert.IsType<TextBlock>(button.Content);
        var runs = block.Inlines.OfType<System.Windows.Documents.Run>().ToList();
        Assert.Equal("Make Default", string.Concat(runs.Select(r => r.Text)));
        Assert.Equal("f", runs.Single(r => r.TextDecorations.Count > 0).Text);
    });

    [Fact]
    public void AMessageQuestion_UnderlinesDifferentLettersOnItsTwoVerbs() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.Ask("Replace", "Replace it?", "Replace", "Keep Both"));
        try
        {
            Assert.Equal("R", Underlined(window, "YesButton"));
            Assert.Equal("K", Underlined(window, "NoButton"));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void CancelOnAMessage_KeepsEscAndNoLetter() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.AskDanger("Delete", "Delete it?", "Delete"));
        try
        {
            Assert.Equal("D", Underlined(window, "YesButton"));
            Assert.Equal("Cancel", ((Button)window.FindName("NoButton")!).Content);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void AMessageVerbSharingTheFirstLetter_TakesItsNextLetter() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.Ask("Delete", "Delete it?", "Delete", "Don't Delete"));
        try
        {
            Assert.Equal("D", Underlined(window, "YesButton"));
            Assert.Equal("o", Underlined(window, "NoButton"));
        }
        finally
        {
            window.Close();
        }
    });

    private static string Underlined(Window window, string name)
    {
        var block = Assert.IsType<TextBlock>(((Button)window.FindName(name)!).Content);
        return block.Inlines.OfType<System.Windows.Documents.Run>().Single(r => r.TextDecorations.Count > 0).Text;
    }
}
