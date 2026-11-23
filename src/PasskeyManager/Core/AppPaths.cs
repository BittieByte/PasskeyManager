namespace PasskeyManager.Core;

/// <summary>Everything the app saves lives next to the executable; the app is portable.</summary>
internal static class AppPaths
{
    internal static string? BaseDirOverride { get; set; }

    private static readonly string ExecutableDirectory = ResolveExecutableDirectory();

    public static string BaseDir => BaseDirOverride ?? ExecutableDirectory;

    public static string BackupDir => Directory.CreateDirectory(BackupDirPath).FullName;

    public static string BackupDirPath => Path.Combine(BaseDir, "backups");

    public static string SeenFile => Path.Combine(BaseDir, "seen.json");

    public static string SitesFile => Path.Combine(BaseDir, "sites.txt");

    internal static string ResolveExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            // Under "dotnet run" the process is the dotnet host, so fall back to the app's own folder.
            var exeName = Path.GetFileNameWithoutExtension(processPath);
            if (!exeName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(dir)) return dir;
            }
        }
        return Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    }
}
