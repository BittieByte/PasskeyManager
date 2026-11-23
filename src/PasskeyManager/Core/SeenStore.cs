using System.Text.Json;

namespace PasskeyManager.Core;

/// <summary>
/// Remembers when this app first saw each key. Keys present on the very first scan are marked as baseline
/// (they existed on or before that date); keys that appear later get a real first-seen time.
/// </summary>
public static class SeenStore
{
    public sealed record SeenRecord(DateTime FirstSeen, bool Baseline);

    private static string FilePath => AppPaths.SeenFile;

    public static void Apply(IReadOnlyList<PasskeyEntry> entries)
    {
        if (entries.Count == 0) return;

        var firstRun = !File.Exists(FilePath);
        var db = Load();
        var now = DateTime.Now;
        var next = new Dictionary<string, SeenRecord>(StringComparer.Ordinal);

        foreach (var e in entries)
        {
            if (!db.TryGetValue(e.KeyName, out var rec))
                rec = new SeenRecord(now, firstRun);
            next[e.KeyName] = rec;
            e.FirstSeen = rec.FirstSeen;
            e.FirstSeenIsBaseline = rec.Baseline;
        }
        Save(next);
    }

    private static Dictionary<string, SeenRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, SeenRecord>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    private static void Save(Dictionary<string, SeenRecord> db)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
