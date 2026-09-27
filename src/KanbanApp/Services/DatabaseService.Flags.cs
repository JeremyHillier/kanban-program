using KanbanApp.Models;

namespace KanbanApp.Services;

// The Flags managed list and the many-to-many CardFlags join table.
public partial class DatabaseService
{
    public List<Flag> GetFlags() => GetListEntries<Flag>("Flags");

    public Flag AddFlag(string name) => AddListEntry<Flag>("Flags", name);

    public void RenameFlag(int flagId, string name) => RenameListEntry("Flags", flagId, name);

    public void SetFlagActive(int flagId, bool isActive) => SetListEntryActive("Flags", flagId, isActive);

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
