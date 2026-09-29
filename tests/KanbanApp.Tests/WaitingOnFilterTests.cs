using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The sidebar's Waiting filter: the answers on the board's tasks, narrowing alongside the other filters.
[Collection(WpfCollection.Name)]
public sealed class WaitingOnFilterTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string? waitingOn = null, string priority = "Normal") =>
        board.AddCard(title, Column(board, "To Do"), board.Projects.First(), priority, null, null, false, null, null, waitingOn: waitingOn);

    [Fact]
    public void TheOptions_AreTheAnswersOnTheBoard_OnceEach_Alphabetical() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal(["All", "Any", "Unassigned"], board.WaitingOnFilterOptions);

        Add(board, "Order parts", "Sam's quote");
        Add(board, "Book room", "facilities");
        Add(board, "Order more parts", "sam's quote"); // same answer, other capitals
        Add(board, "Write report");

        Assert.Equal(["All", "Any", "Unassigned", "facilities", "Sam's quote"], board.WaitingOnFilterOptions);
    });

    [Fact]
    public void AnAnswer_ShowsItsTasks_WhateverTheirCapitals() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var sam = Add(board, "Order parts", "Sam's quote");
        var samAgain = Add(board, "Order more parts", "SAM'S QUOTE");
        var facilities = Add(board, "Book room", "facilities");
        var free = Add(board, "Write report");

        board.SelectedWaitingOnFilter = "Sam's quote";

        Assert.True(sam.IsVisible);
        Assert.True(samAgain.IsVisible);
        Assert.False(facilities.IsVisible);
        Assert.False(free.IsVisible);
    });

    [Fact]
    public void Any_And_Unassigned_SplitTheBoard() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var waiting = Add(board, "Order parts", "Sam's quote");
        var free = Add(board, "Write report");

        board.SelectedWaitingOnFilter = "Any";
        Assert.True(waiting.IsVisible);
        Assert.False(free.IsVisible);

        board.SelectedWaitingOnFilter = "Unassigned";
        Assert.False(waiting.IsVisible);
        Assert.True(free.IsVisible);
    });

    [Fact]
    public void ItNarrowsAlongsideTheOtherFilters_AndClearFiltersResetsIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var urgent = Add(board, "Order parts", "Sam's quote", priority: "High");
        var routine = Add(board, "Order more parts", "Sam's quote");

        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.SelectedWaitingOnFilter = "Sam's quote";
        Assert.True(urgent.IsVisible);
        Assert.False(routine.IsVisible);

        board.ClearFilters();
        Assert.Equal("All", board.SelectedWaitingOnFilter);
        Assert.True(routine.IsVisible);
    });

    [Fact]
    public void TheChosenAnswer_StaysListed_UntilSomethingElseIsChosen() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts", "Sam's quote");
        board.SelectedWaitingOnFilter = "Sam's quote";

        board.SetCardWaitingOn(card, null); // no task says it now

        Assert.Contains("Sam's quote", board.WaitingOnFilterOptions);
        Assert.False(card.IsVisible);

        board.SelectedWaitingOnFilter = "All";
        Assert.DoesNotContain("Sam's quote", board.WaitingOnFilterOptions);
    });

    [Fact]
    public void ANewAnswer_JoinsTheList_AndARewordedOneFollows() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Order parts");

        board.SetCardWaitingOn(card, "Sam's quote");
        Assert.Contains("Sam's quote", board.WaitingOnFilterOptions);

        board.RenameWaitingOnSuggestion("Sam's quote", "Sam's revised quote");
        Assert.Contains("Sam's revised quote", board.WaitingOnFilterOptions);
        Assert.DoesNotContain("Sam's quote", board.WaitingOnFilterOptions);
    });

    [Fact]
    public void ACustomFilter_KeepsIt_AndOldSlotsReadAsAll() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var sam = Add(board, "Order parts", "Sam's quote");
        var free = Add(board, "Write report");

        board.SelectedWaitingOnFilter = "Sam's quote";
        board.CaptureCustomFilter(1, "Sam");
        Assert.Contains("Waiting: Sam's quote", board.CustomFilters[1].Summary);

        board.ClearFilters();
        Assert.True(free.IsVisible);

        Assert.True(board.ApplyCustomFilter(1));
        Assert.Equal("Sam's quote", board.SelectedWaitingOnFilter);
        Assert.True(sam.IsVisible);
        Assert.False(free.IsVisible);

        var old = System.Text.Json.JsonSerializer.Deserialize<CustomFilter>("""{"Name":"Old","Flag":"All"}""")!;
        Assert.Equal("All", old.WaitingOn);
    });

    [Fact]
    public void ItIsRemembered_WithTheRestOfTheView() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "Order parts", "Sam's quote");
        Add(board, "Write report");
        if (!board.RememberLastView) board.SetRememberLastView(true);

        board.SelectedWaitingOnFilter = "Sam's quote";
        board.SaveLastViewState();

        var reopened = OpenBoard();
        Assert.Equal("Sam's quote", reopened.SelectedWaitingOnFilter);
        Assert.False(Column(reopened, "To Do").Cards.Single(c => c.Title == "Write report").IsVisible);
    });
}
