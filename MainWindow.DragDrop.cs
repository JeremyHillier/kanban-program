using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// Dragging cards: picking one (or a selected group) up, moving it to another column, and
// reordering within a column with the insertion line.
public partial class MainWindow
{
    // Set when a plain click lands on a card that's part of a multi-selection: the selection is
    // only cleared if the button comes back up without a drag, since the press may be the start of
    // dragging the whole group.
    private bool _clearSelectionOnMouseUp;

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (sender is FrameworkElement { DataContext: CardViewModel card } && DataContext is MainViewModel viewModel)
            {
                EditCard(card, viewModel);
            }
            e.Handled = true;
            return;
        }

        _dragStartPoint = e.GetPosition(null);
        _clearSelectionOnMouseUp = false;

        // Clicks on a card's own buttons (quick-move, flag, email, delete) act on that card only and
        // leave the selection alone.
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (sender is not FrameworkElement { DataContext: CardViewModel clicked } || DataContext is not MainViewModel board) return;

        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                board.ToggleCardSelection(clicked);
                e.Handled = true;
                break;
            case ModifierKeys.Shift:
                board.SelectCardRange(clicked);
                e.Handled = true;
                break;
            case ModifierKeys.None when clicked.IsSelected:
                _clearSelectionOnMouseUp = true;
                break;
            case ModifierKeys.None when board.SelectedCardCount > 0:
                board.ClearCardSelection();
                break;
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_clearSelectionOnMouseUp) return;
        _clearSelectionOnMouseUp = false;
        if (DataContext is MainViewModel board) board.ClearCardSelection();
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var current = element; current is not null; current = current is System.Windows.Media.Visual
                 ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                 : LogicalTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase) return true;
            if (current is System.Windows.Controls.ContentPresenter { Content: CardViewModel }) return false; // reached the card
        }
        return false;
    }

    // What a card drag carries: the grabbed card (the single-card format every handler already
    // understands), plus, when it's part of a multi-selection, the whole group in board order.
    private const string CardGroupFormat = "KanbanApp.CardGroup";

    internal static DataObject CreateCardDragData(CardViewModel grabbed, IReadOnlyList<CardViewModel> selected)
    {
        var data = new DataObject(typeof(CardViewModel), grabbed);
        if (selected.Count > 1 && selected.Contains(grabbed)) data.SetData(CardGroupFormat, selected.ToList());
        return data;
    }

    internal static List<CardViewModel> GetDraggedCards(IDataObject data)
    {
        if (data.GetDataPresent(CardGroupFormat) && data.GetData(CardGroupFormat) is List<CardViewModel> group) return group;
        return data.GetData(typeof(CardViewModel)) is CardViewModel card ? [card] : [];
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(null);
        var diff = _dragStartPoint - currentPosition;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: CardViewModel card } element && DataContext is MainViewModel viewModel)
        {
            // A drag, not a click, so the mouse-up mustn't clear the selection being dragged.
            _clearSelectionOnMouseUp = false;

            // Dragging a card that isn't selected drags just that card, and drops the selection so
            // what's highlighted always matches what the next group drag would carry.
            if (!card.IsSelected && viewModel.SelectedCardCount > 0) viewModel.ClearCardSelection();

            DragDrop.DoDragDrop(element, CreateCardDragData(card, viewModel.SelectedCards), DragDropEffects.Move);

            // DoDragDrop blocks until the drag ends, however it ends (drop, Esc-cancel, focus loss).
            // Clearing every column's insertion-line indicator here, unconditionally, guarantees none
            // are left stuck visible even when a DragLeave/Drop never fired for whichever column last
            // showed one - e.g. a drop landing on Column_Drop's cross-column move instead of the
            // card area's own manual-reorder Drop, which was the only path resetting it before.
            foreach (var col in viewModel.Columns)
            {
                col.IsDropIndicatorVisible = false;
            }
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        var canDrop = OutlookDragDropHelper.HasDroppableFiles(e.Data);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = canDrop;
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (!OutlookDragDropHelper.HasDroppableFiles(e.Data)) return;
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: CardViewModel card } || DataContext is not MainViewModel viewModel) return;

        List<(string FilePath, string DisplayName, bool WasSaved)> files;
        try
        {
            files = OutlookDragDropHelper.ExtractDroppedFiles(e.Data, viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't read the dropped item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var file in files)
        {
            viewModel.AddAttachmentToCard(card, file.FilePath, file.DisplayName);
        }
    }

    private void Column_Drop(object sender, DragEventArgs e)
    {
        var dragged = GetDraggedCards(e.Data);
        if (dragged.Count > 0 &&
            sender is FrameworkElement { DataContext: ColumnViewModel column } &&
            DataContext is MainViewModel viewModel)
        {
            // Deferred via BeginInvoke: same defensive reasoning as QuickMove_Click — this handler
            // still runs nested inside DoDragDrop's own message loop (Drop fires before DoDragDrop
            // returns to Card_MouseMove), so mutating the collection here immediately carries the
            // same class of risk as mutating it from inside a Click handler.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var moved = viewModel.MoveCards(dragged, column);

                // One card gets the usual completion prompt. A group moved into Done skips the
                // optional "add a note?" question rather than asking once per card, but a task set
                // to force an edit on completion still opens, since that is the task's own setting.
                var askForNote = dragged.Count == 1;
                foreach (var card in moved) MaybePromptCompletionNote(card, column, viewModel, askForNote);
                MaybePromptWaitingOn(moved, column, viewModel);
            }), DispatcherPriority.Background);
        }
    }

    // Positional drag-to-reorder within a column, always available - a card (or a selected group
    // of cards) dragged over its own current column is reordered there, and the board switches into
    // manual sort mode (see MainViewModel.ReorderCardsWithinColumn). A drag that includes any card
    // from a different column keeps using Column_Drop's move, so this deliberately returns false
    // (leaving the event unhandled, to bubble up to Column_Drop) for every other case.
    // The sender is the Grid wrapping each column's card list and its insertion line.
    private static bool TryGetManualReorderContext(object sender, DragEventArgs e,
        out System.Windows.Controls.ItemsControl itemsControl, out ColumnViewModel column, out List<CardViewModel> draggedCards)
    {
        itemsControl = null!;
        column = null!;
        draggedCards = null!;

        if (sender is not System.Windows.Controls.Grid { DataContext: ColumnViewModel col } grid) return false;
        var cards = GetDraggedCards(e.Data);
        if (cards.Count == 0 || !cards.All(col.Cards.Contains)) return false;

        var ic = grid.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
        if (ic is null) return false;

        itemsControl = ic;
        column = col;
        draggedCards = cards;
        return true;
    }

    private void CardsArea_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCards)) return;

        e.Handled = true;
        e.Effects = DragDropEffects.Move;

        var (_, indicatorY) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCards);
        column.DropIndicatorY = indicatorY;
        column.IsDropIndicatorVisible = true;
    }

    private void CardsArea_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.Grid { DataContext: ColumnViewModel column } area) return;

        // Same spurious-DragLeave guard as the sub-task drag indicator (AddTaskWindow.xaml.cs):
        // only actually hide once the mouse has genuinely left the card area's bounds, not just
        // crossed onto a child card that isn't itself drop-enabled.
        var position = e.GetPosition(area);
        if (position.X >= 0 && position.X <= area.ActualWidth &&
            position.Y >= 0 && position.Y <= area.ActualHeight)
        {
            return;
        }

        column.IsDropIndicatorVisible = false;
    }

    private void CardsArea_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetManualReorderContext(sender, e, out var itemsControl, out var column, out var draggedCards)) return;

        e.Handled = true;
        column.IsDropIndicatorVisible = false;
        if (DataContext is not MainViewModel viewModel) return;

        var (newIndex, _) = GetCardDropTarget(itemsControl, column, e.GetPosition(itemsControl), draggedCards);

        // Deferred via BeginInvoke: same reasoning as Column_Drop/QuickMove_Click above - this
        // handler still runs nested inside DoDragDrop's own message loop, so mutating the Cards
        // collection here immediately carries the same class of risk as mutating it from inside a
        // Click handler.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            viewModel.ReorderCardsWithinColumn(draggedCards, column, newIndex);
        }), DispatcherPriority.Background);
    }

    // Returns both where a drop would land (Index, in "before anything moves" Cards-count space -
    // matching MainViewModel.ReorderCardsWithinColumn) and the Y position (relative to itemsControl,
    // i.e. on screen) for the insertion-line indicator, so DragOver and Drop always agree. The
    // dragged cards themselves are never a target.
    //
    // The list is virtualized and filtered, so this walks the list's own items (the visible cards)
    // and only the ones that currently have an on-screen element, then maps the matched card back
    // to its position in the full column. Cards without an element are all either above or below
    // the visible area, and the mouse is inside it, so skipping them can't change the answer.
    internal static (int Index, double IndicatorY) GetCardDropTarget(System.Windows.Controls.ItemsControl itemsControl, ColumnViewModel column,
        Point positionInItemsControl, IReadOnlyCollection<CardViewModel> draggedCards)
    {
        var items = itemsControl.Items;
        var generator = itemsControl.ItemContainerGenerator;
        CardViewModel? lastCard = null;
        var lastIndex = -1;
        var lastBottom = 0.0;

        for (var i = 0; i < items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not FrameworkElement container) continue;

            var card = (CardViewModel)items[i];
            var top = container.TranslatePoint(new Point(0, 0), itemsControl).Y;
            if (!draggedCards.Contains(card) && positionInItemsControl.Y < top + container.ActualHeight / 2)
            {
                return (column.Cards.IndexOf(card), top);
            }

            lastCard = card;
            lastIndex = i;
            lastBottom = top + container.ActualHeight;
        }

        if (lastCard is null) return (0, 0);

        // Below every card that has an element: after the last visible card in the list means the
        // very end of the column (past any hidden ones too, as before); otherwise straight after
        // the last card on screen.
        return lastIndex == items.Count - 1
            ? (column.Cards.Count, lastBottom)
            : (column.Cards.IndexOf(lastCard) + 1, lastBottom);
    }
}
