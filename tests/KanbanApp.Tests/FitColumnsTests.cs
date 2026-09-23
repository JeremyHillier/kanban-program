using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Fit to Window: the task columns share the board's width, which follows the window and the
// button column; switched off, the fixed pixel width applies as before.
[Collection(WpfCollection.Name)]
public sealed class FitColumnsTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Theory]
    [InlineData(1761, 5, 340)]   // (1760 / 5) - 12
    [InlineData(1400, 5, 267)]   // (1399 / 5) - 12, rounded down
    [InlineData(2400, 4, 587)]
    [InlineData(900, 5, 240)]    // would be 167 - held at the minimum, so the board scrolls
    [InlineData(1300, 5, 247)]   // just above it
    public void TheBoardIsSharedEvenly_WithRoomForTheGaps_AndAMinimumWidth(double board, int columns, double expected)
    {
        Assert.Equal(expected, MainViewModel.FittedColumnWidth(true, board, columns, 310));
        Assert.True(expected == MainViewModel.MinColumnWidth || columns * (expected + MainViewModel.ColumnGap) < board);
    }

    [Theory]
    [InlineData(false, 1761, 5)]  // switched off
    [InlineData(true, 0, 5)]      // the board hasn't been laid out yet
    [InlineData(true, 1761, 0)]   // no columns
    public void OtherwiseTheFixedWidthApplies(bool fit, double board, int columns)
    {
        Assert.Equal(310, MainViewModel.FittedColumnWidth(fit, board, columns, 310));
    }

    [Fact]
    public void ANewTaskFile_FitsTheColumns_AndTheColumnsFollowTheBoardWidth() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.True(board.IsFitColumnsToWindow);
        var changes = new List<string?>();
        board.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        board.SetBoardWidth(1761);
        Assert.Equal(340, board.EffectiveColumnWidth);

        board.SetBoardWidth(2101); // the button column hidden: more room for every column
        Assert.Equal(408, board.EffectiveColumnWidth);
        Assert.Equal(2, changes.Count(c => c == nameof(MainViewModel.EffectiveColumnWidth)));
    });

    [Fact]
    public void SwitchingItOff_UsesTheFixedWidth_AndIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetColumnWidth(290);
        board.SetBoardWidth(1761);

        board.SetFitColumnsToWindow(false);
        Assert.Equal(290, board.EffectiveColumnWidth);

        var reopened = OpenBoard();
        Assert.False(reopened.IsFitColumnsToWindow);
        reopened.SetBoardWidth(1761);
        Assert.Equal(290, reopened.EffectiveColumnWidth);

        reopened.SetFitColumnsToWindow(true);
        Assert.Equal(340, reopened.EffectiveColumnWidth);
        Assert.True(OpenBoard().IsFitColumnsToWindow);
    });
}
