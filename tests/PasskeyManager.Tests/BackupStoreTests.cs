using Xunit.Abstractions;
using System.Text;
using System.Text.Json;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class BackupStoreTests(ITestOutputHelper output) : TestBase(output)
{
    private static PasskeyEntry Sample(string site = "example.com", string user = "41646d696e", string tail = "tail")
    {
        var e = Samples.Entry(Samples.ExampleHash, user, tail);
        e.Site = site;
        e.FirstSeen = new DateTime(2026, 1, 2, 3, 4, 5);
        return e;
    }

    [Test]
    public void DeletionRecordIsCreatedInBackupsFolderNextToApp()
    {
        using var sb = new Sandbox();
        var path = BackupStore.WriteDeletionRecord(new[] { Sample() });
        Assert.True(File.Exists(path));
        Assert.Equal(Path.Combine(sb.Dir, "backups"), Path.GetDirectoryName(path)!);
    }

    [Test]
    public void DeletionRecordFileNameHasTimestampPattern()
    {
        using var sandbox = new Sandbox();
        var name = Path.GetFileName(BackupStore.WriteDeletionRecord(new[] { Sample() }));
        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(name, @"^deleted-\d{8}-\d{6}\.json$"), "Unexpected file name: " + name);
    }

    [Test]
    public void DeletionRecordContainsEveryEntryWithAllFields()
    {
        using var sandbox = new Sandbox();
        var a = Sample("a.example", "01020304", "t1");
        var b = Sample("b.example", "41646d696e", "t2");
        var path = BackupStore.WriteDeletionRecord(new[] { a, b });

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.Contains("cannot be restored", root.GetProperty("note").GetString()!);

        var entries = root.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("a.example", entries[0].GetProperty("site").GetString());
        Assert.Equal(Samples.ExampleHash, entries[0].GetProperty("rpIdHash").GetString());
        Assert.Equal("01020304", entries[0].GetProperty("userIdHex").GetString());
        Assert.Equal("AQIDBA", entries[0].GetProperty("userIdBase64Url").GetString());
        Assert.Equal("t1", entries[0].GetProperty("nameTail").GetString());
        Assert.Equal(a.KeyName, entries[0].GetProperty("keyName").GetString());
        Assert.True(entries[0].TryGetProperty("firstSeen", out _));
        Assert.Equal("b.example", entries[1].GetProperty("site").GetString());
        Assert.Equal(b.KeyName, entries[1].GetProperty("keyName").GetString());
    }

    [Test]
    public void DeletionRecordStoresNullSiteForUnidentifiedEntry()
    {
        using var sandbox = new Sandbox();
        var e = Samples.Entry();
        var path = BackupStore.WriteDeletionRecord(new[] { e });
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entry = doc.RootElement.GetProperty("entries")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("site").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("firstSeen").ValueKind);
    }

    [Test]
    public void DeletionRecordForNoEntriesIsStillValidJson()
    {
        using var sandbox = new Sandbox();
        var path = BackupStore.WriteDeletionRecord(Array.Empty<PasskeyEntry>());
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(0, doc.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Test]
    public void DeletionRecordKeepsSpecialCharactersIntact()
    {
        using var sandbox = new Sandbox();
        var e = Sample("quo\"te.example\\x");
        var path = BackupStore.WriteDeletionRecord(new[] { e });
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("quo\"te.example\\x", doc.RootElement.GetProperty("entries")[0].GetProperty("site").GetString());
    }

    [Test]
    public void DeletionRecordFailsLoudlyWhenBackupFolderCannotBeCreated()
    {
        using var sb = new Sandbox();
        File.WriteAllText(Path.Combine(sb.Dir, "backups"), "a file where the folder should be");
        Assert.Throws<IOException>(() => BackupStore.WriteDeletionRecord(new[] { Sample() }));
    }

    [Test]
    public void CsvHasHeaderBomAndCrLfLineEndings()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        BackupStore.WriteCsv(path, new[] { Sample() });

        var bytes = File.ReadAllBytes(path);
        Assert.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));

        var text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Site,UserId,UserIdHex,UserIdBase64Url,SiteHash,FirstSeenByThisApp,NameTail,KeyName", lines[0]);
    }

    [Test]
    public void CsvRowQuotesEveryFieldAndUsesDisplayValues()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        var e = Sample("example.com", "41646d696e", "tail");
        BackupStore.WriteCsv(path, new[] { e });

        var row = File.ReadAllLines(path)[1];
        var expected = string.Join(",", new[]
        {
            "example.com", "Admin", "41646d696e", "QWRtaW4", Samples.ExampleHash, "2026-01-02 03:04", "tail", e.KeyName,
        }.Select(f => "\"" + f + "\""));
        Assert.Equal(expected.TrimStart('\uFEFF'), row.TrimStart('\uFEFF'));
    }

    [Test]
    public void CsvEscapesQuotesAndKeepsCommasInsideFields()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        var e = Sample("Acme, \"Inc\"");
        BackupStore.WriteCsv(path, new[] { e });

        var row = File.ReadAllLines(path)[1];
        Assert.Contains("\"Acme, \"\"Inc\"\"\"", row);
    }

    [Test]
    public void CsvRoundTripsThroughTheSiteCsvSplitter()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        var e = Sample("Acme, \"Inc\"");
        BackupStore.WriteCsv(path, new[] { e });

        var fields = SiteDatabase.SplitCsv(File.ReadAllLines(path)[1].TrimStart('\uFEFF'));
        Assert.Equal(8, fields.Count);
        Assert.Equal("Acme, \"Inc\"", fields[0]);
        Assert.Equal(e.KeyName, fields[7]);
    }

    [Test]
    public void CsvUsesPlaceholdersForUnknownValues()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        BackupStore.WriteCsv(path, new[] { Samples.Entry() });
        var row = File.ReadAllLines(path)[1];
        Assert.Contains("\"Unknown site (a379a6f6…)\"", row);
        Assert.Contains("\"—\"", row);
    }

    [Test]
    public void CsvWithNoEntriesIsHeaderOnly()
    {
        using var sb = new Sandbox();
        var path = Path.Combine(sb.Dir, "out.csv");
        BackupStore.WriteCsv(path, Array.Empty<PasskeyEntry>());
        Assert.Equal(1, File.ReadAllLines(path).Length);
    }

    [Test]
    public void CsvWritesTheRequestedPathOnlyAndNotNextToTheApp()
    {
        using var sb = new Sandbox();
        var elsewhere = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pm-export-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var path = Path.Combine(elsewhere, "chosen.csv");
            BackupStore.WriteCsv(path, new[] { Sample() });
            Assert.True(File.Exists(path));
            Assert.Equal(0, sb.AllFiles().Count());
        }
        finally
        {
            Directory.Delete(elsewhere, true);
        }
    }
}
