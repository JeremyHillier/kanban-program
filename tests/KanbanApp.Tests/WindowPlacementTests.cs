using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Windows' size and position (and whether they were maximized) are remembered in the task
// file between openings, through the general WindowPlacement setting - for each PC that uses the file.
[Collection(WpfCollection.Name)]
public sealed class WindowPlacementTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private MainViewModel OpenBoard(string file, string computer) =>
        new(new DatabaseService(_temp.File(file))) { ThisComputer = computer };

    [Fact]
    public void NothingIsRemembered_UntilTheWindowHasBeenClosedOnce() => wpf.Run(() =>
    {
        Assert.Null(OpenBoard().WindowPlacement("Dashboard"));
    });

    [Fact]
    public void PositionSizeAndMaximized_SurviveReopening() => wpf.Run(() =>
    {
        OpenBoard().SaveWindowPlacement("Dashboard", -1200.5, 40, 1300, 850.25, maximized: true);

        Assert.Equal((-1200.5, 40d, 1300d, 850.25, true), OpenBoard().WindowPlacement("Dashboard"));

        OpenBoard().SaveWindowPlacement("Dashboard", 10, 20, 900, 600, maximized: false);
        Assert.Equal((10d, 20d, 900d, 600d, false), OpenBoard().WindowPlacement("Dashboard"));
    });

    [Fact]
    public void EachWindowHasItsOwnPlace() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveWindowPlacement("Dashboard", 1, 2, 800, 600, false);
        board.SaveWindowPlacement("Other", 3, 4, 700, 500, false);

        Assert.Equal((1d, 2d, 800d, 600d, false), board.WindowPlacement("Dashboard"));
        Assert.Equal((3d, 4d, 700d, 500d, false), board.WindowPlacement("Other"));
    });

    [Fact]
    public void APlaceThatMakesNoSense_IsNotSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveWindowPlacement("Dashboard", 10, 20, 1000, 700, maximized: false);

        board.SaveWindowPlacement("Dashboard", double.NaN, 20, 1000, 700, false);
        board.SaveWindowPlacement("Dashboard", 10, double.PositiveInfinity, 1000, 700, false);
        board.SaveWindowPlacement("Dashboard", 10, 20, 0, 700, false);
        board.SaveWindowPlacement("Dashboard", 10, 20, 1000, -5, false);

        Assert.Equal((10d, 20d, 1000d, 700d, false), OpenBoard().WindowPlacement("Dashboard"));
    });

    [Fact]
    public void AGarbledStoredValue_IsIgnored() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetSetting("DashboardWindowPlacement", "10,20,wide");
        Assert.Null(new MainViewModel(db).WindowPlacement("Dashboard"));
    });

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
