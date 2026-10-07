using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Web.Api;

public sealed record DecoDto(string Name, int Slot, SkillKind Kind, string? IconColor, IReadOnlyList<SkillGrant> Skills);

public sealed record DecoCountDto(string Name, int Slot, SkillKind Kind, string? IconColor, int Count, IReadOnlyList<SkillGrant> Skills);

public sealed record BuildWeaponDto(
    string Type, string Label, int TrueRaw, int DisplayAttack, int Affinity, Element Element, int ElementDisplay, SharpnessColor? Sharpness,
    string? SetBonus, string? GroupSkill, IReadOnlyList<int> Slots, IReadOnlyList<DecoDto?> Decorations);

public sealed record BuildArmorDto(
    ArmorPieceKind Kind, int Id, string Name, string? Set, int Rarity, bool Transcended, IReadOnlyList<int> Slots, IReadOnlyList<DecoDto?> Decorations,
    IReadOnlyList<SkillGrant> Skills, IReadOnlyList<string> SetBonus, string? GroupSkill, int DefenseMax);

public sealed record BuildTalismanDto(string Name, int Rarity, TalismanSource Source, IReadOnlyList<SkillGrant> Skills, IReadOnlyList<string> Slots, IReadOnlyList<DecoDto?> Decorations);

public sealed record BuildSkillDto(string Skill, int Level, int Effective, SkillKind Kind, string? Icon, IReadOnlyList<string> Sources, int Wasted);

public sealed record SetBonusStateDto(string Name, int Pieces, int Tier, string? TierName, bool Active);

public sealed record GroupSkillStateDto(string Name, int Pieces, string? RankName, bool Active);

public sealed record StatsDto(
    double TrueRaw, int DisplayAttack, int Affinity, double CritMultiplier, double CritFactor, SharpnessColor? Sharpness, double SharpnessRaw, double SharpnessElement,
    double ElementTrue, int ElementDisplay, double ElementCap, double CritElement, double Efr, double Efe, double Procs, double Total, IReadOnlyList<string> Modifiers);

/// <param name="Procs">Proc damage per 100 MV (Dark Arts shockwave, Bad Blood); 0 when off or absent.</param>
public sealed record BuildSummaryDto(
    double Attack, int DisplayAttack, int BaseAttack, int Affinity, int BaseAffinity, double CritMultiplier, SharpnessColor? Sharpness,
    Element Element, double ElementTrue, int ElementDisplay, double Efr, double Efe, double Procs, double Total, double TotalAllConditions,
    IReadOnlyList<string> ActiveSetBonuses, IReadOnlyList<string> ActiveGroupSkills,
    IReadOnlyList<string> AffinitySources, IReadOnlyList<string> RawSources, IReadOnlyList<string> ElementSources, IReadOnlyList<string> ProcSources,
    IReadOnlyList<string> DependsOn, string Description);

public sealed record BuildDto(
    int Rank, double Score, double Efr, double Efe, double Procs, BuildSummaryDto Summary, BuildWeaponDto Weapon, IReadOnlyList<BuildArmorDto> Armor, BuildTalismanDto? Talisman,
    IReadOnlyList<DecoCountDto> Decorations, IReadOnlyList<BuildSkillDto> Skills, IReadOnlyList<SetBonusStateDto> SetBonuses, IReadOnlyList<GroupSkillStateDto> GroupSkills,
    StatsDto StatsRequested, StatsDto StatsAllOn, string Text);

public sealed record PairResultDto(int Rank, string Label, string SetBonus, string GroupSkill, double BestScore, long StatesEvaluated, string WorkLabel, string CandidateSummary, IReadOnlyList<BuildDto> Builds);

/// <param name="InputsHash">Fingerprint of the request and talismans the run used (<see cref="ProfileStore.InputsHash"/>); null in results saved before profiles.</param>
public sealed record ResultDto(DateTimeOffset CompletedAt, double ElapsedSeconds, SkillPairMode SkillPairMode, IReadOnlyList<PairResultDto> Pairs, string Text, string? InputsHash = null);

