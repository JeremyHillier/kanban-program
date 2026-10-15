using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// "Days of each month": a task that comes back every day within part of each month.
public class MonthDayRangeTests
{
    private static readonly MonthDayRange Days5To12 = new(5, 12, false);
    private static readonly MonthDayRange FirstTenWorkingDays = new(1, 10, true);

    [Theory]
    [InlineData("Days 5-12 of each month", 5, 12, false)]
    [InlineData("working days 1 - 10 of each month", 1, 10, true)]
    [InlineData("  DAYS 1-31 OF EACH MONTH ", 1, 31, false)]
    public void TheStoredText_ReadsBack(string text, int from, int to, bool working)
    {
        var range = MonthDayRange.Parse(text);
        Assert.Equal(new MonthDayRange(from, to, working), range);
        Assert.Equal(range, MonthDayRange.Parse(range!.ToString()));
    }

    [Theory]
    [InlineData("Daily")]
    [InlineData("Days 12-5 of each month")]   // the wrong way round
    [InlineData("Days 0-5 of each month")]
    [InlineData("Days 5-32 of each month")]
    [InlineData("Days 5-12")]
    [InlineData(null)]
    public void AnythingElse_IsNotARange(string? text) => Assert.Null(MonthDayRange.Parse(text));

    [Fact]
    public void WorkingDays_CountMondayToFridayFromThe1st()
    {
        // November 2026 starts on a Sunday: the first working day is Monday the 2nd, the 10th is Friday the 13th.
        var days = FirstTenWorkingDays.DaysIn(2026, 11).ToList();
        Assert.Equal(10, days.Count);
        Assert.Equal(new DateTime(2026, 11, 2), days[0]);
        Assert.Equal(new DateTime(2026, 11, 13), days[^1]);
        Assert.DoesNotContain(days, d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        // December 2026 starts on a Tuesday: the 10th working day is Monday the 14th.
        Assert.Equal(new DateTime(2026, 12, 14), FirstTenWorkingDays.DaysIn(2026, 12).Last());
    }

    [Fact]
    public void WorkingDays7To9_AreTheMiddleOfThatCount()
    {
        Assert.Equal([new DateTime(2026, 11, 10), new DateTime(2026, 11, 11), new DateTime(2026, 11, 12)],
            new MonthDayRange(7, 9, true).DaysIn(2026, 11));
    }

    [Fact]
    public void AShortMonth_StopsTheRangeAtItsEnd_OrIsSkipped()
    {
        Assert.Equal(new DateTime(2027, 2, 28), new MonthDayRange(25, 31, false).DaysIn(2027, 2).Last());
        Assert.Empty(new MonthDayRange(30, 31, false).DaysIn(2027, 2));
        Assert.Equal(new DateTime(2027, 3, 30), new MonthDayRange(30, 31, false).FirstOnOrAfter(new DateTime(2027, 2, 1)));
    }

    [Theory]
    [InlineData("2026-11-05", "2026-11-01", "2026-11-06")] // the next day in the range
    [InlineData("2026-11-12", "2026-11-01", "2026-12-05")] // the last day: on to next month
    [InlineData("2026-11-06", "2026-11-09", "2026-11-09")] // a day missed: catches up to today
    [InlineData("2026-11-06", "2026-11-20", "2026-12-05")] // fallen right behind: next month
    public void TheNextCopy_IsTheNextDayInTheRange_OrToday(string due, string today, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), Days5To12.NextAfter(DateTime.Parse(due), DateTime.Parse(today)));
    }

    [Fact]
    public void TheText_SaysMonths_WhenItCountsMonths()
    {
        Assert.Equal(" (2 more months after this one)", CardTextFormatter.RepeatsLeftText(3, months: true));
        Assert.Equal(" (this is the last month)", CardTextFormatter.RepeatsLeftText(1, months: true));
        Assert.Equal("Working days 1-10 of each month", RecurrencePatterns.Find("WORKING DAYS 1-10 of each month"));
    }
}

[Collection(WpfCollection.Name)]
public sealed class MonthDayRangeTaskTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // Far enough ahead that "today" never catches up with these dates.
    private static readonly DateTime Jan2030 = new(2030, 1, 1);

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static ColumnViewModel Column(MainViewModel board, string name) => board.Columns.Single(c => c.Name == name);

    private static CardViewModel Finish(MainViewModel board, CardViewModel card)
    {
        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        return Column(board, "To Do").Cards.Single(c => c.Title == card.Title);
    }

    [Fact]
    public void EachDayComesBack_ThenItWaitsForNextMonth_CountingMonths() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var card = board.AddCard("Check timesheets", Column(board, "To Do"), board.Projects.First(), "Normal",
            new DateTime(2030, 1, 10), null, true, "Days 10-12 of each month", null, recurrencesLeft: 2);

        card = Finish(board, card);
        Assert.Equal(new DateTime(2030, 1, 12), Finish(board, card).DueDate);   // 11th, then 12th
        card = Column(board, "To Do").Cards.Single();
        Assert.Equal(2, card.RecurrencesLeft);                                // still January's month

        card = Finish(board, card);
        Assert.Equal(new DateTime(2030, 2, 10), card.DueDate);                // on to February
        Assert.Equal(1, card.RecurrencesLeft);                                // the last month
        Assert.True(card.HasNextOccurrence);                                  // but February's other days still come

        card = Finish(board, card);
        card = Finish(board, card);
        Assert.Equal(new DateTime(2030, 2, 12), card.DueDate);
        Assert.False(card.HasNextOccurrence);                                 // nothing after the last month
        board.MoveCardCommand.Execute((card, Column(board, "Done")));
        Assert.Empty(Column(board, "To Do").Cards);
    });

    [Fact]
    public void SavingOne_StampsTheFile_SoOlderCopiesWarn() => wpf.Run(() =>
    {
        var board = OpenBoard();
        Assert.Equal("3", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));

        board.AddCard("Plain", Column(board, "To Do"), board.Projects.First(), "Normal", null, null, true, "Daily", null);
        Assert.Equal("3", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));

        board.AddCard("Range", Column(board, "To Do"), board.Projects.First(), "Normal", Jan2030, null, true, "Working days 1-10 of each month", null);
        Assert.Equal("6", new DatabaseService(_temp.File("board.db")).GetSetting("FileFormat"));
    });
}
