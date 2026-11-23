using Xunit.Abstractions;
using System.Text.Json;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class BrowserBookmarksTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void ReadsUrlsFromAllRootsAndNestedFolders()
    {
        const string json = """
        {
          "roots": {
            "bookmark_bar": { "type": "folder", "children": [
              { "type": "url", "name": "A", "url": "https://a.example/login" },
              { "type": "folder", "name": "Sub", "children": [
                { "type": "url", "name": "B", "url": "https://www.b.example/" },
                { "type": "folder", "children": [ { "type": "url", "url": "http://deep.example:8080/x" } ] }
              ] }
            ] },
            "other": { "type": "folder", "children": [ { "type": "url", "url": "https://c.example" } ] },
            "synced": { "type": "folder", "children": [] }
          }
        }
        """;
        var hosts = BrowserBookmarks.ParseHosts(json);
        Assert.SequenceEqual(new[] { "a.example", "www.b.example", "deep.example", "c.example" }, hosts);
    }

    [Test]
    public void IgnoresUnusableUrls()
    {
        const string json = """
        { "roots": { "bar": { "children": [
          { "url": "http://192.168.0.1/admin" },
          { "url": "http://intranet/" },
          { "url": "" },
          { "url": "not a url" },
          { "url": "https://ok.example" }
        ] } } }
        """;
        Assert.SequenceEqual(new[] { "ok.example" }, BrowserBookmarks.ParseHosts(json));
    }

    [Test]
    public void IgnoresNonStringUrlValues()
    {
        const string json = """{ "roots": { "bar": { "children": [ { "url": 42 }, { "url": null }, { "url": "https://ok.example" } ] } } }""";
        Assert.SequenceEqual(new[] { "ok.example" }, BrowserBookmarks.ParseHosts(json));
    }

    [Test]
    public void FolderNodesWithoutUrlAreStillWalked()
    {
        const string json = """{ "roots": { "bar": { "name": "x", "children": [ { "children": [ { "url": "https://in.example" } ] } ] } } }""";
        Assert.SequenceEqual(new[] { "in.example" }, BrowserBookmarks.ParseHosts(json));
    }

    [Test]
    public void IgnoresNonObjectChildrenAndNonObjectRoots()
    {
        const string json = """{ "roots": { "version": 1, "bar": { "children": [ "text", 5, null, { "url": "https://ok.example" } ] } } }""";
        Assert.SequenceEqual(new[] { "ok.example" }, BrowserBookmarks.ParseHosts(json));
    }

    [Test]
    public void MissingOrInvalidRootsGiveNoHosts()
    {
        Assert.Equal(0, BrowserBookmarks.ParseHosts("{}").Count);
        Assert.Equal(0, BrowserBookmarks.ParseHosts("""{ "roots": [] }""").Count);
        Assert.Equal(0, BrowserBookmarks.ParseHosts("[]").Count);
        Assert.Equal(0, BrowserBookmarks.ParseHosts("42").Count);
    }

    [Test]
    public void MalformedJsonThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => BrowserBookmarks.ParseHosts("{ not json"));
        Assert.Throws<JsonException>(() => BrowserBookmarks.ParseHosts(""));
    }

    [Test]
    public void HostsAreLowercased()
    {
        const string json = """{ "roots": { "bar": { "children": [ { "url": "https://MiXeD.Example/Path" } ] } } }""";
        Assert.SequenceEqual(new[] { "mixed.example" }, BrowserBookmarks.ParseHosts(json));
    }

    [Test]
    public void CollectNeverThrowsAndReturnsSortedDistinctHosts()
    {
        var scan = BrowserBookmarks.Collect();
        Assert.True(scan.FilesScanned >= 0);
        Assert.SequenceEqual(scan.Hosts.OrderBy(h => h, StringComparer.OrdinalIgnoreCase), scan.Hosts);
        Assert.Equal(scan.Hosts.Count, scan.Hosts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
