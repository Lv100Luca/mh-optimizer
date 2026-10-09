using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Damage;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// A build's "scored on" text (<c>StatsDto.ScoredOn</c>) taken apart: "Sequence: A + B x2 (Element phial) against Rey Dau,
/// Head (raw hitzone 70)". Saved runs keep only this text, so their sequence is known by its step names.
/// </summary>
/// <param name="Attack">The attack name without the phial ("True Charged Slash Lv3"), or the whole "Sequence: ..." text.</param>
/// <param name="StepList">A sequence's steps ("A + B x2"); null for a single attack or the average hit.</param>
/// <param name="Target">"Rey Dau, Head (raw hitzone 70)"; null when the text has none.</param>
public sealed partial record ScoredOnText(string Attack, string? Phial, string? StepList, string? Target)
{
    private const string Against = " against ";

    public bool IsSequence => StepList is not null;

    /// <summary>The steps one by one, "x2" written "×2".</summary>
    public IReadOnlyList<string> Steps => StepList is null ? [] : StepList.Split(" + ").Select(s => Repeat().Replace(s, " ×$1")).ToList();

    public static ScoredOnText Parse(string text)
    {
        var at = text.IndexOf(Against, StringComparison.Ordinal);
        var attack = at >= 0 ? text[..at] : text;
        var target = at >= 0 ? text[(at + Against.Length)..] : null;
        string? phial = null;
        if (PhialSuffix().Match(attack) is { Success: true } m)
        {
            attack = m.Groups[1].Value;
            phial = m.Groups[2].Value;
        }
        var steps = attack.StartsWith(AttackProfile.SequencePrefix, StringComparison.Ordinal) ? attack[AttackProfile.SequencePrefix.Length..] : null;
        return new ScoredOnText(attack, phial, steps, target);
    }

    [GeneratedRegex(@"^(.*) \((\w+) phial\)$")]
    private static partial Regex PhialSuffix();

    [GeneratedRegex(@" x(\d+)$")]
    private static partial Regex Repeat();
}
