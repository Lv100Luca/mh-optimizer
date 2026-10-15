using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// Numeric constants of the damage model. Primary source: the game data (PlayerStatusParam, PlayerSkillParam, SkillData),
/// see data/damage_model.json for provenance and VERIFY flags. All values are in true units (true raw, true element).
/// </summary>
public static class DamageConstants
{
    public const double CriticalMultiplier = 1.25;
    public const double NegativeCriticalMultiplier = 0.75;
    public const int AffinityCap = 100;

    /// <summary>Element after skills is capped at max(base * 2.3, base + 40) true element.</summary>
    public const double ElementCapRate = 2.3;
    public const double ElementCapAdd = 40;

    /// <summary>Overcoming Frenzy grants +15% affinity for 60 s (what Black Eclipse I effectively gives).</summary>
    public const int FrenzyOvercomeAffinity = 15;

    /// <summary>
    /// Powercharm in the item pouch: flat attack after the percentage bonuses, like a meal's. Measured on the status screen
    /// (2026-10-08): a bare 233-raw weapon shows 239 with it, and every reading with skills fits the model + 6 (+ the meal).
    /// </summary>
    public const int PowercharmAttack = 6;

    /// <summary>Weak point for Weakness Exploit = hitzone >= 45. At hitzone 100 every hit is a weak-point hit.</summary>
    public const int WeakPointHitzone = 45;

