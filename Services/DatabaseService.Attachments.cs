using KanbanApp.Models;

namespace KanbanApp.Services;

// A card's attachment rows: replace-all on save, listing a card's file paths (used by
// MainViewModel to clean up files on disk), and checking whether a file path is still referenced
// by some other card before it's safe to delete/move on disk.
public partial class DatabaseService
{
    public List<CardAttachment> SetCardAttachments(int cardId, List<(string FilePath, string DisplayName, DateTime AddedDate)> attachments)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM CardAttachments WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<CardAttachment>();
        foreach (var (filePath, displayName, addedDate) in attachments)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO CardAttachments (CardId, FilePath, DisplayName, AddedDate) VALUES ($cardId, $filePath, $displayName, $addedDate);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            insertCmd.Parameters.AddWithValue("$filePath", filePath);
            insertCmd.Parameters.AddWithValue("$displayName", displayName);
            insertCmd.Parameters.AddWithValue("$addedDate", addedDate.ToString("yyyy-MM-dd HH:mm:ss"));
            var id = (long)insertCmd.ExecuteScalar()!;

            result.Add(new CardAttachment { Id = (int)id, CardId = cardId, FilePath = filePath, DisplayName = displayName, AddedDate = addedDate });
        }

        return result;
    }

    public bool IsAttachmentPathReferencedElsewhere(string filePath, int excludingCardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM CardAttachments WHERE FilePath = $filePath AND CardId != $cardId;";
        cmd.Parameters.AddWithValue("$filePath", filePath);
        cmd.Parameters.AddWithValue("$cardId", excludingCardId);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public List<string> GetCardAttachmentPaths(int cardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath FROM CardAttachments WHERE CardId = $cardId;";
        cmd.Parameters.AddWithValue("$cardId", cardId);

        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }
}
