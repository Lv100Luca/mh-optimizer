using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

public enum SwitchAxePhial { Power, Element }

/// <summary>
/// The kind of a bow arrow, which decides the shot skill that boosts it: Normal Shots (charged shots, Flying Swallow Shot),
/// Spread/Power Shots (Quick Shot, Power Shot, Power Volley), Piercing Shots and Special Ammo Boost (Dragon Piercer, Thousand
/// Dragons), Special Ammo Boost alone (Tracer). Other arrows (Arc Shot, Focus Strike, melee) get no shot skill.
/// </summary>
public enum BowShot { Normal, Power, Dragon, Tracer, Other }

/// <summary>Long Sword Spirit Gauge level: each one multiplies raw (<see cref="DamageConstants.SpiritGaugeRaw"/>).</summary>
public enum SpiritGauge { None, White, Yellow, Red }

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
    /// <summary>Bow arrow: the shot skill that boosts it; null for hits that are not bow arrows.</summary>
    public BowShot? Shot { get; init; }
    /// <summary>Bow arrow that takes the bow's coating (Close-range or Power multiply it, Pierce swaps in <see cref="Pierce"/>).</summary>
    public bool Coated { get; init; }
    /// <summary>Bow arrow with the Pierce coating: lower MV per hit, and it hits several times (<see cref="DamageConstants.PierceHitMultipliers"/>).</summary>
    public AttackHit? Pierce { get; init; }
    /// <summary>Bow arrow with a critical distance window: raw x<see cref="DamageConstants.CriticalDistanceRaw"/> inside it (every shot is assumed inside).</summary>
    public bool CriticalDistance { get; init; }
    /// <summary>
    /// Long Sword: the Spirit Gauge multiplier this hit always uses (Crimson moves only exist at red; Spirit Helm Breaker and Spirit
    /// Release Slash use a lower one); null: the gauge of the conditions (<see cref="Conditions.SpiritGauge"/>).
    /// </summary>
    public SpiritGauge? Spirit { get; init; }
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