    public static double SharpnessRaw(SharpnessColor color) => color switch
    {
        SharpnessColor.Red => 0.50,
        SharpnessColor.Orange => 0.75,
        SharpnessColor.Yellow => 1.00,
        SharpnessColor.Green => 1.05,
        SharpnessColor.Blue => 1.20,
        SharpnessColor.White => 1.32,
        SharpnessColor.Purple => 1.39,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    public static double SharpnessElement(SharpnessColor color) => color switch
    {
        SharpnessColor.Red => 0.25,
        SharpnessColor.Orange => 0.50,
        SharpnessColor.Yellow => 0.75,
        SharpnessColor.Green => 1.00,
        SharpnessColor.Blue => 1.0625,
        SharpnessColor.White => 1.15,
        SharpnessColor.Purple => 1.25,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    /// <summary>Critical hit multiplier for a Critical Boost level (0 = unskilled).</summary>
    public static double CriticalBoost(int level) => level switch
    {
        <= 0 => CriticalMultiplier,
        1 => 1.28,
        2 => 1.31,
        3 => 1.34,
        4 => 1.37,
        _ => 1.40,
    };

    public static bool UsesHeavyCriticalElement(WeaponType type) =>
        type is WeaponType.GreatSword or WeaponType.Hammer or WeaponType.HuntingHorn;

    /// <summary>Element multiplier on critical hits with Critical Element (1.0 without the skill).</summary>
    public static double CriticalElement(WeaponType type, int level)
    {
        if (level <= 0) return 1.0;
        var lv = Math.Min(level, 3);
        return UsesHeavyCriticalElement(type)
            ? lv switch { 1 => 1.07, 2 => 1.14, _ => 1.21 }
            : lv switch { 1 => 1.05, 2 => 1.10, _ => 1.15 };
    }

    public static bool UsesHeavyCoalescence(WeaponType type) =>
        type is WeaponType.GreatSword or WeaponType.Hammer or WeaponType.HuntingHorn
            or WeaponType.Gunlance or WeaponType.SwitchAxe or WeaponType.ChargeBlade;

    public static double Coalescence(WeaponType type, int level)
    {
        if (level <= 0) return 1.0;
        var lv = Math.Min(level, 3);
        return UsesHeavyCoalescence(type) ? 1.0 + 0.10 * lv : 1.0 + 0.05 * lv;
    }

    public static double ChargeMaster(int level) => level switch { <= 0 => 1.0, 1 => 1.15, 2 => 1.20, _ => 1.25 };

    public static double ElementalAbsorptionPercent(int level) => level switch { <= 0 => 1.0, 1 => 1.05, 2 => 1.10, _ => 1.15 };

    /// <summary>
    /// Flat true element from Elemental Absorption: first value per level of PlayerSkillParam _ElementConvertWp??Data
    /// (matches game8's +50/80/100, +30/40/50, +40/50/60 display; the two higher values per level have no known trigger).
    /// </summary>
    public static int ElementalAbsorptionFlat(WeaponType type, int level)
    {
        if (level <= 0) return 0;
        var lv = Math.Min(level, 3);
        return type switch
        {
            WeaponType.GreatSword or WeaponType.HuntingHorn => lv switch { 1 => 5, 2 => 8, _ => 10 },
            WeaponType.DualBlades or WeaponType.Bow or WeaponType.HeavyBowgun or WeaponType.LightBowgun => lv switch { 1 => 3, 2 => 4, _ => 5 },
            _ => lv switch { 1 => 4, 2 => 5, _ => 6 },
        };
    }

    /// <summary>Dark Arts (Soul of the Dark Knight I): element multiplier while red health exists.</summary>
    public static double DarkArts(WeaponType type) =>
        type is WeaponType.GreatSword or WeaponType.Hammer or WeaponType.HeavyBowgun or WeaponType.LightBowgun ? 1.20 : 1.14;

    /// <summary>Element attack skill (Fire/Water/Thunder/Ice/Dragon Attack): element x percent + flat true element.</summary>
    public static (double Percent, int Flat) ElementAttack(int level) => level switch
    {
        <= 0 => (1.0, 0),
        1 => (1.0, 4),
        2 => (1.10, 5),
        _ => (1.20, 6),
    };

    /// <summary>
    /// Switch Axe phials on sword-mode slashes: Power phial raw x1.17, Element phial element x1.45 (wiggler.pet Wilds Switch Axe guide).
    /// VERIFY: single source; whether they also boost the phial explosions is untested (the model assumes not).
    /// </summary>
    public const double PowerPhialRaw = 1.17;
    public const double ElementPhialElement = 1.45;

    // ---------------- bow ----------------

    /// <summary>
    /// Close-range coating: raw x1.4 on coated arrows, at every charge level (datamine Wp11 GlobalActionParam
    /// CloseBottleAttackUpRate 1.4, unchanged since launch). It also shortens the arrows' range (x0.6). Raw only: the field is an
    /// attack rate and no element field exists (VERIFY in game).
    /// </summary>
    public const double CloseRangeCoatingRaw = 1.4;
    /// <summary>Power coating: raw x1.35 on coated arrows (StrongBottleAttackUpRate, 1.30 until TU4 / 1.040). Raw only, like Close-range.</summary>
    public const double PowerCoatingRaw = 1.35;
    /// <summary>
    /// Critical distance: arrows inside their sweet spot deal raw x1.1 (datamine shell params; x0.8 too close, x0.2 too far,
    /// x0.5 too far for Thousand Dragons). Arrows without a window (Tracer, Arc Shot) and Tracer-guided shots stay at 1.0.
    /// The model assumes every shot lands inside the window. Raw only (an attack rate; VERIFY).
    /// </summary>
    public const double CriticalDistanceRaw = 1.1;

    /// <summary>
    /// Pierce coating: damage multiplier of an arrow's 1st, 2nd, ... 8th hit as it passes through the monster (datamine
    /// Wp11PenetrateBottle_MultiHitCurve; status buildup has a curve of its own). How many hits land depends on the monster's size
    /// and the angle. Applied to raw and element alike: a "damage" curve next to a separate status curve suggests so (VERIFY).
    /// </summary>
    public static readonly double[] PierceHitMultipliers = [1.2, 1.2, 0.9, 0.5, 0.3, 0.15, 0.1, 0.05];
    /// <summary>
    /// Pierce hits per arrow when the attack profile names none: a large monster. The one count found (Steam, pre-TU1) was about
    /// 1.9 hits per arrow on Nerscylla, a mid-sized monster; small monsters take 1-2.
    /// </summary>
    public const int DefaultPierceHits = 3;

    /// <summary>Normal Shots, Spread/Power Shots, Piercing Shots: x1.05 on the arrows they name (kiranico 105/105).</summary>
    public const double ShotSkillMultiplier = 1.05;

    /// <summary>Special Ammo Boost on Dragon Piercer, Thousand Dragons and the Tracer: x1.10 / x1.20 (kiranico 110/120).</summary>
    public static double SpecialAmmoBoost(int level) => level switch { <= 0 => 1.0, 1 => 1.10, _ => 1.20 };

    /// <summary>Long Sword Spirit Gauge: raw x1.02 / x1.04 / x1.1 at white / yellow / red (Game8, Icy Veins; not datamined).</summary>
    public static double SpiritGaugeRaw(SpiritGauge gauge) => gauge switch
    {
        SpiritGauge.White => 1.02,
        SpiritGauge.Yellow => 1.04,
        SpiritGauge.Red => 1.1,
        _ => 1.0,
    };

    /// <summary>Shelling level of the Gogma Artian gunlances (all three shelling types, wilds.mhdb.io); weapons entered as plain stats use it too.</summary>
    public const int ArtianShellLevel = 3;

    // MV sheet 1.040, "GL Shelling" tab: per shelling type and shot, Lv1..4 of (raw MV that ignores the hitzone, fixed fire damage)
    private static readonly Dictionary<(GunlanceShelling, GunlanceShot), (double Mv, double Fire)[]> ShellTable = new()
    {
        [(GunlanceShelling.Normal, GunlanceShot.Shell)] = [(7, 6), (8, 7), (9, 8), (10, 9)],
        [(GunlanceShelling.Normal, GunlanceShot.FullburstShell)] = [(9.8, 6), (11.2, 7), (12.6, 8), (14, 9)],
        [(GunlanceShelling.Normal, GunlanceShot.WyrmstakeFullburstShell)] = [(8.75, 6), (10, 7), (11.25, 8), (12.5, 9)],
        [(GunlanceShelling.Normal, GunlanceShot.ChargedShell)] = [(12.6, 6), (14.4, 7), (16.2, 8), (18, 9)],
        [(GunlanceShelling.Normal, GunlanceShot.Wyvernfire)] = [(39, 22), (43, 24), (47, 26), (52, 29)],
        [(GunlanceShelling.Normal, GunlanceShot.WyrmstakeExplosion)] = [(23, 19), (25, 21), (28, 23), (31, 25)],
        [(GunlanceShelling.Long, GunlanceShot.Shell)] = [(10, 14), (11, 15), (12, 16), (13, 17)],
        [(GunlanceShelling.Long, GunlanceShot.FullburstShell)] = [(14, 14), (15.4, 15), (16.8, 16), (18.2, 17)],
        [(GunlanceShelling.Long, GunlanceShot.WyrmstakeFullburstShell)] = [(11, 14), (12.1, 15), (13.2, 16), (14.3, 17)],
        [(GunlanceShelling.Long, GunlanceShot.ChargedShell)] = [(18, 14), (19.8, 15), (21.6, 16), (23.4, 17)],
        [(GunlanceShelling.Long, GunlanceShot.Wyvernfire)] = [(39, 22), (43, 24), (47, 26), (52, 29)],
        [(GunlanceShelling.Long, GunlanceShot.WyrmstakeExplosion)] = [(32, 19), (37, 21), (42, 23), (49, 25)],
        [(GunlanceShelling.Wide, GunlanceShot.Shell)] = [(17, 9), (19, 10), (21, 11), (23, 12)],
        [(GunlanceShelling.Wide, GunlanceShot.FullburstShell)] = [(22.95, 9), (25.65, 10), (28.35, 11), (31.05, 12)],
        [(GunlanceShelling.Wide, GunlanceShot.WyrmstakeFullburstShell)] = [(17.85, 9), (19.95, 10), (22.05, 11), (24.15, 12)],
        [(GunlanceShelling.Wide, GunlanceShot.ChargedShell)] = [(30.6, 9), (34.2, 10), (37.8, 11), (41.4, 12)],
        [(GunlanceShelling.Wide, GunlanceShot.Wyvernfire)] = [(46, 24), (50, 26), (55, 29), (60, 32)],
        [(GunlanceShelling.Wide, GunlanceShot.WyrmstakeExplosion)] = [(23, 19), (25, 21), (27, 23), (30, 25)],
    };

    /// <summary>
    /// A shelling hit of a gunlance: raw MV that ignores the hitzone and sharpness and never crits, plus fixed fire damage (assumed to
    /// ignore the hitzone too). A Wyrmstake tick is an ordinary hit (MV sheet "GL" tab): 4 + level MV (Long 5 + level), no fire.
    /// </summary>
    public static (double Mv, double Fire) Shell(GunlanceShelling type, GunlanceShot shot, int level)
    {
        var lv = Math.Clamp(level, 1, 4);
        if (shot == GunlanceShot.WyrmstakeTick) return ((type == GunlanceShelling.Long ? 5 : 4) + lv, 0);
        return ShellTable[(type, shot)][lv - 1];
    }

    // ---------------- uptime of timed skills without taking damage, see notes/skill_values_vague_skills.md (Latent Power) and
    // notes/skill_values_set_group.md (Azure Bolt, Thunderous Roar) ----------------

    public const int LatentPowerTimerSeconds = 120;
    public const int LatentPowerDamage = 130;

    /// <summary>
    /// Latent Power: on for 120 s after 120 s of fighting near a monster, then the 120 s start again (Game8), so half the time;
    /// Thunderous Roar (Rey Dau's Voltage) makes it last 150 s (I) / 210 s (II). Taking 130 damage in total while it is off starts
    /// it at once (no cooldown), so <paramref name="damageSeconds"/> (how long that takes; null: never hit) shortens the off time.
    /// </summary>
    public static double LatentPowerUptime(SetBonusTier thunderousRoar, double? damageSeconds = null)
    {
        double on = thunderousRoar switch { SetBonusTier.II => 210, SetBonusTier.I => 150, _ => 120 };
        var off = Math.Min(LatentPowerTimerSeconds, damageSeconds ?? LatentPowerTimerSeconds);
        return on / (on + off);
    }

    /// <summary>
    /// Azure Bolt (Leviathan's Fury): +15 affinity for 20 s (I) / 30 s (II) on a proc, about 30 s cooldown from the proc and a gauge
    /// filled by hits. Estimates on the careful side: whether the gauge fills during the cooldown is not documented.
    /// </summary>
    public static double AzureBoltUptime(SetBonusTier tier) => tier == SetBonusTier.II ? 0.85 : 0.60;

    // ---------------- proc damage (extra damage instances), see notes/skill_values_set_group.md ----------------

    // Azure Bolt bursts (Leviathan's Fury) and Scorcher (Rathalos's Flare) are deliberately not scored; their values are in the notes.

    /// <summary>
    /// Dark Arts (Soul of the Dark Knight I), Great Sword: extra shockwave on Lv3 charged slashes, 30 MV raw (crits, uses sharpness).
    /// Consistent with in-game numbers: a near-constant 80-104 damage next to 379-612 damage CS / SCS / TCS hits (about 19 %).
    /// </summary>
    public const double DarkArtsShockwaveMv = 30;
    /// <summary>Dark Arts shockwave dragon element, read as 60 display = 6 true. VERIFY: single source, unit unclear.</summary>
    public const double DarkArtsShockwaveElement = 6;

    /// <summary>Bad Blood (Nu Udra's Mutiny, needs Resentment active): extra hit of 45 / 85 x raw hitzone %. Dummy-verified.</summary>
    public static double BadBlood(SetBonusTier tier) => tier == SetBonusTier.II ? 85 : 45;
    public const double BadBloodCooldownSeconds = 2;

    public readonly record struct BurstBoost(int Attack, int Element);

    // PlayerSkillParam _ContinuousAttackWp??Data in Wp index order.
    // Melee/bow: [duration_s, first_attack, first_element, (attack, element) x Lv1..5]; bowguns: [duration_s, first_attack, attack x Lv1..5].
    // Confirmed by game8: first hit +5 attack / +50 element (DB and Bow +30), window 5 s (GS/HH/GL), 3 s (DB and ranged), 4 s otherwise.
    private static readonly int[][] BurstArrays =
    [
        [5, 5, 5, 10, 8, 12, 10, 14, 12, 16, 16, 18, 20], // GS
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // SnS
        [3, 5, 3, 8, 4, 10, 6, 12, 8, 15, 10, 18, 12],    // DB
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // LS
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // Hammer
        [5, 5, 5, 10, 8, 12, 10, 14, 12, 16, 16, 18, 20], // HH
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // Lance
        [5, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // GL
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // SA
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // CB
        [4, 5, 5, 8, 6, 10, 8, 12, 10, 15, 12, 18, 14],   // IG
        [3, 5, 3, 6, 4, 7, 6, 8, 8, 9, 10, 10, 12],       // Bow
        [3, 5, 6, 7, 8, 9, 10],                            // HBG
        [3, 5, 6, 7, 8, 9, 10],                            // LBG
    ];

    /// <summary>Full Burst boost (after the fifth hit) for a skill level 1..5, in true raw / true element.</summary>
    public static BurstBoost Burst(WeaponType type, int level)
    {
        if (level <= 0) return default;
        var lv = Math.Min(level, 5);
        var a = BurstArrays[(int)type];
        return a.Length == 13
            ? new BurstBoost(a[3 + 2 * (lv - 1)], a[4 + 2 * (lv - 1)])
            : new BurstBoost(a[1 + lv], 0);
    }

    /// <summary>Consecutive hits that give Burst its full boost; fewer give the first-hit boost.</summary>
    public const int BurstFullHits = 5;

    /// <summary>Small boost granted by the first hit, before the fifth hit lands.</summary>
    public static BurstBoost BurstFirstHit(WeaponType type)
    {
        var a = BurstArrays[(int)type];
        return a.Length == 13 ? new BurstBoost(a[1], a[2]) : new BurstBoost(a[1], 0);
    }

    public static int BurstDurationSeconds(WeaponType type) => BurstArrays[(int)type][0];
}
