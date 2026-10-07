using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Inputs;

public enum SkillPairMode
{
    /// <summary>Use the set bonus / group skill the weapon currently has (from the weapon input).</summary>
    Fixed,
    /// <summary>Try every rollable (set bonus, group skill) pair and report the best <see cref="SkillPairSettings.TopN"/>.</summary>
    Optimize,
}

public enum OptimizerEngine
{
    /// <summary>Dynamic programming over skill states with a beam (<see cref="OptimizerOptions.MaxStatesPerDepth"/>); fast, not guaranteed optimal.</summary>
    Beam,
    /// <summary>Constraint programming (OR-Tools CP-SAT) over pieces, talisman and decoration counts with the exact score; proves the optimum.</summary>
    CpSat,
}

public sealed record SkillPairSettings
{
    public SkillPairMode Mode { get; init; } = SkillPairMode.Fixed;
    /// <summary>How many of the best pairs to report in Optimize mode.</summary>
    public int TopN { get; init; } = 3;
}

public sealed record TalismanSettings
{
    /// <summary>Path to the user's random talismans JSON (relative to the request file). Optional.</summary>
    public string? File { get; init; }
    /// <summary>Also consider every craftable charm line at max rank.</summary>
    public bool IncludeCraftable { get; init; } = true;
}

public sealed record OptimizerOptions
{
    /// <summary>Use transcended slots on rarity 5/6 armor (HR 100+).</summary>
    public bool AllowTranscendence { get; init; } = true;
    /// <summary>Number of loadouts to report.</summary>
    public int TopN { get; init; } = 5;
    /// <summary>Ignore armor below this rarity.</summary>
    public int MinRarity { get; init; } = 5;
    /// <summary>Armor set names to leave out (e.g. event sets you do not own).</summary>
    public List<string> ExcludeSets { get; init; } = [];
    /// <summary>Add the weapon's core skills to the targets (Great Sword: Focus 3, Long Sword: Quick Sheathe 3).</summary>
    public bool RequireWeaponCoreSkills { get; init; } = true;
    /// <summary>Search beam: partial builds kept per armor slot. Larger is closer to exhaustive but slower (default 100000).</summary>
    public int MaxStatesPerDepth { get; init; } = 100_000;
    /// <summary>Worker threads for the search; 0 = all logical processors. Values above the processor count are capped.</summary>
    public int MaxThreads { get; init; }
    /// <summary>Search engine: the state search with a beam (default) or the exact CP-SAT model.</summary>
    public OptimizerEngine Engine { get; init; } = OptimizerEngine.Beam;
    /// <summary>CP-SAT only: time limit per solve in seconds; the best build found so far is used when it runs out.</summary>
    public double CpSatTimeLimitSeconds { get; init; } = 120;

    /// <summary>The thread count actually used: <see cref="MaxThreads"/> resolved against this machine.</summary>
    public int EffectiveThreads => MaxThreads <= 0 ? ProcessorCount : Math.Min(MaxThreads, ProcessorCount);

    /// <summary>
    /// Logical processors of this machine. The browser app sets it from <c>navigator.hardwareConcurrency</c>: in WebAssembly
    /// <see cref="Environment.ProcessorCount"/> is 1, but CP-SAT's workers run as their own web workers there.
    /// </summary>
    public static int ProcessorCount { get; set; } = Environment.ProcessorCount;
}

/// <summary>Everything the optimizer needs, loadable from inputs/request.json (snake_case keys).</summary>
public sealed record OptimizationRequest
{
    public required WeaponStatsInput Weapon { get; init; }
    public SkillPairSettings SkillPair { get; init; } = new();
    /// <summary>
    /// Required skills with minimum levels, e.g. { "Weakness Exploit": 5, "Agitator": 5 }. Set bonuses and group skills are
    /// required the same way, the level being the tier the game shows: a set bonus at 1 needs 2 pieces, at 2 needs 4 pieces;
    /// a group skill at 1 needs 3 pieces. The Gogma weapon counts as a piece when it rolled that set bonus / group skill.
    /// </summary>
    public Dictionary<string, int> TargetSkills { get; init; } = new();
    public Conditions Conditions { get; init; } = Conditions.Default;
    public TalismanSettings Talismans { get; init; } = new();
    public OptimizerOptions Options { get; init; } = new();
}

