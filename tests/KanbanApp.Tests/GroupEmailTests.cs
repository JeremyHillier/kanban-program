using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Emailing several tasks at once: a project's open tasks, or a selection, in one email with one
// Excel file that imports all of them.
[Collection(WpfCollection.Name)]
public sealed class GroupEmailTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string name) => new(new DatabaseService(_temp.File(name)));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    [Fact]
    public void AProjectsOpenTasks_AreItsTasksNotYetDone_InBoardOrder() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var office = board.AddProject("Office").Item!;
        var a = board.AddCard("Paint", Column(board, "In Progress"), office, "Normal", null, null, false, null, null);
        var b = board.AddCard("Quotes", Column(board, "To Do"), office, "Normal", null, null, false, null, null);
        board.AddCard("Old job", Column(board, "Done"), office, "Normal", null, null, false, null, null);
        board.AddCard("Elsewhere", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);

        Assert.Equal([b, a], board.OpenTasksInProject(office.Id));
    });

    [Fact]
    public void TheEmail_IsAddressedToEveryoneOnTheTasks_AndNamesTheProject() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var office = board.AddProject("Office").Item!;
        var sam = board.AddPerson("Sam").Item!;
        var priya = board.AddPerson("Priya").Item!;
        var nobody = board.AddPerson("No Email").Item!;
        board.SetPersonEmail(sam, "sam@example.com");
        board.SetPersonEmail(priya, "priya@example.com");

        var col = Column(board, "To Do");
        var cards = new List<CardViewModel>
        {
            board.AddCard("Paint", col, office, "High", new DateTime(2026, 10, 20), sam, false, null, null),
            board.AddCard("Quotes", col, office, "Normal", null, null, false, null, null, people: [priya, sam], waitingOn: "the landlord"),
            board.AddCard("Keys", col, office, "Low", null, nobody, false, null, null),
        };

        Assert.Equal("sam@example.com; priya@example.com", OutlookEmailHelper.GroupRecipients(cards));
        Assert.Equal("Tasks: Office (3)", OutlookEmailHelper.GroupSubject(cards));
        Assert.Equal("Tasks (2)", OutlookEmailHelper.GroupSubject([cards[0], board.AddCard("Other", col, board.Projects.First(), "Normal", null, null, false, null, null)]));

        var html = OutlookEmailHelper.BuildGroupHtmlBody(cards, board);
        Assert.Contains("<b>Paint</b>", html);
        Assert.Contains("20-Oct-2026", html);
        Assert.Contains("the landlord", html);
        Assert.Contains("Priya, Sam", html);

        var text = OutlookEmailHelper.BuildGroupPlainTextBody(cards, board, null);
        Assert.Contains("• Quotes - To Do, Normal, Priya, Sam, waiting on the landlord", text);
    });

    [Fact]
    public void TheFile_ImportsAsAllTheTasks_AndSendingThemBackUpdatesThem() => wpf.Run(() =>
    {
        var alice = OpenBoard("alice.db");
        var office = alice.AddProject("Office").Item!;
        var col = Column(alice, "To Do");
        alice.AddCard("Paint", col, office, "Normal", null, null, false, null, null);
        alice.AddCard("Quotes", col, office, "Normal", null, null, false, null, null, notes: "Three at least");
        var tasks = alice.OpenTasksInProject(office.Id);

        var file = _temp.File("group.xlsx");
        ImportService.SaveTasksFile(file, tasks.Select(t => OutlookEmailHelper.BuildImportRow(t, alice)).ToList());

        var bob = OpenBoard("bob.db");
        var received = bob.ImportCards(ImportService.ReadTasks(file));
        Assert.Equal(["Paint", "Quotes"], received.Select(c => c.Title));
        Assert.Equal("Three at least", received[1].Notes);

        // Bob finishes one and sends the lot back.
        bob.MoveCardCommand.Execute((received[0], Column(bob, "In Progress")));
        var back = _temp.File("back.xlsx");
        ImportService.SaveTasksFile(back, bob.OpenTasksInProject(received[0].ProjectId).Select(t => OutlookEmailHelper.BuildImportRow(t, bob)).ToList());
        alice.ImportCards(ImportService.ReadTasks(back));

        Assert.Equal(2, alice.Columns.Sum(c => c.Cards.Count));                   // updated, not added again
        Assert.Contains(alice.Columns.SelectMany(c => c.Cards), c => c.Title == "Paint" && Column(alice, "In Progress").Cards.Contains(c));
    });

    [Fact]
    public void EachTask_RemembersItWentWithOthers() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        var card = board.AddCard("Paint", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null);

        board.RecordCardEmailed(card, "sam@example.com", "in Outlook", groupSize: 3);
        board.RecordCardEmailed(card, "", "in Outlook", groupSize: 2);

        var history = board.GetEmailHistory(card);
        Assert.Equal("with 1 other task, in Outlook", history[0].Details);
        Assert.Equal("to sam@example.com, with 2 other tasks, in Outlook", history[1].Details);
        Assert.EndsWith("with 1 other task (2 times)", MainViewModel.EmailStampText(history));
    });
}
