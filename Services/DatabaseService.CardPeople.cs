using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// A task can be assigned to several people: the CardPeople join table, in the order they were
// picked. The first is the lead, and Cards.WhoId is always kept equal to it - so anything that
// only needs "the person" (sorting, the older single-person code paths, an older copy of the app)
// still reads one from the card's own row.
public partial class DatabaseService
{
    private static void EnsureCardPeopleTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS CardPeople (
                CardId INTEGER NOT NULL,
                PersonId INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL,
                PRIMARY KEY (CardId, PersonId)
            );
            CREATE INDEX IF NOT EXISTS IX_CardPeople_Person ON CardPeople (PersonId);

            -- Tasks from before there was a list: their one person becomes the lead. Safe on every
            -- startup, since it only touches a task that has a person and no list at all.
            INSERT INTO CardPeople (CardId, PersonId, SortOrder)
            SELECT c.Id, c.WhoId, 0 FROM Cards c
            WHERE c.WhoId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CardPeople cp WHERE cp.CardId = c.Id);
            """;
        cmd.ExecuteNonQuery();
    }

    // Replaces the task's people, lead first, and points the card's WhoId at the lead.
    public void SetCardPeople(int cardId, IEnumerable<int> personIds)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        WriteCardPeople(connection, transaction, cardId, personIds);
        transaction.Commit();
    }

    private static void WriteCardPeople(SqliteConnection connection, SqliteTransaction? transaction, int cardId, IEnumerable<int> personIds)
    {
        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = transaction;
            clearCmd.CommandText = "DELETE FROM CardPeople WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = "INSERT INTO CardPeople (CardId, PersonId, SortOrder) VALUES ($cardId, $personId, $sortOrder);";
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            var personParam = insertCmd.CreateParameter();
            personParam.ParameterName = "$personId";
            insertCmd.Parameters.Add(personParam);
            var orderParam = insertCmd.CreateParameter();
            orderParam.ParameterName = "$sortOrder";
            insertCmd.Parameters.Add(orderParam);

            var order = 0;
            foreach (var personId in personIds.Distinct())
            {
                personParam.Value = personId;
                orderParam.Value = order++;
                insertCmd.ExecuteNonQuery();
            }
        }

        SyncLead(connection, transaction, cardId);
    }

    private static void SyncLead(SqliteConnection connection, SqliteTransaction? transaction, int cardId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            UPDATE Cards SET WhoId = (SELECT PersonId FROM CardPeople WHERE CardId = $cardId ORDER BY SortOrder LIMIT 1)
            WHERE Id = $cardId;
            """;
        cmd.Parameters.AddWithValue("$cardId", cardId);
        cmd.ExecuteNonQuery();
    }

    // Fills CardItem.PeopleIds for the cards just read. A task whose row names a person the list
    // doesn't (an older copy of the app changed it) is read as that one person.
    private static void LoadCardPeople(SqliteConnection connection, List<Models.CardItem> cards)
    {
        var byCard = cards.ToDictionary(c => c.Id);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT CardId, PersonId FROM CardPeople ORDER BY CardId, SortOrder;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (byCard.TryGetValue(reader.GetInt32(0), out var card)) card.PeopleIds.Add(reader.GetInt32(1));
            }
        }

        foreach (var card in cards)
        {
            var lead = card.PeopleIds.Count == 0 ? (int?)null : card.PeopleIds[0];
            if (card.WhoId == lead) continue;
            card.PeopleIds = card.WhoId is { } who ? [who] : [];
        }
    }
}
