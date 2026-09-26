using System.IO;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The app's message window: the point in bold, bullets in a grid of their own, the detail in a box, and
// the buttons it was given. Enter and Esc take the safe way out of a question that does damage.
[Collection(WpfCollection.Name)]
public sealed class MessageWindowTests(WpfDispatcherFixture wpf)
{
    private static Button ButtonOf(Window window, string name) => (Button)window.FindName(name);

    [Fact]
    public void TheTextIsLaidOut_BoldPoint_Bullets_AndTheDetailBox() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.AskDanger("Delete", "Delete it?\n\nWhy:\n• one\n• two", "Delete") with { Detail = @"C:\x" });
        var body = (StackPanel)window.FindName("Body");
        Assert.Equal([typeof(TextBlock), typeof(TextBlock), typeof(Grid), typeof(Grid), typeof(TextBox)],
            body.Children.Cast<object>().Select(c => c.GetType()));
        Assert.Equal(FontWeights.SemiBold, ((TextBlock)body.Children[0]).FontWeight);
        window.Close();
    });

    [Fact]
    public void ADangerousQuestion_EnterAndEscKeepThingsAsTheyAre() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.AskDanger("Delete", "Delete it?", "Delete"));
        Assert.Equal((false, true, DialogChoice.No), (ButtonOf(window, "YesButton").IsDefault, ButtonOf(window, "NoButton").IsDefault, window.Choice));
        Assert.Equal(Visibility.Collapsed, ButtonOf(window, "CancelButton").Visibility);
        window.Close();
    });

    [Fact]
    public void AnUndoableDelete_EnterStillDeletes_EscStillKeeps() => wpf.Run(() =>
    {
        var window = new MessageWindow(DialogMessage.AskDanger("Delete Task", "Delete it?\n\nUndo brings it back.", "Delete") with { EnterChoosesMain = true });
        Assert.Equal((true, DialogChoice.No), (ButtonOf(window, "YesButton").IsDefault, window.Choice));
        window.Close();
    });

    [Fact]
    public void AMessageThatOnlyTells_HasOneOkButton() => wpf.Run(() =>
    {
        var window = new MessageWindow(new DialogMessage("Done", "It is done."));
        Assert.Equal((true, (object)"OK", DialogChoice.Yes), (ButtonOf(window, "YesButton").IsDefault, ButtonOf(window, "YesButton").Content, window.Choice));
        Assert.Equal(Visibility.Collapsed, ButtonOf(window, "NoButton").Visibility);
        window.Close();
    });

    [Fact]
    public void ThreeButtons_TheWayOutIsCancel() => wpf.Run(() =>
    {
        var window = new MessageWindow(new DialogMessage("Waiting On", "Clear it?") { Yes = "Clear", No = "Leave", Cancel = "Cancel", IsDanger = true });
        Assert.Equal((true, DialogChoice.Cancel), (ButtonOf(window, "CancelButton").IsDefault, window.Choice));
        window.Close();
    });

    // Every message goes through Dialogs, so they all look and behave the same. The one exception is the
    // crash prompt's fallback in App.xaml.cs, for when the app's own window cannot be shown.
    [Fact]
    public void NoWindowsMessageBoxIsLeft_ExceptTheCrashFallback()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var offenders = Directory.EnumerateFiles(dir.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetRelativePath(dir.FullName, f), Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("MessageBox.Show("))
            .ToList();
        Assert.Equal(["App.xaml.cs"], offenders.Select(o => o.File));
    }
}
