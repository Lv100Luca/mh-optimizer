using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

/// <summary>The attack picker (per-hit motion values, element modifiers, phials) and the target hitzones.</summary>
public class AttackTargetTests
{
    private const double Tol = 1e-9;

    private static readonly ActiveSkills NoSkills = new(new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>());

    private static GogmaWeaponStats Weapon(WeaponType type, GogmaFocus focus, Element element = Element.None) =>
        new GogmaWeaponSpec { Type = type, Focus = focus, Element = element }.Resolve(TestData.Data);

    [Fact]
    public void TrueChargedSlashUsesThePowerFinisherOnAWeakSpot()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Element, Element.Dragon);
        var sharpEle = DamageConstants.SharpnessElement(gs.TopSharpness!.Value);
        var weak = new AttackProfile().Resolve(gs, new Target { RawHitzone = 60, ElementHitzone = 30 });
        Assert.Equal(16 + 267, weak.TotalMv, Tol);
        Assert.Equal(3.5 * sharpEle * 0.5 * 100 / 283, weak.ElementFactor, Tol);
        Assert.Equal(1, weak.Shockwaves, Tol);

        // raw hitzone 30 x white 1.32 stays below 45: the normal finisher
        var hard = new AttackProfile().Resolve(gs, new Target { RawHitzone = 30, ElementHitzone = 15 });
        Assert.Equal(16 + 209, hard.TotalMv, Tol);
        Assert.Equal(3.5 * sharpEle * 0.5 * 100 / 225, hard.ElementFactor, Tol);
    }

    [Fact]
    public void ChargeComboTackleHitsAtGreenWithoutElement()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Attack, Element.Fire);
        var sharpRaw = DamageConstants.SharpnessRaw(gs.TopSharpness!.Value);
        var p = new AttackProfile { Attack = "charge-combo" }.Resolve(gs, new Target());
        const double mv = 176 + 48 + 187 + 16 + 267;
        Assert.Equal(5, p.Hits);
        Assert.Equal(mv, p.TotalMv, Tol);
        Assert.Equal(((mv - 48) * sharpRaw + 48 * 1.05) / mv, p.RawFactor, Tol);
        Assert.Equal(3, p.Shockwaves, Tol);
    }

    [Fact]
    public void ElementPhialBoostsSwordSlashesAndItsExplosions()
    {
        var sa = Weapon(WeaponType.SwitchAxe, GogmaFocus.Element, Element.Thunder);
        var sharpRaw = DamageConstants.SharpnessRaw(sa.TopSharpness!.Value);
        var sharpEle = DamageConstants.SharpnessElement(sa.TopSharpness!.Value);
        var p = new AttackProfile().Resolve(sa, new Target { RawHitzone = 50, ElementHitzone = 25 });
        Assert.StartsWith("Full Release Slash", p.Name);
        Assert.Equal(367, p.TotalMv, Tol);
        Assert.Equal(sharpRaw, p.RawFactor, Tol); // no raw boost
        // two slashes at element x1 x1.45, eight explosions at x0.8
        Assert.Equal((2 * 1.45 + 8 * 0.8) * sharpEle * 0.5 * 100 / 367, p.ElementFactor, Tol);
    }

    [Fact]
    public void PowerPhialBoostsSwordSlashRaw()
    {
        var sa = Weapon(WeaponType.SwitchAxe, GogmaFocus.Attack, Element.Ice);
        var sharpRaw = DamageConstants.SharpnessRaw(sa.TopSharpness!.Value);
        var sharpEle = DamageConstants.SharpnessElement(sa.TopSharpness!.Value);
        var p = new AttackProfile().Resolve(sa, new Target { RawHitzone = 50, ElementHitzone = 25 });
        Assert.Equal((132 * 1.17 + 235) * sharpRaw / 367, p.RawFactor, Tol);
        Assert.Equal((2 + 8 * 0.35) * sharpEle * 0.5 * 100 / 367, p.ElementFactor, Tol);
    }

    [Fact]
    public void ElementIsScoredAtTheTargetsElementHitzoneShare()
    {
        var sa = Weapon(WeaponType.SwitchAxe, GogmaFocus.Element, Element.Thunder);
        var weak = DamageCalculator.Calculate(sa, NoSkills, Conditions.AllOff with { Target = new Target { RawHitzone = 50, ElementHitzone = 30 } });
        var resistant = DamageCalculator.Calculate(sa, NoSkills, Conditions.AllOff with { Target = new Target { RawHitzone = 50, ElementHitzone = 10 } });
        Assert.Equal(weak.EffectiveRaw, resistant.EffectiveRaw, Tol);
        Assert.Equal(weak.EffectiveElement / 3, resistant.EffectiveElement, Tol);
        Assert.Equal(weak.ElementTrue * weak.Attack.ElementFactor, weak.EffectiveElement, Tol);
    }

    [Fact]
    public void WeaknessExploitNeedsAWeakPointTarget()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Attack);
        var skills = new ActiveSkills(new Dictionary<string, int> { ["Weakness Exploit"] = 5 }, new Dictionary<string, int> { ["Weakness Exploit"] = 5 },
            new Dictionary<string, int>(), new Dictionary<string, int>());
        var cond = Conditions.AllOff with { HittingWeakPoint = true };
        Assert.Equal(gs.Affinity + 30, DamageCalculator.Calculate(gs, skills, cond with { Target = new Target { RawHitzone = 45 } }).Affinity);
        Assert.Equal(gs.Affinity, DamageCalculator.Calculate(gs, skills, cond with { Target = Target.Dummy("Hard part") }).Affinity);
    }

    [Fact]
    public void MonsterTargetsTakeTheWeaponsDamageTypeAndElement()
    {
        var reyDau = TestData.Data.Monsters.Single(m => m.Name == "Rey Dau");
        var head = reyDau.Parts.Single(p => p.Name == "Head");
        var t = Target.ForMonster(reyDau, head, WeaponType.SwitchAxe);
        Assert.Equal((TargetKind.Monster, "Rey Dau", "Head"), (t.Kind, t.Monster, t.Part));
        Assert.Equal(head.Slash, t.RawHitzone);
        Assert.Equal(head.Ice, t.ElementHitzoneFor(Element.Ice));
        Assert.Equal(head.Blunt, Target.ForMonster(reyDau, head, WeaponType.Hammer).RawHitzone);
        Assert.True(TestData.Data.Monsters.Count >= 30);
        Assert.All(TestData.Data.Monsters, m => Assert.NotEmpty(m.Parts));
    }

    [Fact]
    public void UnknownAttacksAndBadHitzonesAreRejected()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Spec = new GogmaWeaponSpecInput { Type = "switch-axe", Focus = GogmaFocus.Element, Element = Element.Fire } },
            Conditions = Conditions.Default with
            {
                AttackProfile = new AttackProfile { Attack = "true-charged-slash" },
                Target = new Target { RawHitzone = 0 },
            },
        };
        var errors = RequestLoader.Resolve(request, TestData.Data, ".").Errors;
        Assert.Contains(errors, e => e.Contains("attack_profile.attack"));
        Assert.Contains(errors, e => e.Contains("target.raw_hitzone"));
    }

    [Fact]
    public void WeaponTypesWithoutAttackDataKeepTheAverageHitModel()
    {
        var ls = Weapon(WeaponType.LongSword, GogmaFocus.Element, Element.Ice);
        var p = new AttackProfile().Resolve(ls, new Target { RawHitzone = 50, ElementHitzone = 20 });
        Assert.Equal("Average hit", p.Name);
        Assert.Equal(35, p.TotalMv, Tol);
        Assert.Equal(DamageConstants.SharpnessElement(ls.TopSharpness!.Value) * 0.4 * 100 / 35, p.ElementFactor, Tol);
    }
}
