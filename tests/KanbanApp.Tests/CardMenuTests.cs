using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The board's right-click "Copy as Text".
public sealed class CardTextFormatterTests
{
    private static CardViewModel Card(Action<CardItem>? configure = null)
    {
        var model = new CardItem { Title = "Quarterly report", Priority = "High" };
        configure?.Invoke(model);
        return new CardViewModel(model) { ProjectName = "Finance", GoalName = "No Goal" };
    }

    [Fact]
    public void IncludesEveryFieldThatHasAValue()
    {
        var card = Card(m =>
        {
            m.DueDate = new DateTime(2026, 9, 14);
            m.DueTime = "14:30";
            m.IsRecurring = true;
            m.RecurrencePattern = "Semi-Monthly";
            m.WebsiteUrl = " example.com/report ";
            m.Notes = "Check figures\nwith Sam";
            m.CompletedAt = new DateTime(2026, 9, 15, 15, 25, 0);
        });
        card.People = [new PersonViewModel(new Person { Id = 7, Name = "Sam Lee" })];
        card.GoalName = "Close the books";
        card.Flags = [new FlagViewModel(new Flag { Name = "Urgent" }), new FlagViewModel(new Flag { Name = "Client" })];
        card.SubTasks = [new SubTaskViewModel(new SubTaskItem { Title = "Draft", IsDone = true }), new SubTaskViewModel(new SubTaskItem { Title = "Review" })];

        var text = CardTextFormatter.Format(card, "Finished");

        Assert.Equal(
            "Quarterly report\r\n\r\n" +
            "Status: Finished\r\n" +
            "Project: Finance\r\n" +
            "Priority: High\r\n" +
            "Due: 14-Sep-2026 2:30 PM\r\n" +
            "Assigned to: Sam Lee\r\n" +
            "Goal: Close the books\r\n" +
            "Flags: Urgent, Client\r\n" +
            "Repeats: Semi-Monthly\r\n" +
            "Completed Sep 15, 2026, 3:25 PM\r\n" +
            "Website: example.com/report\r\n" +
            "\r\nNotes:\r\nCheck figures\r\nwith Sam\r\n" +
            "\r\nSub-tasks:\r\n[x] Draft\r\n[ ] Review",
            text);
    }

    [Fact]
    public void ABareTaskCopiesAsJustItsTitleAndCoreFields()
    {
        var text = CardTextFormatter.Format(Card(), "To Do");

        Assert.Equal("Quarterly report\r\n\r\nStatus: To Do\r\nProject: Finance\r\nPriority: High", text);
    }

    [Fact]
    public void LeavesOutARecurrencePatternOnATaskThatNoLongerRepeats()
    {
        var text = CardTextFormatter.Format(Card(m => m.RecurrencePattern = "Weekly"), "To Do");
        Assert.DoesNotContain("Repeats", text);
    }
}

// The board's right-click "Duplicate".
[Collection(WpfCollection.Name)]
public sealed class DuplicateCardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    [Fact]
    public void CopiesEveryFieldIntoTheSameColumn_AndIsSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Sam Lee");
        board.AddGoal("Close the books");
        board.AddFlag("Urgent");
        var who = board.People.Single(p => p.Name == "Sam Lee");
        var goal = board.Goals.Single(g => g.Name == "Close the books");
        var flag = board.Flags.Single(f => f.Name == "Urgent");
        var subTasks = new List<SubTaskViewModel>
        {
            new(new SubTaskItem { Title = "Draft", IsDone = true }),
            new(new SubTaskItem { Title = "Review" })
        };
        var inProgress = Column(board, "In Progress");
        var original = board.AddCard("Quarterly report", inProgress, board.Projects.First(), "High", new DateTime(2026, 9, 14), who,
            true, "Monthly", goal, [flag], subTasks, "Check figures", forceEditOnComplete: true, websiteUrl: "example.com", dueTime: "14:30");

        var copy = board.DuplicateCard(original);

        Assert.NotNull(copy);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Contains(copy, inProgress.Cards);
        Assert.Equal("Quarterly report (copy)", copy.Title);
        Assert.Equal(original.ProjectId, copy.ProjectId);
        Assert.Equal("High", copy.Priority);
        Assert.Equal(new DateTime(2026, 9, 14), copy.DueDate);
        Assert.Equal("14:30", copy.DueTime);
        Assert.Equal(who.Id, copy.WhoId);
        Assert.Equal(goal.Id, copy.GoalId);
        Assert.True(copy.IsRecurring);
        Assert.Equal("Monthly", copy.RecurrencePattern);
        Assert.Equal("Check figures", copy.Notes);
        Assert.True(copy.ForceEditOnComplete);
        Assert.Equal("example.com", copy.WebsiteUrl);
        Assert.Equal([flag.Id], copy.Flags.Select(f => f.Id));

        // Sub-tasks come across as new, unticked work; the original keeps its ticks.
        Assert.Equal(["Draft", "Review"], copy.SubTasks.Select(s => s.Title));
        Assert.All(copy.SubTasks, s => Assert.False(s.IsDone));
        Assert.True(original.SubTasks[0].IsDone);

        var reopened = Column(OpenBoard(), "In Progress").Cards.Single(c => c.Id == copy.Id);
        Assert.Equal("Quarterly report (copy)", reopened.Title);
        Assert.Equal(2, reopened.SubTasks.Count);
        Assert.Single(reopened.Flags);
    });

    [Fact]
    public void DoesNotShareTheOriginalsAttachedFiles() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var attachment = new AttachmentViewModel(new CardAttachment { FilePath = "somewhere.txt", DisplayName = "somewhere.txt", AddedDate = DateTime.Now });
        var original = board.AddCard("With a file", Column(board, "To Do"), board.Projects.First(), "Normal", null, null,
            false, null, null, attachments: [attachment]);

        var copy = board.DuplicateCard(original)!;

        Assert.Single(original.Attachments);
        Assert.Empty(copy.Attachments);
    });

    [Fact]
    public void ACopyMadeInDoneIsStampedCompleted_AndStartsItsOwnRecurrence() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var original = board.AddCard("Pay rent", Column(board, "Done"), board.Projects.First(), "Normal", new DateTime(2026, 9, 1), null,
            true, "Monthly", null);
        original.NextOccurrenceSpawned = true;

        var copy = board.DuplicateCard(original)!;

        Assert.NotNull(copy.CompletedAt);
        Assert.False(copy.NextOccurrenceSpawned);
    });

    [Fact]
    public void ACardThatIsNoLongerOnTheBoardIsNotDuplicated() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Gone", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);
        board.DeleteCard(card);
        var countBefore = board.Columns.Sum(c => c.Cards.Count);

        Assert.Null(board.DuplicateCard(card));
        Assert.Equal(countBefore, board.Columns.Sum(c => c.Cards.Count));
    });
}