/// <summary>Turns optimizer output into the JSON the client renders (equipment, decorations, skills with sources, stats).</summary>
public static class ResultMapper
{
    public static ResultDto Map(OptimizationResult result, ResolvedRequest resolved, GameData data)
    {
        var pairs = result.PairResults.Select((pr, i) =>
        {
            var parts = pr.Label.Split(" + ", 2);
            return new PairResultDto(i + 1, pr.Label, parts[0], parts.Length > 1 ? parts[1] : "-", pr.BestScore, pr.StatesEvaluated, pr.WorkLabel, pr.CandidateSummary,
                pr.Builds.Select((b, j) => MapBuild(j + 1, b, resolved.Conditions, data)).ToList());
        }).ToList();
        return new ResultDto(DateTimeOffset.UtcNow, result.Elapsed.TotalSeconds, resolved.SkillPair.Mode, pairs, ResultsText.Render(result, resolved, data));
    }

    /// <param name="label">Title of the text report instead of "Build &lt;rank&gt;" (e.g. the name of a hand-entered build).</param>
    public static BuildDto MapBuild(int rank, RankedBuild b, Conditions cond, GameData data, string? label = null)
    {
        var loadout = b.Loadout;
        var w = loadout.Weapon.Stats;
        var s = BuildSummary.Create(loadout, data, cond);
        var skills = SkillAggregator.Aggregate(loadout, data);
        var sources = CollectSources(loadout);

        var weapon = new BuildWeaponDto(w.Type.ApiKind(), Catalog.WeaponLabel(w.Type), w.TrueRaw, w.DisplayAttack, w.Affinity, w.Element, w.ElementDisplay, w.TopSharpness,
            w.SetBonus, w.GroupSkill, w.Slots, loadout.Weapon.Decos.Select(Deco).ToList());

        var armor = loadout.ArmorPieces.Select(a => new BuildArmorDto(
            a.Piece.Piece, a.Piece.Id, a.Piece.Name, a.Piece.Set, a.Piece.Rarity, a.Transcended, a.EffectiveSlots, a.Decos.Select(Deco).ToList(),
            a.Piece.Skills, a.Piece.SetBonus, a.Piece.GroupSkill, a.Piece.DefenseMax)).ToList();

        var talisman = loadout.Talisman is { } t
            ? new BuildTalismanDto(t.Talisman.Name, t.Talisman.Rarity, t.Talisman.Source, t.Talisman.Skills, t.Talisman.Slots.Select(x => x.ToString()).ToList(), t.Decos.Select(Deco).ToList())
            : null;

        var allDecos = loadout.Weapon.Decos.Concat(loadout.ArmorPieces.SelectMany(a => a.Decos)).Concat(loadout.Talisman?.Decos ?? [])
            .Where(d => d is not null).Select(d => d!).ToList();
        var decorations = allDecos.GroupBy(d => d.Name)
            .OrderByDescending(g => g.First().Slot).ThenBy(g => g.Key)
            .Select(g => new DecoCountDto(g.Key, g.First().Slot, g.First().Kind, g.First().IconColor, g.Count(), g.First().Skills))
            .ToList();

        var skillList = skills.Levels
            .Select(kv =>
            {
                var skill = data.SkillsByName.GetValueOrDefault(kv.Key);
                var raw = skills.RawLevels[kv.Key];
                return new BuildSkillDto(kv.Key, kv.Value, cond.Effective(kv.Key, kv.Value), skill?.Kind ?? SkillKind.Armor, skill?.Icon, sources.GetValueOrDefault(kv.Key) ?? [], Math.Max(0, raw - kv.Value));
            })
            .OrderBy(x => x.Kind == SkillKind.Weapon ? 0 : 1).ThenByDescending(x => x.Level).ThenBy(x => x.Skill)
            .ToList();

        var setBonuses = skills.SetBonusPieces.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv =>
        {
            var tier = skills.SetTier(kv.Key);
            return new SetBonusStateDto(kv.Key, kv.Value, (int)tier, tier == SetBonusTier.None ? null : RankName(data, kv.Key, (int)tier), tier != SetBonusTier.None);
        }).ToList();

