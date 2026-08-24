using KanbanApp.Models;

namespace KanbanApp.Services;

// A card's full lifecycle: read (with its flags/sub-tasks/attachments joined in), create, edit,
// reorder, move between columns, archive/reactivate, soft-delete/reactivate, and permanent delete.
public partial class DatabaseService
{
    public List<CardItem> GetCards(bool archivedOnly = false)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        var archivedAtColumn = archivedOnly
            ? ", (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = Cards.Id AND h.EventType = 'Archived')"
            : "";
        cmd.CommandText = $"""
            SELECT Id, ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, WhoId, LastUpdated, IsRecurring, RecurrencePattern, GoalId, Notes, IsImported, ForceEditOnComplete, NextOccurrenceSpawned{archivedAtColumn}
            FROM Cards WHERE IsArchived = {(archivedOnly ? 1 : 0)} AND IsDeleted = 0 ORDER BY SortOrder;
            """;

        var result = new List<CardItem>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(new CardItem
                {
                    Id = reader.GetInt32(0),
                    ColumnId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    ProjectId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Priority = reader.GetString(5),
                    DueDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                    WhoId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    LastUpdated = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    IsRecurring = reader.GetInt32(9) != 0,
                    RecurrencePattern = reader.IsDBNull(10) ? null : reader.GetString(10),
                    GoalId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    Notes = reader.IsDBNull(12) ? null : reader.GetString(12),
                    IsImported = reader.GetInt32(13) != 0,
                    ForceEditOnComplete = reader.GetInt32(14) != 0,
                    NextOccurrenceSpawned = reader.GetInt32(15) != 0,
                    ArchivedAt = archivedOnly && !reader.IsDBNull(16) ? DateTime.Parse(reader.GetString(16)) : null
                });
            }
        }

        using (var flagCmd = connection.CreateCommand())
        {
            flagCmd.CommandText = "SELECT CardId, FlagId FROM CardFlags;";
            using var reader = flagCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(0);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.FlagIds.Add(reader.GetInt32(1));
                }
            }
        }

        using (var subCmd = connection.CreateCommand())
        {
            subCmd.CommandText = "SELECT Id, CardId, Title, IsDone, SortOrder FROM SubTasks ORDER BY CardId, SortOrder;";
            using var reader = subCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(1);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.SubTasks.Add(new SubTaskItem
                    {
                        Id = reader.GetInt32(0),
                        CardId = cardId,
                        Title = reader.GetString(2),
                        IsDone = reader.GetInt32(3) != 0,
                        SortOrder = reader.GetInt32(4)
                    });
                }
            }
        }

        using (var attCmd = connection.CreateCommand())
        {
            attCmd.CommandText = "SELECT Id, CardId, FilePath, DisplayName, AddedDate FROM CardAttachments ORDER BY CardId, Id;";
            using var reader = attCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(1);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.Attachments.Add(new CardAttachment
                    {
                        Id = reader.GetInt32(0),
                        CardId = cardId,
                        FilePath = reader.GetString(2),
                        DisplayName = reader.GetString(3),
                        AddedDate = DateTime.Parse(reader.GetString(4))
                    });
                }
            }
        }

        return result;
    }

    public CardItem AddCard(int columnId, string title, int? projectId, string columnName, string priority, DateTime? dueDate, int? whoId,
        bool isRecurring, string? recurrencePattern, int? goalId, string? notes = null, bool isImported = false, bool forceEditOnComplete = false)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", columnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        var now = NowStamp();
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Cards (ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, WhoId, LastUpdated, IsRecurring, RecurrencePattern, GoalId, Notes, IsImported, ForceEditOnComplete)
            VALUES ($columnId, $title, $sortOrder, $projectId, $priority, $dueDate, $whoId, $lastUpdated, $isRecurring, $recurrencePattern, $goalId, $notes, $isImported, $forceEditOnComplete);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$columnId", columnId);
        insertCmd.Parameters.AddWithValue("$title", title);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        insertCmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$priority", priority);
        insertCmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$whoId", (object?)whoId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$lastUpdated", now);
        insertCmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
        insertCmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$isImported", isImported ? 1 : 0);
        insertCmd.Parameters.AddWithValue("$forceEditOnComplete", forceEditOnComplete ? 1 : 0);
        var id = (long)insertCmd.ExecuteScalar()!;

        LogHistory(connection, (int)id, title, "Created", $"Added to {columnName}");

        return new CardItem
        {
            Id = (int)id, ColumnId = columnId, Title = title, SortOrder = (int)sortOrder, ProjectId = projectId,
            Priority = priority, DueDate = dueDate, WhoId = whoId, LastUpdated = DateTime.Parse(now),
            IsRecurring = isRecurring, RecurrencePattern = recurrencePattern, GoalId = goalId, Notes = notes, IsImported = isImported,
            ForceEditOnComplete = forceEditOnComplete
        };
    }

    public void SetCardImported(int cardId, bool isImported)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cards SET IsImported = $isImported WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isImported", isImported ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", cardId);
        cmd.ExecuteNonQuery();
    }

    public DateTime UpdateCard(int cardId, string title, int? projectId, string priority, DateTime? dueDate, int? whoId,
        bool isRecurring, string? recurrencePattern, int? goalId, string? notes = null, bool forceEditOnComplete = false)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Cards SET Title = $title, ProjectId = $projectId, Priority = $priority,
                    DueDate = $dueDate, WhoId = $whoId, LastUpdated = $lastUpdated,
                    IsRecurring = $isRecurring, RecurrencePattern = $recurrencePattern, GoalId = $goalId, Notes = $notes,
                    ForceEditOnComplete = $forceEditOnComplete
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priority", priority);
            cmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$whoId", (object?)whoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lastUpdated", now);
            cmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
            cmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$forceEditOnComplete", forceEditOnComplete ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, title, "Edited", "Task details updated");
        return DateTime.Parse(now);
    }

    public void UpdateSortOrders(IEnumerable<(int CardId, int SortOrder)> updates)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "UPDATE Cards SET SortOrder = $sortOrder WHERE Id = $id;";
        var sortOrderParam = cmd.CreateParameter();
        sortOrderParam.ParameterName = "$sortOrder";
        cmd.Parameters.Add(sortOrderParam);
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "$id";
        cmd.Parameters.Add(idParam);

        foreach (var (cardId, sortOrder) in updates)
        {
            sortOrderParam.Value = sortOrder;
            idParam.Value = cardId;
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public DateTime MoveCard(int cardId, int newColumnId, string cardTitle, string fromColumnName, string toColumnName)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", newColumnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE Cards SET ColumnId = $columnId, SortOrder = $sortOrder, LastUpdated = $lastUpdated WHERE Id = $id;";
        updateCmd.Parameters.AddWithValue("$columnId", newColumnId);
        updateCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        updateCmd.Parameters.AddWithValue("$lastUpdated", now);
        updateCmd.Parameters.AddWithValue("$id", cardId);
        updateCmd.ExecuteNonQuery();

        LogHistory(connection, cardId, cardTitle, "Moved", $"Moved from {fromColumnName} to {toColumnName}");
        return DateTime.Parse(now);
    }

    public void DeleteCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET IsDeleted = 1, LastUpdated = $lastUpdated WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$lastUpdated", NowStamp());
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Deleted", $"Deleted from {columnName}");
    }

    public DateTime ReactivateCard(int cardId, string cardTitle)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using var toDoCmd = connection.CreateCommand();
        toDoCmd.CommandText = "SELECT Id FROM Columns WHERE Name = 'To Do' LIMIT 1;";
        var toDoColumnId = (long)toDoCmd.ExecuteScalar()!;

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", toDoColumnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Cards SET IsArchived = 0, IsDeleted = 0, ColumnId = $columnId, SortOrder = $sortOrder, LastUpdated = $lastUpdated
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$columnId", toDoColumnId);
            cmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            cmd.Parameters.AddWithValue("$lastUpdated", now);
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Reactivated", "Moved to To Do");
        return DateTime.Parse(now);
    }

    // Recorded the moment a recurring card spawns its next occurrence, so that reactivating this
    // same card later (e.g. from Archive) and marking it Done again can't spawn a duplicate.
    public void MarkNextOccurrenceSpawned(int cardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cards SET NextOccurrenceSpawned = 1 WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", cardId);
        cmd.ExecuteNonQuery();
    }

    public void ArchiveCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET IsArchived = 1, LastUpdated = $lastUpdated WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$lastUpdated", NowStamp());
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Archived", $"Archived from {columnName}");
    }

    public List<ArchivedCardInfo> GetArchivedCards()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.Title, col.Name, COALESCE(p.Name, 'No Project'),
                (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Archived')
            FROM Cards c
            JOIN Columns col ON col.Id = c.ColumnId
            LEFT JOIN Projects p ON p.Id = c.ProjectId
            WHERE c.IsArchived = 1 AND c.IsDeleted = 0
            ORDER BY c.Id DESC;
            """;

        var result = new List<ArchivedCardInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ArchivedCardInfo
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                ColumnName = reader.GetString(2),
                ProjectName = reader.GetString(3),
                ArchivedAt = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4)
            });
        }
        return result;
    }

    // Irreversibly removes a card and its child rows (flags, sub-tasks, attachments) - only ever
    // called from the Archived/Deleted list views, where the card is already off the live board.
    // CardHistory rows are left in place as an audit trail; nothing else references them by CardId.
    public void PermanentlyDeleteCard(int cardId, string cardTitle, string sourceListName)
    {
        using var connection = OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                DELETE FROM CardFlags WHERE CardId = $id;
                DELETE FROM SubTasks WHERE CardId = $id;
                DELETE FROM CardAttachments WHERE CardId = $id;
                DELETE FROM Cards WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "PermanentlyDeleted", $"Permanently deleted from {sourceListName}");
    }

    public List<DeletedCardInfo> GetDeletedCards()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.Title, col.Name, COALESCE(p.Name, 'No Project'),
                (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Deleted')
            FROM Cards c
            JOIN Columns col ON col.Id = c.ColumnId
            LEFT JOIN Projects p ON p.Id = c.ProjectId
            WHERE c.IsDeleted = 1
            ORDER BY c.Id DESC;
            """;

        var result = new List<DeletedCardInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DeletedCardInfo
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                ColumnName = reader.GetString(2),
                ProjectName = reader.GetString(3),
                DeletedAt = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4)
            });
        }
        return result;
    }
}
