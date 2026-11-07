using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The dates the Calendar view shows, and what it remembers.
public sealed class CalendarLayoutTests
{
    [Fact]
    public void AMonth_ShowsWholeWeeks_FromTheMondayBeforeItsFirstDay()
    {
        // November 2026 starts on a Sunday and ends on a Monday.
        var (start, end) = CalendarLayout.Range(CalendarScale.Month, new DateTime(2026, 11, 15));

        Assert.Equal(new DateTime(2026, 10, 26), start);
        Assert.Equal(new DateTime(2026, 12, 7), end);
        Assert.Equal(6, CalendarLayout.Weeks(start, end, weekends: true).Count);
    }

    [Fact]
    public void AWeek_IsMondayToSunday_AndADay_IsOneDay()
    {
        Assert.Equal((new DateTime(2026, 11, 2), new DateTime(2026, 11, 9)), CalendarLayout.Range(CalendarScale.Week, new DateTime(2026, 11, 7)));
        Assert.Equal((new DateTime(2026, 11, 7), new DateTime(2026, 11, 8)), CalendarLayout.Range(CalendarScale.Day, new DateTime(2026, 11, 7, 15, 30, 0)));
    }

    [Fact]
    public void WithoutWeekends_EveryWeekHasFiveDays_AndNoSaturdayOrSunday()
    {
        var (start, end) = CalendarLayout.Range(CalendarScale.Month, new DateTime(2026, 11, 1));

        var weeks = CalendarLayout.Weeks(start, end, weekends: false);

        Assert.All(weeks, w => Assert.Equal(5, w.Count));
        Assert.All(weeks.SelectMany(w => w), d => Assert.NotEqual(DayOfWeek.Saturday, d.DayOfWeek));
        Assert.All(weeks.SelectMany(w => w), d => Assert.NotEqual(DayOfWeek.Sunday, d.DayOfWeek));
    }

    [Fact]
    public void TheArrows_StepAMonthAWeekOrADay()
    {
        Assert.Equal(new DateTime(2026, 12, 1), CalendarLayout.Step(CalendarScale.Month, new DateTime(2026, 11, 30), 1));
        Assert.Equal(new DateTime(2026, 2, 1), CalendarLayout.Step(CalendarScale.Month, new DateTime(2026, 3, 31), -1));
        Assert.Equal(new DateTime(2026, 11, 14), CalendarLayout.Step(CalendarScale.Week, new DateTime(2026, 11, 7), 1));
        Assert.Equal(new DateTime(2026, 11, 6), CalendarLayout.Step(CalendarScale.Day, new DateTime(2026, 11, 7), -1));
    }

    [Fact]
    public void TheTitle_NamesTheMonthTheWeekOrTheDay()
    {
        Assert.Equal("November 2026", CalendarLayout.Title(CalendarScale.Month, new DateTime(2026, 11, 7)));
        Assert.Contains("Nov 2", CalendarLayout.Title(CalendarScale.Week, new DateTime(2026, 11, 7)));
        Assert.Contains("Nov 8, 2026", CalendarLayout.Title(CalendarScale.Week, new DateTime(2026, 11, 7)));
        Assert.Contains("November 7, 2026", CalendarLayout.Title(CalendarScale.Day, new DateTime(2026, 11, 7)));
    }

    [Fact]
    public void AnOverdueTask_HasPassedItsDueDay_AndIsNotFinished()
    {
        var today = new DateTime(2026, 11, 7, 9, 0, 0);

        Assert.True(CalendarLayout.IsOverdue(new DateTime(2026, 11, 6), isDone: false, today));
        Assert.False(CalendarLayout.IsOverdue(new DateTime(2026, 11, 7), isDone: false, today)); // due today is not overdue
        Assert.False(CalendarLayout.IsOverdue(new DateTime(2026, 11, 6), isDone: true, today));
        Assert.False(CalendarLayout.IsOverdue(null, isDone: false, today));
    }

    [Fact]
    public void AnUnknownSavedMode_IsMonth() => Assert.Equal(CalendarScale.Month, CalendarLayout.Parse("Fortnight"));
}

[Collection(WpfCollection.Name)]
public sealed class CalendarSettingsTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheCalendarsChoices_AreRemembered_AndStartAsMonthWithEverythingOn() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal((CalendarScale.Month, true, false, true), (board.CalendarView, board.CalendarShowWeekends, board.CalendarIncludeDone, board.CalendarShowNoDueDate));

        board.SaveCalendarView(CalendarScale.Week);
        board.SaveCalendarShowWeekends(false);
        board.SaveCalendarIncludeDone(true);
        board.SaveCalendarShowNoDueDate(false);

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.Equal((CalendarScale.Week, false, true, false), (reopened.CalendarView, reopened.CalendarShowWeekends, reopened.CalendarIncludeDone, reopened.CalendarShowNoDueDate));
    });
}
