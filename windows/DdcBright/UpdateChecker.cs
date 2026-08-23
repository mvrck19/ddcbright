using System.Net.Http;
using System.Text.Json;

namespace DdcBright;

internal enum UpdateCheckStatus { UpToDate, UpdateAvailable, Unknown }

internal readonly record struct UpdateCheckResult(UpdateCheckStatus Status, string? LatestVersion);

/// <summary>
/// Checks GitHub Releases for a newer DdcBright version than the one
/// currently running. Pure request/compare logic, no UI -- App.xaml.cs owns
/// presenting the result.
/// </summary>
internal static class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/mvrck19/ddcbright/releases/latest";

    public static async Task<UpdateCheckResult> CheckAsync(HttpClient client, string? currentVersion)
    {
        try
        {
            // GitHub's API rejects requests with no User-Agent header.
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("DdcBright");

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("tag_name", out var tagProp))
                return new UpdateCheckResult(UpdateCheckStatus.Unknown, null);

            var tag = tagProp.GetString();
            var latestLabel = tag?.TrimStart('v');
            var latest = ParseVersion(tag);
            var current = ParseVersion(currentVersion);
            if (latest is null || current is null)
                return new UpdateCheckResult(UpdateCheckStatus.Unknown, latestLabel);

            return latest > current
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latestLabel)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, latestLabel);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Unknown, null);
        }
    }

    // "v1.2.3", "1.2.3", and "1.2.3+abcdef" (AssemblyInformationalVersion
    // can carry a +buildmetadata suffix) all need to parse.
    internal static Version? ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.TrimStart('v').Split('+')[0];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
