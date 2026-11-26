using System.Collections.ObjectModel;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Importing without the review (for setting up many projects at once), and leaving a task out of an import with
// Delete on the review list.
[Collection(WpfCollection.Name)]
public sealed class ImportReviewTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static List<CardViewModel> All(MainViewModel board) => board.Columns.SelectMany(c => c.Cards).ToList();

    [Fact]
    public void ReviewEachImport_IsOnAtFirst_AndRemembered() => wpf.Run(() =>
    {
        Assert.True(OpenBoard().ReviewImports);

        OpenBoard().ReviewImports = false;

        Assert.False(OpenBoard().ReviewImports);
    });

    [Fact]
    public void WithoutTheReview_EverythingIsImported_AndNothingIsLeftForReview() => wpf.Run(() =>
    {
        var board = OpenBoard();

        var added = board.ImportCards([new ImportedTaskRow { Title = "Kick-off", Project = "Alpha" }, new ImportedTaskRow { Title = "Budget", Project = "Beta" }],
            markForReview: false);

        Assert.Equal(2, added.Count);
        Assert.Equal(["Budget", "Kick-off"], All(board).Select(c => c.Title).Order());
        Assert.All(All(board), c => Assert.False(c.IsImported));
        Assert.Empty(board.GetImportedCards());
        Assert.Contains(board.Projects, p => p.Name == "Alpha");
    });

    [Fact]
    public void WithTheReview_TheTasksAreListedForReview_AsBefore() => wpf.Run(() =>
    {
        var board = OpenBoard();

        board.ImportCards([new ImportedTaskRow { Title = "Kick-off" }]);

        Assert.Equal(["Kick-off"], board.GetImportedCards().Select(c => c.Title));
    });

    [Fact]
    public void TheWindow_SaysWhatAnImportWithNoReviewDid()
    {
        Assert.Equal("12 new tasks added and 3 tasks updated from Projects.xlsx. Undo (Ctrl+Z) on the board takes the whole import back.",
            ImportTasksWindow.ImportedText(new ImportSummary(12, 3, 0, 0, 0), [@"C:\Setup\Projects.xlsx"]));
        Assert.Equal("1 new task added from 2 files. Undo (Ctrl+Z) on the board takes the whole import back.",
            ImportTasksWindow.ImportedText(new ImportSummary(1, 0, 0, 0, 0), [@"C:\a.xlsx", @"C:\b.xlsx"]));
        Assert.StartsWith("Nothing was added or changed from Projects.xlsx.\n\nThese were left as they are:",
            ImportTasksWindow.ImportedText(new ImportSummary(0, 0, 2, 0, 0), [@"C:\Setup\Projects.xlsx"]));
        Assert.EndsWith("Left out, not being Excel files: notes.txt.",
            ImportTasksWindow.ImportedText(new ImportSummary(1, 0, 0, 0, 0), [@"C:\a.xlsx"], ["notes.txt"]));
    }

    [Fact]
    public void DeleteOnTheReviewList_LeavesATaskOut_WhenSaved_AndKeepChangesYourMind() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.ImportCards([new ImportedTaskRow { Title = "Wanted" }, new ImportedTaskRow { Title = "Not wanted" }, new ImportedTaskRow { Title = "Undecided" }]);

        var window = new ImportedTasksWindow(board);
        var rows = (ObservableCollection<ImportedRowEditViewModel>)window.RowsList.ItemsSource;
        var notWanted = rows.Single(r => r.Title == "Not wanted");
        var undecided = rows.Single(r => r.Title == "Undecided");

        notWanted.IsDeleting = true;
        notWanted.Title = "";                         // a marked row is not checked for task details
        Assert.Equal("Keep", notWanted.DeleteLabel);
        undecided.IsDeleting = true;
        undecided.IsDeleting = false;                  // Keep
        Assert.Equal("Delete", undecided.DeleteLabel);

        Assert.Equal(1, window.SaveRows());

        Assert.Equal(["Undecided", "Wanted"], All(board).Select(c => c.Title).Order());
        Assert.Contains(board.GetDeletedCards(), d => d.Title == "Not wanted");
        Assert.Equal(["Wanted", "Undecided"], rows.Select(r => r.Title));
        window.Close();
    });
}
