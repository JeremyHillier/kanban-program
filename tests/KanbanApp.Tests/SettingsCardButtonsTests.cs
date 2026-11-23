using System.IO;
using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Settings > Board: the details a large card shows, and the buttons a card has, each with a tick box of its own.
[Collection(WpfCollection.Name)]
public sealed class SettingsCardButtonsTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

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

    private static List<CheckBox> Boxes(Panel grid) => grid.Children.OfType<CheckBox>().ToList();

    [Fact]
    public void TheButtons_HaveTheirOwnSection_NamedAfterTheirColumns_AndFollowARename() => wpf.Run(() =>
    {
        EnsureAppResources();
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var window = new SettingsWindow(board);

        var details = Boxes(window.CardPartsGrid);
        var buttons = Boxes(window.CardButtonsGrid);
        Assert.Equal(10, details.Count);
        Assert.DoesNotContain(details, b => b.Content is string text && text.Contains("button", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Move to To Do", "Move to In Progress", "Move to On Hold", "Move to Waiting", "Mark Done", "Delete", "Flag button", "Email button", "Calendar button"],
            buttons.Select(b => b.Content));

        board.RenameColumnDisplayName(board.WaitingColumn!, "Blocked");
        Assert.Equal("Move to Blocked", buttons[3].Content);

        buttons[5].IsChecked = false;                       // Delete
        Assert.False(board.CardParts.DeleteButton);
        buttons[0].IsChecked = false;                       // Move to To Do
        Assert.False(board.CardParts.MoveToDoButton);
        Assert.True(board.CardParts.MoveInProgressButton);
        Assert.True(board.ShowCardFootButtons);
    });
}
