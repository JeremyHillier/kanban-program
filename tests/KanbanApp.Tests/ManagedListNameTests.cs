using System.Collections.ObjectModel;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Project, goal, flag and person names are unique whatever their capitals. A customer's crash log
// showed the old behaviour: adding a name that was already there made a second one, and the Who
// filter list then failed on the pair.
[Collection(WpfCollection.Name)]
public sealed class ManagedListNameTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    [Fact]
    public void AddingANameThatIsAlreadyThere_AddsNothing_AndHandsBackTheExistingOne() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var first = board.AddPerson("Sam Lee");
        var again = board.AddPerson("  sam lee ");

        Assert.Equal(ManagedAddOutcome.Added, first.Outcome);
        Assert.Equal(ManagedAddOutcome.AlreadyThere, again.Outcome);
        Assert.Same(first.Item, again.Item);
        Assert.Single(board.People, p => p.Name.Equals("Sam Lee", StringComparison.OrdinalIgnoreCase));
        Assert.Single(OpenBoard().People, p => p.Name.Equals("Sam Lee", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("already", again.Notice("person", "sam lee"));
        Assert.Null(first.Notice("person", "Sam Lee"));
    });

    [Fact]
    public void TheSameGoesForProjectsGoalsAndFlags() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddProject("Website"); board.AddGoal("Grow"); board.AddFlag("Urgent");

        Assert.Equal(ManagedAddOutcome.AlreadyThere, board.AddProject("WEBSITE").Outcome);
        Assert.Equal(ManagedAddOutcome.AlreadyThere, board.AddGoal("grow").Outcome);
        Assert.Equal(ManagedAddOutcome.AlreadyThere, board.AddFlag("urgent").Outcome);
        Assert.Single(board.Projects, p => p.Name == "Website");
        Assert.Single(board.Goals, g => g.Name == "Grow");
        Assert.Single(board.Flags, f => f.Name == "Urgent");
    });

    [Fact]
    public void AddingAnInactiveName_MakesItActiveAgain() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var sam = board.AddPerson("Sam Lee").Item!;
        board.SetPersonActive(sam, false);

        var again = board.AddPerson("Sam Lee");

        Assert.Equal(ManagedAddOutcome.SwitchedBackOn, again.Outcome);
        Assert.True(sam.IsActive);
        Assert.Contains(board.WhoFilterOptions, o => o.Name == "Sam Lee");
        Assert.Contains("active again", again.Notice("person", "Sam Lee"));
    });

    [Fact]
    public void RenamingOntoAnotherName_IsRefused_ButChangingCapitalsIsNot() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var sam = board.AddPerson("Sam Lee").Item!;
        var bob = board.AddPerson("Bob").Item!;

        Assert.False(board.RenamePerson(bob, "sam lee"));
        Assert.Equal("Bob", bob.Name);
        Assert.Equal("Bob", OpenBoard().People.Single(p => p.Id == bob.Id).Name);

        Assert.True(board.RenamePerson(sam, "SAM LEE"));
        Assert.Equal("SAM LEE", sam.Name);
    });

    // A task file that already holds a pair (made before this rule) must still open and work.
    [Fact]
    public void ATaskFileThatAlreadyHasTheSameNameTwice_StillWorks() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        db.AddPerson("Sam Lee");
        db.AddPerson("Sam Lee");

        var board = new MainViewModel(db);
        Assert.Single(board.WhoFilterOptions, o => o.Name == "Sam Lee");

        board.AddPerson("Zoe");     // The exact step in the crash log: a refresh with the pair present.
        board.AddPerson("Alan");
        Assert.Equal(["Unassigned", "Alan", "Sam Lee", "Zoe"], board.WhoFilterOptions.Select(o => o.Name));
    });

    [Fact]
    public void SyncFilterOptions_CopesWithRepeatedNames()
    {
        var options = new ObservableCollection<FilterOptionViewModel> { new("Unassigned"), new("Sam") };
        MainViewModel.SyncFilterOptions(options, ["Unassigned", "Sam", "Sam", "Zoe"]);
        Assert.Equal(["Unassigned", "Sam", "Zoe"], options.Select(o => o.Name));
    }
}

public class TypeAheadMatcherTests
{
    private static readonly string[] Names = ["Alan Poe", "Sam Lee", "Sandra Oh", "Zoe"];

    [Theory]
    [InlineData("s", 1)]
    [InlineData("SAN", 2)]
    [InlineData("sam l", 1)]
    [InlineData("oh", 2)]      // A surname, when no name starts that way
    [InlineData("z", 3)]
    [InlineData("q", -1)]
    [InlineData("", -1)]
    public void FindsTheFirstNameThatStartsThatWay(string typed, int expected)
    {
        Assert.Equal(expected, TypeAheadMatcher.Match(Names, typed));
    }

    [Fact]
    public void AFirstNameBeatsASurname()
    {
        Assert.Equal(1, TypeAheadMatcher.Match(["Alan Lee", "Lee Child"], "lee"));
    }
}
