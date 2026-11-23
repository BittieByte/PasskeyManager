using System.Text;

namespace PasskeyManager.Core;

/// <summary>One service in the built-in list (Data/passkey-sites.csv).</summary>
public sealed record SiteEntry(
    string Domain,
    IReadOnlyList<string> Aliases,
    string Name,
    string Category,
    string Source)
{
    public IEnumerable<string> AllDomains => Aliases.Prepend(Domain);
}

/// <summary>Loads the built-in list of services that support passkeys.</summary>
public static class SiteDatabase
{
    public const string ResourceName = "PasskeySites.csv";

    public static IReadOnlyList<SiteEntry> LoadBuiltIn()
    {
        using var stream = typeof(SiteDatabase).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader);
    }

    /// <summary>
    /// Columns: domain, aliases (space separated), name, category, source.
    /// Blank lines, lines starting with # and the header row are ignored.
    /// </summary>
    public static IReadOnlyList<SiteEntry> Parse(TextReader reader)
    {
        var entries = new List<SiteEntry>();
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (line.Trim().Length == 0 || line.StartsWith('#')) continue;

            var f = SplitCsv(line);
            if (f[0].Trim().Equals("domain", StringComparison.OrdinalIgnoreCase)) continue;

            var domain = f[0].Trim().ToLowerInvariant();
            if (domain.Length == 0)
                throw new FormatException($"Line {lineNumber}: the domain is empty.");

            string Col(int i) => i < f.Count ? f[i].Trim() : "";
            var aliases = Col(1)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.ToLowerInvariant())
                .ToList();

            entries.Add(new SiteEntry(domain, aliases, Col(2), Col(3), Col(4)));
        }
        return entries;
    }

    /// <summary>Splits one CSV line; fields may be wrapped in double quotes ("" is a literal quote).</summary>
    internal static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
