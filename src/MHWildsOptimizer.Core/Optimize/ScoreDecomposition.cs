using System.Collections.Concurrent;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Optimize;

public enum FeatureKind { Skill, SetBonus, GroupSkill }

/// <summary>An input of the damage formula that the build decides: a skill level, a set-bonus tier (0-2) or a group skill (0/1).</summary>
/// <param name="Index">Index into the matching <see cref="Relevance"/> list.</param>
/// <param name="Max">Largest value: the skill cap, 2 for set bonuses, 1 for group skills.</param>
public sealed record Feature(FeatureKind Kind, int Index, string Name, int Max);

/// <summary>What one combination of feature values puts into each term of the damage formula (see <see cref="DamageCalculator"/>).</summary>
/// <param name="Procs">Proc damage per 100 MV that does not depend on the hit (Bad Blood).</param>
/// <param name="Shockwave">The Dark Arts shockwave fires; its damage follows EFR, so it is added on top of the composed terms.</param>
/// <param name="RawMult">Multiplies EFR after the flat bonuses: the bow's shot skills over the attack (<see cref="DamageResult.ShotMultiplier"/>).</param>
public readonly record struct Channels(double RawPct, double RawFlat, double Affinity, double ElePct, double EleFlat, double CritMult, double CritEleMult, double Procs, bool Shockwave,
    double RawMult = 1);

/// <summary>Features whose contributions interact and are therefore tabulated jointly.</summary>
public sealed class ScoreUnit
{
    public required int[] Features { get; init; }
    /// <summary>State index = sum of feature value * stride.</summary>
    public required int[] Strides { get; init; }
    /// <summary>[side][state]</summary>
    public required Channels[][] Tables { get; init; }
    public int States => Tables[0].Length;
}

/// <summary>
/// One part of the score with its weapon constants: a health side (full health, or red/low health) and, for a sequence whose
/// steps change conditions, one segment of it (<see cref="Conditions.Segments"/>).
/// </summary>
/// <param name="Health">The health side; the score is the best health side's weighted sum of its segments.</param>
/// <param name="Weight">The segment's share of the sequence's motion value (1 without segments) times the part's share of the time (<see cref="Conditions.UptimeParts"/>).</param>
/// <param name="RawFactor">EFR per true raw and crit factor: the attack's MV-weighted sharpness (and phial), <see cref="ResolvedAttackProfile.RawFactor"/>.</param>
/// <param name="SharpEle">The weapon's element sharpness modifier (the Dark Arts shockwave's element).</param>
/// <param name="ShockwaveRawShare">Shockwave damage per 100 MV = this x EFR + <paramref name="ShockwaveConstant"/>.</param>
/// <param name="BaseRawFlat">Flat raw the conditions give every build (Powercharm, meal, buffs); the units' flat raw comes on top.</param>
/// <param name="BaseRawPct">Raw multiplier the conditions give every build (buffs); the units' multipliers multiply it.</param>
/// <param name="BaseAffinity">Affinity the conditions give every build (buffs).</param>
/// <param name="BaseElePct">Element multiplier the conditions give every build (buffs).</param>
/// <param name="BaseEleFlat">True element the conditions give every build.</param>
public sealed record ScoreSide(Conditions Conditions, int Health, double Weight, ResolvedAttackProfile Attack, double RawFactor, double SharpEle,
    double BaseCritMult, double BaseCritEleMult, double ElementCap, double ShockwaveRawShare, double ShockwaveConstant, double BaseRawFlat = 0,
    double BaseRawPct = 1, int BaseAffinity = 0, double BaseElePct = 1, double BaseEleFlat = 0, int Segment = 0)
{
    /// <summary>Element per true element point per 100 MV of the segment's attack, sharpness included: <see cref="ResolvedAttackProfile.ElementFactor"/>.</summary>
    public double ElementScale => Attack.ElementFactor;
    /// <summary>Raw of the hits that cannot crit, per true raw: <see cref="ResolvedAttackProfile.NoCritRawFactor"/>.</summary>
    public double RawNoCrit => Attack.NoCritRawFactor;
    /// <summary>Element of the hits that cannot crit, per true element point: <see cref="ResolvedAttackProfile.NoCritElementFactor"/>.</summary>
    public double ElementNoCrit => Attack.NoCritElementFactor;
    /// <summary>The attack's fixed damage per 100 MV, the same for every build (shell fire); not part of any unit's procs.</summary>
    public double Fixed => Attack.FixedDamage * Attack.PerHundredMv;
}

