using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

public enum SwitchAxePhial { Power, Element }

/// <summary>
/// One hit of an attack: motion value (multiplies true raw, percent) and element modifier (multiplies true element), from the
/// motion value datamine for game version 1.040 (notes/motion_values.md). Every hit can crit; a hit uses the weapon's sharpness
/// unless it has a <see cref="FixedSharpness"/>.
/// </summary>
public sealed record AttackHit(string Name, double Mv, double Element = 1.0)
{
    /// <summary>The hit lands this many times (phial explosions).</summary>
    public int Count { get; init; } = 1;
    /// <summary>The hit ignores the weapon's sharpness and acts as this color (the Great Sword tackle: green).</summary>
    public SharpnessColor? FixedSharpness { get; init; }
    /// <summary>Switch Axe sword-mode slash: the phial boosts it (<see cref="DamageConstants.PowerPhialRaw"/>, <see cref="DamageConstants.ElementPhialElement"/>).</summary>
    public bool SwordMode { get; init; }
    /// <summary>Switch Axe phial explosion: its element modifier with an Element phial; <see cref="Element"/> is the Power phial one.</summary>
    public double? ElementPhialElement { get; init; }
    /// <summary>Great Sword Lv3 charged slash (not the Rising Slash): the Dark Arts shockwave rides on it.</summary>
    public bool ChargedLv3 { get; init; }
    /// <summary>Great Sword True Charged Slash finisher: this hit replaces it when the first hit lands on a weak spot (raw hitzone x sharpness >= 45).</summary>
    public AttackHit? Power { get; init; }
}

/// <summary>An attack (one input or a short combo) the score is computed for.</summary>
/// <param name="Seconds">Rough time one execution takes in a steady stream of them; sets the hits per minute for cooldown-limited procs.</param>
public sealed record AttackDefinition(string Id, WeaponType Type, string Name, string Description, double Seconds, IReadOnlyList<AttackHit> Hits)
{
    public int HitCount => Hits.Sum(h => h.Count);
}

/// <summary>The attacks the attack picker offers (Great Sword and Switch Axe for now).</summary>
public static class Attacks
{
    /// <summary>The attack id of the average-hit model: every hit is one element hit of the profile's average MV.</summary>
    public const string Average = "average";

    private static readonly AttackHit Tcs1 = new("True Charged Slash 1 Lv3", 16);
    private static readonly AttackHit Tcs2 = new("True Charged Slash 2 Lv3", 209, 2.5)
    {
        ChargedLv3 = true,
        Power = new AttackHit("Power True Charged Slash 2 Lv3", 267, 2.5) { ChargedLv3 = true },
    };
    private static readonly AttackHit ChargedSlash = new("Charged Slash Lv3", 176, 1.5) { ChargedLv3 = true };
    private static readonly AttackHit StrongChargedSlash = new("Strong Charged Slash Lv3", 187, 1.8) { ChargedLv3 = true };
    private static readonly AttackHit Tackle = new("Tackle Lv3", 48, 0) { FixedSharpness = SharpnessColor.Green };

    private static AttackHit Sword(string name, double mv) => new(name, mv) { SwordMode = true };
    private static AttackHit Explosion(string name, double mv, double elementPhial, int count = 1) =>
        new(name, mv, 0.35) { ElementPhialElement = elementPhial, Count = count };
    private static AttackHit AmpedExplosion => Explosion("Amped Explosion", 12, 1.0);

