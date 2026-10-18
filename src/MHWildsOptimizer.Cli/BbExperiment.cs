using System.Collections.Concurrent;
using System.Diagnostics;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Cli;

/// <summary>
/// Experiment: an exact branch and bound over the armor kinds. Every (state, next piece) is bounded before it is kept: the
/// score of its levels with the remaining kinds at their best, plus a rigorous bound on what decorations can add (a table of
/// the largest gain any single skill level can ever give, see <see cref="MarginalTable"/>). Survivors of the last kind get an
/// exact decoration fill. Counts states per depth and lists the top scores, against a bar (e.g. the Nth best known build).
///   bench bb &lt;request.json&gt; &lt;bar&gt; [--threads n]
/// </summary>
internal static class BbExperiment
{
    public static int Run(GameData data, ResolvedRequest request, double bar, int threads)
    {
        var pair = request.SkillPairCandidates.FirstOrDefault();
        var weapon = request.Weapon with { SetBonus = pair?.SetBonus ?? request.Weapon.SetBonus, GroupSkill = pair?.GroupSkill ?? request.Weapon.GroupSkill };
        var rel = Relevance.Build(weapon, request, data);
        var armor = Candidates.Armor(data, rel, request.Options);
        var talismans = Candidates.Talismans(request.Talismans, rel);
        var filler = new DecorationFiller(data, rel);
        var sw = Stopwatch.StartNew();
        var score = ScoreDecomposition.Build(weapon, rel, request.Conditions);
        Console.WriteLine($"model {sw.ElapsedMilliseconds} ms: {score.Units.Count} units, {score.Sides.Count} sides; {filler.All.Count} decos");
        var kinds = Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray();
        Console.WriteLine($"armor: {string.Join(", ", kinds.Select(k => $"{k} {armor[k].Count}"))}; {talismans.Count} talismans; bar {bar:0.00}; {threads} threads");

        var n = rel.Skills.Count;
        var nSets = rel.SetBonuses.Count; var nGroups = rel.GroupSkills.Count;
        int offA = n, offW = n + 3, offS = n + 6, offG = offS + nSets, keyLen = offG + nGroups;
        var weaponSkills = Candidates.WeaponSkills(weapon, rel);
        var maxPerKind = kinds.Select(k => Enumerable.Range(0, n).Select(s => armor[k].Count == 0 ? 0 : armor[k].Max(c => c.Skills[s])).ToArray()).ToArray();
        var maxSlots = kinds.Select(k => armor[k].Count == 0 ? new int[3] : new[] { armor[k].Max(c => c.ArmorSlots[2]), armor[k].Max(c => c.ArmorSlots[2] + c.ArmorSlots[1]), armor[k].Max(c => c.ArmorSlots[2] + c.ArmorSlots[1] + c.ArmorSlots[0]) }).ToArray();
        var setRemain = new int[kinds.Length + 1][]; var groupRemain = new int[kinds.Length + 1][];
        for (var depth = 0; depth <= kinds.Length; depth++)
        {
            setRemain[depth] = new int[nSets]; groupRemain[depth] = new int[nGroups];
            for (var d = depth; d < kinds.Length; d++)
            {
                for (var si = 0; si < nSets; si++) if (armor[kinds[d]].Any(c => c.SetIds.Contains(si))) setRemain[depth][si]++;
                for (var gi = 0; gi < nGroups; gi++) if (armor[kinds[d]].Any(c => c.GroupId == gi)) groupRemain[depth][gi]++;
            }
        }
        var wSlots = new[] { weapon.Slots.Count(l => l >= 1), weapon.Slots.Count(l => l >= 2), weapon.Slots.Count(l => l >= 3) };

        sw.Restart();
        var marginal = new MarginalTable(score, rel);
        Console.WriteLine($"marginal table {sw.ElapsedMilliseconds} ms: " + string.Join(", ", Enumerable.Range(0, n).Where(s => rel.Caps[s] > 0 && marginal.M[s][0] > 0).Select(s => $"{rel.Skills[s]} {string.Join("/", marginal.M[s].Take(rel.Caps[s]).Select(x => x.ToString("0.0")))}")));
        // every deco's value for the bound: its grants' marginals from a level, best case
        var decos = filler.All;

        // feature values for the score model: skills, then set tiers, then groups
        int[] Values(int[] levels, int[] key, int depth, bool optimistic)
        {
            var values = new int[score.Features.Count];
            for (var s = 0; s < n; s++) values[s] = Math.Min(levels[s], rel.Caps[s]);
            for (var si = 0; si < nSets; si++)
            {
                var count = key[offS + si] + (optimistic ? setRemain[depth][si] : 0);
                values[n + si] = count >= SkillAggregator.SetTierTwoPieces ? 2 : count >= SkillAggregator.SetTierOnePieces ? 1 : 0;
            }
            for (var gi = 0; gi < nGroups; gi++)
                values[n + nSets + gi] = key[offG + gi] + (optimistic ? groupRemain[depth][gi] : 0) >= SkillAggregator.GroupSkillPieces ? 1 : 0;
            return values;
        }

        // the most decorations can add to levels `lv` with these slots: the best `slots` per-decoration marginal values
        double DecoBound(int[] lv, int armorSlots, int weaponSlots)
        {
            Span<double> armorGains = stackalloc double[decos.Count];
            Span<double> weaponGains = stackalloc double[decos.Count];
            int na = 0, nw = 0;
            foreach (var d in decos)
            {
                var g = 0.0;
                foreach (var (skill, level) in d.Grants)
                    for (var j = 0; j < level && lv[skill] + j < rel.Caps[skill]; j++) g += marginal.M[skill][lv[skill] + j];
                if (g <= 0) continue;
                if (d.Kind == SkillKind.Weapon) weaponGains[nw++] = g; else armorGains[na++] = g;
            }
            return TopSum(armorGains[..na], armorSlots) + TopSum(weaponGains[..nw], weaponSlots);
        }
        static double TopSum(Span<double> gains, int k)
        {
            if (k <= 0 || gains.Length == 0) return 0;
            gains.Sort();
            var sum = 0.0;
            for (var i = gains.Length - 1; i >= 0 && i >= gains.Length - k; i--) sum += gains[i];
            return sum;
        }

        // upper bound of any completion of a state after `depth` kinds are placed
        double Bound(int[] key, int depth)
        {
            var lv = new int[n];
            for (var s = 0; s < n; s++)
            {
                var v = key[s];
                for (var d = depth; d < kinds.Length; d++) v += maxPerKind[d][s];
                lv[s] = Math.Min(rel.Caps[s], v);
            }
            var armorSlots = key[offA] + key[offA + 1] + key[offA + 2];
            for (var d = depth; d < kinds.Length; d++) armorSlots += maxSlots[d][2];
            var weaponSlots = key[offW] + key[offW + 1] + key[offW + 2] + wSlots[0];
            return score.Evaluate(Values(lv, key, depth, true)) + DecoBound(lv, armorSlots, weaponSlots);
        }

        bool Feasible(int[] key, int depth)
        {
            for (var si = 0; si < nSets; si++) if (rel.SetTargets[si] > 0 && key[offS + si] + setRemain[depth][si] < rel.SetTargets[si]) return false;
            for (var gi = 0; gi < nGroups; gi++) if (rel.GroupTargets[gi] > 0 && key[offG + gi] + groupRemain[depth][gi] < rel.GroupTargets[gi]) return false;
            return true;
        }

        var comparer = new KeyComparer();
        var states = new List<int[]>();
        foreach (var t in talismans)
        {
            var key = new int[keyLen];
            for (var s = 0; s < n; s++) key[s] = Math.Min(rel.Caps[s], weaponSkills[s] + t.Skills[s]);
            for (var i = 0; i < 3; i++) { key[offA + i] = t.ArmorSlots[i]; key[offW + i] = t.WeaponSlots[i]; }
            if (weapon.SetBonus is { } ws && rel.SetIndex.TryGetValue(ws, out var si)) key[offS + si] = 1;
            if (weapon.GroupSkill is { } wg && rel.GroupIndex.TryGetValue(wg, out var gi)) key[offG + gi] = 1;
            states.Add(key);
        }
        states = states.Distinct(comparer).ToList();
        var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
        Console.WriteLine($"depth 0 (talismans): {states.Count} states, bound of the best {states.Max(k => Bound(k, 0)):0.00}");

        var total = Stopwatch.StartNew();
        for (var depth = 0; depth < kinds.Length; depth++)
        {
            sw.Restart();
            var candidates = armor[kinds[depth]];
            var next = depth + 1;
            var dict = new ConcurrentDictionary<int[], byte>(comparer);
            long expanded = 0, infeasible = 0, cut = 0;
            Parallel.ForEach(states, po, state =>
            {
                long e = 0, inf = 0, c = 0;
                foreach (var cand in candidates)
                {
                    e++;
                    var key = (int[])state.Clone();
                    for (var s = 0; s < n; s++) key[s] = Math.Min(rel.Caps[s], key[s] + cand.Skills[s]);
                    for (var j = 0; j < 3; j++) key[offA + j] += cand.ArmorSlots[j];
                    foreach (var si in cand.SetIds) key[offS + si] = Math.Min(4, key[offS + si] + 1);
                    if (cand.GroupId >= 0) key[offG + cand.GroupId] = Math.Min(3, key[offG + cand.GroupId] + 1);
                    // a set / group that can no longer activate is as good as none
                    for (var si = 0; si < nSets; si++) if (key[offS + si] > 0 && key[offS + si] + setRemain[next][si] < SkillAggregator.SetTierOnePieces) key[offS + si] = 0;
                    for (var gi = 0; gi < nGroups; gi++) if (key[offG + gi] > 0 && key[offG + gi] + groupRemain[next][gi] < SkillAggregator.GroupSkillPieces) key[offG + gi] = 0;
                    if (!Feasible(key, next)) { inf++; continue; }
                    if (Bound(key, next) < bar - 1e-6) { c++; continue; }
                    dict.TryAdd(key, 0);
                }
                Interlocked.Add(ref expanded, e); Interlocked.Add(ref infeasible, inf); Interlocked.Add(ref cut, c);
            });
            states = dict.Keys.ToList();
            Console.WriteLine($"depth {next} ({kinds[depth]}): {expanded} expansions, {infeasible} infeasible, {cut} cut by the bound, {states.Count} distinct states kept ({sw.ElapsedMilliseconds} ms)");
        }
        var searchMs = total.ElapsedMilliseconds;

        // final states: exact decoration fill
        sw.Restart();
        var scored = new ConcurrentBag<(double Score, int[] Key)>();
        var depthN = kinds.Length;
        var fills = 0L;
        Parallel.ForEach(states, po, key =>
        {
            var fill = new ExactFill(score, rel, decos, marginal, Values(key.AsSpan(0, n).ToArray(), key, depthN, false));
            var best = fill.Best(key.AsSpan(0, n).ToArray(), [key[offA + 2], key[offA + 1], key[offA]],
                [key[offW + 2] + wSlots[2], key[offW + 1] + wSlots[1] - wSlots[2], key[offW] + wSlots[0] - wSlots[1]], bar - 1e-6);
            Interlocked.Add(ref fills, fill.Nodes);
            if (best is { } b) scored.Add((b, key));
        });
        var top = scored.OrderByDescending(x => x.Score).Take(12).ToList();
        Console.WriteLine($"final: {states.Count} states, exact fill in {sw.ElapsedMilliseconds} ms ({fills} nodes): {scored.Count} score >= bar; search {searchMs} ms in all");
        Console.WriteLine($"top scores (exact fill, distinct skill states): {string.Join(", ", top.Select(x => x.Score.ToString("0.00")))}");
        foreach (var (s, key) in top.Take(3))
            Console.WriteLine($"  {s:0.00}: {string.Join(", ", Enumerable.Range(0, n).Where(i => key[i] > 0).Select(i => $"{rel.Skills[i]} {key[i]}"))} | slots a{key[offA + 2]}/{key[offA + 1]}/{key[offA]} w{key[offW + 2]}/{key[offW + 1]}/{key[offW]}");
        return 0;
    }

