using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Core.Optimize;

public sealed record RankedBuild(Loadout Loadout, DamageResult Result, GogmaSkillPair? SkillPair, string SkillPairLabel)
{
    public double Score => Result.Total;
}

/// <summary>Results for one (set bonus, group skill) class; in fixed mode there is exactly one.</summary>
public sealed record SkillPairResult(string Label, GogmaSkillPair? Pair, IReadOnlyList<RankedBuild> Builds, long StatesEvaluated, string CandidateSummary)
{
    public double BestScore => Builds.Count > 0 ? Builds[0].Score : 0;
}

public sealed record OptimizationResult(IReadOnlyList<SkillPairResult> PairResults, TimeSpan Elapsed)
{
    /// <summary>All builds across pairs, best first.</summary>
    public IEnumerable<RankedBuild> AllBuilds => PairResults.SelectMany(p => p.Builds).OrderByDescending(b => b.Score);
}

/// <summary>
/// Set search as dynamic programming over "skill states": talisman and armor pieces are added one kind at a time, partial
/// builds with identical relevant skill levels / slot counts / set and group counts are merged, dominated states are dropped,
/// states that can no longer reach the targets are dropped, and every surviving final state is decorated and scored once.
/// In "optimize" mode the rollable skill pairs are grouped into classes that behave identically for the score.
/// </summary>
public sealed class Optimizer
{
    public const string OtherLabel = "(any other)";
    private readonly GameData _data;
    private readonly ResolvedRequest _request;

    public Optimizer(GameData data, ResolvedRequest request)
    {
        _data = data;
        _request = request;
    }

    public OptimizationResult Run(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var results = new List<SkillPairResult>();

        if (_request.SkillPair.Mode == SkillPairMode.Fixed)
        {
            var pair = _request.SkillPairCandidates.FirstOrDefault();
            var weapon = _request.Weapon with { SetBonus = pair?.SetBonus ?? _request.Weapon.SetBonus, GroupSkill = pair?.GroupSkill ?? _request.Weapon.GroupSkill };
            var label = $"{weapon.SetBonus ?? "-"} + {weapon.GroupSkill ?? "-"}";
            progress?.Report($"Searching builds for {label}");
            results.Add(Search(weapon, pair, label, _request.Options.TopN, progress, ct));
        }
        else
        {
            var baseRel = Relevance.Build(_request.Weapon, _request, _data);
            var classes = _request.SkillPairCandidates.Where(p => p is not null).Select(p => p!)
                .GroupBy(p => (Set: baseRel.SetIndex.ContainsKey(p.SetBonus) ? p.SetBonus : OtherLabel,
                               Group: baseRel.GroupIndex.ContainsKey(p.GroupSkill) ? p.GroupSkill : OtherLabel))
                .ToList();
            progress?.Report($"{classes.Count} score-equivalent skill pair classes, searching in parallel");
            var done = 0;
            var bag = new System.Collections.Concurrent.ConcurrentBag<SkillPairResult>();
            Parallel.ForEach(classes, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, cls =>
            {
                var representative = cls.First();
                var label = $"{cls.Key.Set} + {cls.Key.Group}";
                var weapon = _request.Weapon with
                {
                    SetBonus = cls.Key.Set == OtherLabel ? null : representative.SetBonus,
                    GroupSkill = cls.Key.Group == OtherLabel ? null : representative.GroupSkill,
                };
                var pair = cls.Key.Set == OtherLabel && cls.Key.Group == OtherLabel ? null : representative;
                var r = Search(weapon, pair, label, _request.Options.TopN, null, ct);
                bag.Add(r);
                var n = Interlocked.Increment(ref done);
                progress?.Report($"[{n}/{classes.Count}] {label}: best {r.BestScore:0.0} ({cls.Count()} rollable pairs)");
            });
            results = bag.OrderByDescending(r => r.BestScore).Take(_request.SkillPair.TopN).ToList();
        }

        return new OptimizationResult(results, DateTime.UtcNow - started);
    }

