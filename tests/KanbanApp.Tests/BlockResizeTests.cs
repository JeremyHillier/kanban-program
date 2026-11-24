using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KanbanApp.Converters;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The cross between the blocks: the sizes it gives them, where it sits, how far a drag moves it, and that where it was left is kept.
public sealed class BlockSizeRulesTests
{
    [Fact]
    public void TheCells_ShareTheWidthAndHeightByTheSplit_InWholePixels()
    {
        var (columns, rows) = BoardBlocks.Cells(4, 1000, 800, 0.5, 0.5);
        Assert.Equal([500, 500], columns);
        Assert.Equal([400, 400], rows);

        (columns, rows) = BoardBlocks.Cells(4, 1001, 800, 0.7, 0.3);
        Assert.Equal([701, 300], columns);
        Assert.Equal([240, 560], rows);
    }

    [Fact]
    public void AnOddBlock_MakesAThirdRow_WhichTheRowsBelowTheFirstShare()
    {
        var (columns, rows) = BoardBlocks.Cells(5, 1000, 800, 0.5, 0.5);

        Assert.Equal(2, columns.Length);
        Assert.Equal([400, 200, 200], rows);
    }

    [Fact]
    public void NoBlock_CanBeMadeSmallerThanItsMinimum()
    {
        var (columns, rows) = BoardBlocks.Cells(4, 1000, 800, 0.01, 0.99);
        Assert.Equal(BoardBlocks.MinBlockWidth, columns[0]);
        Assert.Equal(800 - BoardBlocks.MinBlockHeight, rows[0]);

        (columns, rows) = BoardBlocks.Cells(5, 1000, 800, 0.99, 0.99);
        Assert.Equal(1000 - BoardBlocks.MinBlockWidth, columns[0]);
        Assert.Equal(800 - 2 * BoardBlocks.MinBlockHeight, rows[0]);   // two rows below need their minimum

        (columns, _) = BoardBlocks.Cells(4, 300, 800, 0.9, 0.5);        // too little room for two minimums: even
        Assert.Equal([150, 150], columns);
    }

    [Fact]
    public void MovingTheCross_AddsThePixelsToTheShare_WithinTheLimits()
    {
        Assert.Equal(0.6, BoardBlocks.MoveSplit(0.5, 100, 1000, 172, 172), 6);
        Assert.Equal(0.4, BoardBlocks.MoveSplit(0.5, -100, 1000, 172, 172), 6);
        Assert.Equal(1 - 172.0 / 1000, BoardBlocks.MoveSplit(0.5, 900, 1000, 172, 172), 6);
        Assert.Equal(0.5, BoardBlocks.MoveSplit(0.5, 50, 0, 172, 172));
    }

    [Fact]
    public void TheCross_SitsInTheMiddleOfTheGapWhereFourBlocksMeet()
    {
        Assert.Equal(new Point(480, 380), BoardBlocks.CrossLocation(4, 1000, 800, 0.5, 0.5));    // 500 - 12 / 2 - 28 / 2
        Assert.Equal(new Point(680, 220), BoardBlocks.CrossLocation(4, 1000, 800, 0.7, 0.3));
        Assert.Equal(new Point(480, 380), BoardBlocks.CrossLocation(5, 1000, 800, 0.5, 0.5));    // a third row changes nothing above it
    }

