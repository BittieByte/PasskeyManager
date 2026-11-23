using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class PasskeyEntryTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void SiteLabelUsesSiteWhenKnown()
    {
        var e = Samples.Entry();
        e.Site = "example.com";
        Assert.Equal("example.com", e.SiteLabel);
    }

    [Test]
    public void SiteLabelShowsShortHashWhenUnknown()
    {
        Assert.Equal("Unknown site (a379a6f6…)", Samples.Entry().SiteLabel);
    }

    [Test]
    public void SiteLabelIsUnrecognizedWithoutUsableHash()
    {
        Assert.Equal("Unrecognized entry", new PasskeyEntry("k", "", "").SiteLabel);
        Assert.Equal("Unrecognized entry", new PasskeyEntry("k", "abc", "").SiteLabel);
    }

    [Test]
    public void SiteLabelWithExactlyEightHashCharsIsUnknownSite()
    {
        Assert.Equal("Unknown site (12345678…)", new PasskeyEntry("k", "12345678", "").SiteLabel);
    }

    [Test]
    public void Base64UrlUsesUrlSafeAlphabetWithoutPadding()
    {
        Assert.Equal("-_8", new PasskeyEntry("k", "", "fbff").UserIdBase64Url);
        Assert.Equal("AQID", new PasskeyEntry("k", "", "010203").UserIdBase64Url);
        Assert.Equal("AQI", new PasskeyEntry("k", "", "0102").UserIdBase64Url);
        Assert.Equal("AQ", new PasskeyEntry("k", "", "01").UserIdBase64Url);
    }

    [Test]
    public void Base64UrlIsEmptyForInvalidHex()
    {
        Assert.Equal("", new PasskeyEntry("k", "", "").UserIdBase64Url);
        Assert.Equal("", new PasskeyEntry("k", "", "abc").UserIdBase64Url);
        Assert.Equal("", new PasskeyEntry("k", "", "zz").UserIdBase64Url);
    }

    [Test]
    public void UserIdByteLength()
    {
        Assert.Equal(0, new PasskeyEntry("k", "", "").UserIdByteLength);
        Assert.Equal(0, new PasskeyEntry("k", "", "xyz").UserIdByteLength);
        Assert.Equal(4, new PasskeyEntry("k", "", "01020304").UserIdByteLength);
        Assert.Equal(64, new PasskeyEntry("k", "", new string('a', 128)).UserIdByteLength);
    }

    [Test]
    public void UserIdDisplayDelegatesToFormatter()
    {
        Assert.Equal("ad", new PasskeyEntry("k", "", "6164").UserIdDisplay);
        Assert.Equal("(none)", new PasskeyEntry("k", "", "").UserIdDisplay);
    }

    [Test]
    public void FirstSeenLabelWhenUnknown()
    {
        Assert.Equal("—", Samples.Entry().FirstSeenLabel);
    }

    [Test]
    public void FirstSeenLabelForBaselineSaysOnOrBefore()
    {
        var e = Samples.Entry();
        e.FirstSeen = new DateTime(2026, 3, 9, 14, 5, 0);
        e.FirstSeenIsBaseline = true;
        Assert.Equal("on/before 2026-03-09", e.FirstSeenLabel);
    }

    [Test]
    public void FirstSeenLabelForLaterSightingIncludesTime()
    {
        var e = Samples.Entry();
        e.FirstSeen = new DateTime(2026, 3, 9, 14, 5, 0);
        e.FirstSeenIsBaseline = false;
        Assert.Equal("2026-03-09 14:05", e.FirstSeenLabel);
    }

    [Test]
    public void ConstructorStoresAllValues()
    {
        var e = new PasskeyEntry("name", "hash", "beef", "tail");
        Assert.Equal("name", e.KeyName);
        Assert.Equal("hash", e.RpIdHash);
        Assert.Equal("beef", e.UserIdHex);
        Assert.Equal("tail", e.NameTail);
        Assert.Null(e.Site);
        Assert.Null(e.FirstSeen);
        Assert.False(e.FirstSeenIsBaseline);
    }

    [Test]
    public void NameTailDefaultsToEmpty()
    {
        Assert.Equal("", new PasskeyEntry("k", "h", "aa").NameTail);
    }
}
