using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using KanbanApp.Theming;

namespace KanbanApp.Tests;

// A window's buttons must never leave the screen: the content scrolls, the buttons stay put, and
// no window is ever taller or wider than the screen it is on.
public class WindowLayoutTests
{
    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "KanbanApp");
    }

    // The buttons that close or act on a window - the ones that must stay reachable.
    private static bool IsActionButton(XElement button)
    {
        string? A(string name) => button.Attribute(name)?.Value;
        if (A("IsCancel") == "True" || A("IsDefault") == "True") return true;
        return A("Content") is "Close" or "Cancel" or "Save" or "OK" or "Save &amp; Close" or "Save & Close";
    }

    [Fact]
    public void NoWindowPutsItsActionButtonsInsideAScrollRegion()
    {
        var root = SourceRoot();
        var windows = Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.xaml").Append(Path.Combine(root, "MainWindow.xaml"));
        var offenders = new List<string>();

        foreach (var file in windows)
        {
            var doc = XDocument.Load(file);
            if (doc.Root?.Name.LocalName != "Window") continue;

            var inScroll = doc.Descendants().Where(e => e.Name.LocalName == "ScrollViewer")
                .SelectMany(sv => sv.Descendants().Where(e => e.Name.LocalName == "Button" && IsActionButton(e)))
                .Select(b => $"{Path.GetFileName(file)}: {b.Attribute("Content")?.Value ?? "(button)"}");
            offenders.AddRange(inScroll);
        }

        Assert.True(offenders.Count == 0,
            "These buttons sit inside a ScrollViewer, so they can scroll off the screen. Put the content in the ScrollViewer and the buttons in a row beneath it:\n" +
            string.Join("\n", offenders));
    }
}

[Collection(WpfCollection.Name)]
public sealed class WindowFitTests(WpfDispatcherFixture wpf)
{
    private static Window TallWindow(double contentHeight) => new()
    {
        SizeToContent = SizeToContent.WidthAndHeight,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = 10, Top = 10, ShowActivated = false,
        Content = new Border { Width = 300, Height = contentHeight }
    };

    [Fact]
    public void AWindowTallerThanTheScreen_IsCappedAtTheWorkArea() => wpf.Run(() =>
    {
        WindowFit.Register();
        var window = TallWindow(SystemParameters.WorkArea.Height * 3);
        try
        {
            window.Show();

            var area = WindowFit.WorkAreaOf(window);
            Assert.True(window.ActualHeight <= area.Height + 0.5, $"{window.ActualHeight} tall on a {area.Height} work area");
            Assert.True(window.Top + window.ActualHeight <= area.Bottom + 0.5, "the bottom edge is off the screen");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void AWindowPlacedPastTheBottomEdge_IsMovedBackIn() => wpf.Run(() =>
    {
        WindowFit.Register();
        var window = TallWindow(200);
        window.Top = SystemParameters.WorkArea.Bottom - 50; // most of it would hang below the taskbar
        try
        {
            window.Show();

            var area = WindowFit.WorkAreaOf(window);
            Assert.True(window.Top + window.ActualHeight <= area.Bottom + 0.5, $"top {window.Top}, height {window.ActualHeight}, work area bottom {area.Bottom}");
            Assert.True(window.Top >= area.Top - 0.5);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void AWindowThatFits_IsLeftWhereItIs() => wpf.Run(() =>
    {
        WindowFit.Register();
        var window = TallWindow(200);
        window.Left = 40; window.Top = 60;
        try
        {
            window.Show();
            Assert.Equal(40, window.Left);
            Assert.Equal(60, window.Top);
        }
        finally
        {
            window.Close();
        }
    });
}
