using System.Security.Cryptography;
using System.Text;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// What to do with a returned task that is already on the board (Models/SharedTaskInfo). The board
// keeps a fingerprint of the task as it was when it last went out or came in. Comparing three things
// - that fingerprint, the task on the board now, and the task in the file - says which side moved:
//
//   file = board                       nothing to do
//   board unchanged since the exchange the other side changed it: update
//   file unchanged since the exchange  only this side changed it: the file is old news, keep the board's
//   both changed, differently          a clash: the user chooses
//
// A task with no fingerprint (shared before this was kept) is simply updated, as it always was.
public static class ShareSync
{
    public sealed record Field(string Name, string Mine, string Theirs);

    // category maps what a file calls a column to the board's own column name, so a returned task from
    // a PC whose columns have other display names still compares equal.
    public static string Fingerprint(ImportedTaskRow row, Func<string?, string?>? category = null) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\u001f', Fields(row, category).Select(f => f.Value))))).Substring(0, 24);

    // The fields that differ, in the order a person thinks of them.
    public static List<Field> Differences(ImportedTaskRow mine, ImportedTaskRow theirs, Func<string?, string?>? category = null)
    {
        var a = Fields(mine, category);
        var b = Fields(theirs, category);
        return a.Zip(b, (x, y) => (x, y)).Where(p => p.x.Value != p.y.Value).Select(p => new Field(p.x.Name, Show(p.x.Value), Show(p.y.Value))).ToList();
    }

    private static string Show(string value) => value.Length == 0 ? "(none)" : value.Replace("\n", " / ");

    private static List<(string Name, string Value)> Fields(ImportedTaskRow row, Func<string?, string?>? category)
    {
        static string T(string? s) => (s ?? string.Empty).Trim();
        static string D(DateTime? d) => d is { } date ? date.ToString("yyyy-MM-dd") : string.Empty;
        static string List(string? s, bool sort)
        {
            var items = (s ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);
            return string.Join("; ", sort ? items.OrderBy(i => i, StringComparer.OrdinalIgnoreCase) : items);
        }

        var pattern = T(row.RecurrencePattern);
        return
        [
            ("Task", T(row.Title)),
            ("Status", T(category is null ? row.Category : category(row.Category))),
            ("Priority", T(row.Priority)),
            ("Project", T(row.Project)),
            ("Goal", T(row.Goal)),
            ("Due date", D(row.DueDate)),
            ("Due time", row.DueDate is null ? string.Empty : T(row.DueTime)),
            ("Start date", D(row.StartDate)),
            ("Assigned to", List(row.Who, sort: false)),
            ("Waiting on", T(row.WaitingOn)),
            ("Repeats", pattern),
            ("Times", pattern.Length == 0 || row.RecurrenceCount is null ? string.Empty : row.RecurrenceCount.Value.ToString()),
            ("Flags", List(row.Flags, sort: true)),
            ("Link", UrlLauncher.Clean(row.WebsiteUrl)),
            ("Notes", T(row.Notes).Replace("\r\n", "\n")),
            ("Sub-tasks", ImportService.FormatSubTasks(row.SubTasks)),
        ];
    }
}

// What the user chooses when both sides changed the same task.
public enum ConflictChoice
{
    UseTheirs,   // replace the board's task with the file's
    KeepMine,    // leave the board's task as it is
    KeepBoth,    // add the file's version as a separate task
}

// One clash, as the import asks about it: the task on the board, the file's row, what differs, and who from.
public sealed record ImportConflict(CardViewModel Card, ImportedTaskRow Theirs, IReadOnlyList<ShareSync.Field> Differences, string From)
{
    // The question, in words: what the task is and the first few differences.
    public string Describe()
    {
        const int shown = 6;
        var lines = Differences.Take(shown).Select(d => $"• {d.Name}: yours {Quote(d.Mine)}, theirs {Quote(d.Theirs)}").ToList();
        if (Differences.Count > shown) lines.Add($"• and {Differences.Count - shown} more");
        return $"\"{Card.Title}\" has changed both on your board and in {From} copy.\n\n" + string.Join("\n", lines) +
               "\n\nUse Theirs replaces your task. Keep Mine ignores their changes. Keep Both adds theirs as a separate task.";
    }

    private static string Quote(string value) => value == "(none)" ? value : $"\"{(value.Length > 60 ? value[..57] + "..." : value)}\"";
}

// What an import did, for the message afterwards.
public sealed record ImportSummary(int Added, int Updated, int Unchanged, int KeptMine, int KeptBoth)
{
    public int Changes => Added + Updated + KeptBoth;

    public static ImportSummary None { get; } = new(0, 0, 0, 0, 0);
}
