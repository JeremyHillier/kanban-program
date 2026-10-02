using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A task emailed from the app keeps one identity: importing it again - the same email twice, or
// the task sent back with changes - updates the copy already on the board instead of adding another.
[Collection(WpfCollection.Name)]
public sealed class ShareIdTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();
    private int _files;

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string name) => new(new DatabaseService(_temp.File(name)));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static List<CardViewModel> All(MainViewModel board) => board.Columns.SelectMany(c => c.Cards).ToList();

    // What Email This Task attaches, read back the way Import Tasks reads it.
    private List<ImportedTaskRow> Email(CardViewModel card, MainViewModel from)
    {
        var path = _temp.File($"task{++_files}.xlsx");
        ImportService.SaveSingleTaskFile(path, OutlookEmailHelper.BuildImportRow(card, from));
        return ImportService.ReadTasks(path);
    }

    private static CardViewModel Add(MainViewModel board, string title) =>
        board.AddCard(title, Column(board, "To Do"), board.Projects.First(), "Normal", new DateTime(2026, 11, 1), null, false, null, null);

    [Fact]
    public void ATaskSentBackWithChanges_UpdatesTheSendersCopy() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var bob = OpenBoard("bob.db");
        var original = Add(alice, "Draft the proposal");

        // Alice sends it; Bob imports it, works on it, and sends it back.
        var bobsCopy = Assert.Single(bob.ImportCards(Email(original, alice)));
        Assert.Equal(original.ShareId, bobsCopy.ShareId);
        bob.EditCard(bobsCopy, "Draft the proposal (v2)", Column(bob, "In Progress"), bobsCopy.ProjectId is null ? null : bob.Projects.First(),
            "High", new DateTime(2026, 11, 8), [], false, null, null, [], [new SubTaskViewModel(new SubTaskItem { Title = "Outline", IsDone = true })],
            "First draft done", [], false, null, null, null, null, null);

        var back = Email(bobsCopy, bob);
        var updated = Assert.Single(alice.ImportCards(back));

        Assert.Same(original, updated);                          // the same task, not a new one
        Assert.Single(All(alice));
        Assert.Equal("Draft the proposal (v2)", original.Title);
        Assert.Equal("High", original.Priority);
        Assert.Equal(new DateTime(2026, 11, 8), original.DueDate);
        Assert.Equal("First draft done", original.Notes);
        Assert.Equal([("Outline", true)], original.SubTasks.Select(s => (s.Title, s.IsDone)));
        Assert.Contains(original, Column(alice, "In Progress").Cards);
        Assert.True(original.IsImported);                        // listed on the review screen
    });

    [Fact]
    public void ImportingTheSameEmailTwice_GivesOneTask() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var bob = OpenBoard("bob.db");
        var rows = Email(Add(alice, "Book the venue"), alice);

        bob.ImportCards(rows);
        bob.ImportCards(rows);

        Assert.Single(All(bob));
    });

    [Fact]
    public void ATaskKeepsItsID_EverySending_AndOnlyWhenShared() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var card = Add(board, "a");
        Assert.Null(card.ShareId);
        Assert.Equal("3", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat")); // nothing shared yet

        var first = Email(card, board).Single().ShareId;
        var second = Email(card, board).Single().ShareId;

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(first, All(OpenBoard("board.db")).Single().ShareId);                           // saved
        Assert.Equal("5", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));  // older copies warned from now on
    });

    [Fact]
    public void ADuplicate_AndTheNextOccurrence_AreNewTasks_WithoutTheID() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var card = board.AddCard("Weekly report", Column(board, "To Do"), board.Projects.First(), "Normal", new DateTime(2026, 11, 2), null, true, "Weekly", null);
        board.EnsureShareId(card);

        var copy = board.DuplicateCard(card)!;
        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        var next = Column(board, "To Do").Cards.Single(c => c.Title == "Weekly report" && c != copy);

        Assert.Null(copy.ShareId);
        Assert.Null(next.ShareId);
    });

    [Fact]
    public void ATaskAlreadyArchivedHere_ComesBackAsNew() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var bob = OpenBoard("bob.db");
        var original = Add(alice, "Pay invoice");
        var rows = Email(original, alice);
        var bobsCopy = bob.ImportCards(rows).Single();
        bob.MoveCardCommand.Execute((bobsCopy, Column(bob, "Done")));
        bob.ArchiveDoneTasks();

        var again = Assert.Single(bob.ImportCards(rows));

        Assert.NotSame(bobsCopy, again);
        Assert.Contains(again, Column(bob, "To Do").Cards);
    });

    [Fact]
    public void UndoingAnImportThatUpdated_PutsTheTaskBack() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var bob = OpenBoard("bob.db");
        var original = Add(alice, "Original title");
        var bobsCopy = bob.ImportCards(Email(original, alice)).Single();
        bob.EditCard(bobsCopy, "Changed by Bob", Column(bob, "To Do"), bob.Projects.First(), "Normal", bobsCopy.DueDate, [],
            false, null, null, [], [], null, [], false, null, null, null, null, null);
        var back = Email(bobsCopy, bob);

        alice.ImportCards(back);
        Assert.Equal("Changed by Bob", original.Title);

        alice.Undo();
        Assert.Equal("Original title", original.Title);
        Assert.Single(All(alice));
    });

    [Fact]
    public void TheQuestionBeforeImporting_SaysWhichAreUpdates()
    {
        var rows = new List<ImportedTaskRow> { new() { Title = "Mine", ShareId = "a" }, new() { Title = "New one" } };

        var text = ImportDrop.DescribeForConfirm([("task.xlsx", rows)], r => r.ShareId == "a");

        Assert.Contains("• Mine - updates your copy", text);
        Assert.Contains("• New one\n", text);
        Assert.Contains("1 of them is a task you already have, so it will be updated rather than added again.", text);
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", "0f8fad5bd9cb469fa16570867728950e")]
    [InlineData(" 0F8FAD5BD9CB469FA16570867728950E ", "0f8fad5bd9cb469fa16570867728950e")]
    [InlineData("00000000-0000-0000-0000-000000000000", null)]
    [InlineData("task 12", null)]
    [InlineData("", null)]
    public void OnlyARealTaskID_IsTaken(string typed, string? expected)
    {
        Assert.Equal(expected, ImportService.NormalizeShareId(typed));
    }
}
