using System.Windows;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Dragging the cross between the blocks with Shift held: the drag keeps to the direction it has moved furthest in.
public sealed class BlockCrossShiftTests
{
    private static void Near((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact]
    public void WithoutShift_TheOffsetIsKeptAsItIs() =>
        Assert.Equal(new Vector(100, -30), BoardBlocks.LockToAxis(new Vector(100, -30), shift: false));

    [Fact]
    public void WithShift_OnlyTheLargerDirectionIsKept_AcrossWhenTheyAreEqual()
    {
        Assert.Equal(new Vector(100, 0), BoardBlocks.LockToAxis(new Vector(100, -30), shift: true));
        Assert.Equal(new Vector(0, -80), BoardBlocks.LockToAxis(new Vector(20, -80), shift: true));
        Assert.Equal(new Vector(-50, 0), BoardBlocks.LockToAxis(new Vector(-50, 50), shift: true));
    }

    [Fact]
    public void ADrag_MovesTheSplitsFromWhereTheyWere_ByThePointersOffset()
    {
        var atStart = (0.5, 0.5);

        Near((0.6, 0.4625), BoardBlocks.DragSplits(atStart, new Vector(100, -30), false, 1000, 800, 4));
        Near((0.6, 0.5), BoardBlocks.DragSplits(atStart, new Vector(100, -30), true, 1000, 800, 4));      // locked across
        Near((0.5, 0.4), BoardBlocks.DragSplits(atStart, new Vector(20, -80), true, 1000, 800, 4));        // locked up and down
    }

    [Fact]
    public void LettingGoOfShift_MidDrag_PutsTheOtherDirectionBack()
    {
        var atStart = (0.5, 0.5);
        var offset = new Vector(100, -30);

        var locked = BoardBlocks.DragSplits(atStart, offset, true, 1000, 800, 4);
        var free = BoardBlocks.DragSplits(atStart, offset, false, 1000, 800, 4);

        Assert.Equal(0.5, locked.Y);
        Assert.Equal(0.4625, free.Y, 9);      // the same pointer place, worked out from the start again
        Assert.Equal(locked.X, free.X);
    }

    [Fact]
    public void ADrag_StillKeepsEveryBlockBigEnough()
    {
        var (x, y) = BoardBlocks.DragSplits((0.5, 0.5), new Vector(-900, 900), false, 1000, 800, 4);

        Assert.Equal(BoardBlocks.MinBlockWidth / 1000, x, 6);
        Assert.Equal(1 - BoardBlocks.MinBlockHeight / 800, y, 6);
    }
}
