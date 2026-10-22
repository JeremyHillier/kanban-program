using KanbanApp.Models;

namespace KanbanApp.Services;

// The Goals managed list.
public partial class DatabaseService
{
    public List<Goal> GetGoals() =>
        GetListEntries<Goal>("Goals", ", TargetDate", (reader, goal) =>
            goal.TargetDate = !reader.IsDBNull(4) && DateTime.TryParseExact(reader.GetString(4), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : null);

    public Goal AddGoal(string name) => AddListEntry<Goal>("Goals", name);

    public void RenameGoal(int goalId, string name) => RenameListEntry("Goals", goalId, name);

    public void SetGoalActive(int goalId, bool isActive) => SetListEntryActive("Goals", goalId, isActive);

    // Older copies of the app do not know the column, so the file is only stamped as newer once a target
    // date has been set (see RaiseFileFormat).
    public void SetGoalTarget(int goalId, DateTime? target)
    {
        using (var connection = OpenConnection())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Goals SET TargetDate = $target WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$target", target is { } date ? date.ToString("yyyy-MM-dd") : DBNull.Value);
            cmd.Parameters.AddWithValue("$id", goalId);
            cmd.ExecuteNonQuery();
        }

        if (target is not null) RaiseFileFormat(GoalTargetFileFormat);
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