/// <summary>A request after validation, with everything resolved against the dataset.</summary>
/// <param name="TargetSkills">Required armor / weapon skill levels (set bonuses and group skills are split out).</param>
/// <param name="TargetSetBonuses">Required set bonuses with the number of pieces their tier needs.</param>
/// <param name="TargetGroupSkills">Required group skills with the number of pieces they need.</param>
public sealed record ResolvedRequest(
    GogmaWeaponStats Weapon,
    IReadOnlyList<GogmaSkillPair?> SkillPairCandidates,
    SkillPairSettings SkillPair,
    IReadOnlyDictionary<string, int> TargetSkills,
    IReadOnlyDictionary<string, int> TargetSetBonuses,
    IReadOnlyDictionary<string, int> TargetGroupSkills,
    Conditions Conditions,
    IReadOnlyList<Talisman> Talismans,
    OptimizerOptions Options,
    IReadOnlyList<(string Skill, int Level)> AppliedCoreSkills,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    /// <summary>Every requirement for display: "Weakness Exploit 5", "Gore Magala's Tyranny II (4 pieces)", "Lord's Soul (3 pieces)".</summary>
    public IEnumerable<string> TargetLabels =>
        TargetSkills.Select(kv => $"{kv.Key} {kv.Value}")
            .Concat(TargetSetBonuses.Select(kv => SetBonusLabel(kv.Key, kv.Value)))
            .Concat(TargetGroupSkills.Select(kv => GroupSkillLabel(kv.Key, kv.Value)));

    /// <summary>The tier a required piece count stands for: 1 below <see cref="SkillAggregator.SetTierTwoPieces"/>, else 2.</summary>
    public static int SetTierOf(int pieces) => pieces >= SkillAggregator.SetTierTwoPieces ? 2 : 1;

    /// <summary>"Gore Magala's Tyranny II (4 pieces)"</summary>
    public static string SetBonusLabel(string name, int pieces) => $"{name} {(SetTierOf(pieces) == 2 ? "II" : "I")} ({pieces} pieces)";

    /// <summary>"Lord's Soul (3 pieces)"</summary>
    public static string GroupSkillLabel(string name, int pieces) => $"{name} ({pieces} pieces)";
}

public static class RequestLoader
{
    public static OptimizationRequest Read(string path) => GameDataLoader.ReadJson<OptimizationRequest>(path);

