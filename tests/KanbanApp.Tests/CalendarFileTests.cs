using System.Text;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Schedule in Outlook: when the appointment goes, and the calendar file for apps Outlook's
// automation can't reach.
public class CalendarFileTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 9, 40, 0);

    [Fact]
    public void ATaskWithATime_IsBookedAtThatTime_ForHalfAnHour()
    {
        var at = new DateTime(2026, 10, 12, 14, 30, 0);
        var appointment = CalendarFile.For(at.Date, at, Now);

        Assert.Equal(new CalendarFile.Appointment(at, at.AddMinutes(30), false), appointment);
        Assert.Equal("for Oct 12, 2026 at 2:30 PM", CalendarFile.Describe(appointment));
    }

    [Fact]
    public void ATaskWithOnlyADate_IsAllDay()
    {
        var appointment = CalendarFile.For(new DateTime(2026, 10, 12), null, Now);

        Assert.Equal(new CalendarFile.Appointment(new DateTime(2026, 10, 12), new DateTime(2026, 10, 13), true), appointment);
        Assert.Equal("for Oct 12, 2026, all day", CalendarFile.Describe(appointment));
    }

    [Theory]
    [InlineData(9, 40, 10, 0)]
    [InlineData(9, 10, 9, 30)]
    [InlineData(9, 30, 10, 0)]
    [InlineData(23, 45, 24, 0)]
    public void ATaskWithNoDate_GoesAtTheNextHalfHour(int hour, int minute, int startHour, int startMinute)
    {
        var appointment = CalendarFile.For(null, null, new DateTime(2026, 10, 9, hour, minute, 0));

        var start = new DateTime(2026, 10, 9).AddHours(startHour).AddMinutes(startMinute);
        Assert.Equal(new CalendarFile.Appointment(start, start.AddMinutes(30), false), appointment);
    }

    [Fact]
    public void TheFile_HasTheEvent_InLocalTime()
    {
        var at = new DateTime(2026, 10, 12, 14, 30, 0);
        var ics = CalendarFile.BuildIcs("Call Sam, re: budget", "Project: Office\nNotes; bring the figures",
            CalendarFile.For(at.Date, at, Now), new DateTime(2026, 10, 9, 13, 0, 0, DateTimeKind.Utc), "abc@kanban-task-board");

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.Contains("\r\nDTSTART:20261012T143000\r\n", ics);
        Assert.Contains("\r\nDTEND:20261012T150000\r\n", ics);
        Assert.Contains("\r\nDTSTAMP:20261009T130000Z\r\n", ics);
        Assert.Contains("\r\nSUMMARY:Call Sam\\, re: budget\r\n", ics);
        Assert.Contains("\r\nDESCRIPTION:Project: Office\\nNotes\\; bring the figures\r\n", ics);
        Assert.EndsWith("END:VEVENT\r\nEND:VCALENDAR\r\n", ics);
    }

    [Fact]
    public void AnAllDayEvent_UsesDates_AndIsNotBusy()
    {
        var ics = CalendarFile.BuildIcs("Pay rent", "", CalendarFile.For(new DateTime(2026, 10, 31), null, Now), Now.ToUniversalTime(), "x");

        Assert.Contains("\r\nDTSTART;VALUE=DATE:20261031\r\n", ics);
        Assert.Contains("\r\nDTEND;VALUE=DATE:20261101\r\n", ics);
        Assert.Contains("\r\nTRANSP:TRANSPARENT\r\n", ics);
        Assert.DoesNotContain("DESCRIPTION", ics);
    }

    [Fact]
    public void LongLines_AreFolded_WithoutSplittingACharacter()
    {
        var title = string.Concat(Enumerable.Repeat("Réunion é ", 30));
        var ics = CalendarFile.BuildIcs(title, "", CalendarFile.For(null, null, Now), Now.ToUniversalTime(), "x");

        var lines = ics.Split("\r\n");
        Assert.All(lines, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
        Assert.DoesNotContain('�', ics);

        // Unfolded, it reads back exactly.
        var summary = string.Concat(lines.SkipWhile(l => !l.StartsWith("SUMMARY:")).TakeWhile(l => !l.StartsWith("END:"))
            .Select((l, i) => i == 0 ? l : l[1..]));
        Assert.Equal("SUMMARY:" + CalendarFile.Escape(title), summary);
    }
}

[Collection(WpfCollection.Name)]
public sealed class ScheduleHistoryTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ATaskRemembersEachTimeItWasScheduled_ApartFromItsEmails() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var card = board.AddCard("Dentist", board.Columns.First(), board.Projects.First(), "Normal", null, null, false, null, null);
        Assert.Null(MainViewModel.ScheduleStampText(board.GetScheduleHistory(card)));

        board.RecordCardEmailed(card, "sam@example.com", "in Outlook");
        board.RecordCardScheduled(card, "for Oct 12, 2026 at 2:30 PM", "in Outlook");
        board.RecordCardScheduled(card, "for Oct 13, 2026, all day", "in your calendar app");

        var history = board.GetScheduleHistory(card);
        Assert.Equal(2, history.Count);
        Assert.Single(board.GetEmailHistory(card));

        var stamp = MainViewModel.ScheduleStampText(history)!;
        Assert.StartsWith("Scheduled ", stamp);
        Assert.EndsWith(" for Oct 13, 2026, all day (2 times)", stamp);
        Assert.Contains("for Oct 12, 2026 at 2:30 PM, in Outlook", MainViewModel.EmailHistoryText(history));
    });
}
