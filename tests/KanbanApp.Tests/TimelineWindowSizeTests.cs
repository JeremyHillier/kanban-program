using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Timeline window's size, and its Day or Week view, are remembered in the task file between openings.
[Collection(WpfCollection.Name)]
public sealed class TimelineWindowSizeTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void NothingIsRemembered_UntilTheWindowHasBeenClosedOnce() => wpf.Run(() =>
    {
        Assert.Null(OpenBoard().WindowSize("Timeline"));
    });

    [Fact]
    public void TheSize_AndWhetherItWasMaximized_SurviveReopening() => wpf.Run(() =>
    {
        OpenBoard().SaveWindowSize("Timeline", 1432.5, 801, maximized: true);

        Assert.Equal((1432.5, 801d, true), OpenBoard().WindowSize("Timeline"));

        OpenBoard().SaveWindowSize("Timeline", 900, 600, maximized: false);
        Assert.Equal((900d, 600d, false), OpenBoard().WindowSize("Timeline"));
    });

    [Fact]
    public void ASizeThatMakesNoSense_IsNotSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveWindowSize("Timeline", 1000, 700, maximized: false);

        board.SaveWindowSize("Timeline", double.NegativeInfinity, double.NegativeInfinity, maximized: false);
        board.SaveWindowSize("Timeline", 0, 500, maximized: false);
        board.SaveWindowSize("Timeline", double.NaN, 500, maximized: false);

        Assert.Equal((1000d, 700d, false), OpenBoard().WindowSize("Timeline"));
    });

    // Settings keeps its own size; the Timeline's stays under the key it always used, so a size saved
    // before the two shared one store still comes back.
    [Fact]
    public void EachWindow_KeepsItsOwnSize() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetSetting("TimelineWindowSize", "1200,750,0");

        var board = new MainViewModel(db);
        board.SaveWindowSize("Settings", 900, 640, maximized: false);

        Assert.Equal((1200d, 750d, false), board.WindowSize("Timeline"));
        Assert.Equal((900d, 640d, false), OpenBoard().WindowSize("Settings"));
        Assert.Equal("900,640,0", db.GetSetting("SettingsWindowSize"));
    });

    [Fact]
    public void TheDayOrWeekView_IsRemembered_AndStartsAsWeek() => wpf.Run(() =>
    {
        Assert.False(OpenBoard().TimelineDayView);

        OpenBoard().SaveTimelineDayView(true);
        Assert.True(OpenBoard().TimelineDayView);

        OpenBoard().SaveTimelineDayView(false);
        Assert.False(OpenBoard().TimelineDayView);
    });
}
