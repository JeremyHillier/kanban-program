using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// A task's sub-tasks as choices (Pick one): at most one picked, kept in the file, picked and cleared on the board with Undo,
// carried by repeats, duplicates and templates, written to and read from Excel, and shown with round buttons on the task screen.
[Collection(WpfCollection.Name)]
public sealed class SubTaskPickOneTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static List<SubTaskViewModel> SubTasks(params (string Title, bool IsDone)[] items) =>
        items.Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = s.IsDone })).ToList();

    private static CardViewModel AddChoiceTask(MainViewModel board, bool recurring = false, params (string Title, bool IsDone)[] items) =>
        board.AddCard("Lunch order", board.ToDoColumn!, null, "Normal", recurring ? DateTime.Today : null, null, recurring, recurring ? "Daily" : null, null,
            subTasks: SubTasks(items), subTasksPickOne: true);

    private static List<(string, bool)> Ticks(CardViewModel card) => card.SubTasks.Select(s => (s.Title, s.IsDone)).ToList();

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

    [Fact]
    public void APickOneTask_IsKeptInTheFile_WithOnlyOnePicked() => wpf.Run(() =>
    {
        var board = OpenBoard();
        AddChoiceTask(board, false, ("Soup", true), ("Salad", true), ("Sandwich", false)); // two arrived picked: the first is kept

        var loaded = OpenBoard().Columns.SelectMany(c => c.Cards).Single();
        Assert.True(loaded.SubTasksPickOne);
        Assert.Equal([("Soup", true), ("Salad", false), ("Sandwich", false)], Ticks(loaded));
        Assert.Equal("Chosen: Soup", loaded.SubTaskProgressDisplay);
        Assert.Equal(DatabaseService.PickOneFileFormat, new DatabaseService(_temp.File("board.db")).FileFormatAtOpen);
    });

    [Fact]
    public void PickingOnTheBoard_MovesTheChoice_ClearsIt_AndUndoes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddChoiceTask(board, false, ("Soup", false), ("Salad", false));
        Assert.Equal("Chosen: none yet", card.SubTaskProgressDisplay);

        board.PickSubTask(card, card.SubTasks[0]);
        board.PickSubTask(card, card.SubTasks[1]);
        Assert.Equal([("Soup", false), ("Salad", true)], Ticks(card));
        Assert.Equal("Chosen: Salad", card.SubTaskProgressDisplay);
        Assert.Equal([("Soup", false), ("Salad", true)], Ticks(OpenBoard().Columns.SelectMany(c => c.Cards).Single()));

        board.PickSubTask(card, null);
        Assert.All(card.SubTasks, s => Assert.False(s.IsDone));

        board.Undo();
        var restored = board.Columns.SelectMany(c => c.Cards).Single();
        Assert.Equal([("Soup", false), ("Salad", true)], Ticks(restored));
        Assert.True(restored.SubTasksPickOne);
    });

    [Fact]
    public void TheNextOccurrenceAndADuplicate_KeepTheChoices_WithNonePicked() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddChoiceTask(board, true, ("Soup", true), ("Salad", false));

        var copy = board.DuplicateCard(card)!;
        Assert.True(copy.SubTasksPickOne);
        Assert.All(copy.SubTasks, s => Assert.False(s.IsDone));

        board.MoveCards([card], board.DoneColumn!);
        var next = board.ToDoColumn!.Cards.Single(c => c != card && c != copy);
        Assert.True(next.SubTasksPickOne);
        Assert.Equal([("Soup", false), ("Salad", false)], Ticks(next));
    });

    [Fact]
    public void ATemplate_CarriesPickOne() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = AddChoiceTask(board, false, ("Soup", true), ("Salad", false));

        board.SaveTaskTemplate("Lunch", MainViewModel.TemplateFromCard(card));

        var template = OpenBoard().FindTaskTemplate("Lunch")!;
        Assert.True(template.SubTasksPickOne);
        Assert.Equal(["Soup", "Salad"], template.SubTasks);
    });

    [Fact]
    public void Excel_WritesChoicesWithRoundBrackets_AndReadsThemBack()
    {
        var cell = ImportService.FormatSubTasks([("Soup", false), ("Salad", true)], pickOne: true);

        Assert.Equal("( ) Soup\n(x) Salad", cell);
        Assert.True(ImportService.IsPickOne(cell));
        Assert.Equal([("Soup", false), ("Salad", true)], ImportService.ParseSubTasks(cell));

        // A checklist is as before, and a title that merely starts with brackets is not a choice.
        Assert.Equal("Soup\n[x] Salad", ImportService.FormatSubTasks([("Soup", false), ("Salad", true)]));
        Assert.False(ImportService.IsPickOne("Soup\n[x] Salad"));
        Assert.False(ImportService.IsPickOne("(1) Call the client\n(2) Send the quote"));
        Assert.Equal([("(1) Call the client", false)], ImportService.ParseSubTasks("(1) Call the client"));
    }

    [Fact]
    public void ChoiceText_NamesThePick_OrSaysNoneYet()
    {
        Assert.Equal("Chosen: Salad", SubTaskRules.ChoiceText([("Soup", false), ("Salad", true)]));
        Assert.Equal("Chosen: none yet", SubTaskRules.ChoiceText([("Soup", false)]));
        Assert.Equal([("a", true), ("b", false), ("c", false)], SubTaskRules.KeepFirstPicked([("a", true), ("b", true), ("c", false)]));
    }

    [Fact]
    public void TheTaskScreen_ShowsRoundButtons_PicksOnlyOne_AndSwitchesBack() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = OpenBoard();
        var card = AddChoiceTask(board, false, ("Soup", true), ("Salad", false), ("Sandwich", false));

        var window = new AddTaskWindow(board, card, board.ToDoColumn!);
        var ticks = window.SubTasksPanel.Children.OfType<Grid>().Select(row => (ToggleButton)row.Children[1]).ToList();

        Assert.True(window.SubTasksPickOneCheckBox.IsChecked);
        Assert.All(ticks, t => Assert.IsType<RadioButton>(t));
        Assert.Equal("Chosen: Soup", window.SubTaskProgressLabel.Text);
        Assert.Equal(Visibility.Visible, window.ClearSubTaskPickButton.Visibility);

        ticks[2].IsChecked = true;                                         // picking another unpicks the first
        Assert.Equal([false, false, true], ticks.Select(t => t.IsChecked == true));
        Assert.Equal("Chosen: Sandwich", window.SubTaskProgressLabel.Text);

        window.SubTasksPickOneCheckBox.IsChecked = false;                  // back to a checklist, keeping the tick
        var boxes = window.SubTasksPanel.Children.OfType<Grid>().Select(row => (ToggleButton)row.Children[1]).ToList();
        Assert.All(boxes, b => Assert.IsType<CheckBox>(b));
        Assert.Equal([false, false, true], boxes.Select(b => b.IsChecked == true));
        Assert.Equal(Visibility.Collapsed, window.ClearSubTaskPickButton.Visibility);
    });
}
