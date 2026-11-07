using System.Globalization;
using KanbanApp.Models;

namespace KanbanApp.Services;

// Links between tasks (CardLinks): which task depends on, blocks, is related to or is the parent of
// another, by the tasks' own ids. Both ends are foreign keys, so deleting a task for good takes its
// links with it; a task that is only archived or deleted keeps them. Replace-all per task on save,
// as flags and sub-tasks are.
public partial class DatabaseService
{
    public List<CardLink> GetCardLinks()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FromId, ToId, Kind FROM CardLinks ORDER BY FromId, ToId;";
        var links = new List<CardLink>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (Enum.TryParse<LinkKind>(reader.GetString(2), out var kind)) links.Add(new CardLink(reader.GetInt32(0), reader.GetInt32(1), kind));
        }
        return links;
    }

    // Every link that has the task at either end is replaced by `links`.
    public void SetCardLinks(int cardId, IReadOnlyList<CardLink> links)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM CardLinks WHERE FromId = $id OR ToId = $id;";
            clear.Parameters.AddWithValue("$id", cardId);
            clear.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO CardLinks (FromId, ToId, Kind) VALUES ($from, $to, $kind);";
            var from = insert.CreateParameter(); from.ParameterName = "$from"; insert.Parameters.Add(from);
            var to = insert.CreateParameter(); to.ParameterName = "$to"; insert.Parameters.Add(to);
            var kind = insert.CreateParameter(); kind.ParameterName = "$kind"; insert.Parameters.Add(kind);
            foreach (var link in links)
            {
                from.Value = link.FromId;
                to.Value = link.ToId;
                kind.Value = link.Kind.ToString();
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
        if (links.Count > 0) RaiseFileFormat(RelationshipFileFormat);
    }

    // Title, column and whether it was put away, for tasks that may no longer be on the board.
    public Dictionary<int, TaskBrief> GetTaskBriefs(IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().ToList();
        var briefs = new Dictionary<int, TaskBrief>();
        if (wanted.Count == 0) return briefs;

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        var names = new List<string>();
        for (var i = 0; i < wanted.Count; i++)
        {
            names.Add($"$id{i.ToString(CultureInfo.InvariantCulture)}");
            cmd.Parameters.AddWithValue(names[i], wanted[i]);
        }
        cmd.CommandText = $"SELECT c.Id, c.Title, col.Name, c.IsArchived, c.IsDeleted FROM Cards c JOIN Columns col ON col.Id = c.ColumnId WHERE c.Id IN ({string.Join(", ", names)});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            briefs[reader.GetInt32(0)] = new TaskBrief(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3) != 0, reader.GetInt64(4) != 0);
        }
        return briefs;
    }
}
