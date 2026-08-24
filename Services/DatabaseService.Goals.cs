using KanbanApp.Models;

namespace KanbanApp.Services;

// The Goals managed list.
public partial class DatabaseService
{
    public List<Goal> GetGoals()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Goals ORDER BY Name COLLATE NOCASE;";

        var result = new List<Goal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Goal
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public Goal AddGoal(string name)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Goals;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Goals (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Goal { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameGoal(int goalId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Goals SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", goalId);
        cmd.ExecuteNonQuery();
    }

    public void SetGoalActive(int goalId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Goals SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", goalId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGoal(int goalId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET GoalId = NULL WHERE GoalId = $id;";
            clearCmd.Parameters.AddWithValue("$id", goalId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Goals WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", goalId);
        deleteCmd.ExecuteNonQuery();
    }
}
