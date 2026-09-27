using KanbanApp.Models;

namespace KanbanApp.Services;

// The board's fixed columns (To Do, In Progress, On Hold, Waiting, Done) - reading them and
// renaming a column's display label. There's no add/remove: that's by design (see Models.KanbanColumn).
public partial class DatabaseService
{
    public List<KanbanColumn> GetColumns()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, DisplayName, SortOrder FROM Columns ORDER BY SortOrder;";

        var result = new List<KanbanColumn>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            result.Add(new KanbanColumn
            {
                Id = reader.GetInt32(0),
                Name = name,
                DisplayName = reader.IsDBNull(2) ? name : reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return result;
    }

    public void RenameColumnDisplayName(int columnId, string displayName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Columns SET DisplayName = $displayName WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$displayName", displayName);
        cmd.Parameters.AddWithValue("$id", columnId);
        cmd.ExecuteNonQuery();
    }
}
