using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Past Waiting On answers: what is remembered, in what order, and how typing narrows and completes them.
[Collection(WpfCollection.Name)]
public sealed class WaitingOnSuggestionTests(WpfDispatcherFixture wpf) : IDisposable
{
    private static readonly string[] Past = ["Sam's quote", "Client sign-off", "Sara's review", "Legal team", "Budget approval from Sam"];
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static CardViewModel Add(MainViewModel board, string title, string? waitingOn = null) =>
        board.AddCard(title, board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null, waitingOn: waitingOn);

    [Fact]
    public void AnswersAreRemembered_NewestFirst_EvenAfterTheTaskIsFinished() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Task");
        board.SetCardWaitingOn(card, "Sam's quote");
        board.SetCardWaitingOn(card, "Client sign-off");
        board.MoveCardCommand.Execute((card, board.Columns.Single(c => c.Name == "Done"))); // clears the task's own

        Assert.Null(card.WaitingOn);
        Assert.Equal(["Client sign-off", "Sam's quote"], OpenBoard().WaitingOnSuggestions);
    });

    [Fact]
    public void EveryWayOfSettingIt_IsRemembered_AndARepeatMovesToTheTop_WithoutDuplicates() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var a = Add(board, "A", "  From the new task screen ");
        var b = Add(board, "B");
        board.SetCardsWaitingOn([a, b], "Group answer");
        board.EditCard(b, b.Title, board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null, null, null, null, null, false,
            null, null, null, "from the edit screen", null);
        board.SetCardWaitingOn(a, "GROUP ANSWER"); // same answer, different capitals
        board.SetCardWaitingOn(a, null);            // clearing remembers nothing

        Assert.Equal(["GROUP ANSWER", "from the edit screen", "From the new task screen"], board.WaitingOnSuggestions);
    });

    [Fact]
    public void AnAnswerOnATaskButNeverTyped_IsStillOffered_AfterTheRememberedOnes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "Typed", "Typed answer");
        var other = Add(board, "Other", "Zed");
        board.ForgetWaitingOnSuggestion("Zed"); // off the remembered list, but the task still says it

        Assert.Equal("Zed", other.WaitingOn);
        Assert.Equal(["Typed answer", "Zed"], board.WaitingOnSuggestions);

        board.SetCardWaitingOn(other, null);
        Assert.Equal(["Typed answer"], board.WaitingOnSuggestions);
    });

    [Fact]
    public void OnlyTheFiftyMostRecentAreKept() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Task");
        for (var i = 1; i <= 60; i++) board.SetCardWaitingOn(card, $"Answer {i}");
        board.SetCardWaitingOn(card, null);

        var suggestions = OpenBoard().WaitingOnSuggestions;
        Assert.Equal(MainViewModel.MaxWaitingOnHistory, suggestions.Count);
        Assert.Equal("Answer 60", suggestions[0]);
        Assert.Equal("Answer 11", suggestions[^1]);
    });

    [Fact]
    public void Typing_NarrowsTheList_AnswersThatStartWithItFirst()
    {
        Assert.Equal(Past, TextBoxSuggestions.Matches(Past, ""));
        Assert.Equal(["Sam's quote", "Sara's review", "Budget approval from Sam"], TextBoxSuggestions.Matches(Past, "sa"));
        Assert.Equal(["Sam's quote", "Budget approval from Sam"], TextBoxSuggestions.Matches(Past, " SAM "));
        Assert.Empty(TextBoxSuggestions.Matches(Past, "nothing like it"));
        Assert.Empty(TextBoxSuggestions.Matches(Past, "legal team")); // already typed in full: no list needed
    }

    [Fact]
    public void TheBestMatch_CompletesInline_ButOnlyFromTheStart()
    {
        Assert.Equal("Sam's quote", TextBoxSuggestions.Completion(Past, "sa"));
        Assert.Equal("Sara's review", TextBoxSuggestions.Completion(Past, "SAR"));
        Assert.Null(TextBoxSuggestions.Completion(Past, "quote"));       // in the middle: listed, not completed
        Assert.Null(TextBoxSuggestions.Completion(Past, "Legal team"));  // nothing left to add
        Assert.Null(TextBoxSuggestions.Completion(Past, ""));
        Assert.Null(TextBoxSuggestions.Completion(Past, " sa"));
    }

    [Fact]
    public void TheManagedList_IsAlphabetical_AndCountsTheTasksUsingEachAnswer() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "A", "Sam's quote");
        Add(board, "B", "sam's QUOTE");
        Add(board, "C", "Client sign-off");
        Assert.True(board.AddWaitingOnSuggestion("  Legal team "));
        Assert.False(board.AddWaitingOnSuggestion("legal TEAM")); // already there
        Assert.False(board.AddWaitingOnSuggestion("   "));

        var entries = board.WaitingOnEntries;

        Assert.Equal(["Client sign-off", "Legal team", "sam's QUOTE"], entries.Select(e => e.Text));
        Assert.Equal([1, 0, 2], entries.Select(e => e.TaskCount));
        Assert.Equal("Legal team", entries[1].Display);
        Assert.Equal("sam's QUOTE   (2 tasks)", entries[2].Display);
        Assert.Equal("Client sign-off   (1 task)", entries[0].Display);
    });

    [Fact]
    public void Renaming_RewordsTheListAndEveryTaskThatSaysIt_AsOneUndoStep_AndKeepsItsPlace() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var a = Add(board, "A", "Sams qoute");
        var b = Add(board, "B", "Sams qoute");
        var other = Add(board, "C", "Client sign-off"); // typed later, so it is suggested first

        Assert.Equal(2, board.RenameWaitingOnSuggestion("sams QOUTE", " Sam's quote "));

        Assert.Equal("Sam's quote", a.WaitingOn);
        Assert.Equal("Sam's quote", b.WaitingOn);
        Assert.Equal("Client sign-off", other.WaitingOn);
        Assert.Equal(["Client sign-off", "Sam's quote"], OpenBoard().WaitingOnSuggestions);
        Assert.Equal("Sam's quote", OpenBoard().Columns.SelectMany(c => c.Cards).First(c => c.Title == "A").WaitingOn);

        Assert.Equal("Reword waiting-on of 2 tasks", board.Undo());
        Assert.Equal("Sams qoute", a.WaitingOn);

        Assert.Equal(-1, board.RenameWaitingOnSuggestion("Client sign-off", "Client sign-off"));
        Assert.Equal(-1, board.RenameWaitingOnSuggestion("Client sign-off", "  "));
    });

    [Fact]
    public void RenamingOntoAnAnswerThatExists_MergesTheTwo() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "A", "Sam");
        Add(board, "B", "Sam's quote");

        board.RenameWaitingOnSuggestion("Sam", "Sam's quote");

        Assert.Equal(["Sam's quote"], board.WaitingOnSuggestions);
        Assert.Equal(2, board.WaitingOnEntries.Single().TaskCount);
    });

    [Fact]
    public void Deleting_CanLeaveTheTasksAlone_OrClearThemToo() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var a = Add(board, "A", "Sam's quote");
        var b = Add(board, "B", "Legal team");
        board.AddWaitingOnSuggestion("Unused");

        Assert.Equal(0, board.DeleteWaitingOnSuggestion("unused", clearFromTasks: false));
        Assert.DoesNotContain("Unused", board.WaitingOnSuggestions);

        Assert.Equal(0, board.DeleteWaitingOnSuggestion("Sam's quote", clearFromTasks: false));
        Assert.Equal("Sam's quote", a.WaitingOn);                  // the task keeps it...
        Assert.Contains("Sam's quote", board.WaitingOnSuggestions); // ...so it is still offered

        Assert.Equal(1, board.DeleteWaitingOnSuggestion("Legal team", clearFromTasks: true));
        Assert.Null(b.WaitingOn);
        Assert.DoesNotContain("Legal team", OpenBoard().WaitingOnSuggestions);

        board.Undo(); // the task gets it back; the list entry comes back with it, since a task says it again
        Assert.Equal("Legal team", b.WaitingOn);
        Assert.Contains("Legal team", board.WaitingOnSuggestions);
    });
}
