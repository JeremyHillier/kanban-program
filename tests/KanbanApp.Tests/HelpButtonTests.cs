using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace KanbanApp.Tests;

// Every working window has one Help button at the bottom left of its footer, the place Windows puts a
// dialog's Help, opening Help at the window's own section; and every Close, Cancel and Exit button says
// that Esc does the same. Read from the XAML so the next window keeps to it.
public sealed class HelpButtonTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    // The prompts, message box, About, Legal, Update, the splash, Quick Add (a one-line pop-up) and
    // Help itself carry no Help button of their own; F1 still opens Help on the ones that are windows.
    private static readonly string[] WithoutHelp =
    [
        "AboutWindow", "DeleteRecurringTaskWindow", "HelpWindow", "LegalWindow", "MessageWindow", "PromptWindow",
        "QuickAddWindow", "SplashWindow", "UpdateAvailableWindow",
    ];

    private static string ViewsFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "KanbanApp", "Views");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("src/KanbanApp/Views");
    }

    private static IEnumerable<string> WindowFiles() => Directory.GetFiles(ViewsFolder(), "*Window.xaml");

    [Fact]
    public void EveryWorkingWindowHasOneHelpButtonAtTheLeft()
    {
        var wrong = new List<string>();
        foreach (var file in WindowFiles())
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var doc = XDocument.Load(file);
            var help = doc.Descendants(P + "Button").Where(b => (string?)b.Attribute("Content") == "Help").ToList();
            if (WithoutHelp.Contains(name))
            {
                if (help.Count != 0) wrong.Add($"{name}: has a Help button");
                continue;
            }
            if (help.Count != 1) { wrong.Add($"{name}: {help.Count} Help buttons"); continue; }
            var button = help[0];
            if (!((string?)button.Attribute("Command") ?? "").Contains("HelpKey.ShowCommand")) wrong.Add($"{name}: Help does not use HelpKey.ShowCommand");
            // At the left: docked left, aligned left, or first in a left-aligned row.
            var row = button.Parent!;
            var atLeft = (string?)button.Attribute("DockPanel.Dock") == "Left"
                         || (string?)button.Attribute("HorizontalAlignment") == "Left"
                         || (row.Name.LocalName == "StackPanel" && (string?)row.Attribute("HorizontalAlignment") == "Left" && row.Elements(P + "Button").First() == button)
                         || (row.Name.LocalName == "DockPanel" && row.Elements(P + "Button").First() == button);
            if (!atLeft) wrong.Add($"{name}: Help is not at the left of its footer");
            // Before Close or Cancel in the file order: Help leads the footer, the closer ends it.
            var footer = button.Ancestors().First(a => a.Name.LocalName is "Grid" or "DockPanel");
            var buttons = footer.Descendants(P + "Button").ToList();
            var closer = buttons.FirstOrDefault(b => (string?)b.Attribute("Content") is "Close" or "Cancel");
            if (closer is not null && buttons.IndexOf(closer) < buttons.IndexOf(button)) wrong.Add($"{name}: Help comes after {(string?)closer.Attribute("Content")}");
        }
        Assert.Empty(wrong);
    }

    [Fact]
    public void EveryCloseCancelAndExitNamesEsc()
    {
        var wrong = new List<string>();
        var closer = new Regex(@"<Button\b(?:""[^""]*""|[^<>""])*?\bContent=""(Close|Cancel|Exit)""(?:""[^""]*""|[^<>""])*?/>", RegexOptions.Singleline);
        foreach (var file in WindowFiles())
            foreach (Match m in closer.Matches(File.ReadAllText(file)))
            {
                var tip = Regex.Match(m.Value, @"ToolTip=""([^""]*)""");
                if (!tip.Success || !tip.Groups[1].Value.TrimEnd().EndsWith("(Esc)"))
                    wrong.Add($"{Path.GetFileNameWithoutExtension(file)}: {m.Groups[1].Value}");
            }
        Assert.Empty(wrong);
    }

    // Every section a window's Help opens on is a named header on the How it works tab.
    [Fact]
    public void EveryHelpTopicIsANamedSection()
    {
        var help = File.ReadAllText(Path.Combine(ViewsFolder(), "HelpWindow.xaml"));
        var missing = KanbanApp.Views.HelpKey.Topics.Values.Distinct().Where(s => !help.Contains($"x:Name=\"{s}\"")).ToList();
        Assert.Empty(missing);
    }
}
