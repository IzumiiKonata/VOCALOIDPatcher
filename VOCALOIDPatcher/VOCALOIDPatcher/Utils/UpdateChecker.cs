using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VOCALOIDPatcher.Utils;

public static class UpdateChecker
{
    private const string Repo = "IzumiiKonata/VOCALOIDPatcher";
    private const string DefaultReleasesUrl = "https://github.com/" + Repo + "/releases";

    private static readonly Source[] Sources =
    {
        new("https://api.github.com", "https://github.com")
    };

    public static string? LatestVersion { get; private set; }
    public static bool HasUpdate { get; private set; }
    public static string ReleasesPageUrl { get; private set; } = DefaultReleasesUrl;

    public static event Action? UpdateAvailable;

    public static void CheckAsync()
    {
        Task.Run(Check);
    }

    private static async Task Check()
    {
        foreach (var source in Sources)
        {
            try
            {
                if (await CheckSource(source))
                    return;
            }
            catch (Exception e)
            {
                Debug.Print($"检查更新失败 ({source.WebBase}): {e.Message}");
            }
        }
    }

    private static async Task<bool> CheckSource(Source source)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VOCALOIDPatcher");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var json = await client.GetStringAsync($"{source.ApiBase}/repos/{Repo}/releases/latest");

        ReleasesPageUrl = $"{source.WebBase}/{Repo}/releases";

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement))
        {
            Debug.Print($"检查更新失败 ({source.WebBase}): 响应中缺少 tag_name");
            return true;
        }

        var tag = tagElement.GetString();
        var latest = Normalize(tag);
        if (latest == null)
        {
            Debug.Print($"检查更新失败 ({source.WebBase}): 无法解析版本号 {tag}");
            return true;
        }

        if (Compare(latest, Patcher.Version) <= 0)
        {
            Debug.Print($"已是最新版本: {Patcher.Version} (最新发布 {latest}, 源 {source.WebBase})");
            return true;
        }

        LatestVersion = latest;
        HasUpdate = true;
        Debug.Print($"发现新版本: {latest} (当前 {Patcher.Version}, 源 {source.WebBase})");
        UpdateAvailable?.Invoke();
        return true;
    }

    private static string? Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(1);

        var parts = trimmed.Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[0].Trim(), out _))
            return null;

        var (major, minor, patch) = Parse(trimmed);
        return $"{major}.{minor}.{patch}";
    }

    private static int Compare(string a, string b)
    {
        var va = Parse(a);
        var vb = Parse(b);

        if (va.Major != vb.Major) return va.Major.CompareTo(vb.Major);
        if (va.Minor != vb.Minor) return va.Minor.CompareTo(vb.Minor);
        return va.Patch.CompareTo(vb.Patch);
    }

    private static (int Major, int Minor, int Patch) Parse(string version)
    {
        var parts = version.Split('.');
        return (PartAt(parts, 0), PartAt(parts, 1), PartAt(parts, 2));
    }

    private static int PartAt(string[] parts, int index)
    {
        if (index >= parts.Length)
            return 0;

        return int.TryParse(parts[index].Trim(), out var value) ? value : 0;
    }

    private readonly struct Source
    {
        public string ApiBase { get; }
        public string WebBase { get; }

        public Source(string apiBase, string webBase)
        {
            ApiBase = apiBase;
            WebBase = webBase;
        }
    }
}