    private SkillPairResult Search(GogmaWeaponStats weapon, GogmaSkillPair? pair, string label, int topN, IProgress<string>? progress, CancellationToken ct)
    {
        var rel = Relevance.Build(weapon, _request, _data);
        var armor = Candidates.Armor(_data, rel, _request.Options);
        var talismans = Candidates.Talismans(_request.Talismans, rel);
        var filler = new DecorationFiller(_data, rel);
        var kinds = Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray();
        var summary = $"candidates after pruning: {string.Join(", ", kinds.Select(k => $"{k} {armor[k].Count}"))}, talismans {talismans.Count}";
        progress?.Report("  " + summary);

        var search = new StateSearch(_data, rel, weapon, _request.Conditions, filler, armor, kinds, talismans, topN, _request.Options.MaxStatesPerDepth, progress, ct);
        var builds = search.Run().Select(b => b with { SkillPair = pair, SkillPairLabel = label }).ToList();
        progress?.Report($"  {search.StatesEvaluated} final states scored, {builds.Count} builds kept");
        return new SkillPairResult(label, pair, builds, search.StatesEvaluated, summary);
    }

    // ------------------------------------------------------------------------------------------------

    private sealed class State
    {
        public required int[] Key { get; init; }       // levels | armorSlots(3) | weaponSlots(3) | setCounts | groupCounts
        public List<(State? Prev, object Item)> Preds { get; } = [];
    }

    private sealed class KeyComparer : IEqualityComparer<int[]>
    {
        public bool Equals(int[]? a, int[]? b) => a is not null && b is not null && a.AsSpan().SequenceEqual(b);
        public int GetHashCode(int[] a)
        {
            var h = new HashCode();
            foreach (var v in a) h.Add(v);
            return h.ToHashCode();
        }
    }

    private sealed class StateSearch
    {
        private const int MaxPredsPerState = 6;
        private const int SetCap = 4, GroupCap = 3;
        /// <summary>Dominance is only checked against this many of the strongest states per bucket; unchecked states are kept (never wrongly dropped).</summary>
        private const int MaxFrontChecks = 400;
        private readonly int _maxStatesPerDepth;

        private readonly GameData _data;
        private readonly Relevance _rel;
        private readonly GogmaWeaponStats _weapon;
        private readonly Conditions _cond;
        private readonly DecorationFiller _filler;
        private readonly Dictionary<ArmorPieceKind, List<ArmorCandidate>> _armor;
        private readonly ArmorPieceKind[] _kinds;
        private readonly List<TalismanCandidate> _talismans;
        private readonly int _topN;
        private readonly IProgress<string>? _progress;
        private readonly CancellationToken _ct;

        private readonly int _n, _offArmorSlots, _offWeaponSlots, _offSets, _offGroups, _keyLength;
        private readonly int[][] _maxPerKind;
        private readonly int[][] _maxSlotsPerKind; // cumulative: [>=3, >=2, >=1]
        private readonly int[][] _setRemain;       // [depth][set] kinds from depth on that can still add this set bonus
        private readonly int[][] _groupRemain;     // [depth][group]
        private readonly (int MinLevel, int MaxGrant, bool Weapon)[] _targetDecoInfo;
        private readonly int _weaponSlots3, _weaponSlots2, _weaponSlots1;

        public long StatesEvaluated { get; private set; }

