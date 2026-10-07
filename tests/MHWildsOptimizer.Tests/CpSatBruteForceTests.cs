using System.Diagnostics;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

/// <summary>
/// Cross-checks the CP-SAT engine against an exhaustive enumeration on an instance small enough to enumerate: four armor sets
/// (4 pieces per slot, 1024 armor combinations) times three talismans. The enumeration shares no search code with the
/// optimizer: no candidate pruning, no decoration filler, no score model. Every armor + talisman combination gets its exact best
/// decoration fill from a dynamic program over the slots (all reachable skill-level vectors), scored with
/// <see cref="DamageCalculator"/>. The only shared piece is <see cref="Relevance"/>, which says which skills are worth a jewel.
/// The best score is compared against every combination. The ranking below it is compared over the pieces CP-SAT is given:
/// a piece dominated by another of its kind is pruned before the solve, so ties that need it are never reported (the
/// dominating piece scores at least as much, which the best-score check covers).
/// </summary>
public class CpSatBruteForceTests(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    // Weakness Exploit / Agitator / Maximum Might / Antivirus carriers with different slots, the Gore set bonus (the weapon
    // rolled it, and Antivirus only counts with it), Lord's Soul, and groups / sets that do not score under these conditions
    private static readonly string[] KeptSets = ["Gore α", "Arkveld γ", "Dahaad γ", "Lagiacrus β"];

    private static readonly List<TalismanInput> Talismans =
    [
        new() { Name = "Golden Age Charm (Attack Boost 3, Iron Skin 3)", Rarity = 8, Skills = new() { ["Attack Boost"] = 3, ["Iron Skin"] = 3 }, Slots = ["weapon1", "armor1"] },
        new() { Name = "Secret Charm (Attack Boost 3, Burst 1)", Rarity = 7, Skills = new() { ["Attack Boost"] = 3, ["Burst"] = 1 }, Slots = ["armor1", "armor1"] },
        new() { Name = "Historical Charm (Attack Boost 2, Maximum Might 2)", Rarity = 6, Skills = new() { ["Attack Boost"] = 2, ["Maximum Might"] = 2 }, Slots = ["armor1", "armor1"] },
    ];

    /// <summary>Deep enough to get past the builds tied at the top score (9 armor + talisman combinations on this instance).</summary>
    private const int TopN = 12;
    /// <summary>The CP-SAT score is fixed point (multipliers to 1e-4), so builds closer than this may swap places.</summary>
    private const double RoundingBand = 0.1;

    [Fact]
    public void CpSatMatchesAnExhaustiveEnumeration()
    {
        var data = TestData.Data;
        var excluded = data.Armor.Where(a => a.Rarity >= 7 && a.Set is not null && !KeptSets.Contains(a.Set)).Select(a => a.Set!).Distinct().ToList();
        var request = new OptimizationRequest
        {
            // few weapon slots and several armor skills competing for slots, so combinations do not all max out the same skills
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 0, Slots = [1], SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" },
            TargetSkills = new() { ["Weakness Exploit"] = 4 },
            Conditions = Conditions.AllOff with
            {
                HittingWeakPoint = true, MonsterEnraged = true, StaminaFull = true, FrenzyOvercome = true, GutsNotYetTriggered = true,
            },
            Talismans = new TalismanSettings { IncludeCraftable = false },
            Options = new OptimizerOptions { TopN = TopN, MinRarity = 7, ExcludeSets = excluded, RequireWeaponCoreSkills = false, Engine = OptimizerEngine.CpSat },
        };
        var resolved = RequestLoader.Resolve(request, data, RepoRoot, Talismans);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));

        var sw = Stopwatch.StartNew();
        var rel = Relevance.Build(resolved.Weapon, resolved, data);
        var candidates = Candidates.Armor(data, rel, resolved.Options).Values.SelectMany(l => l).Select(c => c.Piece.Name)
            .Concat(Candidates.Talismans(resolved.Talismans, rel).Select(t => t.Talisman.Name)).ToHashSet();
        var brute = Enumerate(data, resolved, candidates);
        output.WriteLine($"exhaustive: {brute.Combinations} armor + talisman combinations, {brute.Feasible} meet the targets, {brute.DistinctScores} distinct best scores, {brute.TiedAtTop} tied at the top, {sw.ElapsedMilliseconds} ms");
        output.WriteLine($"pruned before the solve: {string.Join(", ", data.Armor.Where(a => a.Rarity >= 7 && KeptSets.Contains(a.Set ?? "") && !candidates.Contains(a.Name)).Select(a => a.Name))}");
        sw.Restart();
        var cp = new Optimizer(data, resolved).Run().PairResults.Single();
        output.WriteLine($"cp-sat: {cp.CandidateSummary}, {sw.ElapsedMilliseconds} ms");
        for (var i = 0; i < TopN; i++)
            output.WriteLine($"#{i + 1}  exhaustive {brute.BestOfCandidates[i].Score:0.000} {brute.BestOfCandidates[i].Key}   cp-sat {(i < cp.Builds.Count ? $"{cp.Builds[i].Score:0.000} {Key(cp.Builds[i].Loadout)}" : "-")}");

        Assert.True(brute.Best.Count >= TopN, "the instance should have at least TopN feasible combinations");
        Assert.True(brute.DistinctScores >= 100, $"only {brute.DistinctScores} distinct scores: the instance does not tell combinations apart");
        Assert.True(brute.TiedAtTop < TopN, $"{brute.TiedAtTop} combinations tie at the top: compare more builds to get past the tie");
        Assert.Equal(TopN, cp.Builds.Count);
        // nothing can beat the exhaustive maximum; CP-SAT must reach it up to its fixed-point rounding
        Assert.True(cp.BestScore <= brute.Best[0].Score + 1e-6, $"cp-sat {cp.BestScore} above the exhaustive maximum {brute.Best[0].Score}: the enumeration misses builds");
        Assert.True(cp.BestScore >= brute.Best[0].Score - RoundingBand, $"cp-sat {cp.BestScore:0.000} below the exhaustive maximum {brute.Best[0].Score:0.000}");
        for (var i = 0; i < TopN; i++)
            Assert.True(Math.Abs(cp.Builds[i].Score - brute.BestOfCandidates[i].Score) <= RoundingBand,
                $"#{i + 1}: cp-sat {cp.Builds[i].Score:0.000} vs exhaustive {brute.BestOfCandidates[i].Score:0.000} over the same pieces");
        // every reported build is a real combination with its best decoration fill
        foreach (var b in cp.Builds)
        {
            Assert.True(brute.ByCombination.TryGetValue(Key(b.Loadout), out var expected), $"{Key(b.Loadout)} does not meet the targets in the enumeration");
            Assert.True(Math.Abs(b.Score - expected) <= RoundingBand, $"{Key(b.Loadout)}: cp-sat {b.Score:0.000}, best fill {expected:0.000}");
        }
        // a clear winner must be the same armor + talisman
        if (brute.BestOfCandidates[0].Score - brute.BestOfCandidates[1].Score > RoundingBand)
            Assert.Equal(brute.BestOfCandidates[0].Key, Key(cp.Builds[0].Loadout));
    }

    private static string Key(Loadout l) => string.Join(" | ", l.ArmorPieces.OrderBy(a => a.Piece.Piece).Select(a => a.Piece.Name)) + " | " + l.Talisman?.Talisman.Name;

    /// <param name="ByCombination">Best score of every feasible armor + talisman combination.</param>
    /// <param name="Best">Best combinations over all pieces.</param>
    /// <param name="BestOfCandidates">Best combinations that only use pieces and talismans that survive pruning.</param>
    private sealed record Enumeration(int Combinations, int Feasible, int DistinctScores, int TiedAtTop, List<(double Score, string Key)> Best,
        List<(double Score, string Key)> BestOfCandidates, Dictionary<string, double> ByCombination);

    private static Enumeration Enumerate(GameData data, ResolvedRequest resolved, HashSet<string> candidates)
    {
        var weapon = resolved.Weapon;
        var cond = resolved.Conditions;
        var skills = Relevance.Build(weapon, resolved, data).Skills.ToArray();
        var n = skills.Length;
        var max = skills.Select(s => data.Skill(s).MaxLevel).ToArray();
        int Index(string skill) => Array.IndexOf(skills, skill);

        // every decoration that grants a relevant skill, reduced to (kind, slot level, relevant grants)
        var decos = data.Decorations
            .Select(d => (d.Kind, d.Slot, Grants: Vector(d.Skills)))
            .Where(d => d.Grants.Any(g => g > 0))
            .DistinctBy(d => $"{d.Kind}{d.Slot}:{string.Join(",", d.Grants)}")
            .ToList();
        int[] Vector(IEnumerable<SkillGrant> grants)
        {
            var v = new int[n];
            foreach (var g in grants) if (Index(g.Skill) is var i and >= 0) v[i] += g.Level;
            return v;
        }

        // all skill-level vectors the decorations can add in the given slots (levels capped at the skill max: higher is never worth more)
        int Pack(int[] v) { var p = 0; for (var i = 0; i < n; i++) p = p * 8 + v[i]; return p; }
        int[] Unpack(int p) { var v = new int[n]; for (var i = n - 1; i >= 0; i--) { v[i] = p % 8; p /= 8; } return v; }
        HashSet<int> Fill(IEnumerable<(SkillKind Kind, int Level)> slots)
        {
            var states = new HashSet<int> { 0 };
            foreach (var slot in slots)
            {
                var next = new HashSet<int>(states);
                foreach (var s in states)
                {
                    var v = Unpack(s);
                    foreach (var d in decos)
                    {
                        if (d.Kind != slot.Kind || d.Slot > slot.Level) continue;
                        var w = new int[n];
                        for (var i = 0; i < n; i++) w[i] = Math.Min(max[i], v[i] + d.Grants[i]);
                        next.Add(Pack(w));
                    }
                }
                states = next;
            }
            return states;
        }

        var kinds = Enum.GetValues<ArmorPieceKind>();
        var pieces = kinds.Select(k => data.Armor.Where(a => a.Piece == k && a.Rarity >= 7 && a.Set is not null && KeptSets.Contains(a.Set)).ToList()).ToArray();
        foreach (var p in pieces) Assert.Equal(KeptSets.Length, p.Count);

        var fillCache = new Dictionary<string, int[][]>();
        int[][] FillCached(List<(SkillKind Kind, int Level)> slots)
        {
            var key = string.Join(",", slots.OrderBy(s => s.Kind).ThenBy(s => s.Level));
            if (!fillCache.TryGetValue(key, out var fills)) fillCache[key] = fills = Fill(slots).Select(Unpack).ToArray();
            return fills;
        }
        var scoreCache = new Dictionary<(string, int), double>();

        var best = new List<(double Score, string Key, bool Candidate)>();
        int combinations = 0, feasible = 0;
        foreach (var talisman in resolved.Talismans)
        {
            // weapon jewels only grant weapon skills and armor jewels only armor skills, so the two fills combine freely
            var weaponFills = FillCached([.. weapon.Slots.Select(l => (SkillKind.Weapon, l)), .. talisman.Slots.Where(s => s.Kind == SkillKind.Weapon).Select(s => (SkillKind.Weapon, s.Level))]);
            var combo = new ArmorPiece[kinds.Length];
            void Choose(int depth)
            {
                if (depth < kinds.Length)
                {
                    foreach (var p in pieces[depth]) { combo[depth] = p; Choose(depth + 1); }
                    return;
                }
                combinations++;
                var baseLevels = Vector(combo.SelectMany(p => p.Skills).Concat(talisman.Skills));
                var armorFills = FillCached([.. combo.SelectMany(p => p.Slots).Select(l => (SkillKind.Armor, l)), .. talisman.Slots.Where(s => s.Kind == SkillKind.Armor).Select(s => (SkillKind.Armor, s.Level))]);

                // set bonuses and group skills by name; the weapon counts as one piece of its rolled pair
                var sets = combo.SelectMany(p => p.SetBonus).Append(weapon.SetBonus).OfType<string>().GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
                var groups = combo.Select(p => p.GroupSkill).Append(weapon.GroupSkill).OfType<string>().GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
                var signature = string.Join(",", sets.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")) + "|" + string.Join(",", groups.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

                var comboBest = double.NegativeInfinity;
                var levels = new int[n];
                foreach (var w in weaponFills)
                    foreach (var a in armorFills)
                    {
                        for (var i = 0; i < n; i++) levels[i] = Math.Min(max[i], baseLevels[i] + w[i] + a[i]);
                        var ok = true;
                        foreach (var (skill, target) in resolved.TargetSkills) if (levels[Index(skill)] < target) { ok = false; break; }
                        if (!ok) continue;
                        var packed = Pack(levels);
                        if (!scoreCache.TryGetValue((signature, packed), out var score))
                        {
                            var dict = new Dictionary<string, int>();
                            for (var i = 0; i < n; i++) if (levels[i] > 0) dict[skills[i]] = levels[i];
                            score = DamageCalculator.Calculate(weapon, new ActiveSkills(dict, dict, sets, groups), cond, trace: false).Total;
                            scoreCache[(signature, packed)] = score;
                        }
                        comboBest = Math.Max(comboBest, score);
                    }
                if (double.IsNegativeInfinity(comboBest)) return;
                feasible++;
                var key = string.Join(" | ", combo.Select(p => p.Name)) + " | " + talisman.Name;
                best.Add((comboBest, key, combo.All(p => candidates.Contains(p.Name)) && candidates.Contains(talisman.Name)));
            }
            Choose(0);
        }
        var distinct = best.Select(b => Math.Round(b.Score, 6)).Distinct().Count();
        var top = best.Max(b => b.Score);
        return new Enumeration(combinations, feasible, distinct, best.Count(b => b.Score > top - 1e-9),
            [.. best.OrderByDescending(b => b.Score).Take(TopN).Select(b => (b.Score, b.Key))],
            [.. best.Where(b => b.Candidate).OrderByDescending(b => b.Score).Take(TopN).Select(b => (b.Score, b.Key))],
            best.ToDictionary(b => b.Key, b => b.Score));
    }
}
