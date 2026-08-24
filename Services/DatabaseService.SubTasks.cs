using KanbanApp.Models;

namespace KanbanApp.Services;

// A card's sub-task checklist: replace-all on save (mirrors SetCardFlags/SetCardAttachments) and
// the single-item done/undone toggle used by the board's inline checkbox.
public partial class DatabaseService
{
    public List<SubTaskItem> SetCardSubTasks(int cardId, List<(string Title, bool IsDone)> subTasks)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM SubTasks WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<SubTaskItem>();
        var sortOrder = 0;
        foreach (var (title, isDone) in subTasks)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES ($cardId, $title, $isDone, $sortOrder);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            insertCmd.Parameters.AddWithValue("$title", title);
            insertCmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
            insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            var id = (long)insertCmd.ExecuteScalar()!;

            result.Add(new SubTaskItem { Id = (int)id, CardId = cardId, Title = title, IsDone = isDone, SortOrder = sortOrder });
            sortOrder++;
        }

        return result;
    }

    public void SetSubTaskDone(int subTaskId, bool isDone)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE SubTasks SET IsDone = $isDone WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", subTaskId);
        cmd.ExecuteNonQuery();
    }
}
