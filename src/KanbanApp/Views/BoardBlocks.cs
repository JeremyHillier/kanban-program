using System.Windows;

namespace KanbanApp.Views;

// The board as blocks (Settings, Board tab): how many cards a block shows side by side, how wide each one is, and
// where a dropped card lands among them. Pure, so the rules are tested without a window.
public static class BoardBlocks
{
    // The space between cards side by side.
    public const double CardGap = 6;

    // A column's padding (10 a side) and border (1 a side): a card is this much narrower than its column, before
    // the list's scroll bar.
    public const double ColumnChrome = 22;

    // How many cards go side by side in a list this wide: as many as fit at the width a card has in the narrowest
    // fitted column (Settings), so cards in a block are never narrower than they can be on the plain board.
    // One when the board is not in blocks.
    public static int CardColumns(double listWidth, bool isBlocks, int minColumnWidth, double scrollBarWidth = 17)
    {
        if (!isBlocks || listWidth <= 0) return 1;
        var area = listWidth - scrollBarWidth;
        var cardWidth = Math.Max(120, minColumnWidth - ColumnChrome - scrollBarWidth);
        return Math.Max(1, (int)((area + CardGap) / (cardWidth + CardGap)));
    }

    // A card's width in a list showing this many side by side: the whole width for one (as before), otherwise an
    // equal share less the gap. Before the list has a size (0), the card is left to size itself.
    public static double CardWidth(double viewportWidth, int columns) =>
        viewportWidth <= 0 ? double.NaN : columns <= 1 ? viewportWidth : Math.Floor(viewportWidth / columns) - CardGap;

    // Where a dragged card goes among the others. In one column of cards it goes before the first card whose middle
    // is below the pointer; with cards side by side it is reading order: before a card in a later row, or before
    // one in the pointer's row whose middle is to its right.
    public static bool DropsBefore(Point pointer, Rect card, bool sideBySide) => sideBySide
        ? pointer.Y < card.Top || (pointer.Y < card.Bottom && pointer.X < card.Left + card.Width / 2)
        : pointer.Y < card.Top + card.Height / 2;

    // The smallest a block can be made: its narrowest column and the gap beside it, and a few cards' height.
    public const double MinBlockWidth = 172;
    public const double MinBlockHeight = 120;

    // A split (the share of the total the first part has) kept so the first part is at least minFirst and the rest at
    // least minRest. Too little room for both: shared evenly.
    public static double ClampSplit(double split, double total, double minFirst, double minRest)
    {
        if (total <= 0) return 0.5;
        var low = minFirst / total;
        var high = 1 - minRest / total;
        return low >= high ? 0.5 : Math.Clamp(split, low, high);
    }

    // A split moved by delta pixels of the total, kept within the limits.
    public static double MoveSplit(double split, double delta, double total, double minFirst, double minRest) =>
        total <= 0 ? split : ClampSplit(ClampSplit(split, total, minFirst, minRest) + delta / total, total, minFirst, minRest);

    // The cells of count blocks laid out two to a row in width x height: the two columns' widths and each row's height.
    // splitX is the left column's share of the width; splitY is the top row's share of the height, the rest shared by the
    // other rows. Whole pixels, so the edges stay sharp.
    public static (double[] Columns, double[] Rows) Cells(int count, double width, double height, double splitX, double splitY)
    {
        var rows = Math.Max(1, (count + 1) / 2);

        double[] columns = [width];
        if (count > 1)
        {
            var left = Math.Round(width * ClampSplit(splitX, width, MinBlockWidth, MinBlockWidth));
            columns = [left, width - left];
        }

        var heights = new double[rows];
        if (rows == 1) heights[0] = height;
        else
        {
            var top = Math.Round(height * ClampSplit(splitY, height, MinBlockHeight, MinBlockHeight * (rows - 1)));
            heights[0] = top;
            for (var r = 1; r < rows; r++) heights[r] = (height - top) / (rows - 1);
        }
        return (columns, heights);
    }

    // The cross's size, and the gap each block keeps to its right and below it (the column's own margin and the cell's bottom
    // margin): the cross sits in the middle of the gap where the first four blocks meet.
    public const double CrossSize = 28;
    public const double Gap = 12;

    public static Point CrossLocation(int count, double width, double height, double splitX, double splitY)
    {
        var (columns, rows) = Cells(count, width, height, splitX, splitY);
        return new Point(columns[0] - Gap / 2 - CrossSize / 2, rows[0] - Gap / 2 - CrossSize / 2);
    }
}
