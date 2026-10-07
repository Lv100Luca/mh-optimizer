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

/// <summary>A single input (<see cref="Combo"/> false) or a short combo of inputs the score can be computed for.</summary>
/// <param name="Seconds">Rough time one execution takes in a steady stream of them; sets the hits per minute for cooldown-limited procs.</param>
public sealed record AttackDefinition(string Id, WeaponType Type, string Name, string Description, double Seconds, IReadOnlyList<AttackHit> Hits)
{
    public bool Combo { get; init; }
    public int HitCount => Hits.Sum(h => h.Count);
}

/// <summary>A ready-made attack sequence to start from (the sequence editor loads its steps).</summary>
public sealed record SequencePreset(string Id, WeaponType Type, string Name, string Description, IReadOnlyList<SequenceStep> Steps);

/// <summary>The attacks the attack picker and the sequence editor offer (Great Sword and Switch Axe for now).</summary>
public static class Attacks
{
    /// <summary>The attack id of the average-hit model: every hit is one element hit of the profile's average MV.</summary>
    public const string Average = "average";
    /// <summary>The attack id of a custom sequence (<see cref="AttackProfile.Sequence"/>).</summary>
    public const string Sequence = "sequence";

    // ---------------- Great Sword (1.040) ----------------
    private static readonly AttackHit Tcs1 = new("True Charged Slash 1 Lv3", 16);
    private static readonly AttackHit Tcs2 = new("True Charged Slash 2 Lv3", 209, 2.5)
    {
        ChargedLv3 = true,
        Power = new AttackHit("Power True Charged Slash 2 Lv3", 267, 2.5) { ChargedLv3 = true },
    };
    private static readonly AttackHit ChargedSlash = new("Charged Slash Lv3", 176, 1.5) { ChargedLv3 = true };
    private static readonly AttackHit StrongChargedSlash = new("Strong Charged Slash Lv3", 187, 1.8) { ChargedLv3 = true };
    private static readonly AttackHit Tackle = new("Tackle Lv3", 48, 0) { FixedSharpness = SharpnessColor.Green };

    // ---------------- Switch Axe (1.040) ----------------
    private static AttackHit Sword(string name, double mv) => new(name, mv) { SwordMode = true };
    private static AttackHit Explosion(string name, double mv, double elementPhial, int count = 1) =>
        new(name, mv, 0.35) { ElementPhialElement = elementPhial, Count = count };
    private static AttackHit AmpedExplosion(int count) => Explosion("Amped Explosion", 12, 1.0, count);

    /// <summary>Sword-mode slashes while amped: every slash sets off an amped explosion.</summary>
    private static AttackHit[] Amped(params (string Name, double Mv)[] slashes) =>
        [.. slashes.Select(s => Sword(s.Name, s.Mv)), AmpedExplosion(slashes.Length)];

    private const string AmpedNote = " Amped: every slash sets off an amped explosion (12 MV, element x1 with an Element phial, x0.35 otherwise).";

