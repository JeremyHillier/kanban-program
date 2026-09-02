using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace KanbanApp.Services;

public record ReleaseNote(string Version, string Date, List<string> Items);

// Reads the release notes shown by the What's New screen straight out of the CHANGELOG.md embedded
// in the exe (see the Resource include in KanbanApp.csproj), so there's never a second hand-kept
// copy of the same notes to drift out of sync with the real changelog.
public static partial class ReleaseNotes
{
    // Matches a version heading: "## 0.67.3 — 2026-09-01". The separator is an em dash in practice,
    // but a plain hyphen is accepted too so a hand-typed entry still parses.
    [GeneratedRegex(@"^##\s+(?<version>\S+)\s*[—–-]\s*(?<date>.+?)\s*$")]
    private static partial Regex VersionHeading();

    public static List<ReleaseNote> Load(int maxVersions = 5)
    {
        var text = ReadChangelog();
        if (text is null) return [];

        var notes = new List<ReleaseNote>();
        ReleaseNote? current = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();

            var heading = VersionHeading().Match(line);
            if (heading.Success)
            {
                if (notes.Count == maxVersions) break;
                current = new ReleaseNote(heading.Groups["version"].Value, heading.Groups["date"].Value, []);
                notes.Add(current);
                continue;
            }

            // Bullets before the first heading belong to the file's intro blurb, not a release.
            if (current is not null && line.StartsWith("- "))
            {
                current.Items.Add(line[2..].Trim());
            }
        }

        return notes;
    }

    private static string? ReadChangelog()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("CHANGELOG.md", UriKind.Relative));
            if (resource is null) return null;

            using var reader = new StreamReader(resource.Stream);
            return reader.ReadToEnd();
        }
        catch
        {
            // Release notes are a nicety - never let a missing or unreadable resource break startup.
            return null;
        }
    }
}
