namespace KanbanApp.Services;

// The Settings key/value table backing MainViewModel's app-level settings (theme, layout, paths,
// confirmation toggles, remember-last-view).
public partial class DatabaseService
{
    public string? GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = $value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    // On/off settings are stored as "True" / "False". Reading one depends on its default, exactly as
    // the app always has: a setting that starts on stays on unless it says "False"; one that starts
    // off is on only when it says "True". Anything else stored there falls back the same way.
    public bool GetFlag(string key, bool defaultValue) =>
        defaultValue ? GetSetting(key) != "False" : GetSetting(key) == "True";

    public void SetFlag(string key, bool value) => SetSetting(key, value ? "True" : "False");

    // A whole-number setting, or the default when it's missing or not a number.
    public int GetInt(string key, int defaultValue) =>
        int.TryParse(GetSetting(key), out var value) ? value : defaultValue;
}
