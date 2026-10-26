using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The formatting of a task's Notes, kept beside the plain text rather than instead of it. The plain Notes stay the
// truth for everything that reads them; this only dresses them up in the task window. It records the plain text it
// was made from, and is ignored the moment the task's Notes no longer say the same (an import, an older copy, Undo
// changed them), so the two can never disagree. Older copies of the app ignore the table.
public partial class DatabaseService
{
    private static void EnsureRichNotesTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS CardRichNotes (
                CardId INTEGER PRIMARY KEY REFERENCES Cards(Id) ON DELETE CASCADE,
                Plain TEXT NOT NULL,
                Content TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // The stored formatting, when it was made from exactly these Notes; otherwise null.
    public string? GetRichNotes(int cardId, string? currentNotes)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Plain, Content FROM CardRichNotes WHERE CardId = $id;";
        cmd.Parameters.AddWithValue("$id", cardId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return string.Equals(reader.GetString(0), currentNotes ?? string.Empty, StringComparison.Ordinal) ? reader.GetString(1) : null;
    }

    // content null removes the formatting. The file is only stamped as newer once formatting has been saved.
    public void SetRichNotes(int cardId, string plain, string? content)
    {
        using (var connection = OpenConnection())
        using (var cmd = connection.CreateCommand())
        {
            if (content is null)
            {
                cmd.CommandText = "DELETE FROM CardRichNotes WHERE CardId = $id;";
                cmd.Parameters.AddWithValue("$id", cardId);
            }
            else
            {
                cmd.CommandText = "INSERT INTO CardRichNotes (CardId, Plain, Content) VALUES ($id, $plain, $content) ON CONFLICT(CardId) DO UPDATE SET Plain = $plain, Content = $content;";
                cmd.Parameters.AddWithValue("$id", cardId);
                cmd.Parameters.AddWithValue("$plain", plain);
                cmd.Parameters.AddWithValue("$content", content);
            }
            cmd.ExecuteNonQuery();
        }

        if (content is not null) RaiseFileFormat(RichNotesFileFormat);
    }
}