        public StateSearch(GameData data, Relevance rel, GogmaWeaponStats weapon, Conditions cond, DecorationFiller filler,
            Dictionary<ArmorPieceKind, List<ArmorCandidate>> armor, ArmorPieceKind[] kinds, List<TalismanCandidate> talismans,
            int topN, int maxStatesPerDepth, IProgress<string>? progress, CancellationToken ct)
        {
            _data = data; _rel = rel; _weapon = weapon; _cond = cond; _filler = filler; _armor = armor; _kinds = kinds;
            _talismans = talismans; _topN = topN; _maxStatesPerDepth = Math.Max(1000, maxStatesPerDepth); _progress = progress; _ct = ct;
            _n = rel.Skills.Count;
            _weaponSlots3 = weapon.Slots.Count(l => l >= 3); _weaponSlots2 = weapon.Slots.Count(l => l == 2); _weaponSlots1 = weapon.Slots.Count(l => l == 1);
            _offArmorSlots = _n; _offWeaponSlots = _n + 3; _offSets = _n + 6; _offGroups = _offSets + rel.SetBonuses.Count;
            _keyLength = _offGroups + rel.GroupSkills.Count;
            _maxPerKind = kinds.Select(k => Enumerable.Range(0, _n).Select(s => armor[k].Count == 0 ? 0 : armor[k].Max(c => c.Skills[s])).ToArray()).ToArray();
            _maxSlotsPerKind = kinds.Select(k => armor[k].Count == 0 ? new int[3] : new[]
            {
                armor[k].Max(c => c.ArmorSlots[2]),
                armor[k].Max(c => c.ArmorSlots[2] + c.ArmorSlots[1]),
                armor[k].Max(c => c.ArmorSlots[2] + c.ArmorSlots[1] + c.ArmorSlots[0]),
            }).ToArray();
            _targetDecoInfo = Enumerable.Range(0, _n).Select(s =>
            {
                if (rel.Targets[s] == 0) return (0, 0, false);
                var decos = filler.All.Where(d => d.Grants.Any(g => g.Skill == s)).ToList();
                return decos.Count == 0 ? (99, 0, rel.IsWeaponSkill[s]) : (decos.Min(d => d.Level), decos.Max(d => d.Grants.First(g => g.Skill == s).Level), rel.IsWeaponSkill[s]);
            }).ToArray();

            // how many of the remaining kinds could still contribute each set bonus / group skill
            _setRemain = new int[kinds.Length + 1][];
            _groupRemain = new int[kinds.Length + 1][];
            for (var depth = 0; depth <= kinds.Length; depth++)
            {
                _setRemain[depth] = new int[rel.SetBonuses.Count];
                _groupRemain[depth] = new int[rel.GroupSkills.Count];
                for (var d = depth; d < kinds.Length; d++)
                {
                    for (var si = 0; si < rel.SetBonuses.Count; si++)
                        if (armor[kinds[d]].Any(c => c.SetIds.Contains(si))) _setRemain[depth][si]++;
                    for (var gi = 0; gi < rel.GroupSkills.Count; gi++)
                        if (armor[kinds[d]].Any(c => c.GroupId == gi)) _groupRemain[depth][gi]++;
                }
            }
        }

        /// <summary>Set/group counts that can no longer reach their activation threshold are zeroed so equivalent states merge.</summary>
        private void Normalize(int[] key, int depth)
        {
            for (var si = 0; si < _rel.SetBonuses.Count; si++)
                if (key[_offSets + si] > 0 && key[_offSets + si] + _setRemain[depth][si] < SkillAggregator.SetTierOnePieces) key[_offSets + si] = 0;
            for (var gi = 0; gi < _rel.GroupSkills.Count; gi++)
                if (key[_offGroups + gi] > 0 && key[_offGroups + gi] + _groupRemain[depth][gi] < SkillAggregator.GroupSkillPieces) key[_offGroups + gi] = 0;
        }

