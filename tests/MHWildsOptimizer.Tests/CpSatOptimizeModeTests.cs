using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

public class CpSatOptimizeModeTests(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    private static OptimizationRequest LongSwordRequest(SkillPairMode mode, int threads) => new()
    {
        Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10 },
        SkillPair = new SkillPairSettings { Mode = mode, TopN = 3 },
        TargetSkills = new() { ["Weakness Exploit"] = 5, ["Critical Boost"] = 5 },
        Conditions = Conditions.AllOff with { HittingWeakPoint = true, MonsterEnraged = true, GutsNotYetTriggered = true, FrenzyOvercome = true },
        Talismans = new TalismanSettings { IncludeCraftable = true },
        Options = new OptimizerOptions { TopN = 2, MinRarity = 7, RequireWeaponCoreSkills = false, Engine = OptimizerEngine.CpSat, MaxThreads = threads },
    };

    [Fact]
    public void ParametersNameTheBoundingSubsolversForFewWorkers()
    {
        var one = CpSatParameters.For(1, 60);
        Assert.Equal(1, one.Workers);
        Assert.Equal(2, one.LinearizationLevel);
        Assert.Empty(one.Subsolvers);

        Assert.Equal(["max_lp", "reduced_costs"], CpSatParameters.For(2, 60).Subsolvers);
        var many = CpSatParameters.For(16, 5);
        Assert.Equal(CpSatParameters.MaxWorkersPerSolve, many.Workers);
        Assert.Contains("max_lp", many.Subsolvers);
        Assert.Contains("reduced_costs", many.Subsolvers);
        Assert.Equal("num_workers:4, max_time_in_seconds:5, subsolvers:\"max_lp\", subsolvers:\"reduced_costs\", subsolvers:\"pseudo_costs\", subsolvers:\"quick_restart\"", many.ToText());
        Assert.Equal("num_workers:1, max_time_in_seconds:0.5, linearization_level:2", CpSatParameters.For(0, 0.5).ToText());
    }

    [Fact]
    public void CutoffProvesThatNoBuildReachesAHigherScore()
    {
        var data = TestData.Data;
        var resolved = RequestLoader.Resolve(LongSwordRequest(SkillPairMode.Fixed, 4) with
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10, SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" },
        }, data, RepoRoot);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        var rel = Relevance.Build(resolved.Weapon, resolved, data);
        var armor = Candidates.Armor(data, rel, resolved.Options);
        var kinds = Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray();
        CpSatSearch Search(long? cutoff) => new(data, rel, resolved.Weapon, resolved.Conditions, new DecorationFiller(data, rel), armor, kinds,
            Candidates.Talismans(resolved.Talismans, rel), 1, 4, 60, null, CancellationToken.None) { Cutoff = cutoff };

        var best = Assert.Single(Search(null).Run()).Score;
        Assert.Equal(best, Assert.Single(Search(CpSatSearch.CutoffFor(best)).Run()).Score, 6);
        var above = Search(CpSatSearch.CutoffFor(best + 1));
        Assert.Empty(above.Run());
        output.WriteLine($"best {best:0.000}; above it: {above.Summary}");
    }

    /// <summary>Two-phase with cutoffs must return what searching every class in full returns: the same classes and builds.</summary>
    [Fact]
    public void OptimizeModeMatchesSearchingEveryClassInFull()
    {
        var data = TestData.Data;
        var optimize = RequestLoader.Resolve(LongSwordRequest(SkillPairMode.Optimize, 4), data, RepoRoot);
        Assert.True(optimize.IsValid, string.Join("; ", optimize.Errors));

        // reference: every class as its own fixed-pair search, grouped the way the optimizer groups them
        var baseRel = Relevance.Build(optimize.Weapon, optimize, data);
        var reference = optimize.SkillPairCandidates.Where(p => p is not null).Select(p => p!)
            .GroupBy(p => (Set: baseRel.SetIndex.ContainsKey(p.SetBonus) ? p.SetBonus : null, Group: baseRel.GroupIndex.ContainsKey(p.GroupSkill) ? p.GroupSkill : null))
            .Select(cls =>
            {
                var request = LongSwordRequest(SkillPairMode.Fixed, 4) with
                {
                    Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10, SetBonus = cls.Key.Set, GroupSkill = cls.Key.Group },
                };
                var builds = new Optimizer(data, RequestLoader.Resolve(request, data, RepoRoot)).Run().PairResults.Single().Builds;
                return (Key: $"{cls.Key.Set ?? Optimizer.OtherLabel} + {cls.Key.Group ?? Optimizer.OtherLabel}", Scores: builds.Select(b => b.Score).ToList());
            })
            .Where(c => c.Scores.Count > 0)
            .OrderByDescending(c => c.Scores[0])
            .ToList();
        foreach (var c in reference) output.WriteLine($"reference {c.Key}: {string.Join(" ", c.Scores.Select(s => s.ToString("0.000")))}");

        foreach (var threads in new[] { 4, 16 })
        {
            var result = new Optimizer(data, RequestLoader.Resolve(LongSwordRequest(SkillPairMode.Optimize, threads), data, RepoRoot))
                .Run(new Progress<string>(output.WriteLine));
            Assert.Equal(3, result.PairResults.Count);
            for (var i = 0; i < 3; i++)
            {
                var got = result.PairResults[i];
                Assert.Equal(reference[i].Scores[0], got.BestScore, 6);
                var match = reference.Single(c => c.Key == got.Label);
                Assert.Equal(match.Scores.Count, got.Builds.Count);
                for (var b = 0; b < got.Builds.Count; b++) Assert.Equal(match.Scores[b], got.Builds[b].Score, 6);
            }
        }
    }
}
