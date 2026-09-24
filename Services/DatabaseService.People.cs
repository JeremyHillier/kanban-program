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
    // list steps up (or nobody, if they were the only one).
    public void DeletePerson(int personId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

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