        public IReadOnlyList<RankedBuild> Run()
        {
            // depth 0: talismans
            var states = new Dictionary<int[], State>(new KeyComparer());
            foreach (var t in _talismans)
            {
                var key = new int[_keyLength];
                for (var s = 0; s < _n; s++) key[s] = Math.Min(_rel.Caps[s], t.Skills[s]);
                for (var i = 0; i < 3; i++) { key[_offArmorSlots + i] = t.ArmorSlots[i]; key[_offWeaponSlots + i] = t.WeaponSlots[i]; }
                if (_weapon.SetBonus is { } ws && _rel.SetIndex.TryGetValue(ws, out var si)) key[_offSets + si] = 1;
                if (_weapon.GroupSkill is { } wg && _rel.GroupIndex.TryGetValue(wg, out var gi)) key[_offGroups + gi] = 1;
                Normalize(key, 0);
                Merge(states, key, null, t);
            }
            states = Prune(states, 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var depth = 0; depth < _kinds.Length; depth++)
            {
                _ct.ThrowIfCancellationRequested();
                var next = new Dictionary<int[], State>(new KeyComparer());
                var candidates = _armor[_kinds[depth]];
                foreach (var state in states.Values)
                {
                    if (candidates.Count == 0) { Merge(next, state.Key, state, NoPiece.Instance); continue; }
                    foreach (var c in candidates)
                    {
                        var key = (int[])state.Key.Clone();
                        for (var s = 0; s < _n; s++) key[s] = Math.Min(_rel.Caps[s], key[s] + c.Skills[s]);
                        for (var i = 0; i < 3; i++) key[_offArmorSlots + i] += c.ArmorSlots[i];
                        foreach (var si in c.SetIds) key[_offSets + si] = Math.Min(SetCap, key[_offSets + si] + 1);
                        if (c.GroupId >= 0) key[_offGroups + c.GroupId] = Math.Min(GroupCap, key[_offGroups + c.GroupId] + 1);
                        Normalize(key, depth + 1);
                        Merge(next, key, state, c);
                    }
                }
                var before = next.Count;
                var expandMs = sw.ElapsedMilliseconds; sw.Restart();
                states = Prune(next, depth + 1);
                _progress?.Report($"  {_kinds[depth]}: {before} states, {states.Count} after pruning (expand {expandMs} ms, prune {sw.ElapsedMilliseconds} ms)");
                sw.Restart();
            }

            var finals = states.Values.ToList();
            var builds = EvaluateAndReconstruct(finals);
            _progress?.Report($"  final evaluation {sw.ElapsedMilliseconds} ms");
            return builds;
        }

        private sealed class NoPiece { public static readonly NoPiece Instance = new(); }

        private static void Merge(Dictionary<int[], State> states, int[] key, State? prev, object item)
        {
            if (!states.TryGetValue(key, out var state))
                states[key] = state = new State { Key = key };
            if (state.Preds.Count < MaxPredsPerState) state.Preds.Add((prev, item));
        }

        /// <summary>Drops states that cannot reach the targets any more and states dominated by another state with the same set/group/weapon-slot profile.</summary>
        private Dictionary<int[], State> Prune(Dictionary<int[], State> states, int depth)
        {
            var kept = new Dictionary<int[], State>(new KeyComparer());
            var buckets = new Dictionary<long, List<State>>();
            foreach (var s in states.Values)
            {
                if (!Feasible(s.Key, depth)) continue;
                var b = BucketKey(s.Key);
                (buckets.TryGetValue(b, out var l) ? l : buckets[b] = []).Add(s);
            }
            foreach (var bucket in buckets.Values)
            {
                bucket.Sort((a, b) => Sum(b.Key).CompareTo(Sum(a.Key)));
                var front = new List<State>();
                foreach (var s in bucket)
                {
                    var dominated = false;
                    var checks = Math.Min(front.Count, MaxFrontChecks);
                    for (var i = 0; i < checks; i++) if (Dominates(front[i].Key, s.Key)) { dominated = true; break; }
                    if (!dominated) front.Add(s);
                }
                foreach (var s in front) kept[s.Key] = s;
            }

            if (kept.Count > _maxStatesPerDepth)
            {
                // beam cut: keep the most promising states (partial score plus a value for free slots)
                var ranked = kept.Values.Select(s => (State: s, H: Heuristic(s.Key))).OrderByDescending(x => x.H).Take(_maxStatesPerDepth);
                var cut = new Dictionary<int[], State>(new KeyComparer());
                foreach (var (s, _) in ranked) cut[s.Key] = s;
                return cut;
            }
            return kept;
        }

