using System.Diagnostics;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
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
    public void SkillLimitsKeepTheSearchFromInvestingInCappedSkills()
    {
        var data = TestData.Data;
        var request = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), data);
        Assert.Equal(1, request.Conditions.SkillLimits["Burst"]); // the example caps Burst at 1 for Great Sword
        var result = new Optimizer(data, request).Run();
        var builds = result.PairResults[0].Builds;
        Assert.NotEmpty(builds);
        foreach (var b in builds)
        {
            var decos = b.Loadout.ArmorPieces.SelectMany(a => a.Decos).Concat(b.Loadout.Weapon.Decos).Concat(b.Loadout.Talisman?.Decos ?? [])
                .Where(d => d is not null).Select(d => d!.Name).ToList();
            Assert.True(SkillAggregator.Aggregate(b.Loadout, data).RawLevels.GetValueOrDefault("Burst") <= 1, $"Burst invested past its cap: {string.Join(", ", decos)}");
            var summary = BuildSummary.Create(b.Loadout, data, request.Conditions);
            var burst = summary.Skills.FirstOrDefault(s => s.Skill == "Burst");
            if (burst.Skill is not null) Assert.True(burst.Effective <= 1);
        }
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
    public void RequiredSetBonusesAndGroupSkillsAreInEveryBuild()
    {
        var data = TestData.Data;
        // neither Uth Duna's Cover nor Scaling Prowess does anything for the score, so only the requirement can put them in
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10 },
            TargetSkills = new() { ["Uth Duna's Cover"] = 1, ["Scaling Prowess"] = 1, ["Weakness Exploit"] = 3 },
            Conditions = Conditions.AllOff with { HittingWeakPoint = true },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 3, MinRarity = 7, RequireWeaponCoreSkills = false },
        };
        var resolved = RequestLoader.Resolve(request, data, RepoRoot);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        Assert.Equal(2, resolved.TargetSetBonuses["Uth Duna's Cover"]);
        Assert.Equal(3, resolved.TargetGroupSkills["Scaling Prowess"]);

        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, resolved).Run(new Progress<string>(output.WriteLine));
        output.WriteLine($"{sw.Elapsed.TotalSeconds:0.0} s");
        var builds = Assert.Single(result.PairResults).Builds;
        Assert.NotEmpty(builds);
        foreach (var b in builds)
        {
            Assert.Empty(b.Loadout.Validate(data));
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            Assert.NotEqual(SetBonusTier.None, skills.SetTier("Uth Duna's Cover"));
            Assert.True(skills.GroupActive("Scaling Prowess"));
            Assert.True(skills.Level("Weakness Exploit") >= 3);
            output.WriteLine(LoadoutReport.Render(b.Loadout, data, resolved.Conditions, $"{b.Score:0.0}"));
        }

        // tier II needs four pieces; four Nu Udra pieces leave no room for three Scaling Prowess pieces, so nothing qualifies
        var conflicting = RequestLoader.Resolve(request with { TargetSkills = new() { ["Nu Udra's Mutiny"] = 2, ["Scaling Prowess"] = 1 } }, data, RepoRoot);
        Assert.True(conflicting.IsValid, string.Join("; ", conflicting.Errors));
        Assert.Empty(new Optimizer(data, conflicting).Run().PairResults[0].Builds);

        // the weapon's rolled set bonus and group skill each count as a piece: three Nu Udra pieces plus two Scaling Prowess pieces fit
        var viaWeapon = RequestLoader.Resolve(request with
        {
            Weapon = request.Weapon with { SetBonus = "Nu Udra's Mutiny", GroupSkill = "Scaling Prowess" },
            TargetSkills = new() { ["Nu Udra's Mutiny"] = 2, ["Scaling Prowess"] = 1 },
        }, data, RepoRoot);
        Assert.True(viaWeapon.IsValid, string.Join("; ", viaWeapon.Errors));
        var withWeapon = new Optimizer(data, viaWeapon).Run().PairResults[0].Builds;
        Assert.NotEmpty(withWeapon);
        foreach (var b in withWeapon)
        {
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            Assert.Equal(SetBonusTier.II, skills.SetTier("Nu Udra's Mutiny"));
            Assert.True(skills.GroupActive("Scaling Prowess"));
            Assert.Equal(3, b.Loadout.ArmorPieces.Count(a => a.Piece.SetBonus.Contains("Nu Udra's Mutiny")));
        }
    }

    [Fact]
    public void ProcDamageMakesItsSetsAndThunderAttackRelevant()
    {
        var data = TestData.Data;
        var rawGs = new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }.Resolve(data);
        var targets = new Dictionary<string, int>();

        var on = Relevance.Build(rawGs, targets, Conditions.Default, data);
        Assert.Contains("Soul of the Dark Knight", on.SetBonuses); // shockwave helps a raw Great Sword
        Assert.Contains("Rathalos's Flare", on.SetBonuses);
        Assert.Contains("Leviathan's Fury", on.SetBonuses);
        Assert.Contains("Thunder Attack", on.Skills);
        Assert.DoesNotContain("Nu Udra's Mutiny", on.SetBonuses); // Bad Blood needs red health

        var off = Relevance.Build(rawGs, targets, Conditions.Default with { ProcDamage = false, AzureBoltActive = false }, data);
        Assert.DoesNotContain("Soul of the Dark Knight", off.SetBonuses);
        Assert.DoesNotContain("Rathalos's Flare", off.SetBonuses);
        Assert.DoesNotContain("Leviathan's Fury", off.SetBonuses);
        Assert.DoesNotContain("Thunder Attack", off.Skills);
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
