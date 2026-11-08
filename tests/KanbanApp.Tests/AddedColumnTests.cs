using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Columns the user adds (ten in all): where they go, the limit, moving and removing them, and everything that has to
// carry on working with them there - colours, move-button letters, Settings' Cancel, reports, swim lanes, files.
[Collection(WpfCollection.Name)]
public sealed class AddedColumnTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string file = "board.db") => new(new DatabaseService(_temp.File(file)));

    private static List<string> Labels(MainViewModel board) => board.Columns.Select(c => c.DisplayName).ToList();

    private static CardViewModel AddTo(MainViewModel board, string title, ColumnViewModel column) =>
        board.AddCard(title, column, null, "Normal", null, null, false, null, null);

    [Fact]
    public void AnAddedColumn_SitsBeforeDone_HasNoLetter_AndLeavesTheOthersAlone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var doneColour = board.DoneColumn!.Background;

        Assert.Null(board.AddColumn("  Review "));

        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Review", "Done"], Labels(board));
        var added = board.Columns[4];
        Assert.True(added.IsCustom);
        Assert.StartsWith("Custom ", added.Name);
        Assert.Equal(string.Empty, added.QuickLetter);
        Assert.Equal(["T", "P", "H", "W"], board.Columns.Take(4).Select(c => c.QuickLetter));
        Assert.Same(doneColour, board.DoneColumn.Background); // Done is still green, whatever comes before it
        Assert.NotSame(board.Columns[3].Background, added.Background);

        var reopened = OpenBoard();
        Assert.Equal(Labels(board), Labels(reopened));
        Assert.Equal(added.Name, reopened.Columns[4].Name);
    });

    [Fact]
    public void ABoardHasTenColumnsAtMost_AndRemovingOneMakesRoom() => wpf.Run(() =>
    {
        var board = OpenBoard();
        for (var i = 1; i <= 5; i++) Assert.Null(board.AddColumn($"Extra {i}"));

        Assert.Equal(10, board.Columns.Count);
        Assert.False(board.CanAddColumn);
        Assert.NotNull(board.AddColumn("One too many"));
        Assert.Equal(10, board.Columns.Count);
        Assert.Equal("Done", board.Columns[^1].Name);

        board.RemoveColumn(board.Columns[4]);

        Assert.True(board.CanAddColumn);
        Assert.Null(board.AddColumn("Room again"));
    });

    [Fact]
    public void ANameIsNeededAndCannotRepeatAnotherColumns() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("Review");

        Assert.NotNull(board.AddColumn("   "));
        Assert.NotNull(board.AddColumn("to do"));
        Assert.NotNull(board.AddColumn("REVIEW"));
        Assert.Equal(6, board.Columns.Count);
    });

    [Fact]
    public void AnAddedColumn_CanBeMovedBetweenTheFirstColumnAndDone_AndStaysThere() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("A");
        board.AddColumn("B");
        var b = board.Columns[5];
        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "A", "B", "Done"], Labels(board));
        Assert.False(board.CanMoveColumn(b, 1)); // Done stays last
        Assert.False(board.CanMoveColumn(board.Columns[1], 1)); // only an added column moves

        board.MoveColumn(b, -1);
        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "B", "A", "Done"], Labels(board));

        board.MoveColumn(b, -1); board.MoveColumn(b, -1); board.MoveColumn(b, -1);
        Assert.Equal(["To Do", "B", "In Progress", "On Hold", "Waiting", "A", "Done"], Labels(board));
        Assert.False(board.CanMoveColumn(b, -1)); // the first column stays first: new tasks go there

        Assert.Equal(Labels(board), Labels(OpenBoard()));
        Assert.Equal(["T", "P", "H", "W"], board.Columns.Where(c => !c.IsCustom && c.Name != "Done").Select(c => c.QuickLetter));
    });

    [Fact]
    public void RemovingAColumn_MovesItsTasksToToDo_ArchivedAndDeletedOnesToo() => wpf.Run(() =>
    {
        var path = _temp.File("board.db");
        var db = new DatabaseService(path);
        var first = new MainViewModel(db);
        first.AddColumn("Review");
        var review = first.Columns.Single(c => c.DisplayName == "Review");
        var live = AddTo(first, "Live", review);
        var archived = AddTo(first, "Archived", review);
        AddTo(first, "Plain", first.ToDoColumn!);
        db.ArchiveCard(archived.Id, archived.Title, review.Name);

        var board = new MainViewModel(new DatabaseService(path));
        review = board.Columns.Single(c => c.DisplayName == "Review");
        Assert.Equal(2, board.TasksInColumn(review));

        board.RemoveColumn(review);

        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Done"], Labels(board));
        Assert.Equal(["Plain", "Live"], board.ToDoColumn!.Cards.Select(c => c.Title).Order().Reverse());
        Assert.Equal(board.ToDoColumn.Id, board.FindCardById(live.Id)!.ColumnId);

        var reopened = new MainViewModel(new DatabaseService(path));
        Assert.Equal(Labels(board), Labels(reopened));
        Assert.Equal(["Live", "Plain"], reopened.ToDoColumn!.Cards.Select(c => c.Title).Order());
        Assert.Single(reopened.GetArchivedCards());

        board.RemoveColumn(board.DoneColumn!); // a standard column cannot be removed
        Assert.Equal(5, board.Columns.Count);
    });

    [Fact]
    public void ANewColumn_JoinsTheSavedReportsThatCoverTheOthers_AndOnlyThose() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SaveReportView(new SavedReportView { Name = "Only To Do", Title = "Only To Do", IncludedColumns = ["To Do"] });

        board.AddColumn("Review");
        var name = board.Columns[4].Name;

        Assert.Contains(name, board.SavedReportViews.Single(v => v.Name == "Open Tasks by Person").IncludedColumns);
        Assert.DoesNotContain(name, board.SavedReportViews.Single(v => v.Name == "Only To Do").IncludedColumns);
        Assert.Contains(name, OpenBoard().SavedReportViews.Single(v => v.Name == "Open Tasks by Person").IncludedColumns);
    });

    [Fact]
    public void SettingsCancel_PutsBackLabels_ByColumn_NotPlace_AndLeavesAddedColumnsAlone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("Review");
        var opening = board.CaptureSettings();
        board.RenameColumnDisplayName(board.Columns[4], "QA");
        board.RestoreSettings(opening);
        Assert.Equal("Review", board.Columns[4].DisplayName);

        // A column added after Settings opened is not something Cancel can undo, so the snapshot takes it in.
        board.AddColumn("Later");
        Assert.NotEqual(opening, board.CaptureSettings());
        var refreshed = board.WithCurrentColumns(opening);
        Assert.Equal(refreshed, board.CaptureSettings());
        board.RenameColumnDisplayName(board.ToDoColumn!, "Inbox");
        board.RestoreSettings(refreshed);
        Assert.Equal("To Do", board.ToDoColumn!.DisplayName);
        Assert.Contains("Later", Labels(board));

        // And one removed since is simply not there to put a label back on.
        var withLater = board.CaptureSettings();
        board.RemoveColumn(board.Columns.Single(c => c.DisplayName == "Later"));
        board.RestoreSettings(withLater);
        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Review", "Done"], Labels(board));
    });

    [Fact]
    public void TheGtdAndStandardNames_GoToTheFiveStandardColumns_WhateverIsBetween() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("Review");

        board.UseColumnNames(MainViewModel.GtdColumnNames);
        Assert.Equal(["Inbox", "Next Actions", "In Progress", "Waiting", "Review", "Done"], Labels(board));

        board.UseColumnNames(MainViewModel.StandardColumnNames);
        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Review", "Done"], Labels(board));
    });

    [Fact]
    public void AnAddedColumn_GoesInAFileByItsLabel_AndComesBackToTheColumnWithThatLabel() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("Review");
        var review = board.Columns[4];
        var inReview = AddTo(board, "In review", review);
        var inToDo = AddTo(board, "In to do", board.ToDoColumn!);

        Assert.Equal("Review", OutlookEmailHelper.BuildImportRow(inReview, board).Category);
        Assert.Equal("To Do", OutlookEmailHelper.BuildImportRow(inToDo, board).Category);
        Assert.Same(review, board.FindColumnForImport("review"));
        Assert.Same(board.ToDoColumn, board.FindColumnForImport("To Do"));
    });

    [Fact]
    public void CopyingToAnotherFile_PutsATaskInTheAddedColumnWithTheSameLabel_ElseTheFirst() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddColumn("Review");
        var card = AddTo(board, "Needs a look", board.Columns[4]);

        var withReview = new DatabaseService(_temp.File("with.db"));
        var added = withReview.AddCustomColumn("review");
        var without = new DatabaseService(_temp.File("without.db"));

        TaskCopyService.Copy([(card, "Review")], _temp.File("with.db"));
        TaskCopyService.Copy([(card, "Review")], _temp.File("without.db"));

        Assert.Equal(added.Id, new DatabaseService(_temp.File("with.db")).GetCards().Single().ColumnId);
        Assert.Equal(without.GetColumns()[0].Id, new DatabaseService(_temp.File("without.db")).GetCards().Single().ColumnId);
    });

    [Fact]
    public void SwimLanes_HaveEveryColumn_IncludingOnesAddedOrRemovedWhileTheyAreOn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.SetSwimlaneMode(SwimlaneMode.Project);

        board.AddColumn("Review");
        Assert.All(board.Lanes, lane => Assert.Equal(board.Columns.Select(c => c.Name), lane.Columns.Select(c => c.Name)));

        board.RemoveColumn(board.Columns[4]);
        Assert.All(board.Lanes, lane => Assert.Equal(5, lane.Columns.Count));
    });

    [Fact]
    public void TheDashboard_GivesEachAddedColumnItsOwnColour() => wpf.Run(() =>
    {
        foreach (var dark in new[] { false, true })
        {
            var palette = new DashboardPalette(dark);
            var neutral = ((System.Windows.Media.SolidColorBrush)palette.Neutral).Color;
            var seven = ((System.Windows.Media.SolidColorBrush)palette.Status("Custom 7")).Color;
            var eight = ((System.Windows.Media.SolidColorBrush)palette.Status("Custom 8")).Color;

            Assert.NotEqual(neutral, seven);
            Assert.NotEqual(seven, eight);
        }
    });
}
