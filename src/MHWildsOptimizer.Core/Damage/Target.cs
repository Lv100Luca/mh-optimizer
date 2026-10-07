using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

public enum TargetKind { Custom, Dummy, Monster }

/// <summary>Element hitzones of a monster part, one per element, in percent.</summary>
public sealed record ElementHitzones(double Fire, double Water, double Thunder, double Ice, double Dragon)
{
    public double For(Element element) => element switch
    {
        Element.Fire => Fire,
        Element.Water => Water,
        Element.Thunder => Thunder,
        Element.Ice => Ice,
        Element.Dragon => Dragon,
        _ => 0,
    };
}

/// <summary>
/// Where the hits land: the raw and element hitzone of a monster part, the training dummy, or custom numbers.
/// The score stays on the raw-hitzone-100 scale of EFR (damage divided by raw hitzone / 100), so the raw hitzone decides the
/// weak-point effects (Weakness Exploit, the Great Sword's power True Charged Slash) and element counts at element hitzone / raw hitzone.
/// A monster target carries the numbers of its part, copied from the dataset when it is picked, so a request stays self-contained.
/// </summary>
public sealed record Target
{
    public TargetKind Kind { get; init; } = TargetKind.Custom;
    /// <summary>Monster name (monster targets).</summary>
    public string? Monster { get; init; }
    /// <summary>Part name (monster and dummy targets).</summary>
    public string? Part { get; init; }
    /// <summary>Raw hitzone in percent (slash, blunt or shot, whichever the weapon deals).</summary>
    public double RawHitzone { get; init; } = DefaultRawHitzone;
    /// <summary>Element hitzone in percent, for any element (custom and dummy targets).</summary>
    public double ElementHitzone { get; init; } = DefaultElementHitzone;
    /// <summary>Element hitzones per element (monster targets); overrides <see cref="ElementHitzone"/>.</summary>
    public ElementHitzones? Elements { get; init; }

    /// <summary>A typical weak point hit with the monster's weak element: raw 70, element 28 (element at 40 % of raw).</summary>
    public const double DefaultRawHitzone = 70;
    public const double DefaultElementHitzone = 28;

    public static Target Default => new();

    /// <summary>
    /// The training dummy (Barrel Puncher), soft hide without wounds. Read off community dummy tests (notes/skill_values_set_group.md):
    /// Bad Blood's 45 x raw hitzone hit lands for 36 on the weak point and 9 on the hard part (raw 80 / 20), Scorcher II's
    /// 40 + 120 x fire hitzone for 76 front / 46 back (element 30 / 5). VERIFY: other elements assumed equal to fire.
    /// </summary>
    public static readonly IReadOnlyList<Target> DummyParts =
    [
        new() { Kind = TargetKind.Dummy, Part = "Weak point", RawHitzone = 80, ElementHitzone = 30 },
        new() { Kind = TargetKind.Dummy, Part = "Hard part", RawHitzone = 20, ElementHitzone = 5 },
    ];

    public static Target Dummy(string part) =>
        DummyParts.FirstOrDefault(d => string.Equals(d.Part, part, StringComparison.OrdinalIgnoreCase)) ?? DummyParts[0];

    /// <summary>A monster part as a target; the raw hitzone is the one the weapon type deals (slash, blunt or shot).</summary>
    public static Target ForMonster(MonsterHitzones monster, PartHitzones part, WeaponType type) => new()
    {
        Kind = TargetKind.Monster,
        Monster = monster.Name,
        Part = part.Name,
        RawHitzone = RawHitzoneFor(part, type),
        ElementHitzone = 0,
        Elements = new ElementHitzones(part.Fire, part.Water, part.Thunder, part.Ice, part.Dragon),
    };

    public static int RawHitzoneFor(PartHitzones part, WeaponType type) => type switch
    {
        WeaponType.Hammer or WeaponType.HuntingHorn => part.Blunt,
        WeaponType.Bow or WeaponType.LightBowgun or WeaponType.HeavyBowgun => part.Pierce,
        _ => part.Slash,
    };

    public double ElementHitzoneFor(Element element) => Elements?.For(element) ?? ElementHitzone;

    /// <summary>Element hitzone / raw hitzone: the share of EFR's raw-hitzone-100 scale element is scored at.</summary>
    public double ElementRatio(Element element) => RawHitzone > 0 ? ElementHitzoneFor(element) / RawHitzone : 0;

    /// <summary>Weakness Exploit counts hits on a hitzone of 45 or more.</summary>
    public bool WeakPoint => RawHitzone >= DamageConstants.WeakPointHitzone;

    /// <summary>"Rey Dau, Head" / "Training dummy, Weak point" / "Custom".</summary>
    public string Name => Kind switch
    {
        TargetKind.Monster => $"{Monster}, {Part}",
        TargetKind.Dummy => $"Training dummy, {Part}",
        _ => "Custom",
    };

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (RawHitzone is <= 0 or > 200) errors.Add("conditions.target.raw_hitzone must be above 0 and at most 200.");
        if (ElementHitzone is < 0 or > 200) errors.Add("conditions.target.element_hitzone must be between 0 and 200.");
        if (Elements is { } e && new[] { e.Fire, e.Water, e.Thunder, e.Ice, e.Dragon }.Any(v => v is < 0 or > 200))
            errors.Add("conditions.target.elements must be between 0 and 200.");
        if (Kind == TargetKind.Monster && (string.IsNullOrWhiteSpace(Monster) || string.IsNullOrWhiteSpace(Part)))
            errors.Add("conditions.target needs a monster and a part.");
        return errors;
    }
}
