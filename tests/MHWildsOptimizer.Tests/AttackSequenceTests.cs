using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Tests;

/// <summary>Custom attack sequences: steps add their hits, and steps that change conditions are scored as weighted segments.</summary>
public class AttackSequenceTests
{
    private const double Tol = 1e-9;

    private static ActiveSkills Skills(params (string Skill, int Level)[] levels)
    {
        var d = levels.ToDictionary(x => x.Skill, x => x.Level);
        return new ActiveSkills(d, d, new Dictionary<string, int>(), new Dictionary<string, int>());
    }

    private static GogmaWeaponStats Gs => new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Element, Element = Element.Dragon }.Resolve(TestData.Data);

    private static AttackProfile Sequence(params SequenceStep[] steps) => new() { Attack = Attacks.Sequence, Sequence = steps };

    [Fact]
    public void StepsWithoutOverridesScoreLikeOneAttackOfAllTheirHits()
    {
        var cond = Conditions.Default with { AttackProfile = Sequence(new SequenceStep { Attack = "charged-slash" }, new SequenceStep { Attack = "tackle" },
            new SequenceStep { Attack = "strong-charged-slash" }, new SequenceStep { Attack = "true-charged-slash" }) };
        var combo = Conditions.Default with { AttackProfile = new AttackProfile { Attack = "charge-combo" } };
        Assert.Single(cond.Segments(Gs));
        var a = cond.Attack(Gs);
        var b = combo.Attack(Gs);
        Assert.Equal((b.TotalMv, b.RawFactor, b.ElementFactor, b.Shockwaves), (a.TotalMv, a.RawFactor, a.ElementFactor, a.Shockwaves));
        var skills = Skills(("Maximum Might", 3));
        Assert.Equal(DamageCalculator.Calculate(Gs, skills, combo).Total, DamageCalculator.Calculate(Gs, skills, cond).Total, Tol);
    }

    [Fact]
    public void RepeatCountsTheStepAgain()
    {
        var once = Conditions.Default with { AttackProfile = Sequence(new SequenceStep { Attack = "true-charged-slash" }, new SequenceStep { Attack = "tackle" }) };
        var twice = Conditions.Default with { AttackProfile = Sequence(new SequenceStep { Attack = "true-charged-slash", Repeat = 2 }, new SequenceStep { Attack = "tackle" }) };
        Assert.Equal(once.Attack(Gs).TotalMv + 283, twice.Attack(Gs).TotalMv, Tol); // power TCS on the default weak point
    }

    [Fact]
    public void AStepWithoutMaximumMightIsWeightedByItsMotionValue()
    {
        var skills = Skills(("Maximum Might", 3));
        var tcs = Conditions.Default with { AttackProfile = new AttackProfile { Attack = "true-charged-slash" } };
        var scsNoMight = Conditions.Default with { StaminaFull = false, AttackProfile = new AttackProfile { Attack = "strong-charged-slash" } };
        var cond = Conditions.Default with
        {
            AttackProfile = Sequence(new SequenceStep { Attack = "strong-charged-slash", Conditions = new() { ["stamina_full"] = false } },
                new SequenceStep { Attack = "true-charged-slash" }),
        };
        Assert.Equal(2, cond.Segments(Gs).Count);

        var a = DamageCalculator.Calculate(Gs, skills, tcs);
        var b = DamageCalculator.Calculate(Gs, skills, scsNoMight);
        Assert.Equal(a.Affinity - 30, b.Affinity);
        const double mvA = 283, mvB = 187;
        var r = DamageCalculator.Calculate(Gs, skills, cond);
        Assert.Equal((a.EffectiveRaw * mvA + b.EffectiveRaw * mvB) / (mvA + mvB), r.EffectiveRaw, Tol);
        Assert.Equal((a.EffectiveElement * mvA + b.EffectiveElement * mvB) / (mvA + mvB), r.EffectiveElement, Tol);
        Assert.Equal(mvA + mvB, r.Attack.TotalMv, Tol);
    }

    [Fact]
    public void OverridesThatChangeNothingDoNotSplitTheSequence()
    {
        var cond = Conditions.Default with
        {
            AttackProfile = Sequence(new SequenceStep { Attack = "charged-slash", Conditions = new() { ["stamina_full"] = true } },
                new SequenceStep { Attack = "true-charged-slash" }),
        };
        Assert.Single(cond.Segments(Gs));
    }

    [Fact]
    public void ScoreModelAddsTheSegmentsUp()
    {
        var data = TestData.Data;
        var sa = new GogmaWeaponSpec { Type = WeaponType.SwitchAxe, Focus = GogmaFocus.Element, Element = Element.Thunder }.Resolve(data);
        var cond = Conditions.AllOn with
        {
            OffensiveGuardActive = false,
            AttackProfile = Sequence(
                new SequenceStep { Attack = "amped-sword-combo", Repeat = 2 },
                new SequenceStep { Attack = "full-release-slash", Conditions = new() { ["stamina_full"] = false, ["offensive_guard_active"] = true } },
                new SequenceStep { Attack = "axe-follow-up-morph", Conditions = new() { ["burst_active"] = false } }),
        };
        var rel = Relevance.Build(sa, new Dictionary<string, int>(), cond, data);
        Assert.Contains("Offensive Guard", rel.Skills); // only one step turns it on
        var score = ScoreDecomposition.Build(sa, rel, cond);
        Assert.Equal(3 * score.HealthSides, score.Sides.Count);
        Assert.True(score.Verify(1000) < 1e-6);
    }

    [Fact]
    public void ExampleSequencesAreValid()
    {
        foreach (var preset in Attacks.SequencePresets)
            Assert.Empty(new AttackProfile { Attack = Attacks.Sequence, Sequence = preset.Steps }.Validate(preset.Type));
    }

    [Fact]
    public void BadStepsAreRejected()
    {
        var profile = Sequence(new SequenceStep { Attack = "full-release-slash" },
            new SequenceStep { Attack = "true-charged-slash", Repeat = 0, Conditions = new() { ["red_health"] = true, ["made_up"] = false } });
        var errors = profile.Validate(WeaponType.GreatSword);
        Assert.Contains(errors, e => e.Contains("step 1") && e.Contains("full-release-slash"));
        Assert.Contains(errors, e => e.Contains("step 2") && e.Contains("repeat"));
        Assert.Contains(errors, e => e.Contains("'red_health'"));
        Assert.Contains(errors, e => e.Contains("'made_up'"));
        Assert.Contains(Sequence().Validate(WeaponType.GreatSword), e => e.Contains("at least one step"));
    }

    [Fact]
    public void SequencesCompareByValue()
    {
        AttackProfile Make() => Sequence(new SequenceStep { Attack = "tackle", Conditions = new() { ["stamina_full"] = false } });
        Assert.Equal(Make(), Make());
        Assert.True(ConditionPresets.Same(Conditions.Default with { AttackProfile = Make() }, Conditions.Default with { AttackProfile = Make() }));
        Assert.NotEqual(Make(), Sequence(new SequenceStep { Attack = "tackle" }));
    }
}