    public static readonly IReadOnlyList<AttackDefinition> All =
    [
        // Great Sword moves
        new("true-charged-slash", WeaponType.GreatSword, "True Charged Slash Lv3",
            "Both hits of a fully charged True Charged Slash. The finisher is the power version when the first hit lands on a weak spot (raw hitzone x sharpness of 45 or more).",
            3, [Tcs1, Tcs2]),
        new("strong-charged-slash", WeaponType.GreatSword, "Strong Charged Slash Lv3", "A fully charged Strong Charged Slash.", 2.5, [StrongChargedSlash]),
        new("charged-slash", WeaponType.GreatSword, "Charged Slash Lv3", "A fully charged Charged Slash.", 2.5, [ChargedSlash]),
        new("offset-rising-slash", WeaponType.GreatSword, "Offset Rising Slash Lv3", "A fully charged Offset Rising Slash (no Dark Arts shockwave: it is a rising slash).", 2.5,
            [new AttackHit("Offset Rising Slash Lv3", 154, 1.3)]),
        new("follow-up-cross-slash", WeaponType.GreatSword, "Follow-up Cross Slash Lv3", "Both hits of the follow-up after a successful Lv3 offset.", 2,
            [new AttackHit("Follow-up Cross Slash 1 Lv3", 58), new AttackHit("Follow-up Cross Slash 2 Lv3", 230)]),
        new("tackle", WeaponType.GreatSword, "Tackle (shoulder bash) Lv3", "Costs stamina, deals no element and hits at green sharpness.", 0.8, [Tackle]),
        new("strong-wide-slash", WeaponType.GreatSword, "Strong Wide Slash Lv3", "A fully charged Strong Wide Slash.", 2, [new AttackHit("Strong Wide Slash Lv3", 124, 2.2)]),
        new("leaping-wide-slash", WeaponType.GreatSword, "Leaping Wide Slash Lv3", "A fully charged Leaping Wide Slash.", 2.5, [new AttackHit("Leaping Wide Slash Lv3", 167, 3.1)]),
        new("wide-slash", WeaponType.GreatSword, "Wide Slash", "An uncharged wide slash.", 1, [new AttackHit("Wide Slash", 42)]),
        new("jumping-charged-slash", WeaponType.GreatSword, "Jumping Charged Slash Lv3", "A fully charged jumping slash.", 2, [new AttackHit("Jumping Charged Slash Lv3", 90, 1.3)]),
        new("charged-rising-slash", WeaponType.GreatSword, "Charged Rising Slash Lv3", "A fully charged rising slash.", 1.5, [new AttackHit("Charged Rising Slash Lv3", 98)]),
        // Great Sword combos
        new("charge-combo", WeaponType.GreatSword, "Charge combo",
            "Charged Slash Lv3, Tackle, Strong Charged Slash Lv3, True Charged Slash Lv3. The tackle deals no element and hits at green sharpness.",
            9, [ChargedSlash, Tackle, StrongChargedSlash, Tcs1, Tcs2]) { Combo = true },

        // Switch Axe moves: sword mode
        new("full-release-slash", WeaponType.SwitchAxe, "Full Release Slash",
            "Both slashes and the eight phial explosions of an amped Full Release Slash (explosions: element x0.8 with an Element phial, x0.35 otherwise).",
            3.5, [Sword("Full Release Slash 1", 50), Sword("Full Release Slash 2", 82),
                  Explosion("Full Release Explosion 1", 20, 0.8, 3), Explosion("Full Release Explosion 2", 35, 0.8, 5)]),
        new("unbridled-slash", WeaponType.SwitchAxe, "Unbridled Slash",
            "The slash and its three phial explosions (element x0.8 with an Element phial, x0.35 otherwise).",
            2, [Sword("Unbridled Slash", 85), Explosion("Unbridled Slash Explosion", 25, 0.8, 3)]),
        new("zero-sum-discharge", WeaponType.SwitchAxe, "Zero Sum Discharge",
            "The full Zero Sum Discharge explosion (175 MV, element x1, not boosted by the phial).",
            3, [new AttackHit("Zero Sum Discharge Explosion", 175)]),
        new("sword-overhead-slash", WeaponType.SwitchAxe, "Sword: Overhead Slash", "One sword slash." + AmpedNote, 1, Amped(("Overhead Slash", 40))),
        new("sword-left-rising-slash", WeaponType.SwitchAxe, "Sword: Left Rising Slash", "One sword slash." + AmpedNote, 1, Amped(("Left Rising Slash", 62))),
        new("sword-right-rising-slash", WeaponType.SwitchAxe, "Sword: Right Rising Slash", "One sword slash." + AmpedNote, 1, Amped(("Right Rising Slash", 58))),
        new("sword-double-slash", WeaponType.SwitchAxe, "Sword: Double Slash", "Both slashes." + AmpedNote, 1.2, Amped(("Double Slash 1", 22), ("Double Slash 2", 26))),
        new("sword-heavenward-flurry", WeaponType.SwitchAxe, "Sword: Heavenward Flurry", "Both slashes." + AmpedNote, 1.5, Amped(("Heavenward Flurry 1", 34), ("Heavenward Flurry 2", 46))),
        new("sword-counter-rising-slash", WeaponType.SwitchAxe, "Sword: Counter Rising Slash (success)", "Both hits after a successful counter." + AmpedNote, 2,
            Amped(("Counter Rising Slash 1 (Success)", 37), ("Counter Rising Slash 2", 45))),
        new("sword-morph-double-slash", WeaponType.SwitchAxe, "Sword: Morph Double Slash", "Both slashes, morphing to axe." + AmpedNote, 1.5,
            Amped(("Morph Double Slash 1", 40), ("Morph Double Slash 2", 60))),
        // Switch Axe moves: axe mode (no phial boost; Power Axe is not counted)
        new("axe-overhead-slash", WeaponType.SwitchAxe, "Axe: Overhead Slash", "Axe mode: no phial boost.", 1.2, [new AttackHit("Axe Overhead Slash", 45)]),
        new("axe-side-slash", WeaponType.SwitchAxe, "Axe: Side Slash", "Axe mode: no phial boost.", 1, [new AttackHit("Axe Side Slash", 23)]),
        new("axe-spiral-burst-slash", WeaponType.SwitchAxe, "Axe: Spiral Burst Slash", "Both hits. Axe mode: no phial boost.", 1.8,
            [new AttackHit("Axe Spiral Burst Slash 1", 39), new AttackHit("Axe Spiral Burst Slash 2", 50)]),
        new("axe-heavy-slam", WeaponType.SwitchAxe, "Axe: Heavy Slam", "Both hits; activates Power Axe (not counted).", 2,
            [new AttackHit("Axe Heavy Slam 1", 15), new AttackHit("Axe Heavy Slam 2", 72)]),
        new("axe-follow-up-heavy-slam", WeaponType.SwitchAxe, "Axe: Follow-up Heavy Slam", "Both hits; activates Power Axe (not counted).", 2,
            [new AttackHit("Axe Follow-up Heavy Slam 1", 32), new AttackHit("Axe Follow-up Heavy Slam 2", 90)]),
        new("axe-follow-up-morph-slash", WeaponType.SwitchAxe, "Axe: Follow-up Morph Slash",
            "The morph slash and its explosion (element x0.8 with an Element phial, x0.35 otherwise).", 2,
            [new AttackHit("Axe Follow-up Morph Slash", 123), Explosion("Follow-up Morph Slash Explosion", 120, 0.8)]),
        new("axe-morph-sweep", WeaponType.SwitchAxe, "Axe: Morph Sweep", "All three hits. Axe mode: no phial boost.", 2,
            [new AttackHit("Axe Morph Sweep 1", 20), new AttackHit("Axe Morph Sweep 2", 70), new AttackHit("Axe Morph Sweep 3", 35)]),
        // Switch Axe combos
        new("amped-sword-combo", WeaponType.SwitchAxe, "Amped sword combo",
            "Overhead Slash, Left and Right Rising Slash, Heavenward Flurry while amped." + AmpedNote,
            5, Amped(("Overhead Slash", 40), ("Left Rising Slash", 62), ("Right Rising Slash", 58), ("Heavenward Flurry 1", 34), ("Heavenward Flurry 2", 46))) { Combo = true },
        new("axe-follow-up-morph", WeaponType.SwitchAxe, "Heavy Slam into Follow-up Morph Slash",
            "Axe Follow-up Heavy Slam, then Follow-up Morph Slash and its explosion (element x0.8 with an Element phial). Axe mode: no phial boost; Power Axe is not counted.",
            4, [new AttackHit("Axe Follow-up Heavy Slam 1", 32), new AttackHit("Axe Follow-up Heavy Slam 2", 90),
                new AttackHit("Axe Follow-up Morph Slash", 123), Explosion("Follow-up Morph Slash Explosion", 120, 0.8)]) { Combo = true },
    ];

