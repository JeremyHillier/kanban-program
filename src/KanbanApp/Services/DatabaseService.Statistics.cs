using System.Globalization;

namespace KanbanApp.Services;

// What the statistics report reads from each task's history: when it was added, and every move
// between columns. Column names in the history are the columns' own names ("Done"), which renaming
// a column on the board leaves alone.
public partial class DatabaseService
{
    public Dictionary<int, List<TaskEvent>> GetStatisticsHistory()
    {
        var history = new Dictionary<int, List<TaskEvent>>();

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CardId, EventType, Details, Timestamp FROM CardHistory
            WHERE EventType IN ('Created', 'Moved')
            ORDER BY CardId, Timestamp, Id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!DateTime.TryParseExact(reader.GetString(3), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) continue;
            if (TaskEvent.ColumnEntered(reader.GetString(1), reader.GetString(2)) is not { } column) continue;

            var cardId = reader.GetInt32(0);
            if (!history.TryGetValue(cardId, out var events)) history[cardId] = events = [];
            events.Add(new TaskEvent(at, column, reader.GetString(1) == "Created"));
        }

        return history;
    }
}

// A moment a task entered a column: when it was added ("Added to To Do"), or moved ("Moved from To
// Do to In Progress").
public sealed record TaskEvent(DateTime At, string Column, bool IsCreated)
{
    internal static string? ColumnEntered(string eventType, string details)
    {
        const string added = "Added to ";
        const string movedFrom = "Moved from ";
        if (eventType == "Created") return details.StartsWith(added, StringComparison.Ordinal) ? details[added.Length..] : null;
        if (eventType != "Moved" || !details.StartsWith(movedFrom, StringComparison.Ordinal)) return null;

        // The last " to ": a column's own name never contains one ("To Do" is capitalised).
        var to = details.LastIndexOf(" to ", StringComparison.Ordinal);
        return to > movedFrom.Length ? details[(to + 4)..] : null;
    }
}
