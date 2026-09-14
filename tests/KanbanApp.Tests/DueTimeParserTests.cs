using KanbanApp.Services;

namespace KanbanApp.Tests;

public sealed class DueTimeParserTests
{
    [Theory]
    // No AM/PM button clicked: a bare hour is read as a working hour.
    [InlineData("2:30", null, "14:30")]
    [InlineData("9:00", null, "09:00")]
    [InlineData("7", null, "07:00")]
    [InlineData("6", null, "18:00")]
    [InlineData("12:15", null, "12:15")]
    [InlineData("11:45", null, "11:45")]
    [InlineData("930", null, "09:30")]
    [InlineData("230", null, "14:30")]
    // A clicked button decides a bare hour.
    [InlineData("2:30", false, "02:30")]
    [InlineData("9:00", true, "21:00")]
    [InlineData("12:15", false, "00:15")]
    [InlineData("12:15", true, "12:15")]
    // Typed AM/PM, or 24-hour notation, always wins over the button.
    [InlineData("2:30 PM", false, "14:30")]
    [InlineData("9am", true, "09:00")]
    [InlineData("2:30p", false, "14:30")]
    [InlineData("7a", true, "07:00")]
    [InlineData("2:30 p.m.", false, "14:30")]
    [InlineData("12am", null, "00:00")]
    [InlineData("14:30", false, "14:30")]
    [InlineData("00:30", true, "00:30")]
    [InlineData("06:00", true, "06:00")]
    [InlineData("0930", true, "09:30")]
    [InlineData("  2:30 pm  ", null, "14:30")]
    public void Parse_ReadsTheTimeAsExpected(string text, bool? preferPm, string expected) =>
        Assert.Equal(expected, DueTimeParser.Parse(text, preferPm));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData("2:30 xm")]
    [InlineData("12:61")]
    public void Parse_RejectsBlankOrUnreadableText(string? text) =>
        Assert.Null(DueTimeParser.Parse(text, null));

    [Theory]
    [InlineData("14:30", "2:30 PM")]
    [InlineData("00:05", "12:05 AM")]
    [InlineData("12:00", "12:00 PM")]
    [InlineData(null, "")]
    [InlineData("nonsense", "")]
    public void Format_ShowsTheStoredTimeFor12HourDisplay(string? stored, string expected) =>
        Assert.Equal(expected, DueTimeParser.Format(stored));

    [Theory]
    [InlineData("2:30 PM")]
    [InlineData("12:00 AM")]
    [InlineData("9:05 AM")]
    public void FormattedTime_ParsesBackToTheSameValue(string shown)
    {
        var stored = DueTimeParser.Parse(shown, null);
        Assert.Equal(shown, DueTimeParser.Format(stored));
    }
}
