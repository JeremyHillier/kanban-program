using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Where tasks land on a Timeline row: the box at the start date, a date line from the start day to
// the due day, and lanes so nothing overlaps. The window in these tests is 12 weekly columns from Mon 5 Oct 2026.
public sealed class TimelineLayoutTests
{
    private static readonly DateTime WindowStart = new(2026, 10, 5);
    private const int Weeks = 12;

    private static CardViewModel Task(string title, DateTime? start, DateTime? due) =>
        new(new CardItem { Title = title, StartDate = start, DueDate = due, Priority = "Normal" });

    private static List<TimelineItem> PlaceWeekly(params CardViewModel[] cards) => TimelineLayout.Place(cards, WindowStart, 7, Weeks);

    [Fact]
    public void ATaskWithNoStartDate_IsJustABoxInItsDueWeek()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", null, new DateTime(2026, 10, 21))));

        Assert.Equal(2, item.BoxUnit);
        Assert.False(item.HasLine);
        Assert.False(item.DueAfterWindow);
    }

    [Fact]
    public void AStartDate_PutsTheBoxAtTheStart_WithALineToTheDueDay()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 10, 6), new DateTime(2026, 11, 5))));

        Assert.Equal((0, 4), (item.FirstUnit, item.LastUnit));
        Assert.Equal(0, item.BoxUnit);
        Assert.True(item.HasLine);
        Assert.False(item.StartsBeforeWindow);
    }

    // The line runs from the start of the start day to the end of the due day, to the day, in Week view
    // too: Tue Oct 6 is a seventh into week 0, and the end of Thu Nov 5 is four sevenths into week 4.
    [Fact]
    public void TheLine_MatchesTheStartAndDueDays_NotJustTheirWeeks()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 10, 6), new DateTime(2026, 11, 5))));

        Assert.Equal(1 / 7d, item.StartAt, 6);
        Assert.Equal(4 + 4 / 7d, item.EndAt, 6);
    }

    [Fact]
    public void StartAndDueInTheSameWeek_StillGetALine_FromMondayToTheEndOfFriday()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 10, 5), new DateTime(2026, 10, 9))));

        Assert.True(item.HasLine);
        Assert.Equal(0, item.BoxUnit);
        Assert.Equal((0d, 5 / 7d), (item.StartAt, Math.Round(item.EndAt, 6)), new TupleTolerance());
    }

    [Fact]
    public void StartAndDueOnTheSameDay_IsJustABox() =>
        Assert.False(Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 10, 7), new DateTime(2026, 10, 7)))).HasLine);

    private sealed class TupleTolerance : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) a, (double, double) b) => Math.Abs(a.Item1 - b.Item1) < 1e-6 && Math.Abs(a.Item2 - b.Item2) < 1e-6;
        public int GetHashCode((double, double) value) => 0;
    }

    [Fact]
    public void AStartBeforeTheWindow_PutsTheBoxInTheFirstColumn()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 9, 1), new DateTime(2026, 10, 21))));

        Assert.True(item.StartsBeforeWindow);
        Assert.True(item.HasLine);
        Assert.Equal(0, item.StartAt); // from the left edge
        Assert.Equal(0, item.BoxUnit);
    }

    [Fact]
    public void ADuePastTheWindow_HasTheArrowRunningToTheLastColumn()
    {
        var item = Assert.Single(PlaceWeekly(Task("A", new DateTime(2026, 12, 8), new DateTime(2027, 3, 1))));

        Assert.True(item.DueAfterWindow);
        Assert.Equal(9, item.BoxUnit);
        Assert.Equal((9, 11), (item.FirstUnit, item.LastUnit));
        Assert.Equal(12, item.EndAt); // off the right edge
    }

    [Fact]
    public void TasksOutsideTheWindow_AreLeftOut_ButOneSpanningAllOfItIsShown()
    {
        var items = PlaceWeekly(
            Task("Before", new DateTime(2026, 8, 1), new DateTime(2026, 10, 4)),
            Task("After", new DateTime(2026, 12, 28), new DateTime(2027, 1, 5)),
            Task("No dates", null, null),
            Task("Right across", new DateTime(2026, 9, 1), new DateTime(2027, 2, 1)));

        var item = Assert.Single(items);
        Assert.Equal("Right across", item.Card.Title);
        Assert.True(item.StartsBeforeWindow && item.DueAfterWindow);
        Assert.Equal((0, 11), (item.FirstUnit, item.LastUnit));
    }

    [Fact]
    public void ATaskWithOnlyAStartDate_IsABoxAtItsStart()
    {
        var card = Task("A", new DateTime(2026, 10, 14), null);

        var item = Assert.Single(PlaceWeekly(card));

        Assert.Equal(1, item.BoxUnit);
        Assert.False(item.HasLine);
        Assert.Equal("starts Oct 14", TimelineLayout.DateLabel(card));
    }

    [Fact]
    public void OverlappingTasks_GetTheirOwnLanes_AndNonOverlappingOnesShare()
    {
        var items = PlaceWeekly(
            Task("Long", new DateTime(2026, 10, 5), new DateTime(2026, 11, 20)),      // weeks 0-6
            Task("Overlaps", new DateTime(2026, 10, 20), new DateTime(2026, 12, 1)),  // weeks 2-8
            Task("After long", new DateTime(2026, 11, 24), new DateTime(2026, 12, 20)), // weeks 7-10
            Task("Single in week 3", null, new DateTime(2026, 10, 28)));

        int LaneOf(string title) => items.Single(i => i.Card.Title == title).Lane;
        Assert.Equal(0, LaneOf("Long"));
        Assert.Equal(1, LaneOf("Overlaps"));
        Assert.Equal(0, LaneOf("After long")); // fits after Long in the same lane
        Assert.Equal(2, LaneOf("Single in week 3")); // weeks 3 is taken in lanes 0 and 1

        foreach (var lane in items.GroupBy(i => i.Lane))
        {
            var spans = lane.OrderBy(i => i.FirstUnit).ToList();
            for (var i = 1; i < spans.Count; i++) Assert.True(spans[i].FirstUnit > spans[i - 1].LastUnit);
        }
    }

    [Fact]
    public void SeveralBoxesDueTheSameWeek_StackInLanes()
    {
        var due = new DateTime(2026, 10, 7);
        var items = PlaceWeekly(Task("B", null, due), Task("A", null, due), Task("C", null, due));

        Assert.Equal(["A", "B", "C"], items.OrderBy(i => i.Lane).Select(i => i.Card.Title));
        Assert.Equal([0, 1, 2], items.Select(i => i.Lane).Order());
    }

    [Fact]
    public void DayView_PlacesToTheDay()
    {
        var item = Assert.Single(TimelineLayout.Place([Task("A", new DateTime(2026, 10, 7), new DateTime(2026, 10, 12))], WindowStart, 1, 21));

        Assert.Equal((2, 7), (item.FirstUnit, item.LastUnit));
        Assert.Equal((2d, 8d), (item.StartAt, item.EndAt)); // the start of Oct 7 to the end of Oct 12
        Assert.Equal(2, item.BoxUnit);
    }

    [Fact]
    public void InWindow_CountsATaskWhoseStartToDueSpanTouchesTheRange()
    {
        var end = WindowStart.AddDays(7 * Weeks);

        Assert.True(TimelineLayout.IsInWindow(Task("due later, started inside", new DateTime(2026, 12, 1), new DateTime(2027, 6, 1)), WindowStart, end));
        Assert.True(TimelineLayout.IsInWindow(Task("last day", null, end.AddDays(-1)), WindowStart, end));
        Assert.False(TimelineLayout.IsInWindow(Task("first day after", null, end), WindowStart, end));
        Assert.False(TimelineLayout.IsInWindow(Task("none", null, null), WindowStart, end));
    }
}
