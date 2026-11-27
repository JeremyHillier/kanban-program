using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Deleting a project or person that tasks use can move all of those tasks to another one in the same step:
// on the board, in the task file, and for archived tasks too. Leaving them without still works as before.
[Collection(WpfCollection.Name)]
public sealed class MoveTasksOnDeleteTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static PersonViewModel Person(MainViewModel board, string name) => board.People.Single(p => p.Name == name);

    private static CardViewModel Card(MainViewModel board, string title) => board.Columns.SelectMany(c => c.Cards).Single(c => c.Title == title);

    private static CardViewModel Add(MainViewModel board, string title, ProjectViewModel project, params string[] people) =>
        board.AddCard(title, board.Columns.Single(c => c.Name == "To Do"), project, "Normal", null, null, false, null, null,
            people: people.Select(name => Person(board, name)).ToList());

    [Fact]
    public void ThePersonPicked_TakesTheirPlace_AsLeadWhereTheyLed_AndItIsSaved() => wpf.Run(() =>
    {
        var board = OpenBoard();
        foreach (var name in new[] { "Alice", "Bob", "Cara" }) board.AddPerson(name);
        var project = board.Projects.First();
        Add(board, "Led", project, "Alice", "Cara");
        Add(board, "Second", project, "Cara", "Alice");
        Add(board, "Bob later", project, "Alice", "Cara", "Bob");
        Add(board, "Bob first", project, "Bob", "Alice");
        Add(board, "Untouched", project, "Cara");

        board.DeletePerson(Person(board, "Alice"), Person(board, "Bob"));

        Assert.DoesNotContain(board.People, p => p.Name == "Alice");
        foreach (var b in new[] { board, OpenBoard() })
        {
            Assert.Equal(["Bob", "Cara"], Card(b, "Led").People.Select(p => p.Name));
            Assert.Equal(["Cara", "Bob"], Card(b, "Second").People.Select(p => p.Name));
            Assert.Equal(["Bob", "Cara"], Card(b, "Bob later").People.Select(p => p.Name));
            Assert.Equal(["Bob"], Card(b, "Bob first").People.Select(p => p.Name));
            Assert.Equal(["Cara"], Card(b, "Untouched").People.Select(p => p.Name));
            Assert.Equal(Person(b, "Bob").Id, Card(b, "Led").WhoId);
            Assert.Equal(Person(b, "Bob").Id, Card(b, "Bob later").WhoId);
            Assert.Equal(Person(b, "Cara").Id, Card(b, "Second").WhoId);
        }
    });

    [Fact]
    public void TheTasksOfADeletedProject_MoveToThePickedOne_ArchivedTasksToo_AndTheFiltersSeeIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var old = board.AddProject("Old").Item!;
        var target = board.AddProject("New").Item!;
        var open = Add(board, "Open", old);
        var finished = Add(board, "Finished", old);
        board.MoveCards([finished], board.DoneColumn!);
        board.ArchiveDoneTasks();

        board.ProjectFilterOptions.Single(o => o.Name == "New").IsSelected = true;
        board.ApplyFilters();
        Assert.False(open.IsVisible);

        board.DeleteProject(old, target);

        Assert.DoesNotContain(board.Projects, p => p.Name == "Old");
        Assert.Equal(target.Id, open.ProjectId);
        Assert.Equal("New", open.ProjectName);
        Assert.True(open.IsVisible); // it now matches the project filter
        Assert.Equal("New", board.GetArchivedCards().Single(a => a.Title == "Finished").ProjectName);
        Assert.Equal(target.Id, Card(OpenBoard(), "Open").ProjectId);
    });

    [Fact]
    public void LeavingThemWithNone_WorksAsBefore() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var old = board.AddProject("Old").Item!;
        board.AddPerson("Alice");
        board.AddPerson("Bob");
        var card = Add(board, "Task", old, "Alice", "Bob");

        board.DeleteProject(old);
        board.DeletePerson(Person(board, "Alice"));

        Assert.Null(card.ProjectId);
        Assert.Equal("No Project", card.ProjectName);
        Assert.Equal(["Bob"], card.People.Select(p => p.Name));
        Assert.Null(Card(OpenBoard(), "Task").ProjectId);
    });

    [Fact]
    public void TheDialog_OffersNoneFirst_ThenEveryOtherActiveEntry_AndHandsBackThePick() => wpf.Run(() =>
    {
        var board = OpenBoard();
        foreach (var name in new[] { "Alice", "Bob", "Cara", "Dev" }) board.AddPerson(name);
        board.SetPersonActive(Person(board, "Dev"), false);
        var kind = ManagedListKind.People(board);
        var alice = Person(board, "Alice");
        var card = Add(board, "Call", board.Projects.First(), "Alice");

        var window = new DeleteListEntryWindow(kind, alice, 1);
        try
        {
            Assert.Equal("Delete Person", window.Title);
            Assert.Equal("Nobody: leave them unassigned", window.Choices[0].Name);
            Assert.Null(window.Choices[0].Item);
            var names = window.Choices.Skip(1).Select(c => c.Name).ToList();
            Assert.Contains("Bob", names);
            Assert.Contains("Cara", names);
            Assert.DoesNotContain("Alice", names); // not the one being deleted
            Assert.DoesNotContain("Dev", names);   // nor a retired one

            window.Delete_Click(window, new RoutedEventArgs());
            Assert.Null(window.MoveTo); // nothing picked: the tasks are left without, as before

            window.Choose(Person(board, "Bob"));
            window.Delete_Click(window, new RoutedEventArgs());
            Assert.Same(Person(board, "Bob"), window.MoveTo);
        }
        finally
        {
            window.Close();
        }

        kind.Delete(alice, window.MoveTo);
        Assert.Equal(["Bob"], card.People.Select(p => p.Name));
    });

    [Fact]
    public void WithNoOtherEntry_TheDialogSaysSo() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPerson("Alice");
        var alice = Person(board, "Alice");
        foreach (var other in board.People.Where(p => p != alice).ToList()) board.SetPersonActive(other, false);

        var window = new DeleteListEntryWindow(ManagedListKind.People(board), alice, 2);
        try
        {
            Assert.Single(window.Choices);
            Assert.False(window.MoveToComboBox.IsEnabled);
            Assert.Contains("no other active person", window.HintText.Text);
        }
        finally
        {
            window.Close();
        }
    });
}
