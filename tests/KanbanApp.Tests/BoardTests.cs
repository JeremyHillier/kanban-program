using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Exercises the real board view model against a throwaway task file.
[Collection(WpfCollection.Name)]
public sealed class BoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Add(MainViewModel board, string title, string column = "To Do", string priority = "Normal",
        DateTime? due = null, string? dueTime = null, string? notes = null) =>
        board.AddCard(title, Column(board, column), board.Projects.First(), priority, due, null, false, null, null,
            notes: notes, dueTime: dueTime);

    [Fact]
    public void PriorityFilter_ShowsOnlyTheChosenPriorities() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var high = Add(board, "Call supplier", priority: "High");
        var medium = Add(board, "Order stock", priority: "Medium");
        var low = Add(board, "Tidy desk", priority: "Low");

        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.PriorityFilterOptions.Single(o => o.Name == "Low").IsSelected = true;
        board.ApplyFilters();

        Assert.True(high.IsVisible);
        Assert.False(medium.IsVisible);
        Assert.True(low.IsVisible);

        board.ClearFilters();
        Assert.All([high, medium, low], card => Assert.True(card.IsVisible));
    });

    [Fact]
    public void KeywordFilter_SearchesTitlesAndNotes() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var byTitle = Add(board, "Call supplier");
        var byNotes = Add(board, "Tidy desk", notes: "Supplier invoices are in the drawer");
        var neither = Add(board, "Book dentist");

        board.KeywordFilter = "supplier";

        Assert.True(byTitle.IsVisible);
        Assert.True(byNotes.IsVisible);
        Assert.False(neither.IsVisible);
    });

    [Fact]
    public void DueShortcuts_TodayIncludesOverdue_AndNoDueDateShowsOnlyUndated() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var overdue = Add(board, "Overdue", due: DateTime.Today.AddDays(-3));
        var today = Add(board, "Today", due: DateTime.Today);
        var tomorrow = Add(board, "Tomorrow", due: DateTime.Today.AddDays(1));
        var undated = Add(board, "Someday");

        board.ShowDueFilterOnly("Today");
        Assert.Equal([true, true, false, false], new[] { overdue, today, tomorrow, undated }.Select(c => c.IsVisible));

        board.ShowDueFilterOnly("No Due Date");
        Assert.Equal([false, false, false, true], new[] { overdue, today, tomorrow, undated }.Select(c => c.IsVisible));
    });

    [Fact]
    public void DueShortcut_ReplacesOtherFiltersInsteadOfStackingOnThem() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var lowToday = Add(board, "Low today", priority: "Low", due: DateTime.Today);
        board.PriorityFilterOptions.Single(o => o.Name == "High").IsSelected = true;
        board.ApplyFilters();
        Assert.False(lowToday.IsVisible);

        board.ShowDueFilterOnly("Today");

        Assert.True(lowToday.IsVisible);
    });

    [Fact]
    public void CompletingARecurringTask_CreatesTheNextOccurrenceOnce() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var toDo = Column(board, "To Do");
        var done = Column(board, "Done");
        var subTasks = new List<SubTaskViewModel>
        {
            new(new SubTaskItem { Title = "Stretch", IsDone = true }),
            new(new SubTaskItem { Title = "Run" })
        };
        var card = board.AddCard("Exercise", toDo, board.Projects.First(), "High", new DateTime(2026, 9, 14), null,
            true, "Weekly", null, subTasks: subTasks, notes: "Park loop", dueTime: "07:30");

        board.MoveCardCommand.Execute((card, done));

        var next = Assert.Single(toDo.Cards);
        Assert.Equal("Exercise", next.Title);
        Assert.Equal(new DateTime(2026, 9, 21), next.DueDate);
        Assert.Equal("07:30", next.DueTime);
        Assert.Equal("High", next.Priority);
        Assert.Equal("Park loop", next.Notes);
        Assert.Equal(["Stretch", "Run"], next.SubTasks.Select(s => s.Title));
        Assert.All(next.SubTasks, s => Assert.False(s.IsDone));
        Assert.True(card.NextOccurrenceSpawned);

        // Reopened and completed again: its next occurrence already exists, so no duplicate.
        board.MoveCardCommand.Execute((card, toDo));
        board.MoveCardCommand.Execute((card, done));
        Assert.Single(board.Columns.SelectMany(c => c.Cards), c => c.DueDate == new DateTime(2026, 9, 21));
    });

    [Fact]
    public void NewOccurrence_IsSavedToTheTaskFile() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Pay rent", Column(board, "To Do"), board.Projects.First(), "Normal",
            new DateTime(2026, 9, 1), null, true, "Monthly", null, dueTime: "09:00");
        board.MoveCardCommand.Execute((card, Column(board, "Done")));

        var reopened = OpenBoard();

        var next = Assert.Single(Column(reopened, "To Do").Cards);
        Assert.Equal(new DateTime(2026, 10, 1), next.DueDate);
        Assert.Equal("09:00", next.DueTime);
        Assert.True(Assert.Single(Column(reopened, "Done").Cards).NextOccurrenceSpawned);
    });

    [Fact]
    public void TimeAlerts_OnlyPickUpTimedTasksThatArePastAndNotDone() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var past = Add(board, "Past", due: DateTime.Today.AddDays(-1), dueTime: "09:00");
        Add(board, "Future", due: DateTime.Today.AddDays(1), dueTime: "09:00");
        Add(board, "No time", due: DateTime.Today.AddDays(-1));
        Add(board, "Already done", column: "Done", due: DateTime.Today.AddDays(-1), dueTime: "09:00");

        Assert.Equal([past], board.GetCardsPastDueTime());
    });

    [Fact]
    public void ClearingADueDate_AlsoClearsItsTime() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = Add(board, "Dentist", due: DateTime.Today.AddDays(2), dueTime: "15:00");

        board.SetCardDueDate(card, null);

        Assert.Null(card.DueTime);
        Assert.Null(Assert.Single(OpenBoard().Columns.SelectMany(c => c.Cards)).DueTime);
    });
}
