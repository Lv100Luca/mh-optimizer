using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// Every random talisman the game can roll, as if all of them were owned. A talisman rolls one point row of its rarity, then
/// for each skill a skill level worth exactly that many points, and the slot pattern worth the slot points. Skills the search
/// does not care about all look alike, so they are left off the talisman (the skill is still there, it just does not count).
/// CP-SAT takes the table itself (<see cref="RollTable"/>); the beam search takes the talismans spelled out (<see cref="Generate"/>).
/// </summary>
public static class PossibleTalismans
{
    /// <summary>The talismans a roll table allows, one per distinct set of relevant skills (levels up to the cap) and slots.</summary>
    public static List<Talisman> Generate(RandomTalismanPool pool, Relevance rel, GameData data)
    {
        var table = RollTable.Build(pool, rel);
        var byKey = new Dictionary<string, Talisman>();
        foreach (var row in table.Rows)
        {
            var options = row.SkillPoints.Select(p => table.OptionsFor(p)).ToList();
            foreach (var combo in Combinations(options))
            {
                var picked = combo.Where(o => o is not null).Select(o => o!.Value).ToList();
                if (picked.Select(p => p.Skill).Distinct().Count() < picked.Count) continue; // a talisman never rolls a skill twice
                var key = string.Join(",", picked.Select(p => $"{p.Skill}:{Math.Min(p.Level, rel.Caps[rel.IndexOf(p.Skill)])}").Order())
                          + "|" + string.Join(",", row.Slots.Select(s => s.ToString()).Order());
                if (!byKey.ContainsKey(key)) byKey[key] = Make(row, picked, data);
            }
        }
        return byKey.Values.ToList();
    }

    public static Talisman Make(RollTable.Row row, IReadOnlyList<RollTable.SkillOption> skills, GameData data)
    {
        var grants = skills.Select(p => new SkillGrant(p.Skill, data.Skill(p.Skill).Id, p.Level)).ToList();
        return new Talisman(NameOf(grants, row.Slots), row.Rarity, grants, row.Slots, TalismanSource.Possible);
    }

    /// <summary>"Possible: Attack Boost 3, Weakness Exploit 1 [armor2]" (unique per skills and slots).</summary>
    public static string NameOf(IReadOnlyList<SkillGrant> skills, IReadOnlyList<TalismanSlot> slots) =>
        "Possible: " + (skills.Count == 0 ? "no useful skill" : string.Join(", ", skills.Select(s => $"{s.Skill} {s.Level}")))
        + (slots.Count > 0 ? $" [{string.Join(", ", slots)}]" : "");

    /// <summary>Per skill position: the relevant skill levels it can be, plus null when it can also be a skill the search ignores.</summary>
    private static List<RollTable.SkillOption?> OptionsFor(this RollTable table, int points)
    {
        if (points == 0) return [null];
        var o = table.Options[points];
        var list = o.Relevant.Select(r => (RollTable.SkillOption?)r).ToList();
        if (o.AnyOther) list.Add(null);
        return list;
    }

    private static IEnumerable<T[]> Combinations<T>(IReadOnlyList<List<T>> options, int at = 0)
    {
        if (at == options.Count) { yield return []; yield break; }
        foreach (var rest in Combinations(options, at + 1))
            foreach (var o in options[at])
                yield return [o, .. rest];
    }
}

/// <summary>The random talisman roll table reduced to the skills one search cares about.</summary>
public sealed class RollTable
{
    public readonly record struct SkillOption(string Skill, int SkillIndex, int Level);

    /// <summary>A way to roll: three skill point values (0 = no skill) and a slot layout; the lowest rarity that rolls it.</summary>
    public sealed record Row(int Rarity, int[] SkillPoints, IReadOnlyList<TalismanSlot> Slots, int[] ArmorSlots, int[] WeaponSlots);

    /// <summary>What a skill worth some points can be: the relevant skill levels, and whether it can also be a skill nobody needs.</summary>
    public sealed record PointOptions(IReadOnlyList<SkillOption> Relevant, bool AnyOther);

    public required IReadOnlyList<Row> Rows { get; init; }
    public required IReadOnlyDictionary<int, PointOptions> Options { get; init; }

    public static RollTable Build(RandomTalismanPool pool, Relevance rel)
    {
        var options = pool.LotSkills.GroupBy(s => s.Points).ToDictionary(g => g.Key, g => new PointOptions(
            g.Where(s => rel.IndexOf(s.Skill) >= 0).Select(s => new SkillOption(s.Skill, rel.IndexOf(s.Skill), s.Level)).Distinct().ToList(),
            g.Any(s => rel.IndexOf(s.Skill) < 0)));
        var layouts = pool.SlotPatterns.GroupBy(p => p.Points).ToDictionary(g => g.Key, g => g.Select(p => p.Slots).ToList());

        var rows = new Dictionary<string, Row>();
        foreach (var r in pool.PointRows.OrderBy(r => r.Rarity))
        {
            // a point value no skill is worth cannot roll
            if (r.SkillPoints.Any(p => p != 0 && !options.ContainsKey(p)) || !layouts.TryGetValue(r.SlotPoints, out var slotsList)) continue;
            foreach (var slots in slotsList)
            {
                var key = string.Join(",", r.SkillPoints) + "|" + string.Join(",", slots);
                rows.TryAdd(key, new Row(r.Rarity, [.. r.SkillPoints], slots,
                    Candidates.SlotCounts(slots.Where(s => s.Kind == SkillKind.Armor).Select(s => s.Level)),
                    Candidates.SlotCounts(slots.Where(s => s.Kind == SkillKind.Weapon).Select(s => s.Level))));
            }
        }
        return new RollTable { Rows = rows.Values.ToList(), Options = options };
    }
}
