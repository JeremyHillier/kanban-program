using System.Globalization;
using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// A task's history: every save writes one CardHistory row saying what changed ("Due date Nov 3, 2026 -> Nov 10, 2026"),
// made where the change reaches the file (UpdateCard and the flag, people and sub-task writes), so the task screen,
// quick edits, right-click menus, mass update, Gantt drags and imports are all covered without each one knowing.
// Each writer reads the stored state first, writes, and logs inside its own transaction, so the row and its history
// line reach the file together; nothing visibly changed means no row at all.
public partial class DatabaseService
{
    // Written on each history row so a task used from two PCs says which one changed it. Tests set it.
    public static string ComputerName { get; set; } = Environment.MachineName;

    private const string Arrow = " → ";
    private const int NotesExcerptLength = 80;

    // Newest first.
    public List<CardHistoryRecord> GetCardHistory(int cardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Timestamp, EventType, Details, Computer FROM CardHistory WHERE CardId = $cardId ORDER BY Timestamp DESC, Id DESC;";
        cmd.Parameters.AddWithValue("$cardId", cardId);

        var result = new List<CardHistoryRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!DateTime.TryParseExact(reader.GetString(0), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)) continue;
            result.Add(new CardHistoryRecord(when, reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return result;
    }

    // The task's editable fields as stored, read before UpdateCard writes so DescribeChanges can say what changed.
    // Null when there is no such task.
    private static EditableCardFields? ReadEditable(SqliteConnection connection, int cardId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Title, ProjectId, Priority, DueDate, WhoId, IsRecurring, RecurrencePattern, GoalId, Notes, ForceEditOnComplete,
                   WebsiteUrl, DueTime, StartDate, WaitingOn, RecurrencesLeft
            FROM Cards WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", cardId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        static int? Number(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
        static DateTime? Day(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.Parse(r.GetString(i), CultureInfo.InvariantCulture);

        return new EditableCardFields(r.GetString(0), Number(r, 1), r.GetString(2), Day(r, 3), Number(r, 4), r.GetInt32(5) != 0, Text(r, 6),
            Number(r, 7), Text(r, 8), r.GetInt32(9) != 0, Text(r, 10), Text(r, 11), Day(r, 12), Text(r, 13), Number(r, 14));
    }

    // The Name of a Projects, Goals, Flags or People row (table is always a literal here); null for no id or a row
    // that has since been deleted, which the history then shows as "none".
    private static string? NameOf(SqliteConnection connection, string table, int? id)
    {
        if (id is null) return null;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Name FROM {table} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        return cmd.ExecuteScalar() as string;
    }

    // How a value reads in a history line: titles in quotes, everything else plain, blank as "none".
    private static string Quoted(string? text) => string.IsNullOrWhiteSpace(text) ? "none" : $"\"{text.Trim()}\"";

    private static string Plain(string? text) => string.IsNullOrWhiteSpace(text) ? "none" : text.Trim();

    private static string DayText(DateTime? day) => day is { } d ? d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "none";

    // The start of the old notes for a "Notes changed" line, line breaks and runs of spaces flattened to one space.
    private static string Excerpt(string? text)
    {
        var flat = string.Join(" ", (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= NotesExcerptLength ? flat : flat[..NotesExcerptLength].TrimEnd() + "...";
    }

    // Null, blank and surrounding spaces are all the same to the user, so none of them counts as a change.
    private static bool Same(string? a, string? b) => string.Equals(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, StringComparison.Ordinal);

    // What the user changed between two states of the task's own fields (people, flags and sub-tasks are described by
    // their own writes). Empty when nothing the user can see differs.
    private static List<string> DescribeChanges(SqliteConnection connection, EditableCardFields before, EditableCardFields after)
    {
        var changes = new List<string>();

        void Field(string label, string? was, string? now)
        {
            if (!Same(was, now)) changes.Add($"{label} {Plain(was)}{Arrow}{Plain(now)}");
        }

        if (!Same(before.Title, after.Title)) changes.Add($"Title {Quoted(before.Title)}{Arrow}{Quoted(after.Title)}");
        if (before.ProjectId != after.ProjectId)
        {
            changes.Add($"Project {Plain(NameOf(connection, "Projects", before.ProjectId))}{Arrow}{Plain(NameOf(connection, "Projects", after.ProjectId))}");
        }
        if (before.GoalId != after.GoalId)
        {
            changes.Add($"Goal {Plain(NameOf(connection, "Goals", before.GoalId))}{Arrow}{Plain(NameOf(connection, "Goals", after.GoalId))}");
        }
        Field("Priority", before.Priority, after.Priority);
        if (before.DueDate != after.DueDate) changes.Add($"Due date {DayText(before.DueDate)}{Arrow}{DayText(after.DueDate)}");
        Field("Due time", before.DueTime, after.DueTime);
        if (before.StartDate != after.StartDate) changes.Add($"Start date {DayText(before.StartDate)}{Arrow}{DayText(after.StartDate)}");
        Field("Waiting on", before.WaitingOn, after.WaitingOn);

        var wasRepeat = before.IsRecurring ? Plain(before.RecurrencePattern) : "none";
        var nowRepeat = after.IsRecurring ? Plain(after.RecurrencePattern) : "none";
        if (wasRepeat != nowRepeat) changes.Add($"Repeats {wasRepeat}{Arrow}{nowRepeat}");
        if (after.IsRecurring && before.IsRecurring && before.RecurrencesLeft != after.RecurrencesLeft)
        {
            changes.Add($"Repeat times {before.RecurrencesLeft?.ToString(CultureInfo.InvariantCulture) ?? "none"}{Arrow}{after.RecurrencesLeft?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
        }

        Field("Link", before.WebsiteUrl, after.WebsiteUrl);
        if (before.ForceEditOnComplete != after.ForceEditOnComplete)
        {
            changes.Add(after.ForceEditOnComplete ? "Edit when done switched on" : "Edit when done switched off");
        }
        if (!Same(before.Notes, after.Notes))
        {
            changes.Add(string.IsNullOrWhiteSpace(before.Notes)
                ? "Notes added"
                : $"Notes changed (was: \"{Excerpt(before.Notes)}\")");
        }

        return changes;
    }

    // Writes the sentence for a set of changes as one history row; nothing to say means no row.
    private static void LogChanges(SqliteConnection connection, int cardId, string title, IReadOnlyList<string> changes)
    {
        if (changes.Count == 0) return;
        LogHistory(connection, cardId, title, "Edited", string.Join(" · ", changes));
    }

    // The same for a writer that does not have the task's title to hand: it is looked up only when there is a row to write.
    private static void LogChanges(SqliteConnection connection, int cardId, IReadOnlyList<string> changes)
    {
        if (changes.Count == 0) return;
        LogChanges(connection, cardId, TitleOf(connection, cardId), changes);
    }

    // The task's title as stored, for a history row's CardTitle column.
    private static string TitleOf(SqliteConnection connection, int cardId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Title FROM Cards WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", cardId);
        return cmd.ExecuteScalar() as string ?? string.Empty;
    }

    // The flag or person ids a task has now, read before a replace-all write so the change can be named afterwards.
    private static List<int> IdsOf(SqliteConnection connection, string sql, int cardId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$cardId", cardId);
        var ids = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt32(0));
        return ids;
    }

    // "Flag added: Urgent", "Added Sam" ... for the ids that came or went; a reorder alone is not a change.
    private static void LogSetChange(SqliteConnection connection, int cardId, IReadOnlyList<int> before, IReadOnlyList<int> after, string table,
        Func<string, string> added, Func<string, string> removed)
    {
        var changes = new List<string>();
        foreach (var id in after.Except(before)) changes.Add(added(Plain(NameOf(connection, table, id))));
        foreach (var id in before.Except(after)) changes.Add(removed(Plain(NameOf(connection, table, id))));
        LogChanges(connection, cardId, changes);
    }

    // Sub-tasks are rewritten whole on save and keep no identity across it, so they are matched by title: a title
    // that is new reads as added and one that is gone as removed (a renamed sub-task is therefore both), and for a
    // title in both lists the counts of done and of copies say what happened.
    private static List<string> DescribeSubTaskChanges(IReadOnlyList<(string Title, bool IsDone)> before, IReadOnlyList<(string Title, bool IsDone)> after)
    {
        var changes = new List<string>();
        var wasByTitle = before.GroupBy(s => s.Title).ToDictionary(g => g.Key, g => g.ToList());
        var nowByTitle = after.GroupBy(s => s.Title).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (title, items) in nowByTitle)
        {
            if (!wasByTitle.TryGetValue(title, out var old))
            {
                changes.AddRange(items.Select(_ => $"Sub-task added: \"{title}\""));
                continue;
            }

            var done = items.Count(i => i.IsDone);
            var wasDone = old.Count(i => i.IsDone);
            if (done > wasDone) changes.Add($"Sub-task done: \"{title}\"");
            else if (done < wasDone) changes.Add($"Sub-task reopened: \"{title}\"");
            if (items.Count > old.Count) changes.AddRange(Enumerable.Repeat($"Sub-task added: \"{title}\"", items.Count - old.Count));
            else if (items.Count < old.Count) changes.AddRange(Enumerable.Repeat($"Sub-task removed: \"{title}\"", old.Count - items.Count));
        }

        foreach (var (title, items) in wasByTitle)
        {
            if (!nowByTitle.ContainsKey(title)) changes.AddRange(items.Select(_ => $"Sub-task removed: \"{title}\""));
        }

        return changes;
    }

    // The sub-tasks as stored, read before a replace-all write (titles and done flags are all the diff needs).
    private static List<(string Title, bool IsDone)> ReadSubTasks(SqliteConnection connection, int cardId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Title, IsDone FROM SubTasks WHERE CardId = $cardId ORDER BY SortOrder;";
        cmd.Parameters.AddWithValue("$cardId", cardId);
        var list = new List<(string, bool)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetString(0), r.GetInt32(1) != 0));
        return list;
    }
}
