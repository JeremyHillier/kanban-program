using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Swimlanes: lanes made from projects, goals or people; which tasks each shows; counts; remembered state;
// and what dropping a task on a lane does.
[Collection(WpfCollection.Name)]
public sealed class SwimlaneTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static CardViewModel Add(MainViewModel board, string title, ProjectViewModel? project, string column = "To Do") =>
        board.AddCard(title, board.Columns.Single(c => c.Name == column), project, "Normal", null, null, false, null, null);

    private static List<string> Titles(LaneColumnViewModel column) => column.CardsView.Cast<CardViewModel>().Select(c => c.Title).Order().ToList();

    [Fact]
    public void ProjectLanes_AreTheActiveProjects_ThenALaneForTasksWithNone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        var house = board.AddProject("House").Item!;
        board.SetProjectActive(house, false);

        board.SetSwimlaneMode(SwimlaneMode.Project);

        var names = board.Lanes.Select(l => l.Title).ToList();
        Assert.Contains("Garden", names);
        Assert.DoesNotContain("House", names);
        Assert.Equal("No Project", names[^1]);
        Assert.Equal(board.Columns.Count, board.Lanes[0].Columns.Count);
        Assert.True(board.IsSwimlanesOn);
        Assert.False(board.IsSwimlanesOff);
    });

    [Fact]
    public void ALane_ShowsOnlyItsTasks_InEachColumn_AndCountsThem() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        var house = board.AddProject("House").Item!;
        Add(board, "Mow", garden);
        Add(board, "Rake", garden, "In Progress");
        Add(board, "Paint", house);
        board.SetSwimlaneMode(SwimlaneMode.Project);

        var lane = board.Lanes.Single(l => l.Title == "Garden");

        Assert.Equal(["Mow"], Titles(lane.Columns.Single(c => c.Name == "To Do")));
        Assert.Equal(["Rake"], Titles(lane.Columns.Single(c => c.Name == "In Progress")));
        Assert.Equal(2, lane.Count);
        Assert.Equal(1, board.Lanes.Single(l => l.Title == "House").Count);
    });

    [Fact]
    public void TheFilters_StillApplyInsideLanes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        Add(board, "Mow", garden);
        Add(board, "Rake", garden);
        board.SetSwimlaneMode(SwimlaneMode.Project);

        board.KeywordFilter = "Mow";

        var lane = board.Lanes.Single(l => l.Title == "Garden");
        Assert.Equal(["Mow"], Titles(lane.Columns.Single(c => c.Name == "To Do")));
        Assert.Equal(1, lane.Count);
    });

    [Fact]
    public void ALaneFollowsATaskThatChangesProject() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        var house = board.AddProject("House").Item!;
        var card = Add(board, "Mow", garden);
        board.SetSwimlaneMode(SwimlaneMode.Project);

        board.SetCardProject(card, house);

        Assert.Empty(Titles(board.Lanes.Single(l => l.Title == "Garden").Columns.Single(c => c.Name == "To Do")));
        Assert.Equal(["Mow"], Titles(board.Lanes.Single(l => l.Title == "House").Columns.Single(c => c.Name == "To Do")));
        Assert.Equal(1, board.Lanes.Single(l => l.Title == "House").Count);
    });

    [Fact]
    public void TheGroupingAndCollapsedLanes_AreRemembered_InTheTaskFile() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddProject("Garden");
        board.SetSwimlaneMode(SwimlaneMode.Project);
        board.Lanes.Single(l => l.Title == "Garden").IsExpanded = false;

        var reopened = OpenBoard();

        Assert.Equal(SwimlaneMode.Project, reopened.SwimlaneMode);
        Assert.False(reopened.Lanes.Single(l => l.Title == "Garden").IsExpanded);
        Assert.True(reopened.Lanes.Single(l => l.Title == "No Project").IsExpanded);

        reopened.SetAllLanesExpanded(true);
        Assert.All(OpenBoard().Lanes, l => Assert.True(l.IsExpanded));
    });

    [Fact]
    public void TurningLanesOff_LeavesTheBoardAsItWas() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetSwimlaneMode(SwimlaneMode.Goal);
        Assert.NotEmpty(board.Lanes);

        board.SetSwimlaneMode(SwimlaneMode.Off);

        Assert.Empty(board.Lanes);
        Assert.True(board.IsSwimlanesOff);
        Assert.Equal("Swim lanes", board.SwimlaneButtonLabel);
        Assert.Equal(SwimlaneMode.Off, OpenBoard().SwimlaneMode);
    });

    [Fact]
    public void DroppingOnAnotherLanesColumn_ChangesTheProject_AndTheColumn_InOneUndoStep() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        var house = board.AddProject("House").Item!;
        var card = Add(board, "Mow", garden);
        board.SetSwimlaneMode(SwimlaneMode.Project);
        var target = board.Lanes.Single(l => l.Title == "House").Columns.Single(c => c.Name == "In Progress");

        board.DropOnLane(target, [card]);

        Assert.Equal(("House", "In Progress"), (card.ProjectName, board.ColumnOf(card)!.Name));

        board.Undo();

        Assert.Equal(("Garden", "To Do"), (card.ProjectName, board.ColumnOf(card)!.Name));
    });

    [Fact]
    public void DroppingOnTheNoneLane_ClearsTheGoal_AndAssignedToLanesChangeTheLead() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Launch").Item!;
        var sam = board.AddPerson("Sam").Item!;
        var card = Add(board, "Mow", board.Projects.First());
        board.SetCardGoal(card, goal);

        board.SetSwimlaneMode(SwimlaneMode.Goal);
        board.DropOnLane(board.Lanes[^1].Columns[0], [card]);
        Assert.Null(card.GoalId);

        board.SetSwimlaneMode(SwimlaneMode.AssignedTo);
        board.DropOnLane(board.Lanes.Single(l => l.Title == "Sam").Columns[0], [card]);
        Assert.Equal(sam.Id, card.WhoId);
    });

    // A filter that shows or hides many tasks at once rebuilds each column's list in one go. With lanes on, the lanes'
    // own lists were dropped while WPF still had work queued on them, which failed once for every lane column.
    [Fact]
    public void AFilterThatHidesManyTasks_WithLanesOn_RunsWithoutAnError() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var garden = board.AddProject("Garden").Item!;
        for (var i = 0; i < 80; i++) Add(board, $"Task {i}", garden);
        board.SetSwimlaneMode(SwimlaneMode.Project);
        foreach (var column in board.Columns) _ = column.CardsView.Count; // as the board does by showing them
        foreach (var column in board.Lanes.SelectMany(l => l.Columns)) _ = column.CardsView.Count;

        Exception? failure = null;
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        System.Windows.Threading.DispatcherUnhandledExceptionEventHandler catcher = (_, e) => { failure ??= e.Exception; e.Handled = true; };
        dispatcher.UnhandledException += catcher;
        try
        {
            board.KeywordFilter = "nothing matches this";
            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            board.KeywordFilter = string.Empty;
            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
        finally
        {
            dispatcher.UnhandledException -= catcher;
        }

        Assert.Null(failure);
        var lane = board.Lanes.Single(l => l.Title == "Garden");
        Assert.Equal(80, lane.Count);
        Assert.Equal(80, lane.Columns.Single(c => c.Name == "To Do").CardsView.Cast<CardViewModel>().Count());
    });
}
