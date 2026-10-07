using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>An armor piece with what its set contributes (set bonuses, group skill).</summary>
public sealed record ArmorEntry(ArmorSetPieceDto Piece, string Set, IReadOnlyList<string> SetBonus, string? GroupSkill)
{
    public string Name => Piece.Name;
    public ArmorPieceKind Kind => Piece.Kind;
    public int Rarity => Piece.Rarity;
}

/// <summary>Lookups over the catalog that panels share.</summary>
public sealed class CatalogIndex
{
    public CatalogIndex(Catalog catalog)
    {
        SkillsByName = catalog.Skills.ToDictionary(s => s.Name);
        SetsByName = catalog.ArmorSets.ToDictionary(s => s.Name);
        WeaponTypes = catalog.WeaponTypes.ToDictionary(w => w.Kind);
        var armor = new Dictionary<string, ArmorEntry>();
        foreach (var set in catalog.ArmorSets)
            foreach (var p in set.Pieces)
                armor[p.Name] = new ArmorEntry(p, set.Name, set.SetBonus, set.GroupSkill);
        ArmorByName = armor;
        DecorationsByName = catalog.Decorations.GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First());
        CharmsByName = catalog.Charms.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.Last());
    }

    public IReadOnlyDictionary<string, SkillDto> SkillsByName { get; }
    public IReadOnlyDictionary<string, ArmorSetDto> SetsByName { get; }
    public IReadOnlyDictionary<string, WeaponTypeDto> WeaponTypes { get; }
    public IReadOnlyDictionary<string, ArmorEntry> ArmorByName { get; }
    public IReadOnlyDictionary<string, DecorationDto> DecorationsByName { get; }
    public IReadOnlyDictionary<string, CharmDto> CharmsByName { get; }

    private static readonly Dictionary<string, int> RomanLevels = new() { ["I"] = 1, ["II"] = 2, ["III"] = 3 };

    /// <summary>
    /// The catalog skill behind a display name such as "Gore Magala's Tyranny II (Black Eclipse II)" or "Lord's Soul (Guts)";
    /// a trailing tier is returned as the level.
    /// </summary>
    public (SkillDto Skill, int? Level)? ResolveSkill(string name)
    {
        if (SkillsByName.TryGetValue(name, out var exact)) return (exact, null);
        var bare = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\(.*\)$", "");
        if (SkillsByName.TryGetValue(bare, out var plain)) return (plain, null);
        var tier = System.Text.RegularExpressions.Regex.Match(bare, "^(.*) (I{1,3})$");
        return tier.Success && SkillsByName.TryGetValue(tier.Groups[1].Value, out var tiered) ? (tiered, RomanLevels[tier.Groups[2].Value]) : null;
    }
}
