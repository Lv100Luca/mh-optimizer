using System.Diagnostics;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

/// <summary>
/// Cross-checks CP-SAT's best build for a real configuration from inputs/ (too big to enumerate) with an independent exact
/// decoration fill (<see cref="ExactFill"/>):
/// 1. neighbourhood: every build that differs from the winner in one armor piece or the talisman (all pieces the options allow,
///    no pruning) or in two of them (pruned candidates) must not beat it;
/// 2. slice: the talisman and the two armor slots with the fewest candidates fixed to the winner's, the other three slots
///    enumerated exhaustively over the candidates, compared with CP-SAT solving the same slice.
/// Opt-in, reads the user's configuration:  CROSSCHECK_CONFIG=Current dotnet test --filter Category=CrossCheck
/// </summary>
[Trait("Category", "CrossCheck")]
public class CpSatConfigCrossCheck(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;
    /// <summary>CP-SAT scores in fixed point (multipliers to 1e-4); builds closer than this may swap places.</summary>
    private const double RoundingBand = 0.1;

    [Fact]
    public void CpSatBestBuildSurvivesNeighbourhoodAndSliceChecks()
    {
        var name = Environment.GetEnvironmentVariable("CROSSCHECK_CONFIG");
        if (string.IsNullOrWhiteSpace(name)) { output.WriteLine("Skipped: set CROSSCHECK_CONFIG to a configuration name in inputs/ (e.g. Current)."); return; }

        var data = TestData.Data;
        var resolved = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", name + ".json"), data);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        resolved = resolved with { Options = resolved.Options with { Engine = OptimizerEngine.CpSat, TopN = 1 } };
        var options = resolved.Options;

        var sw = Stopwatch.StartNew();
        var best = new Optimizer(data, resolved).Run().PairResults.OrderByDescending(p => p.BestScore).First();
        var winner = best.Builds[0];
        output.WriteLine($"cp-sat on '{name}': {winner.Score:0.000} ({best.Label}; {best.CandidateSummary}) in {sw.ElapsedMilliseconds} ms");
        output.WriteLine("  " + Describe(winner.Loadout));

        var weapon = winner.Loadout.Weapon.Stats; // carries the skill pair that won
        var rel = Relevance.Build(weapon, resolved, data);
        var fill = new ExactFill(data, rel, weapon, resolved.Conditions, options.AllowTranscendence);
        var kinds = Enum.GetValues<ArmorPieceKind>();
        var pieces = kinds.Select(k => winner.Loadout.ArmorPieces.Single(a => a.Piece.Piece == k).Piece).ToArray();
        var talisman = winner.Loadout.Talisman?.Talisman ?? new Talisman("(no talisman)", 0, [], [], TalismanSource.Crafted);

        // the shortcuts below (upper bounds, dropping beaten skill combinations) need a score that never falls when a level rises
        var violations = fill.MonotonicityViolations(pieces, weapon, 3000);
        Assert.True(violations == 0, $"{violations} cases where raising a skill level lowers the score: the exact fill's shortcuts would be unsound");

        // the winner itself: CP-SAT's decorations must be as good as the exact best fill
        var own = fill.Best(pieces, talisman, double.NegativeInfinity);
        output.WriteLine($"exact best fill of the winner: {own:0.000}");
        Assert.True(Math.Abs(own - winner.Score) <= RoundingBand, $"winner scores {winner.Score:0.000}, its exact best fill {own:0.000}");

        // ---------------- 1. neighbourhood ----------------
        sw.Restart();
        var excluded = options.ExcludeSets.ToHashSet();
        var allPieces = kinds.Select(k => data.Armor.Where(a => a.Piece == k && a.Rarity >= options.MinRarity && (a.Set is null || !excluded.Contains(a.Set))).ToList()).ToArray();
        var candidates = Candidates.Armor(data, rel, options);
        var candidatePieces = kinds.Select(k => candidates[k].Select(c => c.Piece).ToList()).ToArray();
        var candidateTalismans = Candidates.Talismans(resolved.Talismans, rel).Select(t => t.Talisman).Where(t => t.Rarity > 0).ToList();

        var threshold = winner.Score + RoundingBand;
        var checkedBuilds = 0;
        var beaten = new List<string>();
        void Check(ArmorPiece[] p, Talisman t)
        {
            checkedBuilds++;
            var s = fill.Best(p, t, threshold);
            if (s > threshold) beaten.Add($"{s:0.000}: {string.Join(", ", p.Select(x => x.Name))} | {t.Name}");
        }
        // one change, over everything the options allow
        for (var d = 0; d < kinds.Length; d++)
            foreach (var alt in allPieces[d].Where(a => a.Id != pieces[d].Id))
                Check(Replace(pieces, d, alt), talisman);
        foreach (var t in resolved.Talismans.Where(t => t.Name != talisman.Name)) Check(pieces, t);
        var singles = checkedBuilds;
        // two changes, over the pruned candidates (position 5 = talisman)
        for (var a = 0; a < kinds.Length + 1; a++)
            for (var b = a + 1; b < kinds.Length + 1; b++)
                foreach (var x in Options(a))
                    foreach (var y in Options(b))
                    {
                        var p = pieces;
                        var t = talisman;
                        if (a < kinds.Length) p = Replace(p, a, (ArmorPiece)x); else t = (Talisman)x;
                        if (b < kinds.Length) p = Replace(p, b, (ArmorPiece)y); else t = (Talisman)y;
                        Check(p, t);
                    }
        IEnumerable<object> Options(int position) => position < kinds.Length
            ? candidatePieces[position].Where(c => c.Id != pieces[position].Id)
            : candidateTalismans.Where(c => c.Name != talisman.Name);
        output.WriteLine($"neighbourhood: {singles} one-change and {checkedBuilds - singles} two-change builds checked in {sw.ElapsedMilliseconds} ms, {beaten.Count} beat the winner");
        Assert.True(beaten.Count == 0, "builds beating CP-SAT's best:\n" + string.Join("\n", beaten.Take(10)));

        // ---------------- 2. slice ----------------
        sw.Restart();
        var free = Enumerable.Range(0, kinds.Length).OrderByDescending(d => candidatePieces[d].Count).Take(3).OrderBy(d => d).ToArray();
        var fixedKinds = Enumerable.Range(0, kinds.Length).Except(free).ToArray();
        var sliceArmor = kinds.ToDictionary(k => k, k => candidates[k]);
        foreach (var d in fixedKinds) sliceArmor[kinds[d]] = candidates[kinds[d]].Where(c => c.Piece.Id == pieces[d].Id).ToList();
        foreach (var d in fixedKinds) Assert.Single(sliceArmor[kinds[d]]);
        var sliceTalismans = Candidates.Talismans(resolved.Talismans, rel).Where(t => t.Talisman.Name == talisman.Name).ToList();
        Assert.Single(sliceTalismans);
        var cp = new CpSatSearch(data, rel, weapon, resolved.Conditions, new DecorationFiller(data, rel), sliceArmor, kinds, sliceTalismans,
            1, options.EffectiveThreads, options.CpSatTimeLimitSeconds, null, CancellationToken.None);
        var cpSlice = cp.Run().Single().Score;
        var cpMs = sw.ElapsedMilliseconds;
        sw.Restart();

        var bruteSlice = own;
        var sliceBuilds = 0;
        var combo = (ArmorPiece[])pieces.Clone();
        void Enumerate(int i)
        {
            if (i == free.Length)
            {
                sliceBuilds++;
                bruteSlice = Math.Max(bruteSlice, fill.Best(combo, talisman, bruteSlice));
                return;
            }
            foreach (var p in candidatePieces[free[i]]) { combo[free[i]] = p; Enumerate(i + 1); }
            combo[free[i]] = pieces[free[i]];
        }
        Enumerate(0);
        output.WriteLine($"slice: {string.Join(", ", free.Select(d => $"{kinds[d]} {candidatePieces[d].Count}"))} enumerated ({sliceBuilds} builds) with " +
                         $"{string.Join(", ", fixedKinds.Select(d => kinds[d]))} and the talisman fixed: exhaustive {bruteSlice:0.000} in {sw.ElapsedMilliseconds} ms, " +
                         $"cp-sat {cpSlice:0.000} ({cp.Summary}) in {cpMs} ms");
        output.WriteLine($"exact fill: {fill.Fronts} slot layouts, {fill.ScoreCalls} damage calculations");
        Assert.True(Math.Abs(cpSlice - bruteSlice) <= RoundingBand, $"slice: cp-sat {cpSlice:0.000} vs exhaustive {bruteSlice:0.000}");
        Assert.True(bruteSlice <= winner.Score + RoundingBand, $"slice optimum {bruteSlice:0.000} beats the overall cp-sat optimum {winner.Score:0.000}");
    }

    private static ArmorPiece[] Replace(ArmorPiece[] pieces, int index, ArmorPiece piece)
    {
        var copy = (ArmorPiece[])pieces.Clone();
        copy[index] = piece;
        return copy;
    }

    private static string Describe(Loadout l) =>
        string.Join(", ", l.ArmorPieces.Select(a => a.Piece.Name)) + " | " + (l.Talisman?.Talisman.Name ?? "no talisman");
}

