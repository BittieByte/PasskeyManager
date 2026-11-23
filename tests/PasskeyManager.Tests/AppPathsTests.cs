using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class AppPathsTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void AllPathsAreInsideTheBaseDirectory()
    {
        using var sb = new Sandbox();
        Assert.Equal(sb.Dir, AppPaths.BaseDir);
        Assert.Equal(Path.Combine(sb.Dir, "backups"), AppPaths.BackupDirPath);
        Assert.Equal(Path.Combine(sb.Dir, "seen.json"), AppPaths.SeenFile);
        Assert.Equal(Path.Combine(sb.Dir, "sites.txt"), AppPaths.SitesFile);
    }

    [Test]
    public void BackupDirPathDoesNotCreateTheFolder()
    {
        using var sb = new Sandbox();
        _ = AppPaths.BackupDirPath;
        Assert.False(Directory.Exists(Path.Combine(sb.Dir, "backups")));
    }

    [Test]
    public void BackupDirCreatesTheFolderAndIsRepeatable()
    {
        using var sb = new Sandbox();
        var dir = AppPaths.BackupDir;
        Assert.True(Directory.Exists(dir));
        Assert.Equal(Path.Combine(sb.Dir, "backups"), dir);
        Assert.Equal(dir, AppPaths.BackupDir);
    }

    [Test]
    public void OverrideIsRestoredAfterSandbox()
    {
        var before = AppPaths.BaseDirOverride;
        using (new Sandbox()) { }
        Assert.Equal(before, AppPaths.BaseDirOverride);
    }

    [Test]
    public void ResolvedExecutableDirectoryExistsAndHasNoTrailingSeparator()
    {
        var dir = AppPaths.ResolveExecutableDirectory();
        Assert.True(Directory.Exists(dir), "Directory does not exist: " + dir);
        Assert.Equal(Path.TrimEndingDirectorySeparator(dir), dir);
    }

    [Test]
    public void ResolvedExecutableDirectoryDoesNotDependOnTheWorkingDirectory()
    {
        var before = Directory.GetCurrentDirectory();
        var expected = AppPaths.ResolveExecutableDirectory();
        var elsewhere = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pm-cwd-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Directory.SetCurrentDirectory(elsewhere);
            Assert.Equal(expected, AppPaths.ResolveExecutableDirectory());
            Assert.False(AppPaths.ResolveExecutableDirectory().Equals(elsewhere, StringComparison.Ordinal));
        }
        finally
        {
            Directory.SetCurrentDirectory(before);
            Directory.Delete(elsewhere, true);
        }
    }

    [Test]
    public void ResolvedExecutableDirectoryIsNeverTheDotnetHostFolder()
    {
        var dir = AppPaths.ResolveExecutableDirectory();
        var processPath = Environment.ProcessPath;
        if (processPath != null && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), dir);
        else
            Assert.True(Directory.Exists(dir));
    }
}
