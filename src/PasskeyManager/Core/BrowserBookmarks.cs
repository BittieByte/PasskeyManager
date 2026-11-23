using System.Text.Json;

namespace PasskeyManager.Core;

public sealed record BookmarkScan(IReadOnlyList<string> Hosts, int FilesScanned);

/// <summary>Reads the Bookmarks files of Brave, Chrome and Edge to collect website hosts.</summary>
public static class BrowserBookmarks
{
    public static BookmarkScan Collect()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"),
            Path.Combine(local, "Google", "Chrome", "User Data"),
            Path.Combine(local, "Microsoft", "Edge", "User Data"),
        };

        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = 0;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (name != "Default" && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)) continue;

                var file = Path.Combine(dir, "Bookmarks");
                if (!File.Exists(file)) continue;
                try
                {
                    foreach (var h in ParseHosts(File.ReadAllText(file))) hosts.Add(h);
                    files++;
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
            }
        }
        return new BookmarkScan(hosts.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList(), files);
    }

    public static IReadOnlyList<string> ParseHosts(string json)
    {
        var result = new List<string>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("roots", out var roots) &&
            roots.ValueKind == JsonValueKind.Object)
        {
            foreach (var r in roots.EnumerateObject()) Walk(r.Value, result);
        }
        return result;
    }

    private static void Walk(JsonElement node, List<string> acc)
    {
        if (node.ValueKind != JsonValueKind.Object) return;

        if (node.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
        {
            var h = SiteResolver.Normalize(u.GetString() ?? "");
            if (h != null) acc.Add(h);
        }
        if (node.TryGetProperty("children", out var ch) && ch.ValueKind == JsonValueKind.Array)
            foreach (var c in ch.EnumerateArray()) Walk(c, acc);
    }
}