        private double Heuristic(int[] key)
        {
            var levels = key.AsSpan(0, _n).ToArray();
            var slotValue = 3 * key[_offArmorSlots + 2] + 2 * key[_offArmorSlots + 1] + key[_offArmorSlots];
            return Score(levels, key) + 4.0 * slotValue;
        }

        /// <summary>Mixed-radix id of the (weapon slots, set counts, group counts) part of a key; values are all below 5.</summary>
        private long BucketKey(int[] key)
        {
            long id = 0;
            for (var i = _offWeaponSlots; i < _keyLength; i++) id = id * 5 + key[i];
            return id;
        }

        private int Sum(int[] key)
        {
            var sum = 0;
            for (var i = 0; i < _offWeaponSlots; i++) sum += key[i];
            return sum;
        }

        private bool Dominates(int[] a, int[] b)
        {
            for (var s = 0; s < _n; s++) if (a[s] < b[s]) return false;
            var a3 = a[_offArmorSlots + 2]; var b3 = b[_offArmorSlots + 2];
            var a2 = a3 + a[_offArmorSlots + 1]; var b2 = b3 + b[_offArmorSlots + 1];
            var a1 = a2 + a[_offArmorSlots]; var b1 = b2 + b[_offArmorSlots];
            return a3 >= b3 && a2 >= b2 && a1 >= b1;
        }

        /// <summary>
        /// Targets must still be reachable: required set bonuses / group skills through the kinds that can still add a piece,
        /// skill levels through the remaining pieces at their best plus decorations in the slots that could exist.
        /// </summary>
        private bool Feasible(int[] key, int depth)
        {
            for (var si = 0; si < _rel.SetBonuses.Count; si++)
                if (_rel.SetTargets[si] > 0 && key[_offSets + si] + _setRemain[depth][si] < _rel.SetTargets[si]) return false;
            for (var gi = 0; gi < _rel.GroupSkills.Count; gi++)
                if (_rel.GroupTargets[gi] > 0 && key[_offGroups + gi] + _groupRemain[depth][gi] < _rel.GroupTargets[gi]) return false;

            // cumulative armor slots available in the best case: [>=3, >=2, >=1]
            var arm3 = key[_offArmorSlots + 2];
            var arm2 = arm3 + key[_offArmorSlots + 1];
            var arm1 = arm2 + key[_offArmorSlots];
            for (var d = depth; d < _kinds.Length; d++) { arm3 += _maxSlotsPerKind[d][0]; arm2 += _maxSlotsPerKind[d][1]; arm1 += _maxSlotsPerKind[d][2]; }
            var wpn3 = key[_offWeaponSlots + 2] + _weaponSlots3;
            var wpn2 = wpn3 + key[_offWeaponSlots + 1] + _weaponSlots2;
            var wpn1 = wpn2 + key[_offWeaponSlots] + _weaponSlots1;

            int needA3 = 0, needA2 = 0, needA1 = 0, needW3 = 0, needW2 = 0, needW1 = 0;
            for (var s = 0; s < _n; s++)
            {
                if (_rel.Targets[s] == 0) continue;
                var reach = key[s];
                for (var d = depth; d < _kinds.Length; d++) reach += _maxPerKind[d][s];
                var deficit = _rel.Targets[s] - reach;
                if (deficit <= 0) continue;
                var (minLevel, maxGrant, weaponSkill) = _targetDecoInfo[s];
                if (maxGrant == 0) return false;
                var decos = (deficit + maxGrant - 1) / maxGrant;
                if (weaponSkill) { if (minLevel >= 3) needW3 += decos; else if (minLevel == 2) needW2 += decos; else needW1 += decos; }
                else { if (minLevel >= 3) needA3 += decos; else if (minLevel == 2) needA2 += decos; else needA1 += decos; }
            }
            return needA3 <= arm3 && needA3 + needA2 <= arm2 && needA3 + needA2 + needA1 <= arm1
                && needW3 <= wpn3 && needW3 + needW2 <= wpn2 && needW3 + needW2 + needW1 <= wpn1;
        }

        // ---------------------------------------------------------------- evaluation

