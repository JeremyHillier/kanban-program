using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Two PCs can share one task file (a synced folder). What belongs to the PC rather than to the
// tasks - here, which version's What's New it has shown - must not be shared between them.
// (The daily update check's side of this is in UpdateCheckTests.)
[Collection(WpfCollection.Name)]
public sealed class TwoComputerTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenOn(string computer)
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db"))) { ThisComputer = computer };
        if (board.Columns.All(c => c.Cards.Count == 0))
            board.AddCard("A task", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        return board;
    }

    [Fact]
    public void WhatsNew_IsShownOncePerComputer_NotOncePerTaskFile() => wpf.Run(() =>
    {
        var office = OpenOn("OFFICE-PC");
        Assert.True(office.ShouldShowWhatsNewOnStartup());
        office.MarkWhatsNewSeen();
        Assert.False(OpenOn("OFFICE-PC").ShouldShowWhatsNewOnStartup());

        var laptop = OpenOn("LAPTOP"); // same task file, the other machine, freshly updated too
        Assert.True(laptop.ShouldShowWhatsNewOnStartup());
        laptop.MarkWhatsNewSeen();

        Assert.False(OpenOn("LAPTOP").ShouldShowWhatsNewOnStartup());
        Assert.False(OpenOn("OFFICE-PC").ShouldShowWhatsNewOnStartup()); // and neither upsets the other
    });

    [Fact]
    public void OnePcOnAnOlderVersion_DoesNotMakeWhatsNewPopUpOnEverySwitch() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var office = OpenOn("OFFICE-PC");
        office.MarkWhatsNewSeen();
        db.SetSetting("LastSeenVersion:LAPTOP", "v0.1.0"); // the laptop last ran, and showed, an older version

        Assert.False(OpenOn("OFFICE-PC").ShouldShowWhatsNewOnStartup()); // the laptop's record doesn't disturb this PC
        Assert.True(OpenOn("LAPTOP").ShouldShowWhatsNewOnStartup());     // and the laptop still gets it after its own update
    });

    [Fact]
    public void ASinglePcUpgrading_IsNotShownItTwice() => wpf.Run(() =>
    {
        var board = OpenOn("ONLY-PC");
        new DatabaseService(_temp.File("board.db")).SetSetting("LastSeenVersion", board.AppVersion); // as the previous version recorded it

        Assert.False(OpenOn("ONLY-PC").ShouldShowWhatsNewOnStartup());
    });
}
