using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The Projects managed list. AddProject has an internal (SqliteConnection) overload so Schema.cs
// can seed the default "General" project using the same connection/transaction as Initialize().
public partial class DatabaseService
{
    public List<Project> GetProjects() =>
        GetListEntries<Project>("Projects", ", TargetDate, StartDate", (reader, project) =>
        {
            project.TargetDate = ReadDay(reader, 4);
            project.StartDate = ReadDay(reader, 5);
        });

    public void SetProjectTarget(int projectId, DateTime? target) => SetProjectDay(projectId, "TargetDate", target);

    public void SetProjectStart(int projectId, DateTime? start) => SetProjectDay(projectId, "StartDate", start);

    // Older copies of the app do not know the columns, so the file is only stamped as newer once a date is set.
    private void SetProjectDay(int projectId, string column, DateTime? day)
    {
        using (var connection = OpenConnection())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"UPDATE Projects SET {column} = $day WHERE Id = $id;"; // column is one of two fixed names above
            cmd.Parameters.AddWithValue("$day", day is { } date ? date.ToString("yyyy-MM-dd") : DBNull.Value);
            cmd.Parameters.AddWithValue("$id", projectId);
            cmd.ExecuteNonQuery();
        }

        if (day is not null) RaiseFileFormat(ProjectPlanFileFormat);
    }

    public Project AddProject(string name) => AddListEntry<Project>("Projects", name);

    private static Project AddProject(string name, SqliteConnection connection) => AddListEntry<Project>("Projects", name, connection);

    public void RenameProject(int projectId, string name) => RenameListEntry("Projects", projectId, name);

    public void SetProjectActive(int projectId, bool isActive) => SetListEntryActive("Projects", projectId, isActive);

    public void DeleteProject(int projectId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET ProjectId = NULL WHERE ProjectId = $id;";
            clearCmd.Parameters.AddWithValue("$id", projectId);
            clearCmd.ExecuteNonQuery();
        }

        using (var milestonesCmd = connection.CreateCommand())
        {
            milestonesCmd.CommandText = "DELETE FROM ProjectMilestones WHERE ProjectId = $id;";
            milestonesCmd.Parameters.AddWithValue("$id", projectId);
            milestonesCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Projects WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", projectId);
        deleteCmd.ExecuteNonQuery();
    }
}
