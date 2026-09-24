using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The one Manage screen serves Projects, Goals, Flags and Who. Each list keeps the wording its own
// window used to have, and every action reaches that list and no other.
[Collection(WpfCollection.Name)]
public sealed class ManagedListKindTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void EachList_KeepsItsOwnTitleAndWording() => wpf.Run(() =>
    {
        var board = OpenBoard();

        var projects = ManagedListKind.Projects(board);
        Assert.Equal("Manage Projects", projects.Title);
        Assert.Equal("Delete project \"Home\"?", projects.DeleteQuestion("Home"));
        Assert.Equal("they'll show as having no project.", projects.IfDeleted);

        var goals = ManagedListKind.Goals(board);
        Assert.Equal("Manage Goals", goals.Title);
        Assert.Equal("Delete goal \"Grow\"?", goals.DeleteQuestion("Grow"));
        Assert.Equal("they'll show as having no goal.", goals.IfDeleted);

        var flags = ManagedListKind.Flags(board);
        Assert.Equal("Manage Flags", flags.Title);
        Assert.Equal("Delete flag \"Urgent\"?", flags.DeleteQuestion("Urgent"));
        Assert.Equal("they'll lose this flag.", flags.IfDeleted);

        var people = ManagedListKind.People(board);
        Assert.Equal("Manage Who", people.Title);
        Assert.Equal("Delete \"Sam\"?", people.DeleteQuestion("Sam")); // people are never "Delete person"
        Assert.Equal("they'll show as unassigned.", people.IfDeleted);

        Assert.Equal([false, false, false, true], new[] { projects, goals, flags, people }.Select(k => k.HasEmail));
    });

    [Fact]
    public void TheActions_ReachTheRightList() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var kinds = new[] { ManagedListKind.Projects(board), ManagedListKind.Goals(board), ManagedListKind.Flags(board), ManagedListKind.People(board) };
        IEnumerable<IManagedItem> ListOf(int i) => i switch
        {
            0 => board.Projects, 1 => board.Goals, 2 => board.Flags, _ => board.People
        };
        var before = Enumerable.Range(0, 4).Select(i => ListOf(i).Count()).ToArray();

        for (var i = 0; i < 4; i++)
        {
            kinds[i].Add(null!, $"Added {i}"); // a new name raises no message, so no window is needed
            Assert.Same(ListOf(i), kinds[i].Items);
        }
        for (var i = 0; i < 4; i++) Assert.Equal(before[i] + 1, ListOf(i).Count());

        var person = ListOf(3).Single(p => p.Name == "Added 3");
        var card = board.AddCard("Call", board.Columns.First(), board.Projects.First(), "Normal", null, (PersonViewModel)person, false, null, null);
        Assert.Equal(1, kinds[3].CountUsage(person));

        Assert.True(kinds[3].Rename(person, "Sam Lee"));
        Assert.Equal("Sam Lee", card.WhoName);
        kinds[3].SetEmail(person, "  sam@example.com ");
        Assert.Equal("sam@example.com", ((PersonViewModel)person).Email);
        kinds[3].SetActive(person, false);
        Assert.False(person.IsActive);

        var flag = ListOf(2).Single(f => f.Name == "Added 2");
        kinds[2].Delete(flag);
        Assert.DoesNotContain(flag, ListOf(2));
        Assert.Equal(before[0] + 1, ListOf(0).Count()); // the other lists untouched
        Assert.Equal(before[1] + 1, ListOf(1).Count());
    });
}