        var groupSkills = skills.GroupSkillPieces.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Select(kv => new GroupSkillStateDto(kv.Key, kv.Value, RankName(data, kv.Key, 1), skills.GroupActive(kv.Key)))
            .ToList();

        var requested = Stats(w, DamageCalculator.Calculate(w, skills, cond));
        var allOn = Stats(w, DamageCalculator.Calculate(w, skills, Conditions.AllOn));

        var summary = new BuildSummaryDto(s.Attack, s.DisplayAttack, s.BaseAttack, s.Affinity, s.BaseAffinity, s.CritMultiplier, s.Sharpness, s.Element, s.ElementTrue, s.ElementDisplay,
            s.Efr, s.Efe, s.Procs, s.Total, s.TotalAllConditions, s.ActiveSetBonuses, s.ActiveGroupSkills, s.AffinitySources, s.RawSources, s.ElementSources, s.ProcSources,
            s.DependsOn, s.Description);

        var text = LoadoutReport.Render(loadout, data, cond, label is null ? ResultsText.BuildTitle(rank, b) : ResultsText.BuildTitle(label, b)).TrimEnd();

        return new BuildDto(rank, b.Score, b.Result.EffectiveRaw, b.Result.EffectiveElement, b.Result.ProcDamage, summary, weapon, armor, talisman, decorations, skillList, setBonuses, groupSkills, requested, allOn, text);
    }

    private static DecoDto? Deco(Decoration? d) => d is null ? null : new DecoDto(d.Name, d.Slot, d.Kind, d.IconColor, d.Skills);

    private static StatsDto Stats(GogmaWeaponStats w, DamageResult r)
    {
        var mods = r.Breakdown.Skip(1).Where(l => !l.StartsWith("Raw ") && !l.StartsWith("Element ")).ToList();
        var denominator = r.TrueRaw * r.SharpnessRawModifier;
        return new StatsDto(
            r.TrueRaw, (int)Math.Round(r.TrueRaw * w.Type.Bloat()), r.Affinity, r.CriticalMultiplier, denominator > 0 ? r.EffectiveRaw / denominator : 1,
            r.Sharpness, r.SharpnessRawModifier, r.SharpnessElementModifier, r.ElementTrue, (int)Math.Round(r.ElementTrue * 10), r.ElementCap, r.CriticalElementMultiplier,
            r.EffectiveRaw, r.EffectiveElement, r.ProcDamage, r.Total, mods);
    }

    private static string? RankName(GameData data, string skill, int level) =>
        data.SkillsByName.TryGetValue(skill, out var s) ? s.Ranks.FirstOrDefault(x => x.Level == level)?.Name : null;

    /// <summary>Skill name -> "piece/decoration level" strings, same wording as the text report.</summary>
    private static Dictionary<string, List<string>> CollectSources(Loadout loadout)
    {
        var sources = new Dictionary<string, List<string>>();
        List<string> For(string skill) => sources.TryGetValue(skill, out var l) ? l : sources[skill] = [];
        void Add(IEnumerable<SkillGrant> grants, string label)
        {
            foreach (var g in grants) For(g.Skill).Add($"{label} {g.Level}");
        }
        void AddDecos(IEnumerable<Decoration?> decos)
        {
            foreach (var g in decos.Where(d => d is not null).GroupBy(d => d!.Name))
                foreach (var grant in g.First()!.Skills)
                    For(grant.Skill).Add(g.Count() > 1 ? $"{g.Key} x{g.Count()} {grant.Level * g.Count()}" : $"{g.Key} {grant.Level}");
        }

        AddDecos(loadout.Weapon.Decos);
        foreach (var a in loadout.ArmorPieces)
        {
            Add(a.Piece.Skills, a.Piece.Name);
            AddDecos(a.Decos);
        }
        if (loadout.Talisman is { } t)
        {
            Add(t.Talisman.Skills, t.Talisman.Name);
            AddDecos(t.Decos);
        }
        return sources;
    }
}
