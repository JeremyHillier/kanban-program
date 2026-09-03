namespace KanbanApp.ViewModels;

// Multi-key board sort (Ctrl+click stacks keys) and the small badges on the sidebar's sort
// buttons showing each active key's rank.
public partial class MainViewModel
{
    // Manual isn't a real sort key and has no button of its own - it means "stop auto-sorting, keep
    // whatever order the cards are already in". Dragging a card within its column switches into it
    // automatically (see ReorderCardWithinColumn), which unhighlights every real sort button; the
    // next click on any real sort button drops it again.
    public enum SortKey { Project, DueDate, Who, Priority, Manual }

    // Order matters: this is the active multi-key sort, most-significant key first. Never empty -
    // ToggleSortKey falls back to the default rather than letting the board end up unsorted.
    private readonly List<SortKey> _sortKeys = [SortKey.Project];

    public int ProjectSortRank => SortRankOf(SortKey.Project);
    public int DueDateSortRank => SortRankOf(SortKey.DueDate);
    public int WhoSortRank => SortRankOf(SortKey.Who);
    public int PrioritySortRank => SortRankOf(SortKey.Priority);
    public bool IsManualSort => _sortKeys.Contains(SortKey.Manual);

    private int SortRankOf(SortKey key)
    {
        var index = _sortKeys.IndexOf(key);
        return index < 0 ? 0 : index + 1;
    }

    private void NotifySortRanksChanged()
    {
        OnPropertyChanged(nameof(ProjectSortRank));
        OnPropertyChanged(nameof(DueDateSortRank));
        OnPropertyChanged(nameof(WhoSortRank));
        OnPropertyChanged(nameof(PrioritySortRank));
        OnPropertyChanged(nameof(IsManualSort));
    }

    // Plain click: reset the sort to just this one key. Ctrl+click: toggle this key in/out of the
    // active multi-key sort, appending it at the end when added. Never leaves the sort empty - if
    // toggling off the last key would do that, it falls back to the default (Project alone).
    // Any real key always drops Manual first, so clicking a sort button re-sorts a board that was
    // last arranged by hand.
    public void ToggleSortKey(SortKey key, bool additive)
    {
        _sortKeys.Remove(SortKey.Manual);

        if (additive)
        {
            if (!_sortKeys.Remove(key))
            {
                _sortKeys.Add(key);
            }

            if (_sortKeys.Count == 0)
            {
                _sortKeys.Add(SortKey.Project);
            }
        }
        else
        {
            _sortKeys.Clear();
            _sortKeys.Add(key);
        }

        NotifySortRanksChanged();
        ApplySort();
    }

    // Drag-to-reorder within a column: moves the card to newIndex and persists the whole column's
    // resulting order, the same way ApplySort does for an auto-sorted column. Dragging is always
    // available, so this also switches the board into manual sort mode first - otherwise the very
    // next ApplySort (triggered by any routine card mutation) would immediately undo the drag.
    public void ReorderCardWithinColumn(CardViewModel card, ColumnViewModel column, int newIndex)
    {
        var cards = column.Cards;
        var oldIndex = cards.IndexOf(card);
        if (oldIndex < 0) return;

        var insertAt = newIndex;
        if (insertAt > oldIndex) insertAt--;
        insertAt = Math.Clamp(insertAt, 0, cards.Count - 1);

        if (insertAt != oldIndex)
        {
            cards.Move(oldIndex, insertAt);
        }

        if (!IsManualSort)
        {
            _sortKeys.Clear();
            _sortKeys.Add(SortKey.Manual);
            NotifySortRanksChanged();
        }

        _db.UpdateSortOrders(cards.Select((c, i) => (c.Id, i)));
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "High" => 0,
        "Medium" => 1,
        "Normal" => 2,
        "Low" => 3,
        _ => 2
    };

    private static IOrderedEnumerable<CardViewModel> OrderByKey(IEnumerable<CardViewModel> cards, SortKey key) => key switch
    {
        SortKey.DueDate => cards.OrderBy(c => c.DueDate ?? DateTime.MaxValue),
        SortKey.Who => cards.OrderBy(c => c.WhoName, StringComparer.OrdinalIgnoreCase),
        SortKey.Priority => cards.OrderBy(c => PriorityRank(c.Priority)),
        _ => cards.OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
    };

    private static IOrderedEnumerable<CardViewModel> ThenByKey(IOrderedEnumerable<CardViewModel> cards, SortKey key) => key switch
    {
        SortKey.DueDate => cards.ThenBy(c => c.DueDate ?? DateTime.MaxValue),
        SortKey.Who => cards.ThenBy(c => c.WhoName, StringComparer.OrdinalIgnoreCase),
        SortKey.Priority => cards.ThenBy(c => PriorityRank(c.Priority)),
        _ => cards.ThenBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
    };

    private void ApplySort()
    {
        // Manual mode means "leave the current order alone" - skip re-sorting entirely so that
        // routine card mutations (add/edit/move) elsewhere, which all call ApplySort afterward,
        // don't silently undo a manually dragged order.
        if (IsManualSort) return;

        var updates = new List<(int, int)>();
        var anyMoved = false;

        foreach (var column in Columns)
        {
            var ordered = OrderByKey(column.Cards, _sortKeys[0]);
            for (var k = 1; k < _sortKeys.Count; k++)
            {
                ordered = ThenByKey(ordered, _sortKeys[k]);
            }
            var sorted = ordered.ToList();

            // Reorder in place with Move() rather than Clear()+Add(): the latter tears down and
            // recreates every card's visual container, which can orphan an in-flight drag capture
            // or a still-closing popup anchored to one of those cards and appear to freeze the app.
            for (var i = 0; i < sorted.Count; i++)
            {
                var currentIndex = column.Cards.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    column.Cards.Move(currentIndex, i);
                    anyMoved = true;
                }
                updates.Add((sorted[i].Id, i));
            }
        }

        // Every routine card mutation (add, edit, quick-edit, move) calls ApplySort afterwards, and
        // this used to rewrite SortOrder for every card on the board each time - even when editing
        // a field the active sort doesn't depend on, which is the common case. When nothing actually
        // moved, the stored order already equals the on-screen order, so the write is a no-op worth
        // skipping. When anything moved, all rows are still written, so the "stored SortOrder ==
        // index" invariant that makes this safe is preserved exactly as before.
        if (anyMoved)
        {
            _db.UpdateSortOrders(updates);
        }
    }
}
