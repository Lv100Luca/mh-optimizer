using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Data;

/// <summary>One rollable (set bonus, group skill) pair of a Gogma Artian weapon.</summary>
public sealed record GogmaSkillPair(string SetBonus, string GroupSkill);

/// <summary>In-memory dataset with lookup indexes. Build it with <see cref="GameDataLoader"/>.</summary>
public sealed class GameData
{
    public IReadOnlyList<Skill> Skills { get; }
    public IReadOnlyList<ArmorPiece> Armor { get; }
    public IReadOnlyList<Decoration> Decorations { get; }
    public IReadOnlyList<Charm> Charms { get; }
    public IReadOnlyDictionary<WeaponType, IReadOnlyDictionary<GogmaFocus, GogmaWeaponVariant>> GogmaWeapons { get; }
    /// <summary>All 294 rollable (set bonus, group skill) pairs, uniform probability.</summary>
    public IReadOnlyList<GogmaSkillPair> GogmaSkillPairs { get; }
    /// <summary>What random talismans can roll; null when data/random_talisman_pool.json is absent.</summary>
    public RandomTalismanPool? TalismanPool { get; }

    public IReadOnlyDictionary<string, Skill> SkillsByName { get; }
    public IReadOnlyDictionary<int, Skill> SkillsById { get; }
    public IReadOnlyDictionary<string, ArmorPiece> ArmorByName { get; }
    public IReadOnlyDictionary<ArmorPieceKind, IReadOnlyList<ArmorPiece>> ArmorByKind { get; }
    public IReadOnlyDictionary<string, Decoration> DecorationsByName { get; }
    /// <summary>One entry per charm line, at its highest rank. The optimizer should only consider these.</summary>
    public IReadOnlyList<Charm> MaxRankCharms { get; }
    public IReadOnlyDictionary<string, Charm> CharmsByName { get; }
    /// <summary>The craftable baseline talismans (max-rank charms) in talisman form.</summary>
    public IReadOnlyList<Talisman> CraftableTalismans { get; }

    public GameData(
        IReadOnlyList<Skill> skills,
        IReadOnlyList<ArmorPiece> armor,
        IReadOnlyList<Decoration> decorations,
        IReadOnlyList<Charm> charms,
        IReadOnlyDictionary<WeaponType, IReadOnlyDictionary<GogmaFocus, GogmaWeaponVariant>> gogmaWeapons,
        IReadOnlyList<GogmaSkillPair>? gogmaSkillPairs = null,
        RandomTalismanPool? talismanPool = null)
    {
        Skills = skills;
        Armor = armor;
        Decorations = decorations;
        Charms = charms;
        GogmaWeapons = gogmaWeapons;
        GogmaSkillPairs = gogmaSkillPairs ?? [];
        TalismanPool = talismanPool;

        SkillsByName = FirstByKey(skills, s => s.Name);
        SkillsById = FirstByKey(skills, s => s.Id);
        ArmorByName = FirstByKey(armor, a => a.Name);
        ArmorByKind = armor.GroupBy(a => a.Piece).ToDictionary(g => g.Key, g => (IReadOnlyList<ArmorPiece>)g.ToList());
        DecorationsByName = FirstByKey(decorations, d => d.Name);
        MaxRankCharms = charms.Where(c => c.IsMaxRank).ToList();
        CharmsByName = FirstByKey(charms, c => c.Name);
        CraftableTalismans = MaxRankCharms.Select(Talisman.FromCharm).ToList();
    }

    public Skill Skill(string name) =>
        SkillsByName.TryGetValue(name, out var s) ? s : throw new KeyNotFoundException($"Unknown skill '{name}'");

    public ArmorPiece ArmorPiece(string name) =>
        ArmorByName.TryGetValue(name, out var a) ? a : throw new KeyNotFoundException($"Unknown armor piece '{name}'");

    public Decoration Decoration(string name) =>
        DecorationsByName.TryGetValue(name, out var d) ? d : throw new KeyNotFoundException($"Unknown decoration '{name}'");

    public Charm Charm(string name) =>
        CharmsByName.TryGetValue(name, out var c) ? c : throw new KeyNotFoundException($"Unknown charm '{name}'");

    public GogmaWeaponVariant GogmaVariant(WeaponType type, GogmaFocus focus) =>
        GogmaWeapons.TryGetValue(type, out var byFocus) && byFocus.TryGetValue(focus, out var v)
            ? v
            : throw new KeyNotFoundException($"No Gogma weapon variant for {type} / {focus}");

    private static Dictionary<TKey, T> FirstByKey<T, TKey>(IEnumerable<T> items, Func<T, TKey> key) where TKey : notnull =>
        items.GroupBy(key).ToDictionary(g => g.Key, g => g.First());
}
