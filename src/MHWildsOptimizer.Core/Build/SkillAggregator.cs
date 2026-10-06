using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Build;

/// <summary>Skill levels, set-bonus piece counts and group-skill piece counts of a loadout.</summary>
public sealed class ActiveSkills
{
    /// <summary>Armor and weapon skill levels, capped at each skill's max level.</summary>
    public IReadOnlyDictionary<string, int> Levels { get; }
    /// <summary>Same as <see cref="Levels"/> but uncapped (useful to spot wasted points).</summary>
    public IReadOnlyDictionary<string, int> RawLevels { get; }
    /// <summary>Equipped pieces per set-bonus name; the Gogma weapon counts as one piece.</summary>
    public IReadOnlyDictionary<string, int> SetBonusPieces { get; }
    /// <summary>Equipped pieces per group-skill name; the Gogma weapon counts as one piece.</summary>
    public IReadOnlyDictionary<string, int> GroupSkillPieces { get; }

    public ActiveSkills(
        IReadOnlyDictionary<string, int> levels,
        IReadOnlyDictionary<string, int> rawLevels,
        IReadOnlyDictionary<string, int> setBonusPieces,
        IReadOnlyDictionary<string, int> groupSkillPieces)
    {
        Levels = levels;
        RawLevels = rawLevels;
        SetBonusPieces = setBonusPieces;
        GroupSkillPieces = groupSkillPieces;
    }

    public int Level(string skill) => Levels.GetValueOrDefault(skill);
    public bool Has(string skill) => Level(skill) > 0;

    public SetBonusTier SetTier(string setBonus) => SetBonusPieces.GetValueOrDefault(setBonus) switch
    {
        >= SkillAggregator.SetTierTwoPieces => SetBonusTier.II,
        >= SkillAggregator.SetTierOnePieces => SetBonusTier.I,
        _ => SetBonusTier.None,
    };

    public bool GroupActive(string groupSkill) =>
        GroupSkillPieces.GetValueOrDefault(groupSkill) >= SkillAggregator.GroupSkillPieces;

    public IEnumerable<(string Name, SetBonusTier Tier)> ActiveSetBonuses =>
        SetBonusPieces.Keys.Select(n => (n, SetTier(n))).Where(x => x.Item2 != SetBonusTier.None);

    public IEnumerable<string> ActiveGroupSkills => GroupSkillPieces.Keys.Where(GroupActive);
}

public static class SkillAggregator
{
    public const int SetTierOnePieces = 2;
    public const int SetTierTwoPieces = 4;
    public const int GroupSkillPieces = 3;

    public static ActiveSkills Aggregate(Loadout loadout, GameData data)
    {
        var raw = new Dictionary<string, int>();
        var sets = new Dictionary<string, int>();
        var groups = new Dictionary<string, int>();

        foreach (var equipped in loadout.ArmorPieces)
        {
            AddGrants(raw, equipped.Piece.Skills);
            AddDecorations(raw, equipped.Decos);
            foreach (var setBonus in equipped.Piece.SetBonus)
                Bump(sets, setBonus);
            if (equipped.Piece.GroupSkill is { } g)
                Bump(groups, g);
        }

        AddDecorations(raw, loadout.Weapon.Decos);
        if (loadout.Weapon.Stats.SetBonus is { } ws) Bump(sets, ws);
        if (loadout.Weapon.Stats.GroupSkill is { } wg) Bump(groups, wg);

        if (loadout.Talisman is { } t)
        {
            AddGrants(raw, t.Talisman.Skills);
            AddDecorations(raw, t.Decos);
        }

        var capped = raw.ToDictionary(
            kv => kv.Key,
            kv => data.SkillsByName.TryGetValue(kv.Key, out var s) ? Math.Min(kv.Value, s.MaxLevel) : kv.Value);

        return new ActiveSkills(capped, raw, sets, groups);
    }

    private static void AddDecorations(Dictionary<string, int> levels, IEnumerable<Decoration?> decos)
    {
        foreach (var deco in decos)
            if (deco is not null) AddGrants(levels, deco.Skills);
    }

    private static void AddGrants(Dictionary<string, int> levels, IEnumerable<SkillGrant> grants)
    {
        foreach (var g in grants)
            levels[g.Skill] = levels.GetValueOrDefault(g.Skill) + g.Level;
    }

    private static void Bump(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;
}
