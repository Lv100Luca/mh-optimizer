using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>
/// One user-entered random talisman, as in inputs/talismans.json:
/// { "name": "Historical Charm", "rarity": 6, "skills": { "Attack Boost": 2, "Maximum Might": 2 }, "slots": ["armor1", "armor1"] }
/// Slot notation: "armor1".."armor3", "weapon1".."weapon3" (or "a1", "w1").
/// </summary>
public sealed record TalismanInput
{
    public required string Name { get; init; }
    public int Rarity { get; init; }
    public Dictionary<string, int> Skills { get; init; } = new();
    public List<string> Slots { get; init; } = [];
}

public sealed record TalismanConversion(Talisman? Talisman, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public static partial class TalismanInputLoader
{
    public static IReadOnlyList<TalismanInput> Read(string path) => GameDataLoader.ReadJson<List<TalismanInput>>(path);

    /// <summary>Converts and validates every entry; entries with errors are dropped from the returned talisman list.</summary>
    public static (IReadOnlyList<Talisman> Talismans, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) Convert(
        IEnumerable<TalismanInput> inputs, GameData data)
    {
        var talismans = new List<Talisman>();
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (var input in inputs)
        {
            var c = Convert(input, data);
            if (c.Talisman is not null) talismans.Add(c.Talisman);
            errors.AddRange(c.Errors);
            warnings.AddRange(c.Warnings);
        }
        return (talismans, errors, warnings);
    }

    public static TalismanConversion Convert(TalismanInput input, GameData data)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var prefix = $"Talisman '{input.Name}': ";

        var grants = new List<SkillGrant>();
        foreach (var (name, level) in input.Skills)
        {
            if (!data.SkillsByName.TryGetValue(name, out var skill))
            {
                errors.Add(prefix + $"unknown skill '{name}'.");
                continue;
            }
            if (skill.Kind is SkillKind.Set or SkillKind.Group)
            {
                errors.Add(prefix + $"'{name}' is a {skill.Kind} skill and cannot be on a talisman.");
                continue;
            }
            if (level < 1 || level > skill.MaxLevel)
            {
                errors.Add(prefix + $"'{name}' level {level} is outside 1..{skill.MaxLevel}.");
                continue;
            }
            if (data.TalismanPool is { } pool)
            {
                var max = pool.MaxLevel(name);
                if (max is null)
                    warnings.Add(prefix + $"'{name}' is not in the random talisman skill pool.");
                else if (level > max)
                    warnings.Add(prefix + $"'{name}' Lv{level} exceeds the pool maximum Lv{max} for random talismans.");
            }
            grants.Add(new SkillGrant(name, skill.Id, level));
        }

        var slots = new List<TalismanSlot>();
        foreach (var s in input.Slots)
        {
            if (TryParseSlot(s, out var slot)) slots.Add(slot);
            else errors.Add(prefix + $"cannot read slot '{s}' (use e.g. armor1 or weapon1).");
        }
        if (data.TalismanPool is { } p && slots.Count > 0 && !p.IsKnownSlotPattern(slots))
            warnings.Add(prefix + $"slot layout [{string.Join(",", slots)}] is not a layout random talismans roll.");

        if (grants.Count == 0 && errors.Count > 0)
            return new TalismanConversion(null, errors, warnings);

        return new TalismanConversion(new Talisman(input.Name, input.Rarity, grants, slots, TalismanSource.Random), errors, warnings);
    }

    public static bool TryParseSlot(string text, out TalismanSlot slot)
    {
        slot = default!;
        var m = SlotPattern().Match(text.Trim());
        if (!m.Success) return false;
        var kind = m.Groups["kind"].Value.StartsWith("w", StringComparison.OrdinalIgnoreCase) ? SkillKind.Weapon : SkillKind.Armor;
        var level = int.Parse(m.Groups["level"].Value);
        if (level is < 1 or > 3) return false;
        slot = new TalismanSlot(level, kind);
        return true;
    }

    [GeneratedRegex(@"^(?<kind>w(eapon)?|a(rmor)?)\s*(?<level>[1-3])$", RegexOptions.IgnoreCase)]
    private static partial Regex SlotPattern();
}
