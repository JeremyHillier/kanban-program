using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Month view's columns and the Summary view's one bar per project or goal.
public sealed class TimelineSummaryTests
{
    private static readonly DateTime Today = new(2026, 10, 22);

    private static CardViewModel Task(DateTime? start, DateTime? due) =>
        new(new CardItem { Title = "T", StartDate = start, DueDate = due, Priority = "Normal" });

    private static List<DateTime> Months(DateTime start) => TimelineScale.UnitStarts(TimelineZoom.Month, start);

    [Fact]
    public void MonthView_IsSixCalendarMonths_FromThe1st_PagedThreeAtATime()
    {
        var start = TimelineScale.Snap(TimelineZoom.Month, Today);

        Assert.Equal(new DateTime(2026, 10, 1), start);
        Assert.Equal(6, Months(start).Count);
        Assert.Equal(new DateTime(2027, 3, 1), Months(start)[5]);
        Assert.Equal(new DateTime(2027, 4, 1), TimelineScale.End(TimelineZoom.Month, start));
        Assert.Equal(new DateTime(2027, 1, 1), TimelineScale.Step(TimelineZoom.Month, start, 1));
        Assert.Equal(new DateTime(2026, 7, 1), TimelineScale.Step(TimelineZoom.Month, start, -1));
    }

    [Fact]
    public void SwitchingBackFromMonths_SnapsToAMonday()
    {
        Assert.Equal(new DateTime(2026, 9, 28), TimelineScale.Snap(TimelineZoom.Week, new DateTime(2026, 10, 1)));
        Assert.Equal(TimelineZoom.Month, TimelineScale.Parse("Month"));
        Assert.Equal(TimelineZoom.Week, TimelineScale.Parse(null));
    }

    [Fact]
    public void InMonthView_ATaskRunsFromItsStartMonthToItsDueMonth()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var item = Assert.Single(TimelineLayout.Place([Task(new DateTime(2026, 10, 31), new DateTime(2026, 12, 1))], starts, new DateTime(2027, 4, 1)));

