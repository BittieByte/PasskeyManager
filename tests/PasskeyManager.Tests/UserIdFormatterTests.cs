using Xunit.Abstractions;
using PasskeyManager.Core;

namespace PasskeyManager.Tests;

public sealed class UserIdFormatterTests(ITestOutputHelper output) : TestBase(output)
{
    [Test]
    public void TryHexToBytesAcceptsValidHexOfAnyCase()
    {
        Assert.True(UserIdFormatter.TryHexToBytes("00ff", out var b));
        Assert.SequenceEqual(new byte[] { 0x00, 0xFF }, b);
        Assert.True(UserIdFormatter.TryHexToBytes("ABcd", out b));
        Assert.SequenceEqual(new byte[] { 0xAB, 0xCD }, b);
    }

    [Test]
    public void TryHexToBytesRejectsEmptyOddAndInvalid()
    {
        Assert.False(UserIdFormatter.TryHexToBytes("", out var b));
        Assert.Equal(0, b.Length);
        Assert.False(UserIdFormatter.TryHexToBytes("abc", out _));
        Assert.False(UserIdFormatter.TryHexToBytes("zz", out _));
        Assert.False(UserIdFormatter.TryHexToBytes("0x", out _));
        Assert.False(UserIdFormatter.TryHexToBytes("ab cd", out _));
    }

    [Test]
    public void FormatEmptyIsNone()
    {
        Assert.Equal("(none)", UserIdFormatter.Format(""));
    }

    [Test]
    public void FormatInvalidHexReturnsInputUnchanged()
    {
        Assert.Equal("xyz", UserIdFormatter.Format("xyz"));
        Assert.Equal("abc", UserIdFormatter.Format("abc"));
    }

    [Test]
    public void FormatPrintableAsciiShowsText()
    {
        Assert.Equal("Admin", UserIdFormatter.Format("41646d696e"));
        Assert.Equal("a b", UserIdFormatter.Format("612062"));
    }

    [Test]
    public void FormatPrintableBoundaries()
    {
        Assert.Equal(" ", UserIdFormatter.Format("20"));
        Assert.Equal("~", UserIdFormatter.Format("7e"));
        Assert.Equal("(binary ID, 1 bytes) 1f", UserIdFormatter.Format("1f"));
        Assert.Equal("(binary ID, 1 bytes) 7f", UserIdFormatter.Format("7f"));
    }

    [Test]
    public void FormatBinaryShowsLengthAndHex()
    {
        Assert.Equal("(binary ID, 4 bytes) 00010203", UserIdFormatter.Format("00010203"));
    }

    [Test]
    public void FormatLongBinaryIsTruncatedWithEllipsis()
    {
        var hex = "00112233445566778899aabbccddeeff00112233";
        Assert.Equal("(binary ID, 20 bytes) 0011223344556677…", UserIdFormatter.Format(hex));
    }

    [Test]
    public void FormatBinaryOfExactlySixteenHexCharsIsNotTruncated()
    {
        Assert.Equal("(binary ID, 8 bytes) 0001020304050607", UserIdFormatter.Format("0001020304050607"));
    }

    [Test]
    public void FormatNonAsciiBytesAreTreatedAsBinary()
    {
        Assert.Equal("(binary ID, 2 bytes) c3a9", UserIdFormatter.Format("c3a9"));
    }
}
