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
    public void ResultsDoNotDependOnTheThreadCount()
    {
        var data = TestData.Data;
        var request = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), data);
        Assert.True(request.IsValid);
        request = request with { Options = request.Options with { MaxStatesPerDepth = 20_000 } };

        string Fingerprint(int threads)
        {
            var result = new Optimizer(data, request with { Options = request.Options with { MaxThreads = threads } }).Run();
            return string.Join("\n", result.AllBuilds.Select(b =>
                $"{b.Score:R} {string.Join(",", b.Loadout.ArmorPieces.Select(a => a.Piece.Id))} {b.Loadout.Talisman?.Talisman.Name} " +
                string.Join(",", (b.Loadout.Weapon.Decorations ?? []).Concat(b.Loadout.ArmorPieces.SelectMany(a => a.Decorations ?? [])).Select(d => d?.Id ?? 0))));
        }

        var single = Fingerprint(1);
        output.WriteLine(single);
        Assert.NotEmpty(single);
        Assert.Equal(single, Fingerprint(Environment.ProcessorCount));
        Assert.Equal(single, Fingerprint(3));
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
            // armor may carry Burst past the cap as a side effect of its other skills; decorations must not add any above it
            var decos = b.Loadout.ArmorPieces.SelectMany(a => a.Decos).Concat(b.Loadout.Weapon.Decos).Concat(b.Loadout.Talisman?.Decos ?? [])
                .Where(d => d is not null).Select(d => d!).ToList();
            var burstFromDecos = decos.SelectMany(d => d.Skills).Where(g => g.Skill == "Burst").Sum(g => g.Level);
            var total = SkillAggregator.Aggregate(b.Loadout, data).RawLevels.GetValueOrDefault("Burst");
            Assert.True(burstFromDecos == 0 || total <= 1, $"Burst invested past its cap: {string.Join(", ", decos.Select(d => d.Name))}");
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
    public void ProcDamageMakesOnlyTheCountedProcSetsRelevant()
    {
        var data = TestData.Data;
        var rawGs = new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }.Resolve(data);
        var targets = new Dictionary<string, int>();

        var on = Relevance.Build(rawGs, targets, Conditions.Default, data);
        Assert.Contains("Soul of the Dark Knight", on.SetBonuses); // shockwave helps a raw Great Sword
        Assert.Contains("Leviathan's Fury", on.SetBonuses);        // through its affinity window only
        Assert.DoesNotContain("Rathalos's Flare", on.SetBonuses);  // Scorcher is not counted
        Assert.DoesNotContain("Thunder Attack", on.Skills);        // neither is the Azure Bolt burst it scales
        Assert.DoesNotContain("Nu Udra's Mutiny", on.SetBonuses); // Bad Blood needs red health

        var noWindow = Relevance.Build(rawGs, targets, Conditions.Default with { AzureBoltActive = false }, data);
        Assert.DoesNotContain("Leviathan's Fury", noWindow.SetBonuses);

        var off = Relevance.Build(rawGs, targets, Conditions.Default with { DarkArtsShockwave = false, BadBlood = false, AzureBoltActive = false }, data);
        Assert.DoesNotContain("Soul of the Dark Knight", off.SetBonuses);
    }

    public static TheoryData<string> ScoreModelCases => new()
    {
        "default", "all on", "all off", "red health", "long sword raw",
        "gs charge combo, hard part", "sa element phial, monster", "sa power phial, amped combo",
    };

    [Theory]
    [MemberData(nameof(ScoreModelCases))]
    public void CpSatScoreModelMatchesTheDamageCalculator(string name)
    {
        var data = TestData.Data;
        var dragonGs = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, Element = Element.Dragon, Infused = true, AttackParts = 3,
        }.Resolve(data);
        var rawLs = new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity }.Resolve(data);
        var thunderSa = new GogmaWeaponSpec { Type = WeaponType.SwitchAxe, Focus = GogmaFocus.Element, Element = Element.Thunder }.Resolve(data);
        var powerSa = new GogmaWeaponSpec { Type = WeaponType.SwitchAxe, Focus = GogmaFocus.Attack, Element = Element.Ice }.Resolve(data);
        var reyDau = data.Monsters.First(m => m.Name == "Rey Dau");
        var (weapon, cond) = name switch
        {
            "gs charge combo, hard part" => (dragonGs, Conditions.AllOn with { AttackProfile = new AttackProfile { Attack = "charge-combo" }, Target = Target.Dummy("Hard part") }),
            "sa element phial, monster" => (thunderSa, Conditions.AllOn with { Target = Target.ForMonster(reyDau, reyDau.Parts[0], WeaponType.SwitchAxe) }),
            "sa power phial, amped combo" => (powerSa, Conditions.Default with { AttackProfile = new AttackProfile { Attack = "amped-sword-combo" } }),
            "default" => (dragonGs, Conditions.Default),
            "all on" => (dragonGs, Conditions.AllOn),
            "all off" => (dragonGs, Conditions.AllOff),
            "red health" => (dragonGs, Conditions.Default with { RedHealth = true, CounterstrikeActive = true, PowerhouseActive = true }),
            _ => (rawLs, Conditions.AllOn),
        };
        var targets = new Dictionary<string, int> { ["Weakness Exploit"] = 3 };
        var rel = Relevance.Build(weapon, targets, cond, data);
        var score = ScoreDecomposition.Build(weapon, rel, cond);
        output.WriteLine($"{score.Units.Count} units, joint: {string.Join("; ", score.Units.Where(u => u.Features.Length > 1).Select(u => string.Join(" + ", u.Features.Select(f => score.Features[f].Name))))}");
        Assert.True(score.Verify(3000) < 1e-6);
    }

    /// <summary>Skill pair classes differ only in the weapon's set bonus and group skill: they share one score model.</summary>
    [Fact]
    public void ScoreModelIsSharedByWeaponsThatDifferOnlyInTheirSkillPair()
    {
        var data = TestData.Data;
        var weapon = new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity }.Resolve(data);
        var cond = Conditions.AllOn;
        var targets = new Dictionary<string, int> { ["Weakness Exploit"] = 3 };
        var cache = new ScoreDecompositionCache();
        var builds = 0;
        ScoreDecomposition Get(GogmaWeaponStats w, Relevance rel) =>
            cache.GetOrBuild(w, rel, cond, () => { builds++; return ScoreDecomposition.Build(w, rel, cond); }, out _);

        var withPair = weapon with { SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" };
        var relPair = Relevance.Build(withPair, targets, cond, data);
        var plain = Relevance.Build(weapon, targets, cond, data);
        Assert.Equal(ScoreDecomposition.FeaturesOf(plain), ScoreDecomposition.FeaturesOf(relPair));

        var first = Get(withPair, relPair);
        Assert.Same(first, Get(weapon, plain));
        Assert.Equal(1, builds);
        Assert.True(first.Verify(500) < 1e-6);

        // other features, other model
        var other = Relevance.Build(weapon, new Dictionary<string, int> { ["Weakness Exploit"] = 3, ["Maximum Might"] = 3 }, cond, data);
        Assert.NotSame(first, Get(weapon, other));
        Assert.Equal(2, builds);
    }

    [Fact]
    public void CpSatFindsBuildsAtLeastAsGoodAsTheBeam()
    {
        var data = TestData.Data;
        var request = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), data);
        request = request with { Options = request.Options with { MinRarity = 7, TopN = 3 } };
        var beam = new Optimizer(data, request).Run().PairResults[0];
        var cp = new Optimizer(data, request with { Options = request.Options with { Engine = OptimizerEngine.CpSat } }).Run(new Progress<string>(output.WriteLine)).PairResults[0];
        output.WriteLine($"beam {beam.BestScore:0.000}, cp-sat {cp.BestScore:0.000} ({cp.CandidateSummary})");

        Assert.NotEmpty(cp.Builds);
        Assert.True(cp.BestScore >= beam.BestScore - 1e-6, $"cp-sat {cp.BestScore} < beam {beam.BestScore}");
        Assert.Equal(cp.Builds.Select(b => b.Score).OrderByDescending(s => s), cp.Builds.Select(b => b.Score));
        foreach (var b in cp.Builds)
        {
            Assert.Empty(b.Loadout.Validate(data));
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            foreach (var (skill, level) in request.TargetSkills)
                Assert.True(skills.Level(skill) >= level, $"{skill} {skills.Level(skill)} < {level}");
        }
    }

    [Fact]
    public void CpSatHonoursRequiredSetBonusesAndReportsInfeasibleTargets()
    {
        var data = TestData.Data;
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10, SetBonus = "Nu Udra's Mutiny", GroupSkill = "Scaling Prowess" },
            TargetSkills = new() { ["Nu Udra's Mutiny"] = 2, ["Scaling Prowess"] = 1, ["Weakness Exploit"] = 3 },
            Conditions = Conditions.AllOff with { HittingWeakPoint = true },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 2, MinRarity = 7, RequireWeaponCoreSkills = false, Engine = OptimizerEngine.CpSat },
        };
        var resolved = RequestLoader.Resolve(request, data, RepoRoot);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        var builds = new Optimizer(data, resolved).Run().PairResults[0].Builds;
        Assert.NotEmpty(builds);
        foreach (var b in builds)
        {
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            Assert.Equal(SetBonusTier.II, skills.SetTier("Nu Udra's Mutiny"));
            Assert.True(skills.GroupActive("Scaling Prowess"));
            Assert.True(skills.Level("Weakness Exploit") >= 3);
        }

        var impossible = RequestLoader.Resolve(new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000 },
            TargetSkills = new() { ["Weakness Exploit"] = 5, ["Agitator"] = 5, ["Burst"] = 5, ["Maximum Might"] = 3, ["Peak Performance"] = 5, ["Latent Power"] = 5, ["Adrenaline Rush"] = 5, ["Counterstrike"] = 3, ["Foray"] = 5 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
            Options = new OptimizerOptions { TopN = 1, RequireWeaponCoreSkills = false, Engine = OptimizerEngine.CpSat },
        }, data, RepoRoot);
        Assert.Empty(new Optimizer(data, impossible).Run().PairResults[0].Builds);
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
