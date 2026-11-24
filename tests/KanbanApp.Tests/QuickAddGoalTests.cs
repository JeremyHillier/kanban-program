using System.IO;
using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Quick Add's goal: the box beside the project, what it gives the task, and what it starts on next time.
[Collection(WpfCollection.Name)]
public sealed class QuickAddGoalTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    // The tests' application has none of App.xaml's styles, which a window needs (as SimplifiedTaskWindowTests does).
    private static void EnsureAppResources()
    {
        if (Application.Current.Resources.Contains("HoverTextBoxStyle")) return;

        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, typeof(MainViewModel).Assembly);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "KanbanApp", "App.xaml"))) dir = dir.Parent;
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "KanbanApp", "App.xaml"));
        var from = source.IndexOf("<Application.Resources>", StringComparison.Ordinal) + "<Application.Resources>".Length;
        var inside = source[from..source.IndexOf("</Application.Resources>", StringComparison.Ordinal)];
        var assembly = typeof(MainViewModel).Assembly.GetName().Name;
        var text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" "
            + $"xmlns:local=\"clr-namespace:KanbanApp;assembly={assembly}\" xmlns:views=\"clr-namespace:KanbanApp.Views;assembly={assembly}\">{inside}</ResourceDictionary>";
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(text));
    }

    // What a row of the goal box says (the rows are a private type of the window).
    private static string NameOf(object? item) => (string)item!.GetType().GetProperty("Name")!.GetValue(item)!;

    [Fact]
    public void QuickAdd_GivesTheTaskTheGoal_AndStartsOnItNextTime() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Perfect Systems").Item!;

        var card = board.QuickAdd("Draft the plan", null, goal)!;

        Assert.Equal("Perfect Systems", card.GoalName);
        Assert.Equal(goal.Id, card.GoalId);
        Assert.Equal("Perfect Systems", OpenBoard().QuickAddDefaultGoal?.Name);

        board.QuickAdd("Another", null);                                         // no goal chosen: none, and it is remembered
        Assert.Null(OpenBoard().QuickAddDefaultGoal);
    });

    [Fact]
    public void AGoalThatIsNoLongerOffered_IsNotStartedOn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var goal = board.AddGoal("Old goal").Item!;
        board.QuickAdd("Task", null, goal);
        Assert.NotNull(board.QuickAddDefaultGoal);

        goal.CompletedDate = DateTime.Today;                                     // finished: no longer offered for new tasks

        Assert.Null(board.QuickAddDefaultGoal);
    });

    [Fact]
    public void TheStrip_ShowsAGoalBoxBesideTheProject_ThatGivesTheNewTaskItsGoal() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = OpenBoard();
        Assert.Equal(Visibility.Collapsed, new QuickAddWindow(board).GoalComboBox.Visibility);   // no goals: left out

        board.AddGoal("Perfect Systems");
        board.AddGoal("Grow");
        var window = new QuickAddWindow(board);

        Assert.Equal(Visibility.Visible, window.GoalComboBox.Visibility);
        Assert.Equal(["No goal", "Grow", "Perfect Systems"], window.GoalComboBox.Items.Cast<object>().Select(NameOf));
        Assert.Equal("No goal", NameOf(window.GoalComboBox.SelectedItem));

        window.GoalComboBox.SelectedIndex = 2;
        window.TaskTextBox.Text = "Draft the plan";
        window.AddTask();

        Assert.Equal("Perfect Systems", board.Columns.SelectMany(c => c.Cards).Single().GoalName);
        Assert.Equal("Perfect Systems", NameOf(new QuickAddWindow(board).GoalComboBox.SelectedItem));   // starts where it left off
    });
}
