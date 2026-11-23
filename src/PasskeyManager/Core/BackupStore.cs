using System.Text;
using System.Text.Json;

namespace PasskeyManager.Core;

public static class BackupStore
{
    /// <summary>Writes a list of what is about to be deleted. It is not a restorable backup: private keys cannot be exported.</summary>
    public static string WriteDeletionRecord(IEnumerable<PasskeyEntry> entries)
    {
        var path = Path.Combine(AppPaths.BackupDir, $"deleted-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var payload = new
        {
            createdAt = DateTime.Now,
            note = "Record of passkeys deleted from Windows Hello. Private keys cannot be restored from this file.",
            entries = entries.Select(e => new
            {
                site = e.Site,
                rpIdHash = e.RpIdHash,
                userIdHex = e.UserIdHex,
                userIdBase64Url = e.UserIdBase64Url,
                nameTail = e.NameTail,
                firstSeen = e.FirstSeen,
                keyName = e.KeyName,
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public static void WriteCsv(string path, IEnumerable<PasskeyEntry> entries)
    {
        var sb = new StringBuilder("Site,UserId,UserIdHex,UserIdBase64Url,SiteHash,FirstSeenByThisApp,NameTail,KeyName\r\n");
        foreach (var e in entries)
        {
            var cols = new[] { e.SiteLabel, e.UserIdDisplay, e.UserIdHex, e.UserIdBase64Url, e.RpIdHash, e.FirstSeenLabel, e.NameTail, e.KeyName };
            sb.Append(string.Join(",", cols.Select(Csv))).Append("\r\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}
