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
public readonly record struct Channels(double RawPct, double RawFlat, double Affinity, double ElePct, double EleFlat, double CritMult, double CritEleMult, double Procs, bool Shockwave);

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
/// <param name="Weight">The segment's share of the sequence's motion value (1 without segments).</param>
/// <param name="RawFactor">EFR per true raw and crit factor: the attack's MV-weighted sharpness (and phial), <see cref="ResolvedAttackProfile.RawFactor"/>.</param>
/// <param name="SharpEle">The weapon's element sharpness modifier (the Dark Arts shockwave's element).</param>
/// <param name="ShockwaveRawShare">Shockwave damage per 100 MV = this x EFR + <paramref name="ShockwaveConstant"/>.</param>
/// <param name="BaseRawFlat">Flat raw the conditions give every build (Powercharm, meal); the units' flat raw comes on top.</param>
public sealed record ScoreSide(Conditions Conditions, int Health, double Weight, ResolvedAttackProfile Attack, double RawFactor, double SharpEle,
    double BaseCritMult, double BaseCritEleMult, double ElementCap, double ShockwaveRawShare, double ShockwaveConstant, double BaseRawFlat = 0)
{
    /// <summary>Element per true element point per 100 MV of the segment's attack, sharpness included: <see cref="ResolvedAttackProfile.ElementFactor"/>.</summary>
    public double ElementScale => Attack.ElementFactor;
}

