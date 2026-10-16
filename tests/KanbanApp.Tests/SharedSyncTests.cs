using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Tasks passed between two boards by email: the column is the base column whatever it is called, a
// returned task updates the original, a task changed on both sides is flagged, and the sender is
// remembered so a task can be sent back (and told about when it is finished).
[Collection(WpfCollection.Name)]
public sealed class SharedSyncTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();
    private int _files;
    private int _pairs;

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string name, string? userName = null, string? userEmail = null)
    {
        var board = new MainViewModel(new DatabaseService(_temp.File(name)));
        if (userName is not null) board.SetUserName(userName);
        if (userEmail is not null) board.SetUserEmail(userEmail);
        return board;
    }

    private static ColumnViewModel Column(MainViewModel board, string baseName) => board.Columns.Single(c => c.Name == baseName);

    private static List<CardViewModel> All(MainViewModel board) => board.Columns.SelectMany(c => c.Cards).ToList();

    // What happens when a task is emailed: the file is made and the email is recorded.
    private List<ImportedTaskRow> Send(CardViewModel card, MainViewModel from)
    {
        var path = _temp.File($"task{++_files}.xlsx");
        ImportService.SaveSingleTaskFile(path, OutlookEmailHelper.BuildImportRow(card, from));
        from.RecordCardEmailed(card, "them@example.com", "in Outlook");
        return ImportService.ReadTasks(path);
    }

    private static CardViewModel Add(MainViewModel board, string title, string? pattern = null, DateTime? due = null) =>
        board.AddCard(title, Column(board, "To Do"), board.Projects.First(), "Normal", due ?? new DateTime(2030, 3, 1), null, pattern is not null, pattern, null);

    private static void Edit(MainViewModel board, CardViewModel card, string title, string? column = null) =>
        board.EditCard(card, title, column is null ? Column(board, board.Columns.First(c => c.Cards.Contains(card)).Name) : Column(board, column),
            board.Projects.First(), card.Priority, card.DueDate, [], card.IsRecurring, card.RecurrencePattern, null, [], [], card.Notes, [],
            false, null, card.DueTime, null, null, card.RecurrencesLeft);

    // Alice sends a task to Bob, who has imported it.
    private (MainViewModel Alice, MainViewModel Bob, CardViewModel Original, CardViewModel Copy) Shared(string title = "Draft the plan", string? pattern = null)
    {
        var pair = ++_pairs;
        var alice = OpenBoard($"alice{pair}.db", "Alice Lee", "alice@example.com");
        var bob = OpenBoard($"bob{pair}.db", "Bob Roy", "bob@example.com");
        var original = Add(alice, title, pattern);
        var copy = Assert.Single(bob.ImportCards(Send(original, alice)));
        return (alice, bob, original, copy);
    }

    [Fact]
    public void TheColumn_IsTheBaseColumn_WhateverItIsCalled() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var bob = OpenBoard("bob.db");
        bob.UseColumnNames(MainViewModel.GtdColumnNames);       // Inbox, Next Actions, In Progress (is On Hold!), Waiting, Done
        var original = Add(alice, "Order chairs");
        var copy = Assert.Single(bob.ImportCards(Send(original, alice)));
        Assert.Contains(copy, Column(bob, "To Do").Cards);

        // Bob moves it to the column he calls Next Actions - the base column In Progress.
        bob.MoveCardCommand.Execute((copy, Column(bob, "In Progress")));
        var back = Send(copy, bob);
        Assert.Equal("In Progress", Assert.Single(back).Category);       // not "Next Actions"
        alice.ImportCards(back);

        Assert.Contains(original, Column(alice, "In Progress").Cards);

        // And a task bob puts in the column he calls "In Progress" (really On Hold) arrives On Hold.
        bob.MoveCardCommand.Execute((copy, Column(bob, "On Hold")));
        alice.ImportCards(Send(copy, bob));
        Assert.Contains(original, Column(alice, "On Hold").Cards);
    });

    [Fact]
    public void AFileFromAnOlderCopy_StillFindsItsColumnByTheNameItWasGiven() => wpf.Run(() =>
    {
        var bob = OpenBoard("bob.db");
        bob.UseColumnNames(MainViewModel.GtdColumnNames);

        var cards = bob.ImportCards([new ImportedTaskRow { Title = "a", Category = "Next Actions" }, new ImportedTaskRow { Title = "b", Category = "Done" }]);

        Assert.Contains(cards[0], Column(bob, "In Progress").Cards);
        Assert.Contains(cards[1], Column(bob, "Done").Cards);
    });

    [Fact]
    public void WhoSentATask_IsRemembered_OnlyOnTheBoardItArrivedOn() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared();

        var sender = bob.SenderOf(copy);
        Assert.NotNull(sender);
        Assert.Equal("Alice Lee", sender.SenderLabel);
        Assert.Equal("alice@example.com", sender.SenderEmail);
        Assert.Null(alice.SenderOf(original));

        // It comes back; Alice's own task still has no sender.
        Edit(bob, copy, "Draft the plan v2");
        alice.ImportCards(Send(copy, bob));
        Assert.Null(alice.SenderOf(original));
        Assert.Equal("8", new DatabaseService(_temp.File($"bob{_pairs}.db")).GetSetting("FileFormat"));   // the table is in use
    });

    [Fact]
    public void ATaskChangedOnlyByTheOtherSide_UpdatesWithoutAskingAnything() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared();
        bob.MoveCardCommand.Execute((copy, Column(bob, "In Progress")));
        Edit(bob, copy, "Draft the plan - started");

        alice.ImportCards(Send(copy, bob), _ => throw new Xunit.Sdk.XunitException("nothing clashed"));

        Assert.Equal("Draft the plan - started", original.Title);
        Assert.Contains(original, Column(alice, "In Progress").Cards);
        Assert.Equal(1, alice.LastImport.Updated);
    });

    [Fact]
    public void ATaskChangedOnBothSides_IsFlagged_AndEachChoiceDoesWhatItSays() => wpf.Run(() =>
    {
        foreach (var choice in new[] { ConflictChoice.UseTheirs, ConflictChoice.KeepMine, ConflictChoice.KeepBoth })
        {
            var (alice, bob, original, copy) = Shared($"Task {choice}");
            Edit(alice, original, "Alice's wording");
            Edit(bob, copy, "Bob's wording");
            var returned = Send(copy, bob);

            ImportConflict? asked = null;
            alice.ImportCards(returned, conflict => { asked = conflict; return choice; });

            Assert.NotNull(asked);
            Assert.Same(original, asked.Card);
            Assert.Contains(asked.Differences, d => d.Name == "Task" && d.Mine == "Alice's wording" && d.Theirs == "Bob's wording");
            Assert.Contains("Bob Roy's copy", asked.Describe());

            switch (choice)
            {
                case ConflictChoice.UseTheirs:
                    Assert.Equal("Bob's wording", original.Title);
                    Assert.Single(All(alice));
                    break;
                case ConflictChoice.KeepMine:
                    Assert.Equal("Alice's wording", original.Title);
                    Assert.Single(All(alice));
                    Assert.Equal(1, alice.LastImport.KeptMine);
                    break;
                default:
                    Assert.Equal("Alice's wording", original.Title);
                    var other = Assert.Single(All(alice), c => c != original);
                    Assert.Equal("Bob's wording (theirs)", other.Title);
                    Assert.NotEqual(original.ShareId, other.ShareId);  // a task of its own, not linked to the original
                    break;
            }
        }
    });

    [Fact]
    public void AnUnchangedCopyComingBack_NeverOverwritesWhatWasEditedSince() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared();
        Edit(alice, original, "Alice kept working on it");

        var changed = alice.ImportCards(Send(copy, bob), _ => throw new Xunit.Sdk.XunitException("nothing clashed"));

        Assert.Empty(changed);
        Assert.Equal("Alice kept working on it", original.Title);
        Assert.Equal(1, alice.LastImport.Unchanged);
    });

    [Fact]
    public void ImportingTheSameFileTwice_ChangesNothingTheSecondTime() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared();
        Edit(bob, copy, "Changed by Bob");
        var returned = Send(copy, bob);

        Assert.Single(alice.ImportCards(returned));
        Assert.Empty(alice.ImportCards(returned));
        Assert.Equal(1, alice.LastImport.Unchanged);
    });

    [Fact]
    public void FinishingASharedTask_ComesBackAsDone_WithItsCompletionTime() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared();
        bob.MoveCardCommand.Execute((copy, Column(bob, "Done")));

        alice.ImportCards(Send(copy, bob));

        Assert.Contains(original, Column(alice, "Done").Cards);
        Assert.NotNull(original.CompletedAt);
    });

    [Fact]
    public void FinishingARepeatingSharedTask_StillMakesTheNextOne() => wpf.Run(() =>
    {
        var (alice, bob, original, copy) = Shared("Pay the rent", pattern: "Weekly");
        bob.MoveCardCommand.Execute((copy, Column(bob, "Done")));

        alice.ImportCards(Send(copy, bob));

        Assert.Contains(original, Column(alice, "Done").Cards);
        var next = Assert.Single(Column(alice, "To Do").Cards, c => c.Title == "Pay the rent");
        Assert.Equal(new DateTime(2030, 3, 8), next.DueDate);
        Assert.NotEqual(original.ShareId, next.ShareId);             // the next one is a new task
    });

    [Fact]
    public void TheSendersToTell_AreGrouped_AndOnlyThoseWithAnAddress() => wpf.Run(() =>
    {
        var pair = ++_pairs;
        var bob = OpenBoard($"bob{pair}.db", "Bob Roy", "bob@example.com");
        var alice = OpenBoard($"alice{pair}.db", "Alice Lee", "alice@example.com");
        var carol = OpenBoard("carol.db", "Carol", null);              // no email in Your Details
        var fromAlice1 = bob.ImportCards(Send(Add(alice, "A1"), alice)).Single();
        var fromAlice2 = bob.ImportCards(Send(Add(alice, "A2"), alice)).Single();
        var fromCarol = bob.ImportCards(Send(Add(carol, "C1"), carol)).Single();
        var own = Add(bob, "Mine");

        var groups = bob.SendersToTell([fromAlice1, fromAlice2, fromCarol, own]);

        var group = Assert.Single(groups);
        Assert.Equal("Alice Lee", group.Sender.SenderLabel);
        Assert.Equal([fromAlice1, fromAlice2], group.Cards);
        Assert.Equal("Carol", bob.SenderOf(fromCarol)!.SenderLabel);   // known, but nothing to send to
    });

    [Fact]
    public void TheOfferToTellTheSender_CanBeSwitchedOff_AndIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        Assert.True(board.NotifySenderOnDone);
        board.SetNotifySenderOnDone(false);
        Assert.False(OpenBoard("board.db").NotifySenderOnDone);
    });

    [Fact]
    public void TheEmail_SaysWhereTheTaskStands() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var card = Add(board, "Draft the plan");

        Assert.Contains("Status: To Do\r\n", OutlookEmailHelper.BuildPlainTextBody(card, null, "To Do"));
        Assert.DoesNotContain("Status:", OutlookEmailHelper.BuildPlainTextBody(card, null));
    });
}

