using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The Projects managed list. AddProject has an internal (SqliteConnection) overload so Schema.cs
// can seed the default "General" project using the same connection/transaction as Initialize().
public partial class DatabaseService
{
    public List<Project> GetProjects()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Projects ORDER BY Name COLLATE NOCASE;";

        var result = new List<Project>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Project
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public Project AddProject(string name)
    {
        using var connection = OpenConnection();
        return AddProject(name, connection);
    }

    private Project AddProject(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Projects;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Projects (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Project { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameProject(int projectId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", projectId);
        cmd.ExecuteNonQuery();
    }

    public void SetProjectActive(int projectId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", projectId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteProject(int projectId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET ProjectId = NULL WHERE ProjectId = $id;";
            clearCmd.Parameters.AddWithValue("$id", projectId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Projects WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", projectId);
        deleteCmd.ExecuteNonQuery();
    }
}
