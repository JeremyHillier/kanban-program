using KanbanApp.Models;

namespace KanbanApp.Services;

// The board's columns: the five every file starts with (To Do, In Progress, On Hold, Waiting, Done), which can
// only be renamed, and up to five more the user adds (Models.KanbanColumn.IsCustom), which can also be moved and
// removed.
public partial class DatabaseService
{
    public List<KanbanColumn> GetColumns()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, DisplayName, SortOrder FROM Columns ORDER BY SortOrder;";

        var result = new List<KanbanColumn>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            result.Add(new KanbanColumn
            {
                Id = reader.GetInt32(0),
                Name = name,
                DisplayName = reader.IsDBNull(2) ? name : reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return result;
    }

    public void RenameColumnDisplayName(int columnId, string displayName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Columns SET DisplayName = $displayName WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$displayName", displayName);
        cmd.Parameters.AddWithValue("$id", columnId);
        cmd.ExecuteNonQuery();
    }

    // Adds a column just before Done (at the end if there is no Done). Its Name comes from its own row id, so no
    // two columns ever share one, even over a task file's whole life: the history names columns by it.
    public KanbanColumn AddCustomColumn(string displayName)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        long sortOrder;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT COALESCE((SELECT MIN(SortOrder) FROM Columns WHERE Name = 'Done'), (SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Columns));";
            sortOrder = (long)cmd.ExecuteScalar()!;
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE Columns SET SortOrder = SortOrder + 1 WHERE SortOrder >= $sortOrder;";
            cmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            cmd.ExecuteNonQuery();
        }

        long id;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Columns (Name, DisplayName, SortOrder) VALUES ('', $displayName, $sortOrder); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$displayName", displayName);
            cmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            id = (long)cmd.ExecuteScalar()!;
        }

        var name = $"Custom {id}";
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE Columns SET Name = $name WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        return new KanbanColumn { Id = (int)id, Name = name, DisplayName = displayName, SortOrder = (int)sortOrder };
    }

    // Saves the board's column order: the ids, left to right.
    public void SetColumnOrder(IReadOnlyList<int> columnIds)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "UPDATE Columns SET SortOrder = $sortOrder WHERE Id = $id;";
        var sortOrder = cmd.CreateParameter();
        sortOrder.ParameterName = "$sortOrder";
        cmd.Parameters.Add(sortOrder);
        var id = cmd.CreateParameter();
        id.ParameterName = "$id";
        cmd.Parameters.Add(id);

        for (var i = 0; i < columnIds.Count; i++)
        {
            sortOrder.Value = i;
            id.Value = columnIds[i];
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // Every task in the column: on the board, archived or deleted. The link from a task to its column does not
    // allow a column to go while any is left in it.
    public int CountCardsInColumn(int columnId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Cards WHERE ColumnId = $id;";
        cmd.Parameters.AddWithValue("$id", columnId);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    // Removes a column the user added. Its tasks - archived and deleted ones too - go to the end of another
    // column, in the order they had, each with a Moved entry in its history. Returns how many moved.
    public int DeleteColumn(int columnId, int moveToColumnId)
    {
        using var connection = OpenConnection();

        string NameOf(int id)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Columns WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return (string)cmd.ExecuteScalar()!;
        }

        var fromName = NameOf(columnId);
        if (!new KanbanColumn { Name = fromName }.IsCustom) throw new InvalidOperationException("Only a column that was added can be removed.");
        var toName = NameOf(moveToColumnId);

        var tasks = new List<(int Id, string Title)>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Title FROM Cards WHERE ColumnId = $id ORDER BY SortOrder, Id;";
            cmd.Parameters.AddWithValue("$id", columnId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tasks.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        long next;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $id;";
            cmd.Parameters.AddWithValue("$id", moveToColumnId);
            next = (long)cmd.ExecuteScalar()!;
        }

        foreach (var (id, title) in tasks)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE Cards SET ColumnId = $to, SortOrder = $sortOrder WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$to", moveToColumnId);
                cmd.Parameters.AddWithValue("$sortOrder", next++);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            LogHistory(connection, id, title, "Moved", $"Moved from {fromName} to {toName}");
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Columns WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", columnId);
            cmd.ExecuteNonQuery();
        }

        return tasks.Count;
    }
}
