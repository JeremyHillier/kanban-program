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
            "Link: example.com/report\r\n" +
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

// The "Mark as Done" entry at the top of the right-click menu: named after the Done column as the
// user sees it, with O as its access key because D is taken by Set Due Date and Delete.
public sealed class DoneMenuLabelTests
{
    private static ColumnViewModel Done(string displayName) =>
        new(new KanbanColumn { Name = "Done", DisplayName = displayName }, System.Windows.Media.Brushes.White);

    [Theory]
    [InlineData("Done", false, "Mark as D_one")]
    [InlineData("Done", true, "Mark All as D_one")]
    [InlineData("Complete", false, "Mark as C_omplete")]
    [InlineData("Filed", false, "Mark as Filed")]
    [InlineData("To_Do", false, "Mark as T_o__Do")]
    public void TheLabel_UsesTheColumnsNameAndUnderlinesTheFirstO(string displayName, bool all, string expected) =>
        Assert.Equal(expected, MainWindow.DoneLabel(Done(displayName), all));
}

// In a Windows menu every entry has its own underlined letter; where two share one, pressing it only
// moves between them. The menus are built in code that needs a live window, so this reads the letters
// out of the source of each menu.
public sealed class MenuAccessKeyTests
{
    private static string MenuSource()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "KanbanApp", "MainWindow.CardMenus.cs"));
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        var to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from, start);
        return source[from..to];
    }

    private static List<string> Letters(string method)
    {
        // "Email All ... Tasks" has two wordings, one for tasks with no project; only one is ever shown.
        var letters = System.Text.RegularExpressions.Regex.Matches(method, "\"[^\"\n]*?(?<!_)_([A-Za-z])[^\"\n]*\"")
            .Where(m => !m.Value.Contains("With No Project"))
            .Select(m => m.Groups[1].Value.ToUpperInvariant()).ToList();
        // Mark as Done takes the first O of the Done column's name.
        letters.AddRange(System.Text.RegularExpressions.Regex.Matches(method, @"DoneLabel\(").Select(_ => "O"));
        return letters;
    }

    [Theory]
    [InlineData("private void BuildCardMenu(", "private void BuildSelectionMenu(")]
    [InlineData("private void BuildSelectionMenu(", "private void CopyToClipboard(")]
    public void NoTwoEntriesOfAMenu_ShareAnUnderlinedLetter(string start, string end)
    {
        var letters = Letters(Between(MenuSource(), start, end));

        Assert.True(letters.Count >= 12, "The menu's underlined letters were not found.");
        var repeated = letters.GroupBy(l => l).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(repeated.Count == 0, "Underlined letters used twice in one menu: " + string.Join(", ", repeated));
    }
}