/// <summary>
/// The damage formula as seen by an exact solver:
/// raw = TR * (prod rawMult * rawFactor * critFactor(clamp(affinity), critMult) + noCritRawFactor) with TR = B * base pct * prod rawPct
/// + base flat + sum rawFlat, element = min(E * base pct * prod elePct + base flat + sum eleFlat, cap) * (critElementFactor * <see cref="ScoreSide.ElementScale"/>
/// + <see cref="ScoreSide.ElementNoCrit"/>), plus procs and the attack's fixed damage;
/// per health side the segments of a sequence add up weighted by their share of the motion value, and the best health side wins.
/// The per-feature contributions are not re-implemented: they are probed from <see cref="DamageCalculator"/> with two
/// synthetic weapons (so multipliers and flat bonuses can be told apart), and features whose contributions do not compose
/// independently (e.g. Burst and Ebony Odogaron's Power, Gore Magala's Tyranny and Antivirus, the Festival prayers) are
/// merged into one unit and tabulated jointly. <see cref="Verify"/> checks the composition against the calculator.
/// </summary>
public sealed class ScoreDecomposition
{
    private const double B1 = 100, B2 = 300;   // probe true raw
    private const int E1 = 10_000, E2 = 30_000; // probe display element (1000 / 3000 true, far below the element cap)
    private const int ProbeAffinity = -100;     // keeps up to +200 affinity unclamped
    private const int MaxUnitStates = 20_000;

    private readonly GogmaWeaponStats _weapon;
    private readonly Conditions _cond;

    public IReadOnlyList<Feature> Features { get; }
    public IReadOnlyList<ScoreUnit> Units { get; }
    public IReadOnlyList<ScoreSide> Sides { get; }
    public bool HasElement => _weapon.ElementTrue > 0;
    /// <summary>The weapon the score is for, without its set bonus and group skill (they count as pieces instead).</summary>
    public GogmaWeaponStats Weapon => _weapon;
    public int HealthSides => Sides.Select(s => s.Health).Distinct().Count();
    /// <summary>Calls of <see cref="DamageCalculator"/> it took to build the model.</summary>
    public int Probes { get; private init; }

    private ScoreDecomposition(GogmaWeaponStats weapon, Conditions cond, List<Feature> features, List<ScoreUnit> units, List<ScoreSide> sides)
    {
        _weapon = weapon; _cond = cond; Features = features; Units = units; Sides = sides;
    }

    /// <summary>
    /// The features <see cref="Build"/> decomposes the score into. The weapon's own set bonus and group skill only count as
    /// pieces (see <see cref="CpSatSearch"/>), so weapons that differ in nothing else get the same decomposition from the
    /// same features (see <see cref="ScoreDecompositionCache"/>).
    /// </summary>
    public static List<Feature> FeaturesOf(Relevance rel)
    {
        var features = new List<Feature>();
        for (var s = 0; s < rel.Skills.Count; s++) features.Add(new Feature(FeatureKind.Skill, s, rel.Skills[s], rel.Caps[s]));
        for (var i = 0; i < rel.SetBonuses.Count; i++) features.Add(new Feature(FeatureKind.SetBonus, i, rel.SetBonuses[i], 2));
        for (var i = 0; i < rel.GroupSkills.Count; i++) features.Add(new Feature(FeatureKind.GroupSkill, i, rel.GroupSkills[i], 1));
        return features;
    }

    public static ScoreDecomposition Build(GogmaWeaponStats weapon, Relevance rel, Conditions cond) =>
        BuildAsync(weapon, rel, cond, Pacer.Never).GetAwaiter().GetResult();

