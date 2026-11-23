using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The board as blocks: how many cards a block shows side by side, their width, and where a dropped card lands.
public sealed class BoardBlocksTests
{
    [Theory]
    [InlineData(445, true, 240, 2)]    // a block in a 1500-wide window
    [InlineData(645, true, 240, 3)]    // on a wide monitor
    [InlineData(300, true, 240, 1)]
    [InlineData(645, false, 240, 1)]   // not in blocks
    [InlineData(0, true, 240, 1)]      // not laid out yet
    [InlineData(445, true, 160, 3)]    // narrower columns allowed in Settings: more cards side by side
    public void CardColumns_FitAsManyAsTheNarrowestColumnAllows(double width, bool blocks, int minColumnWidth, int expected) =>
        Assert.Equal(expected, BoardBlocks.CardColumns(width, blocks, minColumnWidth));

    [Fact]
    public void CardWidth_IsTheWholeList_OrAnEqualShareLessTheGap()
    {
        Assert.Equal(400, BoardBlocks.CardWidth(400, 1));
        Assert.Equal(194, BoardBlocks.CardWidth(400, 2));
        Assert.Equal(127, BoardBlocks.CardWidth(401, 3));
        Assert.True(double.IsNaN(BoardBlocks.CardWidth(0, 2)));
    }

    [Fact]
    public void ADrop_GoesBeforeTheCardBelowTheMiddle_OrInReadingOrderWhenCardsAreSideBySide()
    {
        var card = new Rect(100, 100, 100, 100);

        Assert.True(BoardBlocks.DropsBefore(new Point(150, 140), card, sideBySide: false));
        Assert.False(BoardBlocks.DropsBefore(new Point(150, 160), card, sideBySide: false));

        Assert.True(BoardBlocks.DropsBefore(new Point(120, 150), card, sideBySide: true));    // left half, same row
        Assert.False(BoardBlocks.DropsBefore(new Point(180, 150), card, sideBySide: true));   // right half
        Assert.True(BoardBlocks.DropsBefore(new Point(400, 50), card, sideBySide: true));     // a row above
        Assert.False(BoardBlocks.DropsBefore(new Point(10, 250), card, sideBySide: true));    // a row below
    }
}

[Collection(WpfCollection.Name)]
public sealed class BoardBlocksSettingTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void Blocks_AreOffAtFirst_KeptInTheFile_AndGiveWayToSwimlanes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.False(board.ShowColumnsAsBlocks);
        Assert.True(board.IsPlainBoardOn);
        Assert.Equal(board.EffectiveColumnWidth, board.ColumnTemplateWidth);

        board.SetShowColumnsAsBlocks(true);
        Assert.True(board.IsBlocksOn);
        Assert.False(board.IsPlainBoardOn);
        Assert.True(double.IsNaN(board.ColumnTemplateWidth));   // a column fills its block
        Assert.True(OpenBoard().ShowColumnsAsBlocks);

        board.SetSwimlaneMode(SwimlaneMode.Project);
        Assert.False(board.IsBlocksOn);
        Assert.False(board.IsPlainBoardOn);
        Assert.Equal(board.EffectiveColumnWidth, board.ColumnTemplateWidth);
    });

    [Fact]
    public void TheBlocks_AreEveryColumnButDone_InBoardOrder_AndShareWhatDoneLeaves() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetShowColumnsAsBlocks(true);

        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting"], board.BlockColumns.Cast<ColumnViewModel>().Select(c => c.Name));
        Assert.Null(board.AddColumn("Later"));
        Assert.Equal("Later", board.BlockColumns.Cast<ColumnViewModel>().Last().DisplayName);
        Assert.Equal(5, board.BlockColumns.Count);

        board.SetBoardWidth(1501);
        Assert.Equal(288, board.BlocksDoneWidth);        // a column on a board of five
        Assert.Equal(300, board.BlocksDoneSlotWidth);
        Assert.Equal(1200, board.BlocksWidth);          // the rest

        board.SetBoardWidth(1000);
        Assert.Equal(240, board.BlocksDoneWidth);        // never under the narrowest fitted column
        Assert.Equal(747, board.BlocksWidth);
    });
}