        Assert.Equal((0, 2), (item.FirstUnit, item.LastUnit));
        Assert.True(item.HasArrow);
    }

    [Fact]
    public void TheBar_RunsFromTheEarliestDateToTheLatest_AndCountsOverdueAndUndated()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var cards = new[]
        {
            Task(null, new DateTime(2026, 10, 15)),                    // overdue
            Task(new DateTime(2026, 11, 2), new DateTime(2027, 1, 20)),
            Task(null, null),                                          // undated
        };

        var bar = TimelineLayout.Summarise(cards, Today, starts, new DateTime(2027, 4, 1))!;

        Assert.Equal((new DateTime(2026, 10, 15), new DateTime(2027, 1, 20)), (bar.Start, bar.End));
        Assert.Equal((0, 3), (bar.FirstUnit, bar.LastUnit));
        Assert.Equal((3, 1, 1), (bar.OpenTasks, bar.Overdue, bar.Undated));
        Assert.Equal("3 open tasks · 1 overdue · 1 undated", bar.CountsText);
        Assert.Equal("Oct 15, 2026 – Jan 20, 2027", bar.DatesText);
    }

    [Fact]
    public void ABarRunningPastEitherEdge_IsClampedAndSaysSo()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var bar = TimelineLayout.Summarise([Task(new DateTime(2026, 8, 1), new DateTime(2027, 6, 1))], Today, starts, new DateTime(2027, 4, 1))!;

        Assert.Equal((0, 5), (bar.FirstUnit, bar.LastUnit));
        Assert.True(bar.StartsBeforeWindow);
        Assert.True(bar.EndsAfterWindow);
    }

    [Fact]
    public void NoBar_WhenNothingHasADate_OrItIsAllOutsideTheWindow()
    {
        var starts = Months(new DateTime(2026, 10, 1));

        Assert.Null(TimelineLayout.Summarise([Task(null, null)], Today, starts, new DateTime(2027, 4, 1)));
        Assert.Null(TimelineLayout.Summarise([Task(null, new DateTime(2027, 5, 1))], Today, starts, new DateTime(2027, 4, 1)));
        Assert.Null(TimelineLayout.Summarise([], Today, starts, new DateTime(2027, 4, 1)));
    }

    [Fact]
    public void ABarRunningPastTheTarget_TurnsRedFromTheTarget_AndSaysHowFar()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var cards = new[] { Task(null, new DateTime(2026, 11, 10)), Task(null, new DateTime(2027, 1, 12)) };

        var bar = TimelineLayout.Summarise(cards, Today, starts, new DateTime(2027, 4, 1), target: new DateTime(2026, 12, 1))!;

        Assert.Equal((1, 3), (bar.FirstUnit, bar.LastUnit));
        Assert.Equal(1d / 3, bar.OverrunFrom!.Value, 3); // the bar covers Nov, Dec, Jan; red starts where December does
        Assert.Equal("ends 6 weeks after target", bar.OverrunText);
    }

    [Fact]
    public void ABarEndingOnOrBeforeTheTarget_IsNotFlagged()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var bar = TimelineLayout.Summarise([Task(null, new DateTime(2026, 12, 1))], Today, starts, new DateTime(2027, 4, 1), target: new DateTime(2026, 12, 1))!;

        Assert.Null(bar.OverrunFrom);
        Assert.Equal(string.Empty, bar.OverrunText);
    }

    [Fact]
    public void ATargetBeforeTheWindow_MakesTheWholeBarRed_AndOneBeyondItNone()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var cards = new[] { Task(null, new DateTime(2026, 11, 20)) };

        Assert.Equal(0d, TimelineLayout.Summarise(cards, Today, starts, new DateTime(2027, 4, 1), target: new DateTime(2026, 9, 1))!.OverrunFrom);
        Assert.Null(TimelineLayout.Summarise(cards, Today, starts, new DateTime(2027, 4, 1), target: new DateTime(2027, 9, 1))!.OverrunFrom);
        Assert.Equal("ends 1 day after target", TimelineLayout.Summarise([Task(null, new DateTime(2026, 11, 2))], Today, starts, new DateTime(2027, 4, 1), target: new DateTime(2026, 11, 1))!.OverrunText);
    }

    [Fact]
    public void TheTargetMarker_SitsInItsMonth_PartWayAcrossIt()
    {
        var starts = Months(new DateTime(2026, 10, 1));
        var position = TimelineLayout.TargetPosition(new DateTime(2026, 11, 16), starts, new DateTime(2027, 4, 1))!.Value;

        Assert.Equal(1, position.Unit);
        Assert.Equal(15d / 30, position.Fraction, 3);
        Assert.Null(TimelineLayout.TargetPosition(new DateTime(2027, 4, 1), starts, new DateTime(2027, 4, 1)));
        Assert.Null(TimelineLayout.TargetPosition(new DateTime(2026, 9, 30), starts, new DateTime(2027, 4, 1)));
    }

    [Theory]
    [InlineData(12, 6, "6 Months")]
    [InlineData(24, 12, "12 Months")]
    [InlineData(3, 1, "1 Month")]
    public void MonthView_CanShowMoreMonths_PagingHalfOfThem(int months, int step, string label)
    {
        var start = new DateTime(2026, 10, 1);

        Assert.Equal(months, TimelineScale.UnitStarts(TimelineZoom.Month, start, months).Count);
        Assert.Equal(start.AddMonths(months), TimelineScale.End(TimelineZoom.Month, start, months));
        Assert.Equal(start.AddMonths(step), TimelineScale.Step(TimelineZoom.Month, start, 1, months));
        Assert.Equal(label, TimelineScale.StepLabel(TimelineZoom.Month, months));
    }

    [Fact]
    public void AnUnknownNumberOfMonths_IsTheDefaultSix()
    {
        Assert.Equal(6, TimelineScale.ValidMonths(7));
        Assert.Equal(18, TimelineScale.ValidMonths(18));
    }
}
