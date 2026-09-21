namespace KanbanApp.Views;

// Which entry a few typed letters mean, for lists that aren't a stock ListBox or ComboBox (the
// task screen's Who tickboxes). The start of the whole name wins; failing that the start of any
// later word, so a surname works too. Capitals don't matter. -1 when nothing fits.
internal static class TypeAheadMatcher
{
    // How long a pause starts a new search instead of adding to the letters typed so far.
    public static readonly TimeSpan ResetAfter = TimeSpan.FromSeconds(1);

    public static int Match(IReadOnlyList<string> names, string typed)
    {
        if (string.IsNullOrEmpty(typed)) return -1;

        for (var i = 0; i < names.Count; i++)
        {
            if (names[i].StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)) return i;
        }

        for (var i = 0; i < names.Count; i++)
        {
            var words = names[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var w = 1; w < words.Length; w++)
            {
                if (string.Join(' ', words[w..]).StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)) return i;
            }
        }

        return -1;
    }
}
