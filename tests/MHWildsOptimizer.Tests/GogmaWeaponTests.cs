using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

public class GogmaWeaponTests
{
    private static Reinforcement R(ReinforcementType t, ReinforcementTier tier) => new(t, tier);

    [Fact]
    public void AttackFocusGreatSwordWithMaxRawReinforcements()
    {
        var spec = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword,
            Focus = GogmaFocus.Attack,
            AttackParts = 3,
            Reinforcements =
            [
                R(ReinforcementType.Attack, ReinforcementTier.EX), R(ReinforcementType.Attack, ReinforcementTier.EX),
                R(ReinforcementType.Affinity, ReinforcementTier.EX), R(ReinforcementType.Affinity, ReinforcementTier.EX),
                R(ReinforcementType.Attack, ReinforcementTier.III),
            ],
        };
        Assert.Empty(spec.Validate(TestData.Data));

        var stats = spec.Resolve(TestData.Data);
        Assert.Equal(200 + 15 + 24 + 9, stats.TrueRaw);
        Assert.Equal(-10 + 20, stats.Affinity);
        Assert.Equal(0, stats.ElementDisplay);
        Assert.Equal(SharpnessColor.White, stats.TopSharpness);
        Assert.Equal((int)Math.Round(248 * 4.8), stats.DisplayAttack);
    }

    [Fact]
    public void ElementFocusGreatSwordAddsBaseInfusionFocusAndReinforcements()
    {
        var spec = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword,
            Focus = GogmaFocus.Element,
            Element = Element.Fire,
            Infused = true,
            AttackParts = 3,
            Reinforcements = [R(ReinforcementType.Element, ReinforcementTier.EX), R(ReinforcementType.Element, ReinforcementTier.EX)],
        };
        Assert.Empty(spec.Validate(TestData.Data));

        var stats = spec.Resolve(TestData.Data);
        Assert.Equal(190 + 15, stats.TrueRaw);
        Assert.Equal(0, stats.Affinity);
        Assert.Equal(450 + 30 + 50 + 220, stats.ElementDisplay);
        Assert.Equal(75.0, stats.ElementTrue);
    }

    [Fact]
    public void AffinityFocusLongSwordWithAffinityParts()
    {
        var spec = new GogmaWeaponSpec
        {
            Type = WeaponType.LongSword,
            Focus = GogmaFocus.Affinity,
            Element = Element.Thunder,
            AttackParts = 0,
        };
        var stats = spec.Resolve(TestData.Data);
        Assert.Equal(180, stats.TrueRaw);
        Assert.Equal(15 + 15, stats.Affinity);
        Assert.Equal(270 + 30 - 20, stats.ElementDisplay);
        Assert.Equal(SharpnessColor.White, stats.TopSharpness);
    }

    [Fact]
    public void ValidationRejectsImpossibleReinforcements()
    {
        var data = TestData.Data;

        var elementIii = new GogmaWeaponSpec
        {
            Type = WeaponType.LongSword, Focus = GogmaFocus.Element, Element = Element.Ice,
            Reinforcements = [R(ReinforcementType.Element, ReinforcementTier.III)],
        };
        Assert.Contains(elementIii.Validate(data), e => e.Contains("does not exist"));

        var tripleEx = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack,
            Reinforcements = Enumerable.Repeat(R(ReinforcementType.Attack, ReinforcementTier.EX), 3).ToList(),
        };
        Assert.Contains(tripleEx.Validate(data), e => e.Contains("More than 2x"));

        var threeSharpness = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack,
            Reinforcements = Enumerable.Repeat(R(ReinforcementType.Sharpness, ReinforcementTier.I), 3).ToList(),
        };
        Assert.Contains(threeSharpness.Validate(data), e => e.Contains("More than 2 Sharpness"));

        var elementOnRawWeapon = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack,
            Reinforcements = [R(ReinforcementType.Element, ReinforcementTier.EX)],
        };
        Assert.Contains(elementOnRawWeapon.Validate(data), e => e.Contains("without an element"));

        var badSkills = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack,
            SetBonus = "Lord's Soul", GroupSkill = "Gogmapocalypse",
        };
        Assert.Equal(2, badSkills.Validate(data).Count);
    }
}