        private sealed record Scored(State State, double Score, int[] Levels, List<DecoCandidate> Decos);

        private IReadOnlyList<RankedBuild> EvaluateAndReconstruct(List<State> finals)
        {
            var best = new PriorityQueue<Scored, double>();
            var keep = Math.Max(_topN * 3, _topN + 5);
            var threshold = double.NegativeInfinity;
            // most promising first so the threshold rises quickly and the bound check skips the rest
            var maxFinals = Math.Max(2000, _maxStatesPerDepth / 5);
            var ordered = finals.OrderByDescending(s => Heuristic(s.Key)).Take(maxFinals).ToList();
            if (finals.Count > ordered.Count) _progress?.Report($"  scoring the {ordered.Count} most promising of {finals.Count} final states");
            foreach (var state in ordered)
            {
                StatesEvaluated++;
                if ((StatesEvaluated & 255) == 0) _ct.ThrowIfCancellationRequested();
                var free = SynthesizeSlots(state.Key);
                var baseLevels = state.Key.AsSpan(0, _n).ToArray();
                var covers = _filler.CoverTargets(baseLevels, free);
                if (covers.Count == 0) continue;
                var sets = state.Key; // set/group counts live in the key

                if (best.Count >= keep)
                {
                    // bound with the cheapest cover: base score plus the best single-decoration gains, one per remaining free slot
                    var cheapest = covers[0];
                    var covered = (int[])baseLevels.Clone();
                    foreach (var d in cheapest.Values) foreach (var (skill, level) in d.Grants) covered[skill] += level;
                    var freeCount = free.Count - cheapest.Count;
                    var maxSlotLevel = free.Count == 0 ? 0 : free.Max(f => f.Level);
                    var bound = Score(covered, sets) + _filler.RelaxedGainBound(covered, freeCount, maxSlotLevel, l => Score(l, sets));
                    if (bound <= threshold) continue;
                }

                Scored? bestHere = null;
                foreach (var cover in covers)
                {
                    var levels = (int[])baseLevels.Clone();
                    foreach (var d in cover.Values) foreach (var (skill, level) in d.Grants) levels[skill] += level;
                    var placed = new Dictionary<FreeSlot, DecoCandidate>(cover);
                    _filler.FillGreedy(levels, free, placed, l => Score(l, sets));
                    var score = Score(levels, sets);
                    if (bestHere is null || score > bestHere.Score)
                        bestHere = new Scored(state, score, levels, placed.Values.ToList());
                }
                if (bestHere is null || (best.Count >= keep && bestHere.Score <= threshold)) continue;
                best.Enqueue(bestHere, bestHere.Score);
                if (best.Count > keep) best.Dequeue();
                if (best.Count >= keep) threshold = best.Peek().Score;
            }

            var builds = new List<RankedBuild>();
            var seen = new HashSet<string>();
            foreach (var scored in best.UnorderedItems.Select(x => x.Element).OrderByDescending(s => s.Score))
            {
                foreach (var combo in Paths(scored.State).Take(MaxPredsPerState))
                {
                    var loadout = BuildLoadout(combo, scored.Decos);
                    if (loadout is null) continue;
                    var key = string.Join("|", loadout.ArmorPieces.Select(a => a.Piece.Id)) + "|" + loadout.Talisman?.Talisman.Name;
                    if (!seen.Add(key)) continue;
                    var result = DamageCalculator.Calculate(loadout, _data, _cond);
                    builds.Add(new RankedBuild(loadout, result, null, ""));
                }
            }
            return builds.OrderByDescending(b => b.Score).Take(_topN).ToList();
        }

