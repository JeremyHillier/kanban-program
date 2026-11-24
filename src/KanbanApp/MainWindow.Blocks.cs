using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The cross over the middle of the blocks: dragging it moves where the blocks meet, holding Shift keeps the drag to one
// direction, and where it was left is kept in the task file.
public partial class MainWindow
{
    // The blocks area the drag is in, where in it the drag began, and the splits then: the splits follow the pointer from there.
    private Grid? _crossArea;
    private Point _crossDragStart;
    private (double X, double Y) _crossSplitsAtStart;

    private void BlocksCross_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement { Parent: Grid area }) return;

        _crossArea = area;
        _crossDragStart = Mouse.GetPosition(area);
        _crossSplitsAtStart = (viewModel.BlocksSplitX, viewModel.BlocksSplitY);
        PreviewKeyDown += CrossDrag_KeyChanged;
        PreviewKeyUp += CrossDrag_KeyChanged;
    }

    private void BlocksCross_DragDelta(object sender, DragDeltaEventArgs e) => FollowCrossDrag();

    // Shift pressed or let go without the mouse moving still takes effect at once.
    private void CrossDrag_KeyChanged(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift) FollowCrossDrag();
    }

    private void FollowCrossDrag()
    {
        if (_crossArea is not { ActualWidth: > 0, ActualHeight: > 0 } area || DataContext is not MainViewModel viewModel) return;

        var (x, y) = BoardBlocks.DragSplits(_crossSplitsAtStart, Mouse.GetPosition(area) - _crossDragStart,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), area.ActualWidth, area.ActualHeight, viewModel.BlockColumns.Count);
        viewModel.BlocksSplitX = x;
        viewModel.BlocksSplitY = y;
    }

    private void BlocksCross_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        PreviewKeyDown -= CrossDrag_KeyChanged;
        PreviewKeyUp -= CrossDrag_KeyChanged;
        _crossArea = null;
        (DataContext as MainViewModel)?.SaveBlocksSplit();
    }

    private void BlocksCross_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        (DataContext as MainViewModel)?.ResetBlocksSplit();
        e.Handled = true;
    }
}
