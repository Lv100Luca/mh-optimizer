using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Api;

/// <summary>
/// One requirement of the request checked against a build: <paramref name="Required"/> / <paramref name="Actual"/> are skill
/// levels, or equipped pieces for set bonuses and group skills.
/// </summary>
public sealed record TargetCheckDto(string Skill, SkillKind Kind, string Label, int Required, int Actual, bool Met, bool FromCore);

/// <summary>A hand-entered build scored under the request's conditions; <paramref name="Build"/> is null when the weapon cannot be resolved.</summary>
public sealed record EvaluatedBuildDto(string Name, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, BuildDto? Build, IReadOnlyList<TargetCheckDto> Targets);

/// <summary>
/// A build's damage on one attack, combo or sequence of the weapon type, under the request's conditions and target.
/// <paramref name="Group"/> is "Moves", "Combos" or "Sequences"; <paramref name="Current"/> marks the attack the build is scored on
/// (its numbers equal the build's score). Per minute uses the attack's own duration, except for the current one, which keeps the
/// profile's landed hits per minute.
/// </summary>
public sealed record AttackDamageDto(string Id, string Name, string Group, bool Current, double Hits, double Mv, double DamagePerExecution, double DamagePerMinute,
    double Score, string Execution);

/// <summary>
/// One hit against the target, before the monster's defense rate and per-hit rounding: raw and element without and with a
/// critical hit, and <paramref name="Expected"/> = the average over the build's affinity (what the score counts). Values are for
/// a single landing; <paramref name="Count"/> is how often it lands per execution.
/// </summary>
/// <param name="Shockwave">The Dark Arts shockwave the hit before it sets off (a proc, listed under its Lv3 charged slash).</param>
public sealed record HitDamageDto(string Name, int Count, double Mv, double Raw, double RawCrit, double Element, double ElementCrit, double Expected,
    IReadOnlyList<string> Notes, bool Shockwave = false);

/// <summary>
/// One step of an attack or sequence (a single attack has one): its hits under the step's conditions, the hunt conditions the
/// step changes (snake_case key, value), the stats they give, and the proc damage of one execution other than the Dark Arts
/// shockwaves (those are rows of <paramref name="Hits"/>). <paramref name="Total"/> is the step's damage over all its <paramref name="Repeat"/>s.
/// </summary>
public sealed record AttackStepDamageDto(string Name, int Repeat, IReadOnlyDictionary<string, bool> Conditions, double TrueRaw, int Affinity, double CritMultiplier,
    double ElementTrue, double CritElementMultiplier, IReadOnlyList<HitDamageDto> Hits, double Procs, IReadOnlyList<string> ProcSources, double Total);

/// <summary>
/// What one row of <see cref="BuildEvaluation.AttackBreakdown"/> is made of. The steps' totals add up to the row's damage per
/// execution. <paramref name="Notes"/> say what applies to the whole attack (health side, target); <paramref name="Breakdown"/> is
/// the calculator's trace (every modifier).
/// </summary>
public sealed record AttackDetailDto(string Id, string Name, IReadOnlyList<AttackStepDamageDto> Steps, double Total, IReadOnlyList<string> Notes,
    IReadOnlyList<string> Breakdown);

/// <summary>Scores hand-entered builds the way the optimizer scores its own: same weapon, conditions, skill limits and attack profile.</summary>
public static class BuildEvaluation
{
    public const string GroupMoves = "Moves";
    public const string GroupCombos = "Combos";
    public const string GroupSequences = "Sequences";

    /// <summary>A row of the attack breakdown: the attack profile it is scored with (the request's own for the current attack).</summary>
    private sealed record AttackRow(string Id, string Name, string Group, bool Current, AttackProfile Profile);

    private static List<AttackRow> AttackRows(ResolvedRequest r)
    {
        var type = r.Weapon.Type;
        var attacks = Attacks.For(type);
        if (attacks.Count == 0) return [];
        var profile = r.Conditions.AttackProfile;
        var currentId = profile.IsSequence ? Attacks.Sequence : profile.Attack ?? Attacks.DefaultFor(type);
        // other attacks run at their own pace: a hits-per-minute override belongs to the attack it was set for
        AttackRow Row(string id, string name, string group, AttackProfile attack) =>
            string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase) ? new(id, name, group, true, profile) : new(id, name, group, false, attack);

