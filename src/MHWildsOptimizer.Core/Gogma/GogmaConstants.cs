using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Gogma;

/// <summary>
/// Gogma Artian weapon rules. Sources: data/gogma_mechanics.json (monsterhunterwiki Gogma/Artian pages and the
/// ArtianBonusData game-data file). Element numbers are DISPLAY values; true element = display / 10.
/// </summary>
public static class GogmaConstants
{
    /// <summary>Rarity-8 Artian base before part bonuses and focus (the API variants already include the focus delta).</summary>
    public const int ArtianR8BaseRaw = 190;
    public const int ArtianR8BaseAffinity = 5;

    public const int PartsCount = 3;
    /// <summary>Each Artian part gives either +5 true attack or +5% affinity.</summary>
    public const int PartAttackBonus = 5;
    public const int PartAffinityBonus = 5;

    public const int ReinforcementSlots = 5;
    /// <summary>At most two reinforcements with the same type and tier.</summary>
    public const int MaxIdenticalReinforcements = 2;

    /// <summary>Rarity-8 Artian element (fire/water/thunder/ice/dragon all share the value), display units.</summary>
    public static int ElementBaseDisplay(WeaponType type) => type switch
    {
        WeaponType.GreatSword => 450,
        WeaponType.LongSword => 270,
        WeaponType.SwordAndShield => 260,
        WeaponType.DualBlades => 250,
        WeaponType.Hammer => 320,
        WeaponType.HuntingHorn => 320,
        WeaponType.Lance => 270,
        WeaponType.Gunlance => 320,
        WeaponType.SwitchAxe => 260,
        WeaponType.ChargeBlade => 270,
        WeaponType.InsectGlaive => 260,
        WeaponType.Bow => 210,
        _ => 0, // bowguns are ammo based
    };

    /// <summary>Bonus when all three Artian parts share the element ("Element Infusion").</summary>
    public static int InfusionBonusDisplay(WeaponType type) => type switch
    {
        WeaponType.SwordAndShield or WeaponType.DualBlades or WeaponType.SwitchAxe or WeaponType.InsectGlaive or WeaponType.Bow => 20,
        WeaponType.HeavyBowgun or WeaponType.LightBowgun => 0,
        _ => 30,
    };

    /// <summary>Element change applied by the chosen focus (attack/affinity deltas are already in the API variants).</summary>
    public static int FocusElementDeltaDisplay(WeaponType type, GogmaFocus focus) => focus switch
    {
        GogmaFocus.Attack => type switch
        {
            WeaponType.HuntingHorn => 30,
            WeaponType.Gunlance => 40,
            _ => 0,
        },
        GogmaFocus.Affinity => type switch
        {
            WeaponType.GreatSword or WeaponType.Hammer => -10,
            WeaponType.HuntingHorn => 20,
            WeaponType.Gunlance => 30,
            WeaponType.HeavyBowgun or WeaponType.LightBowgun => 0,
            _ => -20,
        },
        GogmaFocus.Element => type switch
        {
            WeaponType.GreatSword or WeaponType.LongSword or WeaponType.Lance or WeaponType.ChargeBlade => 50,
            WeaponType.SwordAndShield or WeaponType.Hammer or WeaponType.SwitchAxe or WeaponType.InsectGlaive => 40,
            WeaponType.DualBlades or WeaponType.Bow => 30,
            WeaponType.HuntingHorn or WeaponType.Gunlance => 80,
            _ => 0,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(focus)),
    };

    public static int AttackReinforcement(ReinforcementTier tier) => tier switch
    {
        ReinforcementTier.I => 5,
        ReinforcementTier.II => 6,
        ReinforcementTier.III => 9,
        ReinforcementTier.EX => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    public static int AffinityReinforcement(ReinforcementTier tier) => tier switch
    {
        ReinforcementTier.I => 5,
        ReinforcementTier.II => 6,
        ReinforcementTier.III => 8,
        ReinforcementTier.EX => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    /// <summary>Element reinforcement, display units. Only tiers I, II and EX exist for element.</summary>
    public static int ElementReinforcementDisplay(WeaponType type, ReinforcementTier tier)
    {
        var (i, ii, ex) = type switch
        {
            WeaponType.GreatSword => (80, 90, 110),
            WeaponType.LongSword => (50, 60, 90),
            WeaponType.SwordAndShield => (30, 50, 80),
            WeaponType.DualBlades => (20, 30, 50),
            WeaponType.Hammer => (50, 60, 90),
            WeaponType.HuntingHorn => (50, 60, 90),
            WeaponType.Lance => (50, 60, 90),
            WeaponType.Gunlance => (50, 60, 90),
            WeaponType.SwitchAxe => (30, 50, 80),
            WeaponType.ChargeBlade => (50, 60, 80),
            WeaponType.InsectGlaive => (30, 50, 80),
            WeaponType.Bow => (30, 40, 60),
            _ => (0, 0, 0),
        };
        return tier switch
        {
            ReinforcementTier.I => i,
            ReinforcementTier.II => ii,
            ReinforcementTier.EX => ex,
            _ => throw new ArgumentException("Element reinforcements only come in tiers I, II and EX.", nameof(tier)),
        };
    }

    public static int SharpnessReinforcement(ReinforcementTier tier) => tier switch
    {
        ReinforcementTier.I => 30,
        ReinforcementTier.EX => 50,
        _ => throw new ArgumentException("Sharpness reinforcements only come in tiers I and EX.", nameof(tier)),
    };

    public static int AmmoReinforcement(ReinforcementTier tier) => tier switch
    {
        ReinforcementTier.I => 1,
        ReinforcementTier.EX => 2,
        _ => throw new ArgumentException("Ammo reinforcements only come in tiers I and EX.", nameof(tier)),
    };

    public static bool TierExists(ReinforcementType type, ReinforcementTier tier) => type switch
    {
        ReinforcementType.Attack or ReinforcementType.Affinity => true,
        ReinforcementType.Element => tier != ReinforcementTier.III,
        _ => tier is ReinforcementTier.I or ReinforcementTier.EX,
    };

    /// <summary>Game data caps sharpness/ammo reinforcements at 2 per weapon; the other categories can fill all 5 slots.</summary>
    public static int MaxReinforcementsOfType(ReinforcementType type) =>
        type is ReinforcementType.Sharpness or ReinforcementType.Ammo ? 2 : ReinforcementSlots;
}
