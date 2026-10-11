using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Tests;

/// <summary>The bow: coatings by focus, arrows per shot, the Pierce coating's multi-hit and the shot skills.</summary>
public class BowTests
{
    private const double Tol = 1e-9;

    private static ActiveSkills Skills(params (string Skill, int Level)[] levels)
    {
        var d = levels.ToDictionary(x => x.Skill, x => x.Level);
        return new ActiveSkills(d, d, new Dictionary<string, int>(), new Dictionary<string, int>());
    }

    private static GogmaWeaponStats Bow(GogmaFocus focus, Element element = Element.None) =>
        new GogmaWeaponSpec { Type = WeaponType.Bow, Focus = focus, Element = element }.Resolve(TestData.Data);

    private static Conditions Attack(string id, Target? target = null, int? pierceHits = null) =>
        Conditions.AllOff with { AttackProfile = new AttackProfile { Attack = id, PierceHits = pierceHits }, Target = target ?? new Target { RawHitzone = 50, ElementHitzone = 25 } };

    [Fact]
    public void ArtianBowCoatingFollowsTheFocus()
    {
        Assert.Equal(BowCoating.CloseRange, Bow(GogmaFocus.Attack).Coating);
        Assert.Equal(BowCoating.Power, Bow(GogmaFocus.Affinity).Coating);
        Assert.Equal(BowCoating.Pierce, Bow(GogmaFocus.Element).Coating);
        Assert.Null(Bow(GogmaFocus.Attack).TopSharpness);
        Assert.Null(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }.Resolve(TestData.Data).Coating);
    }

    [Fact]
    public void BowsValidateAndBowgunsStayOutOfScope()
    {
        var data = TestData.Data;
        Assert.Empty(new WeaponStatsInput { Spec = new GogmaWeaponSpecInput { Type = "bow", Focus = GogmaFocus.Affinity, Element = Element.Water } }.Validate(data));
        var plain = new WeaponStatsInput { Type = "bow", Attack = 220, AttackIsDisplay = false, Element = Element.Thunder, ElementDisplay = 250, Coating = BowCoating.CloseRange };
        Assert.Empty(plain.Validate(data));
        var stats = plain.ToStats(data);
        Assert.Equal((220, BowCoating.CloseRange), (stats.TrueRaw, stats.Coating));
        Assert.Null(stats.TopSharpness);
        Assert.Equal(BowCoating.Power, (plain with { Coating = null }).ToStats(data).Coating);

        Assert.Contains(new WeaponStatsInput { Type = "light-bowgun", Attack = 300 }.Validate(data), e => e.Contains("bowguns"));
        Assert.Contains(new WeaponStatsInput { Type = "long-sword", Attack = 660, Coating = BowCoating.Power }.Validate(data), e => e.Contains("coating"));
    }

    [Fact]
    public void PowerCoatingMultipliesEveryArrow()
    {
        var bow = Bow(GogmaFocus.Affinity, Element.Ice);
        var p = Attack("charged-shot-3").Attack(bow);
        Assert.Equal(3, p.Hits);
        Assert.Equal(36, p.TotalMv, Tol);
        Assert.Equal(DamageConstants.PowerCoatingRaw, p.RawFactor, Tol);
        Assert.Equal(3 * 1.0 * 0.5 * 100 / 36, p.ElementFactor, Tol);
        Assert.Equal(1.0, p.Shots.Normal, Tol);
        Assert.Contains("Power coating", p.Name);
    }

    [Fact]
    public void PierceArrowsHitSeveralTimesWithDecayingRaw()
    {
        var bow = Bow(GogmaFocus.Element, Element.Fire);
        var three = Attack("charged-shot-3").Attack(bow);
        // three pierce arrows of 6 MV, each landing 3 times at x1.2, x1.2, x0.9
        Assert.Equal(9, three.Hits);
        Assert.Equal(54, three.TotalMv, Tol);
        Assert.Equal(1.1, three.RawFactor, Tol);
        Assert.Equal(9 * 0.4 * 0.5 * 100 / 54, three.ElementFactor, Tol);

        var one = Attack("charged-shot-3", pierceHits: 1).Attack(bow);
        Assert.Equal((3, 18), (one.Hits, one.TotalMv));
        Assert.Equal(1.2, one.RawFactor, Tol);
        // the eighth hit and beyond add next to nothing
        var eight = Attack("charged-shot-3", pierceHits: 8).Attack(bow);
        Assert.Equal(DamageConstants.PierceHitMultipliers.Sum() * 18, eight.RawFactor * eight.TotalMv, Tol);
    }

    [Fact]
    public void ShotSkillsBoostOnlyTheArrowsTheyName()
    {
        var bow = Bow(GogmaFocus.Affinity);
        var none = Skills();
        var normal = Skills(("Normal Shots", 1));
        var power = Skills(("Spread/Power Shots", 1));

        var charged = Attack("charged-shot-3");
        var bare = DamageCalculator.Calculate(bow, none, charged).EffectiveRaw;
        Assert.Equal(bare * 1.05, DamageCalculator.Calculate(bow, normal, charged).EffectiveRaw, Tol);
        Assert.Equal(bare, DamageCalculator.Calculate(bow, power, charged).EffectiveRaw, Tol);

        var shot = Attack("power-shot-3");
        Assert.Equal(DamageCalculator.Calculate(bow, none, shot).EffectiveRaw * 1.05, DamageCalculator.Calculate(bow, power, shot).EffectiveRaw, Tol);

        // a sequence of both: each skill boosts its share of the raw
        var mixed = Conditions.AllOff with
        {
            AttackProfile = new AttackProfile
            {
                Attack = Attacks.Sequence,
                Sequence = [new SequenceStep { Attack = "charged-shot-3" }, new SequenceStep { Attack = "power-shot-3" }],
            },
        };
        var result = DamageCalculator.Calculate(bow, normal, mixed);
        Assert.Equal(36.0 / 96, result.Attack.Shots.Normal, Tol);
        Assert.Equal(1 + 36.0 / 96 * 0.05, result.ShotMultiplier, Tol);
        Assert.Equal(DamageCalculator.Calculate(bow, none, mixed).EffectiveRaw * result.ShotMultiplier, result.EffectiveRaw, Tol);

        // the per-hit breakdown carries the boost, so the attacks still add up to the damage per execution
        var breakdown = DamageCalculator.Breakdown(bow, normal, mixed);
        Assert.Equal(result.DamagePerExecution, breakdown.Sum(a => a.Average * a.Repeat), 1e-6);
    }

    [Fact]
    public void ShotSkillsAreRelevantForTheArrowsTheAttackShoots()
    {
        var bow = Bow(GogmaFocus.Affinity);
        var targets = new Dictionary<string, int>();
        var charged = Relevance.Build(bow, targets, Attack("charged-shot-3"), TestData.Data).Skills;
        Assert.Contains("Normal Shots", charged);
        Assert.DoesNotContain("Spread/Power Shots", charged);
        Assert.DoesNotContain("Special Ammo Boost", charged);
        var gs = new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }.Resolve(TestData.Data);
        Assert.DoesNotContain("Normal Shots", Relevance.Build(gs, targets, Conditions.AllOn, TestData.Data).Skills);
    }

    /// <summary>The CP model's composition of the shot skills matches the calculator, alone and on a sequence of both kinds of arrows.</summary>
    [Theory]
    [InlineData("charged-shot-3")]
    [InlineData("sequence")]
    public void ScoreModelComposesTheShotSkills(string attack)
    {
        var bow = Bow(GogmaFocus.Affinity, Element.Thunder);
        var cond = attack == "sequence"
            ? Conditions.AllOn with
            {
                AttackProfile = new AttackProfile
                {
                    Attack = Attacks.Sequence,
                    Sequence = [new SequenceStep { Attack = "charged-shot-3", Repeat = 2 }, new SequenceStep { Attack = "power-shot-3", Conditions = new() { ["stamina_full"] = false } }],
                },
            }
            : Conditions.AllOn with { AttackProfile = new AttackProfile { Attack = attack } };
        var rel = Relevance.Build(bow, new Dictionary<string, int> { ["Weakness Exploit"] = 3 }, cond, TestData.Data);
        var score = ScoreDecomposition.Build(bow, rel, cond);
        Assert.True(score.Verify(2000) < 1e-6);
    }
}
