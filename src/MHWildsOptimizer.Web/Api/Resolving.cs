using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Web.Api;

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

public sealed record TargetDto(string Skill, int Level, bool FromCore);

public sealed record TalismanCountsDto(int Total, int Random, int Craftable);

public sealed record BaselineDto(double Attack, int Affinity, double CritMultiplier, double Efr, double Efe, double Total, double TotalAllOn);

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

    public static ResolveDto Describe(ResolvedRequest r, GameData data)
    {
        var weapon = r.IsValid ? Weapon(r.Weapon) : null;
        var core = r.AppliedCoreSkills.ToDictionary(c => c.Skill, c => c.Level);
        var targets = r.TargetSkills
            .Select(kv => new TargetDto(kv.Key, kv.Value, core.TryGetValue(kv.Key, out var lv) && lv >= kv.Value && !r.TargetSkills.Any(t => t.Key == kv.Key && t.Value > lv)))
            .OrderBy(t => t.FromCore).ThenBy(t => t.Skill)
            .ToList();

        BaselineDto? baseline = null;
        RelevanceDto? relevance = null;
        if (r.IsValid)
        {
            var bare = new Loadout { Weapon = new EquippedWeapon(r.Weapon) };
            var res = DamageCalculator.Calculate(bare, data, r.Conditions);
            var allOn = DamageCalculator.Calculate(bare, data, Conditions.AllOn);
            baseline = new BaselineDto(res.TrueRaw, res.Affinity, res.CriticalMultiplier, res.EffectiveRaw, res.EffectiveElement, res.Total, allOn.Total);

            var rel = Core.Optimize.Relevance.Build(r.Weapon, r.TargetSkills, r.Conditions, data);
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
