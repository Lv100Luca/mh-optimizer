using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>A decoration projected onto the relevant skills.</summary>
public sealed record DecoCandidate(Decoration Decoration, SkillKind Kind, int Level, (int Skill, int Level)[] Grants);

/// <summary>A free decoration slot of the loadout, remembering where it is so the result can be written back.</summary>
public readonly record struct FreeSlot(SkillKind Kind, int Level, int Owner, int Index);

/// <summary>
/// Fills decoration slots: first the minimum needed to reach the target skill levels (exact search over the few decorations
/// per target skill), then greedily whatever raises the score most. Decorations are assumed to be available in any quantity.
/// </summary>
public sealed class DecorationFiller
{
    private readonly Relevance _rel;
    private readonly List<DecoCandidate> _decos;
    private readonly Dictionary<int, List<DecoCandidate>> _bySkill;

    public DecorationFiller(GameData data, Relevance rel)
    {
        _rel = rel;
        var all = data.Decorations
            .Select(d => new DecoCandidate(d, d.Kind, d.Slot, d.Skills.Select(g => (rel.IndexOf(g.Skill), g.Level)).Where(g => g.Item1 >= 0).ToArray()))
            .Where(d => d.Grants.Length > 0)
            .ToList();
        // drop decorations dominated by another of the same kind that needs no bigger slot and grants at least as much of every relevant skill
        _decos = all.Where(d => !all.Any(o => !ReferenceEquals(o, d) && o.Kind == d.Kind && o.Level <= d.Level && DominatesGrants(o, d))).ToList();
        _bySkill = new Dictionary<int, List<DecoCandidate>>();
        foreach (var d in _decos)
            foreach (var (skill, _) in d.Grants)
                (_bySkill.TryGetValue(skill, out var l) ? l : _bySkill[skill] = []).Add(d);
        // when covering targets prefer decorations that give the most levels per slot, then single-skill ones, then small slots
        foreach (var skillOptions in _bySkill)
        {
            var skill = skillOptions.Key;
            skillOptions.Value.Sort((a, b) =>
            {
                var ga = a.Grants.First(g => g.Skill == skill).Level;
                var gb = b.Grants.First(g => g.Skill == skill).Level;
                if (ga != gb) return gb - ga;
                if (a.Grants.Length != b.Grants.Length) return a.Grants.Length - b.Grants.Length;
                return a.Level - b.Level;
            });
        }
    }

    public IReadOnlyList<DecoCandidate> All => _decos;

    private static bool DominatesGrants(DecoCandidate a, DecoCandidate b)
    {
        foreach (var (skill, level) in b.Grants)
        {
            var la = 0;
            foreach (var (s, l) in a.Grants) if (s == skill) la = l;
            if (la < level) return false;
        }
        // strictly better somewhere, or equal with a smaller slot, or equal and earlier in the list (ties broken by reference order)
        return a.Grants.Length > b.Grants.Length || a.Level < b.Level || a.Grants.Any(g => b.Grants.All(h => h.Skill != g.Skill || h.Level < g.Level))
               || (a.Level == b.Level && a.Grants.Length == b.Grants.Length && a.Decoration.Id < b.Decoration.Id);
    }

    /// <summary>
    /// Cheap optimistic bound for the greedy fill: the sum of the best <paramref name="freeSlots"/> single-decoration gains at the
    /// current levels (ignores slot kinds/levels and diminishing returns, so it can only over-estimate).
    /// </summary>
    public double RelaxedGainBound(int[] levels, int freeSlots, int maxSlotLevel, Func<int[], double> score)
    {
        if (freeSlots <= 0) return 0;
        var baseScore = score(levels);
        var gains = new List<double>();
        foreach (var d in _decos)
        {
            if (d.Level > maxSlotLevel) continue;
            if (!d.Grants.Any(g => levels[g.Skill] < _rel.MaxLevels[g.Skill])) continue;
            Apply(d, levels, +1);
            var gain = score(levels) - baseScore;
            Apply(d, levels, -1);
            if (gain > 0) gains.Add(gain);
        }
        gains.Sort((a, b) => b.CompareTo(a));
        return gains.Take(freeSlots).Sum();
    }

    private const int CoverSearchBudget = 400;
    private const int MaxCoversReturned = 6;

