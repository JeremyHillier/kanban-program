using System.Text.Json;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The task file's custom fields (Models/CustomField): up to ten the user names, types and offers in Settings. The definitions are a
// setting of the task file (so templates and copies of the file carry them). Each task chooses which of the offered fields apply to it:
// a field applies to a task when the task has an entry for it in Cards.CustomValues, with or without a value, and a new task starts with
// none. A project can list fields that new tasks in it start with. Like the other Manage screens, changes to the definitions are made at
// once and sit outside Undo and Settings' Cancel. A field not offered (Active off) is only hidden: what tasks hold stays and comes back.
public partial class MainViewModel
{
    private const string CustomFieldsKey = "CustomFields";
    private const string ProjectDefaultFieldsKey = "ProjectDefaultFields";

    private List<CustomFieldDef> _customFields = CustomFieldRules.LoadDefinitions(null);

    // Project id -> the fields (slots) a new task in that project starts with.
    private Dictionary<int, List<int>> _projectDefaultFields = [];

    // Always ten entries, slot 1 to 10; one with no name is free.
    public IReadOnlyList<CustomFieldDef> CustomFieldDefs => _customFields;

    // The fields that are named and offered (Active), in slot order: what a task can have applied, and what reports and Excel files use.
    public IReadOnlyList<CustomFieldDef> ActiveCustomFields => _customFields.Where(f => f.IsUsable).ToList();

    private void LoadCustomFields()
    {
        _customFields = CustomFieldRules.LoadDefinitions(_db.GetSetting(CustomFieldsKey));

        _projectDefaultFields = [];
        if (_db.GetSetting(ProjectDefaultFieldsKey) is { Length: > 0 } json)
        {
            try { _projectDefaultFields = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(json) ?? []; }
            catch (JsonException) { /* damaged: no defaults */ }
        }
    }

    // How many tasks hold a value in the field, archived and deleted ones too.
    public int CustomFieldUseCount(int slot) => _db.CountTasksWithCustomValue(slot);

    // Names, types and switches a field. Returns what is wrong, or null when it was saved. A field's type is fixed once any task holds a
    // value in it (the values would no longer fit), and a field needs a name to be offered. keepOnRepeat is whether a repeating task's
    // next occurrence keeps the value (false: the field stays on it, empty).
    public string? SetCustomField(int slot, string name, CustomFieldType type, bool isActive, bool keepOnRepeat = true)
    {
        var field = _customFields.FirstOrDefault(f => f.Slot == slot);
        if (field is null) return "There is no such field.";

        name = (name ?? string.Empty).Trim();
        if (name.Length > CustomFieldRules.MaxNameLength) return $"A field's name can be up to {CustomFieldRules.MaxNameLength} characters.";
        if (name.Length > 0)
        {
            if (_customFields.Any(f => f.Slot != slot && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) return $"There is already a field called \"{name}\".";
            if (ImportService.IsBuiltInHeading(name)) return $"\"{name}\" is already a heading in the task's Excel files. Choose another name.";
        }
        if (isActive && name.Length == 0) return "Give the field a name before offering it.";
        if (type != field.Type && CustomFieldUseCount(slot) > 0) return "Tasks already have a value in this field, so its type can't change. Use another field instead.";
        if (field.Name == name && field.Type == type && field.IsActive == isActive && field.KeepOnRepeat == keepOnRepeat) return null;

        field.Name = name;
        field.Type = type;
        field.IsActive = isActive;
        field.KeepOnRepeat = keepOnRepeat;
        _db.SetSetting(CustomFieldsKey, CustomFieldRules.SaveDefinitions(_customFields));
        if (_customFields.Any(f => f.IsDefined)) _db.RaiseFileFormat(DatabaseService.CustomFieldsFileFormat); // older copies would not know the values
        OnPropertyChanged(nameof(ActiveCustomFields));
        return null;
    }

    // The task's value in the field as it reads on screen; empty when it has none (or the field is not applied to it).
    public static string CustomValueText(CardViewModel card, CustomFieldDef field) =>
        CustomFieldRules.Display(field.Type, card.CustomValues.GetValueOrDefault(field.Slot));

    // The fields chosen for a project, as slots (those no longer offered are still listed here, and come back if offered again).
    public IReadOnlyList<int> DefaultFieldSlots(int projectId) => _projectDefaultFields.GetValueOrDefault(projectId) ?? [];

    // The offered fields a new task in the project starts with.
    public IReadOnlyList<CustomFieldDef> DefaultFieldsOf(int? projectId) =>
        projectId is { } id && _projectDefaultFields.TryGetValue(id, out var slots) ? ActiveCustomFields.Where(f => slots.Contains(f.Slot)).ToList() : [];

    public void SetDefaultFields(int projectId, IEnumerable<int> slots)
    {
        var chosen = slots.Distinct().Order().ToList();
        if (chosen.Count == 0) _projectDefaultFields.Remove(projectId);
        else _projectDefaultFields[projectId] = chosen;

        _db.SetSetting(ProjectDefaultFieldsKey, JsonSerializer.Serialize(_projectDefaultFields));
        if (chosen.Count > 0) _db.RaiseFileFormat(DatabaseService.CustomFieldsFileFormat);
    }

    // What the next occurrence of a repeating task starts with: the same fields applied, each with its value only if the field is kept.
    private Dictionary<int, string> NextOccurrenceCustomValues(CardViewModel completed) =>
        CustomFieldRules.ForNextOccurrence(completed.CustomValues, _customFields);

    // Applies a field to each task, or takes it off. Adding keeps what a task already holds; given a value (already in its stored form),
    // it sets that. Part of a Mass Update, so one Undo step.
    public void ApplyCustomFieldToCards(IEnumerable<CardViewModel> cards, CustomFieldDef field, bool remove, string? value) =>
        ChangeCards(remove ? "Take a custom field off" : "Add a custom field to", cards,
            c => remove
                ? c.CustomValues.ContainsKey(field.Slot)
                : !c.CustomValues.ContainsKey(field.Slot) || (value is not null && c.CustomValues[field.Slot] != value),
            c =>
            {
                var values = new Dictionary<int, string>(c.CustomValues);
                if (remove) values.Remove(field.Slot);
                else values[field.Slot] = value ?? values.GetValueOrDefault(field.Slot) ?? string.Empty;
                c.CustomValues = values;
            });
}
