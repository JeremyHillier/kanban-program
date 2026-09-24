using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// Every field a task has is written when the task is added, written again when it is edited, and
// comes back the same from the task file both times - including clearing each optional field.
public sealed class CardFieldsRoundTripTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CardItem Reload(DatabaseService db, int id) => db.GetCards().Single(c => c.Id == id);

    [Fact]
    public void AddingATask_WritesEveryField_AndTheReturnedTaskMatchesTheFile()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var column = db.GetColumns().First(c => c.Name == "In Progress");
        var project = db.AddProject("Home");
        var goal = db.AddGoal("Grow");
        var person = db.AddPerson("Sam");

        var added = db.AddCard(column.Id, "Paint the fence", project.Id, column.Name, "High", new DateTime(2026, 10, 3), person.Id,
            true, "Weekly", goal.Id, "Two coats", isImported: true, forceEditOnComplete: true, websiteUrl: "https://example.com",
            dueTime: "14:30", startDate: new DateTime(2026, 10, 1), waitingOn: "the paint", recurrencesLeft: 4);

        foreach (var card in new[] { added, Reload(db, added.Id) })
        {
            Assert.Equal(column.Id, card.ColumnId);
            Assert.Equal("Paint the fence", card.Title);
            Assert.Equal(project.Id, card.ProjectId);
            Assert.Equal("High", card.Priority);
            Assert.Equal(new DateTime(2026, 10, 3), card.DueDate);
            Assert.Equal(person.Id, card.WhoId);
            Assert.True(card.IsRecurring);
            Assert.Equal("Weekly", card.RecurrencePattern);
            Assert.Equal(goal.Id, card.GoalId);
            Assert.Equal("Two coats", card.Notes);
            Assert.True(card.IsImported);
            Assert.True(card.ForceEditOnComplete);
            Assert.Equal("https://example.com", card.WebsiteUrl);
            Assert.Equal("14:30", card.DueTime);
            Assert.Equal(new DateTime(2026, 10, 1), card.StartDate);
            Assert.Equal("the paint", card.WaitingOn);
            Assert.Equal(4, card.RecurrencesLeft);
            Assert.Null(card.CompletedAt);
            Assert.NotNull(card.LastUpdated);
        }
        Assert.Equal(added.SortOrder, Reload(db, added.Id).SortOrder);
    }

    [Fact]
    public void AddingStraightIntoDone_CountsAsCompleted_AndSortOrderFollowsTheColumn()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var done = db.GetColumns().First(c => c.Name == "Done");

        var first = db.AddCard(done.Id, "One", null, done.Name, "Normal", null, null, false, null, null);
        var second = db.AddCard(done.Id, "Two", null, done.Name, "Normal", null, null, false, null, null);

        Assert.NotNull(first.CompletedAt);
        Assert.Equal(first.CompletedAt, Reload(db, first.Id).CompletedAt);
        Assert.Equal(first.SortOrder + 1, second.SortOrder);
    }

    [Fact]
    public void EditingATask_RewritesEveryField_AndCanClearEachOptionalOne()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var column = db.GetColumns().First();
        var project = db.AddProject("Home");
        var goal = db.AddGoal("Grow");
        var person = db.AddPerson("Sam");
        var card = db.AddCard(column.Id, "Old", null, column.Name, "Normal", null, null, false, null, null);

        db.UpdateCard(card.Id, "New title", project.Id, "Low", new DateTime(2026, 12, 1), person.Id, true, "Monthly", goal.Id,
            "Some notes", forceEditOnComplete: true, websiteUrl: "mailto:sam@example.com", dueTime: "09:00",
            startDate: new DateTime(2026, 11, 20), waitingOn: "a reply", recurrencesLeft: 2);

        var edited = Reload(db, card.Id);
        Assert.Equal(("New title", project.Id, "Low", new DateTime(2026, 12, 1), person.Id), (edited.Title, edited.ProjectId, edited.Priority, edited.DueDate, edited.WhoId));
        Assert.Equal((true, "Monthly", goal.Id, "Some notes", true), (edited.IsRecurring, edited.RecurrencePattern, edited.GoalId, edited.Notes, edited.ForceEditOnComplete));
        Assert.Equal(("mailto:sam@example.com", "09:00", new DateTime(2026, 11, 20), "a reply", 2), (edited.WebsiteUrl, edited.DueTime, edited.StartDate, edited.WaitingOn, edited.RecurrencesLeft));
        Assert.Equal(column.Id, edited.ColumnId); // editing never moves it

        db.UpdateCard(card.Id, "New title", null, "Normal", null, null, false, null, null);

        var cleared = Reload(db, card.Id);
        Assert.Null(cleared.ProjectId);
        Assert.Null(cleared.DueDate);
        Assert.Null(cleared.WhoId);
        Assert.False(cleared.IsRecurring);
        Assert.Null(cleared.RecurrencePattern);
        Assert.Null(cleared.GoalId);
        Assert.Null(cleared.Notes);
        Assert.False(cleared.ForceEditOnComplete);
        Assert.Null(cleared.WebsiteUrl);
        Assert.Null(cleared.DueTime);
        Assert.Null(cleared.StartDate);
        Assert.Null(cleared.WaitingOn);
        Assert.Null(cleared.RecurrencesLeft);
    }
}
