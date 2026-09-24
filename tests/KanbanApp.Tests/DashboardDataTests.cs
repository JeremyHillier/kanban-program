using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using static KanbanApp.Services.DashboardData;

namespace KanbanApp.Tests;

// The Dashboard's numbers, worked out without drawing anything. "Today" is fixed so the date
// groups are tested at their edges.
public sealed class DashboardDataTests
{
    private static readonly DateTime Today = new(2026, 9, 24); // a Thursday

    private static readonly Status[] Columns =
    [
        new("To Do", "To Do"), new("In Progress", "Doing"), new("On Hold", "On Hold"), new("Waiting", "Waiting"), new("Done", "Done")
    ];

    private static int _nextId;

    private static BoardCard Card(string column = "To Do", DateTime? due = null, string priority = "Normal", string project = "General",
        string[]? people = null, DateTime? updated = null, DateTime? completed = null, string? waitingOn = null) =>
        new(new CardViewModel(new CardItem
        {
            Id = ++_nextId, Title = "Task", Priority = priority, DueDate = due, LastUpdated = updated ?? Today, CompletedAt = completed, WaitingOn = waitingOn
        })
        {
            ProjectName = project,
            People = (people ?? []).Select(name => new PersonViewModel(new Person { Id = name.GetHashCode(), Name = name })).ToList()
        }, column, Columns.Single(c => c.Name == column).DisplayName);

    private static DashboardData Build(IReadOnlyList<BoardCard> cards, IEnumerable<DateTime>? archived = null, DayOfWeek firstDay = DayOfWeek.Sunday) =>
        DashboardData.Build(cards, Columns, archived ?? [], Today, firstDay);

    [Fact]
    public void TheTiles_CountOpenWorkAndWhatNeedsAttention()
    {
        var data = Build(
        [
            Card(due: Today.AddDays(-1)),
            Card(due: Today, waitingOn: "the client"),
            Card("In Progress", due: Today.AddDays(7)),
            Card("Waiting", due: Today.AddDays(8)),
            Card("Done", due: Today.AddDays(-5), completed: Today.AddDays(-2)), // done: not overdue, not open
        ]);

        Assert.Equal(4, data.Open);
        Assert.Equal(1, data.Overdue);
        Assert.Equal(1, data.DueToday);
        Assert.Equal(2, data.DueThisWeek); // today and 7 days out; 8 days out is not this week
        Assert.Equal(1, data.WaitingOn);
        Assert.Equal(1, data.InDone);
    }

    [Fact]
    public void DueDates_FallIntoGroups_AtTheirEdges()
    {
        var data = Build(
        [
            Card(due: Today.AddDays(-30)), Card(due: Today.AddDays(-1)),
            Card(due: Today),
            Card(due: Today.AddDays(1)), Card(due: Today.AddDays(7)),
            Card(due: Today.AddDays(8)), Card(due: Today.AddDays(30)),
            Card(due: Today.AddDays(31)),
            Card(), Card(),
            Card("Done", due: Today.AddDays(-3)), // done tasks are left out
        ]);

        Assert.Equal(["Overdue", "Today", "Next 7 days", "8–30 days", "Later", "No due date"], data.DueDates.Select(b => b.Label));
        Assert.Equal([2, 1, 2, 2, 1, 2], data.DueDates.Select(b => b.Count));
    }

    [Fact]
    public void CompletedPerWeek_CountsDoneAndArchivedTasks_ByTheWeekTheyFinished()
    {
        var data = Build(
            [
                Card("Done", completed: Today),                  // this week (Sun Sep 20 onwards)
                Card("Done", completed: new DateTime(2026, 9, 20, 8, 0, 0)),
                Card("Done", completed: new DateTime(2026, 9, 19)), // last week
                Card("To Do", completed: Today),                 // reopened: no longer counts
            ],
            archived: [new DateTime(2026, 9, 14, 17, 30, 0), new DateTime(2026, 6, 1)]); // last week, and long ago

        Assert.Equal(WeeksShown, data.CompletedPerWeek.Count);
        Assert.Equal(new DateTime(2026, 9, 20), data.CompletedPerWeek[^1].Start);
        Assert.Equal(2, data.CompletedPerWeek[^1].Count);
        Assert.Equal(new DateTime(2026, 9, 13), data.CompletedPerWeek[^2].Start);
        Assert.Equal(2, data.CompletedPerWeek[^2].Count);
        Assert.Equal(4, data.CompletedPerWeek.Sum(w => w.Count)); // June is outside the 12 weeks
        Assert.Equal(new DateTime(2026, 7, 5), data.CompletedPerWeek[0].Start);
    }

