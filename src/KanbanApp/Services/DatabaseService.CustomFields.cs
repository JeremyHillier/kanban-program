using KanbanApp.Models;

namespace KanbanApp.Services;

// What the task file holds for custom fields (Models/CustomField): the values are in Cards.CustomValues, the definitions
// are the CustomFields setting.
public partial class DatabaseService
{
    // How many tasks hold a value in the field (one the field merely applies to, with nothing in it, does not count): on the board,
    // archived or deleted.
    public int CountTasksWithCustomValue(int slot)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CustomValues FROM Cards WHERE CustomValues IS NOT NULL;";
        var count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (CustomFieldRules.ParseValues(reader.GetString(0)).TryGetValue(slot, out var value) && value.Length > 0) count++;
        }
        return count;
    }

    // The names of the fields that have one, by slot: how a history line says which field changed.
    internal Dictionary<int, string> GetCustomFieldNames() =>
        CustomFieldRules.LoadDefinitions(GetSetting("CustomFields")).Where(f => f.IsDefined).ToDictionary(f => f.Slot, f => f.Name);
}
