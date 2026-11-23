using Xunit.Abstractions;
using System.Text.Json;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class PortabilityTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void EverythingTheAppSavesGoesNextToTheExecutableAndNowhereElse()
    {
        var cwd = Directory.GetCurrentDirectory();
        var cwdBefore = Snapshot(cwd);
        using var sb = new Sandbox();

        var entries = new List<PasskeyEntry> { Samples.Entry(Samples.ExampleHash, "01", "a"), Samples.Entry(Samples.GithubHash, "02", "b") };
        new SiteResolver().AddFromText("example.com github.com");
        SeenStore.Apply(entries);
        var record = BackupStore.WriteDeletionRecord(entries);

        var allowed = new[] { "backups", "seen.json", "sites.txt" };
        var created = Directory.EnumerateFileSystemEntries(sb.Dir).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.SequenceEqual(allowed, created);
        Assert.True(record.StartsWith(sb.Dir, StringComparison.Ordinal));
        Assert.SequenceEqual(cwdBefore, Snapshot(cwd), "The working directory changed");
    }

    [Test]
    public void ReadOnlyOperationsCreateNoFiles()
    {
        using var sb = new Sandbox();
        var resolver = new SiteResolver();
        resolver.Lookup(Samples.GithubHash);
        resolver.Resolve(new[] { Samples.Entry() });
        PasskeyStore.Parse(Samples.KeyName(Samples.ExampleHash, "01"));
        SiteDatabase.LoadBuiltIn();
        Assert.Equal(0, Directory.EnumerateFileSystemEntries(sb.Dir).Count());
    }

    [Test]
    public void DeletionRecordMustBeWrittenBeforeDeletionCanProceed()
    {
        using var sb = new Sandbox();
        File.WriteAllText(Path.Combine(sb.Dir, "backups"), "blocking file");
        var written = false;
        try
        {
            BackupStore.WriteDeletionRecord(new[] { Samples.Entry() });
            written = true;
        }
        catch (IOException) { }
        Assert.False(written, "Writing the record must fail when the backups folder is unavailable so nothing gets deleted");
    }

    [Test]
    public void SeenFileJsonIsHumanReadableAndRoundTrips()
    {
        using var sandbox = new Sandbox();
        var e = Samples.Entry();
        SeenStore.Apply(new List<PasskeyEntry> { e });
        var text = File.ReadAllText(AppPaths.SeenFile);
        Assert.Contains("\n", text, "Expected indented JSON");
        using var doc = JsonDocument.Parse(text);
        var rec = doc.RootElement.GetProperty(e.KeyName);
        Assert.True(rec.GetProperty("Baseline").GetBoolean());
        Assert.True(rec.TryGetProperty("FirstSeen", out _));
    }

    private static List<string> Snapshot(string dir) =>
        Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal).ToList();
}
