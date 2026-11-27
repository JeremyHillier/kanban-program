using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The People ("Who") managed list. AddPerson has an internal (SqliteConnection) overload so
// Schema.cs's legacy-Who backfill can create each person on the same connection as its migration.
public partial class DatabaseService
{
    public List<Person> GetPeople() =>
        GetListEntries<Person>("People", ", Email", (reader, person) => person.Email = reader.IsDBNull(4) ? null : reader.GetString(4));

    public Person AddPerson(string name) => AddListEntry<Person>("People", name);

    private static Person AddPerson(string name, SqliteConnection connection) => AddListEntry<Person>("People", name, connection);

    public void RenamePerson(int personId, string name) => RenameListEntry("People", personId, name);

    public void SetPersonEmail(int personId, string? email)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET Email = $email WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void SetPersonActive(int personId, bool isActive) => SetListEntryActive("People", personId, isActive);

    // Takes the person off every task. Where they were the lead, the next person on that task's
    // list steps up (or nobody, if they were the only one). With moveTasksTo, that person takes their
    // place on every task instead, archived and deleted ones too: in their spot on the list, so as lead
    // where they led. Where both were on a task, it keeps whichever of the two places came first.
    public void DeletePerson(int personId, int? moveTasksTo = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (moveTasksTo is { } newId && newId != personId)
        {
            using var moveCmd = connection.CreateCommand();
            moveCmd.Transaction = transaction;
            moveCmd.CommandText = """
                UPDATE CardPeople SET SortOrder = (SELECT o.SortOrder FROM CardPeople o WHERE o.CardId = CardPeople.CardId AND o.PersonId = $id)
                WHERE PersonId = $new AND SortOrder > (SELECT o.SortOrder FROM CardPeople o WHERE o.CardId = CardPeople.CardId AND o.PersonId = $id);
                DELETE FROM CardPeople WHERE PersonId = $id AND CardId IN (SELECT CardId FROM CardPeople WHERE PersonId = $new);
                UPDATE CardPeople SET PersonId = $new WHERE PersonId = $id;
                """;
            moveCmd.Parameters.AddWithValue("$id", personId);
            moveCmd.Parameters.AddWithValue("$new", newId);
            moveCmd.ExecuteNonQuery();
        }

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = transaction;
            clearCmd.CommandText = """
                DELETE FROM CardPeople WHERE PersonId = $id;
                UPDATE Cards SET WhoId = (SELECT PersonId FROM CardPeople WHERE CardId = Cards.Id ORDER BY SortOrder LIMIT 1)
                WHERE WhoId = $id;
                DELETE FROM People WHERE Id = $id;
                """;
            clearCmd.Parameters.AddWithValue("$id", personId);
            clearCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