        private double Score(int[] levels, int[] key)
        {
            var dict = new Dictionary<string, int>(_n);
            for (var s = 0; s < _n; s++)
                if (levels[s] > 0) dict[_rel.Skills[s]] = Math.Min(levels[s], _rel.MaxLevels[s]);
            var sets = new Dictionary<string, int>();
            for (var i = 0; i < _rel.SetBonuses.Count; i++) if (key[_offSets + i] > 0) sets[_rel.SetBonuses[i]] = key[_offSets + i];
            var groups = new Dictionary<string, int>();
            for (var i = 0; i < _rel.GroupSkills.Count; i++) if (key[_offGroups + i] > 0) groups[_rel.GroupSkills[i]] = key[_offGroups + i];
            return DamageCalculator.Calculate(_weapon, new ActiveSkills(dict, dict, sets, groups), _cond, trace: false).Total;
        }

        private List<FreeSlot> SynthesizeSlots(int[] key)
        {
            var free = new List<FreeSlot>();
            for (var i = 0; i < _weapon.Slots.Count; i++) free.Add(new FreeSlot(SkillKind.Weapon, _weapon.Slots[i], -1, i));
            var idx = 0;
            for (var l = 3; l >= 1; l--) for (var c = 0; c < key[_offWeaponSlots + l - 1]; c++) free.Add(new FreeSlot(SkillKind.Weapon, l, -2, idx++));
            for (var l = 3; l >= 1; l--) for (var c = 0; c < key[_offArmorSlots + l - 1]; c++) free.Add(new FreeSlot(SkillKind.Armor, l, 0, idx++));
            return free;
        }

        /// <summary>Enumerates the concrete (talisman, pieces...) combinations that lead to a state.</summary>
        private IEnumerable<List<object>> Paths(State state)
        {
            foreach (var (prev, item) in state.Preds)
            {
                if (prev is null) { yield return [item]; continue; }
                foreach (var path in Paths(prev)) yield return [.. path, item];
            }
        }

        private Loadout? BuildLoadout(List<object> combo, List<DecoCandidate> decos)
        {
            var talisman = (TalismanCandidate)combo[0];
            var pieces = new ArmorCandidate?[_kinds.Length];
            for (var d = 0; d < _kinds.Length; d++) pieces[d] = combo[d + 1] as ArmorCandidate;

            // real slots, then pack the decoration multiset into them
            var free = new List<FreeSlot>();
            for (var i = 0; i < _weapon.Slots.Count; i++) free.Add(new FreeSlot(SkillKind.Weapon, _weapon.Slots[i], -1, i));
            for (var i = 0; i < talisman.Talisman.Slots.Count; i++) free.Add(new FreeSlot(talisman.Talisman.Slots[i].Kind, talisman.Talisman.Slots[i].Level, -2, i));
            for (var d = 0; d < pieces.Length; d++)
            {
                var p = pieces[d];
                if (p is null) continue;
                var slots = p.EffectiveSlots;
                for (var i = 0; i < slots.Count; i++) free.Add(new FreeSlot(SkillKind.Armor, slots[i], d, i));
            }
            var placed = DecorationFiller.Pack(decos, free);
            if (placed is null) return null;

            Decoration?[] Decos(int owner, int count)
            {
                var arr = new Decoration?[count];
                foreach (var (slot, deco) in placed) if (slot.Owner == owner) arr[slot.Index] = deco.Decoration;
                return arr;
            }
            EquippedArmor? Armor(ArmorPieceKind kind)
            {
                var d = Array.IndexOf(_kinds, kind);
                var c = pieces[d];
                return c is null ? null : new EquippedArmor(c.Piece, c.Transcended, Decos(d, c.EffectiveSlots.Count));
            }

            return new Loadout
            {
                Weapon = new EquippedWeapon(_weapon, Decos(-1, _weapon.Slots.Count)),
                Head = Armor(ArmorPieceKind.Head),
                Chest = Armor(ArmorPieceKind.Chest),
                Arms = Armor(ArmorPieceKind.Arms),
                Waist = Armor(ArmorPieceKind.Waist),
                Legs = Armor(ArmorPieceKind.Legs),
                Talisman = talisman.Talisman.Rarity == 0 ? null : new EquippedTalisman(talisman.Talisman, Decos(-2, talisman.Talisman.Slots.Count)),
            };
        }
    }
}
