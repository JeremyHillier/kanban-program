using System.Collections;
using System.Reflection;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The Task Files window edits the entries of the config it holds, then saves that config. If its list rows pointed at
// entries of some other copy, Edit Name would change something that was never saved. (Reads the config only.)
[Collection(WpfCollection.Name)]
public sealed class TaskFilesWindowTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void EveryRowOnTheList_IsAnEntryOfTheConfigTheWindowSaves() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var window = new TaskFilesWindow(board);

        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var config = (AppConfig)typeof(TaskFilesWindow).GetField("_config", flags)!.GetValue(window)!;
        var rows = ((IEnumerable)((System.Windows.Controls.ListBox)window.FindName("FileList")).ItemsSource).Cast<object>().ToList();

        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var entry = (TaskFileEntry)row.GetType().GetProperty("Entry")!.GetValue(row)!;
            Assert.Contains(config.Files, f => ReferenceEquals(f, entry));
        }
        window.Close();
    });
}
