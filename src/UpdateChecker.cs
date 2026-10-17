using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SoundDrought;

/// <summary>Asks GitHub for the latest release. Sends nothing but a plain HTTPS request.</summary>
public static class UpdateChecker
{
    private const string LatestReleaseApi = "https://api.github.com/repos/Ack1-sysadmin/SoundDrought/releases/latest";

    public static Version Current { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    public sealed record Release(Version Version, string Tag, string Url);

    /// <summary>The latest release if it is newer than this build, otherwise null (also on any error).</summary>
    public static async Task<Release?> FindNewerAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"SoundDrought/{Current}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var doc = JsonDocument.Parse(await http.GetStringAsync(LatestReleaseApi));
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.GetProperty("html_url").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;

            latest = Normalize(latest);
            Log.Write($"Update check: running {Current}, latest {latest}");
            return latest > Current ? new Release(latest, tag, url) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Log.Write("Update check failed: " + ex.Message);
            return null;
        }
    }

    // Compare major.minor.build only (0.3.4 vs 0.3.4.0 must be equal).
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
