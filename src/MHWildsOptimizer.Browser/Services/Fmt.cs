using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>Number and name formatting shared by the panels.</summary>
public static partial class Fmt
{
    /// <summary>The app runs with invariant globalization: "1,234.56" like en-US.</summary>
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <summary>"1,234.56" with a fixed number of digits.</summary>
    public static string N(double n, int digits = 2) => n.ToString("N" + digits, En);

    /// <summary>"+5" / "-3" / "0".</summary>
    public static string Signed(int n) => n > 0 ? "+" + n : n.ToString(En);

    /// <summary>"great-sword" -> "Great Sword"; also enum names ("HittingWeakPoint" stays as is).</summary>
    public static string TitleCase(string s) => WordStart().Replace(s.Replace('-', ' ').Replace('_', ' '), m => m.Value.ToUpperInvariant());

    public static string TitleCase<T>(T value) where T : struct, Enum => TitleCase(Slug(value));

    /// <summary>The JSON name of an enum value ("hitting_weak_point", "cp_sat", "fire").</summary>
    public static string Slug<T>(T value) where T : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", _ => n.ToString(En) };

    public static string Plural(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";

    public static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.Now - when;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
            : when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", En);
    }

    [GeneratedRegex(@"\b\w")]
    private static partial Regex WordStart();
}