/// <summary>
/// Exact best decoration fill of a fixed armor + talisman combination, independent of the optimizer's search code. Weapon and
/// armor slots are filled separately (each keeps every skill-level combination its jewels can reach that no other reachable
/// combination beats on every skill), then every pair is scored with <see cref="DamageCalculator"/>. Dropping beaten
/// combinations and the upper bounds used to skip work assume the score never falls when a skill level rises
/// (<see cref="MonotonicityViolations"/> checks that). Skills come from <see cref="Relevance"/>, capped at its levels.
/// </summary>
internal sealed class ExactFill
{
    private readonly Relevance _rel;
    private readonly GogmaWeaponStats _weapon;
    private readonly Conditions _cond;
    private readonly bool _transcend;
    private readonly int _n;
    private readonly int[] _cap;
    private readonly List<(SkillKind Kind, int Level, int[] Grants)> _decos;
    private readonly Dictionary<string, List<int[]>> _fronts = new();
    private readonly Dictionary<(string, long), double> _scores = new();

    public long ScoreCalls { get; private set; }
    public int Fronts => _fronts.Count;

    public ExactFill(GameData data, Relevance rel, GogmaWeaponStats weapon, Conditions cond, bool transcend)
    {
        _rel = rel; _weapon = weapon; _cond = cond; _transcend = transcend;
        _n = rel.Skills.Count;
        _cap = rel.Caps;
        _decos = data.Decorations
            .Select(d => (d.Kind, d.Slot, Grants: Vector(d.Skills)))
            .Where(d => d.Grants.Any(g => g > 0))
            .DistinctBy(d => $"{d.Kind}{d.Slot}:{string.Join(",", d.Grants)}")
            .ToList();
    }

