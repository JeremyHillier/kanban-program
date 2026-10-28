namespace KanbanApp.Models;

// The background colours a task card can be given (right-click, Colour). A fixed set, each with a light-theme
// and a dark-theme shade, so the card's text stays readable in both; stored on the task by key ("Yellow").
// ThemeManager registers them as the "CardColor.<Key>" brushes the card template binds to.
public static class CardColors
{
    public sealed record Entry(string Key, uint Light, uint Dark);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("Yellow", 0xFFF4C2, 0x5A4E1A),
        new("Orange", 0xFFE0B8, 0x5C3E1C),
        new("Pink", 0xF9D6E2, 0x5A2E42),
        new("Purple", 0xE6DAF5, 0x3F2F5A),
        new("Blue", 0xD6E8F8, 0x24405A),
        new("Teal", 0xCDEDE8, 0x1F4A48),
        new("Green", 0xDCF0CB, 0x2F4A2B),
        new("Grey", 0xE2E2E2, 0x3E3E42),
    ];

    public static Entry? Get(string? key) =>
        key is null ? null : All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string ResourceKey(string key) => $"CardColor.{key}";
}
