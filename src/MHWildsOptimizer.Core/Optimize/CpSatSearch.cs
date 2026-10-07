using System.Diagnostics;
using Google.OrTools.Sat;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// Exact search with OR-Tools CP-SAT: one boolean per armor candidate and talisman, an integer count per decoration, slot
/// capacity as nested "level 3 / level 2+ / any" inequalities (exact for decorations that fit any slot of their level or
/// higher), targets as linear constraints, set-bonus tiers and group skills reified on piece counts, and the damage formula
/// of <see cref="ScoreDecomposition"/> as table lookups, products and divisions in fixed point. The solver proves the
/// optimum (up to the fixed-point rounding, ~1e-4); the next builds come from re-solving with the previous armor + talisman
/// combinations excluded. Every build is re-scored with <see cref="DamageCalculator"/>. Solves run on an
/// <see cref="ICpSatBackend"/>: the native library, or or-tools-wasm in the browser.
/// </summary>
internal sealed class CpSatSearch
{
    private const long S = 10_000;          // multipliers in 1e-4
    private const long Micro = 1_000_000;   // score terms in 1e-6
    private const long DecoPenalty = 1;     // per decoration, in 1/ObjectiveScale of a micro point: keeps useless jewels out
    private const long ObjectiveScale = 32;

    /// <summary>Slack under a <see cref="Cutoff"/> for the fixed-point rounding of the score model, in micro points.</summary>
    public const long CutoffSlack = 50_000;

    private readonly GameData _data;
    private readonly Relevance _rel;
    private readonly GogmaWeaponStats _weapon;
    private readonly Conditions _cond;
    private readonly DecorationFiller _filler;
    private readonly Dictionary<ArmorPieceKind, List<ArmorCandidate>> _armor;
    private readonly ArmorPieceKind[] _kinds;
    private readonly List<TalismanCandidate> _talismans;
    private readonly int _topN;
    private readonly CpSatParameters _parameters;
    private readonly IProgress<string>? _progress;
    private readonly CancellationToken _ct;

    public int Solves { get; private set; }
    public bool ProvedOptimal { get; private set; } = true;
    public string Summary { get; private set; } = "";

    /// <summary>Time spent building and checking the model, and waiting for the backend's solves.</summary>
    public TimeSpan ModelTime { get; private set; }
    public TimeSpan SolveTime { get; private set; }

    public ICpSatBackend Backend { get; init; } = NativeCpSatBackend.Instance;

    /// <summary>The backend lane this search solves on (see <see cref="ICpSatBackend.SolveAsync"/>).</summary>
    public int Lane { get; set; }

    /// <summary>
    /// Score (micro points) every build must reach, e.g. the weakest skill pair class that is shown so far: a class that
    /// cannot reach it is proved infeasible, usually much faster than it can be solved.
    /// </summary>
    public long? Cutoff { get; init; }

    /// <summary>The cutoff a build scoring <paramref name="score"/> (points) sets, less <see cref="CutoffSlack"/>.</summary>
    public static long CutoffFor(double score) => (long)Math.Floor(score * Micro) - CutoffSlack;

    public CpSatSearch(GameData data, Relevance rel, GogmaWeaponStats weapon, Conditions cond, DecorationFiller filler,
        Dictionary<ArmorPieceKind, List<ArmorCandidate>> armor, ArmorPieceKind[] kinds, List<TalismanCandidate> talismans,
        int topN, int threads, double timeLimitSeconds, IProgress<string>? progress, CancellationToken ct)
    {
        _data = data; _rel = rel; _weapon = weapon; _cond = cond; _filler = filler; _armor = armor; _kinds = kinds;
        _talismans = talismans; _topN = topN; _parameters = CpSatParameters.For(threads, timeLimitSeconds); _progress = progress; _ct = ct;
    }

    /// <summary>Score models shared with other searches of the same weapon and conditions; null builds one for this search.</summary>
    public ScoreDecompositionCache? Scores { get; init; }

