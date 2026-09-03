using System.Text.Json;
using System.Text.Json.Serialization;

namespace KanbanApp.Models;

// One Alt+0..Alt+9 filter slot: a named snapshot of every board filter, stored as JSON in the
// settings table. An empty Name means the slot is unused.
public class CustomFilter
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(StringOrStringListConverter))]
    public List<string> Project { get; set; } = [];
    [JsonConverter(typeof(StringOrStringListConverter))]
    public List<string> Priority { get; set; } = [];
    [JsonConverter(typeof(StringOrStringListConverter))]
    public List<string> Who { get; set; } = [];

    public string Goal { get; set; } = "All";
    public string Flag { get; set; } = "All";
    public string Due { get; set; } = "All";

    // Stored as yyyy-MM-dd strings rather than DateTime so the persisted JSON stays culture-proof.
    public string? DueFrom { get; set; }
    public string? DueTo { get; set; }

    public string Keyword { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsDefined => !string.IsNullOrWhiteSpace(Name);

    // Plain-English description of what the slot actually narrows by, for the manage window.
    [JsonIgnore]
    public string Summary
    {
        get
        {
            if (!IsDefined) return "Empty - not assigned";

            var parts = new List<string>();
            if (Project.Count > 0) parts.Add($"Project: {string.Join(", ", Project)}");
            if (Priority.Count > 0) parts.Add($"Priority: {string.Join(", ", Priority)}");
            if (Who.Count > 0) parts.Add($"Who: {string.Join(", ", Who)}");
            if (Goal != "All") parts.Add($"Goal: {Goal}");
            if (Flag != "All") parts.Add($"Flag: {Flag}");
            if (Due != "All") parts.Add($"Due: {Due}");
            if (!string.IsNullOrEmpty(DueFrom)) parts.Add($"From: {DueFrom}");
            if (!string.IsNullOrEmpty(DueTo)) parts.Add($"To: {DueTo}");
            if (!string.IsNullOrWhiteSpace(Keyword)) parts.Add($"Keyword: \"{Keyword}\"");

            return parts.Count == 0 ? "Everything (no filters applied)" : string.Join("   •   ", parts);
        }
    }
}

// Project/Priority/Who moved from a single string ("All", "Unassigned", or one name) to a
// multi-select list. Reads either shape so slots saved before that change keep working: a bare
// JSON string becomes a single-item list ("All" becomes empty, meaning no restriction - matching
// what "All" always meant), while a JSON array (the current form) is read as-is.
public class StringOrStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrEmpty(value) || value == "All" ? [] : [value];
        }

        var list = new List<string>();
        if (reader.TokenType != JsonTokenType.StartArray) return list;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var item = reader.GetString();
            if (!string.IsNullOrEmpty(item) && item != "All") list.Add(item);
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
