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
}

/// <summary>Everything the optimizer needs, loadable from inputs/request.json (snake_case keys).</summary>
public sealed record OptimizationRequest
{
    public required WeaponStatsInput Weapon { get; init; }
    public SkillPairSettings SkillPair { get; init; } = new();
    /// <summary>Required skills with minimum levels, e.g. { "Weakness Exploit": 5, "Agitator": 5 }.</summary>
    public Dictionary<string, int> TargetSkills { get; init; } = new();
    public Conditions Conditions { get; init; } = Conditions.Default;
    public TalismanSettings Talismans { get; init; } = new();
    public OptimizerOptions Options { get; init; } = new();
}

/// <summary>A request after validation, with everything resolved against the dataset.</summary>
public sealed record ResolvedRequest(
    GogmaWeaponStats Weapon,
    IReadOnlyList<GogmaSkillPair?> SkillPairCandidates,
    SkillPairSettings SkillPair,
    IReadOnlyDictionary<string, int> TargetSkills,
    Conditions Conditions,
    IReadOnlyList<Talisman> Talismans,
    OptimizerOptions Options,
    IReadOnlyList<(string Skill, int Level)> AppliedCoreSkills,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public static class RequestLoader
{
    public static OptimizationRequest Read(string path) => GameDataLoader.ReadJson<OptimizationRequest>(path);

    /// <summary>Reads and resolves a request file; relative paths inside it are resolved against the file's directory.</summary>
    public static ResolvedRequest Load(string path, GameData data) =>
        Resolve(Read(path), data, Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

    public static ResolvedRequest Resolve(OptimizationRequest request, GameData data, string baseDirectory)
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

        // target skills
        var targets = new Dictionary<string, int>();
        foreach (var (name, level) in request.TargetSkills)
        {
            if (!data.SkillsByName.TryGetValue(name, out var skill)) { errors.Add($"Target skill '{name}' is unknown."); continue; }
            if (skill.Kind is SkillKind.Set or SkillKind.Group) { errors.Add($"Target '{name}' is a {skill.Kind} skill; request set bonuses through the weapon or armor, not as a skill level."); continue; }
            if (level < 1 || level > skill.MaxLevel) { errors.Add($"Target '{name}' level {level} is outside 1..{skill.MaxLevel}."); continue; }
            targets[name] = level;
        }

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
        if (request.Talismans.File is { } file)
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
        foreach (var set in request.Options.ExcludeSets)
            if (!data.Armor.Any(a => a.Set == set)) warnings.Add($"options.exclude_sets: no armor set named '{set}'.");

        return new ResolvedRequest(
            weapon, candidates, request.SkillPair, targets, request.Conditions, talismans, request.Options, applied, errors, warnings);
    }

    private static GogmaWeaponStats PlaceholderWeapon() =>
        new(WeaponType.GreatSword, null, 0, 0, Element.None, 0, SharpnessColor.White, null, 0, [3, 3, 3], null, null);
}