    /// <summary>
    /// M[s][v]: an upper bound on how much the score can rise when skill s goes from level v to v+1, whatever the other levels.
    /// Each channel's change is multiplied by the most the other factors can be (products of nonnegative, nondecreasing
    /// factors), so the sum of these over any sequence of raises bounds the total gain. M is made nonincreasing in v so a
    /// deco's value can be read at the level it starts from.
    /// </summary>
    private sealed class MarginalTable
    {
        public double[][] M { get; }

        public MarginalTable(ScoreDecomposition score, Relevance rel)
        {
            var n = rel.Skills.Count;
            var w = score.Weapon;
            M = new double[n][];
            // per side maxima
            var sides = score.Sides;
            var units = score.Units;
            var pctMax = new double[sides.Count][]; var epctMax = new double[sides.Count][];
            var trMax = new double[sides.Count]; var affPosMax = new double[sides.Count]; var cmMax = new double[sides.Count]; var ceMax = new double[sides.Count];
            var cfMax = new double[sides.Count]; var cefMax = new double[sides.Count]; var eleMax = new double[sides.Count]; var rmMax = new double[sides.Count]; var efrMax = new double[sides.Count];
            var anyShock = new bool[sides.Count];
            for (var side = 0; side < sides.Count; side++)
            {
                var sc = sides[side];
                pctMax[side] = units.Select(u => u.Tables[side].Max(c => c.RawPct)).ToArray();
                epctMax[side] = units.Select(u => u.Tables[side].Max(c => c.ElePct)).ToArray();
                trMax[side] = w.TrueRaw * sc.BaseRawPct * pctMax[side].Aggregate(1.0, (a, b) => a * b) + sc.BaseRawFlat + units.Sum(u => u.Tables[side].Max(c => c.RawFlat));
                var aff = w.Affinity + sc.BaseAffinity + units.Sum(u => u.Tables[side].Max(c => c.Affinity));
                affPosMax[side] = Math.Clamp(aff, 0, DamageConstants.AffinityCap);
                cmMax[side] = Math.Max(sc.BaseCritMult, units.Max(u => u.Tables[side].Max(c => c.CritMult)));
                ceMax[side] = Math.Max(sc.BaseCritEleMult, units.Max(u => u.Tables[side].Max(c => c.CritEleMult)));
                cfMax[side] = 1 + affPosMax[side] / 100 * (cmMax[side] - 1);
                cefMax[side] = 1 + affPosMax[side] / 100 * (ceMax[side] - 1);
                rmMax[side] = units.Select(u => u.Tables[side].Max(c => c.RawMult)).Aggregate(1.0, (a, b) => a * b);
                eleMax[side] = score.HasElement ? Math.Min(w.ElementTrue * sc.BaseElePct * epctMax[side].Aggregate(1.0, (a, b) => a * b) + sc.BaseEleFlat + units.Sum(u => u.Tables[side].Max(c => c.EleFlat)), sc.ElementCap) : 0;
                efrMax[side] = trMax[side] * sc.RawFactor * rmMax[side] * cfMax[side];
                anyShock[side] = units.Any(u => u.Tables[side].Any(c => c.Shockwave));
            }
            var byHealth = sides.Select((s, i) => (s.Health, i)).GroupBy(x => x.Health).Select(g => g.Select(x => x.i).ToArray()).ToList();

            for (var s = 0; s < n; s++)
            {
                M[s] = new double[Math.Max(1, rel.Caps[s])];
                var ui = units.ToList().FindIndex(u => u.Features.Contains(s));
                if (ui < 0) continue;
                var u = units[ui];
                var fi = Array.IndexOf(u.Features, s);
                var stride = u.Strides[fi];
                for (var v = 0; v < rel.Caps[s]; v++)
                {
                    var best = 0.0;
                    for (var a = 0; a < u.States; a++)
                    {
                        if (a / stride % (score.Features[s].Max + 1) != v) continue;
                        var b = a + stride;
                        if (b >= u.States) continue;
                        var perSide = new double[sides.Count];
                        for (var side = 0; side < sides.Count; side++)
                        {
                            var sc = sides[side];
                            var ca = u.Tables[side][a]; var cb = u.Tables[side][b];
                            var othersPct = pctMax[side].Where((_, k) => k != ui).Aggregate(1.0, (x, y) => x * y);
                            var othersEpct = epctMax[side].Where((_, k) => k != ui).Aggregate(1.0, (x, y) => x * y);
                            var dTr = w.TrueRaw * sc.BaseRawPct * Math.Max(0, cb.RawPct - ca.RawPct) * othersPct + Math.Max(0, cb.RawFlat - ca.RawFlat);
                            var dAff = Math.Max(0, cb.Affinity - ca.Affinity);
                            var dCf = dAff / 100 * Math.Max(cmMax[side] - 1, 1 - DamageConstants.NegativeCriticalMultiplier) + affPosMax[side] / 100 * Math.Max(0, cb.CritMult - ca.CritMult);
                            var dRm = Math.Max(0, cb.RawMult - ca.RawMult);
                            var dEfr = sc.RawFactor * (rmMax[side] * (dTr * cfMax[side] + trMax[side] * dCf) + trMax[side] * cfMax[side] * dRm);
                            var d = dEfr + dTr * sc.RawNoCrit + Math.Max(0, cb.Procs - ca.Procs);
                            if (score.HasElement)
                            {
                                var dEle = w.ElementTrue * sc.BaseElePct * Math.Max(0, cb.ElePct - ca.ElePct) * othersEpct + Math.Max(0, cb.EleFlat - ca.EleFlat);
                                var dCef = dAff / 100 * Math.Max(0, ceMax[side] - 1) + affPosMax[side] / 100 * Math.Max(0, cb.CritEleMult - ca.CritEleMult);
                                d += (dEle * cefMax[side] + eleMax[side] * dCef) * sc.ElementScale + dEle * sc.ElementNoCrit;
                            }
                            if (anyShock[side])
                            {
                                d += sc.ShockwaveRawShare * dEfr;
                                if (cb.Shockwave && !ca.Shockwave) d += sc.ShockwaveRawShare * efrMax[side] + sc.ShockwaveConstant;
                            }
                            perSide[side] = d * sc.Weight;
                        }
                        best = Math.Max(best, byHealth.Max(g => g.Sum(i => perSide[i])));
                    }
                    M[s][v] = best;
                }
                for (var v = M[s].Length - 2; v >= 0; v--) M[s][v] = Math.Max(M[s][v], M[s][v + 1]);
            }
        }
    }