    /// <summary>Builds the model, pausing for the host between probes of the calculator (see <see cref="Pacer"/>).</summary>
    public static async Task<ScoreDecomposition> BuildAsync(GogmaWeaponStats weapon, Relevance rel, Conditions cond, Pacer pacer)
    {
        var features = FeaturesOf(rel);

        // with full health and red/low health both on, the calculator scores the better side; the model takes the max
        var sideConditions = cond.FullHealth && (cond.RedHealth || cond.LowHealth)
            ? new[] { cond with { RedHealth = false, LowHealth = false }, cond with { FullHealth = false } }
            : new[] { cond };
        var bare = weapon with { SetBonus = null, GroupSkill = null };
        var none = Skills(features, new int[features.Count]);
        // the shockwave scales with true raw x weapon sharpness x crit factor = EFR x sharpness / the attack's raw factor
        var sharpRaw = weapon.TopSharpness is { } top ? DamageConstants.SharpnessRaw(top) : 1.0;
        var sides = new List<ScoreSide>();
        for (var health = 0; health < sideConditions.Length; health++)
        {
            var segments = sideConditions[health].Segments(bare);
            var results = segments.Select(c => DamageCalculator.Calculate(bare, none, c, trace: false)).ToList();
            var totalMv = results.Sum(r => r.Attack.TotalMv);
            for (var i = 0; i < segments.Count; i++)
            {
                var r = results[i];
                var p = r.Attack;
                // timed skills split the segment by time, with fixed weights (the tier-gated slices, see Conditions.UptimeParts)
                foreach (var (part, share) in segments[i].UptimeParts())
                {
                    var b = BaseOf(bare, none, part);
                    sides.Add(new ScoreSide(part, health, p.TotalMv / totalMv * share, p, p.RawFactor, r.SharpnessElementModifier,
                        r.CriticalMultiplier, r.CriticalElementMultiplier, r.ElementCap,
                        DamageConstants.DarkArtsShockwaveMv / 100.0 * sharpRaw / p.RawFactor * p.Shockwaves * p.PerHundredMv,
                        Shockwave(0, r.SharpnessElementModifier, p), b.RawFlat, b.RawPct, b.Affinity, b.ElePct, b.EleFlat, i));
                }
            }
        }

        var prober = new Prober(weapon, features, sides, pacer);

        // union-find over features; merge any two units whose contributions do not compose. A merge starts the checks over:
        // the units and the pairs that were checked before keep their answers
        var parent = Enumerable.Range(0, features.Count).ToArray();
        int Find(int f) => parent[f] == f ? f : parent[f] = Find(parent[f]);
        List<ScoreUnit> units;
        while (true)
        {
            units = [];
            foreach (var g in Enumerable.Range(0, features.Count).GroupBy(Find)) units.Add(await prober.TabulateAsync(g.ToArray()));
            var merged = false;
            for (var a = 0; a < units.Count && !merged; a++)
                for (var b = a + 1; b < units.Count && !merged; b++)
                    if (!await prober.ComposesIndependentlyAsync(units[a], units[b]))
                    {
                        parent[Find(units[b].Features[0])] = Find(units[a].Features[0]);
                        merged = true;
                    }
            if (!merged) break;
        }

        // units that never change the score (target-only skills such as Focus, sets whose condition is off) are left out
        units = units.Where(u => u.Tables.Select((t, side) => t.Any(c => !Same(c, Base(sides[side])))).Any(x => x)).ToList();
        return new ScoreDecomposition(bare, cond, features, units, sides) { Probes = prober.Calculations };
    }

