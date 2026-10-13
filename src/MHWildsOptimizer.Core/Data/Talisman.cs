using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Data;

/// <summary>Crafted = a charm line at max rank, Random = a melded talisman the user owns, Possible = one the game can roll (the "every possible talisman" mode).</summary>
public enum TalismanSource { Crafted, Random, Possible }

/// <summary>A decoration slot on a talisman. Unlike armor, talismans can carry weapon-kind slots.</summary>
public sealed record TalismanSlot(int Level, SkillKind Kind)
{
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}{Level}";
}

/// <summary>A charm as equipped: either a crafted charm line at its max rank, or a random (melded) talisman the user owns.</summary>
public sealed record Talisman(string Name, int Rarity, IReadOnlyList<SkillGrant> Skills, IReadOnlyList<TalismanSlot> Slots, TalismanSource Source)
{
    public static Talisman FromCharm(Charm charm) => new(charm.Name, charm.Rarity, charm.Skills, [], TalismanSource.Crafted);

    public override string ToString() =>
        $"{Name} [{string.Join(", ", Skills.Select(s => $"{s.Skill} {s.Level}"))}]" + (Slots.Count > 0 ? $" slots {string.Join(",", Slots)}" : "");
}

/// <summary>What random talismans can roll (data/random_talisman_pool.json). Used to validate user input and to list every possible talisman.</summary>
public sealed class RandomTalismanPool
{
    public sealed record PoolSkill(string Group, SkillKind Kind, int MaxLevel, IReadOnlyDictionary<string, int> Points);
    public sealed record SlotPattern(IReadOnlyList<TalismanSlot> Slots, int Points);
    /// <summary>One way a talisman of a rarity rolls: the point value of each of its three skills (0 = no skill) and of its slots.</summary>
    public sealed record PointRow(int Rarity, IReadOnlyList<int> SkillPoints, int SlotPoints);
    /// <summary>A skill level a talisman skill worth <see cref="Points"/> can be (1-4 points: weapon-kind table, 5-10: armor-kind table).</summary>
    public sealed record LotSkill(string Skill, int Level, int Points);

    public required IReadOnlyDictionary<string, PoolSkill> Skills { get; init; }
    public required IReadOnlyList<SlotPattern> SlotPatterns { get; init; }
    public required IReadOnlyDictionary<string, int> RarityByType { get; init; }
    public IReadOnlyList<PointRow> PointRows { get; init; } = [];
    public IReadOnlyList<LotSkill> LotSkills { get; init; } = [];

    public int? MaxLevel(string skill) => Skills.TryGetValue(skill, out var s) ? s.MaxLevel : null;

    public bool IsKnownSlotPattern(IReadOnlyList<TalismanSlot> slots)
    {
        var key = Key(slots);
        return SlotPatterns.Any(p => Key(p.Slots) == key);
    }

    private static string Key(IEnumerable<TalismanSlot> slots) => string.Join("|", slots.Select(s => s.ToString()).OrderBy(s => s));
}
