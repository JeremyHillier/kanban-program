using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The sidebar's Project list and the Priority/Who pair can be resized by dragging; the heights are
// remembered in the task file.
[Collection(WpfCollection.Name)]
public sealed class FilterListHeightTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void NewBoardStartsAtTheDefaultHeight() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal(MainViewModel.DefaultFilterListHeight, board.ProjectFilterListHeight);
        Assert.Equal(MainViewModel.DefaultFilterListHeight, board.PriorityWhoFilterListHeight);
    });

    [Fact]
    public void SavedHeightsComeBackIndependently() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.ProjectFilterListHeight = 222.5;
        board.PriorityWhoFilterListHeight = 88;
        board.SaveFilterListHeights();

        var reopened = OpenBoard();
        Assert.Equal(222.5, reopened.ProjectFilterListHeight);
        Assert.Equal(88, reopened.PriorityWhoFilterListHeight);
    });

    [Fact]
    public void SmallDragStepsAccumulate() => wpf.Run(() =>
    {
        var board = OpenBoard();
        for (var i = 0; i < 10; i++) board.ProjectFilterListHeight += 0.4;
        Assert.Equal(MainViewModel.DefaultFilterListHeight + 4, board.ProjectFilterListHeight, 6);
    });

    [Theory]
    [InlineData(-500, MainViewModel.MinFilterListHeight)]
    [InlineData(10_000, MainViewModel.MaxFilterListHeight)]
    [InlineData(double.NaN, MainViewModel.DefaultFilterListHeight)]
    [InlineData(double.PositiveInfinity, MainViewModel.DefaultFilterListHeight)]
    public void HeightsStayInRange(double requested, double expected)
    {
        Assert.Equal(expected, MainViewModel.ClampFilterListHeight(requested));
    }

    [Fact]
    public void UnreadableStoredValueFallsBackToDefault() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        db.SetSetting("ProjectFilterListHeight", "tall");
        db.SetSetting("PriorityWhoFilterListHeight", "99999");

        var board = new MainViewModel(db);
        Assert.Equal(MainViewModel.DefaultFilterListHeight, board.ProjectFilterListHeight);
        Assert.Equal(MainViewModel.MaxFilterListHeight, board.PriorityWhoFilterListHeight);
    });
}