/// <summary>
/// The damage formula as seen by an exact solver:
/// raw = (B * prod rawPct + base flat + sum rawFlat) * rawFactor * critFactor(clamp(affinity), critMult),
/// element = min(E * prod elePct + sum eleFlat, cap) * critElementFactor * <see cref="ScoreSide.ElementScale"/>, plus procs;
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

    public static ScoreDecomposition Build(GogmaWeaponStats weapon, Relevance rel, Conditions cond)
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
                sides.Add(new ScoreSide(segments[i], health, p.TotalMv / totalMv, p, p.RawFactor, r.SharpnessElementModifier,
                    r.CriticalMultiplier, r.CriticalElementMultiplier, r.ElementCap,
                    DamageConstants.DarkArtsShockwaveMv / 100.0 * sharpRaw / p.RawFactor * p.Shockwaves * p.PerHundredMv,
                    Shockwave(0, r.SharpnessElementModifier, p), r.TrueRaw - bare.TrueRaw));
            }
        }

        var prober = new Prober(weapon, features, sides);

        // union-find over features; merge any two units whose contributions do not compose
        var parent = Enumerable.Range(0, features.Count).ToArray();
        int Find(int f) => parent[f] == f ? f : parent[f] = Find(parent[f]);
        List<ScoreUnit> units;
        while (true)
        {
            units = Enumerable.Range(0, features.Count).GroupBy(Find).Select(g => prober.Tabulate(g.ToArray())).ToList();
            var merged = false;
            for (var a = 0; a < units.Count && !merged; a++)
                for (var b = a + 1; b < units.Count && !merged; b++)
                    if (!prober.ComposesIndependently(units[a], units[b]))
                    {
                        parent[Find(units[b].Features[0])] = Find(units[a].Features[0]);
                        merged = true;
                    }
            if (!merged) break;
        }

        // units that never change the score (target-only skills such as Focus, sets whose condition is off) are left out
        units = units.Where(u => u.Tables.Select((t, side) => t.Any(c => !Same(c, Base(sides[side])))).Any(x => x)).ToList();
        return new ScoreDecomposition(bare, cond, features, units, sides);
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
        var trueRaw = _weapon.TrueRaw * ch.RawPct + ch.RawFlat + side.BaseRawFlat;
        var aff = Math.Clamp(_weapon.Affinity + ch.Affinity, -DamageConstants.AffinityCap, DamageConstants.AffinityCap);
        var critFactor = aff >= 0 ? 1.0 + aff / 100.0 * (ch.CritMult - 1.0) : 1.0 + -aff / 100.0 * (DamageConstants.NegativeCriticalMultiplier - 1.0);
        var efr = trueRaw * side.RawFactor * critFactor;
        double efe = 0;
        if (HasElement)
        {
            var ele = Math.Min(_weapon.ElementTrue * ch.ElePct + ch.EleFlat, side.ElementCap);
            var critEle = aff > 0 ? 1.0 + aff / 100.0 * (ch.CritEleMult - 1.0) : 1.0;
            efe = ele * critEle * side.ElementScale;
        }
        var procs = ch.Procs + (ch.Shockwave ? side.ShockwaveRawShare * efr + side.ShockwaveConstant : 0);
        return efr + efe + procs;
    }

    /// <summary>Compares the composed score with <see cref="DamageCalculator"/> on random feature values; returns the largest difference.</summary>
    public double Verify(int samples, int seed = 1)
    {
        var rng = new Random(seed);
        var worst = 0.0;
        for (var n = 0; n < samples; n++)
        {
            var values = Features.Select(f => rng.Next(f.Max + 1)).ToArray();
            var expected = DamageCalculator.Calculate(_weapon, Skills(Features, values), _cond, trace: false).Total;
            worst = Math.Max(worst, Math.Abs(expected - Evaluate(values)));
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

    internal static Channels Base(ScoreSide side) => new(1, 0, 0, 1, 0, side.BaseCritMult, side.BaseCritEleMult, 0, false);

    /// <summary>Combines two contributions the way the calculator does; null when both change the same critical multiplier.</summary>
    internal static Channels? Compose(Channels a, Channels b, ScoreSide side)
    {
        if (!Pick(a.CritMult, b.CritMult, side.BaseCritMult, out var crit) || !Pick(a.CritEleMult, b.CritEleMult, side.BaseCritEleMult, out var critEle)) return null;
        return new Channels(a.RawPct * b.RawPct, a.RawFlat + b.RawFlat, a.Affinity + b.Affinity, a.ElePct * b.ElePct, a.EleFlat + b.EleFlat,
            crit, critEle, a.Procs + b.Procs, a.Shockwave || b.Shockwave);

        static bool Pick(double x, double y, double baseValue, out double value)
        {
            var xs = Near(x, baseValue); var ys = Near(y, baseValue);
            value = xs ? y : x;
            return xs || ys;
        }
    }

    internal static bool Same(Channels a, Channels b) =>
        Near(a.RawPct, b.RawPct) && Near(a.RawFlat, b.RawFlat) && Near(a.Affinity, b.Affinity) && Near(a.ElePct, b.ElePct) && Near(a.EleFlat, b.EleFlat)
        && Near(a.CritMult, b.CritMult) && Near(a.CritEleMult, b.CritEleMult) && Near(a.Procs, b.Procs) && a.Shockwave == b.Shockwave;

    private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));

    /// <summary>Reads a feature combination's contributions off the calculator.</summary>
    private sealed class Prober(GogmaWeaponStats weapon, List<Feature> features, List<ScoreSide> sides)
    {
        private readonly Dictionary<(string, int), Channels> _cache = new();
        private readonly bool _hasElement = weapon.ElementTrue > 0;
        private readonly GogmaWeaponStats _w1 = weapon with { TrueRaw = (int)B1, Affinity = ProbeAffinity, ElementDisplay = weapon.ElementTrue > 0 ? E1 : 0, SetBonus = null, GroupSkill = null };
        private readonly GogmaWeaponStats _w2 = weapon with { TrueRaw = (int)B2, Affinity = ProbeAffinity, ElementDisplay = weapon.ElementTrue > 0 ? E2 : 0, SetBonus = null, GroupSkill = null };

        public ScoreUnit Tabulate(int[] unitFeatures)
        {
            var strides = new int[unitFeatures.Length];
            var states = 1;
            for (var i = 0; i < unitFeatures.Length; i++) { strides[i] = states; states *= features[unitFeatures[i]].Max + 1; }
            if (states > MaxUnitStates) throw new InvalidOperationException($"Interacting skills form a unit with {states} states: {string.Join(", ", unitFeatures.Select(f => features[f].Name))}");
            var tables = sides.Select((_, side) => Enumerable.Range(0, states).Select(state => Probe(Values(unitFeatures, strides, state), side)).ToArray()).ToArray();
            return new ScoreUnit { Features = unitFeatures, Strides = strides, Tables = tables };
        }

        private int[] Values(int[] unitFeatures, int[] strides, int state)
        {
            var values = new int[features.Count];
            for (var i = unitFeatures.Length - 1; i >= 0; i--) { values[unitFeatures[i]] = state / strides[i]; state %= strides[i]; }
            return values;
        }

        public bool ComposesIndependently(ScoreUnit a, ScoreUnit b)
        {
            for (var side = 0; side < sides.Count; side++)
                for (var sa = 1; sa < a.States; sa++)
                    for (var sb = 1; sb < b.States; sb++)
                    {
                        var values = Values(a.Features, a.Strides, sa);
                        var vb = Values(b.Features, b.Strides, sb);
                        for (var f = 0; f < values.Length; f++) values[f] += vb[f];
                        var composed = Compose(a.Tables[side][sa], b.Tables[side][sb], sides[side]);
                        if (composed is null || !Same(composed.Value, Probe(values, side))) return false;
                    }
            return true;
        }

        private Channels Probe(int[] values, int side)
        {
            var key = (string.Join(",", values), side);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var skills = Skills(features, values);
            var cond = sides[side].Conditions;
            var r1 = DamageCalculator.Calculate(_w1, skills, cond, trace: false);
            var r2 = DamageCalculator.Calculate(_w2, skills, cond, trace: false);
            var rawPct = (r2.TrueRaw - r1.TrueRaw) / (B2 - B1);
            var elePct = _hasElement ? (r2.ElementTrue - r1.ElementTrue) / ((E2 - E1) / 10.0) : 1.0;
            var profile = sides[side].Attack;
            var shockwave = cond.DarkArtsShockwave && weapon.Type == WeaponType.GreatSword && profile.Shockwaves > 0
                            && skills.SetTier(SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None;
            var shock = shockwave ? Shockwave(r1.TrueRaw * r1.SharpnessRawModifier * r1.CriticalFactor, r1.SharpnessElementModifier, profile) : 0;
            // the conditions' own flat raw is the side's base, not part of any unit
            var ch = new Channels(rawPct, r1.TrueRaw - B1 * rawPct - sides[side].BaseRawFlat, r1.Affinity - ProbeAffinity, elePct,
                _hasElement ? r1.ElementTrue - E1 / 10.0 * elePct : 0, r1.CriticalMultiplier, r1.CriticalElementMultiplier, r1.ProcDamage - shock, shockwave);
            _cache[key] = ch;
            return ch;
        }
    }
}

/// <summary>
/// Score models of one weapon and conditions by their features, built (and checked) once: the skill pair classes of an
/// optimize-mode run differ only in the weapon's set bonus and group skill and usually share one model. Thread-safe.
/// </summary>
public sealed class ScoreDecompositionCache
{
    private readonly ConcurrentDictionary<(GogmaWeaponStats Weapon, Conditions Conditions, string Features), Lazy<ScoreDecomposition>> _models = new();

    /// <param name="build">Builds the model when there is none for these features yet (e.g. <see cref="ScoreDecomposition.Build"/> and a check).</param>
    /// <param name="built">Whether this call built it.</param>
    public ScoreDecomposition GetOrBuild(GogmaWeaponStats weapon, Relevance rel, Conditions cond, Func<ScoreDecomposition> build, out bool built)
    {
        var key = (weapon with { SetBonus = null, GroupSkill = null }, cond,
                   string.Join("|", ScoreDecomposition.FeaturesOf(rel).Select(f => $"{f.Kind}:{f.Name}:{f.Max}")));
        var created = new Lazy<ScoreDecomposition>(build);
        var model = _models.GetOrAdd(key, created);
        var value = model.Value;
        built = ReferenceEquals(model, created);
        return value;
    }
}