    private CpModel? _model;
    private Vars? _vars;
    private Constraint? _cutoff;
    private readonly List<RankedBuild> _builds = [];
    private readonly HashSet<string> _seen = [];
    private readonly List<string> _statuses = [];

    /// <summary>Runs the search on a backend that completes without the caller's thread (the native one).</summary>
    public IReadOnlyList<RankedBuild> Run() => RunAsync().GetAwaiter().GetResult();

    /// <summary>Finds the top N builds (at least <see cref="Cutoff"/>, if set).</summary>
    public Task<IReadOnlyList<RankedBuild>> RunAsync() => SolveUntilAsync(_topN);

    /// <summary>
    /// Continues a finished search up to <paramref name="topN"/> builds without its <see cref="Cutoff"/>: the builds found
    /// so far stay excluded from the next solves, so this returns what a new search for the top N would, without building
    /// the model again or repeating its solves.
    /// </summary>
    public Task<IReadOnlyList<RankedBuild>> ExtendAsync(int topN)
    {
        // an empty constraint is no constraint: the model keeps its constraint indices
        _cutoff?.Proto.ClearConstraint();
        _cutoff = null;
        return SolveUntilAsync(topN);
    }

    private async Task<IReadOnlyList<RankedBuild>> SolveUntilAsync(int topN)
    {
        if (_model is null) Build();
        var (model, vars) = (_model!, _vars!);
        var maxSolves = Solves + topN - _builds.Count + 3;

        while (_builds.Count < topN && Solves < maxSolves)
        {
            var solveSw = Stopwatch.StartNew();
            var response = await Backend.SolveAsync(model, _parameters, Lane, _ct);
            SolveTime += solveSw.Elapsed;
            var status = response.Status;
            Solves++;
            _ct.ThrowIfCancellationRequested();
            if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible)) { _statuses.Add(status.ToString().ToLowerInvariant()); break; }
            if (status != CpSolverStatus.Optimal) ProvedOptimal = false;
            _statuses.Add(status == CpSolverStatus.Optimal ? "optimal" : "time limit");

            var chosen = vars.Pieces.Select(list => list.FindIndex(x => response.BooleanValue(x.Var))).ToArray();
            var talisman = vars.Talismans.FindIndex(t => response.BooleanValue(t));
            var decos = new List<DecoCandidate>();
            for (var i = 0; i < vars.Decos.Count; i++)
                for (var n = response.Value(vars.Decos[i]); n > 0; n--) decos.Add(_filler.All[i]);

            var objective = response.ObjectiveValue / ObjectiveScale / Micro;
            _progress?.Report($"  solve {Solves}: {status.ToString().ToLowerInvariant()} {objective:0.00} (bound {response.BestObjectiveBound / ObjectiveScale / Micro:0.00}) in {solveSw.ElapsedMilliseconds} ms");

            // exclude this armor + talisman combination for the next solve (hints and an objective cap from this solve made it slower)
            var used = new List<ILiteral>();
            for (var d = 0; d < chosen.Length; d++) if (chosen[d] >= 0) used.Add(vars.Pieces[d][chosen[d]].Var);
            if (talisman >= 0) used.Add(vars.Talismans[talisman]);
            model.Add(LinearExpr.Sum(used) <= used.Count - 1);

