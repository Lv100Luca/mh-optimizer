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
            var baseRel = Relevance.Build(_request.Weapon, _request.TargetSkills, _request.Conditions, _data);
            var classes = _request.SkillPairCandidates.Where(p => p is not null).Select(p => p!)
                .GroupBy(p => (Set: baseRel.SetIndex.ContainsKey(p.SetBonus) ? p.SetBonus : OtherLabel,
                               Group: baseRel.GroupIndex.ContainsKey(p.GroupSkill) ? p.GroupSkill : OtherLabel))
                .ToList();
            var i = 0;
            foreach (var cls in classes)
            {
                ct.ThrowIfCancellationRequested();
                var representative = cls.First();
                var label = $"{cls.Key.Set} + {cls.Key.Group}";
                progress?.Report($"[{++i}/{classes.Count}] {label} ({cls.Count()} rollable pairs)");
                var weapon = _request.Weapon with
                {
                    SetBonus = cls.Key.Set == OtherLabel ? null : representative.SetBonus,
                    GroupSkill = cls.Key.Group == OtherLabel ? null : representative.GroupSkill,
                };
                var pair = cls.Key.Set == OtherLabel && cls.Key.Group == OtherLabel ? null : representative;
                results.Add(Search(weapon, pair, label, _request.Options.TopN, progress, ct));
            }
            results = results.OrderByDescending(r => r.BestScore).Take(_request.SkillPair.TopN).ToList();
        }

        return new OptimizationResult(results, DateTime.UtcNow - started);
    }

    private SkillPairResult Search(GogmaWeaponStats weapon, GogmaSkillPair? pair, string label, int topN, IProgress<string>? progress, CancellationToken ct)
    {
        var rel = Relevance.Build(weapon, _request.TargetSkills, _request.Conditions, _data);
        var armor = Candidates.Armor(_data, rel, _request.Options);
        var talismans = Candidates.Talismans(_request.Talismans, rel);
        var filler = new DecorationFiller(_data, rel);
        var kinds = Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray();
        var summary = $"candidates after pruning: {string.Join(", ", kinds.Select(k => $"{k} {armor[k].Count}"))}, talismans {talismans.Count}";
        progress?.Report("  " + summary);

        var search = new StateSearch(_data, rel, weapon, _request.Conditions, filler, armor, kinds, talismans, topN, progress, ct);
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
        private readonly (int MinLevel, int MaxGrant, bool Weapon)[] _targetDecoInfo;

        public long StatesEvaluated { get; private set; }

        public StateSearch(GameData data, Relevance rel, GogmaWeaponStats weapon, Conditions cond, DecorationFiller filler,
            Dictionary<ArmorPieceKind, List<ArmorCandidate>> armor, ArmorPieceKind[] kinds, List<TalismanCandidate> talismans,
            int topN, IProgress<string>? progress, CancellationToken ct)
        {
            _data = data; _rel = rel; _weapon = weapon; _cond = cond; _filler = filler; _armor = armor; _kinds = kinds;
            _talismans = talismans; _topN = topN; _progress = progress; _ct = ct;
            _n = rel.Skills.Count;
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
                Merge(states, key, null, t);
            }
            states = Prune(states, 0);

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
                        Merge(next, key, state, c);
                    }
                }
                var before = next.Count;
                states = Prune(next, depth + 1);
                _progress?.Report($"  {_kinds[depth]}: {before} states, {states.Count} after pruning");
            }

            return EvaluateAndReconstruct(states.Values.ToList());
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
            var feasible = states.Values.Where(s => Feasible(s.Key, depth)).ToList();
            var kept = new Dictionary<int[], State>(new KeyComparer());
            foreach (var bucket in feasible.GroupBy(s => BucketKey(s.Key), StringComparer.Ordinal))
            {
                var ordered = bucket.OrderByDescending(s => Sum(s.Key)).ToList();
                var front = new List<State>();
                foreach (var s in ordered)
                {
                    if (front.Any(f => Dominates(f.Key, s.Key))) continue;
                    front.Add(s);
                }
                foreach (var s in front) kept[s.Key] = s;
            }
            return kept;
        }

        private string BucketKey(int[] key)
        {
            var span = key.AsSpan(_offWeaponSlots, _keyLength - _offWeaponSlots);
            return string.Join(",", span.ToArray());
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

        /// <summary>Targets must still be reachable: remaining pieces at their best plus decorations in the slots that could exist.</summary>
        private bool Feasible(int[] key, int depth)
        {
            // cumulative armor slots available in the best case: [>=3, >=2, >=1]
            var arm3 = key[_offArmorSlots + 2];
            var arm2 = arm3 + key[_offArmorSlots + 1];
            var arm1 = arm2 + key[_offArmorSlots];
            for (var d = depth; d < _kinds.Length; d++) { arm3 += _maxSlotsPerKind[d][0]; arm2 += _maxSlotsPerKind[d][1]; arm1 += _maxSlotsPerKind[d][2]; }
            var wpn3 = key[_offWeaponSlots + 2] + _weapon.Slots.Count(l => l >= 3);
            var wpn2 = wpn3 + key[_offWeaponSlots + 1] + _weapon.Slots.Count(l => l == 2);
            var wpn1 = wpn2 + key[_offWeaponSlots] + _weapon.Slots.Count(l => l == 1);

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
            foreach (var state in finals)
            {
                StatesEvaluated++;
                if ((StatesEvaluated & 255) == 0) _ct.ThrowIfCancellationRequested();
                var free = SynthesizeSlots(state.Key);
                var levels = state.Key.AsSpan(0, _n).ToArray();
                var placed = _filler.CoverTargets(levels, free);
                if (placed is null) continue;
                foreach (var d in placed.Values) foreach (var (skill, level) in d.Grants) levels[skill] += level;

                if (best.Count >= keep)
                {
                    var optimistic = (int[])levels.Clone();
                    var freeCount = free.Count - placed.Count;
                    for (var s = 0; s < _n; s++) optimistic[s] = Math.Min(_rel.MaxLevels[s], optimistic[s] + freeCount);
                    if (Score(optimistic, state.Key) <= threshold) continue;
                }

                var sets = state.Key; // set/group counts live in the key
                _filler.FillGreedy(levels, free, placed, l => Score(l, sets));
                var score = Score(levels, sets);
                if (best.Count >= keep && score <= threshold) continue;
                best.Enqueue(new Scored(state, score, levels, placed.Values.ToList()), score);
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
            return DamageCalculator.Calculate(_weapon, new ActiveSkills(dict, dict, sets, groups), _cond).Total;
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
