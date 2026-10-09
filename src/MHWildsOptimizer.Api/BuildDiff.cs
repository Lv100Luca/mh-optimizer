using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Api;

/// <summary>The seven places a build is made of, in the order the game lists them.</summary>
public enum BuildSlot { Weapon, Head, Chest, Arms, Waist, Legs, Talisman }

/// <summary>How a slot differs between two builds: not at all, only its decorations, or another piece (another weapon or roll).</summary>
public enum SlotChange { Same, Decorations, Piece }

/// <summary>A skill whose level differs between two builds; <see cref="From"/> or <see cref="To"/> is 0 when one build lacks it.</summary>
public sealed record SkillChangeDto(string Skill, SkillKind Kind, int From, int To)
{
    public int Delta => To - From;
}

/// <summary>
/// What it takes to get from one build to another: the slots to swap or re-gem, the skills gained and lost (valued skills first),
/// and the set bonuses and group skills that turn on or off.
/// </summary>
public sealed record BuildDiffDto(
    IReadOnlyDictionary<BuildSlot, SlotChange> Slots,
    IReadOnlyList<SkillChangeDto> Skills,
    IReadOnlyList<string> BonusesGained,
    IReadOnlyList<string> BonusesLost)
{
    /// <summary>Slots that take another piece.</summary>
    public IReadOnlyList<BuildSlot> Swapped => Slots.Where(kv => kv.Value == SlotChange.Piece).Select(kv => kv.Key).ToList();

    /// <summary>Slots that keep the piece but take other decorations.</summary>
    public IReadOnlyList<BuildSlot> Regemmed => Slots.Where(kv => kv.Value == SlotChange.Decorations).Select(kv => kv.Key).ToList();

    public IEnumerable<SkillChangeDto> Gains => Skills.Where(s => s.Delta > 0);

    public IEnumerable<SkillChangeDto> Losses => Skills.Where(s => s.Delta < 0);

    public bool Identical => Slots.Values.All(c => c == SlotChange.Same);
}

public static class BuildDiff
{
    public static readonly BuildSlot[] AllSlots = Enum.GetValues<BuildSlot>();

    public static readonly IReadOnlyDictionary<BuildSlot, ArmorPieceKind> ArmorSlots = new Dictionary<BuildSlot, ArmorPieceKind>
    {
        [BuildSlot.Head] = ArmorPieceKind.Head, [BuildSlot.Chest] = ArmorPieceKind.Chest, [BuildSlot.Arms] = ArmorPieceKind.Arms,
        [BuildSlot.Waist] = ArmorPieceKind.Waist, [BuildSlot.Legs] = ArmorPieceKind.Legs,
    };

    /// <param name="valued">Skills that count for the score (they are listed first); null keeps the order by size of the change.</param>
    public static BuildDiffDto Between(BuildDto from, BuildDto to, IReadOnlyCollection<string>? valued = null)
    {
        var slots = AllSlots.ToDictionary(s => s, s => Change(from, to, s));

        var levelsFrom = from.Skills.ToDictionary(x => x.Skill);
        var levelsTo = to.Skills.ToDictionary(x => x.Skill);
        var skills = levelsFrom.Keys.Union(levelsTo.Keys)
            .Select(name =>
            {
                var a = levelsFrom.GetValueOrDefault(name);
                var b = levelsTo.GetValueOrDefault(name);
                return new SkillChangeDto(name, (b ?? a)!.Kind, a?.Level ?? 0, b?.Level ?? 0);
            })
            .Where(x => x.Delta != 0)
            .OrderBy(x => valued is null || valued.Contains(x.Skill) ? 0 : 1)
            .ThenByDescending(x => Math.Abs(x.Delta))
            .ThenBy(x => x.Skill, StringComparer.InvariantCulture)
            .ToList();

        var bonusesFrom = Bonuses(from);
        var bonusesTo = Bonuses(to);
        return new BuildDiffDto(slots, skills, bonusesTo.Except(bonusesFrom).ToList(), bonusesFrom.Except(bonusesTo).ToList());
    }

    private static List<string> Bonuses(BuildDto b) => [.. b.Summary.ActiveSetBonuses, .. b.Summary.ActiveGroupSkills];

    private static SlotChange Change(BuildDto a, BuildDto b, BuildSlot slot)
    {
        switch (slot)
        {
            case BuildSlot.Weapon:
            {
                var (x, y) = (a.Weapon, b.Weapon);
                var same = x.Type == y.Type && x.TrueRaw == y.TrueRaw && x.Affinity == y.Affinity && x.Element == y.Element && x.ElementDisplay == y.ElementDisplay
                           && x.Sharpness == y.Sharpness && x.SetBonus == y.SetBonus && x.GroupSkill == y.GroupSkill && x.Slots.SequenceEqual(y.Slots);
                return !same ? SlotChange.Piece : Decos(x.Decorations, y.Decorations);
            }
            case BuildSlot.Talisman:
            {
                var (x, y) = (a.Talisman, b.Talisman);
                if (x is null && y is null) return SlotChange.Same;
                if (x is null || y is null || x.Name != y.Name) return SlotChange.Piece;
                return Decos(x.Decorations, y.Decorations);
            }
            default:
            {
                var kind = ArmorSlots[slot];
                var x = a.Armor.FirstOrDefault(p => p.Kind == kind);
                var y = b.Armor.FirstOrDefault(p => p.Kind == kind);
                if (x is null && y is null) return SlotChange.Same;
                if (x is null || y is null || x.Name != y.Name || x.Transcended != y.Transcended) return SlotChange.Piece;
                return Decos(x.Decorations, y.Decorations);
            }
        }
    }

    // decorations are compared as a multiset: the same jewels in another slot order are the same build
    private static SlotChange Decos(IReadOnlyList<DecoDto?> a, IReadOnlyList<DecoDto?> b) =>
        a.Where(d => d is not null).Select(d => d!.Name).Order().SequenceEqual(b.Where(d => d is not null).Select(d => d!.Name).Order())
            ? SlotChange.Same
            : SlotChange.Decorations;
}
