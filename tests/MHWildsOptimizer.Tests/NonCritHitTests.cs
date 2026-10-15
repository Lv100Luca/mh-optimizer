using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

/// <summary>Hits that cannot crit, ignore the hitzone or deal fixed damage: Gunlance shells, Charge Blade phial bursts, Hunting Horn soundwaves.</summary>
public class NonCritHitTests(ITestOutputHelper output)
{
    private const double Tol = 1e-9;

    private static GogmaWeaponStats Weapon(WeaponType type, GogmaFocus focus, Element element = Element.None) =>
        new GogmaWeaponSpec { Type = type, Focus = focus, Element = element }.Resolve(TestData.Data);

    private static ActiveSkills Skills(params (string Skill, int Level)[] levels)
    {
        var d = levels.ToDictionary(x => x.Skill, x => x.Level);
        return new ActiveSkills(d, d, new Dictionary<string, int>(), new Dictionary<string, int>());
    }

    private static Conditions On(string attack, double rawHitzone = 50) =>
        Conditions.Default with { AttackProfile = new AttackProfile { Attack = attack }, Target = new Target { RawHitzone = rawHitzone, ElementHitzone = 20 } };

    [Fact]
    public void AShellIgnoresSharpnessHitzoneAndCritsAndAddsFixedFire()
    {
        var gl = Weapon(WeaponType.Gunlance, GogmaFocus.Attack);
        var p = On("shell").Attack(gl);
        Assert.Equal("Shell (Normal shelling Lv3)", p.Name);
        Assert.Equal(9, p.TotalMv, Tol);
        Assert.Equal(0, p.RawFactor, Tol);
        Assert.Equal(100.0 / 50, p.NoCritRawFactor, Tol);
        Assert.Equal(8 * 100.0 / 50, p.FixedDamage, Tol);

        // damage of one shell: true raw x 9 / 100 at hitzone 100, plus 8 fire, whatever the affinity
        foreach (var skills in new[] { Skills(), Skills((SkillNames.CriticalEye, 5), (SkillNames.CriticalBoost, 5)) })
        {
            var r = DamageCalculator.Calculate(gl, skills, On("shell"));
            Assert.Equal(r.TrueRaw * 9 / 100 + 8, r.DamagePerExecution, 1e-6);
            Assert.Equal(r.DamagePerExecution, DamageCalculator.Breakdown(gl, skills, On("shell")).Sum(a => a.Average * a.Repeat), 1e-6);
        }
    }

    [Theory]
    [InlineData(GogmaFocus.Attack, 9, 8)]
    [InlineData(GogmaFocus.Affinity, 12, 16)]
    [InlineData(GogmaFocus.Element, 21, 11)]
    public void TheShellingFollowsTheFocus(GogmaFocus focus, double mv, double fire)
    {
        var hit = Assert.Single(AttackProfile.ResolveHits(Weapon(WeaponType.Gunlance, focus, Element.Fire), new Target(), Attacks.Find(WeaponType.Gunlance, "shell")!.Hits));
        Assert.Equal((mv, fire, true), (hit.Mv, hit.Fixed, hit.NoCrit));
    }

    [Fact]
    public void PhialBurstsFollowThePhialAndDoNotCrit()
    {
        var impact = AttackProfile.ResolveHits(Weapon(WeaponType.ChargeBlade, GogmaFocus.Attack), new Target(), Attacks.Find(WeaponType.ChargeBlade, "saed")!.Hits);
        var element = AttackProfile.ResolveHits(Weapon(WeaponType.ChargeBlade, GogmaFocus.Element, Element.Ice), new Target(), Attacks.Find(WeaponType.ChargeBlade, "saed")!.Hits);
        var (i, e) = (impact[^1], element[^1]);
        Assert.Equal((28.0, 5, true), (i.Mv, i.Count, i.NoCrit));
        Assert.Equal(0, i.ElementModifier, Tol);
        Assert.Equal(0, e.Mv, Tol);
        Assert.Equal(8.2 * DamageConstants.SharpnessElement(SharpnessColor.Yellow), e.ElementModifier, Tol);
        Assert.True(e.NoCrit);
    }

    [Fact]
    public void CritSkillsOnlyRaiseTheHitsThatCanCrit()
    {
        var hh = Weapon(WeaponType.HuntingHorn, GogmaFocus.Element, Element.Thunder);
        var crit = Skills((SkillNames.CriticalEye, 5), (SkillNames.CriticalBoost, 5), (SkillNames.CriticalElement, 3));
        double Score(string attack, ActiveSkills skills) => DamageCalculator.Calculate(hh, skills, On(attack)).Total;
        Assert.Equal(Score("perform-soundwave", Skills()), Score("perform-soundwave", crit), 1e-9);
        Assert.True(Score("forward-smash", crit) > Score("forward-smash", Skills()));

        // the timed Reverb mixes both: the breakdown still adds up
        var r = DamageCalculator.Calculate(hh, crit, On("reverb-timed"));
        Assert.Equal(r.DamagePerExecution, DamageCalculator.Breakdown(hh, crit, On("reverb-timed")).Sum(a => a.Average * a.Repeat), 1e-6);
    }

