using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The Projects managed list. AddProject has an internal (SqliteConnection) overload so Schema.cs
// can seed the default "General" project using the same connection/transaction as Initialize().
public partial class DatabaseService
{
    public List<Project> GetProjects() => GetListEntries<Project>("Projects");

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

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Projects WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", projectId);
        deleteCmd.ExecuteNonQuery();
    }
}
