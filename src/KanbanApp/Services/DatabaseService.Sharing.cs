using System.Globalization;
using KanbanApp.Models;

namespace KanbanApp.Services;

// When a task was emailed, and to whom, and when it was put in a calendar: one "Emailed" or
// "Scheduled" row in CardHistory each time, so the task screen can say so. Nothing is stored on the
// card row itself.
public partial class DatabaseService
{
    public const string EmailedEvent = "Emailed";
    public const string ScheduledEvent = "Scheduled";

    public void RecordCardEmailed(int cardId, string cardTitle, string details)
    {
        using var connection = OpenConnection();
        LogHistory(connection, cardId, cardTitle, EmailedEvent, details);
    }

    public void RecordCardScheduled(int cardId, string cardTitle, string details)
    {
        using var connection = OpenConnection();
        LogHistory(connection, cardId, cardTitle, ScheduledEvent, details);
    }

    public List<CardEmailRecord> GetCardEmailHistory(int cardId) => GetCardEvents(cardId, EmailedEvent);

    public List<CardEmailRecord> GetCardScheduleHistory(int cardId) => GetCardEvents(cardId, ScheduledEvent);

    // Newest first.
    private List<CardEmailRecord> GetCardEvents(int cardId, string eventType)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Timestamp, Details FROM CardHistory WHERE CardId = $cardId AND EventType = $event ORDER BY Timestamp DESC, Id DESC;";
        cmd.Parameters.AddWithValue("$cardId", cardId);
        cmd.Parameters.AddWithValue("$event", eventType);

        var result = new List<CardEmailRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (DateTime.TryParseExact(reader.GetString(0), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            {
                result.Add(new CardEmailRecord(when, reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }
        }
        return result;
    }
}

// A shared task's permanent ID (see CardItem.ShareId).
public partial class DatabaseService
{
    public const int ShareIdFileFormat = 5;

    public void SetCardShareId(int cardId, string shareId)
    {
        using (var connection = OpenConnection())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET ShareId = $shareId WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$shareId", shareId);
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        // From here on the file holds a shared task, which an older copy would import twice.
        RaiseFileFormat(ShareIdFileFormat);
    }
}

// Who sent a shared task here, and what it looked like when last exchanged (Models/SharedTaskInfo).
// Its own small table keyed by the Task ID, not columns on Cards: it only exists for tasks that were
// shared, and older copies simply ignore it. Stamped lazily - the first row written raises the file.
public partial class DatabaseService
{
    public const int SharedTasksFileFormat = 8;

    private static void EnsureSharedTasksTable(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS SharedTasks (ShareId TEXT PRIMARY KEY, SenderName TEXT NULL, SenderEmail TEXT NULL, Fingerprint TEXT NULL);";
        cmd.ExecuteNonQuery();
    }

    public SharedTaskInfo? GetSharedTask(string? shareId)
    {
        if (string.IsNullOrWhiteSpace(shareId)) return null;

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ShareId, SenderName, SenderEmail, Fingerprint FROM SharedTasks WHERE ShareId = $id;";
        cmd.Parameters.AddWithValue("$id", shareId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        return new SharedTaskInfo(reader.GetString(0), Text(1), Text(2), Text(3));
    }

    public void SaveSharedSender(string shareId, string? name, string? email)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SharedTasks (ShareId, SenderName, SenderEmail) VALUES ($id, $name, $email)
            ON CONFLICT(ShareId) DO UPDATE SET SenderName = excluded.SenderName, SenderEmail = excluded.SenderEmail;
            """;
        cmd.Parameters.AddWithValue("$id", shareId);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        RaiseFileFormat(SharedTasksFileFormat);
    }

    public void SaveSharedFingerprint(string shareId, string fingerprint)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SharedTasks (ShareId, Fingerprint) VALUES ($id, $fp)
            ON CONFLICT(ShareId) DO UPDATE SET Fingerprint = excluded.Fingerprint;
            """;
        cmd.Parameters.AddWithValue("$id", shareId);
        cmd.Parameters.AddWithValue("$fp", fingerprint);
        cmd.ExecuteNonQuery();
        RaiseFileFormat(SharedTasksFileFormat);
    }
}
