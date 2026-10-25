using System.Globalization;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// "Every 2 years": a Daily, Weekly, Monthly, Quarterly or Annually task that comes back every Nth step.
public sealed class RecurrenceIntervalTests
{
    private static DateTime D(string date) => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("Annually", 2, "Every 2 years")]
    [InlineData("Weekly", 3, "Every 3 weeks")]
    [InlineData("Monthly", 6, "Every 6 months")]
    [InlineData("Quarterly", 2, "Every 2 quarters")]
    [InlineData("Daily", 10, "Every 10 days")]
    public void TwoOrMore_IsStoredAsReadableText(string pattern, int count, string stored) =>
        Assert.Equal(stored, RecurrenceInterval.Compose(pattern, count));

    [Theory]
    [InlineData("Annually", 1)]
    [InlineData("Weekly", 0)]
    [InlineData("Bi-Weekly", 3)] // already "every 2": takes no count
    [InlineData("Weekday", 2)]
    [InlineData("Semi-Monthly", 2)]
    public void One_OrAPatternThatTakesNoCount_IsJustThePattern(string pattern, int count) =>
        Assert.Equal(pattern, RecurrenceInterval.Compose(pattern, count));

    [Theory]
    [InlineData("Every 2 years", 2, RecurrenceInterval.Unit.Year)]
    [InlineData("every 3 WEEKS", 3, RecurrenceInterval.Unit.Week)]
    [InlineData(" Every 18 months ", 18, RecurrenceInterval.Unit.Month)]
    [InlineData("Every 2 quarters", 2, RecurrenceInterval.Unit.Quarter)]
    [InlineData("Every 99 days", 99, RecurrenceInterval.Unit.Day)]
    public void ReadingBack_AcceptsWhatIsStored_WhateverTheCapitals(string text, int count, RecurrenceInterval.Unit unit) =>
        Assert.Equal(new RecurrenceInterval(count, unit), RecurrenceInterval.Parse(text));

    [Theory]
    [InlineData("Every 1 week")] // that is just Weekly
    [InlineData("Every 0 years")]
    [InlineData("Every 100 days")]
    [InlineData("Every two years")]
    [InlineData("Every 2 fortnights")]
    [InlineData("Weekly")]
    [InlineData(null)]
    public void ReadingBack_IgnoresAnythingElse(string? text) => Assert.Null(RecurrenceInterval.Parse(text));

    [Theory]
    [InlineData("Every 2 years", "2026-10-25", "2028-10-25")]
    [InlineData("Every 2 years", "2028-02-29", "2030-02-28")] // a leap day falls back like Annually does
    [InlineData("Every 3 weeks", "2026-10-25", "2026-11-15")]
    [InlineData("Every 6 months", "2026-08-31", "2027-02-28")] // a short month ends the month
    [InlineData("Every 2 quarters", "2026-10-25", "2027-04-25")]
    [InlineData("Every 10 days", "2026-12-25", "2027-01-04")]
    public void TheNextDueDate_IsNStepsOn(string pattern, string due, string expected) =>
        Assert.Equal(D(expected), MainViewModel.CalculateNextDueDate(D(due), pattern));

    [Fact]
    public void ImportingText_FindsTheStoredForm()
    {
        Assert.Equal("Every 2 years", RecurrencePatterns.Find("every 2 YEARS"));
        Assert.Equal("Weekly", RecurrencePatterns.Find("weekly"));
        Assert.Null(RecurrencePatterns.Find("Every 1 week"));
    }

    [Theory]
    [InlineData("Every 2 years", "every 2 years")]
    [InlineData("Annually", "Annually")]
    [InlineData("Days 5-12 of each month", "Days 5-12 of each month")]
    public void OnACard_ItReadsInTheMiddleOfASentence(string pattern, string phrase) =>
        Assert.Equal(phrase, RecurrenceInterval.Phrase(pattern));

    [Fact]
    public void ACardRepeatingEvery2Years_ShowsItAndSaysSoInCopyAsText()
    {
        var card = new CardViewModel(new CardItem { Title = "Renew passport", IsRecurring = true, RecurrencePattern = "Every 2 years", RecurrencesLeft = 3, Priority = "Normal" });

        Assert.Equal("↻ Repeats every 2 years · 2 more", card.RecurrenceDisplay);
        Assert.Equal("every 2 years (2 more after this one)", CardTextFormatter.RepeatsText(card));
    }
}

[Collection(WpfCollection.Name)]
public sealed class RecurrenceIntervalBoardTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FinishingAnEvery2YearsTask_CreatesTheNextTwoYearsOn_AndStampsTheFile() => wpf.Run(() =>
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var board = new MainViewModel(db);
        var project = board.AddProject("Admin").Item!;
        var toDo = board.Columns.Single(c => c.Name == "To Do");
        var done = board.Columns.Single(c => c.Name == "Done");
        Assert.True(db.FileFormatAtOpen < DatabaseService.RecurrenceIntervalFileFormat);

        var card = board.AddCard("Renew passport", toDo, project, "Normal", new DateTime(2026, 11, 1), null, true, "Every 2 years", null);
        board.MoveCardCommand.Execute((card, done));

        var next = toDo.Cards.Single(c => c.Title == "Renew passport" && c != card);
        Assert.Equal(new DateTime(2028, 11, 1), next.DueDate);
        Assert.Equal("Every 2 years", next.RecurrencePattern);

        var reopened = new DatabaseService(_temp.File("board.db"));
        Assert.Equal(DatabaseService.RecurrenceIntervalFileFormat, reopened.FileFormatAtOpen);
    });
}
