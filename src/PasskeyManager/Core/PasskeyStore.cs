using System.Diagnostics;

namespace PasskeyManager.Core;

/// <summary>Lists and deletes Windows Hello FIDO keys through certutil's NGC provider.</summary>
public static class PasskeyStore
{
    private const string Marker = "FIDO_AUTHENTICATOR//";

    public static async Task<IReadOnlyList<PasskeyEntry>> ListAsync()
    {
        var (code, output) = await RunCertUtilAsync("-csp", "NGC", "-key");
        var entries = Parse(output);

        // certutil can exit non-zero when there are simply no keys, so only fail if it printed something unusable.
        if (entries.Count == 0 && code != 0 && !string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(output.Trim());

        return entries;
    }

    public static async Task<(bool ok, string message)> DeleteAsync(PasskeyEntry entry)
    {
        var (code, output) = await RunCertUtilAsync("-csp", "NGC", "-delkey", entry.KeyName);
        return (code == 0, output.Trim());
    }

    /// <summary>Returns certutil's verbose output for one key, uninterpreted.</summary>
    public static async Task<string> GetDetailsAsync(PasskeyEntry entry)
    {
        var (code, output) = await RunCertUtilAsync("-v", "-csp", "NGC", "-key", entry.KeyName);
        return $"> certutil -v -csp NGC -key {entry.KeyName}\r\nExit code: {code}\r\n\r\n{output.Trim()}";
    }

    public static IReadOnlyList<PasskeyEntry> Parse(string certutilOutput)
    {
        var list = new List<PasskeyEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var reader = new StringReader(certutilOutput);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!line.Contains("FIDO", StringComparison.Ordinal)) continue;

            // The whole trimmed line is the key name that -delkey expects.
            var name = line.Trim();
            if (!seen.Add(name)) continue;

            string rp = "", user = "", tail = "";
            var idx = name.IndexOf(Marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var parts = name[(idx + Marker.Length)..].Split('_');
                if (parts.Length >= 2 && parts[0].Length == 64 && IsHex(parts[0]))
                {
                    rp = parts[0].ToLowerInvariant();
                    user = IsHex(parts[1]) ? parts[1].ToLowerInvariant() : "";
                    tail = string.Join("_", parts.Skip(2));
                }
            }
            list.Add(new PasskeyEntry(name, rp, user, tail));
        }
        return list;
    }

    private static bool IsHex(string s) =>
        s.Length > 0 && s.All(Uri.IsHexDigit);

    private static async Task<(int code, string output)> RunCertUtilAsync(params string[] args)
    {
        var psi = new ProcessStartInfo("certutil.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start certutil.exe");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, await stdout + await stderr);
    }
}
