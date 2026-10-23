using System.Globalization;
using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Milestones of a goal or a project: a description and a date each, in a table per owner (deleted with the owner).
// Older copies ignore them, so the file is only stamped as newer once one has been saved. The table and column
// names are fixed in code, never user text.
public partial class DatabaseService
{
    private static (string Table, string OwnerColumn) MilestoneTable(MilestoneOwner owner) =>
        owner == MilestoneOwner.Goal ? ("GoalMilestones", "GoalId") : ("ProjectMilestones", "ProjectId");

    private static void EnsureMilestoneTables(SqliteConnection connection)
    {
        foreach (var owner in new[] { MilestoneOwner.Goal, MilestoneOwner.Project })
        {
            var (table, column) = MilestoneTable(owner);
            var parent = owner == MilestoneOwner.Goal ? "Goals" : "Projects";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {table} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    {column} INTEGER NOT NULL REFERENCES {parent}(Id) ON DELETE CASCADE,
                    Description TEXT NOT NULL,
                    MilestoneDate TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public List<Milestone> GetMilestones(MilestoneOwner owner)
    {
        var (table, column) = MilestoneTable(owner);
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Id, {column}, Description, MilestoneDate FROM {table} ORDER BY MilestoneDate, Id;";

        var result = new List<Milestone>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!DateTime.TryParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            result.Add(new Milestone { Id = reader.GetInt32(0), OwnerId = reader.GetInt32(1), Owner = owner, Description = reader.GetString(2), Date = date });
        }
        return result;
    }

    public Milestone AddMilestone(MilestoneOwner owner, int ownerId, string description, DateTime date)
    {
        var (table, column) = MilestoneTable(owner);
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {table} ({column}, Description, MilestoneDate) VALUES ($owner, $description, $date); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$owner", ownerId);
        cmd.Parameters.AddWithValue("$description", description);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var id = (long)cmd.ExecuteScalar()!;

        RaiseFileFormat(owner == MilestoneOwner.Goal ? GoalMilestoneFileFormat : ProjectPlanFileFormat);
        return new Milestone { Id = (int)id, OwnerId = ownerId, Owner = owner, Description = description, Date = date.Date };
    }

    public void UpdateMilestone(MilestoneOwner owner, int id, string description, DateTime date)
    {
        var (table, _) = MilestoneTable(owner);
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET Description = $description, MilestoneDate = $date WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$description", description);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteMilestone(MilestoneOwner owner, int id)
    {
        var (table, _) = MilestoneTable(owner);
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
