using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Mass Update's drop-down lists mix plain choices with named entries (projects, goals, people). A list cannot show
// names both ways at once, and the window failed to open when it tried.
[Collection(WpfCollection.Name)]
public sealed class MassUpdateWindowTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheWindowOpens_WithProjectsGoalsAndPeopleOnTheBoard() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var goal = board.AddGoal("Perfect Systems").Item!;
        board.AddPerson("Sam Lee");
        board.AddCard("Draft the plan", board.ToDoColumn!, board.Projects.First(), "High", DateTime.Today, null, false, null, goal);

        var window = new MassUpdateWindow(board, 3);

        Assert.NotNull(window.Content);
        window.Close();
    });
}
