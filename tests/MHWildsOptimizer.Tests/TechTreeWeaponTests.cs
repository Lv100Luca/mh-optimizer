using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Tests;

public class TechTreeWeaponTests
{
    [Fact]
    public void DatasetHasEveryNonArtianWeapon()
    {
        var data = TestData.Data;
        Assert.Equal(1104, data.TechTreeWeapons.Count);
        Assert.DoesNotContain(data.TechTreeWeapons, w => w.Name.StartsWith("Artian", StringComparison.Ordinal));
        Assert.All(data.TechTreeWeapons.SelectMany(w => w.Skills), g => Assert.Equal(SkillKind.Weapon, data.Skill(g.Skill).Kind));
    }

    [Fact]
    public void ResolvesStatsAndSkillsFromTheDataset()
    {
        var data = TestData.Data;
        var input = new WeaponStatsInput { TechTree = "Wyvern Blade \"Maple\"", SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" };
        Assert.Empty(input.Validate(data));

        var w = input.ToStats(data);
        Assert.Equal(WeaponType.LongSword, w.Type);
        Assert.Equal(200, w.TrueRaw);
        Assert.Equal(10, w.Affinity);
        Assert.Equal(Element.Fire, w.Element);
        Assert.Equal(SharpnessColor.White, w.TopSharpness);
        Assert.Equal([3, 2, 1], w.Slots);
        Assert.Null(w.SetBonus);
        Assert.Null(w.GroupSkill);

        var skills = SkillAggregator.Aggregate(new Loadout { Weapon = new EquippedWeapon(w) }, data);
        Assert.Equal(3, skills.Level("Critical Boost"));
    }

    [Fact]
    public void UnknownWeaponIsAnError()
    {
        Assert.Contains(new WeaponStatsInput { TechTree = "No Such Blade" }.Validate(TestData.Data), e => e.Contains("No Such Blade"));
    }

    /// <summary>Darkside comes only from the Shadowbringer weapons: the target is met only when both engines count the weapon's skills.</summary>
    [Theory]
    [InlineData(OptimizerEngine.Beam)]
    [InlineData(OptimizerEngine.CpSat)]
    public void BothEnginesCountTheWeaponSkills(OptimizerEngine engine)
    {
        var data = TestData.Data;
        var request = RequestLoader.Resolve(new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { TechTree = "True Shadowbringer" },
            TargetSkills = new() { ["Darkside"] = 1, ["Focus"] = 3 },
            Options = new OptimizerOptions { MinRarity = 8, TopN = 3, Engine = engine },
        }, data, ".");
        Assert.True(request.IsValid, string.Join(" ", request.Errors));

        var result = new Optimizer(data, request).Run().PairResults[0];
        Assert.NotEmpty(result.Builds);
        foreach (var b in result.Builds)
        {
            Assert.Empty(b.Loadout.Validate(data));
            var skills = SkillAggregator.Aggregate(b.Loadout, data);
            Assert.Equal(1, skills.Level("Darkside"));
            Assert.True(skills.Level("Focus") >= 3);
        }
    }

    [Fact]
    public void SkillPairOptimizeModeIsRejected()
    {
        var request = RequestLoader.Resolve(new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { TechTree = "True Shadowbringer" },
            SkillPair = new SkillPairSettings { Mode = SkillPairMode.Optimize },
        }, TestData.Data, ".");
        Assert.Contains(request.Errors, e => e.Contains("tech tree"));
    }
}
