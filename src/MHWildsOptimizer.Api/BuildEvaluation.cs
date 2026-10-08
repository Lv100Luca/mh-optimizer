using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
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

/// <summary>Scores hand-entered builds the way the optimizer scores its own: same weapon, conditions, skill limits and attack profile.</summary>
public static class BuildEvaluation
{
    public const string GroupMoves = "Moves";
    public const string GroupCombos = "Combos";
    public const string GroupSequences = "Sequences";

    /// <summary>
    /// The damage of <paramref name="input"/> on every move, combo and example sequence of the weapon type, plus the custom
    /// sequence when the attack profile uses one. Empty for weapon types without attack data (the average-hit model).
    /// </summary>
    public static IReadOnlyList<AttackDamageDto> AttackBreakdown(ResolvedRequest r, BuildInput input, GameData data)
    {
        var type = r.Weapon.Type;
        var attacks = Attacks.For(type);
        if (attacks.Count == 0) return [];

        var random = r.Talismans.Where(t => t.Source == TalismanSource.Random).ToList();
        var loadout = BuildInputLoader.Convert(input, r.Weapon, random, data).Loadout;
        var weapon = loadout.Weapon.Stats;
        var skills = SkillAggregator.Aggregate(loadout, data);
        var profile = r.Conditions.AttackProfile;
        var currentId = profile.IsSequence ? Attacks.Sequence : profile.Attack ?? Attacks.DefaultFor(type);

        AttackDamageDto Score(string id, string name, string group, AttackProfile attack)
        {
            var current = string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase);
            // other attacks run at their own pace: a hits-per-minute override belongs to the attack it was set for
            var conditions = r.Conditions with { AttackProfile = current ? profile : attack };
            var result = DamageCalculator.Calculate(weapon, skills, conditions, trace: false);
            var a = result.Attack;
            return new AttackDamageDto(id, name, group, current, a.Hits, a.TotalMv, result.DamagePerExecution, result.DamagePerMinute, result.Total, a.Execution);
        }

        var rows = new List<AttackDamageDto>();
        rows.AddRange(attacks.Where(a => !a.Combo).Select(a => Score(a.Id, a.Name, GroupMoves, new AttackProfile { Attack = a.Id })));
        rows.AddRange(attacks.Where(a => a.Combo).Select(a => Score(a.Id, a.Name, GroupCombos, new AttackProfile { Attack = a.Id })));
        rows.AddRange(Attacks.SequencePresetsFor(type).Select(s =>
            Score("preset:" + s.Id, s.Name, GroupSequences, new AttackProfile { Attack = Attacks.Sequence, Sequence = s.Steps })));
        if (profile.IsSequence && profile.Sequence is { Count: > 0 })
            rows.Add(Score(Attacks.Sequence, "Your sequence", GroupSequences, profile));
        return rows;
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
