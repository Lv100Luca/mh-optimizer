using System.Collections.Concurrent;
using System.Text;

namespace MHWildsOptimizer.Core.Data;

/// <summary>
/// Forgiving search over names and skills, for every search box of the app: the query's words in any order, each one the
/// start of a word of the text or anywhere in it with the spaces taken out ("focus dragon" and "focusdragon" both find
/// Focus/Dragon Jewel), Greek letters as their names ("arkvulcan gamma" finds Arkvulcan Helm γ), punctuation and accents
/// ignored. When nothing matches, a typo or two is forgiven ("arc gamma"): those entries are close matches, best first.
/// </summary>
public static class LooseSearch
{
    /// <summary>The exact matches in their given order, or the close ones, best first, when there are none.</summary>
    public static List<T> Filter<T>(IEnumerable<T> items, Func<T, string> text, string? query)
    {
        var (exact, close) = Split(items, text, query);
        exact.AddRange(close);
        return exact;
    }

    /// <summary>
    /// The items that match exactly, in their given order (those that start with the query as typed first); when there are
    /// none, the close ones (a typo away), best first. An empty query matches everything.
    /// </summary>
    public static (List<T> Exact, List<T> Close) Split<T>(IEnumerable<T> items, Func<T, string> text, string? query)
    {
        var q = LooseQuery.Parse(query);
        var all = items as IReadOnlyList<T> ?? items.ToList();
        if (q.IsEmpty) return (all.ToList(), []);
        var leading = new List<T>();
        var exact = new List<T>();
        foreach (var item in all)
        {
            var t = LooseText.Of(text(item));
            if (Score(t, q, typos: false) is null) continue;
            if (t.StartsWith(q.Compact)) leading.Add(item);
            else exact.Add(item);
        }
        // "focus dragon": Focus/Dragon Jewel before Dragon/Focus Jewel
        leading.AddRange(exact);
        if (leading.Count > 0) return (leading, []);
        var close = all.Select((item, order) => (Item: item, Score: Score(LooseText.Of(text(item)), q, typos: true), Order: order))
            .Where(c => c.Score is not null)
            .OrderBy(c => c.Score).ThenBy(c => c.Order)
            .Select(c => c.Item)
            .ToList();
        return ([], close);
    }

    public static bool Matches(string text, string? query) => Split([text], t => t, query) is ({ Count: > 0 }, _) or (_, { Count: > 0 });

    /// <summary>
    /// Null when some word of the query is not in the text; otherwise the sum over its words of 0 (starts a word of the
    /// text), 1 (inside a word, or running on over several) or 2 + typos (with <paramref name="typos"/>: a word of the text
    /// with the same first letter starts with it after that many typos).
    /// </summary>
    public static int? Score(LooseText text, LooseQuery query, bool typos)
    {
        var total = 0;
        foreach (var w in query.Words)
        {
            if (text.Words.Any(t => t.StartsWith(w, StringComparison.Ordinal))) continue;
            if (text.Words.Any(t => t.Contains(w, StringComparison.Ordinal)) || text.RunsOn(w)) { total += 1; continue; }
            var allowed = typos ? Typos(w) : 0;
            if (allowed == 0) return null;
            var best = int.MaxValue;
            foreach (var t in text.Words)
            {
                // a typo in the first letter is rare, and allowing one matched nearly anything
                if (t[0] != w[0]) continue;
                best = Math.Min(best, PrefixDistance(w, t, allowed));
                if (best == 1) break;
            }
            if (best > allowed) return null;
            total += 2 + best;
        }
        return total;
    }

    /// <summary>Typos forgiven in a word of the query: none in short words, where one would match nearly anything.</summary>
    private static int Typos(string word) => word.Length switch
    {
        < 3 => 0,
        < 7 => 1,
        _ => 2,
    };