    /// <summary>Reads and resolves a request file; relative paths inside it are resolved against the file's directory.</summary>
    public static ResolvedRequest Load(string path, GameData data) =>
        Resolve(Read(path), data, Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

    /// <param name="talismanOverride">Talismans to use instead of reading the request's talisman file (e.g. unsaved edits).</param>
    public static ResolvedRequest Resolve(OptimizationRequest request, GameData data, string baseDirectory, IReadOnlyList<TalismanInput>? talismanOverride = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // weapon
        var weaponErrors = request.Weapon.Validate(data);
        errors.AddRange(weaponErrors);
        var weapon = weaponErrors.Count == 0 ? request.Weapon.ToStats(data) : PlaceholderWeapon();

        // skill pair candidates
        var candidates = new List<GogmaSkillPair?>();
        if (request.SkillPair.Mode == SkillPairMode.Fixed)
        {
            if (weapon.SetBonus is null && weapon.GroupSkill is null)
                warnings.Add("Skill pair mode is 'fixed' but the weapon has neither set_bonus nor group_skill; the weapon will count for nothing.");
            candidates.Add(weapon.SetBonus is { } s && weapon.GroupSkill is { } g ? new GogmaSkillPair(s, g) : null);
        }
        else
        {
            if (data.GogmaSkillPairs.Count == 0)
                errors.Add("Skill pair mode is 'optimize' but data/gogma_skill_pool.json was not loaded.");
            candidates.AddRange(data.GogmaSkillPairs);
            if (request.SkillPair.TopN < 1) errors.Add("skill_pair.top_n must be at least 1.");
        }

        // target skills; set bonuses and group skills are required by tier, converted to the pieces that tier needs
        var targets = new Dictionary<string, int>();
        var setTargets = new Dictionary<string, int>();
        var groupTargets = new Dictionary<string, int>();
        foreach (var (name, level) in request.TargetSkills)
        {
            if (!data.SkillsByName.TryGetValue(name, out var skill)) { errors.Add($"Target skill '{name}' is unknown."); continue; }
            if (level < 1 || level > skill.MaxLevel) { errors.Add($"Target '{name}' level {level} is outside 1..{skill.MaxLevel}."); continue; }
            switch (skill.Kind)
            {
                case SkillKind.Set: setTargets[name] = PiecesRequired(skill, level); break;
                case SkillKind.Group: groupTargets[name] = PiecesRequired(skill, level); break;
                default: targets[name] = level; break;
            }
        }

        // skill limits (0 = exclude, n = value only up to n)
        foreach (var (name, limit) in request.Conditions.SkillLimits)
        {
            if (!data.SkillsByName.TryGetValue(name, out var skill)) { errors.Add($"Skill limit '{name}' is unknown."); continue; }
            if (skill.Kind is SkillKind.Set or SkillKind.Group) { errors.Add($"Skill limit '{name}' is a {skill.Kind} skill."); continue; }
            if (limit < 0 || limit > skill.MaxLevel) { errors.Add($"Skill limit '{name}' {limit} is outside 0..{skill.MaxLevel}."); continue; }
            if (targets.TryGetValue(name, out var target) && target > limit)
                errors.Add($"Target '{name}' {target} is above its limit {limit}; raise the limit or lower the target.");
        }

        // attack profile (element and proc damage)
        var profile = request.Conditions.AttackProfile;
        if (profile.HitsPerMinute is <= 0) errors.Add("conditions.attack_profile.hits_per_minute must be above 0.");
        if (profile.AverageMv is <= 0) errors.Add("conditions.attack_profile.average_mv must be above 0.");
        if (profile.ChargedLv3Share is < 0 or > 1) errors.Add("conditions.attack_profile.charged_lv3_share must be between 0 and 1.");
        if (profile.ElementHitzoneRatio is < 0 or > 1) errors.Add("conditions.attack_profile.element_hitzone_ratio must be between 0 and 1.");

        // weapon core skills (Focus 3 for Great Sword, Quick Sheathe 3 for Long Sword)
        var applied = new List<(string Skill, int Level)>();
        if (request.Options.RequireWeaponCoreSkills && weaponErrors.Count == 0)
        {
            foreach (var (skill, level) in WeaponCoreSkills.For(weapon.Type))
            {
                if (!data.SkillsByName.ContainsKey(skill)) { errors.Add($"Core skill '{skill}' missing from the dataset."); continue; }
                if (targets.GetValueOrDefault(skill) < level) targets[skill] = level;
                applied.Add((skill, level));
            }
        }

        // talismans
        var talismans = new List<Talisman>();
        if (request.Talismans.IncludeCraftable)
            talismans.AddRange(data.CraftableTalismans);
        if (talismanOverride is not null)
        {
            var (parsed, tErrors, tWarnings) = TalismanInputLoader.Convert(talismanOverride, data);
            talismans.AddRange(parsed);
            errors.AddRange(tErrors);
            warnings.AddRange(tWarnings);
        }
        else if (request.Talismans.File is { } file)
        {
            var path = Path.IsPathRooted(file) ? file : Path.Combine(baseDirectory, file);
            if (!File.Exists(path))
                errors.Add($"Talisman file '{path}' not found.");
            else
            {
                var (parsed, tErrors, tWarnings) = TalismanInputLoader.Convert(TalismanInputLoader.Read(path), data);
                talismans.AddRange(parsed);
                errors.AddRange(tErrors);
                warnings.AddRange(tWarnings);
            }
        }
        if (talismans.Count == 0)
            warnings.Add("No talismans available; the optimizer will run without a charm.");

        // options
        if (request.Options.TopN < 1) errors.Add("options.top_n must be at least 1.");
        if (request.Options.MaxThreads < 0) errors.Add("options.max_threads must be 0 (all processors) or more.");
        if (request.Options.CpSatTimeLimitSeconds <= 0) errors.Add("options.cp_sat_time_limit_seconds must be above 0.");
        foreach (var set in request.Options.ExcludeSets)
            if (!data.Armor.Any(a => a.Set == set)) warnings.Add($"options.exclude_sets: no armor set named '{set}'.");

        // required set bonuses / group skills must be reachable with the armor the options allow, plus the weapon's roll
        var excluded = request.Options.ExcludeSets.ToHashSet();
        var allowedArmor = data.Armor.Where(a => a.Rarity >= request.Options.MinRarity && (a.Set is null || !excluded.Contains(a.Set))).ToList();
        bool WeaponCanRoll(Func<GogmaSkillPair, string> part, string? rolled, string name) =>
            request.SkillPair.Mode == SkillPairMode.Fixed ? rolled == name : candidates.Any(p => p is not null && part(p) == name);
        foreach (var (name, pieces) in setTargets)
            CheckReachable(name, pieces, allowedArmor.Where(a => a.SetBonus.Contains(name)), WeaponCanRoll(p => p.SetBonus, weapon.SetBonus, name));
        foreach (var (name, pieces) in groupTargets)
            CheckReachable(name, pieces, allowedArmor.Where(a => a.GroupSkill == name), WeaponCanRoll(p => p.GroupSkill, weapon.GroupSkill, name));
        void CheckReachable(string name, int pieces, IEnumerable<ArmorPiece> carriers, bool weaponPiece)
        {
            var kinds = carriers.Select(a => a.Piece).Distinct().Count();
            var available = kinds + (weaponPiece ? 1 : 0);
            if (available < pieces)
                errors.Add($"Target '{name}' needs {pieces} pieces but only {available} can carry it " +
                           $"({kinds} armor kinds at rarity {request.Options.MinRarity}+ outside the excluded sets{(weaponPiece ? ", plus the weapon" : "")}).");
        }

        return new ResolvedRequest(
            weapon, candidates, request.SkillPair, targets, setTargets, groupTargets, request.Conditions, talismans, request.Options, applied, errors, warnings);
    }

    /// <summary>Pieces (armor, or the Gogma weapon) a set bonus / group skill needs at a level, from the skill's ranks.</summary>
    public static int PiecesRequired(Skill skill, int level) =>
        skill.Ranks.FirstOrDefault(r => r.Level == level)?.PiecesRequired
        ?? (skill.Kind == SkillKind.Group ? SkillAggregator.GroupSkillPieces
            : level >= 2 ? SkillAggregator.SetTierTwoPieces : SkillAggregator.SetTierOnePieces);

    private static GogmaWeaponStats PlaceholderWeapon() =>
        new(WeaponType.GreatSword, null, 0, 0, Element.None, 0, SharpnessColor.White, null, 0, [3, 3, 3], null, null);
}
