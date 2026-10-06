using System.Diagnostics;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

public class OptimizerTests(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    [Fact]
    public void ExampleRequestFindsBuildsThatMeetEveryTarget()
    {
        var data = TestData.Data;
        var request = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), data);
        Assert.True(request.IsValid);

        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, request).Run(new Progress<string>(output.WriteLine));
        sw.Stop();
        output.WriteLine($"{sw.Elapsed.TotalSeconds:0.0} s, {result.PairResults[0].StatesEvaluated} combos");

        var pairResult = Assert.Single(result.PairResults);
        Assert.NotEmpty(pairResult.Builds);
        Assert.True(pairResult.Builds.Count <= request.Options.TopN);

        var scores = pairResult.Builds.Select(b => b.Score).ToList();
        Assert.Equal(scores.OrderByDescending(s => s), scores);

        foreach (var build in pairResult.Builds)
        {
            Assert.Empty(build.Loadout.Validate(data));
            var skills = SkillAggregator.Aggregate(build.Loadout, data);
            foreach (var (skill, level) in request.TargetSkills)
                Assert.True(skills.Level(skill) >= level, $"{skill} {skills.Level(skill)} < {level} in {build.Loadout.ArmorPieces.Select(a => a.Piece.Name).Aggregate((a, b) => a + ", " + b)}");
            Assert.Equal(5, build.Loadout.ArmorPieces.Count());
            Assert.Equal("Soul of the Dark Knight + Lord's Favor", build.SkillPairLabel);
            output.WriteLine(LoadoutReport.Render(build.Loadout, data, request.Conditions, $"{build.Score:0.0}"));
        }
        Assert.True(sw.Elapsed < TimeSpan.FromMinutes(2), "search too slow");
    }

    [Fact]
    public void OptimizeModeRanksSkillPairClasses()
    {
        var data = TestData.Data;
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10 },
            SkillPair = new SkillPairSettings { Mode = SkillPairMode.Optimize, TopN = 3 },
            TargetSkills = new() { ["Weakness Exploit"] = 5, ["Critical Boost"] = 5 },
            Conditions = Conditions.AllOff with { HittingWeakPoint = true, MonsterEnraged = true, GutsNotYetTriggered = true, FrenzyOvercome = true },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 2, MinRarity = 7, RequireWeaponCoreSkills = false },
        };
        var resolved = RequestLoader.Resolve(request, data, RepoRoot);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));

        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, resolved).Run(new Progress<string>(output.WriteLine));
        output.WriteLine($"{sw.Elapsed.TotalSeconds:0.0} s");

        Assert.Equal(3, result.PairResults.Count);
        Assert.True(result.PairResults[0].BestScore >= result.PairResults[1].BestScore);
        foreach (var pr in result.PairResults)
        {
            output.WriteLine($"{pr.Label}: {pr.BestScore:0.0} ({pr.StatesEvaluated} states)");
            Assert.NotEmpty(pr.Builds);
            foreach (var b in pr.Builds)
            {
                Assert.Empty(b.Loadout.Validate(data));
                var skills = SkillAggregator.Aggregate(b.Loadout, data);
                Assert.True(skills.Level("Weakness Exploit") >= 5);
                Assert.True(skills.Level("Critical Boost") >= 5);
            }
        }
        // Gore Magala's Tyranny + Lord's Soul gives affinity and a raw multiplier, so it should be among the top classes
        Assert.Contains(result.PairResults, pr => pr.Label.Contains("Gore Magala's Tyranny"));
    }

    [Fact]
    public void ImpossibleTargetsYieldNoBuilds()
    {
        var data = TestData.Data;
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000 },
            TargetSkills = new() { ["Weakness Exploit"] = 5, ["Agitator"] = 5, ["Burst"] = 5, ["Maximum Might"] = 3, ["Peak Performance"] = 5, ["Latent Power"] = 5, ["Adrenaline Rush"] = 5, ["Counterstrike"] = 3, ["Foray"] = 5 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
            Options = new OptimizerOptions { TopN = 1, RequireWeaponCoreSkills = false },
        };
        var resolved = RequestLoader.Resolve(request, data, RepoRoot);
        var result = new Optimizer(data, resolved).Run();
        Assert.Empty(result.PairResults[0].Builds);
    }

    [Fact]
    public void DominancePruningKeepsTheBestPieces()
    {
        var data = TestData.Data;
        var resolved = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), data);
        var rel = Relevance.Build(resolved.Weapon, resolved.TargetSkills, resolved.Conditions, data);
        var armor = Candidates.Armor(data, rel, resolved.Options);
        foreach (var (kind, list) in armor)
        {
            Assert.NotEmpty(list);
            Assert.True(list.Count < data.ArmorByKind[kind].Count, $"{kind} not pruned");
            output.WriteLine($"{kind}: {list.Count} of {data.ArmorByKind[kind].Count}");
        }
        // a Gore piece carries the weapon-relevant frenzy set and Antivirus, it must survive
        Assert.Contains(armor[ArmorPieceKind.Chest], c => c.Piece.Set == "Gore α");
    }
}
