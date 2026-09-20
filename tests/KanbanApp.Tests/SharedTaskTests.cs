using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// A task assigned to more than one person: the first is the lead; filters match any of them, the
// sort goes by the lead, email goes to all, reports list it under each, Excel uses semicolons.
[Collection(WpfCollection.Name)]
public sealed class SharedTaskTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static PersonViewModel Person(MainViewModel board, string name) => board.People.Single(p => p.Name == name);

    private static MainViewModel BoardWithPeople(Func<MainViewModel> open)
    {
        var board = open();
        board.AddPerson("Alice");
        board.AddPerson("Bob");
        board.AddPerson("Cara");
        board.SetPersonEmail(Person(board, "Alice"), "alice@example.com");
        board.SetPersonEmail(Person(board, "Cara"), "cara@example.com");
        return board;
    }

    private static CardViewModel Add(MainViewModel board, string title, params string[] people) =>
        board.AddCard(title, Column(board, "To Do"), board.Projects.First(), "Normal", null, null, false, null, null,
            people: people.Select(name => Person(board, name)).ToList());

    [Fact]
    public void TheFirstPersonIsTheLead_AndItAllSurvivesReopening() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = Add(board, "Shared", "Bob", "Alice");

        Assert.Equal(Person(board, "Bob").Id, card.WhoId);
        Assert.Equal("Bob", card.LeadName);
        Assert.Equal("Bob, Alice", card.WhoName);
        Assert.Equal("Assigned: Bob, Alice", card.WhoDisplay);

        var reopened = OpenBoard().Columns.SelectMany(c => c.Cards).Single();
        Assert.Equal(["Bob", "Alice"], reopened.People.Select(p => p.Name));
        Assert.Equal(card.WhoId, reopened.WhoId);
    });

    [Fact]
    public void ASinglePersonStillWorksTheOldWay_AndNobodyMeansUnassigned() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var one = board.AddCard("One", Column(board, "To Do"), board.Projects.First(), "Normal", null, Person(board, "Alice"), false, null, null);
        var none = Add(board, "None");

        Assert.Equal(["Alice"], one.People.Select(p => p.Name));
        Assert.Equal("Assigned: Alice", one.WhoDisplay);
        Assert.Null(none.WhoId);
        Assert.Equal("Unassigned", none.WhoName);
        Assert.Equal(string.Empty, none.WhoDisplay);
    });

    [Fact]
    public void TheWhoFilter_ShowsATaskWhenAnyOfItsPeopleIsPicked() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var shared = Add(board, "Shared", "Alice", "Bob");
        var caras = Add(board, "Cara's", "Cara");
        var nobodys = Add(board, "Nobody's");

        void Pick(params string[] names)
        {
            foreach (var option in board.WhoFilterOptions) option.IsSelected = names.Contains(option.Name);
            board.ApplyFilters();
        }

        Pick("Bob"); // not the lead
        Assert.True(shared.IsVisible);
        Assert.False(caras.IsVisible);
        Assert.False(nobodys.IsVisible);

        Pick("Cara", "Unassigned");
        Assert.False(shared.IsVisible);
        Assert.True(caras.IsVisible);
        Assert.True(nobodys.IsVisible);
    });

    [Fact]
    public void SortByWho_GoesByTheLead() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        Add(board, "Cara leads", "Cara", "Alice");
        Add(board, "Alice leads", "Alice", "Cara");
        Add(board, "Bob leads", "Bob");

        board.ToggleSortKey(MainViewModel.SortKey.Who, additive: false);

        Assert.Equal(["Alice leads", "Bob leads", "Cara leads"], Column(board, "To Do").Cards.Select(c => c.Title));
    });

    [Fact]
    public void EmailGoesToEveryoneWithAnAddress_LeadFirst() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = Add(board, "Shared", "Cara", "Bob", "Alice"); // Bob has no address

        Assert.True(card.CanEmailCard);
        Assert.Equal(["cara@example.com", "alice@example.com"], card.PeopleEmails);
        Assert.Equal("cara@example.com; alice@example.com", OutlookEmailHelper.JoinRecipients(card.PeopleEmails));
        Assert.StartsWith("mailto:cara@example.com,alice@example.com?subject=", OutlookEmailHelper.BuildMailtoUri("cara@example.com; alice@example.com", "Task", "Body"));

        Assert.False(Add(board, "Only Bob", "Bob").CanEmailCard);
    });

    [Fact]
    public void TickingPeopleOnAndOff_KeepsTheLead_UntilTheLeadIsTheOneTakenOff() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = Add(board, "Task", "Alice");

        board.ToggleCardPerson(card, Person(board, "Bob"));
        Assert.Equal(["Alice", "Bob"], card.People.Select(p => p.Name));

        board.ToggleCardPerson(card, Person(board, "Alice"));
        Assert.Equal(["Bob"], card.People.Select(p => p.Name));
        Assert.Equal(Person(board, "Bob").Id, OpenBoard().Columns.SelectMany(c => c.Cards).Single().WhoId);

        Assert.Equal("Reassign \"Task\"", board.Undo());
        Assert.Equal(["Alice", "Bob"], card.People.Select(p => p.Name));
        Assert.Equal(["Alice", "Bob"], OpenBoard().Columns.SelectMany(c => c.Cards).Single().People.Select(p => p.Name));
    });

    [Fact]
    public void GroupActions_AddAndTakeOffAPerson_AndAssignToReplacesEveryone() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var a = Add(board, "A", "Alice");
        var b = Add(board, "B", "Bob", "Cara");
        var c = Add(board, "C");

        board.AddPersonToCards([a, b, c], Person(board, "Cara"));
        Assert.Equal(["Alice", "Cara"], a.People.Select(p => p.Name));
        Assert.Equal(["Bob", "Cara"], b.People.Select(p => p.Name)); // already had her
        Assert.Equal(["Cara"], c.People.Select(p => p.Name));
        Assert.Equal("Add a person to 2 tasks", board.Undo());
        Assert.Empty(c.People);

        board.RemovePersonFromCards([a, b, c], Person(board, "Bob"));
        Assert.Equal(["Cara"], b.People.Select(p => p.Name)); // the next person steps up as lead
        Assert.Equal(Person(board, "Cara").Id, b.WhoId);

        board.SetCardsWho([a, b, c], Person(board, "Alice"));
        Assert.All(new[] { a, b, c }, card => Assert.Equal(["Alice"], card.People.Select(p => p.Name)));
    });

    [Fact]
    public void RenamingAndDeletingAPerson_ReachesSharedTasks() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = Add(board, "Shared", "Alice", "Bob");

        board.RenamePerson(Person(board, "Bob"), "Robert");
        Assert.Equal("Alice, Robert", card.WhoName);
        Assert.Equal(1, board.CountTasksUsingPerson(Person(board, "Robert")));

        board.DeletePerson(Person(board, "Alice")); // the lead
        Assert.Equal(["Robert"], card.People.Select(p => p.Name));

        var reopened = OpenBoard().Columns.SelectMany(c => c.Cards).Single();
        Assert.Equal(["Robert"], reopened.People.Select(p => p.Name));
        Assert.Equal(Person(board, "Robert").Id, reopened.WhoId);
    });

    [Fact]
    public void Duplicate_ARecurringTasksNextOne_AndATemplate_KeepEveryone() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = board.AddCard("Weekly", Column(board, "To Do"), board.Projects.First(), "Normal", DateTime.Today, null, true, "Weekly", null,
            people: [Person(board, "Bob"), Person(board, "Alice")]);

        Assert.Equal(["Bob", "Alice"], board.DuplicateCard(card)!.People.Select(p => p.Name));

        var template = MainViewModel.TemplateFromCard(card);
        Assert.Equal([Person(board, "Bob").Id, Person(board, "Alice").Id], template.AllPeopleIds);
        Assert.Equal([5], new TaskTemplate { WhoId = 5 }.AllPeopleIds); // a template saved before sharing

        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        var next = Column(board, "To Do").Cards.Single(c => c.Title == "Weekly" && c != card);
        Assert.Equal(["Bob", "Alice"], next.People.Select(p => p.Name));
    });

    [Fact]
    public void AReportGroupedByWho_ListsASharedTaskUnderEachPerson_AndSaysSo() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        Add(board, "Shared", "Alice", "Bob");
        Add(board, "Cara's", "Cara");
        Add(board, "Nobody's");

        var rows = ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
            [], [], [], "All", "All", "All", null, null, false, null, "Who", "None", "None");
        Assert.Equal(3, rows.Count); // still one row per task

        var groups = ReportService.GroupRows(rows, "Who");
        Assert.Equal(["Alice", "Bob", "Cara", "Unassigned"], groups.Select(g => g.Key));
        Assert.Equal("Shared", groups[0].Single().Title);
        Assert.Equal("Shared", groups[1].Single().Title);

        var shared = rows.Single(r => r.Title == "Shared");
        Assert.Contains("Who: Alice, Bob (shared - also listed under Bob)", ReportService.BuildMetaParts(shared, "Alice"));
        Assert.Contains("Who: Alice, Bob (shared - also listed under Alice)", ReportService.BuildMetaParts(shared, "Bob"));
        Assert.Contains("Who: Alice, Bob", ReportService.BuildMetaParts(shared)); // not grouped by Who: no note
        Assert.Contains("Who: Cara", ReportService.BuildMetaParts(rows.Single(r => r.Title == "Cara's"), "Cara"));

        // The report's own Who filter matches any of a task's people too.
        var bobs = ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
            [], [], ["Bob"], "All", "All", "All", null, null, false, null, "None", "None", "None");
        Assert.Equal(["Shared"], bobs.Select(r => r.Title));
    });

    [Fact]
    public void ExcelImport_ReadsSeveralPeopleFromOneCell_SeparatedBySemicolons() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);

        var cards = board.ImportCards(
        [
            new ImportedTaskRow { Title = "Shared", Who = " bob ;Dana; ; BOB;Alice" },
            new ImportedTaskRow { Title = "Comma name", Who = "Lee, Sam" },
            new ImportedTaskRow { Title = "Nobody", Who = "  " }
        ]);

        Assert.Equal(["Bob", "Dana", "Alice"], cards[0].People.Select(p => p.Name)); // matched whatever the capitals; Dana is new
        Assert.Contains(board.People, p => p.Name == "Dana");
        Assert.Equal(["Lee, Sam"], cards[1].People.Select(p => p.Name));
        Assert.Empty(cards[2].People);
    });

    [Fact]
    public void ATaskFileFromBeforeSharing_KeepsEachTasksPersonAsItsLead() => wpf.Run(() =>
    {
        var board = BoardWithPeople(OpenBoard);
        var card = Add(board, "Old style", "Bob");
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={_temp.File("board.db")}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP TABLE CardPeople;"; // as an older version left it
            cmd.ExecuteNonQuery();
        }

        var reopened = OpenBoard().Columns.SelectMany(c => c.Cards).Single();

        Assert.Equal(["Bob"], reopened.People.Select(p => p.Name));
        Assert.Equal(card.WhoId, reopened.WhoId);
    });
}
