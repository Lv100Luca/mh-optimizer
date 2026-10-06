namespace MHWildsOptimizer.Core.Domain;

/// <summary>Weapon types in the game's internal Wp index order (Wp00..Wp13), which the datamined arrays use.</summary>
public enum WeaponType
{
    GreatSword = 0,
    SwordAndShield = 1,
    DualBlades = 2,
    LongSword = 3,
    Hammer = 4,
    HuntingHorn = 5,
    Lance = 6,
    Gunlance = 7,
    SwitchAxe = 8,
    ChargeBlade = 9,
    InsectGlaive = 10,
    Bow = 11,
    HeavyBowgun = 12,
    LightBowgun = 13,
}

public enum ArmorPieceKind { Head, Chest, Arms, Waist, Legs }

public enum SkillKind { Armor, Weapon, Set, Group }

public enum Element { None, Fire, Water, Thunder, Ice, Dragon }

public enum SharpnessColor { Red, Orange, Yellow, Green, Blue, White, Purple }

public enum GogmaFocus { Attack, Affinity, Element }

public enum ReinforcementType { Attack, Affinity, Element, Sharpness, Ammo }

public enum ReinforcementTier { I, II, III, EX }

public enum SetBonusTier { None = 0, I = 1, II = 2 }
