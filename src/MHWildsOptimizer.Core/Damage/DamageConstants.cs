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

    /// <summary>Flat true element from Elemental Absorption (community values, confidence medium).</summary>
    public static int ElementalAbsorptionFlat(WeaponType type, int level)
    {
        if (level <= 0) return 0;
        var lv = Math.Min(level, 3);
        return type switch
        {
            WeaponType.GreatSword or WeaponType.HuntingHorn => lv switch { 1 => 5, 2 => 8, _ => 10 },
            WeaponType.DualBlades or WeaponType.Bow => lv switch { 1 => 3, 2 => 4, _ => 5 },
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
    // Melee/bow: [first_attack, first_element, duration_s, (attack, element) x Lv1..5]; bowguns: [first_attack, duration_s, attack x Lv1..5].
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

    /// <summary>Small boost granted by the first hit, before the fifth hit lands.</summary>
    public static BurstBoost BurstFirstHit(WeaponType type)
    {
        var a = BurstArrays[(int)type];
        return a.Length == 13 ? new BurstBoost(a[0], a[1]) : new BurstBoost(a[0], 0);
    }

    public static int BurstDurationSeconds(WeaponType type)
    {
        var a = BurstArrays[(int)type];
        return a.Length == 13 ? a[2] : a[1];
    }
}
