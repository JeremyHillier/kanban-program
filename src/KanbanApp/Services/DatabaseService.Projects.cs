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

    // Takes the project off its tasks, archived and deleted ones too, or with moveTasksTo puts them in that
    // project instead. Its milestones go with it. All in one step, so nothing is left half done.
    public void DeleteProject(int projectId, int? moveTasksTo = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        void Run(string sql, object? newProject = null)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", projectId);
            if (newProject is not null) cmd.Parameters.AddWithValue("$new", newProject);
            cmd.ExecuteNonQuery();
        }

        Run("UPDATE Cards SET ProjectId = $new WHERE ProjectId = $id;", moveTasksTo is { } id && id != projectId ? id : DBNull.Value);
        Run("DELETE FROM ProjectMilestones WHERE ProjectId = $id;");
        Run("DELETE FROM Projects WHERE Id = $id;");

        transaction.Commit();
    }
}
