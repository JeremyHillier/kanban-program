using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;

namespace KanbanApp.Services;

// Making sure the user's words really are accepted on this PC. WPF applies the words by registering a copy of the
// file with Windows' spell-checking service, and on some PCs that quietly does not take effect (a word added on one
// PC is accepted, on another it stays underlined). So after the words change, and when a task file is opened, a
// spell-checked box is asked about one of them. If the spell checker is working but still flags it, the words are
// kept in Windows' own personal dictionary instead, which does not depend on WPF's registration, and the boxes are
// refreshed. Words this app put there are written to UserDictionary.windows so they can be taken out again.
public sealed partial class UserDictionary
{
    // Not a word in any language, so Windows must flag it if the spell checker is running at all.
    private const string ControlWord = "Qxzvbkjw";

    // The languages the app's boxes are spell-checked in (the task, notes and Quick Add boxes, and the sub-tasks).
    private static readonly string[] WindowsLanguages = ["en-CA", "en-US"];

    public string WindowsWordsPath => Path.Combine(_folder, "UserDictionary.windows");

    // The result of the last Verify.
    public bool CheckerWorks { get; private set; } = true;

    public bool WordsAccepted { get; private set; } = true;

    public bool UsingWindowsDictionary { get; private set; }

    // Whether a spell-checked box underlines the text. useWords: the box is given the user's list, as the real boxes are.
    private bool Flagged(string text, bool useWords)
    {
        var box = new TextBox { Language = XmlLanguage.GetLanguage("en-CA"), Text = text };
        System.Windows.Controls.SpellCheck.SetIsEnabled(box, true);
        if (useWords) Apply(box, []);
        return box.GetSpellingError(0) is not null;
    }

    // The first word of the list that can be tested (letters only, long enough to be a word of its own).
    private string? ProbeWord() => _words.FirstOrDefault(w => w.Length >= 4 && w.All(char.IsLetter));

    // Tests this PC and, if the app's list is not being applied, keeps the words in Windows' dictionary instead.
    // allowWindowsFallback is false in the tests, which must never write to the real Windows dictionary.
    public void Verify(bool allowWindowsFallback = true)
    {
        try
        {
            if (allowWindowsFallback) RemoveFromWindows(); // ours from an earlier run, so the test below is of the app's own list

            var probe = ProbeWord();
            CheckerWorks = Flagged(ControlWord, useWords: false);
            WordsAccepted = probe is null || !CheckerWorks || !Flagged(probe, useWords: true);
            UsingWindowsDictionary = false;

            if (!allowWindowsFallback || probe is null || !CheckerWorks || WordsAccepted) return;

            UsingWindowsDictionary = AddToWindows();
            if (!UsingWindowsDictionary) return;

            WordsAccepted = !Flagged(probe, useWords: true);
            RefreshBoxes();
        }
        catch
        {
            // A PC where the test itself cannot run: leave spell check as it is.
        }
    }

    // Puts the words into Windows' personal dictionary and writes down the ones that were not there already, so only
    // those are ever taken out again (a word the user keeps there themselves, from Office say, is left alone).
    private bool AddToWindows()
    {
        var lines = FileLines(_words).ToList();
        var before = WindowsSpelling.PersonalWords();
        if (!WindowsSpelling.Add(WindowsLanguages, lines)) return false;

        var added = lines.Where(l => !before.Contains(l)).ToList();
        try { File.WriteAllText(WindowsWordsPath, string.Join("\r\n", added), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)); }
        catch { /* the words are in Windows' dictionary; they just cannot be taken out by this app later */ }
        return true;
    }

    private void RemoveFromWindows()
    {
        if (!File.Exists(WindowsWordsPath)) return;

        try
        {
            if (WindowsSpelling.Remove(File.ReadAllLines(WindowsWordsPath).Where(l => l.Length > 0))) File.Delete(WindowsWordsPath);
        }
        catch
        {
            // Left in place: Verify tries again next time.
        }
    }

    // What the Settings button shows: a plain summary and the facts behind it, for when it needs to be reported.
    public (string Summary, string Detail) Report()
    {
        Verify(AllowWindowsFallback);

        static string Yes(bool value) => value ? "yes" : "no";
        var detail = new StringBuilder();
        detail.AppendLine($"Windows: {RuntimeInformation.OSDescription}");
        foreach (var language in WindowsLanguages) detail.AppendLine($"Spelling dictionary for {language}: {(WindowsSpelling.IsSupported(language) ? "installed" : "NOT installed")}");
        detail.AppendLine($"Spell check underlines a made-up word: {Yes(CheckerWorks)}");
        detail.AppendLine($"Your words: {_words.Count}; the test word: {ProbeWord() ?? "(none long enough)"}");
        detail.AppendLine($"The app's own list is applied here: {Yes(WordsAccepted && !UsingWindowsDictionary)}");
        detail.AppendLine($"Kept in Windows' own dictionary instead: {Yes(UsingWindowsDictionary)}");
        detail.AppendLine($"List file: {FilePath} ({(File.Exists(FilePath) ? "found" : "MISSING")})");
        detail.AppendLine($"Temporary folder writable: {Yes(TempFolderWritable())}");
        detail.AppendLine($"Spelling registry key writable: {Yes(RegistryWritable())}");

        string summary;
        if (_words.Count == 0) summary = "Your dictionary is empty, so there is nothing to test yet.";
        else if (!CheckerWorks) summary = "Spell check is not running on this PC, so no words are underlined or accepted. Windows may not have a spelling dictionary for this language installed.";
        else if (UsingWindowsDictionary && WordsAccepted) summary = "Your words were not being applied on this PC, so they are now kept in Windows' own dictionary. They are accepted.";
        else if (!WordsAccepted) summary = "Your words are still being underlined, and Windows' own dictionary could not take them either.";
        else summary = "Your words are accepted on this PC.";
        return (summary, detail.ToString().TrimEnd());
    }

    private static bool TempFolderWritable()
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "WPF");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(file, "x");
            File.Delete(file);
            return true;
        }
        catch { return false; }
    }

    private static bool RegistryWritable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Spelling\Dictionaries", writable: true)
                ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Spelling\Dictionaries");
            return key is not null;
        }
        catch { return false; }
    }
}
