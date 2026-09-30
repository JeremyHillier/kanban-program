using System.Text.Json;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// The task file's priorities, highest first: the order every menu, list and sort uses. A task
// stores its priority as the name, so a rename has to reword the tasks too - MainViewModel does
// that (MainViewModel.Priorities.cs); this class only keeps the list itself in order.
//
// One entry is the default: what a new task gets, and what a name that isn't on the list is
// treated as when sorting (a task file edited by an older copy of the app, or an import).
public sealed class PriorityList
{
    public const string SettingKey = "PriorityList";

    private static readonly PriorityLevel[] StandardLevels =
        [new("High", "Red"), new("Medium", "Amber"), new("Normal", "Grey"), new("Low", "Blue")];
    private const string StandardDefault = "Normal";

    // What a card falls back on when it was built without a board behind it. Never changed.
    internal static readonly PriorityList Fallback = new();

    private readonly List<PriorityLevel> _levels = [.. StandardLevels];
    private readonly Dictionary<string, Brush> _brushes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PriorityLevel> Levels => _levels;
    public IReadOnlyList<string> Names => _levels.Select(l => l.Name).ToList();
    public string Default { get; private set; } = StandardDefault;

    public bool IsStandard => Default == StandardDefault && _levels.SequenceEqual(StandardLevels);

    // The name as the list spells it, whatever capitals were typed; null when it isn't on the list.
    public string? Find(string? text) =>
        _levels.FirstOrDefault(l => string.Equals(l.Name, text?.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;

    // For anything coming in from outside (an import, a template): a name on the list, else the default.
    public string Resolve(string? text) => Find(text) ?? Default;

    // Highest first. A name that isn't on the list sorts with the default.
    public int Rank(string? name)
    {
        var index = IndexOf(name);
        return index >= 0 ? index : Math.Max(0, IndexOf(Default));
    }

    public string ColorKey(string? name)
    {
        var index = IndexOf(name);
        return index >= 0 ? PriorityColors.Get(_levels[index].Color).Key : PriorityColors.Fallback;
    }

    // The badge colour, as on the board, the Reminders list and the Timeline.
    public Brush Brush(string? name) => BrushForColor(ColorKey(name));

    public Brush BrushForColor(string colorKey)
    {
        var entry = PriorityColors.Get(colorKey);
        if (_brushes.TryGetValue(entry.Key, out var brush)) return brush;

        brush = new SolidColorBrush(Color.FromRgb((byte)(entry.Badge >> 16), (byte)(entry.Badge >> 8), (byte)entry.Badge));
        brush.Freeze();
        return _brushes[entry.Key] = brush;
    }

    private int IndexOf(string? name) =>
        _levels.FindIndex(l => string.Equals(l.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    // ----- Changes. Each says whether it did anything. -----

    // False for an empty name or one already on the list. A new priority goes at the bottom, in
    // the first colour nothing else is using.
    public bool Add(string name, string? color = null)
    {
        name = name.Trim();
        if (name.Length == 0 || Find(name) is not null) return false;

        color ??= PriorityColors.All.Select(c => c.Key).FirstOrDefault(key => _levels.All(l => l.Color != key)) ?? PriorityColors.Fallback;
        _levels.Add(new PriorityLevel(name, PriorityColors.Get(color).Key));
        return true;
    }

    // False when the new name is empty or belongs to another entry. Changing only the capitals is fine.
    public bool Rename(string name, string newName)
    {
        newName = newName.Trim();
        var index = IndexOf(name);
        if (index < 0 || newName.Length == 0) return false;

        var other = IndexOf(newName);
        if (other >= 0 && other != index) return false;
        if (_levels[index].Name == newName) return false;

        if (string.Equals(Default, _levels[index].Name, StringComparison.OrdinalIgnoreCase)) Default = newName;
        _levels[index] = _levels[index] with { Name = newName };
        return true;
    }

    // The default can't go (something has to be what a new task gets), and nor can the last one.
    public bool CanRemove(string name) =>
        IndexOf(name) >= 0 && _levels.Count > 1 && !string.Equals(Default, name.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool Remove(string name)
    {
        if (!CanRemove(name)) return false;
        _levels.RemoveAt(IndexOf(name));
        return true;
    }

    // places < 0 moves it up the list (higher priority), > 0 down.
    public bool Move(string name, int places)
    {
        var index = IndexOf(name);
        if (index < 0) return false;

        var target = Math.Clamp(index + places, 0, _levels.Count - 1);
        if (target == index) return false;

        var level = _levels[index];
        _levels.RemoveAt(index);
        _levels.Insert(target, level);
        return true;
    }

    public bool SetColor(string name, string color)
    {
        var index = IndexOf(name);
        var key = PriorityColors.Get(color).Key;
        if (index < 0 || _levels[index].Color == key) return false;

        _levels[index] = _levels[index] with { Color = key };
        return true;
    }

    public bool SetDefault(string name)
    {
        var found = Find(name);
        if (found is null || found == Default) return false;

        Default = found;
        return true;
    }

    // ----- Storage: one JSON value in the Settings table. -----

    private sealed record Stored(string? Default, List<PriorityLevel>? Levels);

    public string ToJson() => JsonSerializer.Serialize(new Stored(Default, _levels));

    // Anything missing or unreadable gives the standard four, so a damaged setting can never
    // leave a task file without priorities.
    public static PriorityList FromJson(string? json)
    {
        var list = new PriorityList();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(json);
            var levels = (stored?.Levels ?? [])
                .Where(l => !string.IsNullOrWhiteSpace(l?.Name))
                .Select(l => new PriorityLevel(l.Name.Trim(), PriorityColors.Get(l.Color).Key))
                .DistinctBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (levels.Count == 0) return list;

            list._levels.Clear();
            list._levels.AddRange(levels);
            list.Default = list.Find(stored?.Default) ?? levels[0].Name;
        }
        catch (JsonException)
        {
            // Keep the standard list.
        }

        return list;
    }
}
