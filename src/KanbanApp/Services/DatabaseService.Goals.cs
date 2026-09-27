using KanbanApp.Models;

namespace KanbanApp.Services;

// The Goals managed list.
public partial class DatabaseService
{
    public List<Goal> GetGoals() => GetListEntries<Goal>("Goals");

    public Goal AddGoal(string name) => AddListEntry<Goal>("Goals", name);

    public void RenameGoal(int goalId, string name) => RenameListEntry("Goals", goalId, name);

    public void SetGoalActive(int goalId, bool isActive) => SetListEntryActive("Goals", goalId, isActive);

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
