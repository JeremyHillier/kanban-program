namespace KanbanApp.Services;

// The "needs review" mark on a task entered with Quick Add. Its own write, like the colour, so a save from the
// task window never touches it; only Quick Add sets it and only the user's Mark Reviewed clears it.
public partial class DatabaseService
{
    public void SetCardNeedsReview(int cardId, bool needsReview)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE Cards SET NeedsReview = $value WHERE Id = $id AND NeedsReview <> $value;";
            cmd.Parameters.AddWithValue("$value", needsReview ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", cardId);
            if (cmd.ExecuteNonQuery() == 0) return; // already so: nothing to record
        }

        if (!needsReview) LogHistory(connection, cardId, TitleOf(connection, cardId), "Edited", "Quick entry reviewed");
        transaction.Commit();
    }
}
