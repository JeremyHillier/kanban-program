using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Picking several cards (Ctrl/Shift+click) and dragging them together.
[Collection(WpfCollection.Name)]
public sealed class CardSelectionTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static List<CardViewModel> AddMany(MainViewModel board, string column, params string[] titles) =>
        titles.Select(t => board.AddCard(t, Column(board, column), board.Projects.First(), "Normal", null, null, false, null, null)).ToList();

    private static List<string> Titles(ColumnViewModel column) => column.Cards.Select(c => c.Title).ToList();

    [Fact]
    public void CtrlClickTogglesCards_AndTheSelectionIsInBoardOrder() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C");
        var done = AddMany(board, "Done", "D").Single();

        board.ToggleCardSelection(done);
        board.ToggleCardSelection(cards[2]);
        board.ToggleCardSelection(cards[0]);
        Assert.Equal(["A", "C", "D"], board.SelectedCards.Select(c => c.Title));
        Assert.Equal(3, board.SelectedCardCount);

        board.ToggleCardSelection(cards[2]);
        Assert.Equal(["A", "D"], board.SelectedCards.Select(c => c.Title));

        board.ClearCardSelection();
        Assert.Equal(0, board.SelectedCardCount);
        Assert.All(board.Columns.SelectMany(c => c.Cards), c => Assert.False(c.IsSelected));
    });

    [Fact]
    public void ShiftClickSelectsTheRangeShown_SkippingHiddenCards() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C", "D", "E");
        board.SetCardPriority(cards[2], "Low");
        board.PriorityFilterOptions.Single(o => o.Name == "Normal").IsSelected = true;
        board.ApplyFilters(); // C is now hidden

        board.ToggleCardSelection(cards[3]); // D
        board.SelectCardRange(cards[0]);     // back to A

        Assert.Equal(["A", "B", "D"], board.SelectedCards.Select(c => c.Title));
        Assert.False(cards[2].IsSelected);
    });

    [Fact]
    public void ShiftClickInAnotherColumn_JustSelectsThatCard() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = AddMany(board, "To Do", "A", "B");
        var done = AddMany(board, "Done", "X", "Y");

        board.ToggleCardSelection(todo[0]);
        board.SelectCardRange(done[1]);

        Assert.Equal(["A", "Y"], board.SelectedCards.Select(c => c.Title));
    });

    [Fact]
    public void HidingASelectedCard_DeselectsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B");
        board.ToggleCardSelection(cards[0]);
        board.ToggleCardSelection(cards[1]);
        board.PriorityFilterOptions.Single(o => o.Name == "Normal").IsSelected = true;
        board.ApplyFilters();

        // Editing one out of the filter...
        board.SetCardPriority(cards[0], "High");
        Assert.False(cards[0].IsSelected);
        Assert.Equal(["B"], board.SelectedCards.Select(c => c.Title));

        // ...or filtering it away.
        board.KeywordFilter = "no such card";
        Assert.Equal(0, board.SelectedCardCount);
    });

    [Fact]
    public void DeletingASelectedCard_DropsItFromTheCount() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B");
        board.ToggleCardSelection(cards[0]);
        board.ToggleCardSelection(cards[1]);
        var notified = 0;
        board.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.SelectedCardCount)) notified++; };

        board.DeleteCard(cards[0]);

        Assert.Equal(1, board.SelectedCardCount);
        Assert.True(notified > 0);
    });

    [Fact]
    public void MovingAGroup_MovesOnlyTheCardsNotAlreadyThere_InBoardOrder() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = AddMany(board, "To Do", "A", "B", "C");
        var inProgress = AddMany(board, "In Progress", "P");
        var done = Column(board, "Done");
        board.MoveCardCommand.Execute((inProgress[0], done));

        var moved = board.MoveCards([todo[2], inProgress[0], todo[0]], done);

        Assert.Equal(["C", "A"], moved.Select(c => c.Title));
        Assert.Equal(["B"], Titles(Column(board, "To Do")));
        Assert.Equal(3, done.Cards.Count);
        Assert.All(done.Cards, c => Assert.NotNull(c.CompletedAt));

        var reopened = OpenBoard();
        Assert.Equal(3, Column(reopened, "Done").Cards.Count);
    });

    [Fact]
    public void MovingAGroupIntoDone_SpawnsEachRecurringTasksNextOccurrence() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        var first = board.AddCard("Pay rent", todo, board.Projects.First(), "Normal", new DateTime(2026, 9, 1), null, true, "Monthly", null);
        var second = board.AddCard("Water plants", todo, board.Projects.First(), "Normal", new DateTime(2026, 9, 1), null, true, "Weekly", null);

        board.MoveCards([first, second], Column(board, "Done"));

        Assert.Equal(["Pay rent", "Water plants"], todo.Cards.Select(c => c.Title).OrderBy(t => t));
        Assert.All(todo.Cards, c => Assert.True(c.DueDate > new DateTime(2026, 9, 1)));
    });

    [Theory]
    // Column A B C D E F; the group is picked out by index; newIndex is where it was dropped
    // (counted before anything moves); expected is the resulting column.
    [InlineData(new[] { 0, 2 }, 5, "BDEACF")]   // non-adjacent cards dropped low, keep their order
    [InlineData(new[] { 0, 2 }, 6, "BDEFAC")]   // dropped below the last card
    [InlineData(new[] { 3, 5 }, 0, "DFABCE")]   // dropped at the top
    [InlineData(new[] { 4, 1 }, 3, "ACBEDF")]   // picked out of order: board order wins
    [InlineData(new[] { 1, 2 }, 2, "ABCDEF")]   // dropped where they already are
    [InlineData(new[] { 1, 2 }, 3, "ABCDEF")]   // just below themselves: still the same place
    [InlineData(new[] { 2 }, 5, "ABDECF")]      // a single card, down
    [InlineData(new[] { 4 }, 1, "AEBCDF")]      // a single card, up
    public void ReorderingAGroup_PutsItTogetherAtTheDropPoint(int[] picked, int newIndex, string expected) => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C", "D", "E", "F");
        var todo = Column(board, "To Do");

        board.ReorderCardsWithinColumn(picked.Select(i => cards[i]).ToList(), todo, newIndex);

        Assert.Equal(expected, string.Concat(Titles(todo)));
        Assert.True(board.IsManualSort);
        var saved = new DatabaseService(_temp.File("board.db")).GetCards().Where(c => c.ColumnId == todo.Id).OrderBy(c => c.SortOrder);
        Assert.Equal(expected, string.Concat(saved.Select(c => c.Title)));
    });

    [Fact]
    public void ReorderingAGroupMovesOnlyTheDraggedCards() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C", "D", "E", "F", "G", "H");
        var todo = Column(board, "To Do");
        var moves = 0;
        todo.Cards.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move) moves++; };

        board.ReorderCardsWithinColumn([cards[0], cards[1]], todo, 5); // dropped before F

        Assert.Equal("CDEABFGH", string.Concat(Titles(todo)));
        Assert.True(moves <= 4, $"{moves} moves");
    });

    [Fact]
    public void ReorderingWithHiddenCards_LeavesThemInTheirPlaces() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "b", "C", "d", "E");
        board.SetCardPriority(cards[1], "Low");
        board.SetCardPriority(cards[3], "Low");
        board.PriorityFilterOptions.Single(o => o.Name == "Normal").IsSelected = true;
        board.ApplyFilters(); // b and d are hidden
        var todo = Column(board, "To Do");

        // Hidden b and d stay in order relative to each other; the group lands before E.
        board.ReorderCardsWithinColumn([cards[0], cards[2]], todo, 4);

        var order = string.Concat(Titles(todo));
        Assert.Equal("bdACE", order);
    });
}
