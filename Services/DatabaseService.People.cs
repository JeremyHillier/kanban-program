using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The People ("Who") managed list. AddPerson has an internal (SqliteConnection) overload so
// Schema.cs's legacy-Who backfill can create each person on the same connection as its migration.
public partial class DatabaseService
{
    public List<Person> GetPeople()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive, Email FROM People ORDER BY Name COLLATE NOCASE;";

        var result = new List<Person>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Person
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0,
                Email = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return result;
    }

    public Person AddPerson(string name)
    {
        using var connection = OpenConnection();
        return AddPerson(name, connection);
    }

    private Person AddPerson(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM People;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO People (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Person { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenamePerson(int personId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void SetPersonEmail(int personId, string? email)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET Email = $email WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void SetPersonActive(int personId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void DeletePerson(int personId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET WhoId = NULL WHERE WhoId = $id;";
            clearCmd.Parameters.AddWithValue("$id", personId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM People WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", personId);
        deleteCmd.ExecuteNonQuery();
    }
}
