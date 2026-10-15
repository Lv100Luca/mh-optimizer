using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

public class BuffTests
{
    private const double Tol = 1e-9;

    private static readonly GogmaWeaponStats Weapon =
        new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, Element = Element.Fire }.Resolve(TestData.Data) with { TrueRaw = 200 };

    private static ActiveSkills Skills(Dictionary<string, int>? sets = null) =>
        new(new Dictionary<string, int>(), new Dictionary<string, int>(), sets ?? new Dictionary<string, int>(), new Dictionary<string, int>());

    private static DamageResult Calc(Dictionary<string, double> buffs, Conditions? basis = null, ActiveSkills? skills = null) =>
        DamageCalculator.Calculate(Weapon, skills ?? Skills(), (basis ?? Conditions.AllOff) with { Buffs = buffs }, trace: false);

    [Fact]
    public void ItemsAddFlatAttack()
    {
        // Mega Demondrug 7 + Might Seed 10 + Demon Powder 10
        var r = Calc(new() { ["mega-demondrug"] = 1, [BuffCatalog.MightSeed] = 1, ["demon-powder"] = 1 });
        Assert.Equal(227, r.TrueRaw, Tol);
    }

    [Fact]
    public void OnlyTheStrongestOfASlotCounts()
    {
        Assert.Equal(207, Calc(new() { ["demondrug"] = 1, ["mega-demondrug"] = 1 }).TrueRaw, Tol);
        Assert.Equal(200 * 1.10, Calc(new() { ["hh-attack-s"] = 1, ["hh-attack-l2"] = 1 }).TrueRaw, Tol);
    }

    [Fact]
    public void TimedBuffsCountAtTheirShareOfTheHunt()
    {
        Assert.Equal(205, Calc(new() { [BuffCatalog.MightSeed] = 0.5 }).TrueRaw, Tol);
        Assert.Equal(200 * (1 + 0.5 * 0.10), Calc(new() { ["hh-attack-l2"] = 0.5 }).TrueRaw, Tol);
        // Caprice ignores the uptime set: about 4% of the time
        Assert.Equal(200 + 15 * BuffCatalog.CapriceUptime, Calc(new() { ["caprice-hi"] = 1 }).TrueRaw, Tol);
    }

    [Fact]
    public void HornMultipliesTheWeaponRawNotTheFlatBonuses()
    {
        var r = Calc(new() { ["hh-attack-l2"] = 1, ["mega-demondrug"] = 1 }, Conditions.AllOff with { Powercharm = true, MealAttack = 5 });
        Assert.Equal(200 * 1.10 + 7 + 6 + 5, r.TrueRaw, Tol);
    }

    [Fact]
    public void MightSeedSharesItsBoostWithProteinFiend()
    {
        var fiendII = Skills(new() { [SkillNames.XuWusVigor] = SkillAggregator.SetTierTwoPieces });
        var cond = Conditions.AllOff with { ProteinFiendActive = true };
        Assert.Equal(230, Calc(new() { [BuffCatalog.MightSeed] = 1 }, cond, fiendII).TrueRaw, Tol);
        // the pill's 25 beats Protein Fiend I's 15 half the time
        var fiendI = Skills(new() { [SkillNames.XuWusVigor] = SkillAggregator.SetTierOnePieces });
        Assert.Equal(215 + 0.5 * 10, Calc(new() { [BuffCatalog.MightPill] = 0.5 }, cond, fiendI).TrueRaw, Tol);
    }

    [Fact]
    public void AffinityAndElementBuffsAtFullUptimeAreAlwaysOn()
    {
        var off = Calc(new());
        var on = Calc(new() { ["hh-affinity"] = 1, ["hh-element"] = 1 });
        Assert.Equal(off.Affinity + 15, on.Affinity);
        Assert.Equal(Math.Min(off.ElementTrue * 1.08, off.ElementCap), on.ElementTrue, Tol);
    }

    [Fact]
    public void AffinityBuffsSplitTheHuntInsteadOfAveraging()
    {
        // at 90% affinity the bubble's +25 hits the cap: half the hunt at 100%, half at 90%, not 100% all the time
        var basis = Conditions.AllOff;
        var w = Weapon with { Affinity = 90 };
        double Total(double uptime) => DamageCalculator.Calculate(w, Skills(), basis with { Buffs = new() { ["hh-bubble"] = uptime } }, trace: false).Total;
        var half = DamageCalculator.Calculate(w, Skills(), basis with { Buffs = new() { ["hh-bubble"] = 0.5 } }, trace: false);
        Assert.Equal(0.5 * Total(1) + 0.5 * Total(0), half.Total, Tol);
        Assert.Equal(95, half.Affinity);
    }

    [Fact]
    public void SplitBuffsWithTheSameUptimeRunTogether()
    {
        Assert.Single(Conditions.AllOff.UptimeParts());
        Assert.Equal(2, (Conditions.AllOff with { Buffs = new() { ["hh-affinity"] = 0.5, ["hh-element"] = 0.5 } }).UptimeParts().Count);
        var parts = (Conditions.AllOff with { Buffs = new() { ["hh-affinity"] = 0.5, ["hh-element"] = 0.75, ["mega-demondrug"] = 1 } }).UptimeParts();
        Assert.Equal(4, parts.Count);
        Assert.Equal(1, parts.Sum(p => p.Share), Tol);
        Assert.All(parts, p => Assert.True(p.Part.Buffs.ContainsKey("mega-demondrug")));
        Assert.Equal(0.5 * 0.75, parts.Where(p => p.Part.Buffs.ContainsKey("hh-affinity") && p.Part.Buffs.ContainsKey("hh-element")).Sum(p => p.Share), Tol);
        // averaged buffs never split
        Assert.Single((Conditions.AllOff with { Buffs = new() { [BuffCatalog.MightPill] = 0.05 } }).UptimeParts());
    }

    [Fact]
    public void PipingHotNeedsLowHealth()
    {
        var buffs = new Dictionary<string, double> { ["piping-hot"] = 1 };
        Assert.Equal(200, Calc(buffs).TrueRaw, Tol);
        Assert.Equal(200 * 1.35, Calc(buffs, Conditions.AllOff with { LowHealth = true }).TrueRaw, Tol);
    }

    [Fact]
    public void UnknownBuffsAndUptimesOutOfRangeAreErrors()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000 },
            Conditions = Conditions.Default with { Buffs = new() { ["nope"] = 1, ["demon-powder"] = 1.5 } },
        };
        var r = RequestLoader.Resolve(request, TestData.Data, Directory.GetCurrentDirectory());
        Assert.Contains(r.Errors, e => e.Contains("'nope'"));
        Assert.Contains(r.Errors, e => e.Contains("conditions.buffs.demon-powder"));
    }

    [Fact]
    public void BuffsRoundTripThroughJsonAndPresets()
    {
        var c = Conditions.Default with { Buffs = new() { ["mega-demondrug"] = 1, ["hh-attack-l"] = 0.75 } };
        var json = System.Text.Json.JsonSerializer.Serialize(c, RequestFiles.WriteOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<Conditions>(json, RequestFiles.WriteOptions)!;
        Assert.True(ConditionPresets.Same(c, back));
        Assert.Equal([nameof(Conditions.Buffs)], ConditionPresets.Overrides(c, Conditions.Default));
    }
}
