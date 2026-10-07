using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Data;

// JSON models for the normalized dataset in data/*.json (snake_case on disk, see GameDataLoader.JsonOptions).

public sealed record SkillRank(int Level, string? Name, int? PiecesRequired, string? Description);

/// <param name="Icon">Game icon category (attack, affinity, element, handicraft, ranged, defense, health, stamina, offense, utility, item, gathering, set, group).</param>
public sealed record Skill(int Id, string Name, SkillKind Kind, string? Description, int MaxLevel, IReadOnlyList<SkillRank> Ranks, string? Icon = null);

/// <summary>A skill at a level, as granted by an armor piece, decoration or charm.</summary>
public sealed record SkillGrant(string Skill, int SkillId, int Level);

public sealed record Resistances(int Fire, int Water, int Ice, int Thunder, int Dragon);

public sealed record ArmorPiece(
    int Id,
    string Name,
    string? Set,
    int? SetId,
    ArmorPieceKind Piece,
    int Rarity,
    IReadOnlyList<int> Slots,
    IReadOnlyList<int> SlotsTranscended,
    IReadOnlyList<SkillGrant> Skills,
    IReadOnlyList<string> SetBonus,
    string? GroupSkill,
    int DefenseBase,
    int DefenseMax,
    Resistances Resistances)
{
    public override string ToString() => Name;
}

/// <param name="IconColor">Jewel icon color as named by wilds.mhdb.io (purple, white, emerald, sky, ...).</param>
public sealed record Decoration(int Id, string Name, SkillKind Kind, int Slot, int Rarity, IReadOnlyList<SkillGrant> Skills, string? IconColor = null)
{
    public override string ToString() => Name;
}

public sealed record Charm(int CharmId, string Name, int Level, int Rarity, bool IsMaxRank, IReadOnlyList<SkillGrant> Skills)
{
    public override string ToString() => Name;
}

public sealed record SharpnessBar(int Red, int Orange, int Yellow, int Green, int Blue, int White, int Purple)
{
    public int Total => Red + Orange + Yellow + Green + Blue + White + Purple;

    /// <summary>Highest color with any points, i.e. the sharpness a fresh weapon attacks at.</summary>
    public SharpnessColor TopColor =>
        Purple > 0 ? SharpnessColor.Purple :
        White > 0 ? SharpnessColor.White :
        Blue > 0 ? SharpnessColor.Blue :
        Green > 0 ? SharpnessColor.Green :
        Yellow > 0 ? SharpnessColor.Yellow :
        Orange > 0 ? SharpnessColor.Orange : SharpnessColor.Red;
}

/// <summary>One of the three focus variants of a Gogma Artian weapon as listed by the API (raw/affinity already include the focus delta).</summary>
public sealed record GogmaWeaponVariant(
    int ApiId,
    string Name,
    int Raw,
    int DisplayAttack,
    int Affinity,
    SharpnessBar? Sharpness,
    IReadOnlyList<int> Slots);

/// <summary>A large monster and the hitzones of its parts (data/monster_hitzones.json, from wilds.mhdb.io), weakest point first.</summary>
public sealed record MonsterHitzones(string Name, IReadOnlyList<PartHitzones> Parts);

/// <summary>Hitzone values of one monster part in percent (base state: not broken, not wounded).</summary>
public sealed record PartHitzones(string Name, int Slash, int Blunt, int Pierce, int Fire, int Water, int Thunder, int Ice, int Dragon);
