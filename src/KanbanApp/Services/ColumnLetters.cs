namespace KanbanApp.Services;

// The one-letter labels on each card's move buttons, worked out from what the columns are called
// now - the user can rename them. The standard names give T, P, H and W, as they always have.
//
// Each column takes the first letter of its first word that isn't a small joining word ("In
// Progress" is P, "On Hold" is H). If an earlier column already has that letter it tries the other
// words, then the rest of the letters in the name, and as a last resort its position on the board.
// X is never given out: it is the Delete button.
public static class ColumnLetters
{
    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase) { "in", "on", "at", "the", "a", "an", "of", "for" };

    public const string Reserved = "X";

    // One letter per name, in the same order, all different.
    public static IReadOnlyList<string> Assign(IReadOnlyList<string> names)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Reserved };
        var letters = new List<string>();

        for (var i = 0; i < names.Count; i++)
        {
            var letter = Candidates(names[i]).FirstOrDefault(c => !taken.Contains(c)) ?? (i + 1).ToString();
            taken.Add(letter);
            letters.Add(letter);
        }

        return letters;
    }

    private static IEnumerable<string> Candidates(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .ToList();

        foreach (var word in words.Where(w => !SmallWords.Contains(w))) yield return Upper(word[0]);
        foreach (var word in words.Where(w => SmallWords.Contains(w))) yield return Upper(word[0]);
        foreach (var word in words)
        {
            foreach (var c in word.Skip(1)) yield return Upper(c);
        }
    }

    private static string Upper(char c) => char.ToUpperInvariant(c).ToString();
}
