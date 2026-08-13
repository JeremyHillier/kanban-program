using System.Collections.ObjectModel;
using System.Windows.Media;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

public class MainViewModel
{
    private readonly DatabaseService _db;

    private static readonly Brush[] ColumnPalette =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    public ObservableCollection<ColumnViewModel> Columns { get; } = [];

    public RelayCommand AddCardCommand { get; }
    public RelayCommand DeleteCardCommand { get; }
    public RelayCommand MoveCardCommand { get; }

    public MainViewModel(DatabaseService db)
    {
        _db = db;

        AddCardCommand = new RelayCommand(param => AddCard(param as ColumnViewModel));
        DeleteCardCommand = new RelayCommand(param => DeleteCard(param as CardViewModel));
        MoveCardCommand = new RelayCommand(param =>
        {
            if (param is (CardViewModel card, ColumnViewModel targetColumn))
            {
                MoveCard(card, targetColumn);
            }
        });

        Load();
    }

    private void Load()
    {
        Columns.Clear();
        var columns = _db.GetColumns();
        var cards = _db.GetCards();

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var background = ColumnPalette[i % ColumnPalette.Length];
            var columnVm = new ColumnViewModel(column, background);
            foreach (var card in cards.Where(c => c.ColumnId == column.Id))
            {
                columnVm.Cards.Add(new CardViewModel(card));
            }
            Columns.Add(columnVm);
        }
    }

    private void AddCard(ColumnViewModel? column)
    {
        if (column is null || string.IsNullOrWhiteSpace(column.NewCardTitle)) return;

        var card = _db.AddCard(column.Id, column.NewCardTitle.Trim());
        column.Cards.Add(new CardViewModel(card));
        column.NewCardTitle = string.Empty;
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        column?.Cards.Remove(card);
        _db.DeleteCard(card.Id);
    }

    private void MoveCard(CardViewModel card, ColumnViewModel targetColumn)
    {
        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is null || sourceColumn == targetColumn) return;

        sourceColumn.Cards.Remove(card);
        card.ColumnId = targetColumn.Id;
        targetColumn.Cards.Add(card);

        _db.MoveCard(card.Id, targetColumn.Id);
    }
}