    /// <summary>
    /// This model with the parts of the hunt of each attack segment (<see cref="Conditions.UptimeParts"/>) merged into one part
    /// at their share-weighted average contributions: one part per segment instead of up to dozens. An approximation, as the
    /// affinity cap and timed skills that change raw and affinity together do not average exactly. This model itself when no
    /// segment is split.
    /// </summary>
    public ScoreDecomposition Averaged()
    {
        var groups = Enumerable.Range(0, Sides.Count).GroupBy(s => (Sides[s].Health, Sides[s].Segment)).Select(g => g.ToList()).ToList();
        if (groups.Count == Sides.Count) return this;
        var weights = groups.Select(g => g.Sum(s => Sides[s].Weight)).ToList();
        double Avg(int k, Func<int, double> value) => groups[k].Sum(s => value(s) * Sides[s].Weight) / weights[k];
        var sides = groups.Select((g, k) => Sides[g[0]] with
        {
            Weight = weights[k],
            BaseCritMult = Avg(k, s => Sides[s].BaseCritMult), BaseCritEleMult = Avg(k, s => Sides[s].BaseCritEleMult),
            ShockwaveRawShare = Avg(k, s => Sides[s].ShockwaveRawShare), ShockwaveConstant = Avg(k, s => Sides[s].ShockwaveConstant),
            BaseRawFlat = Avg(k, s => Sides[s].BaseRawFlat), BaseRawPct = Avg(k, s => Sides[s].BaseRawPct),
            BaseAffinity = (int)Math.Round(Avg(k, s => Sides[s].BaseAffinity)), BaseElePct = Avg(k, s => Sides[s].BaseElePct),
            BaseEleFlat = Avg(k, s => Sides[s].BaseEleFlat),
        }).ToList();
        var units = Units.Select(u => new ScoreUnit
        {
            Features = u.Features, Strides = u.Strides,
            Tables = groups.Select((_, k) => Enumerable.Range(0, u.States).Select(state =>
            {
                double A(Func<Channels, double> ch) => Avg(k, s => ch(u.Tables[s][state]));
                var first = u.Tables[groups[k][0]][state];
                return new Channels(A(c => c.RawPct), A(c => c.RawFlat), A(c => c.Affinity), A(c => c.ElePct), A(c => c.EleFlat), A(c => c.CritMult),
                    A(c => c.CritEleMult), A(c => c.Procs), first.Shockwave, A(c => c.RawMult));
            }).ToArray()).ToArray(),
        }).ToList();
        return new ScoreDecomposition(_weapon, _cond, Features.ToList(), units, sides);
    }

    /// <summary>What the conditions give every build (no skills): read off the calculator with the two probe weapons.</summary>
    private static (double RawPct, double RawFlat, int Affinity, double ElePct, double EleFlat) BaseOf(GogmaWeaponStats bare, ActiveSkills none, Conditions cond)
    {
        var hasElement = bare.ElementTrue > 0;
        var r1 = DamageCalculator.Calculate(bare with { TrueRaw = (int)B1, Affinity = ProbeAffinity, ElementDisplay = hasElement ? E1 : 0 }, none, cond, trace: false);
        var r2 = DamageCalculator.Calculate(bare with { TrueRaw = (int)B2, Affinity = ProbeAffinity, ElementDisplay = hasElement ? E2 : 0 }, none, cond, trace: false);
        var rawPct = (r2.TrueRaw - r1.TrueRaw) / (B2 - B1);
        var elePct = hasElement ? (r2.ElementTrue - r1.ElementTrue) / ((E2 - E1) / 10.0) : 1.0;
        return (rawPct, r1.TrueRaw - B1 * rawPct, r1.Affinity - ProbeAffinity, elePct, hasElement ? r1.ElementTrue - E1 / 10.0 * elePct : 0);
    }

    /// <summary>Dark Arts shockwave per 100 MV, as <see cref="DamageCalculator"/> counts it.</summary>
    /// <param name="effectiveRawPerMv">True raw x weapon sharpness x crit factor.</param>
    private static double Shockwave(double effectiveRawPerMv, double sharpEle, ResolvedAttackProfile profile) =>
        DamageCalculator.Shockwave(effectiveRawPerMv, sharpEle, profile) * profile.Shockwaves * profile.PerHundredMv;

    /// <summary>The score for the given feature values, composed from the unit tables (same formula the CP model encodes).</summary>
    public double Evaluate(int[] values)
    {
        var byHealth = new Dictionary<int, double>();
        for (var side = 0; side < Sides.Count; side++)
        {
            var ch = Base(Sides[side]);
            foreach (var u in Units)
            {
                var state = 0;
                for (var i = 0; i < u.Features.Length; i++) state += values[u.Features[i]] * u.Strides[i];
                ch = Compose(ch, u.Tables[side][state], Sides[side]) ?? throw new InvalidOperationException("Units do not compose.");
            }
            byHealth[Sides[side].Health] = byHealth.GetValueOrDefault(Sides[side].Health) + Sides[side].Weight * SideTotal(ch, Sides[side]);
        }
        return byHealth.Values.Max();
    }

