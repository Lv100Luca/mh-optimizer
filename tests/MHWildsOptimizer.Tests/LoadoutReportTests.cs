using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

public class LoadoutReportTests
{
    [Fact]
    public void ReportShowsDecorationsPerPieceSkillSourcesAndStats()
    {
        var data = TestData.Data;
        var spec = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, Element = Element.Dragon,
            Reinforcements = [new(ReinforcementType.Attack, ReinforcementTier.III), new(ReinforcementType.Affinity, ReinforcementTier.EX)],
            SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul",
        };
        var crit = data.Decoration("Critical Jewel III [3]");
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(spec, data, [crit, crit, data.Decoration("Expert Jewel III [3]")]),
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Head), Decorations: [data.Decoration("Mighty Jewel [2]")]), // helm has one level-2 slot
            Chest = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Chest)),
            Talisman = data.Charm("Challenger Charm III"),
        };
        Assert.Empty(loadout.Validate(data));

        var text = LoadoutReport.Render(loadout, data, Conditions.Default, "test build");

        Assert.Contains("=== test build ===", text);
        Assert.Contains("TL;DR  Attack", text);
        Assert.Contains("Why: ", text);

        var summary = BuildSummary.Create(loadout, data, Conditions.Default);
        var result = DamageCalculator.Calculate(loadout, data, Conditions.Default);
        Assert.Equal(result.Affinity, summary.Affinity);
        Assert.Equal(result.Total, summary.Total, 0.001);
        Assert.Equal(result.TrueRaw, summary.Attack, 0.001);
        Assert.Contains(summary.ActiveSetBonuses, x => x.StartsWith("Gore Magala's Tyranny I"));
        Assert.Contains(summary.Skills, x => x.Skill == "Critical Boost" && x.Level == 5 && x.Kind == SkillKind.Weapon);
        Assert.Contains("enraged monster", summary.DependsOn);          // Agitator charm
        Assert.Contains(summary.AffinitySources, x => x.StartsWith("Critical Eye"));
        Assert.Contains("affinity with", summary.Description);
        Assert.Contains("decos: Critical Jewel III [3], Critical Jewel III [3], Expert Jewel III [3]", text);
        Assert.Contains("decos: Mighty Jewel [2]", text);
        Assert.Contains("2x Critical Jewel III [3]", text);
        Assert.Contains("Decorations (4)", text);
        Assert.Contains("Lv5 (weapon) <- Critical Jewel III [3] x2 6  (1 wasted)", text);
        Assert.Contains("Lv3 (armor ) <- Challenger Charm III 3", text);
        Assert.Contains("Maximum Might", text);
        Assert.Contains("Gore Magala's Tyranny 3pc -> I (Black Eclipse I)", text);
        Assert.Contains("Lord's Soul 1pc", text);
        Assert.Contains("Stats with every conditional skill active", text);
        Assert.Contains("Stats under the requested conditions", text);
        Assert.Contains("EFR ", text);
        Assert.Contains("Element    Dragon", text);
    }

    [Fact]
    public void AllOnConditionsNeverScoreBelowDefault()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity, Element = Element.Fire }, data),
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Head)),
            Talisman = data.Charm("Chain Charm III"),
        };
        var allOn = DamageCalculator.Calculate(loadout, data, Conditions.AllOn);
        var def = DamageCalculator.Calculate(loadout, data, Conditions.Default);
        var off = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        Assert.True(allOn.Total >= def.Total);
        Assert.True(def.Total >= off.Total);
    }
}