public class ShareSyncRuleTests
{
    private static ImportedTaskRow Row(string title = "Plan", string? notes = null) => new()
    {
        Title = title, Category = "To Do", Priority = "Normal", DueDate = new DateTime(2030, 3, 1), Notes = notes, Flags = "B; A"
    };

    [Fact]
    public void TheFingerprint_IgnoresWhoSentItAndTheWayItIsWritten()
    {
        var a = Row("Plan", "line1\r\nline2");
        var b = new ImportedTaskRow
        {
            Title = " Plan ", Category = "To Do", Priority = "Normal ", DueDate = new DateTime(2030, 3, 1), Notes = "line1\nline2", Flags = "A;B",
            SenderName = "Someone", SenderEmail = "x@y.com", ShareId = "abc"
        };
        Assert.Equal(ShareSync.Fingerprint(a), ShareSync.Fingerprint(b));
    }

    [Fact]
    public void TheFingerprint_ChangesWithAnythingThatMatters()
    {
        var plan = ShareSync.Fingerprint(Row());
        Assert.NotEqual(plan, ShareSync.Fingerprint(Row("Plan 2")));
        Assert.NotEqual(plan, ShareSync.Fingerprint(Row(notes: "x")));
        Assert.NotEqual(plan, ShareSync.Fingerprint(new ImportedTaskRow { Title = "Plan", Category = "Done", Priority = "Normal", DueDate = new DateTime(2030, 3, 1), Flags = "B; A" }));
    }

