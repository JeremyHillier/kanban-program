using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace KanbanApp.Services;

// The user's own words for spell check (names, terms, abbreviations). WPF has "Ignore All" but only for
// the one box, and forgets it when the task is opened again, and it has no "Add to Dictionary". So the
// words live in the task file (Settings, shared by every PC using it) and are written to one fixed
// file on this PC, which every spell-checked box in the app is given.
//
// WPF reads a custom dictionary when a box starts spell-checking, and what it has learned stays for the
// session. So the file always keeps the same name - re-registering the same name makes WPF read it
// again - and when the words change the open boxes are given the list again and spell check is
// switched off and on. A word taken out stops being accepted from then on.
public sealed class UserDictionary
{
    private readonly string _folder;
    private readonly List<(WeakReference<TextBoxBase> Box, Uri[] Others)> _boxes = [];
    private List<string> _words = [];

    // Where the words are kept on this PC; tests use a folder of their own.
    public UserDictionary(string? folder = null) => _folder = folder ?? DefaultFolder;

    public static string DefaultFolder => FolderOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppChannel.DataFolderName);

    // The tests point this at a folder of their own, so they never write where the real app does.
    internal static string? FolderOverride { get; set; }

    public string FilePath => Path.Combine(_folder, "UserDictionary.lex");

    // The words of every task file used on this PC, so a word added in one file is known in the others.
    public string PcWordsPath => Path.Combine(_folder, "UserDictionary.words");

    public string ReadPcWords()
    {
        try { return File.Exists(PcWordsPath) ? File.ReadAllText(PcWordsPath) : string.Empty; }
        catch { return string.Empty; }
    }

    public IReadOnlyList<string> Words => _words;

    // The words as they are shown and saved: one per line.
    public string AsText => string.Join("\n", _words);

    // Told when the user adds a word from the right-click menu, so it can be saved with the task file.
    public event Action? WordsAdded;

    // --- The words ---------------------------------------------------------------------------------

    // What may be a dictionary word: letters, digits, apostrophes and hyphens, nothing longer than 60. Anything
    // typed with spaces, commas or semicolons between is several words. Case-insensitive duplicates keep the
    // first spelling; the list is sorted so it reads like a dictionary.
    public static List<string> ParseWords(string? text)
    {
        var separators = new[] { '\r', '\n', '\t', ' ', ',', ';' };
        return (text ?? string.Empty).Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.Trim('\'', '-', '’'))
            .Where(w => w.Length is >= 1 and <= 60 && w.All(c => char.IsLetterOrDigit(c) || c is '\'' or '-' or '’'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Replaces the whole list (Settings, or the task file being opened) and gives it to every open box.
    public void Set(string? text)
    {
        var words = ParseWords(text);
        if (words.SequenceEqual(_words, StringComparer.Ordinal) && File.Exists(FilePath)) return;

        _words = words;
        Write();
        RefreshBoxes();
    }

    public bool Contains(string word) => _words.Contains(word, StringComparer.OrdinalIgnoreCase);

    // From the right-click menu. False for something that cannot be a word or is there already.
    public bool Add(string word)
    {
        var parsed = ParseWords(word);
        if (parsed.Count != 1 || Contains(parsed[0])) return false;

        _words = ParseWords(AsText + "\n" + parsed[0]);
        Write();
        RefreshBoxes();
        WordsAdded?.Invoke();
        return true;
    }

    // What goes in the file for the words: each as typed and in lower case. Windows takes a lower-case entry in
    // any ordinary capitals ("kanban" also accepts "Kanban"), but a word with a capital inside it ("PayWorks")
    // only matches an entry spelled that way, so both are written (tested in a real task window with fresh words).
    internal static IEnumerable<string> FileLines(IEnumerable<string> words) =>
        words.SelectMany(w => new[] { w, w.ToLowerInvariant() }).Distinct(StringComparer.Ordinal);

    // Plain text, one word a line, in the form WPF's spell checker reads (no language line, so it applies
    // to every language).
    private void Write()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath, string.Join("\r\n", FileLines(_words)) + "\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(PcWordsPath, AsText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // A folder that cannot be written: spell check carries on without the user's words.
        }
    }

    // --- The boxes ---------------------------------------------------------------------------------

    // Gives a spell-checked box the user's words (others: any dictionaries it has of its own, such as the
    // American spellings), keeps it up to date when they change, and gives it the right-click menu with
    // Add to Dictionary.
    public void Attach(TextBox box, params Uri[] others)
    {
        _boxes.RemoveAll(entry => !entry.Box.TryGetTarget(out _));
        _boxes.Add((new WeakReference<TextBoxBase>(box), others));
        Apply(box, others);
        var menu = new ContextMenu();
        box.ContextMenu = menu;
        box.ContextMenuOpening += (_, e) => BuildMenu(box, menu, e);
    }

    private void Apply(TextBoxBase box, Uri[] others)
    {
        var dictionaries = SpellCheck.GetCustomDictionaries(box);
        dictionaries.Clear();
        foreach (var other in others) dictionaries.Add(other);
        if (_words.Count > 0 && File.Exists(FilePath)) dictionaries.Add(new Uri(FilePath));
    }

    // The same for the Notes box, a RichTextBox: its right-click menu finds the word through text pointers.
    public void AttachRich(RichTextBox box, params Uri[] others)
    {
        _boxes.RemoveAll(entry => !entry.Box.TryGetTarget(out _));
        _boxes.Add((new WeakReference<TextBoxBase>(box), others));
        Apply(box, others);
        var menu = new ContextMenu();
        box.ContextMenu = menu;
        box.ContextMenuOpening += (_, e) => BuildRichMenu(box, menu, e);
    }

    // WPF reads a dictionary when spell check starts, so each live box is given the list again and
    // spell check is switched off and on.
    private void RefreshBoxes()
    {
        foreach (var (reference, others) in _boxes.ToList())
        {
            if (!reference.TryGetTarget(out var box)) continue;

            var enabled = SpellCheck.GetIsEnabled(box);
            Apply(box, others);
            if (!enabled) continue;

            SpellCheck.SetIsEnabled(box, false);
            SpellCheck.SetIsEnabled(box, true);
        }
    }

    // --- The right-click menu ----------------------------------------------------------------------

    // The box is given a menu of its own - WPF's built-in one cannot be added to - filled in each time
    // it opens: on a misspelled word, the suggestions, Ignore All and Add to Dictionary, then the usual
    // editing commands.
    // The right-click menu of the rich Notes box: the same entries, with the misspelled word found from a text pointer.
    private void BuildRichMenu(RichTextBox box, ContextMenu menu, ContextMenuEventArgs e)
    {
        menu.Items.Clear();

        var pointer = e.CursorLeft >= 0 ? box.GetPositionFromPoint(new Point(e.CursorLeft, e.CursorTop), snapToText: false) : box.CaretPosition;
        if (pointer is not null && box.GetSpellingError(pointer) is { } error && box.GetSpellingErrorRange(pointer) is { } range)
        {
            var word = range.Text.Trim();
            var suggestions = error.Suggestions.Take(5).ToList();
            foreach (var suggestion in suggestions)
            {
                var item = new MenuItem { Header = suggestion.Replace("_", "__"), FontWeight = FontWeights.Bold };
                item.Click += (_, _) => error.Correct(suggestion);
                menu.Items.Add(item);
            }
            if (suggestions.Count == 0) menu.Items.Add(new MenuItem { Header = "(no spelling suggestions)", IsEnabled = false });

            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Ignore All", Command = EditingCommands.IgnoreSpellingError, CommandTarget = box });

            var add = new MenuItem
            {
                Header = $"Add \"{word.Replace("_", "__")}\" to Dictionary",
                ToolTip = "Always accept this word, in every task. The list is in Settings.",
                IsEnabled = ParseWords(word).Count == 1
            };
            add.Click += (_, _) => Add(word);
            menu.Items.Add(add);
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = box });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = box });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = box });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Select All", Command = ApplicationCommands.SelectAll, CommandTarget = box });
    }

    private void BuildMenu(TextBox box, ContextMenu menu, ContextMenuEventArgs e)
    {
        menu.Items.Clear();

        var index = e.CursorLeft >= 0 ? box.GetCharacterIndexFromPoint(new Point(e.CursorLeft, e.CursorTop), snapToText: false) : box.CaretIndex;
        if (index >= 0 && box.GetSpellingError(index) is { } error)
        {
            var start = box.GetSpellingErrorStart(index);
            var length = box.GetSpellingErrorLength(index);
            if (start >= 0 && length > 0 && start + length <= box.Text.Length)
            {
                var word = box.Text.Substring(start, length);

                var suggestions = error.Suggestions.Take(5).ToList();
                foreach (var suggestion in suggestions)
                {
                    var item = new MenuItem { Header = suggestion.Replace("_", "__"), FontWeight = FontWeights.Bold };
                    item.Click += (_, _) => error.Correct(suggestion);
                    menu.Items.Add(item);
                }
                if (suggestions.Count == 0) menu.Items.Add(new MenuItem { Header = "(no spelling suggestions)", IsEnabled = false });

                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem { Header = "Ignore All", Command = EditingCommands.IgnoreSpellingError, CommandTarget = box });

                var add = new MenuItem
                {
                    Header = $"Add \"{word.Replace("_", "__")}\" to Dictionary",
                    ToolTip = "Always accept this word, in every task. The list is in Settings.",
                    IsEnabled = ParseWords(word).Count == 1
                };
                add.Click += (_, _) => Add(word);
                menu.Items.Add(add);
                menu.Items.Add(new Separator());
            }
        }

        menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = box });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = box });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = box });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Select All", Command = ApplicationCommands.SelectAll, CommandTarget = box });
    }
}