    private double SideTotal(Channels ch, ScoreSide side)
    {
        var trueRaw = _weapon.TrueRaw * side.BaseRawPct * ch.RawPct + ch.RawFlat + side.BaseRawFlat;
        var aff = Math.Clamp(_weapon.Affinity + side.BaseAffinity + ch.Affinity, -DamageConstants.AffinityCap, DamageConstants.AffinityCap);
        var critFactor = aff >= 0 ? 1.0 + aff / 100.0 * (ch.CritMult - 1.0) : 1.0 + -aff / 100.0 * (DamageConstants.NegativeCriticalMultiplier - 1.0);
        var efr = trueRaw * side.RawFactor * ch.RawMult * critFactor;
        double efe = 0;
        if (HasElement)
        {
            var ele = Math.Min(_weapon.ElementTrue * side.BaseElePct * ch.ElePct + side.BaseEleFlat + ch.EleFlat, side.ElementCap);
            var critEle = aff > 0 ? 1.0 + aff / 100.0 * (ch.CritEleMult - 1.0) : 1.0;
            efe = ele * (critEle * side.ElementScale + side.ElementNoCrit);
        }
        // the shockwave follows the raw of the hits that crit
        var procs = ch.Procs + side.Fixed + (ch.Shockwave ? side.ShockwaveRawShare * efr + side.ShockwaveConstant : 0);
        return efr + trueRaw * side.RawNoCrit + efe + procs;
    }

    /// <summary>Compares the composed score with <see cref="DamageCalculator"/> on random feature values; returns the largest difference.</summary>
    public double Verify(int samples, int seed = 1) => VerifyAsync(samples, Pacer.Never, seed).GetAwaiter().GetResult();

    /// <inheritdoc cref="Verify"/>
    public async Task<double> VerifyAsync(int samples, Pacer pacer, int seed = 1)
    {
        var rng = new Random(seed);
        var worst = 0.0;
        for (var n = 0; n < samples; n++)
        {
            var values = Features.Select(f => rng.Next(f.Max + 1)).ToArray();
            var expected = DamageCalculator.Calculate(_weapon, Skills(Features, values), _cond, trace: false).Total;
            worst = Math.Max(worst, Math.Abs(expected - Evaluate(values)));
            await pacer.PauseAsync();
        }
        return worst;
    }

    internal static ActiveSkills Skills(IReadOnlyList<Feature> features, int[] values)
    {
        var levels = new Dictionary<string, int>();
        var sets = new Dictionary<string, int>();
        var groups = new Dictionary<string, int>();
        for (var f = 0; f < features.Count; f++)
        {
            if (values[f] <= 0) continue;
            switch (features[f].Kind)
            {
                case FeatureKind.Skill: levels[features[f].Name] = values[f]; break;
                case FeatureKind.SetBonus: sets[features[f].Name] = values[f] == 2 ? SkillAggregator.SetTierTwoPieces : SkillAggregator.SetTierOnePieces; break;
                case FeatureKind.GroupSkill: groups[features[f].Name] = SkillAggregator.GroupSkillPieces; break;
            }
        }
        return new ActiveSkills(levels, levels, sets, groups);
    }

    internal static Channels Base(ScoreSide side) => new(1, 0, 0, 1, 0, side.BaseCritMult, side.BaseCritEleMult, 0, false, 1);

    /// <summary>Combines two contributions the way the calculator does; null when both change the same critical multiplier.</summary>
    internal static Channels? Compose(Channels a, Channels b, ScoreSide side)
    {
        if (!Pick(a.CritMult, b.CritMult, side.BaseCritMult, out var crit) || !Pick(a.CritEleMult, b.CritEleMult, side.BaseCritEleMult, out var critEle)) return null;
        return new Channels(a.RawPct * b.RawPct, a.RawFlat + b.RawFlat, a.Affinity + b.Affinity, a.ElePct * b.ElePct, a.EleFlat + b.EleFlat,
            crit, critEle, a.Procs + b.Procs, a.Shockwave || b.Shockwave, a.RawMult * b.RawMult);

        static bool Pick(double x, double y, double baseValue, out double value)
        {
            var xs = Near(x, baseValue); var ys = Near(y, baseValue);
            value = xs ? y : x;
            return xs || ys;
        }
    }

