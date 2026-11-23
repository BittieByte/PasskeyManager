using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class NgcFilesTests : TestBase
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "pm-ngc-" + Guid.NewGuid().ToString("N"))).FullName;
    private readonly string? _previous = NgcFiles.RootOverride;

    public NgcFilesTests(ITestOutputHelper output) : base(output) => NgcFiles.RootOverride = _root;

    public override void Dispose()
    {
        NgcFiles.RootOverride = _previous;
        Directory.Delete(_root, true);
        base.Dispose();
    }

    private static PasskeyEntry Entry(string userHex, string tail) =>
        new(Samples.KeyName(Samples.ExampleHash, userHex, tail), Samples.ExampleHash, userHex, tail);

    [Test]
    public void MissingFolderIsReportedInTheNote()
    {
        Directory.Delete(_root, true);
        try
        {
            var (hits, note) = NgcFiles.Find(Entry("0102030405060708", "tail"));
            Assert.Equal(0, hits.Count);
            Assert.Contains("not found", note);
            Assert.Contains(_root, note);
        }
        finally
        {
            Directory.CreateDirectory(_root);
        }
    }

    [Test]
    public void EntryWithNothingToSearchForReturnsExplanation()
    {
        var (hits, note) = NgcFiles.Find(Entry("0102", "abc"));
        Assert.Equal(0, hits.Count);
        Assert.Contains("nothing specific", note);
    }

    [Test]
    public void FindsFileByUserId()
    {
        File.WriteAllText(Path.Combine(_root, "0102030405060708.dat"), "12345");
        var (hits, note) = NgcFiles.Find(Entry("0102030405060708", ""));
        var hit = hits.Single();
        Assert.Equal("user ID", hit.MatchedOn);
        Assert.False(hit.IsDirectory);
        Assert.Equal(5L, hit.Size);
        Assert.Equal("", note);
    }

    [Test]
    public void FindsFolderByNameSuffixWithZeroSize()
    {
        Directory.CreateDirectory(Path.Combine(_root, "abcd-suffix-1234"));
        var (hits, _) = NgcFiles.Find(Entry("01", "suffix"));
        var hit = hits.Single();
        Assert.Equal("key name suffix", hit.MatchedOn);
        Assert.True(hit.IsDirectory);
        Assert.Equal(0L, hit.Size);
    }

    [Test]
    public void SearchesSubfoldersAndIsCaseInsensitive()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_root, "guid", "nested")).FullName;
        File.WriteAllText(Path.Combine(sub, "AABBCCDDEEFF0011.bin"), "x");
        var (hits, _) = NgcFiles.Find(Entry("aabbccddeeff0011", ""));
        Assert.Equal(1, hits.Count);
    }

    [Test]
    public void ReportsAnEntryOnlyOnceEvenWhenBothNeedlesMatch()
    {
        File.WriteAllText(Path.Combine(_root, "0102030405060708_suffix.dat"), "x");
        var (hits, _) = NgcFiles.Find(Entry("0102030405060708", "suffix"));
        Assert.Equal(1, hits.Count);
        Assert.Equal("user ID", hits[0].MatchedOn);
    }

    [Test]
    public void ReturnsNoHitsWhenNothingMatches()
    {
        File.WriteAllText(Path.Combine(_root, "unrelated.dat"), "x");
        var (hits, note) = NgcFiles.Find(Entry("0102030405060708", "suffix"));
        Assert.Equal(0, hits.Count);
        Assert.Equal("", note);
    }

    [Test]
    public void FindsMultipleMatches()
    {
        File.WriteAllText(Path.Combine(_root, "0102030405060708-a.dat"), "x");
        File.WriteAllText(Path.Combine(_root, "0102030405060708-b.dat"), "x");
        var (hits, _) = NgcFiles.Find(Entry("0102030405060708", ""));
        Assert.Equal(2, hits.Count);
    }

    [Test]
    public void RootUsesOverrideAndFallsBackToWindowsLocation()
    {
        Assert.Equal(_root, NgcFiles.Root);
        NgcFiles.RootOverride = null;
        Assert.Contains(Path.Combine("ServiceProfiles", "LocalService"), NgcFiles.Root);
        Assert.True(NgcFiles.Root.EndsWith(Path.Combine("Microsoft", "Ngc"), StringComparison.Ordinal));
        NgcFiles.RootOverride = _root;
    }
}
