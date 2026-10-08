using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The board with a priority list the user has changed: everything that shows, sorts, filters or
// stores a priority has to follow it. Also the column names feeding the letters on the move buttons.
[Collection(WpfCollection.Name)]
public sealed class PriorityBoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string priority = "Normal", string column = "To Do") =>
        board.AddCard(title, Column(board, column), board.Projects.First(), priority, null, null, false, null, null);

    private static CardViewModel Card(MainViewModel board, string title) => board.Columns.SelectMany(c => c.Cards).Single(c => c.Title == title);

    [Fact]
    public void ANewTaskFile_HasTheStandardFour_AndStaysAtTheOlderFileFormat() => wpf.Run(() =>
    {
        var board = OpenBoard();

        Assert.True(board.Priorities.IsStandard);
        Assert.Equal(["High", "Medium", "Normal", "Low"], board.PriorityFilterOptions.Select(o => o.Name));
        Assert.Equal("3", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));
    });

    [Fact]
    public void AnAddedPriority_IsKept_OfferedInTheFilter_AndRaisesTheFileFormat() => wpf.Run(() =>
    {
        var board = OpenBoard();

        Assert.True(board.AddPriority("Urgent"));
        Assert.False(board.AddPriority("urgent"));
        Assert.True(board.MovePriority("Urgent", -4));

        var reopened = OpenBoard();
        Assert.Equal(["Urgent", "High", "Medium", "Normal", "Low"], reopened.Priorities.Names);
        Assert.Equal(["Urgent", "High", "Medium", "Normal", "Low"], reopened.PriorityFilterOptions.Select(o => o.Name));

        // An older copy of the app would put an Urgent task back to Normal, so it must be warned.
        var db = new DatabaseService(_temp.File("board.db"));
        Assert.Equal("4", db.GetSetting("FileFormat"));
        Assert.False(db.IsFromNewerApp);
    });

    [Fact]
    public void SortByPriority_FollowsTheListsOrder() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPriority("Urgent");
        Add(board, "low", "Low"); Add(board, "urgent", "Urgent"); Add(board, "high", "High");

        board.ToggleSortKey(MainViewModel.SortKey.Priority, additive: false);
        Assert.Equal(["high", "low", "urgent"], Column(board, "To Do").Cards.Select(c => c.Title)); // Urgent is last on the list so far

        board.MovePriority("Urgent", -4);
        Assert.Equal(["urgent", "high", "low"], Column(board, "To Do").Cards.Select(c => c.Title));
    });

    [Fact]
    public void Renaming_RewordsEveryTask_OnTheBoardAndArchived() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var open = Add(board, "open", "High");
        Add(board, "finished", "High", column: "Done");
        Add(board, "other", "Low");
        board.ArchiveDoneTasks();

        Assert.True(board.RenamePriority("High", "Critical"));

        Assert.Equal("Critical", open.Priority);
        var reopened = OpenBoard();
        Assert.Equal("Critical", Card(reopened, "open").Priority);
        Assert.Equal("Low", Card(reopened, "other").Priority);
        Assert.Equal("Critical", reopened.GetArchivedReportRows().Single().Card.Priority);
        Assert.Equal(["Critical", "Medium", "Normal", "Low"], reopened.Priorities.Names);
        Assert.Equal(2, reopened.PriorityEntries.Single(e => e.Name == "Critical").TaskCount); // archived tasks count
    });

    [Fact]
    public void Renaming_IsFollowedByTemplates_Filters_SavedReportViews_AndTheBoardsOwnFilter() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "template me", "High");
        Add(board, "hidden", "Low");
        board.SaveTaskTemplate("Weekly", MainViewModel.TemplateFromCard(card));
        board.SaveReportView(new SavedReportView { Name = "Hot", Priority = ["High", "Medium"] });

        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();
        board.CaptureCustomFilter(1, "Hot ones");

        Assert.True(board.RenamePriority("High", "Critical"));

        Assert.Equal("Critical", board.TaskTemplates.Single().Priority);
        Assert.Equal(["Critical"], board.CustomFilters[1].Priority);
        Assert.Equal(["Critical", "Medium"], board.SavedReportViews.Single(v => v.Name == "Hot").Priority);
        Assert.True(board.PriorityFilterOptions.Single(o => o.Name == "Critical").IsSelected); // still filtering
        Assert.True(card.IsVisible);
        Assert.False(Card(board, "hidden").IsVisible);

        var reopened = OpenBoard();
        Assert.Equal("Critical", reopened.TaskTemplates.Single().Priority);
        Assert.Equal(["Critical"], reopened.CustomFilters[1].Priority);
        Assert.Equal(["Critical", "Medium"], reopened.SavedReportViews.Single(v => v.Name == "Hot").Priority);
    });

    [Fact]
    public void Renaming_IsRefused_OntoAnotherPrioritysName() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "a", "High");

        Assert.False(board.RenamePriority("High", "low"));
        Assert.Equal("High", card.Priority);
        Assert.True(board.Priorities.IsStandard);
    });

    [Fact]
    public void Deleting_MovesItsTasksToTheDefault_AndFiltersStopNamingIt() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "was high", "High");
        board.SaveReportView(new SavedReportView { Name = "Hot", Priority = ["High", "Medium"] });
        board.SaveTaskTemplate("T", MainViewModel.TemplateFromCard(card));

        Assert.True(board.DeletePriority("High"));

        Assert.Equal("Normal", card.Priority);
        Assert.Equal(["Medium"], board.SavedReportViews.Single(v => v.Name == "Hot").Priority);
        Assert.Equal("Normal", board.TaskTemplates.Single().Priority);
        Assert.Equal(["Medium", "Normal", "Low"], board.PriorityFilterOptions.Select(o => o.Name));
        Assert.Equal("Normal", Card(OpenBoard(), "was high").Priority);
    });

    [Fact]
    public void TheDefault_CannotBeDeleted_AndANewDefaultIsWhatQuickAddUses() => wpf.Run(() =>
    {
        var board = OpenBoard();

        Assert.False(board.DeletePriority("Normal"));
        Assert.True(board.SetDefaultPriority("Low"));
        Assert.True(board.DeletePriority("Normal"));

        Assert.Equal("Low", board.QuickAdd("plain task", null)!.Priority);
        Assert.Equal("Low", OpenBoard().Priorities.Default);
    });

    [Fact]
    public void ARenameOrDelete_EmptiesUndo_SoAnOldNameCannotComeBack() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "a", "High");
        board.SetCardPriority(card, "Low");
        Assert.True(board.CanUndo);

        board.RenamePriority("High", "Critical");

        Assert.False(board.CanUndo);
        Assert.Equal("Low", card.Priority);
    });

    [Fact]
    public void ReorderingAndRecolouring_LeaveUndoAlone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "a", "High");
        board.SetCardPriority(card, "Low");

        board.MovePriority("Low", -3);
        board.SetPriorityColor("Low", "Purple");

        Assert.True(board.CanUndo);
        Assert.Equal("Purple", board.Priorities.ColorKey("Low"));
        Assert.Same(board.Priorities.BrushForColor("Purple"), card.PriorityBrush);
    });

    [Fact]
    public void ATaskWhosePriorityIsNotOnTheList_KeepsIt_AndSortsWithTheDefault() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Add(board, "odd one", "Someday");

        var card = Card(OpenBoard(), "odd one");

        Assert.Equal("Someday", card.Priority);
        Assert.Equal(board.Priorities.Rank("Normal"), card.PriorityRank);
    });

    [Fact]
    public void Import_TakesAPriorityOnTheList_WhateverItsCapitals_AndTheDefaultOtherwise() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPriority("Urgent");

        var cards = board.ImportCards(
        [
            new ImportedTaskRow { Title = "a", Priority = "URGENT" },
            new ImportedTaskRow { Title = "b", Priority = "Whenever" },
            new ImportedTaskRow { Title = "c" },
        ]);

        Assert.Equal(["Urgent", "Normal", "Normal"], cards.Select(c => c.Priority));
    });

    [Fact]
    public void QuickAdd_UnderstandsTheListsOwnNames() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPriority("Must Do");

        Assert.Equal("Must Do", board.ParseQuickAdd("call the bank !mustdo").Priority);
        Assert.Equal("Must Do", board.ParseQuickAdd("call the bank !mu").Priority);
        Assert.Equal("High", board.ParseQuickAdd("call the bank !h").Priority);
        Assert.Null(board.ParseQuickAdd("call the bank !m").Priority);       // Medium or Must Do? Neither.
        Assert.Equal("call the bank !m", board.ParseQuickAdd("call the bank !m").Title);
    });

    [Fact]
    public void TheDashboard_CountsByTheListsPriorities_AndPutsAnUnknownOneWithTheDefault() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPriority("Urgent");
        board.MovePriority("Urgent", -4);
        Add(board, "u", "Urgent"); Add(board, "n", "Normal"); Add(board, "odd", "Someday");

        var cards = board.Columns.SelectMany(c => c.Cards.Select(card => new DashboardData.BoardCard(card, c.Name, c.DisplayName))).ToList();
        var columns = board.Columns.Select(c => new DashboardData.Status(c.Name, c.DisplayName)).ToList();
        var data = DashboardData.Build(cards, columns, [], DateTime.Today, DayOfWeek.Sunday, board.Priorities);

        Assert.Equal(["Urgent", "High", "Medium", "Normal", "Low"], data.Priorities);
        Assert.Equal([1, 0, 0, 2, 0], data.StatusByPriority.First().Counts);
    });

    [Fact]
    public void Reports_SortAndGroupByPriority_InTheListsOrder() => wpf.Run(() =>
    {
        var board = OpenBoard();
        board.AddPriority("Urgent");
        board.MovePriority("Urgent", -4);
        Add(board, "low", "Low"); Add(board, "urgent", "Urgent"); Add(board, "high", "High");

        var rows = ReportService.BuildRows(board.Columns, board.Columns.Select(c => c.Name).ToHashSet(),
            [], [], [], "All", "All", "All", null, null, true, null, "Priority", "None", "None");

        Assert.Equal(["urgent", "high", "low"], rows.Select(r => r.Title));
        Assert.Equal(["Urgent", "High", "Low"], ReportService.GroupRows(rows, "Priority").Select(g => g.Key));
    });

    // ----- Column names and the letters on each card's move buttons -----

    [Fact]
    public void TheMoveButtons_StartWithTheStandardLetters() => wpf.Run(() =>
    {
        var board = OpenBoard();

        Assert.Equal(["T", "P", "H", "W"], new[] { board.ToDoColumn, board.InProgressColumn, board.OnHoldColumn, board.WaitingColumn }.Select(c => c!.QuickLetter));
        Assert.Equal("Move to In Progress", board.InProgressColumn!.MoveToolTip);
        Assert.Equal("Mark Done", board.DoneColumn!.MoveToolTip);
        Assert.Equal("", board.DoneColumn.QuickLetter); // Done's button is a tick
    });

    [Fact]
    public void RenamingAColumn_ChangesItsLetterAndTip_StraightAway_AndAfterReopening() => wpf.Run(() =>
    {
        var board = OpenBoard();

        board.RenameColumnDisplayName(board.ToDoColumn!, "Backlog");
        board.RenameColumnDisplayName(board.DoneColumn!, "Finished");

        Assert.Equal("B", board.ToDoColumn!.QuickLetter);
        Assert.Equal("Move to Backlog", board.ToDoColumn.MoveToolTip);
        Assert.Equal("Move to Finished", board.DoneColumn!.MoveToolTip);

        var reopened = OpenBoard();
        Assert.Equal("B", reopened.ToDoColumn!.QuickLetter);
        Assert.Equal("Backlog", reopened.QuickAddColumnName);
    });

    [Fact]
    public void ARenameThatClashes_MovesTheLaterColumnToAnotherLetter() => wpf.Run(() =>
    {
        var board = OpenBoard();

        board.RenameColumnDisplayName(board.OnHoldColumn!, "Parked");

        Assert.Equal("P", board.InProgressColumn!.QuickLetter); // In Progress comes first on the board
        Assert.Equal("A", board.OnHoldColumn!.QuickLetter);
    });

    [Fact]
    public void TheGtdNames_CanBeApplied_AndTakenBackByCancellingSettings() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var before = board.CaptureSettings();

        board.UseColumnNames(MainViewModel.GtdColumnNames);

        Assert.Equal(["Inbox", "Next Actions", "In Progress", "Waiting", "Done"], board.Columns.Select(c => c.DisplayName));
        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Done"], board.Columns.Select(c => c.Name)); // roles unchanged
        Assert.Equal(["I", "N", "P", "W"], board.Columns.Take(4).Select(c => c.QuickLetter));
        Assert.NotEqual(before, board.CaptureSettings());

        board.RestoreSettings(before);
        Assert.Equal(MainViewModel.StandardColumnNames, board.Columns.Select(c => c.DisplayName));
        Assert.Equal(["T", "P", "H", "W"], board.Columns.Take(4).Select(c => c.QuickLetter));
    });
}
