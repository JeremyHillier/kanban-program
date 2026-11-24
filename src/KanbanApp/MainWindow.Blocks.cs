using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The cross over the middle of the blocks: dragging it moves where the blocks meet, and where it was left is kept in the task file.
public partial class MainWindow
{
    private void BlocksCross_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement { Parent: Grid area } || area.ActualWidth <= 0 || area.ActualHeight <= 0) return;

        var rows = Math.Max(1, (viewModel.BlockColumns.Count + 1) / 2);
        viewModel.BlocksSplitX = BoardBlocks.MoveSplit(viewModel.BlocksSplitX, e.HorizontalChange, area.ActualWidth, BoardBlocks.MinBlockWidth, BoardBlocks.MinBlockWidth);
        if (rows > 1)
        {
            viewModel.BlocksSplitY = BoardBlocks.MoveSplit(viewModel.BlocksSplitY, e.VerticalChange, area.ActualHeight, BoardBlocks.MinBlockHeight, BoardBlocks.MinBlockHeight * (rows - 1));
        }
    }

    private void BlocksCross_DragCompleted(object sender, DragCompletedEventArgs e) => (DataContext as MainViewModel)?.SaveBlocksSplit();

    private void BlocksCross_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        (DataContext as MainViewModel)?.ResetBlocksSplit();
        e.Handled = true;
    }
}
