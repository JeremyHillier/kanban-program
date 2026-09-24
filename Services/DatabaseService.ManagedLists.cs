using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Reading, adding, renaming and switching on/off work the same for all four managed lists -
// Projects, Goals, Flags and People - apart from the table, so they live here once. Deleting
// differs per list (what happens to the tasks that use the entry) and stays in each list's file.
// The table names are fixed in code, never user text.
public partial class DatabaseService
{
    private List<T> GetListEntries<T>(string table, string extraColumns = "", Action<SqliteDataReader, T>? readExtra = null)
        where T : ManagedListEntry, new()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name, SortOrder, IsActive{extraColumns} FROM {table} ORDER BY Name COLLATE NOCASE;";

        var result = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var entry = new T
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            };
            readExtra?.Invoke(reader, entry);
            result.Add(entry);
        }
        return result;
    }

    // New entries go at the end of the list's order.
    private static T AddListEntry<T>(string table, string name, SqliteConnection connection) where T : ManagedListEntry, new()
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = $"SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM {table};";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = $"INSERT INTO {table} (Name, SortOrder) VALUES ($name, $sortOrder); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new T { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    private T AddListEntry<T>(string table, string name) where T : ManagedListEntry, new()
    {
        using var connection = OpenConnection();
        return AddListEntry<T>(table, name, connection);
    }

    private void RenameListEntry(string table, int id, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private void SetListEntryActive(string table, int id, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