    public static readonly IReadOnlyList<AttackDefinition> All =
    [
        new("true-charged-slash", WeaponType.GreatSword, "True Charged Slash Lv3",
            "Both hits of a fully charged True Charged Slash. The finisher is the power version when the first hit lands on a weak spot (raw hitzone x sharpness of 45 or more).",
            3, [Tcs1, Tcs2]),
        new("charge-combo", WeaponType.GreatSword, "Charge combo",
            "Charged Slash Lv3, Tackle, Strong Charged Slash Lv3, True Charged Slash Lv3. The tackle deals no element and hits at green sharpness.",
            9, [ChargedSlash, Tackle, StrongChargedSlash, Tcs1, Tcs2]),
        new("strong-charged-slash", WeaponType.GreatSword, "Strong Charged Slash Lv3", "A fully charged Strong Charged Slash.", 2.5, [StrongChargedSlash]),
        new("charged-slash", WeaponType.GreatSword, "Charged Slash Lv3", "A fully charged Charged Slash.", 2.5, [ChargedSlash]),

        new("full-release-slash", WeaponType.SwitchAxe, "Full Release Slash",
            "Both slashes and the eight phial explosions of an amped Full Release Slash (explosions: element x0.8 with an Element phial, x0.35 otherwise).",
            3.5, [Sword("Full Release Slash 1", 50), Sword("Full Release Slash 2", 82),
                  Explosion("Full Release Explosion 1", 20, 0.8, 3), Explosion("Full Release Explosion 2", 35, 0.8, 5)]),
        new("amped-sword-combo", WeaponType.SwitchAxe, "Amped sword combo",
            "Overhead Slash, Left and Right Rising Slash, Heavenward Flurry while amped: every slash sets off an amped explosion (element x1 with an Element phial, x0.35 otherwise).",
            5, [Sword("Overhead Slash", 40), Sword("Left Rising Slash", 62), Sword("Right Rising Slash", 58),
                Sword("Heavenward Flurry 1", 34), Sword("Heavenward Flurry 2", 46), AmpedExplosion with { Count = 5 }]),
        new("unbridled-slash", WeaponType.SwitchAxe, "Unbridled Slash",
            "The slash and its three phial explosions (element x0.8 with an Element phial, x0.35 otherwise).",
            2, [Sword("Unbridled Slash", 85), Explosion("Unbridled Slash Explosion", 25, 0.8, 3)]),
        new("zero-sum-discharge", WeaponType.SwitchAxe, "Zero Sum Discharge",
            "The full Zero Sum Discharge explosion (175 MV, element x1, not boosted by the phial).",
            3, [new AttackHit("Zero Sum Discharge Explosion", 175)]),
        new("axe-follow-up-morph", WeaponType.SwitchAxe, "Heavy Slam into Follow-up Morph Slash",
            "Axe Follow-up Heavy Slam, then Follow-up Morph Slash and its explosion (element x0.8 with an Element phial). Axe mode: no phial boost; Power Axe is not counted.",
            4, [new AttackHit("Axe Follow-up Heavy Slam 1", 32), new AttackHit("Axe Follow-up Heavy Slam 2", 90),
                new AttackHit("Axe Follow-up Morph Slash", 123), Explosion("Follow-up Morph Slash Explosion", 120, 0.8)]),
    ];

    public static IReadOnlyList<AttackDefinition> For(WeaponType type) => All.Where(a => a.Type == type).ToList();

    public static AttackDefinition? Find(WeaponType type, string? id) =>
        id is null ? null : All.FirstOrDefault(a => a.Type == type && string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The attack a weapon type is scored with when the profile names none; null means the average-hit model.</summary>
    public static string? DefaultFor(WeaponType type) => type switch
    {
        WeaponType.GreatSword => "true-charged-slash",
        WeaponType.SwitchAxe => "full-release-slash",
        _ => null,
    };

    /// <summary>
    /// The Switch Axe phial follows the Gogma focus (attack: Power, affinity and element: Element). Weapons entered as plain stats
    /// have no focus: Element phial when they carry an element, Power otherwise.
    /// </summary>
    public static SwitchAxePhial PhialOf(GogmaFocus? focus, Element element) => focus switch
    {
        GogmaFocus.Attack => SwitchAxePhial.Power,
        GogmaFocus.Affinity or GogmaFocus.Element => SwitchAxePhial.Element,
        _ => element == Element.None ? SwitchAxePhial.Power : SwitchAxePhial.Element,
    };
}
