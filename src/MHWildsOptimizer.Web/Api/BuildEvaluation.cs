using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Web.Api;

/// <summary>
/// One requirement of the request checked against a build: <paramref name="Required"/> / <paramref name="Actual"/> are skill
/// levels, or equipped pieces for set bonuses and group skills.
/// </summary>
public sealed record TargetCheckDto(string Skill, SkillKind Kind, string Label, int Required, int Actual, bool Met, bool FromCore);

/// <summary>A hand-entered build scored under the request's conditions; <paramref name="Build"/> is null when the weapon cannot be resolved.</summary>
public sealed record EvaluatedBuildDto(string Name, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, BuildDto? Build, IReadOnlyList<TargetCheckDto> Targets);

/// <summary>Scores hand-entered builds the way the optimizer scores its own: same weapon, conditions, skill limits and attack profile.</summary>
public static class BuildEvaluation
{
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
