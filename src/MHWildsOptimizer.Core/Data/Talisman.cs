using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Data;

public enum TalismanSource { Crafted, Random }

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

/// <summary>What random talismans can roll (data/random_talisman_pool.json). Used to validate user input.</summary>
public sealed class RandomTalismanPool
{
    public sealed record PoolSkill(string Group, SkillKind Kind, int MaxLevel, IReadOnlyDictionary<string, int> Points);
    public sealed record SlotPattern(IReadOnlyList<TalismanSlot> Slots, int Points);

    public required IReadOnlyDictionary<string, PoolSkill> Skills { get; init; }
    public required IReadOnlyList<SlotPattern> SlotPatterns { get; init; }
    public required IReadOnlyDictionary<string, int> RarityByType { get; init; }

    public int? MaxLevel(string skill) => Skills.TryGetValue(skill, out var s) ? s.MaxLevel : null;

    public bool IsKnownSlotPattern(IReadOnlyList<TalismanSlot> slots)
    {
        var key = Key(slots);
        return SlotPatterns.Any(p => Key(p.Slots) == key);
    }

    private static string Key(IEnumerable<TalismanSlot> slots) => string.Join("|", slots.Select(s => s.ToString()).OrderBy(s => s));
}
