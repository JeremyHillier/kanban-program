using System.Globalization;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

public sealed class RecurrenceTests
{
    private static DateTime D(string date) => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("Daily", "2026-09-14", "2026-09-15")]
    [InlineData("Weekday", "2026-09-18", "2026-09-21")] // Friday -> Monday
    [InlineData("Weekday", "2026-09-19", "2026-09-21")] // Saturday -> Monday
    [InlineData("Weekday", "2026-09-14", "2026-09-15")]
    [InlineData("Weekly", "2026-09-14", "2026-09-21")]
    [InlineData("Bi-Weekly", "2026-09-14", "2026-09-28")]
    [InlineData("Monthly", "2026-01-31", "2026-02-28")]
    [InlineData("Monthly", "2026-09-14", "2026-10-14")]
    [InlineData("Bi-Monthly", "2026-12-15", "2027-02-15")]
    [InlineData("Quarterly", "2026-11-30", "2027-02-28")]
    [InlineData("Annually", "2028-02-29", "2029-02-28")]
    [InlineData("Something unknown", "2026-09-14", "2026-09-15")]
    public void NextDueDate_FollowsThePattern(string pattern, string due, string expected) =>
        Assert.Equal(D(expected), MainViewModel.CalculateNextDueDate(D(due), pattern));

    [Theory]
    [InlineData("2026-09-03", "2026-09-18")] // first half: +15 days, same month
    [InlineData("2026-09-18", "2026-10-03")] // second half: back 15, next month
    [InlineData("2026-09-05", "2026-09-20")]
    [InlineData("2026-09-20", "2026-10-05")]
    [InlineData("2026-01-31", "2026-02-15")] // last day of the month pairs with the 15th
    [InlineData("2026-09-30", "2026-10-15")] // the 30th is September's last day
    [InlineData("2026-02-15", "2026-02-28")] // the 30th doesn't exist in February
    [InlineData("2026-02-28", "2026-03-15")] // ...and February's last day still pairs with the 15th
    [InlineData("2026-12-20", "2027-01-05")] // year rollover
    public void SemiMonthly_StaysOnTwoFixedDaysOfTheMonth(string due, string expected) =>
        Assert.Equal(D(expected), MainViewModel.CalculateNextDueDate(D(due), "Semi-Monthly"));

    [Fact]
    public void SemiMonthly_SettlesOnThe15thAndLastDayInsteadOfDriftingThroughFebruary()
    {
        var due = D("2026-01-15");
        var seen = new List<DateTime>();
        for (var i = 0; i < 8; i++)
        {
            due = MainViewModel.CalculateNextDueDate(due, "Semi-Monthly");
            seen.Add(due);
        }

        Assert.Equal(
            [D("2026-01-30"), D("2026-02-15"), D("2026-02-28"), D("2026-03-15"), D("2026-03-30"), D("2026-04-15"), D("2026-04-30"), D("2026-05-15")],
            seen);
    }
}
