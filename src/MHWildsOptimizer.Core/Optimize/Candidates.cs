using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>An armor piece projected onto the relevant features, plus the decision whether to use its transcended slots.</summary>
public sealed record ArmorCandidate(ArmorPiece Piece, bool Transcended, int[] Skills, int[] ArmorSlots, int[] SetIds, int GroupId)
{
    public IReadOnlyList<int> EffectiveSlots => Transcended ? Piece.SlotsTranscended : Piece.Slots;
}

/// <summary>A talisman projected onto the relevant features.</summary>
public sealed record TalismanCandidate(Talisman Talisman, int[] Skills, int[] ArmorSlots, int[] WeaponSlots);

/// <summary>Candidate generation with dominance pruning: a piece is dropped when another piece of the same kind is at least as good in every relevant feature.</summary>
public static class Candidates
{
    /// <summary>Slot counts per level: index 0 = level 1, 1 = level 2, 2 = level 3.</summary>
    public static int[] SlotCounts(IEnumerable<int> levels)
    {
        var counts = new int[3];
        foreach (var l in levels) if (l is >= 1 and <= 3) counts[l - 1]++;
        return counts;
    }

    public static Dictionary<ArmorPieceKind, List<ArmorCandidate>> Armor(GameData data, Relevance rel, OptimizerOptions options)
    {
        var excluded = options.ExcludeSets.ToHashSet();
        var result = new Dictionary<ArmorPieceKind, List<ArmorCandidate>>();
        foreach (var kind in Enum.GetValues<ArmorPieceKind>())
        {
            var all = data.Armor
                .Where(a => a.Piece == kind && a.Rarity >= options.MinRarity && (a.Set is null || !excluded.Contains(a.Set)))
                .Select(a => Project(a, rel, options.AllowTranscendence))
                .ToList();
            result[kind] = Prune(all, Dominates, a => a.Piece.DefenseMax);
        }
        return result;
    }

    public static List<TalismanCandidate> Talismans(IEnumerable<Talisman> talismans, Relevance rel)
    {
        var all = talismans.Select(t => Project(t, rel)).ToList();
        if (all.Count == 0)
            all.Add(new TalismanCandidate(new Talisman("(no talisman)", 0, [], [], TalismanSource.Crafted), new int[rel.Skills.Count], new int[3], new int[3]));
        return Prune(all, Dominates, t => t.Talisman.Rarity);
    }

    private static ArmorCandidate Project(ArmorPiece a, Relevance rel, bool transcend)
    {
        var skills = new int[rel.Skills.Count];
        foreach (var g in a.Skills)
        {
            var i = rel.IndexOf(g.Skill);
            if (i >= 0) skills[i] += g.Level;
        }
        var useTranscended = transcend && !a.SlotsTranscended.SequenceEqual(a.Slots);
        var sets = a.SetBonus.Select(s => rel.SetIndex.TryGetValue(s, out var i) ? i : -1).Where(i => i >= 0).OrderBy(i => i).ToArray();
        var group = a.GroupSkill is not null && rel.GroupIndex.TryGetValue(a.GroupSkill, out var gi) ? gi : -1;
        return new ArmorCandidate(a, useTranscended, skills, SlotCounts(useTranscended ? a.SlotsTranscended : a.Slots), sets, group);
    }

    private static TalismanCandidate Project(Talisman t, Relevance rel)
    {
        var skills = new int[rel.Skills.Count];
        foreach (var g in t.Skills)
        {
            var i = rel.IndexOf(g.Skill);
            if (i >= 0) skills[i] += g.Level;
        }
        return new TalismanCandidate(t,
            skills,
            SlotCounts(t.Slots.Where(s => s.Kind == SkillKind.Armor).Select(s => s.Level)),
            SlotCounts(t.Slots.Where(s => s.Kind == SkillKind.Weapon).Select(s => s.Level)));
    }

    private static bool Dominates(ArmorCandidate a, ArmorCandidate b) =>
        GreaterOrEqual(a.Skills, b.Skills)
        && SlotsGreaterOrEqual(a.ArmorSlots, b.ArmorSlots)
        && b.SetIds.All(s => a.SetIds.Contains(s))
        && (b.GroupId < 0 || a.GroupId == b.GroupId);

    private static bool Dominates(TalismanCandidate a, TalismanCandidate b) =>
        GreaterOrEqual(a.Skills, b.Skills)
        && SlotsGreaterOrEqual(a.ArmorSlots, b.ArmorSlots)
        && SlotsGreaterOrEqual(a.WeaponSlots, b.WeaponSlots);

    private static bool GreaterOrEqual(int[] a, int[] b)
    {
        for (var i = 0; i < a.Length; i++) if (a[i] < b[i]) return false;
        return true;
    }

    /// <summary>Cumulative comparison: as many level-3 slots, as many level-2-or-better, as many slots overall.</summary>
    public static bool SlotsGreaterOrEqual(int[] a, int[] b) =>
        a[2] >= b[2] && a[2] + a[1] >= b[2] + b[1] && a[2] + a[1] + a[0] >= b[2] + b[1] + b[0];

    private static List<T> Prune<T>(List<T> items, Func<T, T, bool> dominates, Func<T, int> tieBreak)
    {
        var ordered = items.OrderByDescending(tieBreak).ToList();
        var kept = new List<T>();
        foreach (var item in ordered)
        {
            if (kept.Any(k => dominates(k, item))) continue;
            kept.RemoveAll(k => dominates(item, k));
            kept.Add(item);
        }
        return kept;
    }
}
