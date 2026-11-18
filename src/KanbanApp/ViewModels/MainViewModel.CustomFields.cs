using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The task file's custom fields (Models/CustomField): up to ten the user names, types and switches on in Settings. The
// definitions are a setting of the task file (so templates and copies of the file carry them); a task's values are in
// Cards.CustomValues by slot number. Like the other Manage screens, changes are made at once and sit outside Undo and
// Settings' Cancel. A field switched off is only hidden: what tasks hold stays and comes back when it is switched on.
public partial class MainViewModel
{
    private const string CustomFieldsKey = "CustomFields";

    private List<CustomFieldDef> _customFields = CustomFieldRules.LoadDefinitions(null);

    // Always ten entries, slot 1 to 10; one with no name is free.
    public IReadOnlyList<CustomFieldDef> CustomFieldDefs => _customFields;

    // The fields that are named and switched on, in slot order: what the New Task screen, reports and Excel files use.
    public IReadOnlyList<CustomFieldDef> ActiveCustomFields => _customFields.Where(f => f.IsUsable).ToList();

    private void LoadCustomFields() => _customFields = CustomFieldRules.LoadDefinitions(_db.GetSetting(CustomFieldsKey));

    // How many tasks hold a value in the field, archived and deleted ones too.
    public int CustomFieldUseCount(int slot) => _db.CountTasksWithCustomValue(slot);

    // Names, types and switches a field. Returns what is wrong, or null when it was saved. A field's type is fixed once any
    // task holds a value in it (the values would no longer fit), and a field needs a name to be switched on.
    public string? SetCustomField(int slot, string name, CustomFieldType type, bool isActive)
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
        if (isActive && name.Length == 0) return "Give the field a name before switching it on.";
        if (type != field.Type && CustomFieldUseCount(slot) > 0) return "Tasks already have a value in this field, so its type can't change. Use another field instead.";
        if (field.Name == name && field.Type == type && field.IsActive == isActive) return null;

        field.Name = name;
        field.Type = type;
        field.IsActive = isActive;
        _db.SetSetting(CustomFieldsKey, CustomFieldRules.SaveDefinitions(_customFields));
        if (_customFields.Any(f => f.IsDefined)) _db.RaiseFileFormat(DatabaseService.CustomFieldsFileFormat); // older copies would not know the values
        OnPropertyChanged(nameof(ActiveCustomFields));
        return null;
    }

    // The task's value in the field as it reads on screen; empty when it has none.
    public static string CustomValueText(CardViewModel card, CustomFieldDef field) =>
        CustomFieldRules.Display(field.Type, card.CustomValues.GetValueOrDefault(field.Slot));
}
