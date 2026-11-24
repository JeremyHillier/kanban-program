using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

// The blocks on the board (Settings, Board tab): the columns two to a row. SplitX is the left blocks' share of the width and
// SplitY the top row's share of the height; BoardBlocks.Cells works out the cells and keeps every block big enough. The cross
// the user drags to change the split is a separate element over the blocks (MainWindow.xaml), placed from the same rules.
public sealed class BlockGridPanel : Panel
{
    public static readonly DependencyProperty SplitXProperty = DependencyProperty.Register(nameof(SplitX), typeof(double), typeof(BlockGridPanel),
        new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty SplitYProperty = DependencyProperty.Register(nameof(SplitY), typeof(double), typeof(BlockGridPanel),
        new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double SplitX { get => (double)GetValue(SplitXProperty); set => SetValue(SplitXProperty, value); }

    public double SplitY { get => (double)GetValue(SplitYProperty); set => SetValue(SplitYProperty, value); }

    private (double[] Columns, double[] Rows) _cells = ([0], [0]);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 480 : availableSize.Height;
        var count = InternalChildren.Count;

        _cells = BoardBlocks.Cells(count, width, height, SplitX, SplitY);
        for (var i = 0; i < count; i++)
        {
            InternalChildren[i].Measure(new Size(_cells.Columns[Math.Min(i % 2, _cells.Columns.Length - 1)], _cells.Rows[i / 2]));
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var column = Math.Min(i % 2, _cells.Columns.Length - 1);
            var row = i / 2;
            var x = column == 0 ? 0 : _cells.Columns[0];
            var y = _cells.Rows.Take(row).Sum();
            InternalChildren[i].Arrange(new Rect(x, y, _cells.Columns[column], _cells.Rows[row]));
        }
        return finalSize;
    }
}