/// <summary>The attacks the attack picker and the sequence editor offer (Great Sword, Long Sword, Switch Axe and Bow for now).</summary>
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

    // ---------------- Bow (1.040) ----------------
    /// <summary><paramref name="count"/> arrows of one shot that take the coating; with the Pierce coating each arrow is the pierce version.</summary>
    private static AttackHit Arrows(string name, BowShot shot, int count, double mv, double element, double pierceMv, double pierceElement) =>
        new(name, mv, element)
        {
            Count = count, Shot = shot, Coated = true, CriticalDistance = true,
            Pierce = new AttackHit(name + " (Pierce)", pierceMv, pierceElement) { Count = count, Shot = shot, Coated = true, CriticalDistance = true },
        };

    private static readonly AttackHit ChargedShot1 = Arrows("Charged Shot Lv1", BowShot.Normal, 1, 10, 0.4, 4, 0.2);
    private static readonly AttackHit ChargedShot2 = Arrows("Charged Shot Lv2", BowShot.Normal, 2, 11, 0.7, 5, 0.3);
    private static readonly AttackHit ChargedShot3 = Arrows("Charged Shot Lv3", BowShot.Normal, 3, 12, 1.0, 6, 0.4);
    private static readonly AttackHit QuickShot = Arrows("Quick Shot", BowShot.Power, 3, 10, 0.4, 4, 0.13);
    private static readonly AttackHit PowerShot2 = Arrows("Power Shot Lv2", BowShot.Power, 4, 11, 0.67, 5, 0.2);
    private static readonly AttackHit PowerShot3 = Arrows("Power Shot Lv3", BowShot.Power, 5, 12, 0.8, 6, 0.27);
    private static readonly AttackHit PowerVolley2 = Arrows("Power Volley Lv2", BowShot.Power, 5, 14, 0.87, 6, 0.27);
    private static readonly AttackHit PowerVolley3 = Arrows("Power Volley Lv3", BowShot.Power, 6, 15, 1.0, 7, 0.34);
    // Flying Swallow Shot fires the Power Shot arrows (3 / 4 / 5 by charge level) but counts as normal arrows for Normal Shots
    private static readonly AttackHit FlyingSwallow1 = Arrows("Flying Swallow Shot Lv1", BowShot.Normal, 3, 10, 0.4, 4, 0.13);
    private static readonly AttackHit FlyingSwallow2 = Arrows("Flying Swallow Shot Lv2", BowShot.Normal, 4, 11, 0.67, 5, 0.2);
    private static readonly AttackHit FlyingSwallow3 = Arrows("Flying Swallow Shot Lv3", BowShot.Normal, 5, 12, 0.8, 6, 0.27);
    private static readonly AttackHit ThousandDragons = Arrows("Thousand Dragons", BowShot.Dragon, 6, 25, 0.5, 13, 0.5);

    // ---------------- Long Sword (1.040) ----------------
    private static AttackHit Red(string name, double mv, double element = 1.0) => new(name, mv, element) { Spirit = SpiritGauge.Red };
    private static readonly AttackHit[] CrimsonSlash1 = [Red("Crimson Slash I 1", 33, 0.5), Red("Crimson Slash I 2", 16, 0.5)];
    private static readonly AttackHit LsRisingSlash = new("Rising Slash", 18);
    private static readonly AttackHit SpiritBlade1 = new("Spirit Blade I", 31);
    private static readonly AttackHit SpiritBlade2 = new("Spirit Blade II", 30);
    private static readonly AttackHit[] SpiritBlade3 = [new("Spirit Blade III 1", 14), new("Spirit Blade III 2", 19), new("Spirit Blade III 3", 34)];
    private static readonly AttackHit SpiritRoundslashRed = Red("Spirit Roundslash (Red)", 42);
    private static readonly AttackHit SpinningCrimsonSlash = Red("Spinning Crimson Slash", 60);
    private static readonly AttackHit SpiritThrust = new("Spirit Thrust", 19);
    private static readonly AttackHit HelmBreakerRed = new("Spirit Helm Breaker (Red)", 23, 0.2) { Count = 7, Spirit = SpiritGauge.Yellow };
    private static readonly AttackHit[] SpiritReleaseSlash =
    [
        new("Spirit Release Slash 1", 5) { Count = 2, Spirit = SpiritGauge.Yellow },
        new("Spirit Release Slash 2", 10, 0.1) { Count = 3, Spirit = SpiritGauge.White },
        new("Spirit Release Slash 3", 15, 0.1) { Count = 3, Spirit = SpiritGauge.White },
        new("Spirit Release Slash 4", 22, 0.1) { Count = 3, Spirit = SpiritGauge.White },
        new("Spirit Release Slash 5", 35, 0.1) { Count = 3, Spirit = SpiritGauge.White },
    ];
    private static readonly AttackHit[] IaiSpiritSlashRed = [Red("Iai Spirit Slash 1 (Red)", 39), Red("Iai Spirit Slash 2 (Red)", 86)];

    private const string GaugeNote = " Raw x the Spirit Gauge multiplier of the conditions (red x1.1).";

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
        new("sword-triple-slash", WeaponType.SwitchAxe, "Sword: Triple Slash", "All three slashes." + AmpedNote, 1.5,
            Amped(("Triple Slash 1", 25), ("Triple Slash 2", 22), ("Triple Slash 3", 26))),
        new("sword-counter-rising-slash-whiff", WeaponType.SwitchAxe, "Sword: Counter Rising Slash (no counter)", "The first slash when nothing is countered." + AmpedNote, 1,
            Amped(("Counter Rising Slash 1", 28))),
        new("sword-jumping-slash", WeaponType.SwitchAxe, "Sword: Jumping Slash", "One sword slash." + AmpedNote, 1, Amped(("Jumping Slash", 29))),
        new("sword-jumping-rising-slash", WeaponType.SwitchAxe, "Sword: Jumping Rising Slash", "One sword slash." + AmpedNote, 1, Amped(("Jumping Rising Slash", 29))),
        new("sword-scaling-double-slash", WeaponType.SwitchAxe, "Sword: Scaling Double Slash", "Both slashes." + AmpedNote, 1.2,
            Amped(("Scaling Double Slash 1", 31), ("Scaling Double Slash 2", 29))),
        new("elemental-discharge", WeaponType.SwitchAxe, "Elemental Discharge",
            "The opening hit and the full explosion (element x1, not boosted by the phial). The ticks in between are not counted: add Elemental Discharge tick steps for them.",
            3, [new AttackHit("Elemental Discharge Start", 25), new AttackHit("Elemental Discharge Explosion", 160)]),
        new("elemental-discharge-early", WeaponType.SwitchAxe, "Elemental Discharge (early explosion)",
            "The opening hit and the early explosion (element x1, not boosted by the phial). The ticks in between are not counted.",
            2, [new AttackHit("Elemental Discharge Start", 25), new AttackHit("Elemental Discharge Early Explosion", 75)]),
        new("elemental-discharge-tick", WeaponType.SwitchAxe, "Elemental Discharge tick", "One tick of the grinding phase (12 MV, element x1); repeat it for the ticks you land.",
            0.2, [new AttackHit("Elemental Discharge Tick", 12)]),
        new("zero-sum-discharge-early", WeaponType.SwitchAxe, "Zero Sum Discharge (early explosion)",
            "The early Zero Sum Discharge explosion (80 MV, element x1, not boosted by the phial).", 2, [new AttackHit("Zero Sum Discharge Early Explosion", 80)]),
        new("sword-morph-combination", WeaponType.SwitchAxe, "Sword: Focus Strike (Morph Combination)",
            "The opening hit, the extra hit and the explosion (100 MV, element x1, not boosted by the phial). The swings are not counted: add Focus Strike swing steps for them." + AmpedNote,
            3, [.. Amped(("Sword Morph Combination", 25), ("Sword Morph Combination Extra Hit", 25)), new AttackHit("Sword Morph Combination Explosion", 100)]),
        new("sword-morph-combination-swing", WeaponType.SwitchAxe, "Sword: Focus Strike swing", "One swing of the sword Focus Strike (35 MV); repeat it for the swings you land." + AmpedNote,
            0.3, Amped(("Sword Morph Combination Swing", 35))),
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
        new("axe-forward-slash", WeaponType.SwitchAxe, "Axe: Forward Slash", "Axe mode: no phial boost.", 1, [new AttackHit("Axe Forward Slash", 35)]),
        new("axe-forward-overhead-slash", WeaponType.SwitchAxe, "Axe: Forward Overhead Slash", "Axe mode: no phial boost.", 1.2, [new AttackHit("Axe Forward Overhead Slash", 45)]),
        new("axe-fade-slash", WeaponType.SwitchAxe, "Axe: Fade Slash", "Axe mode: no phial boost.", 1.2, [new AttackHit("Axe Fade Slash", 42)]),
        new("axe-offset-rising-slash", WeaponType.SwitchAxe, "Axe: Offset Rising Slash", "Axe mode: no phial boost.", 1.2, [new AttackHit("Axe Offset Rising Slash", 50)]),
        new("axe-wild-swing", WeaponType.SwitchAxe, "Axe: Wild Swing (left and right)", "One left and one right swing. Axe mode: no phial boost.", 1,
            [new AttackHit("Axe Wild Slash Left", 23), new AttackHit("Axe Wild Slash Right", 21)]),
        new("axe-jumping-slash", WeaponType.SwitchAxe, "Axe: Jumping Slash", "Axe mode: no phial boost.", 1, [new AttackHit("Axe Jumping Slash", 45)]),
        new("axe-jumping-sweep", WeaponType.SwitchAxe, "Axe: Jumping Sweep / Scaling Wide Slash", "Axe mode: no phial boost.", 1, [new AttackHit("Axe Jumping Sweep", 45)]),
        new("axe-morph-combination", WeaponType.SwitchAxe, "Axe: Focus Strike (Morph Combination)",
            "The opening hit, the extra hit and both finisher hits; activates Power Axe (not counted). The swings are not counted: add Focus Strike swing steps for them. Axe mode: no phial boost.",
            3, [new AttackHit("Axe Morph Combination", 15), new AttackHit("Axe Morph Combination Extra Hit", 15),
                new AttackHit("Axe Morph Combination Finisher 1", 20), new AttackHit("Axe Morph Combination Finisher 2", 46)]),
        new("axe-morph-combination-swing", WeaponType.SwitchAxe, "Axe: Focus Strike swing", "One swing of the axe Focus Strike (20 MV); repeat it for the swings you land. Axe mode: no phial boost.",
            0.3, [new AttackHit("Axe Morph Combination Swing", 20)]),
        // Morphs: the gauge a hit fills in notes/motion_values.md (switch gauge in axe mode, amped gauge in sword mode) gives the mode it lands in
        new("axe-morph-slash", WeaponType.SwitchAxe, "Axe: Morph Slash", "Morphs to sword; the slash lands in sword mode." + AmpedNote, 1,
            Amped(("Axe Morph Slash", 34))),
        new("axe-morph-rising-double-slash", WeaponType.SwitchAxe, "Axe: Morph Rising Double Slash", "Both slashes, morphing to sword; they land in sword mode." + AmpedNote, 1.5,
            Amped(("Axe Morph Rising Double Slash 1", 37), ("Axe Morph Rising Double Slash 2", 33))),
        new("sword-morph-slash", WeaponType.SwitchAxe, "Sword: Morph Slash", "Morphs to axe; the slash lands in axe mode (no phial boost).", 1, [new AttackHit("Sword Morph Slash", 23)]),
        new("sword-advancing-morph-slash", WeaponType.SwitchAxe, "Sword: Advancing Morph Slash", "Morphs to axe; the slash lands in axe mode (no phial boost).", 1.2,
            [new AttackHit("Sword Advancing Morph Slash", 55)]),
        new("sword-overhead-morph-slash", WeaponType.SwitchAxe, "Sword: Overhead Morph Slash", "Morphs to axe; the slash lands in axe mode (no phial boost).", 1.2,
            [new AttackHit("Sword Overhead Morph Slash", 45)]),
        new("sword-downward-fade-slash", WeaponType.SwitchAxe, "Sword: Downward Fade Slash", "Morphs to axe; the slash lands in axe mode (no phial boost).", 1.2,
            [new AttackHit("Sword Downward Fade Slash", 55)]),
        // Switch Axe moves: mount, Seikret and dismount (counted without phial boost or amped explosions)
        new("mount-attack", WeaponType.SwitchAxe, "Mount Attack", "One mounted stab (35 MV); repeat it for the stabs you land.", 0.5, [new AttackHit("Mount Attack", 35)]),
        new("mount-finisher", WeaponType.SwitchAxe, "Mount Finisher", "The finisher slash and its explosion (element x1). The multihits are not counted: add Mount Finisher multihit steps for them.",
            3, [new AttackHit("Mount Finisher", 60), new AttackHit("Mount Finisher Explosion", 80)]),
        new("mount-finisher-multihit", WeaponType.SwitchAxe, "Mount Finisher multihit", "One multihit of the mount finisher (12 MV, element x0.5); repeat it for the hits you land.",
            0.2, [new AttackHit("Mount Finisher Multihit", 12, 0.5)]),
        new("seikret-attack", WeaponType.SwitchAxe, "Seikret Attack", "The opening and finishing hits from the Seikret. The swings are not counted: add Seikret swing steps for them.",
            2, [new AttackHit("Seikret Attack Start", 10), new AttackHit("Seikret Attack Finisher", 20)]),
        new("seikret-attack-swing", WeaponType.SwitchAxe, "Seikret Attack swing", "One swing from the Seikret (10 MV); repeat it for the swings you land.",
            0.3, [new AttackHit("Seikret Attack Swing", 10)]),
        new("dismount-attack", WeaponType.SwitchAxe, "Dismount Attack", "Both hits of the attack off the Seikret.", 1.5,
            [new AttackHit("Dismount Attack 1", 10), new AttackHit("Dismount Attack 2", 25)]),
        // Switch Axe combos
        new("amped-sword-combo", WeaponType.SwitchAxe, "Amped sword combo",
            "Overhead Slash, Left and Right Rising Slash, Heavenward Flurry while amped." + AmpedNote,
            5, Amped(("Overhead Slash", 40), ("Left Rising Slash", 62), ("Right Rising Slash", 58), ("Heavenward Flurry 1", 34), ("Heavenward Flurry 2", 46))) { Combo = true },
        new("axe-follow-up-morph", WeaponType.SwitchAxe, "Heavy Slam into Follow-up Morph Slash",
            "Axe Follow-up Heavy Slam, then Follow-up Morph Slash and its explosion (element x0.8 with an Element phial). Axe mode: no phial boost; Power Axe is not counted.",
            4, [new AttackHit("Axe Follow-up Heavy Slam 1", 32), new AttackHit("Axe Follow-up Heavy Slam 2", 90),
                new AttackHit("Axe Follow-up Morph Slash", 123), Explosion("Follow-up Morph Slash Explosion", 120, 0.8)]) { Combo = true },

        // Long Sword moves (seconds are rough)
        new("ls-overhead-slash", WeaponType.LongSword, "Overhead Slash", "The first overhead slash (left click / Triangle); at a red gauge it is Crimson Slash I instead." + GaugeNote, 0.9,
            [new AttackHit("Overhead Slash I", 24)]),
        new("ls-overhead-slash-ii", WeaponType.LongSword, "Overhead Slash II", "The second overhead slash." + GaugeNote, 0.9, [new AttackHit("Overhead Slash II", 18)]),
        new("ls-crescent-slash", WeaponType.LongSword, "Crescent Slash", "The third overhead slash." + GaugeNote, 1, [new AttackHit("Crescent Slash", 32)]),
        new("ls-thrust", WeaponType.LongSword, "Thrust", "A thrust (right click / Circle)." + GaugeNote, 0.8, [new AttackHit("Thrust", 18)]),
        new("ls-rising-slash", WeaponType.LongSword, "Rising Slash", "The rising slash after a thrust or a Crimson Slash (right click / Circle)." + GaugeNote, 0.8, [LsRisingSlash]),
        new("ls-fade-slash", WeaponType.LongSword, "Fade Slash", "A backward fade slash." + GaugeNote, 1.2, [new AttackHit("Fade Slash", 22)]),
        new("crimson-slash-i", WeaponType.LongSword, "Crimson Slash I", "Both hits (element x0.5). Only at a red gauge, so always at the red multiplier (raw x1.1).", 1.1, CrimsonSlash1),
        new("crimson-slash-ii", WeaponType.LongSword, "Crimson Slash II", "Both hits (element x1.05). Only at a red gauge, so always at the red multiplier (raw x1.1).", 1,
            [Red("Crimson Slash II 1", 11, 1.05), Red("Crimson Slash II 2", 22, 1.05)]),
        new("crimson-slash-iii", WeaponType.LongSword, "Crimson Slash III", "All three hits (element x1.1). Only at a red gauge, so always at the red multiplier (raw x1.1).", 1.6,
            [Red("Crimson Slash III 1", 30, 1.1), Red("Crimson Slash III 2", 18, 1.1), Red("Crimson Slash III 3", 55, 1.1)]),
        new("spirit-blade-i", WeaponType.LongSword, "Spirit Blade I", "With gauge (R / R2)." + GaugeNote, 0.9, [SpiritBlade1]),
        new("spirit-blade-ii", WeaponType.LongSword, "Spirit Blade II", "With gauge." + GaugeNote, 0.9, [SpiritBlade2]),
        new("spirit-blade-iii", WeaponType.LongSword, "Spirit Blade III", "All three hits, with gauge." + GaugeNote, 1.4, SpiritBlade3),
        new("spirit-roundslash", WeaponType.LongSword, "Spirit Roundslash", "With gauge; raises the gauge one level." + GaugeNote, 1.5, [new AttackHit("Spirit Roundslash", 38)]),
        new("spirit-roundslash-red", WeaponType.LongSword, "Spirit Roundslash (red)", "The red-gauge version (42 MV), at the red multiplier.", 1.5, [SpiritRoundslashRed]),
        new("spinning-crimson-slash", WeaponType.LongSword, "Spinning Crimson Slash", "The follow-up to a red Spirit Roundslash (60 MV), at the red multiplier.", 1.5, [SpinningCrimsonSlash]),
        new("spirit-thrust", WeaponType.LongSword, "Spirit Thrust", "The thrust into Spirit Helm Breaker." + GaugeNote, 0.8, [SpiritThrust]),
        new("spirit-helm-breaker-red", WeaponType.LongSword, "Spirit Helm Breaker (red)",
            "Seven hits of 23 MV (element x0.2) at a red gauge; drops the gauge a level and uses the yellow multiplier (raw x1.04).", 3, [HelmBreakerRed]),
        new("spirit-helm-breaker-yellow", WeaponType.LongSword, "Spirit Helm Breaker (yellow)",
            "Seven hits of 20 MV (element x0.2) at a yellow gauge; uses the white multiplier (raw x1.02).", 3,
            [new AttackHit("Spirit Helm Breaker (Yellow)", 20, 0.2) { Count = 7, Spirit = SpiritGauge.White }]),
        new("spirit-helm-breaker-white", WeaponType.LongSword, "Spirit Helm Breaker (white)",
            "Seven hits of 15 MV (element x0.2) at a white gauge; no gauge multiplier.", 3,
            [new AttackHit("Spirit Helm Breaker (White)", 15, 0.2) { Count = 7, Spirit = SpiritGauge.None }]),
        new("spirit-release-slash", WeaponType.LongSword, "Spirit Release Slash",
            "Every hit of the follow-up to a red Helm Breaker (14 hits; element x0.1 after the first two), at the yellow and white multipliers.", 3.5, SpiritReleaseSlash),
        new("iai-slash", WeaponType.LongSword, "Iai Slash", "Both hits of the draw from Special Sheathe." + GaugeNote, 1.2,
            [new AttackHit("Iai Slash 1", 18), new AttackHit("Iai Slash 2", 13)]),
        new("iai-spirit-slash-red", WeaponType.LongSword, "Iai Spirit Slash (red, countered)",
            "Both hits of a successful counter from Special Sheathe at a red gauge (the second only on a counter), at the red multiplier.", 2, IaiSpiritSlashRed),
        new("iai-spirit-slash-yellow", WeaponType.LongSword, "Iai Spirit Slash (yellow, countered)",
            "Both hits of a successful counter at a yellow gauge; it raises the gauge to red. At the yellow multiplier (raw x1.04).", 2,
            [new AttackHit("Iai Spirit Slash 1 (Yellow)", 31) { Spirit = SpiritGauge.Yellow }, new AttackHit("Iai Spirit Slash 2 (Yellow)", 72) { Spirit = SpiritGauge.Yellow }]),
        new("foresight-slash", WeaponType.LongSword, "Foresight Slash into Whirl Slash (countered)",
            "A successful Foresight Slash (x1.3: 23.4 MV) and both hits of the successful Foresight Whirl Slash; costs the gauge." + GaugeNote, 2.5,
            [new AttackHit("Foresight Slash (Success)", 23.4), new AttackHit("Foresight Whirl Slash 1 (Success)", 28.6), new AttackHit("Foresight Whirl Slash 2 (Success)", 16.9)]),
        new("unbound-thrust", WeaponType.LongSword, "Focus Strike: Unbound Thrust",
            "Every hit, the last one on a wound (once per wound, raises the gauge a level)." + GaugeNote, 3,
            [new AttackHit("Unbound Thrust 1", 26), new AttackHit("Unbound Thrust Extra Hit", 26), new AttackHit("Unbound Thrust 2", 10),
             new AttackHit("Unbound Thrust 3", 40), new AttackHit("Unbound Thrust 4", 40), new AttackHit("Unbound Thrust 5", 40)]),
        // Long Sword combos
        new("crimson-rising-loop", WeaponType.LongSword, "Crimson Slash into Rising Slash",
            "The steady red-gauge loop (left click, right click): Crimson Slash I (raw x1.1) and a Rising Slash at the gauge of the conditions; it can cancel into Foresight Slash.",
            1.9, [.. CrimsonSlash1, LsRisingSlash]) { Combo = true },
        new("crimson-spirit-loop", WeaponType.LongSword, "Crimson Slash into Spirit Blade I",
            "The harder-hitting red-gauge loop (left click, R): Crimson Slash I (raw x1.1) and Spirit Blade I at the gauge of the conditions; slower to cancel into a counter.",
            2, [.. CrimsonSlash1, SpiritBlade1]) { Combo = true },
        new("red-spirit-combo", WeaponType.LongSword, "Spirit Blade combo into Spinning Crimson Slash",
            "Spirit Blade I, II and III, the red Spirit Roundslash and Spinning Crimson Slash." + GaugeNote, 6,
            [SpiritBlade1, SpiritBlade2, .. SpiritBlade3, SpiritRoundslashRed, SpinningCrimsonSlash]) { Combo = true },
        new("helm-breaker-release", WeaponType.LongSword, "Spirit Thrust, Helm Breaker and Release Slash",
            "The red-gauge finisher: Spirit Thrust, Spirit Helm Breaker (yellow multiplier) and Spirit Release Slash (yellow and white); drops the gauge two levels.", 7,
            [SpiritThrust, HelmBreakerRed, .. SpiritReleaseSlash]) { Combo = true },

        // Bow moves: every arrow of a shot is one hit. Charge levels as the game counts them internally: Lv1 (no glow) to Lv3.
        // Power Shot and Power Volley fire one level above the charge they follow (capped at Lv3): Lv2 after a Lv1 shot.
        new("charged-shot-1", WeaponType.Bow, "Charged Shot Lv1", "One arrow of 10 MV (element x0.4)." + ArrowNote, 0.8, [ChargedShot1]),
        new("charged-shot-2", WeaponType.Bow, "Charged Shot Lv2", "Two arrows of 11 MV (element x0.7 each)." + ArrowNote, 0.8, [ChargedShot2]),
        new("charged-shot-3", WeaponType.Bow, "Charged Shot Lv3", "Three arrows of 12 MV (element x1 each)." + ArrowNote, 0.8, [ChargedShot3]),
        new("quick-shot", WeaponType.Bow, "Quick Shot", "The Power Shot without a shot before it: three arrows of 10 MV (element x0.4)." + ArrowNote, 0.7, [QuickShot]),
        new("power-shot-2", WeaponType.Bow, "Power Shot Lv2", "After a Lv1 shot: four arrows of 11 MV (element x0.67), fanned out, so stay close for all to hit." + ArrowNote, 0.75, [PowerShot2]),
        new("power-shot-3", WeaponType.Bow, "Power Shot Lv3", "After a Lv2 or Lv3 shot: five arrows of 12 MV (element x0.8), fanned out, so stay close for all to hit." + ArrowNote, 0.75, [PowerShot3]),
        new("power-volley-2", WeaponType.Bow, "Power Volley Lv2", "After a Power Shot Lv2: five arrows of 14 MV (element x0.87)." + ArrowNote, 1.6, [PowerVolley2]),
        new("power-volley-3", WeaponType.Bow, "Power Volley Lv3", "After a Power Shot Lv3: six arrows of 15 MV (element x1)." + ArrowNote, 1.6, [PowerVolley3]),
        new("flying-swallow-shot-1", WeaponType.Bow, "Flying Swallow Shot Lv1", "After a Charging Sidestep: three arrows of 10 MV (element x0.4). Normal Shots boosts it." + ArrowNote, 1.2, [FlyingSwallow1]),
        new("flying-swallow-shot-2", WeaponType.Bow, "Flying Swallow Shot Lv2", "After a Charging Sidestep: four arrows of 11 MV (element x0.67). Normal Shots boosts it." + ArrowNote, 1.2, [FlyingSwallow2]),
        new("flying-swallow-shot-3", WeaponType.Bow, "Flying Swallow Shot Lv3", "After a Charging Sidestep: five arrows of 12 MV (element x0.8). Normal Shots boosts it." + ArrowNote, 1.2, [FlyingSwallow3]),
        new("thousand-dragons", WeaponType.Bow, "Thousand Dragons", "Six arrows of 25 MV (element x0.5) at point blank (critical distance only to 10 m); costs stamina, no gauge. Piercing Shots and Special Ammo Boost boost it." + ArrowNote, 2.0, [ThousandDragons]),
        // Bow combos (loop times from the Math Hunter calculator presets)
        new("dash-dance", WeaponType.Bow, "Dash dance: Charged Shot, Power Shot, Power Volley",
            "The standard loop at full charge: Charging Sidestep, Charged Shot Lv3, Power Shot Lv3, Power Volley Lv3 (14 arrows, about 3.1 s); the sidestep keeps the charge. All arrows are assumed to hit (stay close for the fanned-out Power Shot and Volley)." + ArrowNote,
            3.11, [ChargedShot3, PowerShot3, PowerVolley3]) { Combo = true },
        new("shot-power-shot", WeaponType.Bow, "Charged Shot into Power Shot",
            "The short-window loop: Charging Sidestep, Charged Shot Lv3, Power Shot Lv3, skipping the Power Volley's long recovery." + ArrowNote,
            1.5, [ChargedShot3, PowerShot3]) { Combo = true },
        new("ramp-up", WeaponType.Bow, "Ramp-up from no charge",
            "Charged Shot Lv1, Charged Shot Lv2, Power Shot Lv3, Power Volley Lv3: every shot raises the next one's charge (a Discerning Dodge skips this, going straight to Lv3)." + ArrowNote,
            3.94, [ChargedShot1, ChargedShot2, PowerShot3, PowerVolley3]) { Combo = true },
    ];

    private const string ArrowNote = " Raw x1.1 at critical distance; Close-range (x1.4) and Power (x1.35) coatings multiply the raw, with the Pierce coating every arrow is a weaker pierce arrow that hits several times.";

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
        new("ten-shot-combo", WeaponType.Bow, "Ten-shot combo",
            "Ramp-up from no charge, then the dash dance twice: 10 shots, 42 arrows. The benchmark combo of community bow build comparisons (Math Hunter, Grand Sky Ace).",
            [
                new SequenceStep { Attack = "ramp-up" },
                new SequenceStep { Attack = "dash-dance", Repeat = 2 },
            ]),
        new("dash-dance-thousand-dragons", WeaponType.Bow, "Dash dance into Thousand Dragons",
            "A burst on a downed monster: the dash dance, then Thousand Dragons at point blank (not a charged attack, so no Charge Master).",
            [
                new SequenceStep { Attack = "dash-dance" },
                new SequenceStep { Attack = "thousand-dragons", Conditions = new() { ["charged_attack"] = false } },
            ]),
        new("iai-counter-crimson-loop", WeaponType.LongSword, "Iai counter into Crimson Slash loop",
            "A red-gauge Iai Spirit Slash counter (the parry), then Crimson Slash into Rising Slash four times. Timings are rough; edit it to your rotation.",
            [
                new SequenceStep { Attack = "iai-spirit-slash-red" },
                new SequenceStep { Attack = "crimson-rising-loop", Repeat = 4 },
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

    /// <summary>"Offset Rising Slash Lv3 + Tackle x2 + ...": the steps of a sequence by attack name (steps of unknown attacks or no repeat are left out).</summary>
    public static string StepList(WeaponType type, IEnumerable<SequenceStep> steps) =>
        string.Join(" + ", steps.Select(s => (Step: s, Def: Find(type, s.Attack))).Where(x => x.Def is not null && x.Step.Repeat > 0)
            .Select(x => x.Def!.Name + (x.Step.Repeat > 1 ? $" x{x.Step.Repeat}" : "")));

    /// <summary>The attack a weapon type is scored with when the profile names none; null means the average-hit model.</summary>
    public static string? DefaultFor(WeaponType type) => type switch
    {
        WeaponType.GreatSword => "true-charged-slash",
        WeaponType.SwitchAxe => "full-release-slash",
        WeaponType.Bow => "dash-dance",
        WeaponType.LongSword => "crimson-rising-loop",
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
