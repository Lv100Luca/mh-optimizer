using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Tests;

/// <summary>Timed skills (Latent Power, Azure Bolt) scored by their share of the hunt, with defaults by set bonus tier.</summary>
public class UptimeTests
{
    private const double Tol = 1e-9;

    private static ActiveSkills Skills(IEnumerable<(string Skill, int Level)> levels, params (string Set, int Pieces)[] sets)
    {
        var d = levels.ToDictionary(x => x.Skill, x => x.Level);
        return new ActiveSkills(d, d, sets.ToDictionary(x => x.Set, x => x.Pieces), new Dictionary<string, int>());
    }

    private static GogmaWeaponStats Sa => new GogmaWeaponSpec { Type = WeaponType.SwitchAxe, Focus = GogmaFocus.Element, Element = Element.Thunder }.Resolve(TestData.Data);

    /// <summary>The score with the skill on all the time weighted against the score with it off.</summary>
    private static double Expected(ActiveSkills skills, Conditions on, Conditions off, double share) =>
        share * DamageCalculator.Calculate(Sa, skills, on).Total + (1 - share) * DamageCalculator.Calculate(Sa, skills, off).Total;

    [Fact]
    public void ASetUptimeWeightsTheScoreWithAndWithoutTheSkill()
    {
        var skills = Skills([(SkillNames.LatentPower, 5)]);
        var cond = Conditions.Default with { AzureBoltActive = false, LatentPowerActive = true, LatentPowerUptime = 0.25 };
        var on = cond with { LatentPowerUptime = 1 };
        var off = cond with { LatentPowerActive = false };
        Assert.Equal(DamageCalculator.Calculate(Sa, skills, on).Affinity - 50, DamageCalculator.Calculate(Sa, skills, off).Affinity);
        Assert.Equal(Expected(skills, on, off, 0.25), DamageCalculator.Calculate(Sa, skills, cond).Total, Tol);
        Assert.Equal(DamageCalculator.Calculate(Sa, skills, off).Total, DamageCalculator.Calculate(Sa, skills, cond with { LatentPowerUptime = 0 }).Total, Tol);
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(2, 150.0 / 270)]
    [InlineData(4, 210.0 / 330)]
    public void LatentPowerLastsLongerWithThunderousRoar(int roarPieces, double share)
    {
        var skills = Skills([(SkillNames.LatentPower, 3)], (SkillNames.ReyDausVoltage, roarPieces));
        var cond = Conditions.Default with { AzureBoltActive = false, LatentPowerActive = true };
        var expected = Expected(skills, cond with { LatentPowerUptime = 1 }, cond with { LatentPowerActive = false }, share);
        Assert.Equal(expected, DamageCalculator.Calculate(Sa, skills, cond).Total, Tol);
    }

    [Theory]
    [InlineData(2, 0.60)]
    [InlineData(4, 0.85)]
    public void AzureBoltDefaultsToItsTiersUptime(int pieces, double share)
    {
        var skills = Skills([], (SkillNames.LeviathansFury, pieces));
        var cond = Conditions.Default;
        Assert.Null(cond.AzureBoltUptime);
        var expected = Expected(skills, cond with { AzureBoltUptime = 1 }, cond with { AzureBoltActive = false }, share);
        Assert.Equal(expected, DamageCalculator.Calculate(Sa, skills, cond).Total, Tol);
    }

    [Fact]
    public void OffensiveGuardIsWeightedByItsUptime()
    {
        var skills = Skills([(SkillNames.OffensiveGuard, 3)]);
        var cond = Conditions.Default with { AzureBoltActive = false, OffensiveGuardActive = true, OffensiveGuardUptime = 0.25 };
        var on = cond with { OffensiveGuardUptime = 1 };
        var off = cond with { OffensiveGuardActive = false };
        Assert.True(DamageCalculator.Calculate(Sa, skills, on).Total > DamageCalculator.Calculate(Sa, skills, off).Total);
        Assert.Equal(Expected(skills, on, off, 0.25), DamageCalculator.Calculate(Sa, skills, cond).Total, Tol);
        // no uptime set: on all the time, as before there was a choice
        Assert.Single((cond with { OffensiveGuardUptime = null }).UptimeParts(skills));
        Assert.Equal(DamageCalculator.Calculate(Sa, skills, on).Total, DamageCalculator.Calculate(Sa, skills, cond with { OffensiveGuardUptime = null }).Total, Tol);
        Assert.Single(cond.UptimeParts(Skills([(SkillNames.AttackBoost, 5)])));
    }

    [Fact]
    public void BuildsWithoutTheSkillsAreScoredInOnePart()
    {
        var cond = Conditions.Default with { LatentPowerActive = true };
        Assert.Equal(12, cond.UptimeParts().Count); // four Latent Power slices x three Azure Bolt slices
        Assert.Equal(1, cond.UptimeParts().Sum(p => p.Share), Tol);
        Assert.Single(cond.UptimeParts(Skills([(SkillNames.AttackBoost, 5)])));
        // Latent Power without Thunderous Roar: on half the time, off the other half
        var parts = cond.UptimeParts(Skills([(SkillNames.LatentPower, 1)]));
        Assert.Equal(2, parts.Count);
        Assert.Equal(0.5, parts.Single(p => p.Part.LatentPowerActive).Share, Tol);
    }

    [Fact]
    public void AllOnCountsTheTimedSkillsAllTheTime()
    {
        Assert.Single(Conditions.AllOn.UptimeParts());
        Assert.Single(Conditions.AllOnLike(Conditions.Default).UptimeParts());
    }

    [Fact]
    public void ScoreModelWeightsThePartsOfTheTime()
    {
        var data = TestData.Data;
        var cond = Conditions.Default with
        {
            LatentPowerActive = true,
            AttackProfile = new AttackProfile
            {
                Attack = Attacks.Sequence,
                Sequence = [new SequenceStep { Attack = "amped-sword-combo" }, new SequenceStep { Attack = "full-release-slash", Conditions = new() { ["stamina_full"] = false } }],
            },
        };
        var rel = Relevance.Build(Sa, new Dictionary<string, int>(), cond, data);
        Assert.Contains(SkillNames.LatentPower, rel.Skills);
        Assert.Contains(SkillNames.ReyDausVoltage, rel.SetBonuses);
        Assert.Contains(SkillNames.LeviathansFury, rel.SetBonuses);
        var score = ScoreDecomposition.Build(Sa, rel, cond);
        Assert.Equal(2 * 12 * score.HealthSides, score.Sides.Count);
        Assert.Equal(score.HealthSides, score.Sides.Sum(s => s.Weight), 1e-9);
        Assert.True(score.Verify(1000) < 1e-6);
    }

    [Fact]
    public void StepsStillAddUpToTheDamagePerExecution()
    {
        var skills = Skills([(SkillNames.LatentPower, 5), (SkillNames.CriticalElement, 3)], (SkillNames.LeviathansFury, 4));
        var cond = Conditions.Default with
        {
            LatentPowerActive = true,
            AttackProfile = new AttackProfile
            {
                Attack = Attacks.Sequence,
                Sequence = [new SequenceStep { Attack = "amped-sword-combo" }, new SequenceStep { Attack = "full-release-slash", Conditions = new() { ["stamina_full"] = false } }],
            },
        };
        var steps = DamageCalculator.Breakdown(Sa, skills, cond);
        Assert.Equal(DamageCalculator.Calculate(Sa, skills, cond).DamagePerExecution, steps.Sum(s => s.Average * s.Repeat), 1e-6);
    }
}
