namespace KanbanApp.Models;

// The colours a priority can have. A fixed set rather than a free colour picker: each has a badge
// colour (the board, Reminders, the Timeline - white text sits on it) and its own Dashboard colours
// for light and dark.
//
// The first four are the colours High, Medium, Normal and Low have always had. The other four were
// chosen by search to sit as far as possible from those and from each other - measured as colour
// difference (CIELAB) under normal vision and simulated protan, deutan and tritan colour blindness
// - while staying in the app's muted range, readable under white text (badges) and clear of the
// chart card (Dashboard). The closest pair in each set: badges 14.7, light charts 16.6, dark
// charts 19.2. Before changing a value or adding a colour, measure it the same way: two colours
// that look different to most people can be the same colour to some (the first teal and pink tried
// here came out at 2.9).
public static class PriorityColors
{
    public sealed record Entry(string Key, uint Badge, uint ChartLight, uint ChartDark);

    public const string Fallback = "Grey";

    public static readonly IReadOnlyList<Entry> All =
    [
        new("Red", 0xD9534F, 0xd03b3b, 0xd03b3b),
        new("Amber", 0xE09A3E, 0xeda100, 0xc98500),
        new("Grey", 0x9E9E9E, 0x8a8984, 0x6f6e69),
        new("Blue", 0x5C8AAE, 0x2a78d6, 0x3987e5),
        new("Green", 0x29A366, 0x269755, 0xabe085),
        new("Purple", 0x8932C8, 0x451bc0, 0xc490d5),
        new("Pink", 0xAF2C84, 0x7e3071, 0xea6ca0),
        new("Teal", 0x37675B, 0x1facdb, 0x6ceaea),
    ];

    public static Entry Get(string? key) =>
        All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? All.First(e => e.Key == Fallback);
}
