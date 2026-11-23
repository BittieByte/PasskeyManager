using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class SiteResolverTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void HashMatchesKnownSha256Vectors()
    {
        Assert.Equal(Samples.ExampleHash, SiteResolver.Hash("example.com"));
        Assert.Equal(Samples.GithubHash, SiteResolver.Hash("github.com"));
    }

    [Test]
    public void HashIsLowercaseHexOf64Chars()
    {
        var h = SiteResolver.Hash("anything.example");
        Assert.Equal(64, h.Length);
        Assert.Equal(h.ToLowerInvariant(), h);
    }

    [Test]
    public void HashIsCaseSensitiveBecauseItHashesTheExactString()
    {
        Assert.False(SiteResolver.Hash("Example.com") == SiteResolver.Hash("example.com"));
    }

    [Test]
    public void NormalizeStripsSchemePathPortQueryAndCase()
    {
        Assert.Equal("example.com", SiteResolver.Normalize("https://Example.com/login?x=1#frag"));
        Assert.Equal("example.com", SiteResolver.Normalize("http://EXAMPLE.com:8443/a/b"));
        Assert.Equal("example.com", SiteResolver.Normalize("example.com"));
        Assert.Equal("example.com", SiteResolver.Normalize("example.com/path"));
    }

    [Test]
    public void NormalizeKeepsSubdomains()
    {
        Assert.Equal("www.example.com", SiteResolver.Normalize("https://www.example.com"));
        Assert.Equal("a.b.example.com", SiteResolver.Normalize("a.b.example.com"));
    }

    [Test]
    public void NormalizeTrimsWhitespaceAndQuotes()
    {
        Assert.Equal("example.com", SiteResolver.Normalize("  \"example.com\"  "));
        Assert.Equal("example.com", SiteResolver.Normalize("'example.com'"));
    }

    [Test]
    public void NormalizeRejectsEmptyAndWhitespace()
    {
        Assert.Null(SiteResolver.Normalize(""));
        Assert.Null(SiteResolver.Normalize("   "));
        Assert.Null(SiteResolver.Normalize("\"\""));
    }

    [Test]
    public void NormalizeRejectsIpAddresses()
    {
        Assert.Null(SiteResolver.Normalize("192.168.1.1"));
        Assert.Null(SiteResolver.Normalize("http://10.0.0.1:8080/x"));
        Assert.Null(SiteResolver.Normalize("http://[::1]/"));
    }

    [Test]
    public void NormalizeRejectsHostsWithoutADot()
    {
        Assert.Null(SiteResolver.Normalize("intranet"));
        Assert.Null(SiteResolver.Normalize("https://printer/status"));
    }

    [Test]
    public void NormalizeAcceptsLocalhost()
    {
        Assert.Equal("localhost", SiteResolver.Normalize("http://localhost:3000"));
    }

    [Test]
    public void NormalizeConvertsInternationalNamesToPunycode()
    {
        Assert.Equal("xn--bcher-kva.example", SiteResolver.Normalize("https://bücher.example"));
    }

    [Test]
    public void NormalizeRejectsUnparseableInput()
    {
        Assert.Null(SiteResolver.Normalize("http://"));
        Assert.Null(SiteResolver.Normalize("https://exa mple.com"));
    }

    [Test]
    public void LookupFindsAddedDomain()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "example.com" });
        Assert.Equal("example.com", r.Lookup(Samples.ExampleHash));
    }

    [Test]
    public void LookupIsCaseInsensitiveOnTheHash()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "example.com" });
        Assert.Equal("example.com", r.Lookup(Samples.ExampleHash.ToUpperInvariant()));
    }

    [Test]
    public void LookupReturnsNullForUnknownAndEmptyHash()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        Assert.Null(r.Lookup(new string('0', 64)));
        Assert.Null(r.Lookup(""));
    }

    [Test]
    public void SubdomainAlsoMatchesItsParentDomain()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "a.example.com" });
        Assert.Equal("a.example.com", r.Lookup(Samples.SubdomainHash));
        Assert.Equal("example.com", r.Lookup(Samples.ExampleHash));
    }

    [Test]
    public void ParentIndexingStopsBeforeBareTld()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "example.com" });
        Assert.Null(r.Lookup(SiteResolver.Hash("com")));
    }

    [Test]
    public void ParentIndexingStopsBeforeCountrySecondLevelSuffix()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "foo.co.uk" });
        Assert.Equal("foo.co.uk", r.Lookup(Samples.CoUkHash));
        Assert.Null(r.Lookup(SiteResolver.Hash("co.uk")));
        Assert.Null(r.Lookup(SiteResolver.Hash("uk")));
    }

    [Test]
    public void ExactDomainWinsWhenAlsoReachableAsParent()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "example.com", "a.example.com" });
        Assert.Equal("example.com", r.Lookup(Samples.ExampleHash));
    }

    [Test]
    public void BuiltInListIsLoaded()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        Assert.Equal("github.com", r.Lookup(Samples.GithubHash));
    }

    [Test]
    public void BuiltInSubdomainEntryResolvesItsParent()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        Assert.Equal("drive.google.com", r.Lookup(SiteResolver.Hash("drive.google.com")));
        Assert.Equal("google.com", r.Lookup(Samples.GoogleHash));
    }

    [Test]
    public void EveryBuiltInDomainAndAliasResolvesToItself()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        foreach (var e in SiteDatabase.LoadBuiltIn())
            foreach (var d in e.AllDomains)
                Assert.Equal(d, r.Lookup(SiteResolver.Hash(d)), $"{d} did not resolve to itself");
    }

    [Test]
    public void AddFromTextAcceptsAllSeparators()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var n = r.AddFromText("a1.example\r\nb1.example c1.example,d1.example;e1.example\tf1.example\n\n");
        Assert.Equal(6, n);
        Assert.Equal("a1.example", r.Lookup(SiteResolver.Hash("a1.example")));
        Assert.Equal("f1.example", r.Lookup(SiteResolver.Hash("f1.example")));
    }

    [Test]
    public void AddFromTextAcceptsFullUrls()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        Assert.Equal(1, r.AddFromText("https://Shop.Example.net/cart?id=3"));
        Assert.Equal("shop.example.net", r.Lookup(SiteResolver.Hash("shop.example.net")));
    }

    [Test]
    public void AddHostsCountsOnlyUsableDistinctEntries()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var n = r.AddHosts(new[] { "a.example", "A.EXAMPLE", "https://a.example/x", "10.0.0.1", "nodot", "", "b.example" });
        Assert.Equal(2, n);
    }

    [Test]
    public void AddHostsWithNothingUsableDoesNotCreateFile()
    {
        using var sb = new Sandbox();
        var r = new SiteResolver();
        Assert.Equal(0, r.AddHosts(new[] { "", "nodot", "1.2.3.4" }));
        Assert.False(File.Exists(AppPaths.SitesFile));
    }

    [Test]
    public void AddHostsPersistsToSitesFileNextToApp()
    {
        using var sb = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "one.example", "two.example" });
        Assert.Equal(Path.Combine(sb.Dir, "sites.txt"), SiteResolver.UserFile);
        Assert.SequenceEqual(new[] { "one.example", "two.example" }, File.ReadAllLines(SiteResolver.UserFile));
    }

    [Test]
    public void AddHostsDoesNotWriteDuplicatesToFile()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        r.AddHosts(new[] { "one.example" });
        r.AddHosts(new[] { "ONE.example", "two.example" });
        r.AddHosts(new[] { "two.example" });
        Assert.SequenceEqual(new[] { "one.example", "two.example" }, File.ReadAllLines(SiteResolver.UserFile));
    }

    [Test]
    public void SavedSitesAreLoadedByANewResolver()
    {
        using var sandbox = new Sandbox();
        new SiteResolver().AddHosts(new[] { "persisted.example" });
        var second = new SiteResolver();
        Assert.Equal("persisted.example", second.Lookup(SiteResolver.Hash("persisted.example")));
    }

    [Test]
    public void UserFileWithJunkLinesIsToleratedAndUrlsAreNormalized()
    {
        using var sandbox = new Sandbox();
        File.WriteAllLines(AppPaths.SitesFile, new[] { "", "   ", "nodot", "10.0.0.1", "https://User.Example/path", "ok.example" });
        var r = new SiteResolver();
        Assert.Equal("user.example", r.Lookup(SiteResolver.Hash("user.example")));
        Assert.Equal("ok.example", r.Lookup(SiteResolver.Hash("ok.example")));
    }

    [Test]
    public void ResolveSetsSiteOnEntries()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var known = Samples.Entry(Samples.GithubHash);
        var unknown = Samples.Entry(new string('1', 64));
        r.Resolve(new[] { known, unknown });
        Assert.Equal("github.com", known.Site);
        Assert.Null(unknown.Site);
    }

    [Test]
    public void ResolveClearsSiteWhenHashIsNoLongerKnown()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var e = Samples.Entry(new string('2', 64));
        e.Site = "stale.example";
        r.Resolve(new[] { e });
        Assert.Null(e.Site);
    }

    [Test]
    public void ResolveLeavesEntriesWithoutHashUnmatched()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var e = new PasskeyEntry("FIDO weird", "", "");
        r.Resolve(new[] { e });
        Assert.Null(e.Site);
    }

    [Test]
    public void AddedSiteIsUsedByLaterResolve()
    {
        using var sandbox = new Sandbox();
        var r = new SiteResolver();
        var e = Samples.Entry(Samples.ExampleHash);
        r.Resolve(new[] { e });
        Assert.Null(e.Site);
        r.AddFromText("example.com");
        r.Resolve(new[] { e });
        Assert.Equal("example.com", e.Site);
    }
}
