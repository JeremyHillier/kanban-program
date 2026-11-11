using System.IO;
using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The short task window hides the rest of the form without losing it, and the setting chooses where it starts.
[Collection(WpfCollection.Name)]
public sealed class SimplifiedTaskWindowTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard()
    {
        EnsureAppResources();
        return new(new DatabaseService(_temp.File("board.db")));
    }

    // The tests' application has none of App.xaml's styles, which a window needs: give it the same ones
    // (the inside of Application.Resources, read from the source file).
    private static void EnsureAppResources()
    {
        if (Application.Current.Resources.Contains("HoverTextBoxStyle")) return;

        // pack://application:,,, addresses resolve against the application's assembly, which here is the test host.
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
        var dictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(text);
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
    }

    [Fact]
    public void TheStartingView_FollowsTheSetting_AndIsRemembered() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.False(board.SimplifiedTaskWindow);

        board.SetSimplifiedTaskWindow(true);
        Assert.True(OpenBoard().SimplifiedTaskWindow);
    });

    [Fact]
    public void TheButton_SwitchesViews_AndHiddenFieldsKeepTheirValues() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Order parts", board.Columns.First(), board.Projects.First(), "High", null, null, false, null, null, notes: "Call the supplier");
        var window = new AddTaskWindow(board, card, board.Columns.First());

        Assert.Equal(Visibility.Visible, window.NotesEditor.Visibility);
        Assert.Equal(Visibility.Visible, window.PriorityComboBox.Visibility);

        window.ViewModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

        Assert.Equal(Visibility.Collapsed, window.NotesEditor.Visibility);
        Assert.Equal(Visibility.Collapsed, window.PriorityComboBox.Visibility);
        Assert.Equal(Visibility.Visible, window.DueDatePicker.Visibility);
        Assert.Equal(Visibility.Visible, window.RecurringCheckBox.Visibility);
        Assert.Equal(Visibility.Visible, window.WhoButton.Visibility);
        Assert.Equal("Full View", window.ViewModeButton.Content);
        Assert.Contains("Call the supplier", window.NotesEditor.TextBox.Document.ContentStart.GetTextInRun(System.Windows.Documents.LogicalDirection.Forward)
            + new System.Windows.Documents.TextRange(window.NotesEditor.TextBox.Document.ContentStart, window.NotesEditor.TextBox.Document.ContentEnd).Text);

        window.ViewModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(Visibility.Visible, window.NotesEditor.Visibility);
        Assert.Equal("Simple View", window.ViewModeButton.Content);
    });

    [Fact]
    public void WithTheSettingOn_TheWindowOpensSimplified() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetSimplifiedTaskWindow(true);

        var window = new AddTaskWindow(board);

        Assert.Equal(Visibility.Collapsed, window.NotesEditor.Visibility);
        Assert.Equal(Visibility.Collapsed, window.TemplatePanel.Visibility);
    });
}