    /// <summary>
    /// Fewest edits (a letter changed, added, dropped, or two swapped) that turn <paramref name="word"/> into the start of
    /// <paramref name="text"/>; more than <paramref name="limit"/> when it takes more. Short words may not lose letters:
    /// "arc" is "ark" with a typo, not "ar".
    /// </summary>
    internal static int PrefixDistance(string word, string text, int limit)
    {
        var minEnd = word.Length >= 5 ? word.Length - limit : word.Length;
        var maxEnd = Math.Min(text.Length, word.Length + limit);
        if (maxEnd < minEnd) return limit + 1;
        // optimal string alignment distance over the first maxEnd letters of the text
        var d = new int[word.Length + 1, maxEnd + 1];
        for (var i = 0; i <= word.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= maxEnd; j++) d[0, j] = j;
        for (var i = 1; i <= word.Length; i++)
        {
            for (var j = 1; j <= maxEnd; j++)
            {
                var cost = word[i - 1] == text[j - 1] ? 0 : 1;
                var v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && word[i - 1] == text[j - 2] && word[i - 2] == text[j - 1]) v = Math.Min(v, d[i - 2, j - 2] + 1);
                d[i, j] = v;
            }
        }
        var best = limit + 1;
        for (var j = Math.Max(minEnd, 0); j <= maxEnd; j++) best = Math.Min(best, d[word.Length, j]);
        return best;
    }

    /// <summary>Lower case, Greek letters spelled out, accents dropped, everything but letters and digits a space.</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            var lower = char.ToLowerInvariant(c);
            switch (lower)
            {
                case 'α': sb.Append(" alpha "); break;
                case 'β': sb.Append(" beta "); break;
                case 'γ': sb.Append(" gamma "); break;
                case 'δ': sb.Append(" delta "); break;
                case 'ω': sb.Append(" omega "); break;
                case 'à' or 'á' or 'â' or 'ä' or 'ã' or 'å': sb.Append('a'); break;
                case 'è' or 'é' or 'ê' or 'ë': sb.Append('e'); break;
                case 'ì' or 'í' or 'î' or 'ï': sb.Append('i'); break;
                case 'ò' or 'ó' or 'ô' or 'ö' or 'õ': sb.Append('o'); break;
                case 'ù' or 'ú' or 'û' or 'ü': sb.Append('u'); break;
                case 'ñ': sb.Append('n'); break;
                case 'ç': sb.Append('c'); break;
                case 'ð': sb.Append('d'); break;
                case 'ß': sb.Append("ss"); break;
                default: sb.Append(char.IsLetterOrDigit(lower) ? lower : ' '); break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>A text prepared for <see cref="LooseSearch"/>: its distinct words, and all of it without spaces.</summary>
public sealed class LooseText
{
    // catalog names and texts: a bounded set, prepared once each
    private static readonly ConcurrentDictionary<string, LooseText> Cache = new(StringComparer.Ordinal);

    private readonly string _compact;
    private readonly int[] _starts;

    public string[] Words { get; }

    private LooseText(string text)
    {
        var words = LooseSearch.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Words = words.Distinct().ToArray();
        _compact = string.Concat(words);
        _starts = new int[words.Length];
        for (int i = 0, at = 0; i < words.Length; at += words[i].Length, i++) _starts[i] = at;
    }

    public static LooseText Of(string text) => Cache.GetOrAdd(text, t => new LooseText(t));

    /// <summary>Whether the text starts with <paramref name="run"/>, spaces left out.</summary>
    public bool StartsWith(string run) => _compact.StartsWith(run, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="run"/> starts at a word and runs on over the next ones, spaces left out ("focusdragon").</summary>
    public bool RunsOn(string run)
    {
        foreach (var start in _starts)
            if (_compact.AsSpan(start).StartsWith(run, StringComparison.Ordinal)) return true;
        return false;
    }
}

/// <summary>A query prepared for <see cref="LooseSearch"/>: its words.</summary>
public sealed class LooseQuery
{
    public string[] Words { get; }
    public bool IsEmpty => Words.Length == 0;

    /// <summary>The words as typed, run together.</summary>
    public string Compact { get; }

    private LooseQuery(string[] words)
    {
        Words = words.Distinct().ToArray();
        Compact = string.Concat(words);
    }

    public static LooseQuery Parse(string? query) =>
        new(LooseSearch.Normalize(query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