    /// <summary>
    /// Finds ways to reach every target level. Returns up to a few distinct covers, the ones leaving the most slot value free first
    /// (weapon slots count double because weapon skills are scarcer); empty when the targets cannot be reached.
    /// </summary>
    public List<Dictionary<FreeSlot, DecoCandidate>> CoverTargets(int[] levels, List<FreeSlot> free)
    {
        var deficits = new List<(int Skill, int Deficit)>();
        for (var s = 0; s < levels.Length; s++)
            if (_rel.Targets[s] > levels[s]) deficits.Add((s, _rel.Targets[s] - levels[s]));
        if (deficits.Count == 0) return [new Dictionary<FreeSlot, DecoCandidate>()];

        // hardest skills first: the fewest decoration options
        deficits.Sort((a, b) => (_bySkill.GetValueOrDefault(a.Skill)?.Count ?? 0) - (_bySkill.GetValueOrDefault(b.Skill)?.Count ?? 0));
        var found = new List<(int FreeValue, string Key, Dictionary<FreeSlot, DecoCandidate> Placed)>();
        var budget = CoverSearchBudget;
        Cover(deficits, 0, (int[])levels.Clone(), [], free, found, ref budget);
        return found.GroupBy(f => f.Key).Select(g => g.First())
            .OrderByDescending(f => f.FreeValue)
            .Take(MaxCoversReturned)
            .Select(f => f.Placed)
            .ToList();
    }

    private void Cover(List<(int Skill, int Deficit)> deficits, int i, int[] levels, List<DecoCandidate> chosen, List<FreeSlot> free,
        List<(int, string, Dictionary<FreeSlot, DecoCandidate>)> found, ref int budget)
    {
        if (budget <= 0) return;
        if (i == deficits.Count)
        {
            budget--;
            var packed = Pack(chosen, free);
            if (packed is null) return;
            var used = packed.Keys.ToHashSet();
            var freeValue = free.Where(f => !used.Contains(f)).Sum(f => f.Level * (f.Kind == SkillKind.Weapon ? 2 : 1));
            var key = string.Join("|", chosen.Select(c => c.Decoration.Id).Order());
            found.Add((freeValue, key, packed));
            return;
        }
        var (skill, _) = deficits[i];
        var need = _rel.Targets[skill] - levels[skill];
        if (need <= 0) { Cover(deficits, i + 1, levels, chosen, free, found, ref budget); return; }
        if (!_bySkill.TryGetValue(skill, out var options) || chosen.Count >= free.Count) return;

        foreach (var d in options)
        {
            if (!Fits(d, chosen, free)) continue;
            Apply(d, levels, +1);
            chosen.Add(d);
            Cover(deficits, i, levels, chosen, free, found, ref budget);
            chosen.RemoveAt(chosen.Count - 1);
            Apply(d, levels, -1);
            if (budget <= 0) return;
        }
    }

    /// <summary>Greedy: repeatedly put the decoration with the best score gain into the largest free slot.</summary>
    public void FillGreedy(int[] levels, List<FreeSlot> free, Dictionary<FreeSlot, DecoCandidate> placed, Func<int[], double> score)
    {
        var current = score(levels);
        foreach (var slot in free.Where(f => !placed.ContainsKey(f)).OrderByDescending(f => f.Level).ToList())
        {
            DecoCandidate? best = null;
            var bestScore = current;
            foreach (var d in _decos)
            {
                if (d.Kind != slot.Kind || d.Level > slot.Level) continue;
                if (!d.Grants.Any(g => levels[g.Skill] < _rel.MaxLevels[g.Skill])) continue;
                Apply(d, levels, +1);
                var s = score(levels);
                Apply(d, levels, -1);
                if (s > bestScore + 1e-9) { bestScore = s; best = d; }
            }
            if (best is null) continue;
            Apply(best, levels, +1);
            placed[slot] = best;
            current = bestScore;
        }
    }

    private void Apply(DecoCandidate d, int[] levels, int sign)
    {
        foreach (var (skill, level) in d.Grants) levels[skill] += sign * level;
    }

    private static bool Fits(DecoCandidate d, List<DecoCandidate> chosen, List<FreeSlot> free) =>
        Pack([.. chosen, d], free) is not null;

    /// <summary>Assigns decorations to slots: biggest decorations first, each into the smallest free slot of its kind that fits.</summary>
    public static Dictionary<FreeSlot, DecoCandidate>? Pack(List<DecoCandidate> decos, List<FreeSlot> free)
    {
        var result = new Dictionary<FreeSlot, DecoCandidate>();
        var available = free.OrderBy(f => f.Level).ToList();
        foreach (var d in decos.OrderByDescending(d => d.Level))
        {
            var idx = available.FindIndex(f => f.Kind == d.Kind && f.Level >= d.Level);
            if (idx < 0) return null;
            result[available[idx]] = d;
            available.RemoveAt(idx);
        }
        return result;
    }
}