    /// <summary>
    /// Exact decoration fill of one skill state: slot by slot (largest first), the distinct level vectors reachable so far,
    /// each dropped when its score plus the marginal bound of the slots left cannot reach the best known.
    /// </summary>
    private sealed class ExactFill(ScoreDecomposition score, Relevance rel, IReadOnlyList<DecoCandidate> decos, MarginalTable marginal, int[] values)
    {
        public long Nodes;
        private readonly int _n = rel.Skills.Count;
        private static readonly KeyComparer Keys = new();

        /// <summary>The best score reachable, or null if below <paramref name="bar"/> or the targets cannot be met.</summary>
        public double? Best(int[] levels, int[] armorSlots321, int[] weaponSlots321, double bar)
        {
            var slots = new List<(SkillKind Kind, int Level)>();
            for (var l = 3; l >= 1; l--) for (var c = 0; c < weaponSlots321[3 - l]; c++) slots.Add((SkillKind.Weapon, l));
            for (var l = 3; l >= 1; l--) for (var c = 0; c < armorSlots321[3 - l]; c++) slots.Add((SkillKind.Armor, l));
            var best = bar;
            var found = false;
            var frontier = new HashSet<int[]>(Keys) { (int[])levels.Clone() };
            for (var slot = 0; slot < slots.Count; slot++)
            {
                var (kind, level) = slots[slot];
                var next = new HashSet<int[]>(Keys);
                foreach (var lv in frontier)
                {
                    Nodes++;
                    if (Bound(lv, slots, slot) <= best) continue;
                    var any = false;
                    foreach (var d in decos)
                    {
                        if (d.Kind != kind || d.Level > level) continue;
                        var useful = false;
                        foreach (var (skill, _) in d.Grants) if (lv[skill] < rel.Caps[skill]) { useful = true; break; }
                        if (!useful) continue;
                        any = true;
                        var child = (int[])lv.Clone();
                        foreach (var (skill, g) in d.Grants) child[skill] = Math.Min(rel.Caps[skill], child[skill] + g);
                        next.Add(child);
                    }
                    if (!any) next.Add(lv);
                }
                frontier = next;
            }
            foreach (var lv in frontier)
            {
                var ok = true;
                for (var s = 0; s < _n && ok; s++) if (rel.Targets[s] > lv[s]) ok = false;
                if (!ok) continue;
                var at = Eval(lv);
                if (at > best) { best = at; found = true; }
            }
            return found ? best : null;
        }

