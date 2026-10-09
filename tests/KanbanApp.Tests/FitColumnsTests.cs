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
        Assert.True(expected == MainViewModel.DefaultMinFittedColumnWidth || columns * (expected + MainViewModel.ColumnGap) < board);
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

    [Fact]
    public void OnASmallScreen_TheNarrowestFittedWidthCanBeLowered_AndIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetBoardWidth(900);                       // five columns of 168 would fit
        Assert.Equal(240, board.EffectiveColumnWidth);  // held at the standard narrowest, so it scrolls
        Assert.False(board.IsNarrowColumns);

        board.SetMinFittedColumnWidth(160);
        Assert.Equal(167, board.EffectiveColumnWidth);  // (899 / 5) - 12: everything on screen
        Assert.True(board.IsNarrowColumns);
        Assert.False(board.ShowCardExtras);

        var reopened = OpenBoard();
        Assert.Equal(160, reopened.MinFittedColumnWidth);
        reopened.SetBoardWidth(1761);
        Assert.False(reopened.IsNarrowColumns);         // plenty of room: the full card again
        Assert.True(reopened.ShowCardExtras);
    });

    [Theory]
    [InlineData(100, 160)]   // below the smallest a card can take
    [InlineData(195, 195)]
    [InlineData(5000, 800)]
    public void ColumnWidths_StayWithinWhatACardCanTake(int typed, int kept) => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetMinFittedColumnWidth(typed);
        board.SetColumnWidth(typed);
        Assert.Equal(kept, board.MinFittedColumnWidth);
        Assert.Equal(kept, board.ColumnWidth);
    });

    [Fact]
    public void TheCardButtons_CanBeSwitchedOff_AndStaySo() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetBoardWidth(1761);
        Assert.True(board.ShowCardButtons);
        Assert.True(board.ShowCardSideButtons);
        Assert.Equal("Hide Buttons", board.CardButtonsButtonLabel);

        board.ToggleCardButtons();
        Assert.False(board.ShowCardButtons);
        Assert.False(board.ShowCardSideButtons);
        Assert.Equal("Show Buttons", board.CardButtonsButtonLabel);
        Assert.False(OpenBoard().ShowCardButtons);

        board.ToggleCardButtons();
        board.SetMinFittedColumnWidth(160);
        board.SetBoardWidth(900);
        Assert.True(board.ShowCardButtons);             // the row stays, wrapped onto two lines
        Assert.False(board.ShowCardSideButtons);        // but the side buttons go in a narrow column
    });
}
