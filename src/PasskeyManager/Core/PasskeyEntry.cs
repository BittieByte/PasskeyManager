using System.Text;

namespace PasskeyManager.Core;

/// <summary>One WebAuthn credential stored in Windows Hello (NGC).</summary>
public sealed class PasskeyEntry
{
    public PasskeyEntry(string keyName, string rpIdHash, string userIdHex, string nameTail = "")
    {
        KeyName = keyName;
        RpIdHash = rpIdHash;
        UserIdHex = userIdHex;
        NameTail = nameTail;
    }

    /// <summary>Exact key name as certutil prints it; this is what -delkey needs.</summary>
    public string KeyName { get; }

    /// <summary>SHA-256 of the site's RP ID, lowercase hex. Empty if unparseable.</summary>
    public string RpIdHash { get; }

    /// <summary>The WebAuthn user handle, hex. Often random bytes rather than a readable name.</summary>
    public string UserIdHex { get; }

    /// <summary>Everything in the key name after the user ID.</summary>
    public string NameTail { get; }

    public string? Site { get; set; }

    /// <summary>When this app first saw the key; Windows provides no creation time.</summary>
    public DateTime? FirstSeen { get; set; }
    public bool FirstSeenIsBaseline { get; set; }

    public string SiteLabel =>
        Site ?? (RpIdHash.Length >= 8 ? $"Unknown site ({RpIdHash[..8]}…)" : "Unrecognized entry");

    public string UserIdDisplay => UserIdFormatter.Format(UserIdHex);

    public int UserIdByteLength =>
        UserIdFormatter.TryHexToBytes(UserIdHex, out var b) ? b.Length : 0;

    /// <summary>The user handle as Base64URL without padding.</summary>
    public string UserIdBase64Url =>
        UserIdFormatter.TryHexToBytes(UserIdHex, out var b)
            ? Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_')
            : "";

    public string FirstSeenLabel
    {
        get
        {
            if (FirstSeen is not { } d) return "—";
            return FirstSeenIsBaseline ? $"on/before {d:yyyy-MM-dd}" : $"{d:yyyy-MM-dd HH:mm}";
        }
    }
}

public static class UserIdFormatter
{
    public static bool TryHexToBytes(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (hex.Length == 0 || hex.Length % 2 != 0) return false;
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Shows the user handle as text when it is printable ASCII, otherwise as shortened hex.</summary>
    public static string Format(string hex)
    {
        if (hex.Length == 0) return "(none)";
        if (!TryHexToBytes(hex, out var bytes)) return hex;

        if (bytes.All(b => b >= 0x20 && b <= 0x7E))
            return Encoding.ASCII.GetString(bytes);

        var shortHex = hex.Length > 16 ? hex[..16] + "…" : hex;
        return $"(binary ID, {bytes.Length} bytes) {shortHex}";
    }
}