        private double Eval(int[] levels)
        {
            for (var s = 0; s < _n; s++) values[s] = Math.Min(levels[s], rel.Caps[s]);
            return score.Evaluate(values);
        }

        private double Bound(int[] levels, List<(SkillKind Kind, int Level)> slots, int slot)
        {
            var at = Eval(levels);
            int armor = 0, weapon = 0;
            for (var i = slot; i < slots.Count; i++) if (slots[i].Kind == SkillKind.Weapon) weapon++; else armor++;
            Span<double> a = stackalloc double[decos.Count]; Span<double> w = stackalloc double[decos.Count];
            int na = 0, nw = 0;
            foreach (var d in decos)
            {
                var g = 0.0;
                foreach (var (skill, level) in d.Grants)
                    for (var j = 0; j < level && levels[skill] + j < rel.Caps[skill]; j++) g += marginal.M[skill][levels[skill] + j];
                if (g <= 0) continue;
                if (d.Kind == SkillKind.Weapon) w[nw++] = g; else a[na++] = g;
            }
            return at + Top(a[..na], armor) + Top(w[..nw], weapon);
        }

        private static double Top(Span<double> gains, int k)
        {
            if (k <= 0 || gains.Length == 0) return 0;
            gains.Sort();
            var sum = 0.0;
            for (var i = gains.Length - 1; i >= 0 && i >= gains.Length - k; i--) sum += gains[i];
            return sum;
        }
    }

    private sealed class KeyComparer : IEqualityComparer<int[]>
    {
        public bool Equals(int[]? a, int[]? b) => a is not null && b is not null && a.AsSpan().SequenceEqual(b);
        public int GetHashCode(int[] a) { var h = new HashCode(); foreach (var v in a) h.Add(v); return h.ToHashCode(); }
    }
}