    [Fact]
    public void Weeks_StartOnTheDayTheComputerIsSetTo()
    {
        var data = Build([Card("Done", completed: new DateTime(2026, 9, 20))], firstDay: DayOfWeek.Monday);

        Assert.Equal(new DateTime(2026, 9, 21), data.CompletedPerWeek[^1].Start); // Monday
        Assert.Equal(0, data.CompletedPerWeek[^1].Count);
        Assert.Equal(1, data.CompletedPerWeek[^2].Count); // Sunday belongs to the week before
    }

    [Fact]
    public void DoneInTheLast7Days_CountsTodayAndTheSixDaysBefore()
    {
        var data = Build([Card("Done", completed: Today.AddDays(-6)), Card("Done", completed: Today)],
            archived: [Today.AddDays(-7), Today.AddDays(-1)]);

        Assert.Equal(3, data.DoneLast7Days);
    }

    [Fact]
    public void LastUpdated_GroupsOpenTasksByHowLongTheyHaveSat()
    {
        var data = Build(
        [
            Card(updated: Today.AddDays(-6)),
            Card(updated: Today.AddDays(-7)), Card(updated: Today.AddDays(-29)),
            Card(updated: Today.AddDays(-30)), Card(updated: Today.AddDays(-89)),
            Card(updated: Today.AddDays(-90)),
            Card("Done", updated: Today.AddDays(-200)),
        ]);

        Assert.Equal([1, 2, 2, 1], data.LastUpdated.Select(b => b.Count));
    }

    [Fact]
    public void StatusByPriority_HasABarPerColumn_SplitHighToLow()
    {
        var data = Build([Card(priority: "High"), Card(priority: "High"), Card(priority: "Low"), Card("In Progress", priority: "Medium")]);

        Assert.Equal(["To Do", "Doing", "On Hold", "Waiting", "Done"], data.StatusByPriority.Select(s => s.Label)); // shown names
        Assert.Equal([2, 0, 0, 1], data.StatusByPriority[0].Counts);
        Assert.Equal([0, 1, 0, 0], data.StatusByPriority[1].Counts);
    }

    [Fact]
    public void Projects_AreBiggestFirst_SplitByColumn()
    {
        var data = Build([Card(project: "Home"), Card("Done", project: "Work"), Card("In Progress", project: "Work"), Card(project: "Admin")]);

        Assert.Equal(["Work", "Admin", "Home"], data.ProjectsByStatus.Select(p => p.Label));
        Assert.Equal([0, 1, 0, 0, 1], data.ProjectsByStatus[0].Counts);
        Assert.Equal(0, data.FoldedProjectCount);
    }

    [Fact]
    public void ManyProjects_KeepTheBiggest_AndGroupTheRestAsOther()
    {
        var cards = Enumerable.Range(1, 25).SelectMany(i => Enumerable.Range(0, 30 - i).Select(_ => Card(project: $"P{i:00}"))).ToList();

        var data = Build(cards);

        Assert.Equal(MaxProjectRows, data.ProjectsByStatus.Count);
        Assert.Equal("P19", data.ProjectsByStatus[^2].Label);
        Assert.Equal("Other (6 projects)", data.ProjectsByStatus[^1].Label);
        Assert.Equal(Enumerable.Range(20, 6).Sum(i => 30 - i), data.ProjectsByStatus[^1].Total);
        Assert.Equal(6, data.FoldedProjectCount);
    }

    [Fact]
    public void People_CountOpenTasksOnly_ASharedTaskForEachPerson_AndUnassignedLast()
    {
        var data = Build(
        [
            Card(people: ["Sam", "Lee"]),
            Card("Waiting", people: ["Sam"]),
            Card(),
            Card("Done", people: ["Lee"]),
        ]);

        Assert.Equal(["Sam", "Lee", "Unassigned"], data.PeopleByStatus.Select(p => p.Label));
        Assert.Equal([1, 0, 0, 1], data.PeopleByStatus[0].Counts); // To Do, In Progress, On Hold, Waiting - no Done
        Assert.Equal(1, data.PeopleByStatus[1].Total);
        Assert.Equal(4, data.OpenStatuses.Count);
    }

    [Fact]
    public void AnEmptyBoard_GivesZeros_NotErrors()
    {
        var data = Build([]);

        Assert.Equal(0, data.Open);
        Assert.All(data.DueDates, b => Assert.Equal(0, b.Count));
        Assert.Equal(WeeksShown, data.CompletedPerWeek.Count);
        Assert.Empty(data.ProjectsByStatus);
        Assert.Empty(data.PeopleByStatus);
    }
}
