using KanbanApp.Models;

namespace KanbanApp.Services;

// A card's sub-task checklist: replace-all on save (mirrors SetCardFlags/SetCardAttachments) and
// the single-item done/undone toggle used by the board's inline checkbox.
public partial class DatabaseService
{
    public List<SubTaskItem> SetCardSubTasks(int cardId, List<(string Title, bool IsDone)> subTasks, bool record = true)
    {
        using var connection = OpenConnection();
        var before = record ? ReadSubTasks(connection, cardId) : [];
        using var transaction = connection.BeginTransaction();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = transaction;
            clearCmd.CommandText = "DELETE FROM SubTasks WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<SubTaskItem>();

        // One transaction and one reused command for the whole set - see the note on
        // DatabaseService.SetCardFlags for why the per-row version was the expensive part.
        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES ($cardId, $title, $isDone, $sortOrder);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            var titleParam = insertCmd.CreateParameter();
            titleParam.ParameterName = "$title";
            insertCmd.Parameters.Add(titleParam);
            var isDoneParam = insertCmd.CreateParameter();
            isDoneParam.ParameterName = "$isDone";
            insertCmd.Parameters.Add(isDoneParam);
            var sortOrderParam = insertCmd.CreateParameter();
            sortOrderParam.ParameterName = "$sortOrder";
            insertCmd.Parameters.Add(sortOrderParam);

            var sortOrder = 0;
            foreach (var (title, isDone) in subTasks)
            {
                titleParam.Value = title;
                isDoneParam.Value = isDone ? 1 : 0;
                sortOrderParam.Value = sortOrder;
                var id = (long)insertCmd.ExecuteScalar()!;

                result.Add(new SubTaskItem { Id = (int)id, CardId = cardId, Title = title, IsDone = isDone, SortOrder = sortOrder });
                sortOrder++;
            }
        }

        transaction.Commit();
        if (record) LogChanges(connection, cardId, TitleOf(connection, cardId), DescribeSubTaskChanges(before, subTasks));
        return result;
    }

    public void SetSubTaskDone(int subTaskId, bool isDone)
    {
        using var connection = OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE SubTasks SET IsDone = $isDone WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", subTaskId);
            cmd.ExecuteNonQuery();
        }

        using var findCmd = connection.CreateCommand();
        findCmd.CommandText = "SELECT CardId, Title FROM SubTasks WHERE Id = $id;";
        findCmd.Parameters.AddWithValue("$id", subTaskId);
        using var r = findCmd.ExecuteReader();
        if (!r.Read()) return;
        var cardId = r.GetInt32(0);
        var title = r.GetString(1);
        r.Close();
        LogHistory(connection, cardId, TitleOf(connection, cardId), "Edited", $"Sub-task {(isDone ? "done" : "reopened")}: \"{title}\"");
    }
}
