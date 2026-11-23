namespace PasskeyManager.Core;

public sealed record FileHit(string Path, string MatchedOn, bool IsDirectory,
    DateTime Created, DateTime Modified, DateTime Accessed, long Size);

/// <summary>
/// Best-effort search of Windows Hello's protected folder for entries whose name contains a key's user ID
/// or name suffix. File times are not guaranteed to equal the passkey's creation time.
/// </summary>
public static class NgcFiles
{
    internal static string? RootOverride { get; set; }

    public static string Root => RootOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "ServiceProfiles", "LocalService", "AppData", "Local", "Microsoft", "Ngc");

    public static (IReadOnlyList<FileHit> Hits, string Note) Find(PasskeyEntry e)
    {
        var hits = new List<FileHit>();
        var root = Root;
        if (!Directory.Exists(root))
            return (hits, $"Folder not found or not accessible:\r\n{root}");

        var needles = new List<(string Label, string Value)>();
        if (e.UserIdHex.Length >= 8) needles.Add(("user ID", e.UserIdHex));
        if (e.NameTail.Length >= 4) needles.Add(("key name suffix", e.NameTail));
        if (needles.Count == 0) return (hits, "This key has nothing specific to search for.");

        var note = "";
        var scanned = 0;
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
            };
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", opts))
            {
                if (++scanned > 50_000) { note = "Stopped after scanning 50,000 entries."; break; }

                foreach (var (label, value) in needles)
                {
                    if (!path.Contains(value, StringComparison.OrdinalIgnoreCase)) continue;

                    var isDir = Directory.Exists(path);
                    FileSystemInfo info = isDir ? new DirectoryInfo(path) : new FileInfo(path);
                    hits.Add(new FileHit(path, label, isDir,
                        info.CreationTime, info.LastWriteTime, info.LastAccessTime,
                        info is FileInfo fi ? fi.Length : 0));
                    break;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            note = "Access to part of the folder was denied (it is protected by Windows).";
        }
        catch (IOException ex)
        {
            note = "Could not read the folder: " + ex.Message;
        }
        return (hits, note);
    }
}
