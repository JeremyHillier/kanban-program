using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Timeline window's size is remembered in the task file between openings.
[Collection(WpfCollection.Name)]
public sealed class TimelineWindowSizeTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void NothingIsRemembered_UntilTheWindowHasBeenClosedOnce() => wpf.Run(() =>
    {
        Assert.Null(OpenBoard().TimelineWindowSize);
    });

    [Fact]
    public void TheSize_AndWhetherItWasMaximized_SurviveReopening() => wpf.Run(() =>
    {
        OpenBoard().SaveTimelineWindowSize(1432.5, 801, maximized: true);

        Assert.Equal((1432.5, 801d, true), OpenBoard().TimelineWindowSize);

        OpenBoard().SaveTimelineWindowSize(900, 600, maximized: false);
        Assert.Equal((900d, 600d, false), OpenBoard().TimelineWindowSize);
    });

    [Fact]
    public void ASizeThatMakesNoSense_IsNotSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveTimelineWindowSize(1000, 700, maximized: false);

        board.SaveTimelineWindowSize(double.NegativeInfinity, double.NegativeInfinity, maximized: false);
        board.SaveTimelineWindowSize(0, 500, maximized: false);
        board.SaveTimelineWindowSize(double.NaN, 500, maximized: false);

        Assert.Equal((1000d, 700d, false), OpenBoard().TimelineWindowSize);
    });
}
