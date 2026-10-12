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

public class WeaponTuningTests(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    [Fact]
    public void ReinforcementSetsTakeTheBestTiersOfEachType()
    {
        var sets = WeaponSpace.ReinforcementSets(WeaponType.GreatSword, Element.Fire, ReinforcementTier.EX).ToList();
        // attack / affinity / element / sharpness over 5 lines, sharpness at most 2: 21 + 15 + 10
        Assert.Equal(46, sets.Count);
        Assert.All(sets, s => Assert.Equal(5, s.Count));
        Assert.All(sets, s => Assert.All(s.GroupBy(r => r), g => Assert.True(g.Count() <= 2)));

        var allAttack = Assert.Single(sets, s => s.All(r => r.Type == ReinforcementType.Attack));
        Assert.Equal([ReinforcementTier.EX, ReinforcementTier.EX, ReinforcementTier.III, ReinforcementTier.III, ReinforcementTier.II], allAttack.Select(r => r.Tier));

        // a realistic cap: no EX, sharpness only at tier I, no 5x element (II, II, I, I only)
        var capped = WeaponSpace.ReinforcementSets(WeaponType.GreatSword, Element.Fire, ReinforcementTier.III).ToList();
        Assert.DoesNotContain(capped, s => s.Any(r => r.Tier == ReinforcementTier.EX));
        Assert.DoesNotContain(capped, s => s.Count(r => r.Type == ReinforcementType.Element) > 4);

        // bows: no sharpness, no element without an element
        var bow = WeaponSpace.ReinforcementSets(WeaponType.Bow, Element.None, ReinforcementTier.EX).ToList();
        Assert.Equal(6, bow.Count);
        Assert.All(bow, s => Assert.All(s, r => Assert.True(r.Type is ReinforcementType.Attack or ReinforcementType.Affinity)));
    }

    [Fact]
    public void FixedPartsStayAsTheSpecHasThem()
    {
        var data = TestData.Data;
        var spec = new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity, Element = Element.Ice, AttackParts = 2 };
        var space = new WeaponSpace(spec, new WeaponOptimizeSettings { Reinforcements = true }, data, [3, 3, 3]);
        Assert.Equal(46, space.Variants.Count);
        Assert.All(space.Variants, v =>
        {
            Assert.Equal(GogmaFocus.Affinity, v.Focus);
            Assert.Equal(Element.Ice, v.Element);
            Assert.Equal(2, v.Spec!.AttackParts);
        });

        var all = new WeaponSpace(spec, new WeaponOptimizeSettings { Focus = true, Element = true, AttackParts = true, Reinforcements = true }, data, [3, 3, 3]);
        Assert.Equal(3 * 5 * 4 * 46, all.Variants.Count);
        Assert.Equal(all.Variants.Count, all.Variants.Select(v => v.Spec!.Key).Distinct().Count());
    }

    [Fact]
    public void TuningTheWeaponBeatsTheFixedWeapon()
    {
        var data = TestData.Data;
        var request = RequestLoader.Read(Path.Combine(RepoRoot, "inputs", "request.example.json"));
        request = request with { Options = request.Options with { MaxStatesPerDepth = 20_000, TopN = 3 } };
        var fixedResolved = RequestLoader.Resolve(request, data, Path.Combine(RepoRoot, "inputs"));
        Assert.True(fixedResolved.IsValid, string.Join("; ", fixedResolved.Errors));
        var fixedBest = new Optimizer(data, fixedResolved).Run().AllBuilds.First();

        var tunedRequest = request with { WeaponOptimize = new WeaponOptimizeSettings { Focus = true, Element = true, AttackParts = true, Reinforcements = true } };
        var resolved = RequestLoader.Resolve(tunedRequest, data, Path.Combine(RepoRoot, "inputs"));
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        Assert.NotNull(resolved.WeaponSpace);

        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, resolved).Run(new Progress<string>(output.WriteLine));
        output.WriteLine($"{sw.Elapsed.TotalSeconds:0.0} s");

        var builds = Assert.Single(result.PairResults).Builds;
        Assert.NotEmpty(builds);
        Assert.True(builds[0].Score >= fixedBest.Score, $"tuned {builds[0].Score} < fixed {fixedBest.Score}");
        foreach (var b in builds)
        {
            Assert.Empty(b.Loadout.Validate(data));
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            foreach (var (skill, level) in resolved.TargetSkills)
                Assert.True(skills.Level(skill) >= level);
            Assert.NotNull(b.Loadout.Weapon.Stats.Spec);
            Assert.Equal("Soul of the Dark Knight", b.Loadout.Weapon.Stats.SetBonus);
            // the chosen weapon is the best of every part's options on this armor
            Assert.NotNull(b.WeaponOptions);
            foreach (var part in b.WeaponOptions!.GroupBy(o => o.Part))
            {
                Assert.True(part.Single(o => o.Chosen).Score >= part.Max(o => o.Score) - 1e-9, part.Key);
                Assert.Equal(b.Score, part.Max(o => o.Score), 6);
            }
            output.WriteLine($"{b.Score:0.00}  {b.Loadout.Weapon.Stats.Spec!.Label()}");
        }
        output.WriteLine($"fixed weapon best {fixedBest.Score:0.00}");
    }

    [Theory]
    [InlineData(SkillPairMode.Fixed)]
    [InlineData(SkillPairMode.Optimize)]
    public void CpSatTunesTheWeaponLikeTheBeam(SkillPairMode mode)
    {
        var data = TestData.Data;
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput
            {
                Spec = new GogmaWeaponSpecInput { Type = "long-sword", Focus = GogmaFocus.Attack, Element = Element.Fire, AttackParts = 3 },
                SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul",
            },
            WeaponOptimize = new WeaponOptimizeSettings { AttackParts = true, Reinforcements = true },
            SkillPair = new SkillPairSettings { Mode = mode, TopN = 2 },
            TargetSkills = new() { ["Weakness Exploit"] = 5 },
            Conditions = Conditions.AllOff with { HittingWeakPoint = true, MonsterEnraged = true, GutsNotYetTriggered = true, FrenzyOvercome = true },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 2, MinRarity = 7, RequireWeaponCoreSkills = false, MaxStatesPerDepth = 20_000 },
        };
        OptimizationResult Run(OptimizerEngine engine)
        {
            var resolved = RequestLoader.Resolve(request with { Options = request.Options with { Engine = engine } }, data, RepoRoot);
            Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
            return new Optimizer(data, resolved).Run(new Progress<string>(output.WriteLine));
        }

        var cp = Run(OptimizerEngine.CpSat);
        var beam = Run(OptimizerEngine.Beam);
        Assert.Equal(mode == SkillPairMode.Fixed ? 1 : 2, cp.PairResults.Count);
        Assert.All(cp.PairResults, pr => Assert.Equal(2, pr.Builds.Count));
        Assert.All(cp.AllBuilds, b => Assert.NotNull(b.WeaponOptions));
        // CP-SAT proves each search optimal; the beam may only match it
        Assert.True(cp.AllBuilds.First().Score >= beam.AllBuilds.First().Score - 1e-6, $"CP-SAT {cp.AllBuilds.First().Score} < beam {beam.AllBuilds.First().Score}");
        output.WriteLine($"CP-SAT {cp.AllBuilds.First().Score:0.00} with {cp.AllBuilds.First().Loadout.Weapon.Stats.Spec!.Label()}; beam {beam.AllBuilds.First().Score:0.00}");
    }

    [Fact]
    public void OptimizingNeedsTheWeaponDescribed()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660 },
            WeaponOptimize = new WeaponOptimizeSettings { Reinforcements = true },
        };
        var resolved = RequestLoader.Resolve(request, TestData.Data, RepoRoot);
        Assert.Contains(resolved.Errors, e => e.Contains("optimizing the weapon"));
    }
}