    private int[] Vector(IEnumerable<SkillGrant> grants)
    {
        var v = new int[_n];
        foreach (var g in grants) if (_rel.IndexOf(g.Skill) is var i and >= 0) v[i] += g.Level;
        return v;
    }

    private IReadOnlyList<int> Slots(ArmorPiece p) => _transcend && !p.SlotsTranscended.SequenceEqual(p.Slots) ? p.SlotsTranscended : p.Slots;

    /// <summary>Best score of the combination with any decorations, or -infinity when it misses a target or cannot beat <paramref name="threshold"/>.</summary>
    public double Best(ArmorPiece[] pieces, Talisman talisman, double threshold)
    {
        var baseLevels = Vector(pieces.SelectMany(p => p.Skills).Concat(talisman.Skills));
        var weaponFront = Front([.. _weapon.Slots.Select(l => (SkillKind.Weapon, l)), .. talisman.Slots.Where(s => s.Kind == SkillKind.Weapon).Select(s => (SkillKind.Weapon, s.Level))]);
        var armorFront = Front([.. pieces.SelectMany(p => Slots(p)).Select(l => (SkillKind.Armor, l)), .. talisman.Slots.Where(s => s.Kind == SkillKind.Armor).Select(s => (SkillKind.Armor, s.Level))]);
        var (sets, groups, signature) = Pieces(pieces);

        var armorMax = Max(armorFront);
        var best = double.NegativeInfinity;
        var v = new int[_n];
        foreach (var w in weaponFront)
        {
            // optimistic: this weapon fill with the best armor fill for every skill at once
            Sum(baseLevels, w, armorMax, v);
            if (!MeetsTargets(v) || Score(v, sets, groups, signature) <= Math.Max(threshold, best)) continue;
            foreach (var a in armorFront)
            {
                Sum(baseLevels, w, a, v);
                if (!MeetsTargets(v)) continue;
                var s = Score(v, sets, groups, signature);
                if (s > best) best = s;
            }
        }
        return best > threshold ? best : double.NegativeInfinity;
    }

