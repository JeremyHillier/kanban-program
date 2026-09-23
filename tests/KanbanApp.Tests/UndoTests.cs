using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Undo (Ctrl+Z): each test does something, undoes it, and checks both the board on screen and a
// freshly reopened board (what was saved).
[Collection(WpfCollection.Name)]
public sealed class UndoTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static List<CardViewModel> AddMany(MainViewModel board, string column, params string[] titles) =>
        titles.Select(t => board.AddCard(t, Column(board, column), board.Projects.First(), "Normal", null, null, false, null, null)).ToList();

    private static string Titles(ColumnViewModel column) => string.Join(",", column.Cards.Select(c => c.Title));

    private static void Move(MainViewModel board, CardViewModel card, string column) =>
        board.MoveCardCommand.Execute((card, Column(board, column)));


    [Fact]
    public void NothingToUndo_OnAFreshBoard() => wpf.Run(() =>
    {
        var board = OpenBoard();

        Assert.False(board.CanUndo);
        Assert.Null(board.Undo());
        Assert.Equal("Nothing to undo (Ctrl+Z)", board.UndoToolTip);
    });

    [Fact]
    public void Move_IsUndone_AndTheSameCardObjectStaysOnTheBoard() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddMany(board, "To Do", "Write report").Single();
        var before = card.LastUpdated;

        Move(board, card, "Done");
        Assert.NotNull(card.CompletedAt);
        Assert.Equal("Undo: Move \"Write report\" to Done (Ctrl+Z)", board.UndoToolTip);

        Assert.Equal("Move \"Write report\" to Done", board.Undo());

        Assert.Same(card, Assert.Single(Column(board, "To Do").Cards));
        Assert.Empty(Column(board, "Done").Cards);
        Assert.Null(card.CompletedAt);
        Assert.Equal(before, card.LastUpdated);

        var saved = Assert.Single(Column(OpenBoard(), "To Do").Cards);
        Assert.Null(saved.CompletedAt);
    });

    [Fact]
    public void CompletingARecurringTask_IsUndone_IncludingTheNextOccurrence() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var todo = Column(board, "To Do");
        var weekly = board.AddCard("Weekly", todo, board.Projects.First(), "Normal", new DateTime(2026, 9, 21), null, true, "Weekly", null);

        Move(board, weekly, "Done");
        Assert.Equal(new DateTime(2026, 9, 28), Assert.Single(todo.Cards).DueDate);

        board.Undo();

        Assert.Same(weekly, Assert.Single(todo.Cards));
        Assert.False(weekly.NextOccurrenceSpawned);
        Assert.Empty(board.GetDeletedCards()); // the next occurrence is gone, not binned

        // Completing it again makes exactly one next occurrence, as if the first time never happened.
        Move(board, weekly, "Done");
        var reopened = OpenBoard();
        Assert.Equal(new DateTime(2026, 9, 28), Assert.Single(Column(reopened, "To Do").Cards).DueDate);
        Assert.Single(Column(reopened, "Done").Cards);
    });

    [Fact]
    public void QuickEdits_AreUndoneOneAtATime_NewestFirst() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Sam Lee");
        board.AddProject("Website");
        board.AddFlag("Urgent");
        var card = AddMany(board, "To Do", "A").Single();
        var originalProject = card.ProjectName;

        board.SetCardPriority(card, "High");
        board.SetCardWho(card, board.People.Single());
        board.SetCardProject(card, board.Projects.Single(p => p.Name == "Website"));
        board.SetCardDueDate(card, new DateTime(2026, 10, 1));
        board.AddFlagToCard(card, board.Flags.Single());

        board.Undo();
        Assert.Empty(card.Flags);
        Assert.Equal(new DateTime(2026, 10, 1), card.DueDate);
        board.Undo();
        Assert.Null(card.DueDate);
        board.Undo();
        Assert.Equal(originalProject, card.ProjectName);
        board.Undo();
        Assert.Null(card.WhoId);
        Assert.Equal("Unassigned", card.WhoName);
        Assert.Equal("High", card.Priority);
        board.Undo();
        Assert.Equal("Normal", card.Priority);
        Assert.True(board.CanUndo); // only the add is left

        var saved = Assert.Single(Column(OpenBoard(), "To Do").Cards);
        Assert.Equal("Normal", saved.Priority);
        Assert.Null(saved.WhoId);
        Assert.Null(saved.DueDate);
        Assert.Empty(saved.Flags);
    });

    [Fact]
    public void Edit_IsUndone_IncludingNotesFlagsSubTasksAndColumn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddFlag("Urgent");
        var subTasks = new List<SubTaskViewModel> { new(new SubTaskItem { Title = "Draft" }), new(new SubTaskItem { Title = "Send" }) };
        var card = board.AddCard("Report", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null,
            subTasks: subTasks, notes: "first notes");

        board.EditCard(card, "Report v2", Column(board, "Done"), board.Projects.First(), "High", new DateTime(2026, 10, 1), null,
            false, null, null, [board.Flags.Single()], [new SubTaskViewModel(new SubTaskItem { Title = "Only one", IsDone = true })],
            "second notes", card.Attachments, false, "https://example.com", "09:30", new DateTime(2026, 9, 25), "the client", null);

        Assert.Equal("Edit \"Report\"", board.Undo());

        foreach (var c in new[] { card, Assert.Single(Column(OpenBoard(), "To Do").Cards) })
        {
            Assert.Equal("Report", c.Title);
            Assert.Equal("Normal", c.Priority);
            Assert.Null(c.DueDate);
            Assert.Null(c.DueTime);
            Assert.Null(c.StartDate);
            Assert.Null(c.WaitingOn);
            Assert.Null(c.WebsiteUrl);
            Assert.Equal("first notes", c.Notes);
            Assert.Empty(c.Flags);
            Assert.Equal(["Draft", "Send"], c.SubTasks.Select(s => s.Title));
            Assert.All(c.SubTasks, s => Assert.False(s.IsDone));
            Assert.Null(c.CompletedAt);
        }
        Assert.Same(card, Assert.Single(Column(board, "To Do").Cards));
    });

    [Fact]
    public void TickingASubTask_IsUndone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Report", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null,
            subTasks: [new SubTaskViewModel(new SubTaskItem { Title = "Draft" })]);

        board.SetSubTaskDone(card, card.SubTasks.Single(), true);
        board.Undo();

        Assert.False(card.SubTasks.Single().IsDone);
        Assert.False(Assert.Single(Column(OpenBoard(), "To Do").Cards).SubTasks.Single().IsDone);
    });

    [Fact]
    public void Delete_IsUndone_BackToTheSamePlace_InAHandArrangedColumn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C", "D");
        var todo = Column(board, "To Do");
        board.ReorderCardsWithinColumn([cards[3]], todo, 0); // D,A,B,C by hand
        Assert.Equal("D,A,B,C", Titles(todo));

        board.DeleteCard(cards[0]); // A
        Assert.Equal("D,B,C", Titles(todo));
        Assert.Single(board.GetDeletedCards());

        Assert.Equal("Delete \"A\"", board.Undo());

        Assert.Equal("D,A,B,C", Titles(todo));
        Assert.Empty(board.GetDeletedCards());
        Assert.Equal("D,A,B,C", Titles(Column(OpenBoard(), "To Do")));
    });

    [Fact]
    public void GroupActions_AreOneStepEach() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C");
        var todo = Column(board, "To Do");
        var done = Column(board, "Done");

        board.SetCardsPriority(cards, "High");
        board.MoveCards(cards, done);
        board.DeleteCards([cards[0], cards[1]]);

        Assert.Equal("Delete 2 tasks", board.Undo());
        Assert.Equal(3, done.Cards.Count);
        Assert.Equal("Move 3 tasks to Done", board.Undo());
        Assert.Equal(3, todo.Cards.Count);
        Assert.All(cards, c => Assert.Null(c.CompletedAt));
        Assert.Equal("Change priority of 3 tasks", board.Undo());
        Assert.All(cards, c => Assert.Equal("Normal", c.Priority));

        Assert.All(Column(OpenBoard(), "To Do").Cards, c => Assert.Equal("Normal", c.Priority));
        Assert.Equal(3, Column(OpenBoard(), "To Do").Cards.Count);
    });

    [Fact]
    public void Duplicate_IsUndone_AndTheCopyIsErasedNotBinned() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B");

        board.DuplicateCards(cards);
        Assert.Equal(4, Column(board, "To Do").Cards.Count);

        Assert.Equal("Duplicate 2 tasks", board.Undo());

        Assert.Equal(2, Column(board, "To Do").Cards.Count);
        Assert.Empty(board.GetDeletedCards());
        Assert.Equal(2, Column(OpenBoard(), "To Do").Cards.Count);
    });

    [Fact]
    public void AddingATask_IsUndone_AndOneWithAttachmentsGoesToTheDeletedList() => wpf.Run(() =>
    {
        var board = OpenBoard();
        AddMany(board, "To Do", "Plain");
        Assert.Equal("Add \"Plain\"", board.Undo());
        Assert.Empty(Column(board, "To Do").Cards);
        Assert.Empty(board.GetDeletedCards());

        var file = _temp.File("note.txt");
        System.IO.File.WriteAllText(file, "x");
        board.AddCard("With file", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null,
            attachments: [new AttachmentViewModel(new CardAttachment { FilePath = file, DisplayName = "note.txt", AddedDate = DateTime.Now })]);
        board.Undo();

        Assert.Empty(Column(board, "To Do").Cards);
        Assert.Equal("With file", Assert.Single(board.GetDeletedCards()).Title);
    });

    [Fact]
    public void ArchiveDone_IsUndone_AndCompletionTimesSurvive() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B");
        board.MoveCards(cards, Column(board, "Done"));
        var completed = cards.Select(c => c.CompletedAt).ToList();

        board.ArchiveDoneTasks();
        Assert.Empty(Column(board, "Done").Cards);
        Assert.Equal(2, board.GetArchivedCards().Count);

        Assert.Equal("Archive 2 tasks", board.Undo());

        Assert.Equal(2, Column(board, "Done").Cards.Count);
        Assert.Empty(board.GetArchivedCards());
        Assert.Equal(completed, Column(OpenBoard(), "Done").Cards.OrderBy(c => c.Title).Select(c => c.CompletedAt));
    });

    [Fact]
    public void DragReorder_IsUndone_AndTheSortButtonComesBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B", "C", "D");
        var todo = Column(board, "To Do");
        Assert.Equal(1, board.ProjectSortRank);

        board.ReorderCardsWithinColumn([cards[3]], todo, 0);
        Assert.True(board.IsManualSort);
        board.ReorderCardsWithinColumn([cards[0], cards[1]], todo, 4); // D,C,A,B
        Assert.Equal("D,C,A,B", Titles(todo));

        Assert.Equal("Reorder 2 tasks", board.Undo());
        Assert.Equal("D,A,B,C", Titles(todo));
        Assert.True(board.IsManualSort);
        Assert.Equal("D,A,B,C", Titles(Column(OpenBoard(), "To Do")));

        board.Undo();
        Assert.Equal("A,B,C,D", Titles(todo));
        Assert.False(board.IsManualSort);
        Assert.Equal(1, board.ProjectSortRank);
    });

    [Fact]
    public void Import_IsUndoneAsOneStep() => wpf.Run(() =>
    {
        var board = OpenBoard();
        AddMany(board, "To Do", "Existing");

        board.ImportCards([new ImportedTaskRow { Title = "One" }, new ImportedTaskRow { Title = "Two" }, new ImportedTaskRow { Title = " " }]);
        Assert.Equal(3, Column(board, "To Do").Cards.Count);

        Assert.Equal("Import 2 tasks", board.Undo());

        Assert.Equal("Existing", Titles(Column(board, "To Do")));
        Assert.Equal("Existing", Titles(Column(OpenBoard(), "To Do")));
    });

    [Fact]
    public void AProjectDeletedSinceTheAction_ComesBackAsNoProject() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddProject("Old");
        board.AddProject("New");
        var old = board.Projects.Single(p => p.Name == "Old");
        var card = board.AddCard("A", Column(board, "To Do"), old, "Normal", null, null, false, null, null);

        board.SetCardProject(card, board.Projects.Single(p => p.Name == "New"));
        board.DeleteProject(old); // nothing uses it any more

        board.Undo();

        Assert.Null(card.ProjectId);
        Assert.Null(Assert.Single(Column(OpenBoard(), "To Do").Cards).ProjectId);
    });

    [Fact]
    public void AnUndoThatNoLongerMatchesTheFilters_HidesTheCard() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddMany(board, "To Do", "A").Single();
        board.SetCardPriority(card, "High");
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();
        Assert.True(card.IsVisible);

        board.Undo(); // back to Normal, which the filter hides

        Assert.False(card.IsVisible);
    });

    [Fact]
    public void OnlyTheLastThirtyActionsAreKept() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddMany(board, "To Do", "A").Single();
        for (var i = 0; i < 40; i++) board.SetCardPriority(card, i % 2 == 0 ? "High" : "Low");

        var undone = 0;
        while (board.Undo() is not null) undone++;

        Assert.Equal(30, undone);
        Assert.Single(Column(board, "To Do").Cards); // the add itself was pushed off the end
    });

    [Fact]
    public void UndoingLeavesANoteInTheTaskHistory_AndClearsTheSelection() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var cards = AddMany(board, "To Do", "A", "B");
        board.ToggleCardSelection(cards[0]);
        board.SetCardPriority(cards[1], "High");

        board.Undo();

        Assert.Equal(0, board.SelectedCardCount);
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_temp.File("board.db")}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Details FROM CardHistory WHERE EventType = 'Undone';";
        Assert.Equal("Undid: Change priority of \"B\"", cmd.ExecuteScalar());
    });
}
