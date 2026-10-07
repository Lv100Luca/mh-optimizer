using System.Diagnostics;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace WasmSpeedTest;

/// <summary>
/// CP-SAT in skill pair "optimize" mode: the classes one after another as Optimizer does today ("baseline"), versus
/// "two-phase" (each class's best first, top-N builds only for the classes shown) and "cutoff" (two-phase, and once enough
/// classes are known a class must beat the weakest of them or is proved infeasible).
/// </summary>
internal static class OptimizeExperiment
{
    /// <summary>Slack under the cutoff, in micro points, for the fixed-point rounding of the CP-SAT score.</summary>
    private const long Tolerance = 50_000;

    private sealed record Class(string Label, GogmaWeaponStats Weapon, int Specific);

    /// <param name="lanes">Classes solved at the same time (each with <paramref name="threads"/> workers).</param>
    public static void Run(GameData data, string weaponsDirectory, string config, int threads, string strategy, string parameters, int lanes = 1)
    {
        var r = Bench.Resolve(data, weaponsDirectory, new BenchCase(config, CpSat: true), threads);
        if (!r.IsValid) throw new InvalidOperationException(string.Join("; ", r.Errors));
        var other = Optimizer.OtherLabel;
        var baseRel = Relevance.Build(r.Weapon, r, data);
        var classes = r.SkillPairCandidates.Where(p => p is not null).Select(p => p!)
            .GroupBy(p => (Set: baseRel.SetIndex.ContainsKey(p.SetBonus) ? p.SetBonus : other,
                           Group: baseRel.GroupIndex.ContainsKey(p.GroupSkill) ? p.GroupSkill : other))
            .Select(g => new Class($"{g.Key.Set} + {g.Key.Group}",
                r.Weapon with
                {
                    SetBonus = g.Key.Set == other ? null : g.First().SetBonus,
                    GroupSkill = g.Key.Group == other ? null : g.First().GroupSkill,
                },
                (g.Key.Set == other ? 0 : 1) + (g.Key.Group == other ? 0 : 1)))
            .ToList();
        var shown = r.SkillPair.TopN;
        var topN = r.Options.TopN;
        Console.WriteLine($"{config}: {classes.Count} classes, {shown} shown with {topN} builds each, {threads} workers, strategy {strategy}, {lanes} lane(s) {parameters}");

        var total = Stopwatch.StartNew();
        var solves = 0;
        (double Best, IReadOnlyList<RankedBuild> Builds) Search(Class c, int n, long? cutoff, string phase)
        {
            var rel = Relevance.Build(c.Weapon, r, data);
            var armor = Candidates.Armor(data, rel, r.Options);
            var kinds = Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray();
            var search = new CpSatCutoffSearch(data, rel, c.Weapon, r.Conditions, new DecorationFiller(data, rel), armor, kinds,
                Candidates.Talismans(r.Talismans, rel), n, threads, r.Options.CpSatTimeLimitSeconds, null, CancellationToken.None)
            { Cutoff = cutoff, Parameters = parameters };
            var builds = search.Run();
            Interlocked.Add(ref solves, search.Solves);
            var best = builds.FirstOrDefault()?.Score ?? 0;
            Console.WriteLine($"  [{total.Elapsed.TotalSeconds,6:F1}s] {phase} {c.Label,-48} {search.Seconds,6:F2}s  {(builds.Count == 0 ? "cannot reach cutoff" : $"best {best:F2}")}  ({search.Summary})");
            return (best, builds);
        }

        List<(Class Class, double Best, IReadOnlyList<RankedBuild> Builds)> results;
        if (strategy == "baseline")
        {
            results = classes.Select(c => { var (b, builds) = Search(c, topN, null, "all"); return (c, b, builds); }).ToList();
        }
        else
        {
            // phase 1: each class's best build; with "cutoff", classes with specific set bonus / group skill go first and the
            // rest must beat the weakest of the best `shown` classes so far
            var order = strategy == "cutoff" ? classes.OrderByDescending(c => c.Specific).ToList() : classes;
            var bests = new List<(Class Class, double Best)>();
            var lanesOptions = new ParallelOptions { MaxDegreeOfParallelism = lanes };
            Parallel.ForEach(order, lanesOptions, c =>
            {
                long? cutoff = null;
                lock (bests)
                {
                    var kept = bests.Select(b => b.Best).OrderByDescending(b => b).ToList();
                    if (strategy == "cutoff" && kept.Count >= shown) cutoff = (long)Math.Floor(kept[shown - 1] * 1_000_000) - Tolerance;
                }
                var (best, _) = Search(c, 1, cutoff, "best");
                if (best > 0) lock (bests) bests.Add((c, best));
            });
            // phase 2: the full top-N for the classes that are shown
            var top = bests.OrderByDescending(b => b.Best).Take(shown).ToList();
            var full = new (Class Class, double Best, IReadOnlyList<RankedBuild> Builds)[top.Count];
            Parallel.For(0, top.Count, lanesOptions, i => { var (best, builds) = Search(top[i].Class, topN, null, "topN"); full[i] = (top[i].Class, best, builds); });
            results = [.. full];
        }

        Console.WriteLine($"{strategy}: {total.Elapsed.TotalSeconds:F1}s, {solves} solves");
        foreach (var (c, best, builds) in results.OrderByDescending(x => x.Best).Take(shown))
            Console.WriteLine($"  {c.Label,-48} {string.Join(" ", builds.Select(b => b.Score.ToString("F2")))}");
    }
}
