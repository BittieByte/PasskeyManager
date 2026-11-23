using Xunit.Abstractions;
using System.Text.RegularExpressions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class SiteDatabaseTests(ITestOutputHelper output) : TestBase(output)
{
    private static IReadOnlyList<SiteEntry> ParseText(string text) => SiteDatabase.Parse(new StringReader(text));

    [Test]
    public void ParsesAllColumns()
    {
        var e = ParseText("example.com,www.example.com example.org,Example,demo,https://example.com/docs").Single();
        Assert.Equal("example.com", e.Domain);
        Assert.SequenceEqual(new[] { "www.example.com", "example.org" }, e.Aliases);
        Assert.Equal("Example", e.Name);
        Assert.Equal("demo", e.Category);
        Assert.Equal("https://example.com/docs", e.Source);
    }

    [Test]
    public void SkipsHeaderCommentsAndBlankLines()
    {
        var text = "# comment\r\n\r\ndomain,aliases,name,category,source\r\n   \r\na.com,,A,x,\r\n# another\r\nb.com,,B,y,\r\n";
        Assert.SequenceEqual(new[] { "a.com", "b.com" }, ParseText(text).Select(e => e.Domain));
    }

    [Test]
    public void HeaderIsOptional()
    {
        Assert.Equal(1, ParseText("a.com,,A,x,").Count);
    }

    [Test]
    public void HeaderDetectionIsCaseInsensitive()
    {
        Assert.Equal(0, ParseText("DOMAIN,aliases,name,category,source").Count);
    }

    [Test]
    public void DomainsAndAliasesAreLowercasedAndTrimmed()
    {
        var e = ParseText("  Example.COM  , WWW.Example.com  Other.ORG ,Name,cat,").Single();
        Assert.Equal("example.com", e.Domain);
        Assert.SequenceEqual(new[] { "www.example.com", "other.org" }, e.Aliases);
    }

    [Test]
    public void MissingTrailingColumnsBecomeEmpty()
    {
        var e = ParseText("example.com").Single();
        Assert.Equal(0, e.Aliases.Count);
        Assert.Equal("", e.Name);
        Assert.Equal("", e.Category);
        Assert.Equal("", e.Source);
    }

    [Test]
    public void EmptyAliasesColumnGivesNoAliases()
    {
        Assert.Equal(0, ParseText("a.com,,A,x,").Single().Aliases.Count);
        Assert.Equal(0, ParseText("a.com,   ,A,x,").Single().Aliases.Count);
    }

    [Test]
    public void EmptyDomainThrowsWithLineNumber()
    {
        var ex = Assert.Throws<FormatException>(() => ParseText("# c\r\na.com,,A,x,\r\n,,NoDomain,x,"));
        Assert.Contains("Line 3", ex.Message);
    }

    [Test]
    public void AllDomainsListsMainDomainFirstThenAliases()
    {
        var e = ParseText("a.com,b.com c.com,A,x,").Single();
        Assert.SequenceEqual(new[] { "a.com", "b.com", "c.com" }, e.AllDomains);
    }

    [Test]
    public void QuotedFieldsMayContainCommas()
    {
        var e = ParseText("a.com,,\"Acme, Inc.\",x,").Single();
        Assert.Equal("Acme, Inc.", e.Name);
    }

    [Test]
    public void EmptyInputGivesNoEntries()
    {
        Assert.Equal(0, ParseText("").Count);
    }

    [Test]
    public void SplitCsvPlainFields()
    {
        Assert.SequenceEqual(new[] { "a", "b", "c" }, SiteDatabase.SplitCsv("a,b,c"));
    }

    [Test]
    public void SplitCsvKeepsEmptyFields()
    {
        Assert.SequenceEqual(new[] { "", "", "" }, SiteDatabase.SplitCsv(",,"));
        Assert.SequenceEqual(new[] { "a", "", "" }, SiteDatabase.SplitCsv("a,,"));
        Assert.SequenceEqual(new[] { "" }, SiteDatabase.SplitCsv(""));
    }

    [Test]
    public void SplitCsvHandlesQuotesAndEscapedQuotes()
    {
        Assert.SequenceEqual(new[] { "a,b", "say \"hi\"", "z" }, SiteDatabase.SplitCsv("\"a,b\",\"say \"\"hi\"\"\",z"));
    }

    [Test]
    public void SplitCsvDoesNotTrimInsideQuotes()
    {
        Assert.SequenceEqual(new[] { "  padded  " }, SiteDatabase.SplitCsv("\"  padded  \""));
    }

    [Test]
    public void BuiltInListLoads()
    {
        var list = SiteDatabase.LoadBuiltIn();
        Assert.True(list.Count >= 50, "Expected the built-in list to have at least 50 entries, got " + list.Count);
    }

    [Test]
    public void BuiltInEntriesAllHaveNameAndCategory()
    {
        foreach (var e in SiteDatabase.LoadBuiltIn())
        {
            Assert.True(e.Name.Length > 0, $"{e.Domain} has no name");
            Assert.True(e.Category.Length > 0, $"{e.Domain} has no category");
        }
    }

    [Test]
    public void BuiltInDomainsAreWellFormedLowercaseHosts()
    {
        var pattern = new Regex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$");
        foreach (var e in SiteDatabase.LoadBuiltIn())
            foreach (var d in e.AllDomains)
                Assert.True(d == "localhost" || pattern.IsMatch(d), $"'{d}' (entry {e.Name}) is not a plain lowercase host name");
    }

    [Test]
    public void BuiltInDomainsAreAlreadyNormalized()
    {
        foreach (var e in SiteDatabase.LoadBuiltIn())
            foreach (var d in e.AllDomains)
                Assert.Equal(d, SiteResolver.Normalize(d), $"'{d}' (entry {e.Name}) changes when normalized");
    }

    [Test]
    public void BuiltInHasNoDuplicateDomainsOrAliases()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in SiteDatabase.LoadBuiltIn())
            foreach (var d in e.AllDomains)
            {
                if (seen.TryGetValue(d, out var first))
                    throw new AssertionException($"'{d}' is listed twice: under '{first}' and '{e.Name}'");
                seen[d] = e.Name;
            }
    }

    [Test]
    public void BuiltInSourcesAreEmptyOrHttpsUrls()
    {
        foreach (var e in SiteDatabase.LoadBuiltIn())
        {
            if (e.Source.Length == 0) continue;
            Assert.True(Uri.TryCreate(e.Source, UriKind.Absolute, out var uri), $"{e.Domain}: source is not an absolute URL");
            Assert.Equal("https", uri!.Scheme, $"{e.Domain}: source must be https");
        }
    }

    [Test]
    public void BuiltInHasNoDuplicateNames()
    {
        var names = SiteDatabase.LoadBuiltIn().GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Equal(0, names.Count, "Duplicate names: " + string.Join(", ", names));
    }

    [Test]
    public void BuiltInContainsWellKnownService()
    {
        Assert.True(SiteDatabase.LoadBuiltIn().Any(e => e.Domain == "github.com"));
    }
}
