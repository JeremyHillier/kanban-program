namespace KanbanApp.Services;

// A task stores its priority as the name. The list of names is a setting (PriorityList); these
// keep the tasks in step when a name on that list is changed or taken off it.
public partial class DatabaseService
{
    // Every task with that priority, whatever its capitals and wherever it is (the board, Archived,
    // Deleted), so nothing comes back later under a name that no longer exists. Returns how many.
    public int ChangeTaskPriority(string from, string to)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cards SET Priority = $to WHERE Priority = $from COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("$to", to);
        cmd.Parameters.AddWithValue("$from", from);
        return cmd.ExecuteNonQuery();
    }

    // How many tasks have each priority, counting archived tasks but not deleted ones.
    public Dictionary<string, int> CountTasksByPriority()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Priority, COUNT(*) FROM Cards WHERE IsDeleted = 0 GROUP BY Priority;";

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            counts[name] = counts.GetValueOrDefault(name) + reader.GetInt32(1);
        }
        return counts;
    }
}
