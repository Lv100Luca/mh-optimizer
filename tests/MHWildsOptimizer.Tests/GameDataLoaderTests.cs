using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Tests;

public class GameDataLoaderTests
{
    [Fact]
    public void LoadsTheWholeDataset()
    {
        var d = TestData.Data;
        Assert.Equal(582, d.Armor.Count);
        Assert.Equal(179, d.Skills.Count);
        Assert.Equal(361, d.Decorations.Count);
        Assert.Equal(14, d.GogmaWeapons.Count);
        Assert.All(d.GogmaWeapons.Values, byFocus => Assert.Equal(3, byFocus.Count));
        Assert.NotEmpty(d.MaxRankCharms);
    }

    [Fact]
    public void SkillKindsAndRanksAreTyped()
    {
        var d = TestData.Data;
        var attackBoost = d.Skill("Attack Boost");
        Assert.Equal(SkillKind.Weapon, attackBoost.Kind);
        Assert.Equal(5, attackBoost.MaxLevel);

        var gogma = d.Skill("Gogmapocalypse");
        Assert.Equal(SkillKind.Set, gogma.Kind);
        Assert.Equal(2, gogma.Ranks[0].PiecesRequired);
        Assert.Equal(4, gogma.Ranks[1].PiecesRequired);
        Assert.Equal("Mutual Hostility I", gogma.Ranks[0].Name);

        Assert.Equal(SkillKind.Group, d.Skill("Lord's Soul").Kind);
        Assert.Equal(SkillKind.Armor, d.Skill("Weakness Exploit").Kind);
    }

    [Fact]
    public void GogmaziosPiecesCarryTwoSetBonusesAndNoGroupSkill()
    {
        var helm = TestData.Data.ArmorPiece("Gogmazios Helm α");
        Assert.Equal(ArmorPieceKind.Head, helm.Piece);
        Assert.Equal(8, helm.Rarity);
        Assert.Contains("Gogmapocalypse", helm.SetBonus);
        Assert.Contains("Zoh Shia's Pulse", helm.SetBonus);
        Assert.Null(helm.GroupSkill);
        Assert.Equal([3, 1], helm.Slots);
        Assert.Contains(helm.Skills, s => s.Skill == "Peak Performance" && s.Level == 2);
    }

    [Fact]
    public void TranscendedSlotsFollowTheRarityRule()
    {
        var d = TestData.Data;
        var r6 = d.ArmorPiece("Blango Helm β");
        Assert.Equal(6, r6.Rarity);
        Assert.Equal([2, 1, 1], r6.Slots);
        Assert.Equal([3, 2, 1], r6.SlotsTranscended);

        var r8 = d.ArmorPiece("Gogmazios Helm α");
        Assert.Equal(r8.Slots, r8.SlotsTranscended);
    }

    [Fact]
    public void GogmaVariantsMatchTheFocusDeltas()
    {
        var gs = TestData.Data.GogmaWeapons[WeaponType.GreatSword];
        Assert.Equal(200, gs[GogmaFocus.Attack].Raw);
        Assert.Equal(-10, gs[GogmaFocus.Attack].Affinity);
        Assert.Equal(180, gs[GogmaFocus.Affinity].Raw);
        Assert.Equal(15, gs[GogmaFocus.Affinity].Affinity);
        Assert.Equal(190, gs[GogmaFocus.Element].Raw);
        Assert.Equal(0, gs[GogmaFocus.Element].Affinity);
        Assert.Equal(SharpnessColor.White, gs[GogmaFocus.Attack].Sharpness!.TopColor);
        Assert.Equal([3, 3, 3], gs[GogmaFocus.Attack].Slots);

        Assert.Null(TestData.Data.GogmaWeapons[WeaponType.LightBowgun][GogmaFocus.Attack].Sharpness);
    }
}
