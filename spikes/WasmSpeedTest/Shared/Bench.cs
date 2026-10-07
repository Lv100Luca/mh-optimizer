using System.Diagnostics;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace WasmSpeedTest;

/// <param name="Beam">Overrides the config's max_states_per_depth when set.</param>
/// <param name="CpSat">Runs the CP-SAT engine instead of the beam search (native only).</param>
public sealed record BenchCase(string Config, int? Beam = null, bool CpSat = false)
{
    public string Name => CpSat ? $"{Config} cp-sat" : Beam is { } b ? $"{Config} @{b / 1000}k" : Config;
}

public sealed record BenchResult(string Case, int Threads, double Seconds, long States, double Best, string? Error = null);

/// <summary>Runs one saved weapon config with the Beam engine on a given thread count; shared by the native and browser runners.</summary>
public static class Bench
{
    /// <summary>Weapon configs under inputs/profiles/Luca/weapons, lightest first.</summary>
    public static readonly BenchCase[] Cases = [new("Test", 20_000), new("Test"), new("Current"), new("the best")];

    /// <summary>CP-SAT on the same configs, for the native runner.</summary>
    public static readonly BenchCase[] CpSatCases = [new("Test", CpSat: true), new("Current", CpSat: true), new("the best", CpSat: true)];

    /// <summary>The top-level files in data/ that <see cref="GameDataLoader.Load"/> reads.</summary>
    public static readonly string[] DataFiles =
    [
        "skills.json", "armor_hr.json", "decorations.json", "charms.json", "gogma_weapons_base.json",
        "gogma_skill_pool.json", "random_talisman_pool.json",
    ];

    public static ResolvedRequest Resolve(GameData data, string weaponsDirectory, BenchCase c, int threads)
    {
        var path = Path.Combine(weaponsDirectory, c.Config + ".json");
        var request = RequestLoader.Read(path);
        // CP-SAT has no browser build, so the browser cases use the beam search
        request = request with
        {
            Options = request.Options with
            {
                Engine = c.CpSat ? OptimizerEngine.CpSat : OptimizerEngine.Beam,
                MaxThreads = threads,
                MaxStatesPerDepth = c.Beam ?? request.Options.MaxStatesPerDepth,
            },
        };
        return RequestLoader.Resolve(request, data, Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    }

    public static BenchResult Run(GameData data, string weaponsDirectory, BenchCase c, int threads, IProgress<string>? progress = null)
    {
        var resolved = Resolve(data, weaponsDirectory, c, threads);
        if (!resolved.IsValid)
            return new BenchResult(c.Name, threads, 0, 0, 0, string.Join("; ", resolved.Errors));

        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, resolved).Run(progress);
        sw.Stop();
        return new BenchResult(c.Name, resolved.Options.EffectiveThreads, sw.Elapsed.TotalSeconds,
            result.PairResults.Sum(p => p.StatesEvaluated), result.AllBuilds.FirstOrDefault()?.Score ?? 0);
    }
}
