namespace KanbanApp.ViewModels;

// Multi-key board sort (Ctrl+click stacks keys) and the small badges on the sidebar's sort
// buttons showing each active key's rank.
public partial class MainViewModel
{
    // Manual isn't a real sort key - it means "stop auto-sorting, keep whatever order the cards
    // are already in" (drag-to-reorder persists that order). It never combines with a real key:
    // ToggleSortKey strips it the moment any real key is toggled, and toggling Manual itself
    // always replaces the whole selection rather than stacking.
    public enum SortKey { Project, DueDate, Who, Priority, Manual }

    // Order matters: this is the active multi-key sort, most-significant key first. Never empty -
    // ToggleSortKey falls back to the default rather than letting the board end up unsorted.
    private readonly List<SortKey> _sortKeys = [SortKey.Project];

    public int ProjectSortRank => SortRankOf(SortKey.Project);
    public int DueDateSortRank => SortRankOf(SortKey.DueDate);
    public int WhoSortRank => SortRankOf(SortKey.Who);
    public int PrioritySortRank => SortRankOf(SortKey.Priority);
    public int ManualSortRank => SortRankOf(SortKey.Manual);
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
        OnPropertyChanged(nameof(ManualSortRank));
        OnPropertyChanged(nameof(IsManualSort));
    }

    // Plain click: reset the sort to just this one key. Ctrl+click: toggle this key in/out of the
    // active multi-key sort, appending it at the end when added. Never leaves the sort empty - if
    // toggling off the last key would do that, it falls back to the default (Project alone).
    // Manual is handled separately (see the SortKey.Manual doc comment above): selecting it always
    // replaces the selection outright and skips ApplySort so the current card order is preserved;
    // selecting any real key always drops Manual first, so any other sort button overrides it.
    public void ToggleSortKey(SortKey key, bool additive)
    {
        if (key == SortKey.Manual)
        {
            _sortKeys.Clear();
            _sortKeys.Add(SortKey.Manual);
            NotifySortRanksChanged();
            return;
        }

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

    // Drag-to-reorder within a column, only meaningful (and only ever called) in manual sort mode -
    // moves the card to newIndex and persists the whole column's resulting order, the same way
    // ApplySort does for an auto-sorted column.
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
                }
                updates.Add((sorted[i].Id, i));
            }
        }

        _db.UpdateSortOrders(updates);
    }
}
