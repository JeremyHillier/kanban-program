using System.Globalization;
using KanbanApp.Models;

namespace KanbanApp.Services;

// When a task was emailed, and to whom: one "Emailed" row in CardHistory each time, so the task
// screen can say when it was last shared. Nothing is stored on the card row itself.
public partial class DatabaseService
{
    public const string EmailedEvent = "Emailed";

    public void RecordCardEmailed(int cardId, string cardTitle, string details)
    {
        using var connection = OpenConnection();
        LogHistory(connection, cardId, cardTitle, EmailedEvent, details);
    }

    // Newest first.
    public List<CardEmailRecord> GetCardEmailHistory(int cardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Timestamp, Details FROM CardHistory WHERE CardId = $cardId AND EventType = $event ORDER BY Timestamp DESC, Id DESC;";
        cmd.Parameters.AddWithValue("$cardId", cardId);
        cmd.Parameters.AddWithValue("$event", EmailedEvent);

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
