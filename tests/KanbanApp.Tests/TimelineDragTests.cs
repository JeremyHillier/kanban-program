using KanbanApp.Services;

namespace KanbanApp.Tests;

// Turning a place on the Timeline back into a day, which is what dragging a date needs.
public sealed class TimelineDragTests
{
    [Fact]
    public void Week_EachColumnIsSevenDays_AndDayAtIsTheOppositeOfOffset()
    {
        var start = new DateTime(2026, 10, 12); // a Monday
        var units = TimelineScale.UnitStarts(TimelineZoom.Week, start);
        var end = TimelineScale.End(TimelineZoom.Week, start);

        for (var d = 0; d < 84; d++)
        {
            var day = start.AddDays(d);
            Assert.Equal(day, TimelineLayout.DayAt(TimelineLayout.Offset(day, units, end), units, end));
            Assert.Equal(day, TimelineLayout.DayAt(TimelineLayout.EndOffset(day, units, end) - 1e-4, units, end)); // an end of a day belongs to that day
        }
    }

    [Fact]
    public void Months_AreMeasuredOnTheirOwn()
    {
        var start = new DateTime(2026, 10, 1);
        var units = TimelineScale.UnitStarts(TimelineZoom.Month, start, 6);
        var end = TimelineScale.End(TimelineZoom.Month, start, 6);

        foreach (var day in new[] { new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), new DateTime(2026, 11, 30), new DateTime(2027, 2, 28), new DateTime(2027, 3, 31) })
            Assert.Equal(day, TimelineLayout.DayAt(TimelineLayout.Offset(day, units, end), units, end));
    }

    [Fact]
    public void ADragPastEitherEdge_StaysInsideTheDaysShowing()
    {
        var start = new DateTime(2026, 10, 12);
        var units = TimelineScale.UnitStarts(TimelineZoom.Week, start);
        var end = TimelineScale.End(TimelineZoom.Week, start);

        Assert.Equal(start, TimelineLayout.DayAt(-3.5, units, end));
        Assert.Equal(end.AddDays(-1), TimelineLayout.DayAt(units.Count + 4, units, end));
    }

    [Fact]
    public void ADragNearAnEdge_PicksTheNearestDay_EitherSideOfTheLine()
    {
        var start = new DateTime(2026, 10, 12);
        var units = TimelineScale.UnitStarts(TimelineZoom.Week, start);
        var end = TimelineScale.End(TimelineZoom.Week, start);
        var oct20 = new DateTime(2026, 10, 20);

        foreach (var nudge in new[] { -0.02, -0.001, 0, 0.001, 0.02 })
        {
            // Sitting on (or a hair either side of) the end of Oct 20: it is Oct 20's end that is nearest.
            Assert.Equal(oct20, TimelineLayout.DayNear(TimelineLayout.EndOffset(oct20, units, end) + nudge, endOfDay: true, units, end));
            // And on the start of Oct 20, the start of Oct 20.
            Assert.Equal(oct20, TimelineLayout.DayNear(TimelineLayout.Offset(oct20, units, end) + nudge, endOfDay: false, units, end));
        }
    }

    [Fact]
    public void DayNear_StaysInsideTheWindow()
    {
        var start = new DateTime(2026, 10, 12);
        var units = TimelineScale.UnitStarts(TimelineZoom.Week, start);
        var end = TimelineScale.End(TimelineZoom.Week, start);

        Assert.Equal(start, TimelineLayout.DayNear(-5, true, units, end));
        Assert.Equal(start, TimelineLayout.DayNear(-5, false, units, end));
        Assert.Equal(end.AddDays(-1), TimelineLayout.DayNear(units.Count + 5, true, units, end));
        Assert.Equal(end.AddDays(-1), TimelineLayout.DayNear(units.Count + 5, false, units, end));
    }
}
