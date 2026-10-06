using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

public class DamageCalculatorTests
{
    private const double Tol = 0.01;

    [Fact]
    public void BareGreatSwordAttackFocus()
    {
        // raw 200 + 15 (parts) = 215, affinity -10%, white sharpness: 215 * 1.32 * (1 - 0.10 * 0.25)
        var loadout = new Loadout { Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }, TestData.Data) };
        var r = DamageCalculator.Calculate(loadout, TestData.Data, Conditions.AllOff);
        Assert.Equal(215, r.BaseTrueRaw);
        Assert.Equal(-10, r.Affinity);
        Assert.Equal(SharpnessColor.White, r.Sharpness);
        Assert.Equal(215 * 1.32 * 0.975, r.EffectiveRaw, Tol);
        Assert.Equal(0, r.EffectiveElement);
        Assert.Equal(r.EffectiveRaw, r.Total, Tol);
    }

    [Fact]
    public void CriticalSkillsRaiseEffectiveRaw()
    {
        var data = TestData.Data;
        var crit = data.Decoration("Critical Jewel III [3]");
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Affinity, AttackParts = 0 }, TestData.Data,
                [crit, crit, data.Decoration("Expert Jewel III [3]")]),
        };
        // raw 180, affinity 15 + 15 (parts) + 12 (Critical Eye 3) = 42, Critical Boost capped at 5 -> 1.40
        var r = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        Assert.Equal(180, r.BaseTrueRaw);
        Assert.Equal(42, r.Affinity);
        Assert.Equal(1.40, r.CriticalMultiplier);
        Assert.Equal(180 * 1.32 * (1 + 0.42 * 0.40), r.EffectiveRaw, Tol);
    }

    [Fact]
    public void TogglesOnlyMatterWhenTheLoadoutHasTheSkill()
    {
        var loadout = new Loadout { Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }, TestData.Data) };
        var off = DamageCalculator.Calculate(loadout, TestData.Data, Conditions.AllOff);
        var on = DamageCalculator.Calculate(loadout, TestData.Data, Conditions.Default);
        Assert.Equal(off.Total, on.Total, Tol);
    }

    [Fact]
    public void AgitatorAppliesOnlyWhenEnraged()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }, TestData.Data),
            Talisman = data.Charm("Challenger Charm III"), // Agitator 3: +12 attack, +7% affinity
        };
        var calm = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        var enraged = DamageCalculator.Calculate(loadout, data, Conditions.AllOff with { MonsterEnraged = true });
        Assert.Equal(215, calm.TrueRaw, Tol);
        Assert.Equal(227, enraged.TrueRaw, Tol);
        Assert.Equal(-10, calm.Affinity);
        Assert.Equal(-3, enraged.Affinity);
        Assert.True(enraged.EffectiveRaw > calm.EffectiveRaw);
    }

    [Fact]
    public void ElementIsCappedAndBoostedByMutualHostility()
    {
        var data = TestData.Data;
        var spec = new GogmaWeaponSpec
        {
            Type = WeaponType.GreatSword, Focus = GogmaFocus.Element, Element = Element.Fire,
            SetBonus = "Gogmapocalypse",
        };
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(spec, TestData.Data),
            Head = new EquippedArmor(TestData.PieceOf("Gogmazios α", ArmorPieceKind.Head)),
        };
        // base element 450 + 30 + 50 = 530 display = 53 true; white sharpness element x1.15
        var calm = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        Assert.Equal(53, calm.BaseElementTrue, Tol);
        Assert.Equal(53 * 1.15, calm.EffectiveElement, Tol);
        Assert.Equal(Math.Max(53 * 2.3, 53 + 40), calm.ElementCap, Tol);

        var enraged = DamageCalculator.Calculate(loadout, data, Conditions.AllOff with { MonsterEnraged = true });
        Assert.Equal(53 * 1.2 + 2, enraged.ElementTrue, Tol);
        Assert.Equal((53 * 1.2 + 2) * 1.15, enraged.EffectiveElement, Tol);
    }

    [Fact]
    public void BurstUsesThePerWeaponTable()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Attack, Element = Element.Fire }, TestData.Data),
            Talisman = data.Charm("Chain Charm III"), // Burst 3
        };
        var off = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        var on = DamageCalculator.Calculate(loadout, data, Conditions.AllOff with { BurstActive = true });
        Assert.Equal(off.TrueRaw + 12, on.TrueRaw, Tol);           // LS Burst 3: +12 attack
        Assert.Equal(off.ElementTrue + 10, on.ElementTrue, Tol);   // LS Burst 3: +10 true element
    }

    [Fact]
    public void SkillLimitsCapTheValuedLevel()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.LongSword, Focus = GogmaFocus.Attack, Element = Element.Fire }, data),
            Talisman = data.Charm("Chain Charm III"), // Burst 3
        };
        var burstOn = Conditions.AllOff with { BurstActive = true };
        var full = DamageCalculator.Calculate(loadout, data, burstOn);
        var capped = DamageCalculator.Calculate(loadout, data, burstOn with { SkillLimits = new() { ["Burst"] = 1 } });
        var excluded = DamageCalculator.Calculate(loadout, data, burstOn with { SkillLimits = new() { ["Burst"] = 0 } });
        var off = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);

        Assert.Equal(off.TrueRaw + 12, full.TrueRaw, Tol);     // LS Burst 3
        Assert.Equal(off.TrueRaw + 8, capped.TrueRaw, Tol);    // valued as Burst 1
        Assert.Equal(off.TrueRaw, excluded.TrueRaw, Tol);
        Assert.Equal(off.Total, excluded.Total, Tol);
    }

    private static readonly Conditions ProcsOnly = Conditions.AllOff with
    {
        ProcDamage = true,
        AttackProfile = new AttackProfile { HitsPerMinute = 20, AverageMv = 100, ChargedLv3Share = 0.5 }, // 3 s per hit
    };

    [Fact]
    public void AzureBoltBurstIsSpreadOverItsCooldown()
    {
        var data = TestData.Data;
        var spec = new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, SetBonus = "Leviathan's Fury" };
        Loadout Build(params Decoration?[] decos) => new()
        {
            Weapon = new EquippedWeapon(spec, data, decos),
            Head = new EquippedArmor(TestData.PieceOf("Lagiacrus α", ArmorPieceKind.Head)),
            Chest = new EquippedArmor(TestData.PieceOf("Lagiacrus α", ArmorPieceKind.Chest)),
            Arms = new EquippedArmor(TestData.PieceOf("Lagiacrus α", ArmorPieceKind.Arms)),
        };
        var off = DamageCalculator.Calculate(Build(), data, Conditions.AllOff);
        var on = DamageCalculator.Calculate(Build(), data, ProcsOnly);
        // tier II burst 60 + 200 thunder, one per 30 s; at 3 s per hit that is 0.1 bursts per 100-MV hit
        Assert.Equal(0, off.ProcDamage);
        Assert.Equal(26, on.ProcDamage, Tol);
        Assert.Equal(off.Total + 26, on.Total, Tol);

        // Thunder Attack 3 scales the thunder part on a non-thunder weapon: 60 + 200 * 1.2 + 6 = 306
        var bolt = DamageCalculator.Calculate(Build(data.Decoration("Bolt Jewel III [3]")), data, ProcsOnly);
        Assert.Equal(30.6, bolt.ProcDamage, Tol);
    }

    [Fact]
    public void DarkArtsShockwaveRidesOnGreatSwordChargedSlashes()
    {
        var data = TestData.Data;
        Loadout Build(WeaponType type) => new()
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = type, Focus = GogmaFocus.Attack, SetBonus = "Soul of the Dark Knight" }, data),
            Head = new EquippedArmor(TestData.PieceOf("Bale Armor α", ArmorPieceKind.Head)),
        };
        var gs = DamageCalculator.Calculate(Build(WeaponType.GreatSword), data, ProcsOnly);
        // half the hits add a 30 MV raw shockwave plus 6 true dragon (white sharpness x1.15), no red health needed
        Assert.Equal(0.5 * (gs.EffectiveRaw * 0.30 + 6 * 1.15), gs.ProcDamage, Tol);

        var ls = DamageCalculator.Calculate(Build(WeaponType.LongSword), data, ProcsOnly);
        Assert.Equal(0, ls.ProcDamage);
    }

    [Fact]
    public void ProcsPerHitRespectChanceAndCooldown()
    {
        var gs = new ResolvedAttackProfile(20, 100, 0); // 3 s per hit
        Assert.Equal(0.1, gs.ProcsPerHit(30), Tol);
        Assert.Equal(1.0, gs.ProcsPerHit(2), Tol);       // Bad Blood is ready on every hit
        Assert.Equal(0.33, gs.ProcsPerHit(2.4, 0.33), Tol);
        var ls = new ResolvedAttackProfile(50, 35, 0);   // 1.2 s per hit: Scorcher rolls every other hit
        Assert.Equal(0.33 * 0.5, ls.ProcsPerHit(2.4, 0.33), Tol);
        Assert.Equal(new ResolvedAttackProfile(30, 120, 0.3), new AttackProfile { HitsPerMinute = 30 }.Resolve(WeaponType.GreatSword));
    }

    [Fact]
    public void BurstTableMatchesDecodedGameData()
    {
        Assert.Equal(new DamageConstants.BurstBoost(18, 20), DamageConstants.Burst(WeaponType.GreatSword, 5));
        Assert.Equal(new DamageConstants.BurstBoost(18, 14), DamageConstants.Burst(WeaponType.LongSword, 5));
        Assert.Equal(new DamageConstants.BurstBoost(18, 12), DamageConstants.Burst(WeaponType.DualBlades, 5));
        Assert.Equal(new DamageConstants.BurstBoost(10, 0), DamageConstants.Burst(WeaponType.LightBowgun, 5));
        Assert.Equal(new DamageConstants.BurstBoost(5, 5), DamageConstants.BurstFirstHit(WeaponType.GreatSword));
        Assert.Equal(new DamageConstants.BurstBoost(10, 8), DamageConstants.Burst(WeaponType.GreatSword, 1));
        Assert.Equal(5, DamageConstants.BurstDurationSeconds(WeaponType.GreatSword));
        Assert.Equal(3, DamageConstants.BurstDurationSeconds(WeaponType.DualBlades));
    }

    [Fact]
    public void GoreSetGivesFrenzyAffinityAndTierTwoAttack()
    {
        var data = TestData.Data;
        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack, SetBonus = "Gore Magala's Tyranny" }, TestData.Data),
            Head = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Head)),
            Chest = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Chest)),
            Arms = new EquippedArmor(TestData.PieceOf("Gore α", ArmorPieceKind.Arms)),
        };
        var off = DamageCalculator.Calculate(loadout, data, Conditions.AllOff);
        var overcome = DamageCalculator.Calculate(loadout, data, Conditions.AllOff with { FrenzyOvercome = true });
        Assert.Equal(off.TrueRaw + 15 - 10, overcome.TrueRaw, Tol);      // tier II: +10 while infected, +15 after overcoming
        Assert.Equal(off.Affinity + 15 - 0 + AntivirusBonus(data, loadout), overcome.Affinity);
    }

    private static int AntivirusBonus(Core.Data.GameData data, Loadout loadout)
    {
        var lv = SkillAggregator.Aggregate(loadout, data).Level("Antivirus");
        return lv switch { 0 => 0, 1 => 3, 2 => 6, _ => 10 };
    }
}
