using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Each task file keeps where its windows were - and whether full screen - for each PC that uses it.
[Collection(WpfCollection.Name)]
public sealed class WindowPlacementTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string file, string computer) =>
        new(new DatabaseService(_temp.File(file))) { ThisComputer = computer };

    [Fact]
    public void EachPc_KeepsItsOwnPlacement_InEachTaskFile() => wpf.Run(() =>
    {
        OpenBoard("work.db", "OFFICE").SaveWindowPlacement("Board", 10, 20, 1600, 900, maximized: true);
        OpenBoard("work.db", "LAPTOP").SaveWindowPlacement("Board", 0, 0, 1200, 700, maximized: false);
        OpenBoard("home.db", "OFFICE").SaveWindowPlacement("Board", 300, 200, 1000, 600, maximized: false);

        Assert.Equal((10, 20, 1600, 900, true), OpenBoard("work.db", "OFFICE").WindowPlacement("Board"));
        Assert.Equal((0, 0, 1200, 700, false), OpenBoard("work.db", "LAPTOP").WindowPlacement("Board"));
        Assert.Equal((300, 200, 1000, 600, false), OpenBoard("home.db", "OFFICE").WindowPlacement("Board"));
    });

    [Fact]
    public void APcWithNoneYet_StartsFromTheSharedOne() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("work.db"));
        db.SetSetting("DashboardWindowPlacement", "5,6,800,600,1"); // as versions before 0.168.0 saved it
        db.SetSetting("TimelineWindowSize", "900,700,0");

        var board = new MainViewModel(db) { ThisComputer = "NEW-PC" };
        Assert.Equal((5, 6, 800, 600, true), board.WindowPlacement("Dashboard"));
        Assert.Equal((900, 700, false), board.WindowSize("Timeline"));

        board.SaveWindowPlacement("Dashboard", 1, 2, 3, 4, maximized: false);
        Assert.Equal("5,6,800,600,1", db.GetSetting("DashboardWindowPlacement")); // the shared one is left for other PCs
        Assert.Equal((1, 2, 3, 4, false), board.WindowPlacement("Dashboard"));
    });
}
