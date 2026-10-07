using System.Globalization;
using Google.OrTools.Sat;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// How one CP-SAT solve runs. On this model CP-SAT finds the optimum early and spends the rest proving it; the bound comes from
/// the subsolvers with the full LP relaxation (<c>max_lp</c>, <c>reduced_costs</c>), which CP-SAT's own lineup leaves out below
/// 8 workers. Naming them makes 4 workers as fast as 16 default ones, and one worker with linearization level 2 about half
/// as fast (measured on saved configurations, see spikes/WasmSpeedTest).
/// </summary>
public sealed record CpSatParameters(int Workers, double TimeLimitSeconds, IReadOnlyList<string> Subsolvers, int? LinearizationLevel = null)
{
    /// <summary>More workers per solve do not help; spare threads solve skill pair classes side by side instead.</summary>
    public const int MaxWorkersPerSolve = 4;

    /// <summary>
    /// Random skill combinations every search checks its score model against the damage calculator before solving. The
    /// browser app lowers it: its .NET runs interpreted, where 300 checks take about 0.4 s per skill pair class.
    /// </summary>
    public static int ScoreChecks { get; set; } = 300;

    public static CpSatParameters For(int threads, double timeLimitSeconds) => Math.Clamp(threads, 1, MaxWorkersPerSolve) switch
    {
        1 => new(1, timeLimitSeconds, [], 2),
        2 => new(2, timeLimitSeconds, ["max_lp", "reduced_costs"]),
        3 => new(3, timeLimitSeconds, ["max_lp", "reduced_costs", "quick_restart"]),
        _ => new(4, timeLimitSeconds, ["max_lp", "reduced_costs", "pseudo_costs", "quick_restart"]),
    };

    /// <summary>SatParameters in text format, as <see cref="CpSolver.StringParameters"/> takes them.</summary>
    public string ToText()
    {
        var parts = new List<string>
        {
            $"num_workers:{Workers}",
            $"max_time_in_seconds:{TimeLimitSeconds.ToString(CultureInfo.InvariantCulture)}",
        };
        if (LinearizationLevel is { } level) parts.Add($"linearization_level:{level}");
        parts.AddRange(Subsolvers.Select(s => $"subsolvers:\"{s}\""));
        return string.Join(", ", parts);
    }
}

/// <summary>Where CP-SAT solves run: the native OR-Tools library, or or-tools-wasm in the browser.</summary>
public interface ICpSatBackend
{
    /// <summary>Solves <paramref name="model"/>; cancelling stops the search and throws <see cref="OperationCanceledException"/>.</summary>
    /// <param name="lane">0-based slot when several solves run at the same time (one per skill pair class), so a backend can keep them apart.</param>
    Task<CpSolverResponse> SolveAsync(CpModel model, CpSatParameters parameters, int lane, CancellationToken ct);
}

/// <summary>The OR-Tools native library; each solve runs on the thread pool, so solves on different lanes run in parallel.</summary>
public sealed class NativeCpSatBackend : ICpSatBackend
{
    public static readonly NativeCpSatBackend Instance = new();

    public Task<CpSolverResponse> SolveAsync(CpModel model, CpSatParameters parameters, int lane, CancellationToken ct) => Task.Run(() =>
    {
        using var solver = new CpSolver();
        solver.StringParameters = parameters.ToText();
        using (ct.Register(solver.StopSearch)) solver.Solve(model);
        ct.ThrowIfCancellationRequested();
        return solver.Response ?? throw new InvalidOperationException("CP-SAT returned no response.");
    }, ct);
}

/// <summary>Reads variable values from a solve's response, like <see cref="CpSolver"/> does from its own.</summary>
public static class CpSolverResponseExtensions
{
    public static long Value(this CpSolverResponse response, IntVar variable) => response.Solution[variable.GetIndex()];

    public static bool BooleanValue(this CpSolverResponse response, ILiteral literal)
    {
        var index = literal.GetIndex();
        return index >= 0 ? response.Solution[index] != 0 : response.Solution[-index - 1] == 0;
    }
}