    internal static bool Same(Channels a, Channels b) =>
        Near(a.RawPct, b.RawPct) && Near(a.RawFlat, b.RawFlat) && Near(a.Affinity, b.Affinity) && Near(a.ElePct, b.ElePct) && Near(a.EleFlat, b.EleFlat)
        && Near(a.CritMult, b.CritMult) && Near(a.CritEleMult, b.CritEleMult) && Near(a.Procs, b.Procs) && a.Shockwave == b.Shockwave && Near(a.RawMult, b.RawMult);

    private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));

    /// <summary>Reads a feature combination's contributions off the calculator.</summary>
    private sealed class Prober(GogmaWeaponStats weapon, List<Feature> features, List<ScoreSide> sides, Pacer pacer)
    {
        private readonly Dictionary<(int, (bool, bool, bool), string), Channels> _cache = new();
        private readonly Dictionary<string, ActiveSkills> _skills = new();
        private readonly int[] _groups = Groups(sides);
        private readonly Dictionary<string, ScoreUnit> _units = new();
        private readonly Dictionary<(ScoreUnit, ScoreUnit), bool> _composes = new();
        private readonly bool _hasElement = weapon.ElementTrue > 0;
        private readonly GogmaWeaponStats _w1 = weapon with { TrueRaw = (int)B1, Affinity = ProbeAffinity, ElementDisplay = weapon.ElementTrue > 0 ? E1 : 0, SetBonus = null, GroupSkill = null };
        private readonly GogmaWeaponStats _w2 = weapon with { TrueRaw = (int)B2, Affinity = ProbeAffinity, ElementDisplay = weapon.ElementTrue > 0 ? E2 : 0, SetBonus = null, GroupSkill = null };

        /// <summary>Calls of the calculator so far.</summary>
        public int Calculations { get; private set; }

        /// <summary>The unit of these features; the same instance every time they are asked for again.</summary>
        public async ValueTask<ScoreUnit> TabulateAsync(int[] unitFeatures)
        {
            var key = string.Join(",", unitFeatures);
            if (_units.TryGetValue(key, out var known)) return known;
            var strides = new int[unitFeatures.Length];
            var states = 1;
            for (var i = 0; i < unitFeatures.Length; i++) { strides[i] = states; states *= features[unitFeatures[i]].Max + 1; }
            if (states > MaxUnitStates) throw new InvalidOperationException($"Interacting skills form a unit with {states} states: {string.Join(", ", unitFeatures.Select(f => features[f].Name))}");
            var tables = new Channels[sides.Count][];
            for (var side = 0; side < sides.Count; side++) tables[side] = new Channels[states];
            for (var state = 0; state < states; state++)
            {
                var loadout = Loadout(Values(unitFeatures, strides, state));
                for (var side = 0; side < sides.Count; side++) tables[side][state] = Probe(loadout, side);
                await pacer.PauseAsync();
            }
            return _units[key] = new ScoreUnit { Features = unitFeatures, Strides = strides, Tables = tables };
        }

        private int[] Values(int[] unitFeatures, int[] strides, int state)
        {
            var values = new int[features.Count];
            for (var i = unitFeatures.Length - 1; i >= 0; i--) { values[unitFeatures[i]] = state / strides[i]; state %= strides[i]; }
            return values;
        }

        /// <summary>Whether two units' contributions add up as the calculator has them; asked again, the earlier answer.</summary>
        public async ValueTask<bool> ComposesIndependentlyAsync(ScoreUnit a, ScoreUnit b)
        {
            if (!_composes.TryGetValue((a, b), out var composes)) _composes[(a, b)] = composes = await Check();
            return composes;

            async ValueTask<bool> Check()
            {
                for (var sa = 1; sa < a.States; sa++)
                    for (var sb = 1; sb < b.States; sb++)
                    {
                        var values = Values(a.Features, a.Strides, sa);
                        var vb = Values(b.Features, b.Strides, sb);
                        for (var f = 0; f < values.Length; f++) values[f] += vb[f];
                        var loadout = Loadout(values);
                        for (var side = 0; side < sides.Count; side++)
                        {
                            var composed = Compose(a.Tables[side][sa], b.Tables[side][sb], sides[side]);
                            if (composed is null || !Same(composed.Value, Probe(loadout, side))) return false;
                        }
                        await pacer.PauseAsync();
                    }
                return true;
            }
        }

        /// <summary>A feature combination as the calculator takes it, with its key in the caches.</summary>
        private (string Key, ActiveSkills Skills) Loadout(int[] values)
        {
            var key = string.Create(values.Length, values, static (span, v) =>
            {
                for (var i = 0; i < v.Length; i++) span[i] = (char)('0' + v[i]);
            });
            if (!_skills.TryGetValue(key, out var skills)) _skills[key] = skills = Skills(features, values);
            return (key, skills);
        }

        private Channels Probe((string Key, ActiveSkills Skills) loadout, int side)
        {
            var s = sides[side];
            var cond = s.Conditions;
            // the uptime parts of a segment give the same contributions for loadouts that turn on the same timed skills in them
            var key = (_groups[side], cond.TimedSkillsOn(loadout.Skills), loadout.Key);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var skills = loadout.Skills;
            var r1 = DamageCalculator.Calculate(_w1, skills, cond, trace: false);
            var r2 = DamageCalculator.Calculate(_w2, skills, cond, trace: false);
            Calculations += 2;
            var rawSlope = (r2.TrueRaw - r1.TrueRaw) / (B2 - B1);
            var eleSlope = _hasElement ? (r2.ElementTrue - r1.ElementTrue) / ((E2 - E1) / 10.0) : 1.0;
            var profile = s.Attack;
            var shockwave = cond.DarkArtsShockwave && weapon.Type == WeaponType.GreatSword && profile.Shockwaves > 0
                            && cond.SetTier(skills, SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None;
            var shock = shockwave ? Shockwave(r1.TrueRaw * r1.SharpnessRawModifier * r1.CriticalFactor, r1.SharpnessElementModifier, profile) : 0;
            // what the conditions give every build and the attack's fixed damage are the side's base, not part of any unit
            var ch = new Channels(rawSlope / s.BaseRawPct, r1.TrueRaw - B1 * rawSlope - s.BaseRawFlat, r1.Affinity - ProbeAffinity - s.BaseAffinity, eleSlope / s.BaseElePct,
                _hasElement ? r1.ElementTrue - E1 / 10.0 * eleSlope - s.BaseEleFlat : 0, r1.CriticalMultiplier, r1.CriticalElementMultiplier, r1.ProcDamage - shock - s.Fixed, shockwave,
                r1.ShotMultiplier);
            _cache[key] = ch;
            return ch;
        }

        /// <summary>
        /// Sides that differ only in which timed skills are on (the uptime parts of a segment, <see cref="Conditions.UptimeParts"/>)
        /// and in weight share a group: the calculator scores a loadout alike on them when <see cref="Conditions.TimedSkillsOn"/>
        /// agrees, and their bases are the same, so the loadout's contributions are too.
        /// </summary>
        private static int[] Groups(List<ScoreSide> sides)
        {
            var bare = sides.Select(s => s with { Conditions = s.Conditions.WithTimedSkills((false, false, false)), Weight = 0 }).ToList();
            return bare.Select(b => bare.IndexOf(b)).ToArray();
        }
    }
}

