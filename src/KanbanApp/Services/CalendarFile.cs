using System.Globalization;
using System.Text;

namespace KanbanApp.Services;

// When an appointment for a task goes in the calendar, and the same appointment as an iCalendar
// (.ics) file for calendar apps that cannot be driven directly (the new Outlook, Windows Calendar).
// Pure, so the rules are unit-tested.
public static class CalendarFile
{
    public sealed record Appointment(DateTime Start, DateTime End, bool AllDay);

    internal static readonly TimeSpan DefaultLength = TimeSpan.FromMinutes(30);

    // At the due time if the task has one; all day on the due date if it has only a date; otherwise
    // at the next half hour today, for the user to move.
    public static Appointment For(DateTime? dueDate, DateTime? dueDateTime, DateTime now)
    {
        if (dueDateTime is { } dueAt) return new Appointment(dueAt, dueAt + DefaultLength, false);
        if (dueDate is { } due) return new Appointment(due.Date, due.Date.AddDays(1), true);

        var start = now.Date.AddHours(now.Hour).AddMinutes(now.Minute < 30 ? 30 : 60);
        return new Appointment(start, start + DefaultLength, false);
    }

    // "for Oct 12, 2026 at 2:30 PM" / "for Oct 12, 2026, all day" - how the task's history says it.
    public static string Describe(Appointment appointment) => appointment.AllDay
        ? $"for {appointment.Start.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}, all day"
        : $"for {appointment.Start.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)} at {appointment.Start.ToString("h:mm tt", CultureInfo.CurrentCulture)}";

    // Times are written without a time zone ("floating"), so a calendar app reads them as local time,
    // which is what the task's due time means. utcNow is the file's own stamp.
    public static string BuildIcs(string title, string description, Appointment appointment, DateTime utcNow, string uid)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            $"PRODID:-//{AppInfo.Company}//{AppInfo.ProductName}//EN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{uid}",
            $"DTSTAMP:{utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}"
        };

        if (appointment.AllDay)
        {
            lines.Add($"DTSTART;VALUE=DATE:{appointment.Start.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            lines.Add($"DTEND;VALUE=DATE:{appointment.End.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            lines.Add("TRANSP:TRANSPARENT"); // an all-day reminder of a due date shouldn't block the day as busy
        }
        else
        {
            lines.Add($"DTSTART:{appointment.Start.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}");
            lines.Add($"DTEND:{appointment.End.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}");
        }

        lines.Add($"SUMMARY:{Escape(title)}");
        if (!string.IsNullOrWhiteSpace(description)) lines.Add($"DESCRIPTION:{Escape(description)}");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        return string.Concat(lines.Select(line => Fold(line) + "\r\n"));
    }

    // RFC 5545: backslash, semicolon and comma are escaped, line breaks become \n.
    internal static string Escape(string text) => text
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");

    // Lines longer than 75 bytes are folded: a line break and a space, never in the middle of a
    // character (UTF-8 can take several bytes for one).
    internal static string Fold(string line)
    {
        const int limit = 75;
        var sb = new StringBuilder();
        var bytes = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var length = char.IsHighSurrogate(line[i]) && i + 1 < line.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(i, length));
            if (bytes + size > limit)
            {
                sb.Append("\r\n ");
                bytes = 1; // the leading space counts
            }
            sb.Append(line, i, length);
            bytes += size;
            i += length - 1;
        }
        return sb.ToString();
    }
}
