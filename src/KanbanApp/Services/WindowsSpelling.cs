using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KanbanApp.Services;

// Windows' own spell-checking service (the one WPF's spell check sits on), used directly for one job: keeping the
// user's words in Windows' personal dictionary on a PC where WPF's custom-dictionary file is not being applied
// (UserDictionary.Verify). Adding goes through the service; the service has no way to take a word out again, so
// that edits the personal dictionary file itself, which Windows notices straight away (checked in a real box).
// Every call is guarded: a PC without the service, or with a language it does not support, simply reports false
// and the app carries on.
internal static class WindowsSpelling
{
    private static readonly Guid FactoryClsid = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

    [ComImport, Guid("8E018A9D-2415-4677-BF08-794EA61F94BB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        [return: MarshalAs(UnmanagedType.Interface)] object get_SupportedLanguages();
        [return: MarshalAs(UnmanagedType.Bool)] bool IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
        ISpellChecker CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
    }

    [ComImport, Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        [return: MarshalAs(UnmanagedType.LPWStr)] string get_LanguageTag();
        [return: MarshalAs(UnmanagedType.Interface)] object Check([MarshalAs(UnmanagedType.LPWStr)] string text);
        [return: MarshalAs(UnmanagedType.Interface)] object Suggest([MarshalAs(UnmanagedType.LPWStr)] string word);
        void Add([MarshalAs(UnmanagedType.LPWStr)] string word);
    }

    // The personal dictionary every language shares: one word a line, UTF-16 with a byte-order mark.
    public static string PersonalDictionaryPath => PersonalDictionaryOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Spelling", "neutral", "default.dic");

    // The tests point this at a file of their own.
    internal static string? PersonalDictionaryOverride { get; set; }

    private static ISpellCheckerFactory? Factory()
    {
        try { return Activator.CreateInstance(Type.GetTypeFromCLSID(FactoryClsid)!) as ISpellCheckerFactory; }
        catch { return null; }
    }

    // Whether Windows has a spelling dictionary for the language (en-CA, en-US ...).
    public static bool IsSupported(string language)
    {
        try { return Factory()?.IsSupported(language) == true; }
        catch { return false; }
    }

    // The words in Windows' personal dictionary now.
    public static HashSet<string> PersonalWords()
    {
        try
        {
            return File.Exists(PersonalDictionaryPath)
                ? File.ReadAllLines(PersonalDictionaryPath).Where(l => l.Length > 0).ToHashSet(StringComparer.Ordinal)
                : [];
        }
        catch { return []; }
    }

    // Puts the words into Windows' personal dictionary, through the first of the languages Windows has. False when it cannot.
    public static bool Add(IEnumerable<string> languages, IEnumerable<string> words)
    {
        try
        {
            var factory = Factory();
            if (factory is null) return false;

            foreach (var language in languages)
            {
                if (!factory.IsSupported(language)) continue;

                var checker = factory.CreateSpellChecker(language);
                foreach (var word in words) checker.Add(word);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // Takes exactly these words back out of the personal dictionary file (and nothing else in it).
    public static bool Remove(IEnumerable<string> words)
    {
        try
        {
            var path = PersonalDictionaryPath;
            if (!File.Exists(path)) return true;

            var gone = words.ToHashSet(StringComparer.Ordinal);
            if (gone.Count == 0) return true;

            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            var lines = encoding.GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF').Split("\r\n");
            var kept = lines.Where(l => !gone.Contains(l)).ToList();
            if (kept.Count == lines.Length) return true;

            File.WriteAllText(path, string.Join("\r\n", kept), encoding);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
