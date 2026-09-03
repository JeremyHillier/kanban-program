using KanbanApp.Models;

namespace KanbanApp.Services;

// The Flags managed list and the many-to-many CardFlags join table.
public partial class DatabaseService
{
    public List<Flag> GetFlags()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Flags ORDER BY Name COLLATE NOCASE;";

        var result = new List<Flag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Flag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public Flag AddFlag(string name)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Flags;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Flags (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Flag { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameFlag(int flagId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Flags SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", flagId);
        cmd.ExecuteNonQuery();
    }

    public void SetFlagActive(int flagId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Flags SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", flagId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteFlag(int flagId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM CardFlags WHERE FlagId = $id;";
            clearCmd.Parameters.AddWithValue("$id", flagId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Flags WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", flagId);
        deleteCmd.ExecuteNonQuery();
    }

    // Delete-then-reinsert, in one transaction with a single reused command - same shape as
    // UpdateSortOrders. Without the transaction SQLite commits (and fsyncs) once per row, which is
    // the dominant cost of saving a task; without hoisting the command out of the loop, every row
    // also re-parses the same INSERT.
    public void SetCardFlags(int cardId, IEnumerable<int> flagIds)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = transaction;
            clearCmd.CommandText = "DELETE FROM CardFlags WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = "INSERT INTO CardFlags (CardId, FlagId) VALUES ($cardId, $flagId);";
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            var flagIdParam = insertCmd.CreateParameter();
            flagIdParam.ParameterName = "$flagId";
            insertCmd.Parameters.Add(flagIdParam);

            foreach (var flagId in flagIds)
            {
                flagIdParam.Value = flagId;
                insertCmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }
}
