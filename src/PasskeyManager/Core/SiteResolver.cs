using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PasskeyManager.Core;

/// <summary>
/// Windows only stores a SHA-256 hash of each site's RP ID, so known domains (the built-in list plus any
/// the user added) are hashed and looked up.
/// </summary>
public sealed class SiteResolver
{
    private static readonly Regex CountrySuffix =
        new(@"^(com|co|org|net|gov|edu|ac|ne|or)\.[a-z]{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    public static string UserFile => AppPaths.SitesFile;

    public SiteResolver()
    {
        try
        {
            foreach (var entry in SiteDatabase.LoadBuiltIn())
                foreach (var domain in entry.AllDomains)
                    AddHostWithParents(domain);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            // A damaged built-in list must not stop the app; user-added sites still work.
        }
        LoadUserFile();
    }

    public static string Hash(string domain) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(domain))).ToLowerInvariant();

    public string? Lookup(string hash) =>
        hash.Length > 0 && _map.TryGetValue(hash, out var site) ? site : null;

    /// <summary>Adds domains or URLs separated by whitespace, commas or semicolons. Returns how many were usable.</summary>
    public int AddFromText(string text) =>
        AddHosts(text.Split(new[] { '\r', '\n', ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Adds domains or URLs, saves new ones to sites.txt and returns how many were usable.</summary>
    public int AddHosts(IEnumerable<string> inputs)
    {
        var hosts = inputs
            .Select(Normalize)
            .Where(h => h != null)
            .Select(h => h!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var h in hosts) AddHostWithParents(h);

        if (hosts.Count > 0)
        {
            try
            {
                var existing = File.Exists(UserFile)
                    ? File.ReadAllLines(UserFile).Select(l => l.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var fresh = hosts.Where(h => !existing.Contains(h)).ToList();
                if (fresh.Count > 0) File.AppendAllLines(UserFile, fresh);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // still usable for this session
            }
        }
        return hosts.Count;
    }

    public void Resolve(IEnumerable<PasskeyEntry> entries)
    {
        foreach (var e in entries) e.Site = Lookup(e.RpIdHash);
    }

    /// <summary>Turns "https://Example.com/login" into "example.com", or null if it isn't a usable domain.</summary>
    public static string? Normalize(string input)
    {
        input = input.Trim().Trim('"', '\'');
        if (input.Length == 0) return null;
        if (!input.Contains("://", StringComparison.Ordinal)) input = "https://" + input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return null;

        var host = uri.IdnHost.ToLowerInvariant();
        if (host == "localhost") return host;
        if (IPAddress.TryParse(host, out _)) return null;
        return host.Contains('.') ? host : null;
    }

    private void LoadUserFile()
    {
        try
        {
            if (!File.Exists(UserFile)) return;
            foreach (var line in File.ReadAllLines(UserFile))
            {
                var h = Normalize(line);
                if (h != null) AddHostWithParents(h);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // An RP ID can be the full host or a parent domain, so index both, stopping before a bare TLD or
    // country second-level suffix (com, co.uk, ...).
    private void AddHostWithParents(string host)
    {
        var current = host;
        while (true)
        {
            _map.TryAdd(Hash(current), current);
            var dot = current.IndexOf('.');
            if (dot < 0) break;
            var parent = current[(dot + 1)..];
            if (!parent.Contains('.') || CountrySuffix.IsMatch(parent)) break;
            current = parent;
        }
    }
}
