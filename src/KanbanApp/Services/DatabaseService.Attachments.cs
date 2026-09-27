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
        using var transaction = connection.BeginTransaction();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = transaction;
            clearCmd.CommandText = "DELETE FROM CardAttachments WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<CardAttachment>();

        // One transaction and one reused command for the whole set - see the note on
        // DatabaseService.SetCardFlags for why the per-row version was the expensive part.
        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO CardAttachments (CardId, FilePath, DisplayName, AddedDate) VALUES ($cardId, $filePath, $displayName, $addedDate);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            var filePathParam = insertCmd.CreateParameter();
            filePathParam.ParameterName = "$filePath";
            insertCmd.Parameters.Add(filePathParam);
            var displayNameParam = insertCmd.CreateParameter();
            displayNameParam.ParameterName = "$displayName";
            insertCmd.Parameters.Add(displayNameParam);
            var addedDateParam = insertCmd.CreateParameter();
            addedDateParam.ParameterName = "$addedDate";
            insertCmd.Parameters.Add(addedDateParam);

            foreach (var (filePath, displayName, addedDate) in attachments)
            {
                filePathParam.Value = filePath;
                displayNameParam.Value = displayName;
                addedDateParam.Value = addedDate.ToString("yyyy-MM-dd HH:mm:ss");
                var id = (long)insertCmd.ExecuteScalar()!;

                result.Add(new CardAttachment { Id = (int)id, CardId = cardId, FilePath = filePath, DisplayName = displayName, AddedDate = addedDate });
            }
        }

        transaction.Commit();
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
