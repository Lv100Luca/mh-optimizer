using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

public class PossibleTalismanTests(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    private static OptimizationRequest Request(bool allPossible) => new()
    {
        Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10 },
        TargetSkills = new() { ["Weakness Exploit"] = 5 },
        Conditions = Conditions.AllOff with { HittingWeakPoint = true },
        Talismans = new TalismanSettings { IncludeCraftable = true, IncludeAllPossible = allPossible },
        Options = new OptimizerOptions { TopN = 1, MinRarity = 7, RequireWeaponCoreSkills = false },
    };

    private static List<Talisman> Generate(ResolvedRequest r) =>
        PossibleTalismans.Generate(TestData.Data.TalismanPool!, Relevance.Build(r.Weapon, r, TestData.Data), TestData.Data);

    [Fact]
    public void RollTableIsLoaded()
    {
        var pool = TestData.Data.TalismanPool!;
        Assert.Equal([4, 5, 6, 7], pool.PointRows.Select(r => r.Rarity).Distinct().Order());
        Assert.All(pool.PointRows, r => Assert.Equal(3, r.SkillPoints.Count));
        // Critical Boost only rolls as the 4-point first skill (rarity 6 and 7, with two 1-point weapon skills)
        Assert.Contains(pool.LotSkills, s => s.Skill == "Critical Boost" && s.Level == 1 && s.Points == 4);
        Assert.Contains(pool.LotSkills, s => s.Skill == "Weakness Exploit" && s.Level == 1 && s.Points == 10);
    }

    [Fact]
    public void OnlyRollableCombinationsAreGenerated()
    {
        var r = RequestLoader.Resolve(Request(true), TestData.Data, RepoRoot);
        var all = Generate(r);
        output.WriteLine($"{all.Count} possible talismans for {string.Join(", ", Relevance.Build(r.Weapon, r, TestData.Data).Skills)}");
        Assert.All(all, t => Assert.Equal(TalismanSource.Possible, t.Source));
        Assert.All(all, t => Assert.True(t.Skills.Count <= 3 && t.Skills.Select(s => s.Skill).Distinct().Count() == t.Skills.Count));

        bool Has(Talisman t, string skill, int level) => t.Skills.Any(s => s.Skill == skill && s.Level == level);
        // a 10-point skill always comes alone in the armor-kind slots: Weakness Exploit and Agitator never share a talisman
        Assert.DoesNotContain(all, t => Has(t, "Weakness Exploit", 1) && Has(t, "Agitator", 1));
        // Attack Boost 3 + Weakness Exploit 1 rolls at rarity 6 (up to a level-2 armor slot) and rarity 7 (weapon slot + armor slots)
        var abWex = all.Where(t => Has(t, "Attack Boost", 3) && Has(t, "Weakness Exploit", 1)).ToList();
        Assert.Contains(abWex, t => t.Rarity == 6 && t.Slots.SequenceEqual([new TalismanSlot(2, SkillKind.Armor)]));
        Assert.Contains(abWex, t => t.Rarity == 7 && t.Slots.Any(s => s.Kind == SkillKind.Weapon));
        Assert.DoesNotContain(abWex, t => t.Slots.Any(s => s.Level == 3));
        // every generated talisman passes the same validation as a user-entered one
        foreach (var t in all.Take(200))
        {
            var c = TalismanInputLoader.Convert(new TalismanInput
            {
                Name = t.Name, Rarity = t.Rarity, Skills = t.Skills.ToDictionary(s => s.Skill, s => s.Level), Slots = t.Slots.Select(s => s.ToString()).ToList(),
            }, TestData.Data);
            Assert.Empty(c.Errors);
            Assert.Empty(c.Warnings);
        }
    }

    [Fact]
    public void EveryPossibleTalismanScoresAtLeastTheCraftableCharms()
    {
        var data = TestData.Data;
        var owned = RequestLoader.Resolve(Request(false), data, RepoRoot);
        var possible = RequestLoader.Resolve(Request(true), data, RepoRoot);
        Assert.True(possible.IsValid, string.Join("; ", possible.Errors));
        Assert.True(possible.AllPossibleTalismans);

        var ownedBest = Assert.Single(new Optimizer(data, owned).Run().PairResults[0].Builds);
        var possibleBest = Assert.Single(new Optimizer(data, possible).Run(new Progress<string>(output.WriteLine)).PairResults[0].Builds);
        output.WriteLine($"craftable only {ownedBest.Score:0.00} ({ownedBest.Loadout.Talisman?.Talisman.Name}), every possible {possibleBest.Score:0.00} ({possibleBest.Loadout.Talisman?.Talisman.Name})");
        Assert.True(possibleBest.Score >= ownedBest.Score - 1e-6);
        Assert.Empty(possibleBest.Loadout.Validate(data));

        // CP-SAT takes the roll table instead of the spelled-out talismans the beam search used: same optimum
        var cp = RequestLoader.Resolve(Request(true) with { Options = Request(true).Options with { Engine = OptimizerEngine.CpSat } }, data, RepoRoot);
        var cpBest = Assert.Single(new Optimizer(data, cp).Run(new Progress<string>(output.WriteLine)).PairResults[0].Builds);
        output.WriteLine($"cp-sat {cpBest.Score:0.00} ({cpBest.Loadout.Talisman?.Talisman.Name})");
        Assert.Equal(possibleBest.Score, cpBest.Score, 3);
        Assert.Equal(TalismanSource.Possible, cpBest.Loadout.Talisman?.Talisman.Source);
        Assert.Empty(cpBest.Loadout.Validate(data));

        // a build with a possible talisman survives the round trip through a saved build
        var input = BuildInput.FromLoadout("theory", possibleBest.Loadout);
        var back = BuildInputLoader.Convert(input, possible.Weapon, [], data);
        Assert.Empty(back.Errors);
        Assert.Equal(possibleBest.Loadout.Talisman?.Talisman.Skills, back.Loadout.Talisman?.Talisman.Skills);
    }
}
