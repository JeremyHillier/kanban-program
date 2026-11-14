using KanbanApp.Models;

namespace KanbanApp.Services;

// A card's sub-task checklist: replace-all on save (mirrors SetCardFlags/SetCardAttachments) and
// the single-item done/undone toggle used by the board's inline checkbox.
public partial class DatabaseService
{
    // Replaces the task's sub-tasks and hands back the stored items with their new ids. The history
    // line (added, removed, done, reopened - see DescribeSubTaskChanges) is written in the same
    // transaction; record: false (a task being created or copied) writes none.
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

        if (record) LogChanges(connection, cardId, DescribeSubTaskChanges(before, subTasks));
        transaction.Commit();
        return result;
    }

    // Ticks or unticks one sub-task and writes its history line in the same transaction. A sub-task
    // that is gone (an older row id) is a no-op.
    public void SetSubTaskDone(int subTaskId, bool isDone)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE SubTasks SET IsDone = $isDone WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", subTaskId);
            cmd.ExecuteNonQuery();
        }

        // The sub-task's card and title, and the card's title for the history row, in one read.
        using (var findCmd = connection.CreateCommand())
        {
            findCmd.Transaction = transaction;
            findCmd.CommandText = """
                SELECT s.CardId, s.Title, c.Title FROM SubTasks s LEFT JOIN Cards c ON c.Id = s.CardId WHERE s.Id = $id;
                """;
            findCmd.Parameters.AddWithValue("$id", subTaskId);
            using var r = findCmd.ExecuteReader();
            if (r.Read())
            {
                var cardId = r.GetInt32(0);
                var title = r.GetString(1);
                var cardTitle = r.IsDBNull(2) ? string.Empty : r.GetString(2);
                r.Close();
                LogHistory(connection, cardId, cardTitle, "Edited", $"Sub-task {(isDone ? "done" : "reopened")}: \"{title}\"");
            }
        }

        transaction.Commit();
    }
}