    [Fact]
    public void TheConverter_GivesTheCrossItsMargin_AndNothingBeforeTheAreaHasASize()
    {
        var converter = new BlockCrossPlacementConverter();

        Assert.Equal(new Thickness(680, 220, 0, 0), converter.Convert([1000.0, 800.0, 0.7, 0.3, 4], typeof(Thickness), null!, CultureInfo.InvariantCulture));
        Assert.Equal(new Thickness(0), converter.Convert([DependencyProperty.UnsetValue, 800.0, 0.7, 0.3, 4], typeof(Thickness), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null, 0.5)]
    [InlineData("", 0.5)]
    [InlineData("abc", 0.5)]
    [InlineData("0.7", 0.7)]
    [InlineData("0.04", 0.5)]
    [InlineData("1.5", 0.5)]
    public void ASavedSplit_IsReadBack_OrTheEvenSplitWhenItIsNotOne(string? text, double expected) =>
        Assert.Equal(expected, MainViewModel.ParseSplit(text));
}

[Collection(WpfCollection.Name)]
public sealed class BlockGridPanelTests(WpfDispatcherFixture wpf)
{
    private static BlockGridPanel Make(int count, double splitX = 0.5, double splitY = 0.5)
    {
        var panel = new BlockGridPanel { SplitX = splitX, SplitY = splitY };
        for (var i = 0; i < count; i++) panel.Children.Add(new Border());
        return panel;
    }

    private static void Lay(BlockGridPanel panel, double width = 1000, double height = 800)
    {
        panel.Measure(new Size(width, height));
        panel.Arrange(new Rect(0, 0, width, height));
    }

    private static Rect Slot(UIElement element) => LayoutInformation.GetLayoutSlot((FrameworkElement)element);

    [Fact]
    public void FourBlocks_FillTheSpaceTwoToARow() => wpf.Run(() =>
    {
        var panel = Make(4);
        Lay(panel);

        Assert.Equal(new Rect(0, 0, 500, 400), Slot(panel.Children[0]));
        Assert.Equal(new Rect(500, 0, 500, 400), Slot(panel.Children[1]));
        Assert.Equal(new Rect(0, 400, 500, 400), Slot(panel.Children[2]));
        Assert.Equal(new Rect(500, 400, 500, 400), Slot(panel.Children[3]));
    });

    [Fact]
    public void ASplit_MovesTheBlocks_AndIsHeldAtTheSmallestBlock() => wpf.Run(() =>
    {
        var panel = Make(4, 0.7, 0.3);
        Lay(panel);

        Assert.Equal(new Rect(0, 0, 700, 240), Slot(panel.Children[0]));
        Assert.Equal(new Rect(700, 240, 300, 560), Slot(panel.Children[3]));

        panel.SplitX = 0.01;
        Lay(panel);
        Assert.Equal(BoardBlocks.MinBlockWidth, Slot(panel.Children[0]).Width);
    });

    [Fact]
    public void TheRowsBelowTheFirst_ShareTheRest() => wpf.Run(() =>
    {
        var panel = Make(5);
        Lay(panel);

        Assert.Equal(new Rect(0, 400, 500, 200), Slot(panel.Children[2]));
        Assert.Equal(new Rect(0, 600, 500, 200), Slot(panel.Children[4]));
    });
}

[Collection(WpfCollection.Name)]
public sealed class BlockSplitSettingTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void WhereTheCrossWasLeft_IsKeptInTheFile_OnceADragEnds_AndADoubleClickEvensIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal((0.5, 0.5), (board.BlocksSplitX, board.BlocksSplitY));

        board.BlocksSplitX = 0.65;
        board.BlocksSplitY = 0.35;
        var notYet = OpenBoard();
        Assert.Equal((0.5, 0.5), (notYet.BlocksSplitX, notYet.BlocksSplitY));        // not until the drag ends

        board.SaveBlocksSplit();
        var reopened = OpenBoard();
        Assert.Equal((0.65, 0.35), (reopened.BlocksSplitX, reopened.BlocksSplitY));

        board.BlocksSplitX = 7;                                                      // never outside 5% to 95%
        Assert.Equal(0.95, board.BlocksSplitX);

        board.ResetBlocksSplit();
        var evened = OpenBoard();
        Assert.Equal((0.5, 0.5), (board.BlocksSplitX, board.BlocksSplitY));
        Assert.Equal((0.5, 0.5), (evened.BlocksSplitX, evened.BlocksSplitY));
    });
}
