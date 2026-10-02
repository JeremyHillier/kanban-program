using KanbanApp.Services;

namespace KanbanApp.Tests;

// "Start when Windows starts": a value under the user's Run key pointing at this exe. The tests
// use the in-memory store (set up by WpfDispatcherFixture for the whole run), never the real key.
[Collection(WpfCollection.Name)]
public sealed class WindowsStartupTests(WpfDispatcherFixture wpf)
{
    [Fact]
    public void TurningItOn_WritesThisExesPath_Quoted_AndOffRemovesIt() => wpf.Run(() =>
    {
        WindowsStartup.SetEnabled(false);
        Assert.False(WindowsStartup.IsEnabled);
        Assert.Null(WindowsStartup.Read());

        WindowsStartup.SetEnabled(true);

        var stored = WindowsStartup.Read();
        Assert.NotNull(stored);
        Assert.StartsWith("\"", stored);
        Assert.EndsWith("\"", stored);
        Assert.Equal(Environment.ProcessPath, stored.Trim('"'));
        Assert.True(WindowsStartup.IsEnabled);

        WindowsStartup.SetEnabled(false);
        Assert.Null(WindowsStartup.Read());
        Assert.False(WindowsStartup.IsEnabled);
    });

    [Fact]
    public void AValueLeftByACopyInstalledElsewhere_ReadsAsOff() => wpf.Run(() =>
    {
        WindowsStartup.Write(@"""C:\Somewhere Else\KanbanApp.exe""");
        Assert.False(WindowsStartup.IsEnabled);

        WindowsStartup.SetEnabled(true);  // ticking the box puts it right
        Assert.True(WindowsStartup.IsEnabled);
        WindowsStartup.SetEnabled(false);
    });

    [Fact]
    public void TheValueName_IsPerChannel()
    {
        Assert.Equal(AppChannel.IsTest ? "KanbanTaskBoard-Test" : "KanbanTaskBoard", WindowsStartup.ValueName);
    }
}
