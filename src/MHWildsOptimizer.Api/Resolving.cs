using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Api;

public sealed record WeaponStatsDto(
    string Type,
    string Label,
    GogmaFocus? Focus,
    int TrueRaw,
    int DisplayAttack,
    int Affinity,
    Element Element,
    int ElementDisplay,
    double ElementTrue,
    SharpnessColor? Sharpness,
    SharpnessBarDto? SharpnessBar,
    int SharpnessBonus,
    IReadOnlyList<int> Slots,
    string? SetBonus,
    string? GroupSkill);

/// <summary>
/// One requirement. Armor / weapon skills carry a minimum level; set bonuses carry the tier (1 or 2) as the level and the
/// pieces that tier needs; group skills carry level 1 and their pieces. <paramref name="Label"/> is the text the CLI prints.
/// </summary>
public sealed record TargetDto(string Skill, int Level, SkillKind Kind, int? Pieces, string Label, bool FromCore);

public sealed record TalismanCountsDto(int Total, int Random, int Craftable);

public sealed record BaselineDto(double Attack, int Affinity, double CritMultiplier, double Efr, double Efe, double Procs, double Total, double TotalAllOn);

/// <summary>The skills, set bonuses and group skills the optimizer will value under the request's conditions.</summary>
public sealed record RelevanceDto(IReadOnlyList<string> Skills, IReadOnlyList<string> SetBonuses, IReadOnlyList<string> GroupSkills);

public sealed record ResolveDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    WeaponStatsDto? Weapon,
    IReadOnlyList<TargetDto> Targets,
    SkillPairMode SkillPairMode,
    int SkillPairCandidates,
    TalismanCountsDto Talismans,
    BaselineDto? Baseline,
    RelevanceDto? Relevance);

/// <summary>Validates a request + talismans against the dataset and describes what the optimizer would run with.</summary>
public static class Resolving
{
    public static ResolvedRequest Resolve(ConfigPayload payload, GameData data, string baseDirectory) =>
        RequestLoader.Resolve(payload.Request, data, baseDirectory, payload.Talismans ?? []);

    /// <param name="requested">The user's own target skills, to tell them apart from the weapon core skills the options add.</param>
    public static ResolveDto Describe(ResolvedRequest r, IReadOnlyDictionary<string, int> requested, GameData data)
    {
        var weapon = r.IsValid ? Weapon(r.Weapon) : null;
        var core = r.AppliedCoreSkills.Select(c => c.Skill).ToHashSet();
        var targets = r.TargetSkills
            .Select(kv => new TargetDto(kv.Key, kv.Value, data.SkillsByName.TryGetValue(kv.Key, out var s) ? s.Kind : SkillKind.Armor, null,
                $"{kv.Key} {kv.Value}", core.Contains(kv.Key) && !requested.ContainsKey(kv.Key)))
            .Concat(r.TargetSetBonuses.Select(kv => new TargetDto(kv.Key, ResolvedRequest.SetTierOf(kv.Value), SkillKind.Set, kv.Value, ResolvedRequest.SetBonusLabel(kv.Key, kv.Value), false)))
            .Concat(r.TargetGroupSkills.Select(kv => new TargetDto(kv.Key, 1, SkillKind.Group, kv.Value, ResolvedRequest.GroupSkillLabel(kv.Key, kv.Value), false)))
            .OrderBy(t => t.FromCore).ThenBy(t => t.Kind is SkillKind.Set or SkillKind.Group ? 1 : 0).ThenBy(t => t.Skill)
            .ToList();

        BaselineDto? baseline = null;
        RelevanceDto? relevance = null;
        if (r.IsValid)
        {
            var bare = new Loadout { Weapon = new EquippedWeapon(r.Weapon) };
            var res = DamageCalculator.Calculate(bare, data, r.Conditions);
            var allOn = DamageCalculator.Calculate(bare, data, Conditions.AllOn);
            baseline = new BaselineDto(res.TrueRaw, res.Affinity, res.CriticalMultiplier, res.EffectiveRaw, res.EffectiveElement, res.ProcDamage, res.Total, allOn.Total);

            var rel = Core.Optimize.Relevance.Build(r.Weapon, r, data);
            relevance = new RelevanceDto(rel.Skills, rel.SetBonuses, rel.GroupSkills);
        }

        return new ResolveDto(
            r.IsValid, r.Errors, r.Warnings, weapon, targets, r.SkillPair.Mode, r.SkillPairCandidates.Count,
            new TalismanCountsDto(r.Talismans.Count, r.Talismans.Count(t => t.Source == TalismanSource.Random), r.Talismans.Count(t => t.Source == TalismanSource.Crafted)),
            baseline, relevance);
    }

    public static WeaponStatsDto Weapon(GogmaWeaponStats w) => new(
        w.Type.ApiKind(), Catalog.WeaponLabel(w.Type), w.Focus, w.TrueRaw, w.DisplayAttack, w.Affinity, w.Element, w.ElementDisplay, w.ElementTrue,
        w.TopSharpness, w.SharpnessBar is { } sb ? new SharpnessBarDto(sb.Red, sb.Orange, sb.Yellow, sb.Green, sb.Blue, sb.White, sb.Purple) : null,
        w.SharpnessBonus, w.Slots, w.SetBonus, w.GroupSkill);
}
