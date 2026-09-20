using System.IO;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The licence agreement and privacy note travel inside the app. Their text names the company and
// the support address, which the app also holds in AppInfo - these keep the two from drifting
// apart, and hold the privacy note to what the app really does.
public sealed class LegalDocumentTests
{
    [Fact]
    public void BothDocuments_AreInsideTheApp_AndNameTheCompanyAndSupportAddress()
    {
        foreach (var document in new[] { LegalDocuments.Eula, LegalDocuments.Privacy })
        {
            Assert.True(document.Length > 1500);
            Assert.Contains(AppInfo.Company, document);
            Assert.Contains(AppInfo.SupportEmail, document);
            Assert.Contains("Last updated:", document);
        }
    }

    [Fact]
    public void TheLicence_SaysWhatWasDecided()
    {
        var eula = LegalDocuments.Eula;

        Assert.Contains("One licence is for one person", eula);
        Assert.Contains("Province of Ontario", eula);
        Assert.Contains("\"as is\"", eula);
    }

    [Fact]
    public void ThePrivacyNote_DescribesTheOneInternetRequest_AndHowToTurnItOff()
    {
        var privacy = LegalDocuments.Privacy;

        Assert.Contains("stays on your own computer", privacy);
        Assert.Contains("GitHub", privacy); // where UpdateChecker.LatestReleaseUrl points
        Assert.Contains("api.github.com", UpdateChecker.LatestReleaseUrl);
        Assert.Contains("Check for a newer version automatically", privacy); // the Settings tickbox, word for word
    }

    [Fact]
    public void TheInstallerShowsTheSameLicenceTheAppCarries()
    {
        var root = FindRepoRoot();
        var script = File.ReadAllText(Path.Combine(root, "installer", "KanbanTaskBoard.iss"));
        Assert.Contains(@"LicenseFile=..\Legal\EULA.txt", script);

        var onDisk = File.ReadAllText(Path.Combine(root, "Legal", "EULA.txt"));
        Assert.Equal(Normalise(onDisk), Normalise(LegalDocuments.Eula));

        var settings = File.ReadAllText(Path.Combine(root, "Views", "SettingsWindow.xaml"));
        Assert.Contains("Content=\"Check for a newer version automatically\"", settings);
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n").Trim();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repository root from the test folder.");
    }
}
