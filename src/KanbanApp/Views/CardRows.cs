using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// A block wide enough for more than one card across lists its cards in rows (Settings, blocks). The list itself stays
// virtualized - only the rows in view are built - and each row lays its cards side by side. A row is the cards it
// holds, plus the column's own name for the move buttons that dim in the card's own column.
public sealed class CardRow(string name) : ObservableCollection<CardViewModel>
{
    public string Name { get; } = name;

    // Changes only the places that differ, so a card that stays put keeps what was built for it.
    internal void SetCards(IReadOnlyList<CardViewModel> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (i >= Count) Add(cards[i]);
            else if (!ReferenceEquals(this[i], cards[i])) this[i] = cards[i];
        }
        while (Count > cards.Count) RemoveAt(Count - 1);
    }
}

// A column's cards (its filtered list, CardsView) as rows of perRow, kept in step as cards are shown, hidden, added,
// moved or removed. Listens weakly, so a list that is replaced is free to go.
public sealed class CardRowList : ObservableCollection<CardRow>
{
    private readonly ICollectionView _cards;
    private readonly int _perRow;
    private readonly string _columnName;

    public CardRowList(ICollectionView cards, int perRow, string columnName)
    {
        _cards = cards;
        _perRow = Math.Max(1, perRow);
        _columnName = columnName;
        Sync();
        CollectionChangedEventManager.AddHandler(cards, OnCardsChanged);
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void Sync()
    {
        var cards = _cards.Cast<CardViewModel>().ToList();
        var rows = (cards.Count + _perRow - 1) / _perRow;
        for (var r = 0; r < rows; r++)
        {
            var slice = cards.GetRange(r * _perRow, Math.Min(_perRow, cards.Count - r * _perRow));
            if (r < Count) this[r].SetCards(slice);
            else
            {
                var row = new CardRow(_columnName);
                row.SetCards(slice);
                Add(row);
            }
        }
        while (Count > rows) RemoveAt(Count - 1);
    }
}