    [Fact]
    public void APerformIsTheHornHitAndAShockwaveThatIgnoresSharpness()
    {
        var hh = Weapon(WeaponType.HuntingHorn, GogmaFocus.Element, Element.Thunder);
        var hits = AttackProfile.ResolveHits(hh, new Target { RawHitzone = 100 }, Attacks.Find(WeaponType.HuntingHorn, "forward-perform")!.Hits);
        Assert.Equal([35.0, 27.0], hits.Select(h => h.Mv));
        Assert.Equal(DamageConstants.SharpnessRaw(hh.TopSharpness!.Value), hits[0].RawModifier, Tol);
        Assert.Equal((1.0, 1.0, true), (hits[1].RawModifier, hits[1].ElementModifier, hits[1].NoCrit));
        Assert.Equal(60, Assert.Single(Attacks.Find(WeaponType.HuntingHorn, "back-perform")!.Hits, h => !h.NoCrit).Mv);
    }

    [Fact]
    public void AnElementalEchoWaveAddsFixedElementAtItsHitzone()
    {
        var hh = Weapon(WeaponType.HuntingHorn, GogmaFocus.Attack);
        var r = DamageCalculator.Calculate(hh, Skills((SkillNames.CriticalEye, 5)), On("echo-wave-dragon-1"));
        // 45 MV without sharpness or crits at raw hitzone 50, plus 60 dragon at element hitzone 20
        Assert.Equal(r.TrueRaw * 45 / 100 * 0.5 + 60 * 0.2, r.DamagePerExecution, 1e-6);
    }

    [Fact]
    public void TheEchoDragonLoopAddsUp()
    {
        var hh = Weapon(WeaponType.HuntingHorn, GogmaFocus.Element, Element.Dragon);
        var preset = Attacks.SequencePresets.Single(p => p.Id == "echo-dragon-loop");
        Assert.Empty(new AttackProfile { Attack = Attacks.Sequence, Sequence = preset.Steps }.Validate(WeaponType.HuntingHorn));
        var cond = Conditions.Default with { AttackProfile = new AttackProfile { Attack = Attacks.Sequence, Sequence = preset.Steps } };
        var skills = Skills((SkillNames.CriticalEye, 5));
        Assert.Equal(DamageCalculator.Calculate(hh, skills, cond).DamagePerExecution, DamageCalculator.Breakdown(hh, skills, cond).Sum(a => a.Average * a.Repeat), 1e-6);
    }

    [Theory]
    [InlineData(WeaponType.HuntingHorn, GogmaFocus.Element, Element.Dragon, "echo-wave-dragon-3")]
    [InlineData(WeaponType.Gunlance, GogmaFocus.Element, Element.Fire, "rising-slash-shell-combo")]
    [InlineData(WeaponType.ChargeBlade, GogmaFocus.Element, Element.Ice, "aed-saed")]
    [InlineData(WeaponType.ChargeBlade, GogmaFocus.Attack, Element.None, "saed")]
    [InlineData(WeaponType.HuntingHorn, GogmaFocus.Affinity, Element.Thunder, "reverb-timed")]
    public void TheScoreModelMatchesTheCalculator(WeaponType type, GogmaFocus focus, Element element, string attack)
    {
        var weapon = Weapon(type, focus, element);
        var cond = Conditions.AllOn with { AttackProfile = new AttackProfile { Attack = attack }, Target = new Target { RawHitzone = 40, ElementHitzone = 25 } };
        var rel = Relevance.Build(weapon, new Dictionary<string, int> { ["Weakness Exploit"] = 3 }, cond, TestData.Data);
        var score = ScoreDecomposition.Build(weapon, rel, cond);
        Assert.True(score.Verify(1000) < 1e-6);
    }

    [Fact]
    public void CpSatFindsGunlanceBuildsAtLeastAsGoodAsTheBeam()
    {
        var data = TestData.Data;
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Spec = new GogmaWeaponSpecInput { Type = "gunlance", Focus = GogmaFocus.Element, Element = Element.Fire, AttackParts = 3 } },
            TargetSkills = new() { ["Weakness Exploit"] = 3 },
            Conditions = Conditions.Default with { AttackProfile = new AttackProfile { Attack = "rising-slash-shell-combo" } },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 2, MinRarity = 8, RequireWeaponCoreSkills = false },
        };
        var resolved = RequestLoader.Resolve(request, data, Directory.GetParent(Core.Data.GameDataLoader.FindDataDirectory())!.FullName);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        var beam = new Optimizer(data, resolved).Run().PairResults[0];
        var cp = new Optimizer(data, resolved with { Options = resolved.Options with { Engine = OptimizerEngine.CpSat } }).Run().PairResults[0];
        output.WriteLine($"beam {beam.BestScore:0.000}, cp-sat {cp.BestScore:0.000}");
        Assert.NotEmpty(cp.Builds);
        Assert.True(cp.BestScore >= beam.BestScore - 1e-6, $"cp-sat {cp.BestScore} < beam {beam.BestScore}");
    }
}
