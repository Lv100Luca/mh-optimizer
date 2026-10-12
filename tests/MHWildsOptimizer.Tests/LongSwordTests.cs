using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

/// <summary>Long Sword attacks (motion value sheet 1.040) and the Spirit Gauge multiplier.</summary>
public class LongSwordTests
{
    private static GogmaWeaponStats Ls => new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Attack }.Resolve(TestData.Data);

    private static readonly ActiveSkills None = new(new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>());

    private static ResolvedAttackProfile Resolve(string attack, SpiritGauge gauge) =>
        (Conditions.Default with { SpiritGauge = gauge, AttackProfile = new AttackProfile { Attack = attack } }).Attack(Ls);

    [Fact]
    public void TheDefaultAttackIsTheCrimsonSlashLoopAtARedGauge()
    {
        Assert.Equal("crimson-rising-loop", Attacks.DefaultFor(WeaponType.LongSword));
        Assert.Equal(SpiritGauge.Red, Conditions.Default.SpiritGauge);
        var a = Conditions.Default.Attack(Ls);
        Assert.Equal(33 + 16 + 18, a.TotalMv, 1e-9);
    }

    [Fact]
    public void TheGaugeMultipliesTheRawOfHitsThatUseIt()
    {
        var none = Resolve("ls-rising-slash", SpiritGauge.None);
        foreach (var (gauge, mult) in new[] { (SpiritGauge.White, 1.02), (SpiritGauge.Yellow, 1.04), (SpiritGauge.Red, 1.1) })
            Assert.Equal(none.RawFactor * mult, Resolve("ls-rising-slash", gauge).RawFactor, 1e-9);
        Assert.Equal(none.ElementFactor, Resolve("ls-rising-slash", SpiritGauge.Red).ElementFactor, 1e-9); // raw only
    }

    [Fact]
    public void HitsWithTheirOwnGaugeIgnoreTheConditions()
    {
        // Crimson moves only exist at red; the red Helm Breaker uses the yellow multiplier
        Assert.Equal(Resolve("crimson-slash-i", SpiritGauge.Red).RawFactor, Resolve("crimson-slash-i", SpiritGauge.None).RawFactor, 1e-9);
        var helm = Resolve("spirit-helm-breaker-red", SpiritGauge.Red);
        var sharp = DamageConstants.SharpnessRaw(Ls.TopSharpness!.Value);
        Assert.Equal(sharp * 1.04, helm.RawFactor, 1e-9);
        Assert.Equal(7, helm.Hits, 1e-9);
    }

    [Fact]
    public void TheIaiCounterLoopIsTheCounterAndFourLoops()
    {
        var preset = Attacks.SequencePresetsFor(WeaponType.LongSword).Single(p => p.Id == "iai-counter-crimson-loop");
        var seq = DamageCalculator.Calculate(Ls, None, Conditions.Default with { AttackProfile = new AttackProfile { Attack = Attacks.Sequence, Sequence = preset.Steps } });
        var iai = DamageCalculator.Calculate(Ls, None, Conditions.Default with { AttackProfile = new AttackProfile { Attack = "iai-spirit-slash-red" } });
        var loop = DamageCalculator.Calculate(Ls, None, Conditions.Default with { AttackProfile = new AttackProfile { Attack = "crimson-rising-loop" } });
        Assert.Equal(39 + 86 + 4 * (33 + 16 + 18), seq.Attack.TotalMv, 1e-9);
        Assert.Equal(iai.DamagePerExecution + 4 * loop.DamagePerExecution, seq.DamagePerExecution, 1e-6);
    }

    [Fact]
    public void StepsAddUpWithTheGauge()
    {
        var cond = Conditions.Default with { SpiritGauge = SpiritGauge.Yellow, AttackProfile = new AttackProfile { Attack = "helm-breaker-release" } };
        var steps = DamageCalculator.Breakdown(Ls, None, cond);
        Assert.Equal(DamageCalculator.Calculate(Ls, None, cond).DamagePerExecution, steps.Sum(s => s.Average * s.Repeat), 1e-6);
    }
}