    [Fact]
    public void TheDifferences_NameWhatChanged()
    {
        var theirs = new ImportedTaskRow
        {
            Title = "Plan", Category = "In Progress", Priority = "High", DueDate = new DateTime(2030, 3, 5), Flags = "B; A",
            SubTasks = [("Outline", true)]
        };

        var names = ShareSync.Differences(Row(), theirs).Select(d => d.Name).ToList();

        Assert.Equal(["Status", "Priority", "Due date", "Sub-tasks"], names);
    }

    [Fact]
    public void TheCategory_IsComparedByTheBoardsOwnNaming()
    {
        string? Base(string? c) => c == "Next Actions" ? "In Progress" : c;
        var mine = new ImportedTaskRow { Title = "Plan", Category = "In Progress" };
        var theirs = new ImportedTaskRow { Title = "Plan", Category = "Next Actions" };

        Assert.Equal(ShareSync.Fingerprint(mine, Base), ShareSync.Fingerprint(theirs, Base));
        Assert.NotEqual(ShareSync.Fingerprint(mine), ShareSync.Fingerprint(theirs));
    }

    [Theory]
    [InlineData("alice@example.com", "alice@example.com")]
    [InlineData("  sam.lee@mail.example.co.uk ", "sam.lee@mail.example.co.uk")]
    [InlineData("alice@example", null)]
    [InlineData("alice example.com", null)]
    [InlineData("a@b.com; c@d.com", null)]
    [InlineData("\"Alice\" <a@b.com>", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void OnlyOnePlainAddress_IsUsedToReply(string? typed, string? expected) => Assert.Equal(expected, ImportService.NormalizeEmail(typed));

    [Fact]
    public void TheSenderTravelsInTheExcelFile()
    {
        using var temp = new TempFolder();
        var path = temp.File("one.xlsx");
        ImportService.SaveSingleTaskFile(path, new ImportedTaskRow { Title = "Plan", SenderName = "Alice Lee", SenderEmail = "alice@example.com" });

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("Alice Lee", row.SenderName);
        Assert.Equal("alice@example.com", row.SenderEmail);
    }
}
