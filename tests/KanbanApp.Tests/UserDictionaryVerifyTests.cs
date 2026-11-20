using System.IO;
using System.Text;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// UserDictionary.Verify: is the user's list really accepted on this PC, and the way back out of Windows' own dictionary.
[Collection(WpfCollection.Name)]
public sealed class UserDictionaryVerifyTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose()
    {
        WindowsSpelling.PersonalDictionaryOverride = null;
        _temp.Dispose();
    }

    // Letters only and not a word anywhere, and different every run (WPF remembers earlier runs' words).
    private static string FreshWord() => "Zq" + new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(7).ToArray()) + "x";

    [Fact]
    public void WhereTheListIsApplied_TheCheckSaysSo_AndWindowsDictionaryIsLeftAlone() => wpf.Run(() =>
    {
        UserDictionary.FolderOverride = _temp.File("dict");
        var dictionary = new UserDictionary();
        dictionary.Set(FreshWord() + "\n" + FreshWord());

        var (summary, detail) = dictionary.Report();

        Assert.True(dictionary.CheckerWorks);
        Assert.True(dictionary.WordsAccepted);
        Assert.False(dictionary.UsingWindowsDictionary);
        Assert.Equal("Your words are accepted on this PC.", summary);
        Assert.Contains("The app's own list is applied here: yes", detail);
        Assert.False(File.Exists(dictionary.WindowsWordsPath));
    });

    [Fact]
    public void AnEmptyList_HasNothingToTest() => wpf.Run(() =>
    {
        UserDictionary.FolderOverride = _temp.File("dict");

        Assert.StartsWith("Your dictionary is empty", new UserDictionary().Report().Summary);
    });

    [Fact]
    public void TakingWordsOutOfWindowsDictionary_RemovesOnlyThoseWords_AndKeepsTheFileFormat()
    {
        var file = _temp.File("default.dic");
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        File.WriteAllText(file, string.Join("\r\n", "Abdul", "Hillier", "Kefron", "kefron", "she/her/hers"), encoding);
        WindowsSpelling.PersonalDictionaryOverride = file;

        Assert.True(WindowsSpelling.Remove(["Kefron", "kefron", "NotThere"]));

        var bytes = File.ReadAllBytes(file);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]); // still UTF-16 with its byte-order mark
        Assert.Equal(["Abdul", "Hillier", "she/her/hers"], File.ReadAllLines(file));
        Assert.Equal(new HashSet<string> { "Abdul", "Hillier", "she/her/hers" }, WindowsSpelling.PersonalWords());
    }
}
