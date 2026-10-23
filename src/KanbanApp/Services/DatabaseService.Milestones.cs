using System.Globalization;
using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// A goal's milestones: a description and a date each. They have a table of their own (deleted with the goal);
// older copies ignore it, so the file is only stamped as newer once a milestone has been saved.
public partial class DatabaseService
{
    private static void EnsureGoalMilestonesTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS GoalMilestones (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GoalId INTEGER NOT NULL REFERENCES Goals(Id) ON DELETE CASCADE,
                Description TEXT NOT NULL,
                MilestoneDate TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public List<GoalMilestone> GetGoalMilestones()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GoalId, Description, MilestoneDate FROM GoalMilestones ORDER BY MilestoneDate, Id;";

        var result = new List<GoalMilestone>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!DateTime.TryParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            result.Add(new GoalMilestone { Id = reader.GetInt32(0), GoalId = reader.GetInt32(1), Description = reader.GetString(2), Date = date });
        }
        return result;
    }

    public GoalMilestone AddGoalMilestone(int goalId, string description, DateTime date)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO GoalMilestones (GoalId, Description, MilestoneDate) VALUES ($goal, $description, $date); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$goal", goalId);
        cmd.Parameters.AddWithValue("$description", description);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var id = (long)cmd.ExecuteScalar()!;

        RaiseFileFormat(GoalMilestoneFileFormat);
        return new GoalMilestone { Id = (int)id, GoalId = goalId, Description = description, Date = date.Date };
    }

    public void UpdateGoalMilestone(int id, string description, DateTime date)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE GoalMilestones SET Description = $description, MilestoneDate = $date WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$description", description);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGoalMilestone(int id)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM GoalMilestones WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