            var loadout = BuildLoadout(chosen, talisman, decos);
            if (loadout is null) continue;
            var key = string.Join("|", loadout.ArmorPieces.Select(a => a.Piece.Id)) + "|" + loadout.Talisman?.Talisman.Name;
            if (!_seen.Add(key)) continue;
            _builds.Add(new RankedBuild(loadout, DamageCalculator.Calculate(loadout, _data, _cond), null, ""));
        }
        Summary = $"cp-sat {Solves} solve(s): {string.Join(", ", _statuses.GroupBy(s => s).Select(g => $"{g.Count()} {g.Key}"))}";
        return _builds.OrderByDescending(b => b.Score).Take(topN).ToList();
    }

    private void Build()
    {
        var sw = Stopwatch.StartNew();
        var built = true;
        var score = Scores is null ? CheckedScore() : Scores.GetOrBuild(_weapon, _rel, _cond, CheckedScore, out built);
        _progress?.Report(built
            ? $"  score model: {score.Units.Count} units ({string.Join(", ", score.Units.Where(u => u.Features.Length > 1).Select(u => string.Join(" + ", u.Features.Select(f => score.Features[f].Name))))} joint), {score.Sides.Count} health side(s), built in {sw.ElapsedMilliseconds} ms"
            : $"  score model: {score.Units.Count} units, shared with an earlier search");

        _model = new CpModel();
        _vars = BuildModel(_model, score);
        if (Cutoff is { } cutoff) _cutoff = _model.Add(_vars.Score >= cutoff);
        ModelTime = sw.Elapsed;
    }

    private ScoreDecomposition CheckedScore()
    {
        var score = ScoreDecomposition.Build(_weapon, _rel, _cond);
        var mismatch = score.Verify(CpSatParameters.ScoreChecks);
        if (mismatch > 1e-6) throw new InvalidOperationException($"CP-SAT score model differs from the damage calculator by {mismatch:0.######}.");
        return score;
    }

    private sealed record Vars(List<List<(ArmorCandidate Candidate, BoolVar Var)>> Pieces, List<BoolVar> Talismans, List<IntVar> Decos, IntVar Score);

    private Vars BuildModel(CpModel m, ScoreDecomposition score)
    {
        // ---------------- choices ----------------
        var pieces = _kinds.Select((k, d) => _armor[k].Select((c, i) => (c, m.NewBoolVar($"{k}{i}"))).ToList()).ToList();
        foreach (var list in pieces) if (list.Count > 0) m.AddExactlyOne(list.Select(x => (ILiteral)x.Item2));
        var tals = _talismans.Select((_, i) => m.NewBoolVar($"t{i}")).ToList();
        m.AddExactlyOne(tals);

        var maxSlots = _weapon.Slots.Count + 3 + 3 * _kinds.Length;
        var decos = _filler.All.Select((d, i) => m.NewIntVar(0, maxSlots, $"d{i}")).ToList();

        // ---------------- decoration slots: nested capacity per kind ----------------
        for (var level = 1; level <= 3; level++)
        {
            var armorCap = new List<LinearExpr>();
            foreach (var list in pieces) foreach (var (c, x) in list) armorCap.Add(LinearExpr.Term(x, SlotsAtLeast(c.ArmorSlots, level)));
            for (var j = 0; j < tals.Count; j++) armorCap.Add(LinearExpr.Term(tals[j], SlotsAtLeast(_talismans[j].ArmorSlots, level)));
            var armorUse = _filler.All.Select((d, i) => (d, i)).Where(x => x.d.Kind == SkillKind.Armor && x.d.Level >= level).Select(x => (LinearExpr)decos[x.i]);
            m.Add(LinearExpr.Sum(armorUse) <= LinearExpr.Sum(armorCap));

            var weaponCap = new List<LinearExpr> { LinearExpr.Constant(_weapon.Slots.Count(l => l >= level)) };
            for (var j = 0; j < tals.Count; j++) weaponCap.Add(LinearExpr.Term(tals[j], SlotsAtLeast(_talismans[j].WeaponSlots, level)));
            var weaponUse = _filler.All.Select((d, i) => (d, i)).Where(x => x.d.Kind == SkillKind.Weapon && x.d.Level >= level).Select(x => (LinearExpr)decos[x.i]);
            m.Add(LinearExpr.Sum(weaponUse) <= LinearExpr.Sum(weaponCap));
        }

        // ---------------- skill levels and targets ----------------
        var featureVars = new LinearExpr[score.Features.Count];
        for (var s = 0; s < _rel.Skills.Count; s++)
        {
            var terms = new List<LinearExpr>();
            foreach (var list in pieces) foreach (var (c, x) in list) if (c.Skills[s] != 0) terms.Add(LinearExpr.Term(x, c.Skills[s]));
            for (var j = 0; j < tals.Count; j++) if (_talismans[j].Skills[s] != 0) terms.Add(LinearExpr.Term(tals[j], _talismans[j].Skills[s]));
            for (var i = 0; i < decos.Count; i++)
                foreach (var (skill, grant) in _filler.All[i].Grants) if (skill == s) terms.Add(LinearExpr.Term(decos[i], grant));
            var sum = LinearExpr.Sum(terms);
            if (_rel.Targets[s] > 0) m.Add(sum >= _rel.Targets[s]);
            var level = m.NewIntVar(0, _rel.Caps[s], $"lv{s}");
            m.AddMinEquality(level, [sum, LinearExpr.Constant(_rel.Caps[s])]);
            featureVars[Feature(score, FeatureKind.Skill, s)] = level;
        }

        // ---------------- set bonuses and group skills: the weapon counts as one piece ----------------
        for (var si = 0; si < _rel.SetBonuses.Count; si++)
        {
            var count = Count(pieces, c => c.SetIds.Contains(si), _weapon.SetBonus == _rel.SetBonuses[si]);
            if (_rel.SetTargets[si] > 0) m.Add(count >= _rel.SetTargets[si]);
            var one = Reify(m, count, SkillAggregator.SetTierOnePieces);
            var two = Reify(m, count, SkillAggregator.SetTierTwoPieces);
            featureVars[Feature(score, FeatureKind.SetBonus, si)] = one + two;
        }
        for (var gi = 0; gi < _rel.GroupSkills.Count; gi++)
        {
            var count = Count(pieces, c => c.GroupId == gi, _weapon.GroupSkill == _rel.GroupSkills[gi]);
            if (_rel.GroupTargets[gi] > 0) m.Add(count >= _rel.GroupTargets[gi]);
            featureVars[Feature(score, FeatureKind.GroupSkill, gi)] = Reify(m, count, SkillAggregator.GroupSkillPieces);
        }

        // ---------------- score ----------------
        var unitIndex = score.Units.Select((u, ui) =>
        {
            var idx = m.NewIntVar(0, u.States - 1, $"u{ui}");
            m.Add(idx == LinearExpr.Sum(u.Features.Select((f, i) => LinearExpr.Term(featureVars[f], u.Strides[i]))));
            return idx;
        }).ToList();
        var sideScores = score.Sides.Select((_, side) => SideScore(m, score, side, unitIndex)).ToList();
        IntVar total;
        if (sideScores.Count == 1) total = sideScores[0].Var;
        else
        {
            total = m.NewIntVar(sideScores.Min(x => x.Lb), sideScores.Max(x => x.Ub), "score");
            m.AddMaxEquality(total, sideScores.Select(x => (LinearExpr)x.Var));
        }
        m.Maximize(LinearExpr.Term(total, ObjectiveScale) - LinearExpr.Term(LinearExpr.Sum(decos), DecoPenalty));
        return new Vars(pieces, tals, decos, total);
    }

    private sealed record Bounded(IntVar Var, long Lb, long Ub)
    {
        public static implicit operator LinearExpr(Bounded b) => b.Var;
    }

    private static Bounded NewVar(CpModel m, long lb, long ub, string name) => new(m.NewIntVar(lb, ub, name), lb, ub);

    /// <summary>Score of one health side in micro points: EFR + EFE + procs (+ the Dark Arts shockwave).</summary>
    private Bounded SideScore(CpModel m, ScoreDecomposition score, int side, List<IntVar> unitIndex)
    {
        var sc = score.Sides[side];
        // per-unit lookups of the channels that vary within the unit
        // state 0 of every unit is "nothing active", so a channel that is constant within a unit is at its neutral value
        Bounded? Lookup(int ui, Func<Channels, long> value, string name)
        {
            var table = score.Units[ui].Tables[side].Select(value).ToArray();
            if (table.All(v => v == table[0])) return null;
            var v = NewVar(m, table.Min(), table.Max(), $"{name}{side}_{ui}");
            m.AddElement(unitIndex[ui], table, v.Var);
            return v;
        }
        List<Bounded> All(Func<Channels, long> value, string name) =>
            Enumerable.Range(0, score.Units.Count).Select(ui => Lookup(ui, value, name)).Where(b => b is not null).Select(b => b!).ToList();
        Bounded Product(List<Bounded> factors, string name)
        {
            var acc = new Bounded(m.NewConstant(S), S, S);
            foreach (var f in factors)
            {
                var raw = NewVar(m, acc.Lb * f.Lb, acc.Ub * f.Ub, $"{name}x");
                m.AddMultiplicationEquality(raw.Var, acc.Var, f.Var);
                var next = NewVar(m, raw.Lb / S, raw.Ub / S, name);
                m.AddDivisionEquality(next.Var, raw.Var, S);
                acc = next;
            }
            return acc;
        }
        static long Sum(List<Bounded> xs, Func<Bounded, long> f) => xs.Sum(f);

        // raw: TR = B * prod(pct) + sum(flat), in 1e-4
        var pct = Product(All(c => (long)Math.Round(c.RawPct * S), "pct"), $"rawPct{side}");
        var flats = All(c => (long)Math.Round(c.RawFlat * S), "flat");
        var tr = NewVar(m, _weapon.TrueRaw * pct.Lb + Sum(flats, f => f.Lb), _weapon.TrueRaw * pct.Ub + Sum(flats, f => f.Ub), $"tr{side}");
        m.Add(tr.Var == LinearExpr.Term(pct.Var, _weapon.TrueRaw) + LinearExpr.Sum(flats.Select(f => (LinearExpr)f.Var)));

        // affinity, clamped
        var affs = All(c => (long)Math.Round(c.Affinity), "aff");
        var cap = DamageConstants.AffinityCap;
        var affRaw = LinearExpr.Constant(_weapon.Affinity) + LinearExpr.Sum(affs.Select(a => (LinearExpr)a.Var));
        var affLow = m.NewIntVar(-cap, Math.Max(-cap, _weapon.Affinity + Sum(affs, a => a.Ub)), $"affLow{side}");
        m.AddMaxEquality(affLow, [affRaw, LinearExpr.Constant(-cap)]);
        var aff = m.NewIntVar(-cap, cap, $"aff{side}");
        m.AddMinEquality(aff, [affLow, LinearExpr.Constant(cap)]);
        var affPos = m.NewIntVar(0, cap, $"affPos{side}");
        m.AddMaxEquality(affPos, [aff, LinearExpr.Constant(0)]);
        var affNeg = m.NewIntVar(0, cap, $"affNeg{side}");
        m.AddMaxEquality(affNeg, [LinearExpr.Term(aff, -1), LinearExpr.Constant(0)]);

        // crit factor in 1e-6: 1 + aff/100 * (critMult - 1), or the negative-crit penalty
        var crit = Single(All(c => (long)Math.Round(c.CritMult * S), "crit"), (long)Math.Round(sc.BaseCritMult * S));
        var critGain = NewVar(m, 0, cap * (crit.Ub - S), $"critGain{side}");
        m.AddMultiplicationEquality(critGain.Var, affPos, crit.Var - S);
        var negStep = (long)Math.Round((DamageConstants.NegativeCriticalMultiplier - 1.0) * S);
        var cf = NewVar(m, Micro + cap * negStep, Micro + critGain.Ub, $"cf{side}");
        m.Add(cf.Var == Micro + critGain.Var + LinearExpr.Term(affNeg, negStep));

        // EFR in micro points = TR(1e-4) * CF(1e-6) * sharp(1e-4) / 1e8
        var sharp = (long)Math.Round(sc.RawFactor * S);
        var trcf = NewVar(m, Math.Min(tr.Lb * cf.Lb, 0), tr.Ub * cf.Ub, $"trcf{side}");
        m.AddMultiplicationEquality(trcf.Var, tr.Var, cf.Var);
        var efr = NewVar(m, Math.Min(trcf.Lb * sharp / 100_000_000, 0), trcf.Ub * sharp / 100_000_000 + 1, $"efr{side}");
        m.AddDivisionEquality(efr.Var, LinearExpr.Term(trcf.Var, sharp), 100_000_000);

        var terms = new List<LinearExpr> { efr.Var };
        long lb = efr.Lb, ub = efr.Ub;

        if (score.HasElement)
        {
            // element in 1e-5 true = display * prod(pct)(1e-4) + sum(flat * 1e5), capped
            var elePct = Product(All(c => (long)Math.Round(c.ElePct * S), "epct"), $"elePct{side}");
            var eleFlats = All(c => (long)Math.Round(c.EleFlat * 10 * S), "eflat");
            var display = _weapon.ElementDisplay;
            var eleRaw = NewVar(m, display * elePct.Lb + Sum(eleFlats, f => f.Lb), display * elePct.Ub + Sum(eleFlats, f => f.Ub), $"eleRaw{side}");
            m.Add(eleRaw.Var == LinearExpr.Term(elePct.Var, display) + LinearExpr.Sum(eleFlats.Select(f => (LinearExpr)f.Var)));
            var eleCap = (long)Math.Round(sc.ElementCap * 10 * S);
            var ele = NewVar(m, Math.Min(eleRaw.Lb, eleCap), Math.Min(eleRaw.Ub, eleCap), $"ele{side}");
            m.AddMinEquality(ele.Var, [eleRaw.Var, LinearExpr.Constant(eleCap)]);

            var critEle = Single(All(c => (long)Math.Round(c.CritEleMult * S), "cele"), (long)Math.Round(sc.BaseCritEleMult * S));
            var ceGain = NewVar(m, 0, cap * Math.Max(0, critEle.Ub - S), $"ceGain{side}");
            m.AddMultiplicationEquality(ceGain.Var, affPos, critEle.Var - S);
            var cef = NewVar(m, Micro, Micro + ceGain.Ub, $"cef{side}");
            m.Add(cef.Var == Micro + ceGain.Var);

            // EFE in micro points = ele(1e-5) * CEF(1e-6) / 1e5 (-> 1e-6) * element scale (1e-6, sharpness included) / 1e6
            var ec = NewVar(m, ele.Lb * cef.Lb, ele.Ub * cef.Ub, $"ec{side}");
            m.AddMultiplicationEquality(ec.Var, ele.Var, cef.Var);
            var ecMicro = NewVar(m, ec.Lb / 100_000, ec.Ub / 100_000, $"ecMicro{side}");
            m.AddDivisionEquality(ecMicro.Var, ec.Var, 100_000);
            var eleFactor = (long)Math.Round(score.ElementScale * Micro);
            var efe = NewVar(m, ecMicro.Lb * eleFactor / Micro, ecMicro.Ub * eleFactor / Micro + 1, $"efe{side}");
            m.AddDivisionEquality(efe.Var, LinearExpr.Term(ecMicro.Var, eleFactor), Micro);
            terms.Add(efe.Var); lb += efe.Lb; ub += efe.Ub;
        }

        foreach (var p in All(c => (long)Math.Round(c.Procs * Micro), "proc"))
        {
            terms.Add(p.Var); lb += p.Lb; ub += p.Ub;
        }

        // Dark Arts shockwave: share * EFR + constant while Soul of the Dark Knight is active
        var shock = All(c => c.Shockwave ? 1 : 0, "shock");
        if (shock.Count > 0)
        {
            var on = shock[0].Var;
            var shockEfr = NewVar(m, Math.Min(efr.Lb, 0), efr.Ub, $"shockEfr{side}");
            m.AddMultiplicationEquality(shockEfr.Var, on, efr.Var);
            var share = (long)Math.Round(score.ShockwaveRawShare * Micro);
            var shockRaw = NewVar(m, Math.Min(shockEfr.Lb * share / Micro, 0), shockEfr.Ub * share / Micro + 1, $"shockRaw{side}");
            m.AddDivisionEquality(shockRaw.Var, LinearExpr.Term(shockEfr.Var, share), Micro);
            var constant = (long)Math.Round(score.ShockwaveConstant[side] * Micro);
            terms.Add(shockRaw.Var); terms.Add(LinearExpr.Term(on, constant));
            ub += shockRaw.Ub + constant; lb += Math.Min(0, shockRaw.Lb);
        }

        var result = NewVar(m, lb, ub, $"side{side}");
        m.Add(result.Var == LinearExpr.Sum(terms));
        return result;

        Bounded Single(List<Bounded> varying, long baseValue) =>
            varying.Count switch
            {
                0 => new Bounded(m.NewConstant(baseValue), baseValue, baseValue),
                1 => varying[0],
                _ => throw new InvalidOperationException("More than one unit changes the same critical multiplier."),
            };
    }

    private static int Feature(ScoreDecomposition score, FeatureKind kind, int index)
    {
        for (var f = 0; f < score.Features.Count; f++) if (score.Features[f].Kind == kind && score.Features[f].Index == index) return f;
        throw new InvalidOperationException($"No feature {kind} {index}.");
    }

    private static LinearExpr Count(List<List<(ArmorCandidate Candidate, BoolVar Var)>> pieces, Func<ArmorCandidate, bool> carries, bool weapon) =>
        LinearExpr.Sum(pieces.SelectMany(l => l).Where(x => carries(x.Candidate)).Select(x => (LinearExpr)x.Var)) + (weapon ? 1 : 0);

    /// <summary>A boolean that is true exactly when <paramref name="count"/> reaches <paramref name="threshold"/>.</summary>
    private static BoolVar Reify(CpModel m, LinearExpr count, int threshold)
    {
        var b = m.NewBoolVar($"ge{threshold}");
        m.Add(count >= threshold).OnlyEnforceIf(b);
        m.Add(count <= threshold - 1).OnlyEnforceIf(b.Not());
        return b;
    }

    /// <summary>Slots of level <paramref name="level"/> or higher, from per-level counts (index 0 = level 1).</summary>
    private static int SlotsAtLeast(int[] counts, int level)
    {
        var n = 0;
        for (var l = level; l <= 3; l++) n += counts[l - 1];
        return n;
    }

    private Loadout? BuildLoadout(int[] chosen, int talismanIndex, List<DecoCandidate> decos)
    {
        var talisman = talismanIndex >= 0 ? _talismans[talismanIndex].Talisman : null;
        var pieces = chosen.Select((c, d) => c >= 0 ? _armor[_kinds[d]][c] : null).ToArray();
        var free = new List<FreeSlot>();
        for (var i = 0; i < _weapon.Slots.Count; i++) free.Add(new FreeSlot(SkillKind.Weapon, _weapon.Slots[i], -1, i));
        if (talisman is not null)
            for (var i = 0; i < talisman.Slots.Count; i++) free.Add(new FreeSlot(talisman.Slots[i].Kind, talisman.Slots[i].Level, -2, i));
        for (var d = 0; d < pieces.Length; d++)
        {
            if (pieces[d] is not { } p) continue;
            for (var i = 0; i < p.EffectiveSlots.Count; i++) free.Add(new FreeSlot(SkillKind.Armor, p.EffectiveSlots[i], d, i));
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
            return pieces[d] is { } c ? new EquippedArmor(c.Piece, c.Transcended, Decos(d, c.EffectiveSlots.Count)) : null;
        }
        return new Loadout
        {
            Weapon = new EquippedWeapon(_weapon, Decos(-1, _weapon.Slots.Count)),
            Head = Armor(ArmorPieceKind.Head),
            Chest = Armor(ArmorPieceKind.Chest),
            Arms = Armor(ArmorPieceKind.Arms),
            Waist = Armor(ArmorPieceKind.Waist),
            Legs = Armor(ArmorPieceKind.Legs),
            Talisman = talisman is null || talisman.Rarity == 0 ? null : new EquippedTalisman(talisman, Decos(-2, talisman.Slots.Count)),
        };
    }
}