    /// <summary>Random skill levels with the given pieces' set bonuses: how often one more level of a skill lowers the score.</summary>
    public int MonotonicityViolations(ArmorPiece[] pieces, GogmaWeaponStats weapon, int samples)
    {
        var (sets, groups, signature) = Pieces(pieces);
        var rng = new Random(7);
        var violations = 0;
        for (var k = 0; k < samples; k++)
        {
            var v = _cap.Select(c => rng.Next(c + 1)).ToArray();
            var s = Score(v, sets, groups, signature);
            for (var i = 0; i < _n; i++)
            {
                if (v[i] >= _cap[i]) continue;
                v[i]++;
                if (Score(v, sets, groups, signature) < s - 1e-9) violations++;
                v[i]--;
            }
        }
        return violations;
    }

    private (Dictionary<string, int> Sets, Dictionary<string, int> Groups, string Signature) Pieces(ArmorPiece[] pieces)
    {
        // set bonuses and group skills by name; the Gogma weapon counts as one piece of its rolled pair
        var sets = pieces.SelectMany(p => p.SetBonus).Append(_weapon.SetBonus).OfType<string>().GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        var groups = pieces.Select(p => p.GroupSkill).Append(_weapon.GroupSkill).OfType<string>().GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        var signature = string.Join(",", sets.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")) + "|" + string.Join(",", groups.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        return (sets, groups, signature);
    }

    private void Sum(int[] a, int[] b, int[] c, int[] into)
    {
        for (var i = 0; i < _n; i++) into[i] = Math.Min(_cap[i], a[i] + b[i] + c[i]);
    }

    private int[] Max(List<int[]> front)
    {
        var m = new int[_n];
        foreach (var v in front) for (var i = 0; i < _n; i++) m[i] = Math.Max(m[i], v[i]);
        return m;
    }

    private bool MeetsTargets(int[] v)
    {
        for (var i = 0; i < _n; i++) if (v[i] < _rel.Targets[i]) return false;
        return true;
    }

    private double Score(int[] v, Dictionary<string, int> sets, Dictionary<string, int> groups, string signature)
    {
        long packed = 0;
        for (var i = 0; i < _n; i++) packed = packed * 8 + v[i];
        if (_scores.TryGetValue((signature, packed), out var s)) return s;
        var levels = new Dictionary<string, int>();
        for (var i = 0; i < _n; i++) if (v[i] > 0) levels[_rel.Skills[i]] = v[i];
        ScoreCalls++;
        s = DamageCalculator.Calculate(_weapon, new ActiveSkills(levels, levels, sets, groups), _cond, trace: false).Total;
        _scores[(signature, packed)] = s;
        return s;
    }

    /// <summary>Every skill-level vector the jewels can add in these slots that no other reachable vector beats on every skill.</summary>
    private List<int[]> Front(List<(SkillKind Kind, int Level)> slots)
    {
        var key = string.Join(",", slots.OrderBy(s => s.Kind).ThenByDescending(s => s.Level));
        if (_fronts.TryGetValue(key, out var cached)) return cached;
        var states = new List<int[]> { new int[_n] };
        foreach (var slot in slots.OrderByDescending(s => s.Level))
        {
            var next = new Dictionary<long, int[]>();
            foreach (var v in states)
            {
                Add(next, v);
                foreach (var d in _decos)
                {
                    if (d.Kind != slot.Kind || d.Level > slot.Level) continue;
                    var w = new int[_n];
                    for (var i = 0; i < _n; i++) w[i] = Math.Min(_cap[i], v[i] + d.Grants[i]);
                    Add(next, w);
                }
            }
            states = Undominated([.. next.Values]);
        }
        _fronts[key] = states;
        return states;

        void Add(Dictionary<long, int[]> into, int[] v)
        {
            long p = 0;
            for (var i = 0; i < _n; i++) p = p * 8 + v[i];
            into.TryAdd(p, v);
        }
    }

    private List<int[]> Undominated(List<int[]> vectors)
    {
        // strongest first, so a vector can only be beaten by one already kept
        vectors.Sort((a, b) => b.Sum().CompareTo(a.Sum()));
        var kept = new List<int[]>();
        foreach (var v in vectors)
        {
            var beaten = false;
            foreach (var k in kept)
            {
                var all = true;
                for (var i = 0; i < _n && all; i++) if (k[i] < v[i]) all = false;
                if (all) { beaten = true; break; }
            }
            if (!beaten) kept.Add(v);
        }
        return kept;
    }
}