        var rows = new List<AttackRow>();
        rows.AddRange(attacks.Where(a => !a.Combo).Select(a => Row(a.Id, a.Name, GroupMoves, new AttackProfile { Attack = a.Id })));
        rows.AddRange(attacks.Where(a => a.Combo).Select(a => Row(a.Id, a.Name, GroupCombos, new AttackProfile { Attack = a.Id })));
        rows.AddRange(Attacks.SequencePresetsFor(type).Select(s =>
            Row("preset:" + s.Id, s.Name, GroupSequences, new AttackProfile { Attack = Attacks.Sequence, Sequence = s.Steps })));
        if (profile.IsSequence && profile.Sequence is { Count: > 0 })
            rows.Add(Row(Attacks.Sequence, "Your sequence", GroupSequences, profile));
        return rows;
    }

    private static (GogmaWeaponStats Weapon, ActiveSkills Skills) Equip(ResolvedRequest r, BuildInput input, GameData data)
    {
        var random = r.Talismans.Where(t => t.Source == TalismanSource.Random).ToList();
        var loadout = BuildInputLoader.Convert(input, r.Weapon, random, data).Loadout;
        return (loadout.Weapon.Stats, SkillAggregator.Aggregate(loadout, data));
    }

    /// <summary>
    /// The damage of <paramref name="input"/> on every move, combo and example sequence of the weapon type, plus the custom
    /// sequence when the attack profile uses one. Empty for weapon types without attack data (the average-hit model).
    /// </summary>
    public static IReadOnlyList<AttackDamageDto> AttackBreakdown(ResolvedRequest r, BuildInput input, GameData data)
    {
        var rows = AttackRows(r);
        if (rows.Count == 0) return [];
        var (weapon, skills) = Equip(r, input, data);
        return rows.Select(row =>
        {
            var result = DamageCalculator.Calculate(weapon, skills, r.Conditions with { AttackProfile = row.Profile }, trace: false);
            var a = result.Attack;
            return new AttackDamageDto(row.Id, row.Name, row.Group, row.Current, a.Hits, a.TotalMv, result.DamagePerExecution, result.DamagePerMinute, result.Total, a.Execution);
        }).ToList();
    }

    /// <summary>
    /// One row of <see cref="AttackBreakdown"/> taken apart: every hit without and with a crit, each sequence step under its own
    /// conditions, procs, and the calculator's trace. Null when <paramref name="id"/> is not a row.
    /// </summary>
    public static AttackDetailDto? AttackDetail(ResolvedRequest r, BuildInput input, GameData data, string id)
    {
        if (AttackRows(r).FirstOrDefault(x => x.Id == id) is not { } row) return null;
        var (weapon, skills) = Equip(r, input, data);
        var type = weapon.Type;
        var asked = r.Conditions with { AttackProfile = row.Profile };
        // both health sides on: the calculator scores the better one, so the steps use that one too
        var conditions = DamageCalculator.HealthSide(weapon, skills, asked);
        var whole = DamageCalculator.Calculate(weapon, skills, conditions, trace: true);
        var hitsPerMinute = conditions.Attack(weapon).HitsPerMinute;
        var target = conditions.Target;
        var eleHitzone = target.ElementHitzoneFor(weapon.Element);

        var notes = new List<string>();
        if (!ReferenceEquals(conditions, asked))
            notes.Add(conditions.FullHealth ? "Scored at full health (better for this build than red / low health)." : "Scored at red / low health (better for this build than full health).");
        var eleName = weapon.Element == Element.None ? "element" : weapon.Element.ToString().ToLowerInvariant();
        notes.Add(FormattableString.Invariant($"Against {target.Name}: raw hitzone {target.RawHitzone:0.#}, {eleName} hitzone {eleHitzone:0.#}{(target.WeakPoint ? ", a weak point" : "")}. Before the monster's defense rate and per-hit rounding."));

        IEnumerable<SequenceStep> steps = row.Profile.IsSequence
            ? row.Profile.Sequence ?? []
            : [new SequenceStep { Attack = row.Profile.Attack ?? Attacks.DefaultFor(type)! }];
        var result = new List<AttackStepDamageDto>();
        foreach (var step in steps)
        {
            if (Attacks.Find(type, step.Attack) is not { } attack || step.Repeat < 1) continue;
            var changed = step.Conditions.Where(kv => ConditionToggles.StepKeys.Contains(kv.Key) && ConditionToggles.Get(conditions, kv.Key) != kv.Value)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);
            // a step scored on its own, at the pace of the whole attack (cooldown-limited procs per hit stay the same)
            var stepConditions = ConditionToggles.With(conditions, changed) with
            {
                AttackProfile = new AttackProfile { Attack = attack.Id, HitsPerMinute = hitsPerMinute },
            };
            var d = DamageCalculator.Calculate(weapon, skills, stepConditions, trace: true);
            var critFactor = d.CriticalFactor;
            var critEleFactor = d.Affinity > 0 ? 1.0 + d.Affinity / 100.0 * (d.CriticalElementMultiplier - 1.0) : 1.0;
            var shockwave = DamageCalculator.CountsShockwave(type, skills, stepConditions);
            var (waveRaw, waveEle) = DamageCalculator.ShockwaveParts(d.TrueRaw, d.SharpnessRawModifier, d.SharpnessElementModifier, target.ElementRatio(weapon.Element));
            waveRaw *= target.RawHitzone / 100.0;
            waveEle *= target.RawHitzone / 100.0;
            var hits = new List<HitDamageDto>();
            foreach (var h in AttackProfile.ResolveHits(weapon, target, attack.Hits))
            {
                var raw = d.TrueRaw * h.Mv / 100.0 * h.RawModifier * target.RawHitzone / 100.0;
                var ele = d.ElementTrue * h.ElementModifier * eleHitzone / 100.0;
                hits.Add(new HitDamageDto(h.Name, h.Count, h.Mv, raw, raw * d.CriticalMultiplier, ele, ele * d.CriticalElementMultiplier,
                    raw * critFactor + ele * critEleFactor, h.Notes));
                if (shockwave && h.ChargedLv3)
                    hits.Add(new HitDamageDto(DamageCalculator.ShockwaveLabel, h.Count, DamageConstants.DarkArtsShockwaveMv, waveRaw, waveRaw * d.CriticalMultiplier,
                        waveEle, waveEle, waveRaw * critFactor + waveEle,
                        [FormattableString.Invariant($"{DamageConstants.DarkArtsShockwaveMv:0} MV raw + {DamageConstants.DarkArtsShockwaveElement:0} fixed element (no crit), Soul of the Dark Knight")],
                        Shockwave: true));
            }
            // the shockwaves are rows above; what is left are the other procs (Bad Blood)
            var procs = d.ProcDamage * d.Attack.TotalMv / 100.0 * target.RawHitzone / 100.0 - hits.Where(h => h.Shockwave).Sum(h => h.Expected * h.Count);
            if (Math.Abs(procs) < 1e-9) procs = 0;
            var sources = d.Breakdown.Where(l => l.Contains(": proc +", StringComparison.Ordinal)).Select(l => l[..l.IndexOf(": proc +", StringComparison.Ordinal)])
                .Where(l => l != DamageCalculator.ShockwaveLabel).ToList();
            var once = hits.Sum(h => h.Expected * h.Count) + procs;
            result.Add(new AttackStepDamageDto(attack.Name, step.Repeat, changed, d.TrueRaw, d.Affinity, d.CriticalMultiplier, d.ElementTrue, d.CriticalElementMultiplier,
                hits, procs, sources, once * step.Repeat));
        }
        return new AttackDetailDto(row.Id, row.Name, result, result.Sum(s => s.Total), notes, whole.Breakdown);
    }

    public static IReadOnlyList<EvaluatedBuildDto> Evaluate(ConfigPayload payload, ResolvedRequest r, GameData data)
    {
        var builds = payload.Builds ?? [];
        if (builds.Count == 0) return [];

        var weaponErrors = payload.Request.Weapon.Validate(data);
        if (weaponErrors.Count > 0)
            return builds.Select(b => new EvaluatedBuildDto(b.Name, ["The weapon has errors; fix it on the Weapon tab to score builds.", .. weaponErrors], [], null, [])).ToList();

        var random = r.Talismans.Where(t => t.Source == TalismanSource.Random).ToList();
        string[] requestWarning = r.IsValid ? [] : ["The request has errors (see Review); the score may be off."];

        return builds.Select(input =>
        {
            var c = BuildInputLoader.Convert(input, r.Weapon, random, data);
            var loadout = c.Loadout;
            var w = loadout.Weapon.Stats;
            var pair = w.SetBonus is { } s && w.GroupSkill is { } g ? new GogmaSkillPair(s, g) : null;
            var ranked = new RankedBuild(loadout, DamageCalculator.Calculate(loadout, data, r.Conditions), pair, $"{w.SetBonus ?? "-"} + {w.GroupSkill ?? "-"}");
            var build = ResultMapper.MapBuild(0, ranked, r.Conditions, data, input.Name);
            var targets = CheckTargets(r, SkillAggregator.Aggregate(loadout, data), payload.Request.TargetSkills, data);
            return new EvaluatedBuildDto(input.Name, c.Errors, [.. c.Warnings, .. requestWarning], build, targets);
        }).ToList();
    }

    /// <param name="requested">The user's own targets, to tell them apart from the weapon core skills the options add.</param>
    private static List<TargetCheckDto> CheckTargets(ResolvedRequest r, ActiveSkills skills, IReadOnlyDictionary<string, int> requested, GameData data)
    {
        var core = r.AppliedCoreSkills.Select(c => c.Skill).ToHashSet();
        return r.TargetSkills
            .Select(kv => Check(kv.Key, data.SkillsByName.TryGetValue(kv.Key, out var s) ? s.Kind : SkillKind.Armor, $"{kv.Key} {kv.Value}", kv.Value,
                skills.Level(kv.Key), core.Contains(kv.Key) && !requested.ContainsKey(kv.Key)))
            .Concat(r.TargetSetBonuses.Select(kv => Check(kv.Key, SkillKind.Set, ResolvedRequest.SetBonusLabel(kv.Key, kv.Value), kv.Value,
                skills.SetBonusPieces.GetValueOrDefault(kv.Key), false)))
            .Concat(r.TargetGroupSkills.Select(kv => Check(kv.Key, SkillKind.Group, ResolvedRequest.GroupSkillLabel(kv.Key, kv.Value), kv.Value,
                skills.GroupSkillPieces.GetValueOrDefault(kv.Key), false)))
            .OrderBy(t => t.FromCore).ThenBy(t => t.Kind is SkillKind.Set or SkillKind.Group ? 1 : 0).ThenBy(t => t.Skill)
            .ToList();

        static TargetCheckDto Check(string skill, SkillKind kind, string label, int required, int actual, bool fromCore) =>
            new(skill, kind, label, required, actual, actual >= required, fromCore);
    }
}
