using Xunit.Abstractions;
using System.Text.Json;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class SeenStoreTests(ITestOutputHelper output) : TestBase(output)
{
    private static PasskeyEntry Key(string user) => Samples.Entry(Samples.ExampleHash, user, "t");

    [Test]
    public void FirstScanMarksEveryKeyAsBaseline()
    {
        using var sandbox = new Sandbox();
        var list = new List<PasskeyEntry> { Key("01"), Key("02") };
        var before = DateTime.Now;
        SeenStore.Apply(list);
        foreach (var e in list)
        {
            Assert.True(e.FirstSeenIsBaseline);
            Assert.True(e.FirstSeen is { } d && d >= before.AddSeconds(-1) && d <= DateTime.Now.AddSeconds(1));
        }
        Assert.True(File.Exists(AppPaths.SeenFile));
    }

    [Test]
    public void KeysSeenBeforeKeepTheirOriginalTimestampAndBaselineFlag()
    {
        using var sandbox = new Sandbox();
        var first = new List<PasskeyEntry> { Key("01") };
        SeenStore.Apply(first);
        var original = first[0].FirstSeen;

        var second = new List<PasskeyEntry> { Key("01") };
        SeenStore.Apply(second);
        Assert.Equal(original, second[0].FirstSeen);
        Assert.True(second[0].FirstSeenIsBaseline);
    }

    [Test]
    public void KeyAppearingLaterIsNotBaseline()
    {
        using var sandbox = new Sandbox();
        SeenStore.Apply(new List<PasskeyEntry> { Key("01") });

        var later = new List<PasskeyEntry> { Key("01"), Key("02") };
        SeenStore.Apply(later);
        Assert.True(later[0].FirstSeenIsBaseline);
        Assert.False(later[1].FirstSeenIsBaseline);
        Assert.NotNull(later[1].FirstSeen);
    }

    [Test]
    public void LaterKeyKeepsItsRealFirstSeenTimeOnNextScan()
    {
        using var sandbox = new Sandbox();
        SeenStore.Apply(new List<PasskeyEntry> { Key("01") });
        SeenStore.Apply(new List<PasskeyEntry> { Key("01"), Key("02") });

        var third = new List<PasskeyEntry> { Key("01"), Key("02") };
        SeenStore.Apply(third);
        Assert.False(third[1].FirstSeenIsBaseline);
    }

    [Test]
    public void EmptyScanDoesNotCreateOrWipeHistory()
    {
        using var sandbox = new Sandbox();
        SeenStore.Apply(new List<PasskeyEntry>());
        Assert.False(File.Exists(AppPaths.SeenFile));

        SeenStore.Apply(new List<PasskeyEntry> { Key("01") });
        var saved = File.ReadAllText(AppPaths.SeenFile);
        SeenStore.Apply(new List<PasskeyEntry>());
        Assert.Equal(saved, File.ReadAllText(AppPaths.SeenFile));
    }

    [Test]
    public void KeysThatDisappearAreDroppedFromTheFile()
    {
        using var sandbox = new Sandbox();
        var a = Key("01");
        var b = Key("02");
        SeenStore.Apply(new List<PasskeyEntry> { a, b });
        SeenStore.Apply(new List<PasskeyEntry> { a });

        using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.SeenFile));
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.SequenceEqual(new[] { a.KeyName }, names);
    }

    [Test]
    public void CorruptFileIsTreatedAsEmptyHistoryAndRepaired()
    {
        using var sandbox = new Sandbox();
        File.WriteAllText(AppPaths.SeenFile, "{ this is not json");
        var list = new List<PasskeyEntry> { Key("01") };
        SeenStore.Apply(list);

        Assert.NotNull(list[0].FirstSeen);
        Assert.False(list[0].FirstSeenIsBaseline);
        using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.SeenFile));
        Assert.Equal(1, doc.RootElement.EnumerateObject().Count());
    }

    [Test]
    public void FileHoldingJsonNullIsTreatedAsEmptyHistory()
    {
        using var sandbox = new Sandbox();
        File.WriteAllText(AppPaths.SeenFile, "null");
        var list = new List<PasskeyEntry> { Key("01") };
        SeenStore.Apply(list);
        Assert.NotNull(list[0].FirstSeen);
    }

    [Test]
    public void HistoryIsKeyedByFullKeyName()
    {
        using var sandbox = new Sandbox();
        SeenStore.Apply(new List<PasskeyEntry> { Key("01") });
        var other = new List<PasskeyEntry> { Key("01"), Key("02") };
        SeenStore.Apply(other);
        Assert.True(other[0].FirstSeenIsBaseline);
        Assert.False(other[1].FirstSeenIsBaseline);
    }

    [Test]
    public void SavingFailureDoesNotThrow()
    {
        using var sb = new Sandbox();
        Directory.CreateDirectory(AppPaths.SeenFile);
        var list = new List<PasskeyEntry> { Key("01") };
        SeenStore.Apply(list);
        Assert.NotNull(list[0].FirstSeen);
    }

    [Test]
    public void SeenFileLivesNextToApp()
    {
        using var sb = new Sandbox();
        SeenStore.Apply(new List<PasskeyEntry> { Key("01") });
        Assert.SequenceEqual(new[] { Path.Combine(sb.Dir, "seen.json") }, sb.AllFiles());
    }
}
