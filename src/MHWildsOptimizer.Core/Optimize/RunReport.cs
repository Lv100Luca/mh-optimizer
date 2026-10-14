using System.Globalization;
using MHWildsOptimizer.Core.Damage;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>How far CP-SAT got with the solve that found a build.</summary>
public enum ProofKind
{
    /// <summary>No build left in that search scores higher.</summary>
    Proved,
    /// <summary>Builds 2 and on with the shorter rank time limit: ranked by the best build the solve found, usually the same.</summary>
    Ranked,
    /// <summary>The solve ran out of its time limit before it proved the build it found the best.</summary>
    TimeLimit,
}

/// <param name="GapPercent">Not proved: how much better a build might still be, as the solver's bound over the build found.</param>
/// <param name="TimeLimitSeconds">The time limit the solve had.</param>
public sealed record SolveProof(ProofKind Kind, double GapPercent, double TimeLimitSeconds);

/// <summary>
/// Where a run is, as one status line for people (the progress log has the details): up to three levels, e.g. the weapon
/// start of weapon tuning, the armor search round and the build being solved. Setting a level clears the ones below it.
/// </summary>
public sealed class RunStage(Action<string> report)
{
    private readonly string?[] _levels = new string?[3];

    public void Set(int level, string? text)
    {
        string line;
        lock (_levels)
        {
            _levels[level] = text;
            for (var i = level + 1; i < _levels.Length; i++) _levels[i] = null;
            line = string.Join(" · ", _levels.Where(x => !string.IsNullOrEmpty(x)));
        }
        report(line);
    }
}

/// <summary>Numbers for the progress log, in the units the results show.</summary>
public static class OptimizerText
{
    /// <summary>A score (per 100 MV) as the damage per attack the results show: "1,150".</summary>
    public static string Damage(ResolvedAttackProfile attack, double score) =>
        attack.DamagePerExecution(score).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"8 s", "2 min", "2.5 min".</summary>
    public static string Seconds(double seconds) =>
        seconds < 90 ? $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)} s" : $"{(seconds / 60).ToString("0.#", CultureInfo.InvariantCulture)} min";
}
