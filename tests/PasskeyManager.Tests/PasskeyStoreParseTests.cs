using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class PasskeyStoreParseTests(ITestOutputHelper output) : TestBase(output)
{
    private static readonly string User = "0102030405060708";

    [Test]
    public void ParsesHashUserAndTail()
    {
        var line = $"FIDO_AUTHENTICATOR//{Samples.ExampleHash}_{User}_abc_def";
        var e = PasskeyStore.Parse(line).Single();
        Assert.Equal(line, e.KeyName);
        Assert.Equal(Samples.ExampleHash, e.RpIdHash);
        Assert.Equal(User, e.UserIdHex);
        Assert.Equal("abc_def", e.NameTail);
    }

    [Test]
    public void KeyNameIsTheWholeTrimmedLine()
    {
        var key = Samples.KeyName(Samples.ExampleHash, User);
        var e = PasskeyStore.Parse("   \t" + key + "  \t").Single();
        Assert.Equal(key, e.KeyName);
    }

    [Test]
    public void UppercaseHexIsNormalizedToLowercase()
    {
        var key = $"FIDO_AUTHENTICATOR//{Samples.ExampleHash.ToUpperInvariant()}_{User.ToUpperInvariant()}_t";
        var e = PasskeyStore.Parse(key).Single();
        Assert.Equal(Samples.ExampleHash, e.RpIdHash);
        Assert.Equal(User, e.UserIdHex);
        Assert.Equal(key, e.KeyName);
    }

    [Test]
    public void TailIsEmptyWhenThereAreOnlyTwoParts()
    {
        var e = PasskeyStore.Parse($"FIDO_AUTHENTICATOR//{Samples.ExampleHash}_{User}").Single();
        Assert.Equal("", e.NameTail);
        Assert.Equal(User, e.UserIdHex);
    }

    [Test]
    public void EmptyInputGivesNoEntries()
    {
        Assert.Equal(0, PasskeyStore.Parse("").Count);
        Assert.Equal(0, PasskeyStore.Parse("\r\n\r\n   \r\n").Count);
    }

    [Test]
    public void LinesWithoutFidoAreIgnored()
    {
        var output = string.Join("\r\n",
            "NGC Key Storage Provider:",
            "  Microsoft Passport Key Storage Provider",
            "CertUtil: -key command completed successfully.");
        Assert.Equal(0, PasskeyStore.Parse(output).Count);
    }

    [Test]
    public void MixedCertutilOutputKeepsOnlyFidoKeys()
    {
        var k1 = Samples.KeyName(Samples.ExampleHash, User);
        var k2 = Samples.KeyName(Samples.GithubHash, "aabbccdd");
        var output = $"NGC Key Storage Provider:\r\n  {k1}\r\n  S-1-5-21-1111/other-key\r\n  {k2}\r\nCertUtil: -key command completed successfully.\r\n";
        var list = PasskeyStore.Parse(output);
        Assert.SequenceEqual(new[] { k1, k2 }, list.Select(e => e.KeyName));
    }

    [Test]
    public void DuplicateKeyNamesAppearOnce()
    {
        var key = Samples.KeyName(Samples.ExampleHash, User);
        Assert.Equal(1, PasskeyStore.Parse(key + "\r\n" + key + "\r\n  " + key).Count);
    }

    [Test]
    public void HandlesLfAndCrLfLineEndings()
    {
        var k1 = Samples.KeyName(Samples.ExampleHash, "aa");
        var k2 = Samples.KeyName(Samples.GithubHash, "bb");
        Assert.Equal(2, PasskeyStore.Parse(k1 + "\n" + k2).Count);
        Assert.Equal(2, PasskeyStore.Parse(k1 + "\r\n" + k2).Count);
    }

    [Test]
    public void FidoLineWithoutMarkerStillListedWithoutParsedParts()
    {
        var e = PasskeyStore.Parse("SOMETHING_FIDO_ELSE").Single();
        Assert.Equal("SOMETHING_FIDO_ELSE", e.KeyName);
        Assert.Equal("", e.RpIdHash);
        Assert.Equal("", e.UserIdHex);
    }

    [Test]
    public void HashWithWrongLengthIsNotParsed()
    {
        var e = PasskeyStore.Parse($"FIDO_AUTHENTICATOR//{Samples.ExampleHash[..63]}_{User}_x").Single();
        Assert.Equal("", e.RpIdHash);
        Assert.Equal("", e.UserIdHex);
        Assert.Equal("", e.NameTail);
    }

    [Test]
    public void HashWithNonHexCharactersIsNotParsed()
    {
        var bad = "z" + Samples.ExampleHash[1..];
        var e = PasskeyStore.Parse($"FIDO_AUTHENTICATOR//{bad}_{User}_x").Single();
        Assert.Equal("", e.RpIdHash);
    }

    [Test]
    public void NonHexUserIdBecomesEmptyButHashIsKept()
    {
        var e = PasskeyStore.Parse($"FIDO_AUTHENTICATOR//{Samples.ExampleHash}_not-hex_tail").Single();
        Assert.Equal(Samples.ExampleHash, e.RpIdHash);
        Assert.Equal("", e.UserIdHex);
        Assert.Equal("tail", e.NameTail);
    }

    [Test]
    public void MarkerWithoutAnyPartsIsListedUnparsed()
    {
        var e = PasskeyStore.Parse("FIDO_AUTHENTICATOR//").Single();
        Assert.Equal("", e.RpIdHash);
    }

    [Test]
    public void MarkerIsCaseSensitive()
    {
        var line = $"fido_authenticator//{Samples.ExampleHash}_{User}";
        Assert.Equal(0, PasskeyStore.Parse(line).Count);
    }

    [Test]
    public void ManyEntriesKeepTheirOrder()
    {
        var keys = Enumerable.Range(0, 50)
            .Select(i => Samples.KeyName(Samples.ExampleHash, i.ToString("x2"), "t" + i))
            .ToList();
        var list = PasskeyStore.Parse(string.Join("\r\n", keys));
        Assert.SequenceEqual(keys, list.Select(e => e.KeyName));
    }
}
