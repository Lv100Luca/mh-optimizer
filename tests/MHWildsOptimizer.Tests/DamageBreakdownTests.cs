using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

/// <summary>The per-attack, per-hit damage breakdown: hits without a crit, with one and on average, adding up to the damage per attack or sequence.</summary>
public class DamageBreakdownTests
{
    private static ActiveSkills Skills(IReadOnlyDictionary<string, int>? sets = null, params (string Skill, int Level)[] levels)
    {
        var d = levels.ToDictionary(x => x.Skill, x => x.Level);
        return new ActiveSkills(d, d, sets ?? new Dictionary<string, int>(), new Dictionary<string, int>());
    }

    private static GogmaWeaponStats Weapon(WeaponType type, GogmaFocus focus, Element element = Element.None) =>
        new GogmaWeaponSpec { Type = type, Focus = focus, Element = element }.Resolve(TestData.Data);

    private static double Sum(IReadOnlyList<AttackDamage> attacks) => attacks.Sum(a => a.Average * a.Repeat);

    [Fact]
    public void ASequenceBreaksDownIntoItsStepsInOrderAndAddsUpToItsDamage()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Element, Element.Dragon);
        // Dark Arts shockwaves (procs), Maximum Might lost on one step, full and red health both on (scored at the better side)
        var skills = Skills(new Dictionary<string, int> { [SkillNames.SoulOfTheDarkKnight] = 4 }, (SkillNames.MaximumMight, 3), (SkillNames.Resentment, 5));
        var cond = Conditions.Default with
        {
            RedHealth = true,
            AttackProfile = new AttackProfile
            {
                Attack = Attacks.Sequence,
                Sequence =
                [
                    new SequenceStep { Attack = "offset-rising-slash" },
                    new SequenceStep { Attack = "tackle" },
                    new SequenceStep { Attack = "strong-charged-slash", Conditions = new() { ["stamina_full"] = false } },
                    new SequenceStep { Attack = "true-charged-slash", Repeat = 2 },
                ],
            },
        };
        var r = DamageCalculator.Calculate(gs, skills, cond);
        var attacks = DamageCalculator.Breakdown(gs, skills, cond);

        Assert.Equal(["Offset Rising Slash Lv3", "Tackle (shoulder bash) Lv3", "Strong Charged Slash Lv3", "True Charged Slash Lv3"], attacks.Select(a => a.Name));
        Assert.Equal(2, attacks[3].Repeat);
        Assert.Equal(new Dictionary<string, bool> { ["stamina_full"] = false }, attacks[2].Changes);
        Assert.Empty(attacks[3].Changes);
        Assert.Equal(attacks[3].Affinity - 30, attacks[2].Affinity);
        Assert.Equal("Dark Arts shockwave", Assert.Single(attacks[3].Procs).Name);
        Assert.Empty(attacks[0].Procs); // a rising slash carries no shockwave
        Assert.Equal(r.DamagePerExecution, Sum(attacks), 1e-6);
    }

    [Fact]
    public void PhialExplosionsAreOneLineWithTheirCount()
    {
        var sa = Weapon(WeaponType.SwitchAxe, GogmaFocus.Element, Element.Thunder);
        var cond = Conditions.Default with { AttackProfile = new AttackProfile { Attack = "full-release-slash" } };
        var skills = Skills(null, (SkillNames.CriticalEye, 5), (SkillNames.CriticalElement, 1));
        var attack = Assert.Single(DamageCalculator.Breakdown(sa, skills, cond));
        Assert.Equal(4, attack.Hits.Count);
        Assert.Equal(5, attack.Hits[3].Count);
        Assert.Equal(DamageCalculator.Calculate(sa, skills, cond).DamagePerExecution, attack.Average, 1e-6);
        Assert.All(attack.Hits, h => Assert.True(h.Normal < h.Average && h.Average < h.Crit));
    }

    [Fact]
    public void AHitIsTrueRawTimesItsMotionValueSharpnessAndHitzone()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Attack) with { Affinity = 15 };
        var cond = Conditions.AllOff with { Target = new Target { RawHitzone = 30, ElementHitzone = 0 }, AttackProfile = new AttackProfile { Attack = "charged-slash" } };
        var skills = Skills(null, (SkillNames.CriticalBoost, 5));
        var r = DamageCalculator.Calculate(gs, skills, cond);
        var hit = Assert.Single(Assert.Single(DamageCalculator.Breakdown(gs, skills, cond)).Hits);
        var normal = r.TrueRaw * 176 / 100 * DamageConstants.SharpnessRaw(gs.TopSharpness!.Value) * 30 / 100;
        Assert.Equal(normal, hit.Normal, 1e-9);
        Assert.Equal(normal * r.CriticalMultiplier, hit.Crit, 1e-9);
        Assert.Equal(normal * r.CriticalFactor, hit.Average, 1e-9);
    }

    [Fact]
    public void NegativeAffinityShowsAFeebleHit()
    {
        var gs = Weapon(WeaponType.GreatSword, GogmaFocus.Attack) with { Affinity = -20 };
        var cond = Conditions.AllOff with { AttackProfile = new AttackProfile { Attack = "charged-slash" } };
        var hit = Assert.Single(Assert.Single(DamageCalculator.Breakdown(gs, Skills(), cond)).Hits);
        Assert.Equal(hit.Normal * DamageConstants.NegativeCriticalMultiplier, hit.Crit, 1e-9);
        Assert.True(hit.Crit < hit.Average && hit.Average < hit.Normal);
    }

    [Fact]
    public void TheAverageHitModelIsOneHit()
    {
        var ls = Weapon(WeaponType.LongSword, GogmaFocus.Element, Element.Fire);
        var cond = Conditions.Default;
        var skills = Skills(null, (SkillNames.AttackBoost, 3));
        var attack = Assert.Single(DamageCalculator.Breakdown(ls, skills, cond));
        Assert.Equal("Average hit", Assert.Single(attack.Hits).Name);
        Assert.Equal(DamageCalculator.Calculate(ls, skills, cond).DamagePerExecution, attack.Average, 1e-6);
    }
}
