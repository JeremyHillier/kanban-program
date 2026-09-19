using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The right-click menu's actions applied to a whole selection of cards.
[Collection(WpfCollection.Name)]
public sealed class SelectionActionTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static List<CardViewModel> AddMany(MainViewModel board, string column, params string[] titles) =>
        titles.Select(t => board.AddCard(t, Column(board, column), board.Projects.First(), "Normal", null, null, false, null, null)).ToList();

    private static CardViewModel Find(MainViewModel board, string title) =>
        board.Columns.SelectMany(c => c.Cards).Single(c => c.Title == title);

    [Fact]
    public void PriorityWhoAndProject_ChangeEverySelectedCard_AndAreSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Sam Lee");
        board.AddProject("Website");
        var cards = AddMany(board, "To Do", "A", "B", "C");
        var group = new[] { cards[0], cards[2] };

        board.SetCardsPriority(group, "High");
        board.SetCardsWho(group, board.People.Single(p => p.Name == "Sam Lee"));
        board.SetCardsProject(group, board.Projects.Single(p => p.Name == "Website"));

        var reopened = OpenBoard();
        foreach (var title in new[] { "A", "C" })
        {
            var card = Find(reopened, title);
            Assert.Equal("High", card.Priority);
            Assert.Equal("Sam Lee", card.WhoName);
            Assert.Equal("Website", card.ProjectName);
        }

        var untouched = Find(reopened, "B");
        Assert.Equal("Normal", untouched.Priority);
        Assert.Null(untouched.WhoId);
        Assert.NotEqual("Website", untouched.ProjectName);
    });

    [Fact]
    public void UnassigningAGroup_ClearsWho() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Sam Lee");
        var cards = AddMany(board, "To Do", "A", "B");
        board.SetCardsWho(cards, board.People.Single());

        board.SetCardsWho(cards, null);

        Assert.All(cards, c => Assert.Null(c.WhoId));
        Assert.All(cards, c => Assert.Equal("Unassigned", c.WhoName));
        Assert.All(OpenBoard().Columns.SelectMany(c => c.Cards), c => Assert.Null(c.WhoId));
    });

    [Fact]
    public void AChangeThatHidesCards_DropsThemFromTheSelection() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C");
        board.PriorityFilterOptions.Single(o => o.Name == "Normal").IsSelected = true;
        board.ApplyFilters();
        foreach (var card in cards) board.ToggleCardSelection(card);

        board.SetCardsPriority([cards[0], cards[1]], "Low"); // no longer match the Normal filter

        Assert.False(cards[0].IsVisible);
        Assert.False(cards[1].IsVisible);
        Assert.Equal(["C"], board.SelectedCards.Select(c => c.Title));
        Assert.Equal(1, board.SelectedCardCount);
    });

    [Fact]
    public void AddingAFlag_SkipsCardsThatAlreadyHaveIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddFlag("Urgent");
        var urgent = board.Flags.Single(f => f.Name == "Urgent");
        var cards = AddMany(board, "To Do", "A", "B");
        board.AddFlagToCard(cards[0], urgent);

        board.AddFlagToCards(cards, urgent);

        Assert.All(cards, c => Assert.Single(c.Flags));
        Assert.All(OpenBoard().Columns.SelectMany(c => c.Cards), c => Assert.Equal("Urgent", Assert.Single(c.Flags).Name));
    });

    [Fact]
    public void DuplicatingAGroup_CopiesEachIntoItsOwnColumn_AndLeavesTheOriginalsSelected() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = AddMany(board, "To Do", "A").Single();
        var done = AddMany(board, "Done", "B").Single();
        board.ToggleCardSelection(todo);
        board.ToggleCardSelection(done);

        var copies = board.DuplicateCards(board.SelectedCards);

        Assert.Equal(["A (copy)", "B (copy)"], copies.Select(c => c.Title));
        Assert.Contains(copies[0], Column(board, "To Do").Cards);
        Assert.Contains(copies[1], Column(board, "Done").Cards);
        Assert.All(copies, c => Assert.False(c.IsSelected));
        Assert.Equal(["A", "B"], board.SelectedCards.Select(c => c.Title));
    });

    [Fact]
    public void DeletingAGroup_RemovesTheCards_AndEmptiesTheSelection() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C");
        board.ToggleCardSelection(cards[0]);
        board.ToggleCardSelection(cards[1]);
        var counts = new List<int>();
        board.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.SelectedCardCount)) counts.Add(board.SelectedCardCount); };

        board.DeleteCards(board.SelectedCards);

        Assert.Equal(["C"], Column(board, "To Do").Cards.Select(c => c.Title));
        Assert.Equal(0, board.SelectedCardCount);
        Assert.Equal(0, counts.Last());
        Assert.Equal(["C"], OpenBoard().Columns.SelectMany(c => c.Cards).Select(c => c.Title));
    });

    [Fact]
    public void DeletingAGroup_KeepingSeriesGoing_CreatesTheNextOccurrenceOfRecurringCardsOnly() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        var plain = AddMany(board, "To Do", "Plain").Single();
        var weekly = board.AddCard("Weekly", todo, board.Projects.First(), "Normal", new DateTime(2026, 9, 21), null, true, "Weekly", null);

        board.DeleteCards([plain, weekly], spawnNextOccurrence: true);

        var next = Assert.Single(todo.Cards);
        Assert.Equal("Weekly", next.Title);
        Assert.Equal(new DateTime(2026, 9, 28), next.DueDate);
    });

    [Fact]
    public void DeletingAGroup_EndingSeries_CreatesNothing() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        var weekly = board.AddCard("Weekly", todo, board.Projects.First(), "Normal", new DateTime(2026, 9, 21), null, true, "Weekly", null);
        var other = AddMany(board, "To Do", "Other").Single();

        board.DeleteCards([weekly, other]);

        Assert.Empty(todo.Cards);
    });
}
