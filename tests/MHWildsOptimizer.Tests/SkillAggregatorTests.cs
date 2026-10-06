using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

public class SkillAggregatorTests
{
    private static EquippedWeapon Weapon(string? setBonus = null, string? groupSkill = null) =>
        new(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, SetBonus = setBonus, GroupSkill = groupSkill }, TestData.Data);

    [Fact]
    public void WeaponCountsAsOnePieceForItsSetBonus()
    {
        var data = TestData.Data;
        var one = new Loadout
        {
            Weapon = Weapon(setBonus: "Gore Magala's Tyranny"),
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Head)),
        };
        var skills = SkillAggregator.Aggregate(one, data);
        Assert.Equal(2, skills.SetBonusPieces["Gore Magala's Tyranny"]);
        Assert.Equal(SetBonusTier.I, skills.SetTier("Gore Magala's Tyranny"));

        var three = new Loadout
        {
            Weapon = Weapon(setBonus: "Gore Magala's Tyranny"),
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Head)),
            Chest = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Chest)),
            Arms = new EquippedArmor(TestData.PieceOf("Gore β", ArmorPieceKind.Arms)),
        };
        Assert.Equal(SetBonusTier.II, SkillAggregator.Aggregate(three, data).SetTier("Gore Magala's Tyranny"));

        Assert.Empty(one.Validate(data));
    }

    [Fact]
    public void WeaponCountsAsOnePieceForItsGroupSkill()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = Weapon(groupSkill: "Lord's Soul"),
            Waist = new EquippedArmor(TestData.PieceOf("Rey Dau γ", ArmorPieceKind.Waist)),
            Legs = new EquippedArmor(TestData.PieceOf("Dahaad γ", ArmorPieceKind.Legs)),
        };
        var skills = SkillAggregator.Aggregate(loadout, data);
        Assert.Equal(3, skills.GroupSkillPieces["Lord's Soul"]);
        Assert.True(skills.GroupActive("Lord's Soul"));
        Assert.Contains("Lord's Soul", skills.ActiveGroupSkills);

        var two = new Loadout
        {
            Weapon = Weapon(),
            Waist = new EquippedArmor(TestData.PieceOf("Rey Dau γ", ArmorPieceKind.Waist)),
            Legs = new EquippedArmor(TestData.PieceOf("Dahaad γ", ArmorPieceKind.Legs)),
        };
        Assert.False(SkillAggregator.Aggregate(two, data).GroupActive("Lord's Soul"));
    }

    [Fact]
    public void GogmaziosPiecesCountTowardsTwoSetBonuses()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = Weapon(setBonus: "Xu Wu's Vigor"),
            Chest = new EquippedArmor(TestData.PieceOf("Gogmazios α", ArmorPieceKind.Chest)), // Xu Wu's Vigor + Gogmapocalypse
            Head = new EquippedArmor(TestData.PieceOf("Gogmazios α", ArmorPieceKind.Head)),   // Zoh Shia's Pulse + Gogmapocalypse
        };
        var skills = SkillAggregator.Aggregate(loadout, data);
        Assert.Equal(SetBonusTier.I, skills.SetTier("Xu Wu's Vigor"));      // weapon + mail α
        Assert.Equal(SetBonusTier.I, skills.SetTier("Gogmapocalypse"));    // helm α + mail α
        Assert.Equal(1, skills.SetBonusPieces["Zoh Shia's Pulse"]);          // helm α only
        Assert.Equal(SetBonusTier.None, skills.SetTier("Zoh Shia's Pulse"));
    }

    [Fact]
    public void SkillLevelsSumAcrossSourcesAndAreCapped()
    {
        var data = TestData.Data;
        var critJewel = data.Decoration("Critical Jewel III [3]");
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity }, TestData.Data,
                [critJewel, critJewel, data.Decoration("Expert Jewel III [3]")]),
            Talisman = data.Charm("Challenger Charm III"),
        };
        var skills = SkillAggregator.Aggregate(loadout, data);
        Assert.Equal(6, skills.RawLevels["Critical Boost"]);
        Assert.Equal(5, skills.Level("Critical Boost"));
        Assert.Equal(3, skills.Level("Critical Eye"));
        Assert.Equal(3, skills.Level("Agitator"));
        Assert.Empty(loadout.Validate(data));
    }

    [Fact]
    public void ValidationCatchesWrongPieceAndDecorationKind()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }, TestData.Data,
                [data.Decoration("Tenderizer Jewel [3]")]), // armor deco in a weapon slot
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Chest)), // chest piece on the head
        };
        var errors = loadout.Validate(data);
        Assert.Contains(errors, e => e.Contains("Armor decoration"));
        Assert.Contains(errors, e => e.Contains("is a Chest piece"));
    }
}
