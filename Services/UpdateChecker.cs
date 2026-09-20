using System.Net.Http;
using System.Text.Json;

namespace KanbanApp.Services;

// What the check found: the newest published version and its release notes, one bullet per entry.
public sealed record UpdateInfo(string Version, List<string> Notes);

// Asks the public downloads list what the latest released version is. This is the only time the
// app contacts the internet by itself, and the Privacy Note (Legal/PRIVACY.txt) describes it - so
// keep the two in step: the request carries the app's name and version and nothing else, it goes
// to GitHub's releases API for the public downloads repo, and the user can turn it off.
//
// Nothing from the reply is trusted beyond being shown as text. In particular the download link is
// never taken from it: the app only ever sends people to AppInfo.DownloadPageUrl.
public static class UpdateChecker
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/JeremyHillier/kanban-task-board-downloads/releases/latest";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private const int MaxNotes = 40;
    private const int MaxNoteLength = 600;

    // Once a day at most. A last check that claims to be in the future (the clock was changed)
    // doesn't get to switch checking off.
    internal static bool IsDue(DateTime? lastCheck, DateTime now) =>
        lastCheck is null || lastCheck > now || now - lastCheck >= CheckInterval;

    // "v0.104.0" and "0.104.0" both read as 0.104.0. Anything that isn't a plain version is never
    // newer, so a malformed reply can't produce an update prompt.
    internal static bool IsNewer(string? latest, string? current) =>
        TryParseVersion(latest, out var latestVersion) && TryParseVersion(current, out var currentVersion) && latestVersion > currentVersion;

    private static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        var cleaned = (text ?? string.Empty).Trim().TrimStart('v', 'V');
        if (!Version.TryParse(cleaned, out var parsed)) return false;

        // 0.104 and 0.104.0 are the same release; Version treats a missing part as -1.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }

    // The releases API's JSON for one release. Drafts and pre-releases are ignored, as is anything
    // without a usable version. The notes are the body's "- " lines, as CHANGELOG.md writes them.
    internal static UpdateInfo? ParseLatestRelease(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (IsTrue(root, "draft") || IsTrue(root, "prerelease")) return null;

            var tag = root.TryGetProperty("tag_name", out var tagElement) && tagElement.ValueKind == JsonValueKind.String ? tagElement.GetString() : null;
            if (!TryParseVersion(tag, out var version)) return null;

            var body = root.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String ? bodyElement.GetString() ?? string.Empty : string.Empty;
            var notes = body.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("- ") || line.StartsWith("* "))
                .Select(line => line[2..].Trim())
                .Where(line => line.Length > 0)
                .Select(line => line.Length > MaxNoteLength ? line[..MaxNoteLength].TrimEnd() + "…" : line)
                .Take(MaxNotes)
                .ToList();

            return new UpdateInfo(version.ToString(3), notes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTrue(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.True;

    // Lets a test or a self-check stand in for the internet. Never set in the shipped app.
    internal static Func<string, Task<UpdateInfo?>>? FetchOverride { get; set; }

    // Null means "couldn't find out" - offline, blocked, timed out, or a reply that made no sense.
    // A failed check is never an error the user has to deal with.
    public static async Task<UpdateInfo?> FetchLatestAsync(string appVersion, CancellationToken cancellation = default)
    {
        if (FetchOverride is { } stand) return await stand(appVersion);
        return await FetchFromInternetAsync(appVersion, cancellation);
    }

    private static async Task<UpdateInfo?> FetchFromInternetAsync(string appVersion, CancellationToken cancellation)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd($"KanbanTaskBoard/{appVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.SendAsync(request, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            return ParseLatestRelease(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
