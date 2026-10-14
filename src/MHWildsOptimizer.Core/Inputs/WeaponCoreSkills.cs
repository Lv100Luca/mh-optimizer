using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>
/// Skills a weapon type needs to function properly, and the targets a new weapon of the type starts with. They are plain
/// targets: a new weapon carries them in its target skills, where they can be changed or removed like any other.
/// </summary>
public static class WeaponCoreSkills
{
    private static readonly Dictionary<WeaponType, (string Skill, int Level)[]> Table = new()
    {
        [WeaponType.GreatSword] = [("Focus", 3)],
        [WeaponType.LongSword] = [("Quick Sheathe", 3)],
    };

    private static readonly Dictionary<WeaponType, (string Skill, int Level)[]> Starting = new()
    {
        [WeaponType.GreatSword] = [("Focus", 3), ("Stun Resistance", 3)],
        [WeaponType.LongSword] = [("Quick Sheathe", 3)],
    };

    /// <summary>The core skills of a weapon type (Great Sword: Focus 3, Long Sword: Quick Sheathe 3).</summary>
    public static IReadOnlyList<(string Skill, int Level)> For(WeaponType type) =>
        Table.TryGetValue(type, out var skills) ? skills : [];

    /// <summary>The targets a new weapon of a type starts with: its core skills, for the Great Sword also Stun Resistance 3.</summary>
    public static IReadOnlyList<(string Skill, int Level)> StartingTargets(WeaponType type) =>
        Starting.TryGetValue(type, out var skills) ? skills : [];

    /// <inheritdoc cref="StartingTargets(WeaponType)"/>
    /// <param name="kind">The API kind, e.g. "great-sword"; an unknown kind has none.</param>
    public static IReadOnlyList<(string Skill, int Level)> StartingTargets(string? kind) =>
        TryType(kind) is { } type ? StartingTargets(type) : [];

    /// <summary>
    /// The targets of a weapon whose type changes from <paramref name="fromKind"/> to <paramref name="toKind"/>: the old type's
    /// starting targets still at their starting level go, the new type's come in (a higher target stays). A new starting
    /// target is left out where <paramref name="limits"/> cap the skill below it.
    /// </summary>
    public static Dictionary<string, int> FollowType(IReadOnlyDictionary<string, int> targets, string? fromKind, string? toKind,
        IReadOnlyDictionary<string, int> limits)
    {
        var result = new Dictionary<string, int>(targets);
        if (fromKind == toKind) return result;
        foreach (var (skill, level) in StartingTargets(fromKind))
            if (result.TryGetValue(skill, out var t) && t == level) result.Remove(skill);
        foreach (var (skill, level) in StartingTargets(toKind))
            if (result.GetValueOrDefault(skill) < level && (!limits.TryGetValue(skill, out var cap) || cap >= level))
                result[skill] = level;
        return result;
    }

    /// <summary>
    /// A request saved while <see cref="OptimizerOptions.RequireWeaponCoreSkills"/> was an option (before 2026-10-14): the core
    /// skills it added at resolve time written into the target skills (a higher target stays) and the option off. A request
    /// with the option off comes back as it is.
    /// </summary>
    public static OptimizationRequest FoldLegacyOption(OptimizationRequest request)
    {
        if (!request.Options.RequireWeaponCoreSkills) return request;
        var targets = new Dictionary<string, int>(request.TargetSkills);
        if (TryType(request.Weapon.Spec?.Type ?? request.Weapon.Type) is { } type)
            foreach (var (skill, level) in For(type))
                if (targets.GetValueOrDefault(skill) < level) targets[skill] = level;
        return request with { TargetSkills = targets, Options = request.Options with { RequireWeaponCoreSkills = false } };
    }

    private static WeaponType? TryType(string? kind)
    {
        if (kind is null) return null;
        try { return WeaponTypeInfo.FromApiKind(kind); }
        catch (ArgumentException) { return null; }
    }
}
