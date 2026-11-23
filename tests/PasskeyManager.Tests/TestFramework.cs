using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PasskeyManager.Core;
using Xunit;
using Xunit.Abstractions;

namespace PasskeyManager.Tests;

/// <summary>An xUnit fact whose display name is the method name written as a sentence.</summary>
public sealed class TestAttribute : FactAttribute
{
    public TestAttribute([CallerMemberName] string method = "")
    {
        DisplayName = Humanize(method);
    }

    internal static string Humanize(string name)
    {
        var spaced = Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])", " ");
        var words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((w, i) => i == 0 || (w.Length > 1 && w.All(char.IsUpper)) ? w : char.ToLowerInvariant(w[0]) + w[1..]);
        return string.Join(" ", words);
    }
}

public sealed class AssertionException(string message) : Exception(message) { }

public abstract class TestBase : IDisposable
{
    protected TestBase(ITestOutputHelper output) => TestLog.Begin(output);

    public virtual void Dispose() => TestLog.End();
}

internal static class TestLog
{
    private const int MaxLines = 25;

    private static ITestOutputHelper? _output;
    private static int _lines;
    private static int _hidden;

    public static void Begin(ITestOutputHelper output)
    {
        _output = output;
        _lines = 0;
        _hidden = 0;
    }

    public static void Passed(string what)
    {
        if (_output == null) return;
        if (++_lines <= MaxLines) _output.WriteLine("  ✓ " + what);
        else _hidden++;
    }

    public static void End()
    {
        if (_output != null && _hidden > 0) _output.WriteLine($"  … and {_hidden} more checks passed");
        _output = null;
    }

    public static string Show(object? value) => value switch
    {
        null => "null",
        string s => Clip("\"" + s.Replace("\r", "\\r").Replace("\n", "\\n") + "\""),
        System.Collections.IEnumerable items => ShowItems(items),
        _ => Clip(value.ToString() ?? ""),
    };

    private static string ShowItems(System.Collections.IEnumerable items)
    {
        var all = items.Cast<object?>().ToList();
        var shown = string.Join(", ", all.Take(8).Select(Show));
        return all.Count > 8 ? $"[{shown}, … {all.Count} items]" : $"[{shown}]";
    }

    private static string Clip(string text) => text.Length <= 100 ? text : text[..100] + $"… ({text.Length} chars)";
}

public static class Assert
{
    public static void True(bool condition, string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw new AssertionException(message ?? $"Expected this to be true: {expression}");
        TestLog.Passed($"{expression} is true");
    }

    public static void False(bool condition, string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (condition) throw new AssertionException(message ?? $"Expected this to be false: {expression}");
        TestLog.Passed($"{expression} is false");
    }

    public static void Equal<T>(T expected, T actual, string? message = null,
        [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{message ?? $"Unexpected value for: {expression}"}\n    expected: {TestLog.Show(expected)}\n    actual:   {TestLog.Show(actual)}");
        TestLog.Passed($"{expression} = {TestLog.Show(actual)}");
    }

    public static void Null(object? value, string? message = null,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (value != null) throw new AssertionException($"{message ?? $"Expected null: {expression}"}\n    actual: {TestLog.Show(value)}");
        TestLog.Passed($"{expression} is null");
    }

    public static void NotNull(object? value, string? message = null,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (value == null) throw new AssertionException(message ?? $"Expected a value but was null: {expression}");
        TestLog.Passed($"{expression} is not null (= {TestLog.Show(value)})");
    }

    public static void Contains(string needle, string haystack, string? message = null,
        [CallerArgumentExpression(nameof(haystack))] string? expression = null)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
            throw new AssertionException($"{message ?? "Text not found."}\n    looked for: {TestLog.Show(needle)}\n    in:         {TestLog.Show(haystack)}");
        TestLog.Passed($"{expression} contains {TestLog.Show(needle)}");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null,
        [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        var e = expected.ToList();
        var a = actual.ToList();
        if (!e.SequenceEqual(a))
            throw new AssertionException($"{message ?? $"Sequence differs: {expression}"}\n    expected: {TestLog.Show(e)}\n    actual:   {TestLog.Show(a)}");
        TestLog.Passed($"{expression} = {TestLog.Show(a)}");
    }

    public static TException Throws<TException>(Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = null) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            TestLog.Passed($"{expression} threw {typeof(TException).Name}: {TestLog.Show(ex.Message)}");
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new AssertionException($"Expected {typeof(TException).Name} but nothing was thrown.");
    }
}

internal sealed class Sandbox : IDisposable
{
    private readonly string? _previousBase = AppPaths.BaseDirOverride;

    public string Dir { get; } = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "PasskeyManagerTests-" + Guid.NewGuid().ToString("N"))).FullName;

    public Sandbox() => AppPaths.BaseDirOverride = Dir;

    public IEnumerable<string> AllFiles() =>
        Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories);

    public void Dispose()
    {
        AppPaths.BaseDirOverride = _previousBase;
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { }
    }
}

internal static class Samples
{
    public const string ExampleHash = "a379a6f6eeafb9a55e378c118034e2751e682fab9f2d30ab13d2125586ce1947";
    public const string GithubHash = "3aeb002460381c6f258e8395d3026f571f0d9a76488dcd837639b13aed316560";
    public const string GoogleHash = "d4c9d9027326271a89ce51fcaf328ed673f17be33469ff979e8ab8dd501e664f";
    public const string SubdomainHash = "7653e4959f8eac25a7d55884d60580c5c3cf122e12ed0a34f48251bb3ea749ce";
    public const string CoUkHash = "ef991775f0d852a192506d941ab526705e2ef83f96f42e0109db6c74a15e9b47";

    public static string KeyName(string hash, string userHex, string? tail = "x") =>
        $"FIDO_AUTHENTICATOR//{hash}_{userHex}" + (tail == null ? "" : "_" + tail);

    public static PasskeyManager.Core.PasskeyEntry Entry(string hash = ExampleHash, string userHex = "6164", string tail = "x") =>
        new(KeyName(hash, userHex, tail), hash, userHex, tail);
}
