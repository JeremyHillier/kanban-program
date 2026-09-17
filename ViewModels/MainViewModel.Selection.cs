namespace KanbanApp.ViewModels;

// Selecting several cards (Ctrl+click, Shift+click) so they can be dragged together: to another
// column, or as a block to a new place in their own column. Selection is screen state only and is
// never saved.
public partial class MainViewModel
{
    private CardViewModel? _selectionAnchor;

    // Selected cards in board order (column by column, top to bottom). Only cards currently shown
    // count - ApplyFilters deselects anything it hides, so this is belt and braces.
    public List<CardViewModel> SelectedCards =>
        Columns.SelectMany(c => c.Cards).Where(c => c.IsSelected && c.IsVisible).ToList();

    public int SelectedCardCount => SelectedCards.Count;

    private void NotifySelectionChanged() => OnPropertyChanged(nameof(SelectedCardCount));

    public void ToggleCardSelection(CardViewModel card)
    {
        card.IsSelected = !card.IsSelected;
        _selectionAnchor = card;
        NotifySelectionChanged();
    }

    // Shift+click: everything between the last card clicked and this one, in the list as shown. A
    // range can't span columns, so a Shift+click in a different column just selects that card.
    public void SelectCardRange(CardViewModel card)
    {
        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (column is null) return;

        var shown = column.Cards.Where(c => c.IsVisible).ToList();
        var from = _selectionAnchor is null ? -1 : shown.IndexOf(_selectionAnchor);
        var to = shown.IndexOf(card);
        if (from < 0 || to < 0)
        {
            ToggleCardSelection(card);
            return;
        }

        for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++) shown[i].IsSelected = true;
        NotifySelectionChanged();
    }

    public void ClearCardSelection()
    {
        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.IsSelected)) card.IsSelected = false;
        _selectionAnchor = null;
        NotifySelectionChanged();
    }

    // Moves each card that isn't already in the target column there, in board order, exactly as
    // moving them one at a time would (history, completion time, recurring next occurrence).
    // Returns the cards that actually moved.
    public List<CardViewModel> MoveCards(IEnumerable<CardViewModel> cards, ColumnViewModel targetColumn)
    {
        var moving = cards.Where(c => !targetColumn.Cards.Contains(c)).ToList();
        foreach (var card in moving) MoveCard(card, targetColumn);
        return moving;
    }

    // Drag-reorder within a column for one or more cards: they end up together, in their current
    // order, at newIndex (counted in the column before any of them move, the same as the drop
    // target). Persists the column's order and switches to manual sort, since otherwise the next
    // automatic re-sort would undo the drag.
    //
    // Only the dragged cards are moved, so nothing else in the column is disturbed: one Move for a
    // single card, and for a group two passes - each card to the end, then the block back into
    // place - which is 2 moves per card however far it travels.
    public void ReorderCardsWithinColumn(IEnumerable<CardViewModel> cards, ColumnViewModel column, int newIndex)
    {
        var list = column.Cards;
        var draggedSet = cards.ToHashSet();
        var dragged = list.Where(draggedSet.Contains).ToList();
        if (dragged.Count == 0) return;

        var insertAt = list.Take(Math.Clamp(newIndex, 0, list.Count)).Count(c => !draggedSet.Contains(c));
        var rest = list.Where(c => !draggedSet.Contains(c)).ToList();
        var final = rest.Take(insertAt).Concat(dragged).Concat(rest.Skip(insertAt)).ToList();

        if (!final.SequenceEqual(list))
        {
            if (dragged.Count == 1)
            {
                list.Move(list.IndexOf(dragged[0]), insertAt);
            }
            else
            {
                foreach (var card in dragged) list.Move(list.IndexOf(card), list.Count - 1);
                var firstDragged = list.Count - dragged.Count;
                for (var j = 0; j < dragged.Count && insertAt < firstDragged; j++) list.Move(firstDragged + j, insertAt + j);
            }

            _db.UpdateSortOrders(list.Select((c, i) => (c.Id, i)));
        }

        if (!IsManualSort)
        {
            _sortKeys.Clear();
            _sortKeys.Add(SortKey.Manual);
            NotifySortRanksChanged();
        }
    }
}
