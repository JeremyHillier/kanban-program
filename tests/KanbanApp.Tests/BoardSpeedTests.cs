using System.IO;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// What keeps filtering the board quick: one filter pass per change, a block's cards as rows, and a card template that
// is cheap to build (no searches up the tree for the window, no styles built for each card).
[Collection(WpfCollection.Name)]
public sealed class BoardSpeedTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    // The card lists' live filtering applies a change a moment later, on the dispatcher, as it does in the app.
    private static void LetTheListsCatchUp()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static int CountPasses(MainViewModel board, Action action)
    {
        var passes = 0;
        void Count(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.HiddenTaskCount)) passes++;
        }
        board.PropertyChanged += Count;
        action();
        board.PropertyChanged -= Count;
        return passes;
    }

    [Fact]
    public void TheQuickButtons_ClearFilters_AndACustomFilter_EachFilterTheBoardOnce() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddCard("Due today", board.ToDoColumn!, null, "Normal", DateTime.Today, null, false, null, null);
        board.AddCard("Some day", board.ToDoColumn!, null, "Normal", null, null, false, null, null);
        foreach (var option in board.PriorityFilterOptions) option.IsSelected = option.Name == "High";
        board.CaptureCustomFilter(1, "High");

        Assert.Equal(1, CountPasses(board, () => board.ShowDueFilterOnly("Today")));
        Assert.Equal(["Due today"], board.Columns.SelectMany(c => c.Cards).Where(c => c.IsVisible).Select(c => c.Title));

        Assert.Equal(1, CountPasses(board, board.ClearFilters));
        Assert.Equal(2, board.Columns.SelectMany(c => c.Cards).Count(c => c.IsVisible));

        Assert.Equal(1, CountPasses(board, () => board.ApplyCustomFilter(1)));
        Assert.Equal(1, CountPasses(board, board.ApplyFilters));                   // on its own, as before
    });

    [Fact]
    public void ABlocksCards_AreListedInRows_ThatFollowTheColumn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var column = board.ToDoColumn!;
        foreach (var title in new[] { "A", "B", "C", "D", "E" }) board.AddCard(title, column, null, "Normal", null, null, false, null, null);

        var rows = new CardRowList(column.CardsView, 2, "To Do");
        List<CardViewModel> Shown() => column.CardsView.Cast<CardViewModel>().ToList();

        Assert.Equal([2, 2, 1], rows.Select(r => r.Count));
        Assert.Equal(Shown(), rows.SelectMany(r => r));
        Assert.All(rows, r => Assert.Equal("To Do", r.Name));

        Shown()[1].IsVisible = false;                                                // filtered out: the rows close up
        LetTheListsCatchUp();
        Assert.Equal([2, 2], rows.Select(r => r.Count));
        Assert.Equal(Shown(), rows.SelectMany(r => r));

        board.AddCard("F", column, null, "Normal", null, null, false, null, null);    // a new card takes its place
        LetTheListsCatchUp();
        Assert.Equal([2, 2, 1], rows.Select(r => r.Count));
        Assert.Equal(Shown(), rows.SelectMany(r => r));
    });

    [Fact]
    public void ARow_KeepsTheCardsThatStayPut()
    {
        var row = new CardRow("To Do");
        var a = new CardViewModel(new Models.CardItem { Title = "A" });
        var b = new CardViewModel(new Models.CardItem { Title = "B" });
        var c = new CardViewModel(new Models.CardItem { Title = "C" });
        row.SetCards([a, b]);

        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        row.CollectionChanged += (_, e) => changes.Add(e.Action);
        row.SetCards([a, c]);

        Assert.Equal([a, c], row);
        Assert.Equal([System.Collections.Specialized.NotifyCollectionChangedAction.Replace], changes);   // only B's place changed
    }

    [Fact]
    public void TheCardTemplate_NeverSearchesUpToTheWindow_NorBuildsAStyleForEachCard()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        var xaml = File.ReadAllText(Path.Combine(dir!.FullName, "src", "KanbanApp", "MainWindow.xaml"));

        var card = xaml[xaml.IndexOf("<DataTemplate x:Key=\"CardTemplate\">", StringComparison.Ordinal)..xaml.IndexOf("<DataTemplate x:Key=\"CardRowTemplate\">", StringComparison.Ordinal)];
        var column = xaml[xaml.IndexOf("<DataTemplate x:Key=\"ColumnTemplate\">", StringComparison.Ordinal)..xaml.IndexOf("</Window.Resources>", StringComparison.Ordinal)];

        Assert.DoesNotContain("AncestorType=Window", card);
        Assert.DoesNotContain("AncestorType=Window", column);
        Assert.DoesNotContain("<Style ", card);
        Assert.Contains("Source={StaticResource Board}", card);
    }

    [Fact]
    public void TheBindingProxy_HoldsWhatItIsGiven()
    {
        var proxy = new BindingProxy { Data = "board" };

        Assert.Equal("board", proxy.Data);
    }
}