/// <summary>
/// Score models of one weapon and conditions by their features, built (and checked) once: the skill pair classes of an
/// optimize-mode run differ only in the weapon's set bonus and group skill and usually share one model. Thread-safe; a
/// search that asks while another one builds the model waits for that build.
/// </summary>
public sealed class ScoreDecompositionCache
{
    private readonly ConcurrentDictionary<(GogmaWeaponStats Weapon, Conditions Conditions, string Features), Lazy<Task<ScoreDecomposition>>> _models = new();

    /// <param name="build">Builds the model when there is none for these features yet (e.g. <see cref="ScoreDecomposition.BuildAsync"/> and a check).</param>
    /// <returns>The model, and whether this call built it.</returns>
    public async Task<(ScoreDecomposition Score, bool Built)> GetOrBuildAsync(GogmaWeaponStats weapon, Relevance rel, Conditions cond, Func<Task<ScoreDecomposition>> build)
    {
        var key = (weapon with { SetBonus = null, GroupSkill = null, Spec = null }, cond,
                   string.Join("|", ScoreDecomposition.FeaturesOf(rel).Select(f => $"{f.Kind}:{f.Name}:{f.Max}")));
        var created = new Lazy<Task<ScoreDecomposition>>(build);
        var model = _models.GetOrAdd(key, created);
        return (await model.Value, ReferenceEquals(model, created));
    }
}
