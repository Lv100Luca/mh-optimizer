using MHWildsOptimizer.Api;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// The damage of a build's attack or sequence against the target, which the UI leads with instead of the per-100-MV score
/// (both rank builds the same within one attack). Results saved before damage was recorded carry none: callers fall back to the score.
/// </summary>
public static class BuildDamage
{
    public static double? Of(BuildDto? b) => b?.StatsRequested.DamagePerExecution is > 0 and var d ? d : null;

    /// <summary>"damage per sequence".</summary>
    public static string Label(BuildDto b) => $"damage per {b.StatsRequested.Execution ?? "attack"}";

    /// <summary>The build's damage, or its score when it has none; whole numbers for damage.</summary>
    public static string Text(BuildDto b) => Of(b) is { } d ? Fmt.N(d, 0) : Fmt.N(b.Score);

    /// <summary>Damage of both builds when both have it (0 digits), else their scores (1 digit).</summary>
    public static (double Value, double Reference, int Digits) Compare(BuildDto b, BuildDto reference) =>
        Of(b) is { } d && Of(reference) is { } r ? (d, r, 0) : (b.Score, reference.Score, 1);
}
