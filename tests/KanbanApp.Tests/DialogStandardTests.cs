using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using KanbanApp.Services;
using KanbanApp.Theming;

namespace KanbanApp.Tests;

// The dialog standard, read from the XAML and the source so the next dialog keeps to it: footer buttons one size, the save
// words, a margin of 12, an underlined letter on every labelled button, and the band, reveal and keys registered at startup.
public sealed class DialogStandardTests
{
    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "KanbanApp");
    }

    // The board's pop-up and the splash are not dialogs.
    private static readonly string[] NotDialogs = ["QuickAddWindow", "SplashWindow"];

    private static IEnumerable<(string Name, XDocument Doc)> Dialogs() =>
        Directory.GetFiles(Path.Combine(Source(), "Views"), "*Window.xaml")
            .Select(f => (Name: Path.GetFileNameWithoutExtension(f), Doc: XDocument.Load(f)))
            .Where(w => !NotDialogs.Contains(w.Name));

    private static string? A(XElement e, string name) =>
        (name == "x:Name" ? e.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") : e.Attribute(name))?.Value;
    private static bool Is(XElement e, string name) => e.Name.LocalName == name;
    private static bool InTemplate(XElement e) => e.Ancestors().Any(a => Is(a, "DataTemplate") || Is(a, "ControlTemplate"));

    private static XElement RootLayout(XDocument doc) => doc.Root!.Elements().First(e => !e.Name.LocalName.Contains('.'));

    [Fact]
    public void FooterButtons_AreOneSize()
    {
        var wrong = new List<string>();
        foreach (var (name, doc) in Dialogs())
        {
            var closers = doc.Descendants().Where(e => Is(e, "Button") && !InTemplate(e)
                && (A(e, "Content") is "Help" or "Close" or "Cancel" || A(e, "IsCancel") == "True"));
            foreach (var closer in closers)
            {
                // The buttons in the same row: the same panel, or in a grid the same grid row.
                var row = closer.Parent!.Elements().Where(e => Is(e, "Button")
                    && (!Is(closer.Parent!, "Grid") || A(e, "Grid.Row") == A(closer, "Grid.Row")));
                foreach (var button in row)
                {
                    var label = A(button, "Content") ?? A(button, "x:Name") ?? "(button)";
                    if (A(button, "Style") is not null || button.Elements().Any(c => c.Name.LocalName == "Button.Template")) continue;
                    if (A(button, "Content") is { Length: > 0 } content && !char.IsLetter(content[0])) continue; // a symbol, such as Mark Done's tick
                    var minWidth = double.TryParse(A(button, "MinWidth"), out var m) ? m : 0;
                    if (minWidth < 96 || A(button, "Padding") != "14,6" || A(button, "Width") is not null)
                        wrong.Add($"{name}: {label} is not MinWidth 96, Padding 14,6 with no Width");
                }
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Distinct()));
    }

    [Fact]
    public void ButtonsSayWhatTheyDo_NeverOkSaveChangesOrSaveAndAmpersand()
    {
        string[] banned = ["OK", "Save Changes", "Save & Close"];
        var wrong = Dialogs().SelectMany(w => w.Doc.Descendants().Where(e => Is(e, "Button") && banned.Contains(A(e, "Content")))
            .Select(e => $"{w.Name}: {A(e, "Content")}")).ToList();

        foreach (var file in Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("\"Save & Close\"") || text.Contains("\"Save Changes\"")) wrong.Add(Path.GetFileName(file));
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.Equal("Close", new DialogMessage("Saved", "Done.").Yes);
    }

    [Fact]
    public void EveryDialog_HasAMarginOf12()
    {
        // The task window and Settings inset each of their parts by 12 instead: their forms scroll to the window's edge.
        var ownInsets = new Dictionary<string, string?> { ["AddTaskWindow"] = "0,12,0,12", ["SettingsWindow"] = null };
        var wrong = new List<string>();
        foreach (var (name, doc) in Dialogs())
        {
            var margin = A(RootLayout(doc), "Margin");
            var expected = ownInsets.TryGetValue(name, out var own) ? own : "12";
            if (margin != expected) wrong.Add($"{name}: root margin {margin ?? "none"}, expected {expected ?? "none"}");
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // Help, Close, Cancel, Save, New and Delete keep F1, Esc, Ctrl+S, Ctrl+N and the Delete key instead of a letter.
    private static readonly string[] NoLetter = ["Help", "Close", "Cancel", "Save", "New", "Delete"];

    // Buttons whose label is set in code, so their letter cannot be marked here.
    private static readonly string[] LabelSetInCode = ["AddTaskWindow.ViewModeButton", "AddTaskWindow.SubmitButton", "PromptWindow.OkButton", "TaskFileDetailsWindow.OkButton"];

    [Fact]
    public void EveryLabelledButton_HasItsOwnUnderlinedLetter()
    {
        var wrong = new List<string>();
        foreach (var (name, doc) in Dialogs())
        {
            // Each tab is a scope of its own, together with whatever sits outside the tabs.
            var marked = doc.Descendants().Where(e => !InTemplate(e) && (A(e, "Content") ?? "").Contains('_') && !(A(e, "Content") ?? "").StartsWith('{'))
                .Select(e => (Tab: TabOf(e), Letter: char.ToUpperInvariant(A(e, "Content")![A(e, "Content")!.IndexOf('_') + 1]), Label: A(e, "Content")!)).ToList();
            foreach (var tab in marked.Select(m => m.Tab).Where(t => t != "").Distinct().Append(""))
            {
                var inScope = marked.Where(m => m.Tab == tab || m.Tab == "").ToList();
                foreach (var clash in inScope.GroupBy(m => m.Letter).Where(g => g.Count() > 1))
                    wrong.Add($"{name}{(tab == "" ? "" : " / " + tab)}: Alt+{clash.Key} on {string.Join(" and ", clash.Select(c => c.Label))}");
            }

            var buttons = doc.Descendants().Where(e => Is(e, "Button") && !InTemplate(e) && A(e, "Content") is { Length: > 0 } c && char.IsLetter(c[0])).ToList();
            foreach (var button in buttons)
            {
                var label = A(button, "Content")!;
                if (label.Contains('_') || LabelSetInCode.Contains($"{name}.{A(button, "x:Name")}")) continue;
                if (NoLetter.Contains(label.Split(' ')[0].TrimEnd('.'))) continue;
                if (buttons.Count(b => A(b, "Content") == label && TabOf(b) == TabOf(button)) > 1) continue; // a repeated label: no letter can tell them apart
                wrong.Add($"{name}: {label} has no underlined letter");
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    private static string TabOf(XElement e) => e.Ancestors().FirstOrDefault(a => Is(a, "TabItem")) is { } tab ? A(tab, "Header") ?? "?" : "";

    [Fact]
    public void Startup_AddsTheBandBeforeTheCopyright_AndRegistersTheRevealAndTheKeys()
    {
        var app = File.ReadAllText(Path.Combine(Source(), "App.xaml.cs"));

        Assert.True(app.IndexOf("DialogBand.Register()", StringComparison.Ordinal) is var band and > 0 && band < app.IndexOf("DialogCopyright.Register()", StringComparison.Ordinal));
        Assert.Contains("WindowReveal.Register()", app);
        Assert.Contains("AccessKeys.Register()", app);
    }

    [Fact]
    public void TheBandColours_AreDefined_AndTheRuleIsGold()
    {
        var xaml = File.ReadAllText(Path.Combine(Source(), "App.xaml"));

        Assert.Contains("x:Key=\"DialogBandBrush\"", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"DialogBandRuleBrush\" Color=\"#FFC9A24B\"/>", xaml);
        Assert.Contains("res[\"DialogBandBrush\"]", File.ReadAllText(Path.Combine(Source(), "Theming", "ThemeManager.cs")));
    }
}

[Collection(WpfCollection.Name)]
public sealed class DialogBandTests(WpfDispatcherFixture wpf)
{
    [Fact]
    public void TheBand_IsInset_Rounded_WithAGoldRule_AndTheTitleInCapitals() => wpf.Run(() =>
    {
        var window = new Window { Title = "Import Tasks" };
        var band = DialogBand.Build(window);
        var title = ((DockPanel)band.Child).Children.OfType<TextBlock>().Last();

        Assert.Equal(new Thickness(12, 12, 12, 0), band.Margin);
        Assert.Equal(new Thickness(16, 10, 16, 10), band.Padding);
        Assert.Equal(new Thickness(0, 0, 0, 2), band.BorderThickness);
        Assert.Equal(new CornerRadius(4), band.CornerRadius);
        Assert.Equal(17, title.FontSize);
        Assert.Equal(FontWeights.Bold, title.FontWeight);
        Assert.Equal("IMPORT TASKS", title.Text);

        DialogBand.SetTitle(window, "Import Tasks from Excel");
        Assert.Equal("IMPORT TASKS FROM EXCEL", title.Text);
        DialogBand.SetTitle(window, null);
        Assert.Equal("IMPORT TASKS", title.Text);
    });

    [Fact]
    public void TheBand_GoesAboveTheContent_Once() => wpf.Run(() =>
    {
        var content = new Grid();
        var window = new Window { Title = "Settings", Content = content };

        DialogBand.Add(window);
        DialogBand.Add(window);

        var host = Assert.IsType<DockPanel>(window.Content);
        Assert.Equal(2, host.Children.Count);
        Assert.IsType<Border>(host.Children[0]);
        Assert.Same(content, host.Children[1]);
    });

    [Fact]
    public void TheBoard_TheSplash_AndAFramelessWindow_GetNoBand() => wpf.Run(() =>
    {
        Assert.False(DialogBand.Wants(new Window { WindowStyle = WindowStyle.None }));
        Assert.True(DialogBand.Wants(new Window()));
    });
}
