using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>Skills a weapon type needs to function properly; added to the targets when options.require_weapon_core_skills is on (default).</summary>
public static class WeaponCoreSkills
{
    private static readonly Dictionary<WeaponType, (string Skill, int Level)[]> Table = new()
    {
        [WeaponType.GreatSword] = [("Focus", 3)],
        [WeaponType.LongSword] = [("Quick Sheathe", 3)],
    };

    public static IReadOnlyList<(string Skill, int Level)> For(WeaponType type) =>
        Table.TryGetValue(type, out var skills) ? skills : [];
}
