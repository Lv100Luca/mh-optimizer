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
        _decos = data.Decorations
            .Select(d => new DecoCandidate(d, d.Kind, d.Slot, d.Skills.Select(g => (rel.IndexOf(g.Skill), g.Level)).Where(g => g.Item1 >= 0).ToArray()))
            .Where(d => d.Grants.Length > 0)
            .ToList();
        _bySkill = new Dictionary<int, List<DecoCandidate>>();
        foreach (var d in _decos)
            foreach (var (skill, _) in d.Grants)
                (_bySkill.TryGetValue(skill, out var l) ? l : _bySkill[skill] = []).Add(d);
        // prefer single-skill, low-slot decorations when covering targets
        foreach (var l in _bySkill.Values)
            l.Sort((a, b) => a.Grants.Length != b.Grants.Length ? a.Grants.Length - b.Grants.Length : a.Level - b.Level);
    }

    public IReadOnlyList<DecoCandidate> All => _decos;

    /// <summary>Tries to reach every target level. Returns the decorations placed (slot -> deco) or null when impossible.</summary>
    public Dictionary<FreeSlot, DecoCandidate>? CoverTargets(int[] levels, List<FreeSlot> free)
    {
        var deficits = new List<(int Skill, int Deficit)>();
        for (var s = 0; s < levels.Length; s++)
            if (_rel.Targets[s] > levels[s]) deficits.Add((s, _rel.Targets[s] - levels[s]));
        if (deficits.Count == 0) return new Dictionary<FreeSlot, DecoCandidate>();

        // hardest skills first: the fewest decoration options
        deficits.Sort((a, b) => (_bySkill.GetValueOrDefault(a.Skill)?.Count ?? 0) - (_bySkill.GetValueOrDefault(b.Skill)?.Count ?? 0));
        var chosen = new List<DecoCandidate>();
        var working = (int[])levels.Clone();
        return Cover(deficits, 0, working, chosen, free) ? Pack(chosen, free) : null;
    }

    private bool Cover(List<(int Skill, int Deficit)> deficits, int i, int[] levels, List<DecoCandidate> chosen, List<FreeSlot> free)
    {
        if (i == deficits.Count) return Pack(chosen, free) is not null;
        var (skill, _) = deficits[i];
        var need = _rel.Targets[skill] - levels[skill];
        if (need <= 0) return Cover(deficits, i + 1, levels, chosen, free);
        if (!_bySkill.TryGetValue(skill, out var options) || chosen.Count >= free.Count) return false;

        foreach (var d in options)
        {
            if (!Fits(d, chosen, free)) continue;
            Apply(d, levels, +1);
            chosen.Add(d);
            if (Cover(deficits, i, levels, chosen, free)) return true; // keep the state on success
            chosen.RemoveAt(chosen.Count - 1);
            Apply(d, levels, -1);
        }
        return false;
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
