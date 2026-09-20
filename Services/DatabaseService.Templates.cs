using System.Text.Json;
using KanbanApp.Models;

namespace KanbanApp.Services;

// Task templates. The template's contents are stored as one JSON value rather than a column per
// field: nothing ever queries inside a template, and it means a new task field can be added to
// templates without a schema change. Id and Name live in their own columns and win over whatever
// the JSON says.
public partial class DatabaseService
{
    private void EnsureTaskTemplatesTable(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS TaskTemplates (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Json TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    public List<TaskTemplate> GetTaskTemplates()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Json FROM TaskTemplates ORDER BY Name COLLATE NOCASE;";

        var result = new List<TaskTemplate>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            TaskTemplate template;
            try
            {
                template = JsonSerializer.Deserialize<TaskTemplate>(reader.GetString(2)) ?? new TaskTemplate();
            }
            catch (JsonException)
            {
                template = new TaskTemplate(); // a damaged template still lists, so it can be deleted
            }

            template.Id = reader.GetInt32(0);
            template.Name = reader.GetString(1);
            result.Add(template);
        }

        return result;
    }

    // Inserts when template.Id is 0, otherwise replaces that template. Returns the id.
    public int SaveTaskTemplate(TaskTemplate template)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = template.Id == 0
            ? "INSERT INTO TaskTemplates (Name, Json) VALUES ($name, $json); SELECT last_insert_rowid();"
            : "UPDATE TaskTemplates SET Name = $name, Json = $json WHERE Id = $id; SELECT $id;";
        cmd.Parameters.AddWithValue("$name", template.Name);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(template));
        cmd.Parameters.AddWithValue("$id", template.Id);
        template.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return template.Id;
    }

    public void DeleteTaskTemplate(int templateId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM TaskTemplates WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", templateId);
        cmd.ExecuteNonQuery();
    }
}
