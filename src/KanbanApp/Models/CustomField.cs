using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KanbanApp.Models;

public enum CustomFieldType { Text, Number, Date, YesNo }

// One of the (up to ten) fields the user defines for a task file: a name, what kind of value it holds and whether it is
// switched on. A task keeps its value for the field under the field's slot number (1 to 10), so renaming a field, or
// switching it off, never loses what tasks hold. A field with no name is a free slot.
public sealed class CustomFieldDef
{
    public int Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomFieldType Type { get; set; } = CustomFieldType.Text;
    // Offered on tasks. A field not offered is hidden everywhere, but what tasks hold stays and comes back when it is offered again.
    public bool IsActive { get; set; }

    // For a repeating task: the next occurrence keeps this field's value (true), or has the field but empty (false).
    public bool KeepOnRepeat { get; set; } = true;

    // Named and switched on: the fields the New Task screen, reports and Excel files use.
    [JsonIgnore]
    public bool IsUsable => IsActive && !string.IsNullOrWhiteSpace(Name);

    [JsonIgnore]
    public bool IsDefined => !string.IsNullOrWhiteSpace(Name);
}

// The rules for custom fields, with no screen or file in them: reading and writing the definitions and the values a task
// holds, and what a value must look like for each type.
public static class CustomFieldRules
{
    public const int MaxFields = 10;
    public const int MaxNameLength = 40;
    public const int MaxTextLength = 255;

    public static string TypeLabel(CustomFieldType type) => type switch
    {
        CustomFieldType.Number => "Number",
        CustomFieldType.Date => "Date",
        CustomFieldType.YesNo => "Yes / No",
        _ => "Text"
    };

    // Always MaxFields entries, slot 1 to 10, whatever the stored text holds (nothing, damaged, or fewer).
    public static List<CustomFieldDef> LoadDefinitions(string? json)
    {
        var loaded = new List<CustomFieldDef>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { loaded = JsonSerializer.Deserialize<List<CustomFieldDef>>(json) ?? []; }
            catch (JsonException) { /* a damaged setting reads as no fields */ }
        }

        var result = new List<CustomFieldDef>();
        for (var slot = 1; slot <= MaxFields; slot++)
        {
            var found = loaded.FirstOrDefault(f => f.Slot == slot);
            result.Add(new CustomFieldDef
            {
                Slot = slot,
                Name = found?.Name?.Trim() ?? string.Empty,
                Type = found is null ? CustomFieldType.Text : found.Type,
                IsActive = found?.IsActive ?? false,
                KeepOnRepeat = found?.KeepOnRepeat ?? true
            });
        }
        return result;
    }

    public static string SaveDefinitions(IEnumerable<CustomFieldDef> definitions) =>
        JsonSerializer.Serialize(definitions.Where(d => d.IsDefined).ToList());

    // The fields that apply to a task, by slot, with what it holds in each, as stored in Cards.CustomValues. A field applies to a task
    // when it has an entry, even an empty one; anything unreadable is left out.
    public static Dictionary<int, string> ParseValues(string? json)
    {
        var result = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw is null) return result;
            foreach (var (key, value) in raw)
            {
                if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var slot) && slot is >= 1 and <= MaxFields)
                {
                    result[slot] = value?.Trim() ?? string.Empty;
                }
            }
        }
        catch (JsonException) { /* damaged: no values */ }
        return result;
    }

    // Null for a task with no fields, so a file that uses none stores nothing at all.
    public static string? SerializeValues(IReadOnlyDictionary<int, string>? values)
    {
        if (values is null) return null;
        var clean = values
            .Where(p => p.Key is >= 1 and <= MaxFields)
            .OrderBy(p => p.Key)
            .ToDictionary(p => p.Key.ToString(CultureInfo.InvariantCulture), p => p.Value?.Trim() ?? string.Empty);
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }

    // What to store for typed or imported text: false when it is not a value of the type (a number that is not one, a date
    // that is not a date). Blank is always fine and stores nothing. Numbers are stored with a full stop for the decimal point
    // and dates as yyyy-MM-dd, so a file reads the same on every PC.
    public static bool TryNormalise(CustomFieldType type, string? input, out string? stored)
    {
        stored = null;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0) return true;

        switch (type)
        {
            case CustomFieldType.Number:
                if (!TryParseNumber(text, out var number)) return false;
                stored = number.ToString("0.############", CultureInfo.InvariantCulture);
                return true;
            case CustomFieldType.Date:
                if (!TryParseDay(text, out var day)) return false;
                stored = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            case CustomFieldType.YesNo:
                stored = text.ToLowerInvariant() is "yes" or "y" or "true" or "1" or "x" ? "Yes" : null;
                return true;
            default:
                stored = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
                return true;
        }
    }

    // No thousands separators: "1,5" is one and a half in some languages and fifteen hundred in others.
    private static bool TryParseNumber(string text, out decimal number)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        return decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out number) || decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryParseDay(string text, out DateTime day) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
        || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out day);

    // What a repeating task's next occurrence starts with: the same fields applied, each holding its value only when the field is kept on repeat.
    public static Dictionary<int, string> ForNextOccurrence(IReadOnlyDictionary<int, string> values, IReadOnlyList<CustomFieldDef> definitions) =>
        values.ToDictionary(p => p.Key, p => (definitions.FirstOrDefault(f => f.Slot == p.Key)?.KeepOnRepeat ?? true) ? p.Value : string.Empty);

    // How a stored value reads on screen, in a report or in a history line.
    public static string Display(CustomFieldType type, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return string.Empty;
        switch (type)
        {
            case CustomFieldType.Number when decimal.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                return number.ToString("0.############", CultureInfo.CurrentCulture);
            case CustomFieldType.Date when DateTime.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                return day.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
            default:
                return stored;
        }
    }
}
