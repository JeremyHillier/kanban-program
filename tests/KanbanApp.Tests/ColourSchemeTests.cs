using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The board's colours: in the dark theme each column carries a line of its own colour along its top, the task card's
// move, done and delete buttons are their colours at half strength, and the header wears the dialogs' title band.
[Collection(WpfCollection.Name)]
public sealed class ColourSchemeTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string MainWindowXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "KanbanApp", "MainWindow.xaml"));
    }

    [Fact]
    public void InTheDarkTheme_EachColumnHasAStripeOfItsOwn_AndInTheLightThemeNone() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.False(board.IsDarkMode);
        Assert.All(board.Columns, c => Assert.Null(c.Stripe));

        board.ToggleTheme();
        try
        {
            Assert.All(board.Columns, c => Assert.NotNull(c.Stripe));
            Assert.Equal(board.Columns.Count, board.Columns.Select(c => ((SolidColorBrush)c.Stripe!).Color).Distinct().Count());
        }
        finally
        {
            board.ToggleTheme(); // the theme's colours are shared by every test: put the light theme back
        }

        Assert.All(board.Columns, c => Assert.Null(c.Stripe));
    });

    [Fact]
    public void TheCardsQuickButtons_AreTheirColoursAtHalfStrength()
    {
        var xaml = MainWindowXaml();

        foreach (var key in new[] { "ToDo", "InProgress", "OnHold", "Waiting", "Done", "Delete" })
        {
            Assert.Matches($"<SolidColorBrush x:Key=\"Quick{key}Brush\" Color=\"#FF[0-9A-F]{{6}}\" Opacity=\"0.5\"/>", xaml);
            Assert.Contains($"Background=\"{{StaticResource Quick{key}Brush}}\" Foreground=\"{{DynamicResource Quick{key}TextBrush}}\"", xaml);
        }
    }

    [Fact]
    public void TheHeader_WearsTheDialogsTitleBand()
    {
        var xaml = MainWindowXaml();

        Assert.Matches(new Regex("Background=\"\\{DynamicResource DialogBandBrush\\}\" BorderBrush=\"\\{DynamicResource DialogBandRuleBrush\\}\" BorderThickness=\"0,0,0,2\"\\s*MouseLeftButtonDown=\"Header_MouseLeftButtonDown\""), xaml);
    }
}