    /// <summary>Example sequences to start the editor from; they are starting points, not measured optimal rotations.</summary>
    public static readonly IReadOnlyList<SequencePreset> SequencePresets =
    [
        new("offset-into-tcs", WeaponType.GreatSword, "Offset into True Charged Slash",
            "Lv3 offset with follow-up, another Lv3 offset, tackle (costs stamina, so the Strong Charged Slash after it loses Maximum Might), Strong Charged Slash, True Charged Slash.",
            [
                new SequenceStep { Attack = "offset-rising-slash" },
                new SequenceStep { Attack = "follow-up-cross-slash" },
                new SequenceStep { Attack = "offset-rising-slash" },
                new SequenceStep { Attack = "tackle" },
                new SequenceStep { Attack = "strong-charged-slash", Conditions = new() { ["stamina_full"] = false } },
                new SequenceStep { Attack = "true-charged-slash" },
            ]),
        new("full-release-loop", WeaponType.SwitchAxe, "Full Release loop",
            "Full Release Slash, Axe Spiral Burst Slash, two Sword Morph Double Slashes, after the loop described in the wiggler.pet Switch Axe guide. Edit it to your rotation.",
            [
                new SequenceStep { Attack = "full-release-slash" },
                new SequenceStep { Attack = "axe-spiral-burst-slash" },
                new SequenceStep { Attack = "sword-morph-double-slash", Repeat = 2 },
            ]),
        new("sword-uptime-loop", WeaponType.SwitchAxe, "Sword uptime into Full Release",
            "Amped sword combo twice, Full Release Slash, then Follow-up Heavy Slam into Follow-up Morph Slash back to sword.",
            [
                new SequenceStep { Attack = "amped-sword-combo", Repeat = 2 },
                new SequenceStep { Attack = "full-release-slash" },
                new SequenceStep { Attack = "axe-follow-up-morph" },
            ]),
    ];

    public static IReadOnlyList<AttackDefinition> For(WeaponType type) => All.Where(a => a.Type == type).ToList();

    public static AttackDefinition? Find(WeaponType type, string? id) =>
        id is null ? null : All.FirstOrDefault(a => a.Type == type && string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<SequencePreset> SequencePresetsFor(WeaponType type) => SequencePresets.Where(s => s.Type == type).ToList();

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
