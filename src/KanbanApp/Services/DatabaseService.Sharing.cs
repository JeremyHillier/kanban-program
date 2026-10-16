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

// A permanent ID for every task, and an identity for the task file itself - what a link to a task
// (Services/TaskLinks) is made of. Every new task is given its ID when it is added; a task file from
// before that is given them all once, after a backup (App, at start-up).
public partial class DatabaseService
{
    private const string TaskFileIdKey = "TaskFileId";

    // The tasks (board, archived and deleted) that have no ID yet.
    public int CountTasksWithoutId()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Cards WHERE ShareId IS NULL OR ShareId = '';";
        return (int)(long)cmd.ExecuteScalar()!;
    }

    // 32 random hex digits, the same form as a new task's ID; true when any were given.
    public bool EnsureEveryTaskHasAnId()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cards SET ShareId = lower(hex(randomblob(16))) WHERE ShareId IS NULL OR ShareId = '';";
        var changed = cmd.ExecuteNonQuery() > 0;
        if (changed) RaiseFileFormat(ShareIdFileFormat);
        return changed;
    }

    // Tells this task file from any other: kept in the file, so a copy on another PC has the same one.
    // Made the first time it is asked for.
    public string GetTaskFileId()
    {
        var stored = GetSetting(TaskFileIdKey);
        if (!string.IsNullOrWhiteSpace(stored)) return stored;

        var id = Guid.NewGuid().ToString("N");
        SetSetting(TaskFileIdKey, id);
        return id;
    }

    // A task by its ID, wherever it is - on the board, archived or deleted - for a link to open.
    public TaskLocation? FindTask(string shareId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Title, IsArchived, IsDeleted FROM Cards WHERE ShareId = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", shareId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var id = reader.GetInt32(0);
        var title = reader.GetString(1);
        var archived = reader.GetInt64(2) != 0;
        var deleted = reader.GetInt64(3) != 0;
        reader.Close();

        // When it was put away: the latest Archived or Deleted line in its history.
        DateTime? when = null;
        if (archived || deleted)
        {
            using var historyCmd = connection.CreateCommand();
            historyCmd.CommandText = "SELECT MAX(Timestamp) FROM CardHistory WHERE CardId = $id AND EventType IN ('Archived', 'Deleted');";
            historyCmd.Parameters.AddWithValue("$id", id);
            if (historyCmd.ExecuteScalar() is string text
                && DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) when = parsed;
        }

        return new TaskLocation(id, title, archived, deleted, when);
    }
}
