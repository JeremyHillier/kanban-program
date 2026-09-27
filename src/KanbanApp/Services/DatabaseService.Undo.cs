using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// What Undo needs from the task file: a copy of a task exactly as stored, and a way to put that
// copy back. The task's own row is copied column by column without naming the columns, so a field
// added to Cards later is covered by Undo with no change here. Flags and sub-tasks go with it.
// Attachments deliberately do not: their files move around on disk (and are erased when removed
// from a task), so Undo leaves the attachment list as it is and the caller only moves the files
// to suit the restored status.
public sealed class CardSnapshot
{
    public required int CardId { get; init; }
    public required Dictionary<string, object?> Row { get; init; }
    public required List<int> FlagIds { get; init; }
    public List<int> PersonIds { get; init; } = [];
    public required List<(string Title, bool IsDone)> SubTasks { get; init; }
}

public partial class DatabaseService
{
    // A reference to a project, goal or person that has been deleted since the snapshot was taken
    // is restored as "none" instead, the same as deleting it would have left the task.
    private static readonly (string Column, string Table)[] SnapshotReferences =
        [("ProjectId", "Projects"), ("GoalId", "Goals"), ("WhoId", "People")];

    public List<CardSnapshot> SnapshotCards(IReadOnlyCollection<int> cardIds)
    {
        var result = new List<CardSnapshot>();
        if (cardIds.Count == 0) return result;

        using var connection = OpenConnection();
        var idList = string.Join(",", cardIds.Distinct());
        var byId = new Dictionary<int, CardSnapshot>();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM Cards WHERE Id IN ({idList});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                var snapshot = new CardSnapshot { CardId = Convert.ToInt32(row["Id"]), Row = row, FlagIds = [], SubTasks = [] };
                byId[snapshot.CardId] = snapshot;
                result.Add(snapshot);
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT CardId, FlagId FROM CardFlags WHERE CardId IN ({idList});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) byId[reader.GetInt32(0)].FlagIds.Add(reader.GetInt32(1));
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT CardId, PersonId FROM CardPeople WHERE CardId IN ({idList}) ORDER BY CardId, SortOrder;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) byId[reader.GetInt32(0)].PersonIds.Add(reader.GetInt32(1));
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT CardId, Title, IsDone FROM SubTasks WHERE CardId IN ({idList}) ORDER BY CardId, SortOrder;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) byId[reader.GetInt32(0)].SubTasks.Add((reader.GetString(1), reader.GetInt32(2) != 0));
        }

        return result;
    }

    // Puts each task back exactly as it was snapshotted, all in one transaction, and notes it in
    // the task's history. A task that no longer exists (erased from the Deleted list since) is
    // skipped. Returns the ids that were restored.
    public List<int> RestoreCards(IEnumerable<CardSnapshot> snapshots, string undoneAction)
    {
        var restored = new List<int>();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var snapshot in snapshots)
        {
            var row = new Dictionary<string, object?>(snapshot.Row);
            row.Remove("Id");
            foreach (var (column, table) in SnapshotReferences)
            {
                if (row.GetValueOrDefault(column) is { } id && !Exists(connection, table, Convert.ToInt32(id))) row[column] = null;
            }

            var names = row.Keys.ToList();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"UPDATE Cards SET {string.Join(", ", names.Select((n, i) => $"\"{n}\" = $p{i}"))} WHERE Id = $id;";
                for (var i = 0; i < names.Count; i++) cmd.Parameters.AddWithValue($"$p{i}", row[names[i]] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$id", snapshot.CardId);
                if (cmd.ExecuteNonQuery() == 0) continue;
            }

            Execute(connection, "DELETE FROM CardFlags WHERE CardId = $id;", ("$id", snapshot.CardId));
            foreach (var flagId in snapshot.FlagIds.Where(f => Exists(connection, "Flags", f)))
            {
                Execute(connection, "INSERT INTO CardFlags (CardId, FlagId) VALUES ($id, $flagId);", ("$id", snapshot.CardId), ("$flagId", flagId));
            }

            // People who have been deleted since are dropped; the lead (WhoId) follows the list, so
            // if it was the lead who went, the next person steps up.
            WriteCardPeople(connection, transaction, snapshot.CardId, snapshot.PersonIds.Where(p => Exists(connection, "People", p)).ToList());

            Execute(connection, "DELETE FROM SubTasks WHERE CardId = $id;", ("$id", snapshot.CardId));
            for (var i = 0; i < snapshot.SubTasks.Count; i++)
            {
                Execute(connection, "INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES ($id, $title, $isDone, $sortOrder);",
                    ("$id", snapshot.CardId), ("$title", snapshot.SubTasks[i].Title), ("$isDone", snapshot.SubTasks[i].IsDone ? 1 : 0), ("$sortOrder", i));
            }

            LogHistory(connection, snapshot.CardId, row.GetValueOrDefault("Title")?.ToString() ?? string.Empty, "Undone", $"Undid: {undoneAction}");
            restored.Add(snapshot.CardId);
        }

        transaction.Commit();
        return restored;
    }

    // Ids of the tasks on the board in a column, in stored order - how Undo finds where a restored
    // task belongs among the cards already showing.
    public List<int> GetColumnCardOrder(int columnId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Cards WHERE ColumnId = $columnId AND IsArchived = 0 AND IsDeleted = 0 ORDER BY SortOrder, Id;";
        cmd.Parameters.AddWithValue("$columnId", columnId);
        var result = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetInt32(0));
        return result;
    }

    private static bool Exists(SqliteConnection connection, string table, int id)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {table} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is not null;
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
