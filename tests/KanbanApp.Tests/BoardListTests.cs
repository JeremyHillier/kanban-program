using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// What each board column actually lists (ColumnViewModel.CardsView): the column's cards in board
// order, minus the ones the filters hide. The board only builds on-screen cards, so this list,
// not each card's IsVisible, is what decides what the user sees.
[Collection(WpfCollection.Name)]
public sealed class BoardListTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string column = "To Do", string priority = "Normal", DateTime? due = null) =>
        board.AddCard(title, Column(board, column), board.Projects.First(), priority, due, null, false, null, null);

    // WPF applies live filtering a moment after a card changes (queued on the UI thread, ahead of
    // the next draw), so let it catch up before reading the list, as the screen would.
    private static List<string> Listed(ColumnViewModel column)
    {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        return column.CardsView.Cast<CardViewModel>().Select(c => c.Title).ToList();
    }

    // The invariant every test below checks: the list is exactly the visible cards, in board order.
    private static void AssertListMatchesBoard(MainViewModel board)
    {
        foreach (var column in board.Columns)
        {
            Assert.Equal(column.Cards.Where(c => c.IsVisible).Select(c => c.Title), Listed(column));
        }
    }

    [Fact]
    public void FilteringLeavesHiddenCardsOutOfTheList_AndClearingBringsThemBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "A high", priority: "High");
        Add(board, "B normal");
        Add(board, "C high", priority: "High");
        var todo = Column(board, "To Do");
        Assert.Equal(3, Listed(todo).Count);

        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters(); // as the board's filter checkboxes do
        Assert.Equal(["A high", "C high"], Listed(todo));
        AssertListMatchesBoard(board);

        board.ClearFilters();
        Assert.Equal(3, Listed(todo).Count);
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void EditingACardOutOfTheFilter_RemovesItFromTheList_AndBackIn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Changes priority", priority: "High");
        Add(board, "Stays high", priority: "High");
        var todo = Column(board, "To Do");
        _ = todo.CardsView; // bound, as the board would be
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters(); // as the board's filter checkboxes do

        board.SetCardPriority(card, "Low");
        Assert.DoesNotContain("Changes priority", Listed(todo));
        AssertListMatchesBoard(board);

        board.SetCardPriority(card, "High");
        Assert.Contains("Changes priority", Listed(todo));
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void ANewCardThatDoesNotMatchTheFilter_IsNotListed() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        _ = todo.CardsView;
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters(); // as the board's filter checkboxes do

        Add(board, "Hidden newcomer", priority: "Low");
        Add(board, "Shown newcomer", priority: "High");

        Assert.Equal(["Shown newcomer"], Listed(todo));
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void MovingACard_ListsItInItsNewColumnOnly() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Travelling card");
        var todo = Column(board, "To Do");
        var done = Column(board, "Done");
        _ = todo.CardsView;
        _ = done.CardsView;

        board.MoveCardCommand.Execute((card, done));

        Assert.Empty(Listed(todo));
        Assert.Equal(["Travelling card"], Listed(done));
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void ARoutineEditKeepsTheSameList_WhileABigResortHandsTheBoardAFreshOne() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        var start = new DateTime(2026, 1, 1);
        // Project sort (the default) leaves these in creation order, so a due-date sort reverses
        // every one of them - far more than a routine edit moves.
        for (var i = 0; i < 120; i++) Add(board, $"Task {i:000}", due: start.AddDays(-i));
        var listBefore = todo.CardsView;
        var notified = new List<string?>();
        todo.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        board.SetCardPriority(todo.Cards[5], "High");
        Assert.Same(listBefore, todo.CardsView);
        Assert.DoesNotContain(nameof(ColumnViewModel.CardsView), notified);

        board.ToggleSortKey(MainViewModel.SortKey.DueDate, additive: false);

        Assert.Contains(nameof(ColumnViewModel.CardsView), notified);
        Assert.NotSame(listBefore, todo.CardsView);
        Assert.Equal("Task 119", Listed(todo)[0]);
        Assert.Equal("Task 000", Listed(todo)[^1]);
        AssertListMatchesBoard(board);

        // The old list was cut loose, so later changes don't reach it...
        var countOfOldList = listBefore.Count;
        Add(board, "Added after the re-sort", due: start.AddDays(-500));
        Assert.Equal(countOfOldList, listBefore.Count);
        todo.Cards[0].IsVisible = false;
        _ = Listed(todo); // let any queued live update run before checking the old list
        Assert.Equal(countOfOldList, listBefore.Count);
        todo.Cards[0].IsVisible = true;
        // ...and the new one keeps tracking the board.
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void ABigResortWithAFilterOn_StillListsOnlyTheMatchingCardsInTheNewOrder() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        for (var i = 0; i < 120; i++) Add(board, $"Task {i:000}", priority: i % 2 == 0 ? "High" : "Low", due: new DateTime(2026, 1, 1).AddDays(-i));
        _ = todo.CardsView;
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters(); // as the board's filter checkboxes do

        board.ToggleSortKey(MainViewModel.SortKey.DueDate, additive: false);

        Assert.Equal(60, Listed(todo).Count);
        Assert.Equal("Task 118", Listed(todo)[0]);
        AssertListMatchesBoard(board);
    });

    [Fact]
    public void DragReorderIsReflectedInTheList() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "First");
        Add(board, "Second");
        var third = Add(board, "Third");
        var todo = Column(board, "To Do");
        _ = todo.CardsView;

        board.ReorderCardWithinColumn(third, todo, 0);

        Assert.Equal(["Third", "First", "Second"], Listed(todo));
        AssertListMatchesBoard(board);
    });
}
