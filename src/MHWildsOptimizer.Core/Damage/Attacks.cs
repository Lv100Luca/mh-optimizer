using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

public enum SwitchAxePhial { Power, Element }

public enum ChargeBladePhial { Impact, Element }

public enum GunlanceShelling { Normal, Long, Wide }

/// <summary>A Gunlance shelling hit; its values come from the weapon's shelling type and level (<see cref="DamageConstants.Shell"/>).</summary>
public enum GunlanceShot { Shell, FullburstShell, WyrmstakeFullburstShell, ChargedShell, Wyvernfire, WyrmstakeExplosion, WyrmstakeTick }

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
/// motion value datamine for game version 1.040 (notes/motion_values.md). A hit can crit unless <see cref="NoCrit"/>; it uses the
/// weapon's sharpness unless it has a <see cref="FixedSharpness"/>.
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
    /// <summary>The hit never crits, and is never a feeble hit (shells, phial bursts, soundwaves).</summary>
    public bool NoCrit { get; init; }
    /// <summary>The hit's raw ignores the target's hitzone (acts as hitzone 100); its element still uses the element hitzone.</summary>
    public bool IgnoresHitzone { get; init; }
    /// <summary>Gunlance shelling hit: <see cref="Mv"/> and <see cref="Element"/> are replaced by the weapon's shelling values.</summary>
    public GunlanceShot? Shell { get; init; }
    /// <summary>Charge Blade phial burst: <see cref="Mv"/> is its raw with Impact phials; with Element phials it deals no raw and this element modifier.</summary>
    public double? PhialBurstElement { get; init; }
    /// <summary>The hit ignores sharpness: raw and element x1.</summary>
    public bool IgnoresSharpness { get; init; }
    /// <summary>Fixed element damage at the target's hitzone for that element, independent of the weapon and skills (Hunting Horn Echo Wave).</summary>
    public FixedElementDamage? FixedElement { get; init; }
}

/// <summary>Element damage of a fixed <paramref name="Value"/> (true element) at the target's <paramref name="Element"/> hitzone.</summary>
public readonly record struct FixedElementDamage(Element Element, double Value);

/// <summary>A single input (<see cref="Combo"/> false) or a short combo of inputs the score can be computed for.</summary>
/// <param name="Seconds">Rough time one execution takes in a steady stream of them; sets the hits per minute for cooldown-limited procs.</param>
public sealed record AttackDefinition(string Id, WeaponType Type, string Name, string Description, double Seconds, IReadOnlyList<AttackHit> Hits)
{
    public bool Combo { get; init; }
    public int HitCount => Hits.Sum(h => h.Count);
}

/// <summary>A ready-made attack sequence to start from (the sequence editor loads its steps).</summary>
public sealed record SequencePreset(string Id, WeaponType Type, string Name, string Description, IReadOnlyList<SequenceStep> Steps);

/// <summary>The attacks the attack picker and the sequence editor offer (every weapon type but the bowguns).</summary>
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

    // Left out of the attacks below: hitzone-ignoring sneak attacks, kinsect attacks, clash and underwater attacks.
    // Blunt hits of cutting weapons are counted at the slash hitzone.
    private const string ShieldNote = " Shield hit: blunt damage, counted at the slash hitzone; no element.";

    // ---------------- Sword & Shield (1.040) ----------------
    private static readonly AttackHit[] PerfectRush =
    [
        new("Perfect Rush I 1", 10, 0), new("Perfect Rush I 2", 15), new("Perfect Rush I 3", 15),
        new("Perfect Rush II", 20, 0), new("Perfect Rush III", 30), new("Perfect Rush III Multihit", 18) { Count = 3 },
    ];
    private static readonly AttackHit[] PowerPerfectRush =
    [
        new("Power Perfect Rush I 1", 40, 0), new("Power Perfect Rush I 2", 40, 1.5), new("Power Perfect Rush I 3", 45, 1.5),
        new("Power Perfect Rush II", 60, 0), new("Power Perfect Rush III", 80, 1.5), new("Power Perfect Rush III Multihit", 34) { Count = 3 },
    ];

    // ---------------- Dual Blades (1.040) ----------------
    private static readonly AttackHit[] DemonFangs = [new("Demon Fangs", 14, 0.6)];
    private static readonly AttackHit[] TwofoldDemonSlash = [new("Twofold Demon Slash 1", 15, 0.6), new("Twofold Demon Slash 2", 7, 0.6)];
    private static readonly AttackHit[] SixfoldDemonSlash =
    [
        new("Sixfold Demon Slash 1", 13, 0.6), new("Sixfold Demon Slash 2", 13, 0.6), new("Sixfold Demon Slash 3", 9, 0.6),
        new("Sixfold Demon Slash 4", 9, 0.6), new("Sixfold Demon Slash 5", 18, 0.6), new("Sixfold Demon Slash 6", 18, 0.6),
    ];
    private static readonly AttackHit[] BladeDance1 =
    [
        new("Blade Dance I 1", 18), new("Blade Dance I 2", 18), new("Blade Dance I 3", 6, 0.6), new("Blade Dance I 4", 6, 0.6),
        new("Blade Dance I 5", 10, 0.6), new("Blade Dance I 6", 10, 0.6), new("Blade Dance I 7", 4, 0.8), new("Blade Dance I 8", 4, 0.8),
        new("Blade Dance I 9", 20, 0.8), new("Blade Dance I 10", 20, 0.8), new("Blade Dance I 11", 11, 0.8), new("Blade Dance I 12", 11, 0.8),
    ];
    private static readonly AttackHit[] BladeDance2 =
        [new("Blade Dance II 1", 16, 0.6), new("Blade Dance II 2", 16, 0.6), new("Blade Dance II 3", 6, 0.8), new("Blade Dance II 4", 25, 0.8)];
    private static readonly AttackHit[] BladeDance3 =
    [
        new("Blade Dance III 1", 22, 0.6), new("Blade Dance III 2", 8, 0.6), new("Blade Dance III 3", 5, 0.6), new("Blade Dance III 4", 22, 0.6),
        new("Blade Dance III 5", 5, 0.8), new("Blade Dance III 6", 14, 0.8), new("Blade Dance III 7", 18, 0.8), new("Blade Dance III 8", 8, 0.8),
        new("Blade Dance III 9", 18, 0.8), new("Blade Dance III 10", 36, 0.8), new("Blade Dance III 11", 36, 0.8),
    ];

    // ---------------- Hammer (1.040) ----------------
    private static readonly AttackHit[] MightyChargeSlam =
        [new("Mighty Charge Slam 1", 20), new("Mighty Charge Slam 2", 20), new("Mighty Charge Slam 3", 200, 1.5)];
    private static readonly AttackHit[] FollowUpSpinslam = [new("Follow-up Spinslam 1", 50), new("Follow-up Spinslam 2", 210, 1.5)];

    // ---------------- Hunting Horn (1.040) ----------------
    private const string SongNote = " Song effects are not counted; soundwaves and echo hits are their own attacks.";
    private const string WaveNote = " Cannot crit, ignores sharpness and the hitzone.";
    private const string EchoWaveNote = " Cannot crit, ignores sharpness; it fires on an Encore, its level is the number of Echo Wave melodies played before it.";
    private const string PerformNote = " The shockwave (27 MV, when a melody completes) ignores sharpness and the hitzone and cannot crit." + SongNote;
    // dtlnor Wp05 shells: soundwaves, echo bubbles and echo waves have IsNoUseKireaji and (all but the slash wave) IsNoCritical.
    // Shockwaves and echo bubble hits also ignore the hitzone (kuroyonhon.com/mhwilds/memo/8.php); for echo waves no source says so.
    private static AttackHit Wave(string name, double mv, double element = 1.0) =>
        new(name, mv, element) { NoCrit = true, IgnoresSharpness = true, IgnoresHitzone = true };
    private static AttackHit[] Perform(string name, double mv) => [new(name, mv), Wave($"{name} Shockwave", 27)];
    /// <summary>Echo Wave levels: blunt and slash MV, elemental MV and its fixed element value (dtlnor Wp05 Shell 100-120).</summary>
    private static readonly (int Level, double Blunt, double ElementMv, double ElementValue)[] EchoWaveLevels = [(1, 50, 45, 60), (2, 70, 65, 100), (3, 100, 85, 140)];
    private static readonly Element[] EchoElements = [Element.Fire, Element.Water, Element.Thunder, Element.Ice, Element.Dragon];
    /// <summary>Resounding Melody soundwaves: raw x1 on the first two, then x0.925, 0.85, 0.65, 0.588, 0.525, 0.463, 0.4, 0.25.</summary>
    private static readonly double[] ResoundingDecay = [1, 1, 0.925, 0.85, 0.65, 0.588, 0.525, 0.463, 0.4, 0.25];
    private static readonly AttackHit[] PowerPerformanceBeat = [.. Perform("Power Performance Beat I", 40), .. Perform("Power Performance Beat II", 50)];
    private static readonly AttackHit[] PowerEncore = [new("Encore 1", 20), new("Power Encore 2", 75)];

    // ---------------- Lance (1.040) ----------------
    private static readonly AttackHit[] MidThrusts = [new("Mid Thrust I", 26, 1.2), new("Mid Thrust II", 26, 1.2), new("Mid Thrust III", 35, 1.4)];
    private static readonly AttackHit[] HighThrusts = [new("High Thrust I", 31), new("High Thrust II", 31), new("High Thrust III", 42)];
    /// <summary>Charge Counter and Double Counter-thrust by charge level: Lv1..3 are the Lv0 MVs x1.2 / 1.4 / 1.7.</summary>
    private static readonly (string Level, double Counter, double Thrust1, double Thrust2)[] LanceCounters =
        [("Lv0", 43, 33, 50), ("Lv1", 51.6, 39.6, 60), ("Lv2", 60.2, 46.2, 70), ("Lv3", 73.1, 56.1, 85)];
    /// <summary>Power Guard aura levels: the power counters are the base MVs x1.1 (white), x1.3 (yellow), x1.7 (red).</summary>
    private static readonly (int Level, string Aura)[] LanceAuras = [(1, "white"), (2, "yellow"), (3, "red")];

    // ---------------- Gunlance (1.040) ----------------
    private const string ShellNote = " Shells are their own attacks.";
    private const string ShellingNote =
        " The values follow the shelling type (Gogma focus: attack Normal, affinity Long, element Wide) at Lv3; the raw ignores sharpness and the hitzone, cannot crit, and the fire is fixed.";
    /// <summary>A shelling hit; its MV here is the Normal Lv3 one, the weapon's shelling replaces it (<see cref="DamageConstants.Shell"/>).</summary>
    private static AttackHit ShellHit(string name, GunlanceShot shot, int count = 1) =>
        new(name, DamageConstants.Shell(GunlanceShelling.Normal, shot, DamageConstants.ArtianShellLevel).Mv, 0) { Shell = shot, Count = count };
    private static readonly AttackHit Shell = ShellHit("Shell", GunlanceShot.Shell);

    // ---------------- Charge Blade (1.040) ----------------
    private const string PhialNote =
        " Phial bursts follow the phial (Gogma focus: attack Impact, affinity and element Element): Impact raw, or Element element only; they cannot crit and hit at yellow sharpness.";
    /// <summary>A phial burst: <paramref name="impactMv"/> raw with Impact phials, <paramref name="element"/> element only with Element phials.</summary>
    private static AttackHit PhialBurst(string name, double impactMv, double element, int count = 1) =>
        new(name, impactMv, 0) { PhialBurstElement = element, NoCrit = true, FixedSharpness = SharpnessColor.Yellow, Count = count };
    private static readonly AttackHit[] Aed = [new("Axe Amped Elemental Discharge", 68), PhialBurst("Phial Burst AED", 16, 4)];
    private static readonly AttackHit[] AedFollowUp = [new("Axe Amped Elemental Discharge Follow-up", 78), PhialBurst("Phial Burst AED", 16, 4)];
    private static readonly AttackHit[] SaedSwings =
        [new("Axe Super Amped Elemental Discharge 1", 16), new("Axe Super Amped Elemental Discharge 2", 78), new("Axe Super Amped Elemental Discharge 3", 45)];

    // ---------------- Insect Glaive (1.040) ----------------
    private const string ExtractNote = " Extract buffs and kinsect attacks are not counted.";
    private static readonly AttackHit[] StrongRisingSlashCombo =
        [new("Strong Rising Slash Combo 1", 24, 0.8), new("Strong Rising Slash Combo 2", 12, 0.8), new("Strong Rising Slash Combo 3", 24, 0.8)];
    private static readonly AttackHit[] StrongReapingSlash = [new("Strong Reaping Slash 1", 18, 0.8), new("Strong Reaping Slash 2", 28, 0.8)];
    private static readonly AttackHit[] StrongDoubleSlash =
        [new("Strong Double Slash 1", 26, 0.8), new("Strong Double Slash 2", 18, 0.8), new("Strong Double Slash 3", 40)];
    private static readonly AttackHit[] TornadoSlash = [new("Tornado Slash 1", 22), new("Tornado Slash 2", 48)];
    /// <summary>Descending Slash Lv2 hits; Lv1 and Lv0 are x0.75 and x0.6 of them.</summary>
    private static readonly double[] DescendingSlash = [20, 12, 16, 32];
    private static readonly double[] StrongDescendingSlash = [32, 12, 18, 18, 18, 46];
    private static readonly (int Level, double Scale)[] GlaiveLevels = [(0, 0.6), (1, 0.75), (2, 1.0)];
    /// <summary>Rising Spiral Slash: one hit of 8 (Lv2 64 MV, Lv1 48 MV).</summary>
    private static AttackHit RisingSpiralSlash(int level) => new($"Rising Spiral Slash Lv{level}", level == 2 ? 64 : 48, 0.8) { Count = 8 };

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

        // Sword & Shield moves (seconds are rough)
        new("advancing-slash", WeaponType.SwordAndShield, "Advancing Slash", "A lunging slash.", 0.8, [new AttackHit("Advancing Slash", 28)]),
        new("rising-slash", WeaponType.SwordAndShield, "Rising Slash", "A rising slash.", 0.6, [new AttackHit("Rising Slash", 18)]),
        new("chop", WeaponType.SwordAndShield, "Chop", "A chop (element x1.3).", 0.6, [new AttackHit("Chop", 18, 1.3)]),
        new("side-slash", WeaponType.SwordAndShield, "Side Slash", "A side slash (element x1.3).", 0.6, [new AttackHit("Side Slash", 17, 1.3)]),
        new("diagonal-rising-slash", WeaponType.SwordAndShield, "Diagonal Rising Slash", "Element x1.3.", 0.7, [new AttackHit("Diagonal Rising Slash", 24, 1.3)]),
        new("diagonal-chop", WeaponType.SwordAndShield, "Diagonal Chop", "Element x1.3.", 0.7, [new AttackHit("Diagonal Chop", 27, 1.3)]),
        new("lateral-slash", WeaponType.SwordAndShield, "Lateral Slash", "Element x1.2.", 0.7, [new AttackHit("Lateral Slash", 27, 1.2)]),
        new("return-stroke", WeaponType.SwordAndShield, "Return Stroke", "Element x1.2.", 0.7, [new AttackHit("Return Stroke", 28, 1.2)]),
        new("spinning-rising-slash", WeaponType.SwordAndShield, "Spinning Rising Slash", "Element x1.2.", 0.9, [new AttackHit("Spinning Rising Slash", 39, 1.2)]),
        new("roundslash", WeaponType.SwordAndShield, "Roundslash", "A roundslash.", 1, [new AttackHit("Roundslash", 41)]),
        new("spinning-reaper", WeaponType.SwordAndShield, "Spinning Reaper", "A spinning slash.", 1.2, [new AttackHit("Spinning Reaper", 65)]),
        new("shield-attack", WeaponType.SwordAndShield, "Shield Attack", "A shield hit." + ShieldNote, 0.7, [new AttackHit("Shield Attack", 16, 0)]),
        new("shield-bash", WeaponType.SwordAndShield, "Shield Bash", "A shield hit." + ShieldNote, 0.8, [new AttackHit("Shield Bash", 27, 0)]),
        new("hard-basher", WeaponType.SwordAndShield, "Hard Basher", "A shield hit." + ShieldNote, 1, [new AttackHit("Hard Basher", 40, 0)]),
        new("guard-slash", WeaponType.SwordAndShield, "Guard Slash", "A slash from guard.", 0.6, [new AttackHit("Guard Slash", 14)]),
        new("charged-slash", WeaponType.SwordAndShield, "Charged Slash", "The charged slash (its first hit no longer exists).", 1.5, [new AttackHit("Charged Slash 2", 40)]),
        new("scaling-slash", WeaponType.SwordAndShield, "Scaling Slash", "A slash while climbing a ledge.", 0.8, [new AttackHit("Scaling Slash", 25)]),
        new("jumping-rising-slash", WeaponType.SwordAndShield, "Jumping Rising Slash", "An aerial slash.", 0.8, [new AttackHit("Jumping Rising Slash", 18)]),
        new("jumping-advancing-slash", WeaponType.SwordAndShield, "Jumping Advancing Slash", "An aerial slash.", 0.8, [new AttackHit("Jumping Advancing Slash", 18)]),
        new("jumping-slash", WeaponType.SwordAndShield, "Jumping Slash", "An aerial slash.", 0.8, [new AttackHit("Jumping Slash", 20)]),
        new("falling-bash", WeaponType.SwordAndShield, "Falling Bash", "Both shield hits." + ShieldNote, 1.5,
            [new AttackHit("Falling Bash 1", 52, 0), new AttackHit("Falling Bash 2", 47, 0)]),
        new("plunging-thrust", WeaponType.SwordAndShield, "Plunging Thrust", "The aerial plunge (element x0.3).", 1, [new AttackHit("Plunging Thrust", 12, 0.3)]),
        new("sliding-sweep", WeaponType.SwordAndShield, "Sliding Sweep", "Both hits.", 1.2, [new AttackHit("Sliding Sweep 1", 10), new AttackHit("Sliding Sweep 2", 50)]),
        new("sliding-slash", WeaponType.SwordAndShield, "Sliding Slash", "Both hits.", 1, [new AttackHit("Sliding Slash 1", 12), new AttackHit("Sliding Slash 2", 30)]),
        new("leaping-slash", WeaponType.SwordAndShield, "Leaping Slash", "A leaping slash.", 0.9, [new AttackHit("Leaping Slash", 30)]),
        new("perfect-rush", WeaponType.SwordAndShield, "Perfect Rush",
            "All three inputs untimed: six hits, the multihit lands three times. The first and fourth hits are shield hits without element.", 2.8, PerfectRush),
        new("perfect-rush-timed", WeaponType.SwordAndShield, "Perfect Rush (timed)",
            "All three inputs timed: the power version of every hit (element x1.5 on the slashes). The first and fourth hits are shield hits without element.", 2.8, PowerPerfectRush),
        new("charged-chop", WeaponType.SwordAndShield, "Charged Chop", "Every hit; the last one lands four times (element x0.3).", 1.8,
            [new AttackHit("Charged Chop 1", 18), new AttackHit("Charged Chop 2", 28, 1.2), new AttackHit("Charged Chop 3", 14, 0.3) { Count = 4 }]),
        new("charged-chop-power", WeaponType.SwordAndShield, "Charged Chop (power)", "Every hit with the power version of the last one: four hits of 28 MV (element x1.25).", 1.8,
            [new AttackHit("Charged Chop 1", 18), new AttackHit("Charged Chop 2", 28, 1.2), new AttackHit("Power Charged Chop 3", 28, 1.25) { Count = 4 }]),
        new("counter-slash", WeaponType.SwordAndShield, "Counter Slash", "Element x1.5.", 1.2, [new AttackHit("Counter Slash", 55, 1.5)]),
        new("recovering-slash", WeaponType.SwordAndShield, "Recovering Slash", "A slash out of a recovery.", 0.8, [new AttackHit("Recovering Slash", 18)]),
        new("vital-stab", WeaponType.SwordAndShield, "Focus Strike: Vital Stab", "The stab and its extra hit on a wound.", 1.5,
            [new AttackHit("Vital Stab", 15), new AttackHit("Focus Strike: Vital Stab Extra Hit", 15)]),
        new("vital-stab-open", WeaponType.SwordAndShield, "Vital Stab (opening a wound)", "Three hits of 19 MV (element x0.3) that force a wound open.", 1.5,
            [new AttackHit("Vital Stab forcing a wound to open", 19, 0.3) { Count = 3 }]),
        new("aerial-vital-stab", WeaponType.SwordAndShield, "Aerial Vital Stab", "The aerial Focus Strike stab.", 1.2, [new AttackHit("Aerial Vital Stab", 15)]),
        new("seikret-attack", WeaponType.SwordAndShield, "Seikret Attack", "Both attacks from the Seikret.", 1.5,
            [new AttackHit("Seikret Attack I", 10), new AttackHit("Seikret Attack II", 10)]),
        new("seikret-dash-attack", WeaponType.SwordAndShield, "Seikret Dash Attack", "A dashing attack from the Seikret.", 1, [new AttackHit("Seikret Dash Attack", 10)]),
        new("dismount-attack", WeaponType.SwordAndShield, "Dismount Attack", "The attack off the Seikret.", 1, [new AttackHit("Dismount Attack", 20)]),
        new("mount-attack", WeaponType.SwordAndShield, "Mount Attack", "Both mounted stabs; repeat it for the stabs you land.", 1,
            [new AttackHit("Mount Attack 1", 15), new AttackHit("Mount Attack 2", 17)]),
        new("mount-finisher", WeaponType.SwordAndShield, "Mount Finisher", "Every hit; the first three are shield hits without element.", 4,
            [new AttackHit("Mount Finisher 1", 13, 0), new AttackHit("Mount Finisher 2", 13, 0), new AttackHit("Mount Finisher 3", 25, 0),
             new AttackHit("Mount Finisher 4", 70), new AttackHit("Mount Finisher 5", 100)]),

        // Dual Blades moves (seconds are rough)
        new("double-slash", WeaponType.DualBlades, "Double Slash", "Both hits (element x0.6).", 0.8, [new AttackHit("Double Slash 1", 8, 0.6), new AttackHit("Double Slash 2", 11, 0.6)]),
        new("double-slash-return-stroke", WeaponType.DualBlades, "Double Slash Return Stroke", "Both hits (element x0.6).", 0.8,
            [new AttackHit("Double Slash Return Stroke 1", 9, 0.6), new AttackHit("Double Slash Return Stroke 2", 11, 0.6)]),
        new("circle-slash", WeaponType.DualBlades, "Circle Slash", "All three hits (element x0.7).", 1.2,
            [new AttackHit("Circle Slash 1", 6, 0.7), new AttackHit("Circle Slash 2", 11, 0.7), new AttackHit("Circle Slash 3", 12, 0.7)]),
        new("fade-slash", WeaponType.DualBlades, "Fade Slash", "Element x0.6.", 0.8, [new AttackHit("Fade Slash", 10, 0.6)]),
        new("rising-slash", WeaponType.DualBlades, "Rising Slash", "Element x0.6.", 0.6, [new AttackHit("Rising Slash", 8, 0.6)]),
        new("lunging-strike", WeaponType.DualBlades, "Lunging Strike", "All four hits (element x0.6).", 1,
            [new AttackHit("Lunging Strike 1", 3, 0.6), new AttackHit("Lunging Strike 2", 3, 0.6), new AttackHit("Lunging Strike 3", 6, 0.6), new AttackHit("Lunging Strike 4", 6, 0.6)]),
        new("side-lunging-strike", WeaponType.DualBlades, "Side/Back Lunging Strike", "Both hits (element x0.6).", 0.9,
            [new AttackHit("Side/Back Lunging Strike 1", 6, 0.6), new AttackHit("Side/Back Lunging Strike 2", 6, 0.6)]),
        new("demon-fangs", WeaponType.DualBlades, "Demon Fangs", "Demon mode (element x0.6).", 0.7, DemonFangs),
        new("twofold-demon-slash", WeaponType.DualBlades, "Twofold Demon Slash", "Both hits, demon mode (element x0.6).", 0.8, TwofoldDemonSlash),
        new("sixfold-demon-slash", WeaponType.DualBlades, "Sixfold Demon Slash", "All six hits, demon mode (element x0.6).", 1.6, SixfoldDemonSlash),
        new("demon-rising-slash", WeaponType.DualBlades, "Demon Rising Slash", "Demon mode (element x0.8).", 0.6, [new AttackHit("Demon Rising Slash", 10, 0.8)]),
        new("demon-flurry-rush", WeaponType.DualBlades, "Demon Flurry Rush", "All six hits (element x0.6, the last two x1).", 1.2,
            [new AttackHit("Demon Flurry Rush 1", 5, 0.6), new AttackHit("Demon Flurry Rush 2", 5, 0.6), new AttackHit("Demon Flurry Rush 3", 4, 0.6),
             new AttackHit("Demon Flurry Rush 4", 4, 0.6), new AttackHit("Demon Flurry Rush 5", 11), new AttackHit("Demon Flurry Rush 6", 11)]),
        new("spinning-slash", WeaponType.DualBlades, "Spinning Slash", "All three hits of the left or right Spinning Slash (element x0.6).", 1,
            [new AttackHit("Spinning Slash 1", 15, 0.6), new AttackHit("Spinning Slash 2", 7, 0.6), new AttackHit("Spinning Slash 3", 5, 0.6)]),
        new("double-roundslash", WeaponType.DualBlades, "Double Roundslash", "All three hits of the left or right Double Roundslash.", 1.2,
            [new AttackHit("Double Roundslash 1", 16), new AttackHit("Double Roundslash 2", 8), new AttackHit("Double Roundslash 3", 5)]),
        new("blade-dance-i", WeaponType.DualBlades, "Blade Dance I", "All twelve hits (element x0.6 to x1).", 1.6, BladeDance1),
        new("blade-dance-ii", WeaponType.DualBlades, "Blade Dance II", "All four hits (element x0.6 to x0.8).", 1.2, BladeDance2),
        new("blade-dance-iii", WeaponType.DualBlades, "Blade Dance III", "All eleven hits (element x0.6 to x0.8).", 1.8, BladeDance3),
        new("demon-flurry-i", WeaponType.DualBlades, "Demon Flurry I", "All four hits.", 1.2,
            [new AttackHit("Demon Flurry I 1", 15), new AttackHit("Demon Flurry I 2", 15), new AttackHit("Demon Flurry I 3", 15), new AttackHit("Demon Flurry I 4", 15)]),
        new("demon-flurry-ii", WeaponType.DualBlades, "Demon Flurry II", "All four hits (element x0.6, the last two x1).", 1.2,
            [new AttackHit("Demon Flurry II 1", 6, 0.6), new AttackHit("Demon Flurry II 2", 8, 0.6), new AttackHit("Demon Flurry II 3", 18), new AttackHit("Demon Flurry II 4", 18)]),
        new("jumping-doubleslash", WeaponType.DualBlades, "Jumping Doubleslash", "Both hits (element x0.6).", 0.9,
            [new AttackHit("Jumping Doubleslash 1", 12, 0.6), new AttackHit("Jumping Doubleslash 2", 9, 0.6)]),
        new("midair-spinning-blade-dance", WeaponType.DualBlades, "Midair Spinning Blade Dance", "The four spins and the six finisher hits (element x0.5 to x0.8).", 2.5,
            [new AttackHit("Midair Spinning Blade Dance 1", 12, 0.5), new AttackHit("Midair Spinning Blade Dance 2", 8, 0.5),
             new AttackHit("Midair Spinning Blade Dance 3", 12, 0.7), new AttackHit("Midair Spinning Blade Dance 4", 8, 0.7),
             new AttackHit("Midair Spinning Blade Dance Finisher 1", 15, 0.7), new AttackHit("Midair Spinning Blade Dance Finisher 2", 15, 0.7),
             new AttackHit("Midair Spinning Blade Dance Finisher 3", 20, 0.8), new AttackHit("Midair Spinning Blade Dance Finisher 4", 20, 0.8),
             new AttackHit("Midair Spinning Blade Dance Finisher 5", 6, 0.8), new AttackHit("Midair Spinning Blade Dance Finisher 6", 6, 0.8)]),
        new("heavenly-blade-dance-finisher", WeaponType.DualBlades, "Heavenly Blade Dance finisher",
            "The six finisher hits (element x0.7 to x1). The spinning hits before it are not counted: their raw drops after the 8th hit.", 2,
            [new AttackHit("Heavenly Blade Dance Finisher 1", 20), new AttackHit("Heavenly Blade Dance Finisher 2", 20),
             new AttackHit("Heavenly Blade Dance Finisher 3", 24, 0.7), new AttackHit("Heavenly Blade Dance Finisher 4", 24, 0.7),
             new AttackHit("Heavenly Blade Dance Finisher 5", 8, 0.7), new AttackHit("Heavenly Blade Dance Finisher 6", 8, 0.7)]),
        new("sliding-slash", WeaponType.DualBlades, "Sliding Slash", "All four hits (element x0.6).", 1.2,
            [new AttackHit("Sliding Slash 1", 10, 0.6), new AttackHit("Sliding Slash 2", 10, 0.6), new AttackHit("Sliding Slash 3", 13, 0.6), new AttackHit("Sliding Slash 4", 13, 0.6)]),
        new("perfect-evade", WeaponType.DualBlades, "Perfect Evade attack", "The three hits of the attack after a perfect evade (element x0.3 and x0.6).", 1,
            [new AttackHit("Perfect Evade 1", 6, 0.3), new AttackHit("Perfect Evade 2", 6, 0.3), new AttackHit("Perfect Evade 3", 10, 0.6)]),
        new("screw-slicer", WeaponType.DualBlades, "Screw Slicer", "All six hits (element x0.8 and x1).", 1.5,
            [new AttackHit("Screw Slicer 1", 2, 0.8), new AttackHit("Screw Slicer 2", 3, 0.8), new AttackHit("Screw Slicer 3", 15), new AttackHit("Screw Slicer 4", 10),
             new AttackHit("Screw Slicer 5", 15), new AttackHit("Screw Slicer 6", 10)]),
        new("turning-tide", WeaponType.DualBlades, "Focus Strike: Turning Tide", "Both hits, their extra hits on a wound and both follow-up hits.", 2.5,
            [new AttackHit("Focus Strike: Turning Tide 1", 10), new AttackHit("Focus Strike: Turning Tide 2", 10),
             new AttackHit("Focus Strike: Turning Tide 1 Extra Hit", 10), new AttackHit("Focus Strike: Turning Tide 2 Extra Hit", 10),
             new AttackHit("Focus Strike: Turning Tide Followup 1", 27), new AttackHit("Focus Strike: Turning Tide Followup 2", 27)]),
        new("turning-tidedrill", WeaponType.DualBlades, "Turning Tidedrill", "All ten hits (element x0.6 and x0.7).", 2.5,
            [new AttackHit("Turning Tidedrill 1", 12, 0.6), new AttackHit("Turning Tidedrill 2", 9, 0.6), new AttackHit("Turning Tidedrill 3", 9, 0.6),
             new AttackHit("Turning Tidedrill 4", 12, 0.6), new AttackHit("Turning Tidedrill 5", 9, 0.6), new AttackHit("Turning Tidedrill 6", 12, 0.6),
             new AttackHit("Turning Tidedrill 7", 12, 0.6), new AttackHit("Turning Tidedrill 8", 9, 0.6), new AttackHit("Turning Tidedrill 9", 18, 0.7),
             new AttackHit("Turning Tidedrill 10", 24, 0.7)]),
        new("seikret-attack", WeaponType.DualBlades, "Seikret Attack", "Both attacks from the Seikret, two hits each (element x0.6).", 1.5,
            [new AttackHit("Seikret Attack I 1", 10, 0.6), new AttackHit("Seikret Attack I 2", 8, 0.6), new AttackHit("Seikret Attack II 1", 10, 0.6), new AttackHit("Seikret Attack II 2", 8, 0.6)]),
        new("dismount-attack", WeaponType.DualBlades, "Dismount Attack", "All seven hits of the attack off the Seikret (element x0.5 to x0.7).", 2,
            [new AttackHit("Dismount Attack 1", 12, 0.5), new AttackHit("Dismount Attack 2", 8, 0.5), new AttackHit("Dismount Attack 3", 12, 0.7), new AttackHit("Dismount Attack 4", 8, 0.7),
             new AttackHit("Dismount Attack Finisher 1", 14, 0.6), new AttackHit("Dismount Attack Finisher 2", 6, 0.6), new AttackHit("Dismount Attack Finisher 3", 8, 0.6)]),
        new("mount-attack", WeaponType.DualBlades, "Mount Attack", "All six mounted hits (element x0.6); repeat it for the rounds you land.", 1.5,
            [new AttackHit("Mounted Attack 1", 5, 0.6), new AttackHit("Mounted Attack 2", 5, 0.6), new AttackHit("Mounted Attack 3", 8, 0.6),
             new AttackHit("Mounted Attack 4", 8, 0.6), new AttackHit("Mounted Attack 5", 6, 0.6), new AttackHit("Mounted Attack 6", 6, 0.6)]),
        new("mount-finisher", WeaponType.DualBlades, "Mount Finisher", "All ten hits (element x0.6, the last two x1).", 4,
            [new AttackHit("Mounted Finisher 1", 20, 0.6), new AttackHit("Mounted Finisher 2", 18, 0.6), new AttackHit("Mounted Finisher 3", 18, 0.6),
             new AttackHit("Mounted Finisher 4", 20, 0.6), new AttackHit("Mounted Finisher 5", 10, 0.6), new AttackHit("Mounted Finisher 6", 8, 0.6),
             new AttackHit("Mounted Finisher 7", 10, 0.6), new AttackHit("Mounted Finisher 8", 8, 0.6), new AttackHit("Mounted Finisher 9", 30), new AttackHit("Mounted Finisher 10", 30)]),
        // Dual Blades combos
        new("demon-combo", WeaponType.DualBlades, "Demon Fangs into Sixfold Demon Slash",
            "The demon-mode chain: Demon Fangs, Twofold Demon Slash, Sixfold Demon Slash (element x0.6).", 3.1,
            [.. DemonFangs, .. TwofoldDemonSlash, .. SixfoldDemonSlash]) { Combo = true },
        new("blade-dance", WeaponType.DualBlades, "Blade Dance I, II and III", "All three Blade Dances (27 hits).", 4.6,
            [.. BladeDance1, .. BladeDance2, .. BladeDance3]) { Combo = true },

        // Hammer moves (seconds are rough)
        new("swing", WeaponType.Hammer, "Swing", "A swing.", 0.7, [new AttackHit("Swing", 22)]),
        new("follow-up", WeaponType.Hammer, "Follow-up", "The follow-up swing.", 0.7, [new AttackHit("Follow-up", 18)]),
        new("overhead-smash-i", WeaponType.Hammer, "Overhead Smash I", "The first overhead smash.", 0.8, [new AttackHit("Overhead Smash I", 41)]),
        new("overhead-smash-ii", WeaponType.Hammer, "Overhead Smash II", "The second overhead smash.", 0.8, [new AttackHit("Overhead Smash II", 45)]),
        new("upswing", WeaponType.Hammer, "Upswing", "The upswing finisher (element x1.3).", 1.2, [new AttackHit("Upswing", 95, 1.3)]),
        new("side-smash", WeaponType.Hammer, "Side Smash", "A side smash.", 0.7, [new AttackHit("Side Smash", 23)]),
        new("charged-side-blow", WeaponType.Hammer, "Charged Side Blow", "The Lv1 charge release (element x1.1).", 0.8, [new AttackHit("Charged Side Blow", 25, 1.1)]),
        new("charged-follow-up", WeaponType.Hammer, "Charged Follow-up", "The follow-up after a charged attack.", 1, [new AttackHit("Charged Followup", 55)]),
        new("charged-brutal-upswing", WeaponType.Hammer, "Charged Brutal Upswing", "The Lv2 charge release (element x1.2).", 1.2, [new AttackHit("Charged Brutal Upswing", 70, 1.2)]),
        new("charged-big-bang", WeaponType.Hammer, "Charged Big Bang", "Both hits of the Lv3 charge release (the second element x1.5).", 1.5,
            [new AttackHit("Charged Big Bang 1", 20), new AttackHit("Charged Big Bang 2", 130, 1.5)]),
        new("spinning-bludgeon", WeaponType.Hammer, "Spinning Bludgeon", "The first spin and the finish (element x2.5 and x1.5). Add Spinning Bludgeon spin steps for the spins in between.", 1.5,
            [new AttackHit("Spinning Bludgeon Start", 23, 2.5), new AttackHit("Spinning Bludgeon Finish", 25, 1.5)]),
        new("spinning-bludgeon-spin", WeaponType.Hammer, "Spinning Bludgeon spin", "One spin (18 MV, element x2.5); repeat it for the spins you land.", 0.4,
            [new AttackHit("Spinning Bludgeon Mid", 18, 2.5)]),
        new("spinning-side-smash", WeaponType.Hammer, "Spinning Side Smash", "Out of a Spinning Bludgeon (element x1.5).", 1, [new AttackHit("Spinning Side Smash", 30, 1.5)]),
        new("spinning-follow-up", WeaponType.Hammer, "Spinning Follow-up", "Out of a Spinning Bludgeon (element x1.5).", 1, [new AttackHit("Spinning Followup", 55, 1.5)]),
        new("spinning-strong-upswing", WeaponType.Hammer, "Spinning Strong Upswing", "Out of a Spinning Bludgeon.", 1.2, [new AttackHit("Spinning Strong Upswing", 100)]),
        new("mighty-upswing", WeaponType.Hammer, "Mighty Upswing", "Element x1.2.", 1.3, [new AttackHit("Mighty Upswing", 130, 1.2)]),
        new("mighty-charge-slam", WeaponType.Hammer, "Mighty Charge Slam", "All three hits (the last element x1.5).", 2, MightyChargeSlam),
        new("follow-up-spinslam", WeaponType.Hammer, "Follow-up Spinslam", "Both hits (the last element x1.5). Add Follow-up Spinslam spin steps for the spins before them (up to five).", 2,
            FollowUpSpinslam),
        new("follow-up-spinslam-spin", WeaponType.Hammer, "Follow-up Spinslam spin", "One spin (20 MV); repeat it for the spins you land (up to five).", 0.4,
            [new AttackHit("Follow-up Spinslam Spin", 20)]),
        new("big-bang", WeaponType.Hammer, "Big Bang (all five)", "Big Bang I to V, every hit (element x1.1, the finisher x1.5).", 6,
            [new AttackHit("Big Bang I", 43, 1.1), new AttackHit("Big Bang II", 48, 1.1), new AttackHit("Big Bang III", 70, 1.1), new AttackHit("Big Bang IV", 90, 1.1),
             new AttackHit("Big Bang V 1", 28, 1.1), new AttackHit("Big Bang V 2", 28, 1.1), new AttackHit("Big Bang V 3", 130, 1.5)]),
        new("jump-attack", WeaponType.Hammer, "Jump Attack", "An aerial attack.", 1, [new AttackHit("Jump Attack", 37)]),
        new("aerial-charge-1", WeaponType.Hammer, "Aerial Charge Lv1", "Element x1.1.", 1, [new AttackHit("Aerial Charge Lv1", 72, 1.1)]),
        new("aerial-charge-2", WeaponType.Hammer, "Aerial Charge Lv2", "Element x1.2.", 1.2, [new AttackHit("Aerial Charge Lv2", 77, 1.2)]),
        new("aerial-charge-3", WeaponType.Hammer, "Aerial Charge Lv3", "Element x1.5.", 1.4, [new AttackHit("Aerial Charge Lv3", 88, 1.5)]),
        new("midair-spinning-bludgeon", WeaponType.Hammer, "Midair Spinning Bludgeon", "One spin and the landing.", 1.5,
            [new AttackHit("Midair Spinning Bludgeon Spin", 27), new AttackHit("Midair Spinning Bludgeon Land", 110)]),
        new("earthquake", WeaponType.Hammer, "Focus Strike: Earthquake", "The start and the finish. Add Earthquake spin steps for the spins in between.", 2,
            [new AttackHit("Focus Strike: Earthquake Start", 15), new AttackHit("Focus Strike: Earthquake Finish", 45)]),
        new("earthquake-spin", WeaponType.Hammer, "Focus Strike: Earthquake spin", "One spin (30 MV); repeat it for the spins you land.", 0.4,
            [new AttackHit("Focus Strike: Earthquake Spin", 30)]),
        new("seikret-attack", WeaponType.Hammer, "Seikret Attack", "An attack from the Seikret.", 1, [new AttackHit("Seikret Attack", 10)]),
        new("dismount-attack", WeaponType.Hammer, "Dismount Attack", "The spin and the landing off the Seikret.", 1.5,
            [new AttackHit("Dismount Attack Spin", 10), new AttackHit("Dismount Attack Land", 37)]),
        new("mount-attack", WeaponType.Hammer, "Mount Attack", "One mounted hit (37 MV); repeat it for the hits you land.", 0.8, [new AttackHit("Mount Attack", 37)]),
        new("mount-finisher", WeaponType.Hammer, "Mount Finisher", "Both hits.", 3, [new AttackHit("Mount Finisher 1", 140), new AttackHit("Mount Finisher 2", 100)]),
        // Hammer combos
        new("mighty-spinslam", WeaponType.Hammer, "Mighty Charge Slam into Follow-up Spinslam",
            "Mighty Charge Slam and the Follow-up Spinslam without spins (element x1.5 on both finishers).", 4,
            [.. MightyChargeSlam, .. FollowUpSpinslam]) { Combo = true },

        // Hunting Horn moves (seconds are rough)
        new("forward-smash", WeaponType.HuntingHorn, "Forward Smash", "A forward smash." + SongNote, 0.8, [new AttackHit("Forward Smash", 32)]),
        new("left-swing", WeaponType.HuntingHorn, "Left Swing", "A left swing." + SongNote, 0.8, [new AttackHit("Left Swing", 33)]),
        new("right-swing", WeaponType.HuntingHorn, "Right Swing", "A right swing." + SongNote, 0.8, [new AttackHit("Right Swing", 33)]),
        new("flourish", WeaponType.HuntingHorn, "Flourish", "Both hits." + SongNote, 1, [new AttackHit("Flourish 1", 15), new AttackHit("Flourish 2", 25)]),
        new("backwards-strike", WeaponType.HuntingHorn, "Backwards Strike", "A backwards strike." + SongNote, 1, [new AttackHit("Backwards Strike", 46)]),
        new("overhead-smash", WeaponType.HuntingHorn, "Overhead Smash", "Both hits." + SongNote, 1.2, [new AttackHit("Overhead Smash 1", 14), new AttackHit("Overhead Smash 2", 42)]),
        new("crush", WeaponType.HuntingHorn, "Crush", "A crushing smash." + SongNote, 1.2, [new AttackHit("Crush", 65)]),
        new("hilt-stab", WeaponType.HuntingHorn, "Hilt Stab", "Sever damage, counted at the blunt hitzone.", 0.7, [new AttackHit("Hilt Stab", 10)]),
        // directional performs (dtlnor Wp05: in place, forward, left/right, back), each with its shockwave
        new("neutral-perform", WeaponType.HuntingHorn, "Neutral Perform", "The horn hit and the shockwave." + PerformNote, 1, Perform("Neutral Perform", 35)),
        new("forward-perform", WeaponType.HuntingHorn, "Forward Perform", "The horn hit and the shockwave." + PerformNote, 1.4, Perform("Forward Perform", 35)),
        new("side-perform", WeaponType.HuntingHorn, "Side Perform", "Left or right: the horn hit and the shockwave." + PerformNote, 1, Perform("Side Perform", 45)),
        new("back-perform", WeaponType.HuntingHorn, "Back Perform", "The horn hit and the shockwave." + PerformNote, 1.2, Perform("Back Perform", 60)),
        new("performance-beat-i", WeaponType.HuntingHorn, "Performance Beat I", "Untimed: the horn hit and the shockwave." + PerformNote, 1, Perform("Performance Beat I", 33)),
        new("performance-beat-i-timed", WeaponType.HuntingHorn, "Performance Beat I (timed)", "The timed power version and the shockwave." + PerformNote, 1,
            Perform("Power Performance Beat I", 40)),
        new("performance-beat-ii", WeaponType.HuntingHorn, "Performance Beat II", "Untimed: the horn hit and the shockwave." + PerformNote, 1, Perform("Performance Beat II", 35)),
        new("performance-beat-ii-timed", WeaponType.HuntingHorn, "Performance Beat II (timed)", "The timed power version and the shockwave." + PerformNote, 1,
            Perform("Power Performance Beat II", 50)),
        new("performance-beat", WeaponType.HuntingHorn, "Performance Beat", "Beats I and II untimed, each with its shockwave." + PerformNote, 2,
            [.. Perform("Performance Beat I", 33), .. Perform("Performance Beat II", 35)]),
        new("performance-beat-timed", WeaponType.HuntingHorn, "Performance Beat (timed)", "Beats I and II timed: the power versions, each with its shockwave." + PerformNote, 2,
            PowerPerformanceBeat),
        new("encore", WeaponType.HuntingHorn, "Encore", "Both hits, untimed." + SongNote, 2.5, [new AttackHit("Encore 1", 20), new AttackHit("Encore 2", 60)]),
        new("encore-timed", WeaponType.HuntingHorn, "Encore (timed)", "Both hits, the second the timed power version." + SongNote, 2.5, PowerEncore),
        new("jumping-smash", WeaponType.HuntingHorn, "Jumping Smash", "An aerial smash.", 1, [new AttackHit("Jumping Smash", 35)]),
        new("jumping-strong-overhead-smash", WeaponType.HuntingHorn, "Jumping Strong Overhead Smash", "A strong aerial smash.", 1.2, [new AttackHit("Jumping Strong Overhead Smash", 55)]),
        new("scaling-smash", WeaponType.HuntingHorn, "Scaling Smash", "A smash while climbing a ledge.", 1, [new AttackHit("Scaling Smash", 30)]),
        new("offset-melody", WeaponType.HuntingHorn, "Offset Melody", "The offset attack.", 1.5, [new AttackHit("Offset Melody", 75)]),
        new("resounding-melody", WeaponType.HuntingHorn, "Resounding Melody", "The three swings." + SongNote, 2.5,
            [new AttackHit("Resounding Melody 1", 20), new AttackHit("Resounding Melody 2", 22), new AttackHit("Resounding Melody 3", 20)]),
        new("reverb", WeaponType.HuntingHorn, "Focus Strike: Reverb", "The swing." + SongNote, 2, [new AttackHit("Focus Strike: Reverb", 20)]),
        new("reverb-timed", WeaponType.HuntingHorn, "Focus Strike: Reverb (timed)",
            "The swing, five timed soundwaves (25 MV) and the finisher (70 MV) when all five are timed." + WaveNote, 4,
            [new AttackHit("Focus Strike: Reverb", 20), Wave("Focus Strike Soundwave", 25) with { Count = 5 }, Wave("Focus Strike Finisher", 70)]),
        new("perform-soundwave", WeaponType.HuntingHorn, "Perform shockwave", "The shockwave of a performance alone (27 MV); the perform attacks include it." + WaveNote, 0.3,
            [Wave("Perform Shockwave", 27)]),
        new("song-soundwave", WeaponType.HuntingHorn, "Song shockwave", "The shockwave when a song takes effect (10 MV)." + WaveNote, 0.3, [Wave("Song Shockwave", 10)]),
        new("resounding-melody-soundwaves", WeaponType.HuntingHorn, "Resounding Melody soundwaves",
            "Ten soundwaves of 15 MV (element x0.5), raw lower from the third on." + WaveNote, 3,
            [.. ResoundingDecay.Select((d, i) => Wave($"Resounding Melody Soundwave {i + 1}", Math.Round(15 * d, 3), 0.5))]),
        new("echo-bubble", WeaponType.HuntingHorn, "Echo Bubble", "Placing the bubble on the monster (45 MV, element x0.3)." + WaveNote, 1,
            [Wave("Echo Bubble Place", 45, 0.3)]),
        new("echo-bubble-note", WeaponType.HuntingHorn, "Echo Bubble follow-up (attack)",
            "The follow-up a bubble adds to an attack near it (5 MV, element x0.3); only the first Flourish and the second Overhead Smash hit trigger it." + WaveNote, 0.3,
            [Wave("Echo Bubble Follow-up", 5, 0.3)]),
        new("echo-bubble-song", WeaponType.HuntingHorn, "Echo Bubble follow-up (performance)",
            "The follow-up a bubble adds when a melody completes near it (12 MV, element x0.3)." + WaveNote, 0.3,
            [Wave("Echo Bubble Follow-up (Performance)", 12, 0.3)]),
        .. EchoWaveLevels.SelectMany(w => new[]
        {
            new AttackDefinition($"echo-wave-blunt-{w.Level}", WeaponType.HuntingHorn, $"Echo Wave (blunt) Lv{w.Level}",
                $"{w.Blunt} MV, no element." + EchoWaveNote, 1.5, [Wave($"Echo Wave Blunt Lv{w.Level}", w.Blunt, 0) with { IgnoresHitzone = false }]),
            new AttackDefinition($"echo-wave-slash-{w.Level}", WeaponType.HuntingHorn, $"Echo Wave (slash) Lv{w.Level}",
                $"{w.Blunt} MV, no element, ignores sharpness; unlike the other waves it can crit. Sever damage, counted at the blunt hitzone.", 1.5,
                [new AttackHit($"Echo Wave Slash Lv{w.Level}", w.Blunt, 0) { IgnoresSharpness = true }]),
        }),
        .. EchoWaveLevels.SelectMany(w => EchoElements.Select(e =>
            new AttackDefinition($"echo-wave-{e.ToString().ToLowerInvariant()}-{w.Level}", WeaponType.HuntingHorn, $"Echo Wave ({e}) Lv{w.Level}",
                $"{w.ElementMv} MV raw and a fixed {w.ElementValue} {e.ToString().ToLowerInvariant()} element at the target's {e.ToString().ToLowerInvariant()} hitzone (skills do not change it)." + EchoWaveNote,
                1.5, [Wave($"Echo Wave {e} Lv{w.Level}", w.ElementMv, 0) with { IgnoresHitzone = false, FixedElement = new FixedElementDamage(e, w.ElementValue) }]))),
        new("seikret-attack", WeaponType.HuntingHorn, "Seikret Attack", "Both attacks from the Seikret.", 1.5, [new AttackHit("Seikret Attack I", 20), new AttackHit("Seikret Attack II", 30)]),
        new("seikret-perform", WeaponType.HuntingHorn, "Seikret Perform", "The performance hit from the Seikret." + SongNote, 1, [new AttackHit("Seikret Perform", 29)]),
        new("seikret-encore", WeaponType.HuntingHorn, "Seikret Encore", "The encore hit from the Seikret." + SongNote, 1.5, [new AttackHit("Seikret Encore", 35)]),
        new("dismount-attack", WeaponType.HuntingHorn, "Dismount Attack", "The attack off the Seikret.", 1, [new AttackHit("Seikret Dismount", 45)]),
        new("mount-attack", WeaponType.HuntingHorn, "Mount Attack", "One mounted hit (37 MV); repeat it for the hits you land.", 0.8, [new AttackHit("Mounted Attack", 37)]),
        new("mount-finisher", WeaponType.HuntingHorn, "Mount Finisher", "Both hits.", 3, [new AttackHit("Mounted Finisher 1", 140), new AttackHit("Mounted Finisher 2", 100)]),
        // Hunting Horn combos
        new("beat-encore", WeaponType.HuntingHorn, "Performance Beat into Encore (timed)",
            "Timed Performance Beats I and II, then a timed Encore." + SongNote, 4.5, [.. PowerPerformanceBeat, .. PowerEncore]) { Combo = true },

        // Lance moves (seconds are rough)
        new("mid-thrust-i", WeaponType.Lance, "Mid Thrust I", "Element x1.2.", 0.6, [MidThrusts[0]]),
        new("mid-thrust-ii", WeaponType.Lance, "Mid Thrust II", "Element x1.2.", 0.6, [MidThrusts[1]]),
        new("mid-thrust-iii", WeaponType.Lance, "Mid Thrust III", "Element x1.4.", 0.8, [MidThrusts[2]]),
        new("high-thrust-i", WeaponType.Lance, "High Thrust I", "A high thrust.", 0.6, [HighThrusts[0]]),
        new("high-thrust-ii", WeaponType.Lance, "High Thrust II", "A high thrust.", 0.6, [HighThrusts[1]]),
        new("high-thrust-iii", WeaponType.Lance, "High Thrust III", "A high thrust.", 0.8, [HighThrusts[2]]),
        new("triple-thrust", WeaponType.Lance, "Triple Thrust", "All three thrusts (element x1.3, the last x1.5).", 1.5,
            [new AttackHit("Triple Thrust 1", 25, 1.3), new AttackHit("Triple Thrust 2", 25, 1.3), new AttackHit("Triple Thrust 3", 42, 1.5)]),
        .. LanceCounters.Select(c => new AttackDefinition($"charge-counter-{c.Level[2..]}", WeaponType.Lance, $"Charge Counter {c.Level}",
            "The counter thrust at this charge level.", 1.5, [new AttackHit($"Charge Counter {c.Level}", c.Counter)])),
        .. LanceCounters.Select(c => new AttackDefinition($"double-counter-thrust-{c.Level[2..]}", WeaponType.Lance, $"Double Counter-thrust {c.Level}",
            "Both thrusts at this charge level.", 1.5, [new AttackHit($"Double Counter-thrust 1 {c.Level}", c.Thrust1), new AttackHit($"Double Counter-thrust 2 {c.Level}", c.Thrust2)])),
        new("leaping-thrust", WeaponType.Lance, "Leaping Thrust", "Both hits (element x1.2).", 1, [new AttackHit("Leaping Thrust", 29, 1.2) { Count = 2 }]),
        new("wide-sweep", WeaponType.Lance, "Wide Sweep", "Element x1.4.", 1.2, [new AttackHit("Wide Sweep", 80, 1.4)]),
        new("shield-attack", WeaponType.Lance, "Shield Attack", "A shield hit." + ShieldNote, 0.8, [new AttackHit("Shield Attack", 15, 0)]),
        new("guard-thrust", WeaponType.Lance, "Guard Thrust", "A thrust from guard.", 0.7, [new AttackHit("Guard Thrust", 20)]),
        new("dash-attack", WeaponType.Lance, "Dash Attack", "One hit of the charge (element x0.5); repeat it for the hits you land.", 0.5, [new AttackHit("Dash Attack", 17, 0.5)]),
        new("air-dash-attack", WeaponType.Lance, "Air Dash Attack", "Element x0.5.", 1, [new AttackHit("Air Dash Attack", 25, 0.5)]),
        new("finishing-thrust", WeaponType.Lance, "Finishing Thrust", "The charge finisher (element x1.4).", 1, [new AttackHit("Finishing Thrust", 55, 1.4)]),
        new("finishing-twin-thrust", WeaponType.Lance, "Finishing Twin Thrust", "Both thrusts (element x1.6).", 1.3,
            [new AttackHit("Finishing Twin Thrust 1", 35, 1.6), new AttackHit("Finishing Twin Thrust 2", 70, 1.6)]),
        new("reverse-attack", WeaponType.Lance, "Reverse Attack", "Element x1.4.", 1, [new AttackHit("Reverse Attack", 53, 1.4)]),
        new("high-speed-reverse-attack", WeaponType.Lance, "High Speed Reverse Attack", "Element x1.4.", 1, [new AttackHit("High Speed Reverse Attack", 70, 1.4)]),
        new("dash-jump-attack", WeaponType.Lance, "Dash Jump Attack", "Element x0.5.", 1, [new AttackHit("Dash Jump Attack", 30, 0.5)]),
        new("high-speed-dash-jump-attack", WeaponType.Lance, "High Speed Dash Jump Attack", "Element x0.5.", 1, [new AttackHit("High Speed Dash Jump Attack", 33, 0.5)]),
        new("jump-attack", WeaponType.Lance, "Jump Attack", "An aerial thrust.", 1, [new AttackHit("Jump Attack", 30)]),
        new("shield-counter", WeaponType.Lance, "Shield Counter", "A shield hit." + ShieldNote, 0.8, [new AttackHit("Shield Counter", 20, 0)]),
        new("perfect-shield-counter", WeaponType.Lance, "Perfect Shield Counter", "After a Perfect Guard." + ShieldNote, 0.8, [new AttackHit("Perfect Shield Counter", 25, 0)]),
        new("return-thrust", WeaponType.Lance, "Return Thrust", "Element x1.2.", 0.8, [new AttackHit("Return Thrust", 24, 1.2)]),
        new("perfect-return-thrust", WeaponType.Lance, "Perfect Return Thrust", "After a Perfect Guard (element x1.4).", 0.8, [new AttackHit("Perfect Return Thrust", 31, 1.4)]),
        .. LanceAuras.Select(a => new AttackDefinition($"power-return-thrust-{a.Level}", WeaponType.Lance, $"Power Return Thrust Lv{a.Level}",
            $"After a Power Guard grants the {a.Aura} aura (element x1.4).", 0.8, [new AttackHit($"Power Return Thrust Lv{a.Level}", a.Level switch { 1 => 26.4, 2 => 31.2, _ => 40.8 }, 1.4)])),
        new("payback-thrust", WeaponType.Lance, "Payback Thrust", "Both thrusts.", 1.2, [new AttackHit("Payback Thrust 1", 30), new AttackHit("Payback Thrust 2", 25)]),
        new("perfect-payback-thrust", WeaponType.Lance, "Perfect Payback Thrust", "Both thrusts after a Perfect Guard (the first element x1.4).", 1.2,
            [new AttackHit("Perfect Payback Thrust 1", 42, 1.4), new AttackHit("Perfect Payback Thrust 2", 25)]),
        .. LanceAuras.Select(a => new AttackDefinition($"power-payback-thrust-{a.Level}", WeaponType.Lance, $"Power Payback Thrust Lv{a.Level}",
            $"Both thrusts after a Power Guard grants the {a.Aura} aura.", 1.2,
            a.Level switch
            {
                1 => [new AttackHit("Power Payback Thrust 1 Lv1", 33), new AttackHit("Power Payback Thrust 2 Lv1", 27.5)],
                2 => [new AttackHit("Power Payback Thrust 1 Lv2", 39), new AttackHit("Power Payback Thrust 2 Lv2", 32.5)],
                _ => [new AttackHit("Power Payback Thrust 1 Lv3", 51), new AttackHit("Power Payback Thrust 2 Lv3", 42.5)],
            })),
        new("retribution-thrust", WeaponType.Lance, "Retribution Thrust", "Both thrusts (the second element x1.4).", 1.5,
            [new AttackHit("Retribution Thrust 1", 20), new AttackHit("Retribution Thrust 2", 30, 1.4)]),
        new("perfect-retribution-thrust", WeaponType.Lance, "Perfect Retribution Thrust", "Both thrusts after a Perfect Guard (the second element x1.6).", 1.5,
            [new AttackHit("Perfect Retribution Thrust 1", 20), new AttackHit("Perfect Retribution Thrust 2", 58, 1.6)]),
        .. LanceAuras.Select(a => new AttackDefinition($"grand-retribution-thrust-{a.Level}", WeaponType.Lance, $"Grand Retribution Thrust Lv{a.Level}",
            $"All three thrusts after a Power Guard grants the {a.Aura} aura.", 1.8,
            a.Level switch
            {
                1 => [new AttackHit("Grand Retribution Thrust 1 Lv1", 61.6), new AttackHit("Grand Retribution Thrust 2 Lv1", 22), new AttackHit("Grand Retribution Thrust 3 Lv1", 13.2)],
                2 => [new AttackHit("Grand Retribution Thrust 1 Lv2", 72.8), new AttackHit("Grand Retribution Thrust 2 Lv2", 26), new AttackHit("Grand Retribution Thrust 3 Lv2", 15.6)],
                _ => [new AttackHit("Grand Retribution Thrust 1 Lv3", 95.2), new AttackHit("Grand Retribution Thrust 2 Lv3", 34), new AttackHit("Grand Retribution Thrust 3 Lv3", 20.4)],
            })),
        new("follow-up-dash-attack", WeaponType.Lance, "Follow-up Dash Attack", "The three hits that land (element x0.5 and x1).", 1.5,
            [new AttackHit("Follow-up Dash Attack 2", 17, 0.5), new AttackHit("Follow-up Dash Attack 3", 24), new AttackHit("Follow-up Dash Attack 4", 17, 0.5)]),
        new("victory-thrust", WeaponType.Lance, "Victory Thrust", "The thrust, four charge hits and three resolve hits (element x1.2).", 3,
            [new AttackHit("Victory Thrust", 20), new AttackHit("Victory Thrust Charge", 15) { Count = 4 }, new AttackHit("Victory Thrust Resolve", 40, 1.2) { Count = 3 }]),
        new("laser-counter-finisher", WeaponType.Lance, "Laser Counter Finisher", "Element x1.4.", 1.5, [new AttackHit("Laser Counter Finisher", 70, 1.4)]),
        new("seikret-charge", WeaponType.Lance, "Seikret Charge", "Element x0.5.", 1, [new AttackHit("Seikret Charge", 10, 0.5)]),
        new("seikret-attack", WeaponType.Lance, "Seikret Upswing and Sweep", "Both attacks from the Seikret.", 1.5, [new AttackHit("Seikret Upswing", 30), new AttackHit("Seikret Sweep", 35)]),
        new("dismount-attack", WeaponType.Lance, "Dismount Attack", "The attack off the Seikret.", 1, [new AttackHit("Dismount Attack", 20)]),
        new("seikret-charge-dismount", WeaponType.Lance, "Seikret Charge Dismount Attack", "Both hits; the first is a shield hit without element.", 1.5,
            [new AttackHit("Seikret Charge Dismount Attack 1", 10, 0), new AttackHit("Seikret Charge Dismount Attack 2", 30)]),
        new("mount-attack", WeaponType.Lance, "Mount Attack", "One mounted hit (37 MV); repeat it for the hits you land.", 0.8, [new AttackHit("Mounted Attack", 37)]),
        new("mount-finisher", WeaponType.Lance, "Mount Finisher", "The finisher.", 3, [new AttackHit("Mounted Finisher", 180)]),
        // Lance combos
        new("mid-thrust-combo", WeaponType.Lance, "Mid Thrust I, II and III", "The three mid thrusts (element x1.2, the last x1.4).", 2, MidThrusts) { Combo = true },
        new("high-thrust-combo", WeaponType.Lance, "High Thrust I, II and III", "The three high thrusts.", 2, HighThrusts) { Combo = true },

        // Gunlance moves (seconds are rough)
        new("lateral-thrust", WeaponType.Gunlance, "Lateral Thrust", "Lateral Thrust I or II (element x2)." + ShellNote, 0.6, [new AttackHit("Lateral Thrust", 24, 2)]),
        new("lunging-upthrust", WeaponType.Gunlance, "Lunging Upthrust", "Element x1.2." + ShellNote, 0.8, [new AttackHit("Lunging Upthrust", 38, 1.2)]),
        new("rising-slash", WeaponType.Gunlance, "Rising Slash", "Element x1.2." + ShellNote, 0.8, [new AttackHit("Rising Slash", 38, 1.2)]),
        new("guard-thrust", WeaponType.Gunlance, "Guard Thrust", "Element x2." + ShellNote, 0.6, [new AttackHit("Guard Thrust", 18, 2)]),
        new("overhead-smash", WeaponType.Gunlance, "Overhead Smash", "Element x2." + ShellNote, 1, [new AttackHit("Overhead Smash", 57, 2)]),
        new("wide-sweep", WeaponType.Gunlance, "Wide Sweep", "Also Moving Sweep and Aerial Burst (element x1.5)." + ShellNote, 1, [new AttackHit("Wide Sweep", 40, 1.5)]),
        new("wyrmstake-thrust", WeaponType.Gunlance, "Wyrmstake Thrust", "The thrust (element x2). Add Wyrmstake tick steps for the ticks of the stake." + ShellNote, 1.2,
            [new AttackHit("Wyrmstake Thrust", 40, 2)]),
        new("wyrmstake-attach", WeaponType.Gunlance, "Wyrmstake Attach", "Element x2. Add Wyrmstake tick steps for the ticks of the stake." + ShellNote, 1,
            [new AttackHit("Wyrmstake Attach", 25, 2)]),
        new("wyrmstake-full-blast-thrust", WeaponType.Gunlance, "Wyrmstake Full Blast Thrust", "The thrust." + ShellNote, 1.5, [new AttackHit("Wyrmstake Full Blast Thrust", 20)]),
        new("wyrmstake-full-blast-sweep", WeaponType.Gunlance, "Wyrmstake Full Blast Sweep", "The sweep." + ShellNote, 1.5, [new AttackHit("Wyrmstake Full Blast Sweep", 30)]),
        new("multi-wyrmstake-full-blast-sweep", WeaponType.Gunlance, "Multi Wyrmstake Full Blast Sweep", "The sweep." + ShellNote, 1.5,
            [new AttackHit("Multi Wyrmstake Full Blast Sweep", 30)]),
        new("wyrmstake-tick", WeaponType.Gunlance, "Wyrmstake tick",
            "One tick of a Wyrmstake (element x0.5): 7 MV with Normal or Wide shelling Lv3, 8 MV with Long; repeat it for the ticks that land (their number depends on the attack that placed it).",
            0.2, [new AttackHit("Wyrmstake Tick", 7, 0.5) { Shell = GunlanceShot.WyrmstakeTick }]),
        new("shell", WeaponType.Gunlance, "Shell", "One shell." + ShellingNote, 0.8, [Shell]),
        new("charged-shell", WeaponType.Gunlance, "Charged Shell", "One charged shell." + ShellingNote, 1.5, [ShellHit("Charged Shell", GunlanceShot.ChargedShell)]),
        new("full-burst-shell", WeaponType.Gunlance, "Full Burst (one shell)", "One shell of a Full Burst; repeat it for the shells loaded." + ShellingNote, 0.4,
            [ShellHit("Fullburst Shell", GunlanceShot.FullburstShell)]),
        new("wyrmstake-full-burst-shell", WeaponType.Gunlance, "Wyrmstake Full Blast (one shell)", "One shell of a Wyrmstake Full Blast; repeat it for the shells loaded." + ShellingNote, 0.4,
            [ShellHit("Wyrmstake Fullburst Shell", GunlanceShot.WyrmstakeFullburstShell)]),
        new("wyvernfire", WeaponType.Gunlance, "Wyvern's Fire", "The blast." + ShellingNote, 4, [ShellHit("Wyvernfire", GunlanceShot.Wyvernfire)]),
        new("wyrmstake-explosion", WeaponType.Gunlance, "Wyrmstake explosion", "The stake's explosion." + ShellingNote, 0.5,
            [ShellHit("Wyrmstake Explosion", GunlanceShot.WyrmstakeExplosion)]),
        new("jumping-thrust", WeaponType.Gunlance, "Jumping Thrust", "An aerial thrust." + ShellNote, 1, [new AttackHit("Jumping Thrust", 25)]),
        new("jumping-smash", WeaponType.Gunlance, "Jumping Smash", "An aerial smash." + ShellNote, 1, [new AttackHit("Jumping Smash", 44)]),
        new("jumping-rising-slash", WeaponType.Gunlance, "Jumping Rising Slash", "An aerial slash." + ShellNote, 1, [new AttackHit("Jumping Rising Slash", 35)]),
        new("drake-auger", WeaponType.Gunlance, "Focus Strike: Drake Auger", "The opening hit. Add Drake Auger tick steps for the drill ticks." + ShellNote, 2,
            [new AttackHit("Drake Auger", 18)]),
        new("drake-auger-tick", WeaponType.Gunlance, "Drake Auger tick", "One drill tick (7 MV, element x0.5); repeat it for the ticks you land.", 0.2,
            [new AttackHit("Drake Auger Ticks", 7, 0.5)]),
        new("seikret-charge", WeaponType.Gunlance, "Seikret Charge", "Element x0.5.", 1, [new AttackHit("Seikret Charge", 10, 0.5)]),
        new("seikret-attack", WeaponType.Gunlance, "Seikret Upswing and Slam", "Both attacks from the Seikret.", 1.5, [new AttackHit("Seikret Upswing", 30), new AttackHit("Seikret Slam", 48)]),
        new("mount-attack", WeaponType.Gunlance, "Mount Attack", "One mounted hit (15 MV); repeat it for the hits you land." + ShellNote, 0.8, [new AttackHit("Mount Attack", 15)]),
        new("mount-finisher", WeaponType.Gunlance, "Mount Finisher", "The finisher hit. Add Drake Auger tick steps for its ticks." + ShellNote, 2, [new AttackHit("Mount Finisher", 10)]),
        // Gunlance combos
        new("rising-slash-combo", WeaponType.Gunlance, "Rising Slash, Overhead Smash, Wide Sweep",
            "The melee chain (element x1.2, x2, x1.5), no shells." + ShellNote, 2.8,
            [new AttackHit("Rising Slash", 38, 1.2), new AttackHit("Overhead Smash", 57, 2), new AttackHit("Wide Sweep", 40, 1.5)]) { Combo = true },
        new("rising-slash-shell-combo", WeaponType.Gunlance, "Rising Slash, Overhead Smash, Wide Sweep with shells",
            "The melee chain with a shell after each attack." + ShellingNote, 4.6,
            [new AttackHit("Rising Slash", 38, 1.2), Shell, new AttackHit("Overhead Smash", 57, 2), Shell, new AttackHit("Wide Sweep", 40, 1.5), Shell]) { Combo = true },

        // Charge Blade moves (seconds are rough)
        new("sword-forward-slash", WeaponType.ChargeBlade, "Sword: Forward Slash", "A sword slash.", 0.6, [new AttackHit("Sword Forward Slash", 18)]),
        new("sword-weak-slash", WeaponType.ChargeBlade, "Sword: Weak Slash", "A sword slash.", 0.6, [new AttackHit("Sword Weak Slash", 15)]),
        new("sword-return-stroke", WeaponType.ChargeBlade, "Sword: Return Stroke", "A sword slash.", 0.6, [new AttackHit("Sword Return Stroke", 18)]),
        new("sword-roundslash", WeaponType.ChargeBlade, "Sword: Roundslash", "Also Axe Morph Slash and Elemental Roundslash.", 1, [new AttackHit("Sword Roundslash", 30)]),
        new("sword-fade-slash", WeaponType.ChargeBlade, "Sword: Fade Slash", "A sword slash.", 0.8, [new AttackHit("Sword Fade Slash", 24)]),
        new("charged-rising-slash", WeaponType.ChargeBlade, "Sword: Charged Rising Slash", "A sword slash.", 0.8, [new AttackHit("Charged Rising Slash", 15)]),
        new("charged-double-slash", WeaponType.ChargeBlade, "Sword: Charged Double Slash", "Both slashes.", 1.5,
            [new AttackHit("Charged Double Slash 1", 25), new AttackHit("Charged Double Slash 2", 16)]),
        new("sword-shield-thrust", WeaponType.ChargeBlade, "Sword: Shield Thrust", "Both hits.", 1, [new AttackHit("Sword Shield Thrust 1", 15), new AttackHit("Sword Shield Thrust 2", 6)]),
        new("condensed-elemental-slash", WeaponType.ChargeBlade, "Sword: Condensed Elemental Slash",
            "The slash and its explosion at yellow sharpness, which can crit: Impact phial 25 MV without element, Element phial element x4 without raw.", 1.5,
            [new AttackHit("Sword Condensed Elemental Slash", 48),
             new AttackHit("Condensed Elemental Slash Explosion", 25, 0) { PhialBurstElement = 4, FixedSharpness = SharpnessColor.Yellow }]),
        new("sword-morph-slash", WeaponType.ChargeBlade, "Sword: Morph Slash", "Morphs to axe.", 1, [new AttackHit("Sword Morph Slash", 54)]),
        new("axe-overhead-slash", WeaponType.ChargeBlade, "Axe: Overhead Slash", "An axe slash.", 1, [new AttackHit("Axe Overhead Slash", 54)]),
        new("axe-rising-slash", WeaponType.ChargeBlade, "Axe: Rising Slash", "An axe slash.", 1, [new AttackHit("Axe Rising Slash", 38)]),
        new("axe-dash-slam", WeaponType.ChargeBlade, "Axe: Dash Slam", "An axe slam.", 1, [new AttackHit("Axe Dash Slam", 48)]),
        new("axe-fade-slash", WeaponType.ChargeBlade, "Axe: Lateral Fade Slash", "Also Backstep Slash.", 1, [new AttackHit("Axe Lateral Fade Slash", 42)]),
        new("elemental-discharge-i", WeaponType.ChargeBlade, "Axe: Elemental Discharge I", "The swing and one phial burst." + PhialNote, 1.2,
            [new AttackHit("Axe Elemental Discharge I", 53), PhialBurst("Phial Burst ED", 12, 2.1)]),
        new("elemental-discharge-ii", WeaponType.ChargeBlade, "Axe: Elemental Discharge II", "Both swings and one phial burst." + PhialNote, 1.5,
            [new AttackHit("Axe Elemental Discharge II 1", 35), new AttackHit("Axe Elemental Discharge II 2", 58), PhialBurst("Phial Burst ED", 12, 2.1)]),
        new("aed", WeaponType.ChargeBlade, "Axe: Amped Elemental Discharge", "The swing and one phial burst." + PhialNote, 1.8, Aed),
        new("aed-follow-up", WeaponType.ChargeBlade, "Axe: Amped Elemental Discharge Follow-up", "The swing and one phial burst." + PhialNote, 1.8, AedFollowUp),
        new("saed", WeaponType.ChargeBlade, "Axe: Super Amped Elemental Discharge", "All three swings and five phial bursts (full phials)." + PhialNote, 3,
            [.. SaedSwings, PhialBurst("Phial Burst SAED", 28, 8.2, 5)]),
        new("saed-overcharged", WeaponType.ChargeBlade, "Axe: Super Amped Elemental Discharge (overcharged)",
            "All three swings and five overcharged phial bursts." + PhialNote, 3, [.. SaedSwings, PhialBurst("Phial Burst SAED Overcharged Phial", 30, 11, 5)]),
        new("savage-axe", WeaponType.ChargeBlade, "Savage Axe", "The slam; activates Power Axe. Add Power Axe tick steps for the ticks of later axe swings.", 1.5,
            [new AttackHit("Savage Axe", 58)]),
        new("power-axe-tick", WeaponType.ChargeBlade, "Power Axe tick", "One tick of a Power Axe swing (12 MV, element x0.2), up to four per swing.", 0.2,
            [new AttackHit("Power Axe Ticks", 12, 0.2)]),
        new("sword-jumping-slash", WeaponType.ChargeBlade, "Sword: Jumping Slash", "Also Axe Jumping Morph Slash.", 0.8, [new AttackHit("Sword Jumping Slash", 22)]),
        new("axe-jumping-slash", WeaponType.ChargeBlade, "Axe: Jumping Slash", "Also Sword Jumping Morph Slash.", 1, [new AttackHit("Axe Jumping Slash", 46)]),
        new("sword-sliding-slash", WeaponType.ChargeBlade, "Sword: Sliding Slash", "Both hits.", 1, [new AttackHit("Sword Sliding Slash 1", 12), new AttackHit("Sword Sliding Slash 2", 30)]),
        new("sword-jumping-rising-slash", WeaponType.ChargeBlade, "Sword: Jumping Rising Slash", "An aerial slash.", 0.8, [new AttackHit("Sword Jumping Rising Slash", 25)]),
        new("scaling-chop", WeaponType.ChargeBlade, "Scaling Chop / Roundslash", "Both hits of the sword Scaling Chop or the axe Scaling Roundslash.", 1,
            [new AttackHit("Scaling Chop 1", 20), new AttackHit("Scaling Chop 2", 40)]),
        new("double-rend", WeaponType.ChargeBlade, "Focus Strike: Double Rend", "Both hits and the extra hit on a wound.", 1.8,
            [new AttackHit("Double Rend 1", 15), new AttackHit("Double Rend 2", 15), new AttackHit("Double Rend Extra Hit", 15)]),
        new("focus-savage-axe", WeaponType.ChargeBlade, "Focus Strike: Savage Axe", "The slam; activates Power Axe. Add Focus Strike Savage Axe tick steps for its ticks.", 1.8,
            [new AttackHit("Focus Strike Savage Axe", 50)]),
        new("focus-savage-axe-tick", WeaponType.ChargeBlade, "Focus Strike: Savage Axe tick", "One tick (6 MV, element x0.3); repeat it for the ticks you land.", 0.2,
            [new AttackHit("Focus Strike Savage Axe Ticks", 6, 0.3)]),
        new("seikret-attack", WeaponType.ChargeBlade, "Seikret Attack", "Both attacks from the Seikret (three hits).", 1.5,
            [new AttackHit("Seikret Attack I 1", 12), new AttackHit("Seikret Attack I 2", 18), new AttackHit("Seikret Attack II", 24)]),
        new("dismount-attack", WeaponType.ChargeBlade, "Dismount Attack", "All three hits of the attack off the Seikret.", 1.5,
            [new AttackHit("Dismount Attack 1", 30), new AttackHit("Dismount Attack 2", 12), new AttackHit("Dismount Attack 3", 6)]),
        new("mount-attack", WeaponType.ChargeBlade, "Mount Attack", "Both mounted hits; repeat it for the rounds you land.", 1, [new AttackHit("Mount Attack 1", 14), new AttackHit("Mount Attack 2", 16)]),
        new("mount-finisher", WeaponType.ChargeBlade, "Mount Finisher", "The four finisher hits; activates Power Axe. Its ticks are not counted.", 4,
            [new AttackHit("Mount Finisher 1", 20), new AttackHit("Mount Finisher 2", 15), new AttackHit("Mount Finisher 3", 40), new AttackHit("Mount Finisher 4", 70)]),
        // Charge Blade combos
        new("aed-saed", WeaponType.ChargeBlade, "AED, Follow-up and SAED",
            "Amped Elemental Discharge, its follow-up, then the power Super Amped Elemental Discharge, starting with five phials: one burst each, three for the SAED." + PhialNote,
            6.5, [.. Aed, .. AedFollowUp, .. SaedSwings, PhialBurst("Phial Burst Power SAED", 38, 10, 3)]) { Combo = true },

        // Insect Glaive moves (seconds are rough)
        new("rising-slash-combo", WeaponType.InsectGlaive, "Rising Slash Combo", "Both hits (element x0.7 and x0.8)." + ExtractNote, 0.9,
            [new AttackHit("Rising Slash Combo 1", 20, 0.7), new AttackHit("Rising Slash Combo 2", 14, 0.8)]),
        new("reaping-slash", WeaponType.InsectGlaive, "Reaping Slash", "Element x0.8." + ExtractNote, 0.7, [new AttackHit("Reaping Slash", 18, 0.8)]),
        new("double-slash", WeaponType.InsectGlaive, "Double Slash", "Both hits (element x0.7 and x0.8)." + ExtractNote, 0.9,
            [new AttackHit("Double Slash 1", 18, 0.7), new AttackHit("Double Slash 2", 24, 0.8)]),
        new("sidestep-slash", WeaponType.InsectGlaive, "Sidestep Slash", "Element x0.8." + ExtractNote, 0.7, [new AttackHit("Sidestep Slash", 24, 0.8)]),
        new("sidestep-slash-combo", WeaponType.InsectGlaive, "Sidestep Slash Combo", "Both hits (element x0.8)." + ExtractNote, 0.9,
            [new AttackHit("Sidestep Slash Combo 1", 14, 0.8), new AttackHit("Sidestep Slash Combo 2", 28, 0.8)]),
        new("wide-sweep", WeaponType.InsectGlaive, "Wide Sweep", "Element x0.8." + ExtractNote, 0.8, [new AttackHit("Wide Sweep", 22, 0.8)]),
        new("leaping-slash", WeaponType.InsectGlaive, "Leaping Slash", "Element x0.8." + ExtractNote, 0.8, [new AttackHit("Leaping Slash", 28, 0.8)]),
        new("overhead-smash", WeaponType.InsectGlaive, "Overhead Smash", "Element x0.8." + ExtractNote, 0.8, [new AttackHit("Overhead Smash", 26, 0.8)]),
        new("dodge-slash", WeaponType.InsectGlaive, "Dodge Slash", "Element x0.8." + ExtractNote, 0.7, [new AttackHit("Dodge Slash", 20, 0.8)]),
        new("strong-rising-slash-combo", WeaponType.InsectGlaive, "Strong Rising Slash Combo", "All three hits with red extract (element x0.8)." + ExtractNote, 1.2,
            StrongRisingSlashCombo),
        new("strong-reaping-slash", WeaponType.InsectGlaive, "Strong Reaping Slash", "Both hits with red extract (element x0.8)." + ExtractNote, 1, StrongReapingSlash),
        new("strong-double-slash", WeaponType.InsectGlaive, "Strong Double Slash", "All three hits with red extract (element x0.8, the last x1)." + ExtractNote, 1.3,
            StrongDoubleSlash),
        new("strong-sidestep-slash", WeaponType.InsectGlaive, "Strong Sidestep Slash", "With Triple Up (element x0.8)." + ExtractNote, 0.7, [new AttackHit("Strong Sidestep Slash", 26, 0.8)]),
        new("strong-sidestep-slash-combo", WeaponType.InsectGlaive, "Strong Sidestep Slash Combo", "Both hits with Triple Up (element x0.8)." + ExtractNote, 0.9,
            [new AttackHit("Strong Sidestep Slash Combo 1", 18, 0.8), new AttackHit("Strong Sidestep Slash Combo 2", 30, 0.8)]),
        new("strong-wide-sweep", WeaponType.InsectGlaive, "Strong Wide Sweep", "Both hits with red extract (element x0.8)." + ExtractNote, 1,
            [new AttackHit("Strong Wide Sweep 1", 12, 0.8), new AttackHit("Strong Wide Sweep 2", 24, 0.8)]),
        new("tornado-slash", WeaponType.InsectGlaive, "Tornado Slash", "Both hits with red extract." + ExtractNote, 1.2, TornadoSlash),
        new("jumping-slash", WeaponType.InsectGlaive, "Jumping Slash", "An aerial slash; Vaulting Dance multipliers are not counted." + ExtractNote, 0.7,
            [new AttackHit("Jumping Slash", 18)]),
        new("jumping-advancing-slash", WeaponType.InsectGlaive, "Jumping Advancing Slash", "Element x0.8; Vaulting Dance multipliers are not counted." + ExtractNote, 0.8,
            [new AttackHit("Jumping Advancing Slash", 26, 0.8)]),
        new("strong-jumping-slash", WeaponType.InsectGlaive, "Strong Jumping Slash", "One hit of the multihit (14 MV, element x0.6); Vaulting Dance multipliers are not counted." + ExtractNote, 0.7,
            [new AttackHit("Strong Jumping Slash", 14, 0.6)]),
        new("strong-jumping-advancing-slash", WeaponType.InsectGlaive, "Strong Jumping Advancing Slash",
            "All five hits (element x0.5 and x0.8); Vaulting Dance multipliers are not counted." + ExtractNote, 1,
            [new AttackHit("Strong Jumping Advancing Slash 1-4", 9, 0.5) { Count = 4 }, new AttackHit("Strong Jumping Advancing Slash 5", 32, 0.8)]),
        new("scaling-double-slash", WeaponType.InsectGlaive, "Scaling Double Slash", "Both hits (element x0.8)." + ExtractNote, 1,
            [new AttackHit("Scaling Double Slash 1", 12, 0.8), new AttackHit("Scaling Double Slash 2", 20, 0.8)]),
        .. GlaiveLevels.Select(l => new AttackDefinition($"descending-slash-{l.Level}", WeaponType.InsectGlaive, $"Descending Slash Lv{l.Level}",
            "All four hits; the level comes from the charge or the attack before it." + ExtractNote, 1.5,
            DescendingSlash.Select((mv, i) => new AttackHit($"Descending Slash {i + 1} Lv{l.Level}", Math.Round(mv * l.Scale, 2))).ToList())),
        .. GlaiveLevels.Select(l => new AttackDefinition($"strong-descending-slash-{l.Level}", WeaponType.InsectGlaive, $"Strong Descending Slash Lv{l.Level}",
            "All six hits with red extract; the level comes from the charge or the attack before it." + ExtractNote, 1.8,
            StrongDescendingSlash.Select((mv, i) => new AttackHit($"Strong Descending Slash {i + 1} Lv{l.Level}", Math.Round(mv * l.Scale, 2))).ToList())),
        new("descending-thrust-1", WeaponType.InsectGlaive, "Descending Thrust Lv1",
            "All four hits; the third marks the monster (blunt, no element). Vaulting Dance multipliers are not counted." + ExtractNote, 1.5,
            [new AttackHit("Descending Thrust 1 Lv1", 21), new AttackHit("Descending Thrust 2 Lv1", 7.5), new AttackHit("Descending Thrust 3 Lv1", 3.75, 0),
             new AttackHit("Descending Thrust 4 Lv1", 18)]),
        new("descending-thrust-2", WeaponType.InsectGlaive, "Descending Thrust Lv2",
            "All four hits; the third marks the monster (blunt, no element). Vaulting Dance multipliers are not counted." + ExtractNote, 1.5,
            [new AttackHit("Descending Thrust 1 Lv2", 28), new AttackHit("Descending Thrust 2 Lv2", 10), new AttackHit("Descending Thrust 3 Lv2", 5, 0),
             new AttackHit("Descending Thrust 4 Lv2", 24)]),
        new("strong-descending-thrust-1", WeaponType.InsectGlaive, "Strong Descending Thrust Lv1",
            "All six hits with red extract; the third marks the monster (blunt, no element). Vaulting Dance multipliers are not counted." + ExtractNote, 1.8,
            [new AttackHit("Strong Descending Thrust 1 Lv1", 21), new AttackHit("Strong Descending Thrust 2 Lv1", 7.5), new AttackHit("Strong Descending Thrust 3 Lv1", 3.75, 0),
             new AttackHit("Strong Descending Thrust 4 Lv1", 13), new AttackHit("Strong Descending Thrust 5 Lv1", 13), new AttackHit("Strong Descending Thrust 6 Lv1", 21)]),
        new("strong-descending-thrust-2", WeaponType.InsectGlaive, "Strong Descending Thrust Lv2",
            "All six hits with red extract; the third marks the monster (blunt, no element). Vaulting Dance multipliers are not counted." + ExtractNote, 1.8,
            [new AttackHit("Strong Descending Thrust 1 Lv2", 28), new AttackHit("Strong Descending Thrust 2 Lv2", 10), new AttackHit("Strong Descending Thrust 3 Lv2", 5, 0),
             new AttackHit("Strong Descending Thrust 4 Lv2", 18), new AttackHit("Strong Descending Thrust 5 Lv2", 18), new AttackHit("Strong Descending Thrust 6 Lv2", 28)]),
        new("rising-spiral-slash-1", WeaponType.InsectGlaive, "Rising Spiral Slash Lv1",
            "Eight hits of 48 MV (element x0.8); it takes the level of the Strong Descending attack before it (Lv0 becomes Lv1)." + ExtractNote, 2, [RisingSpiralSlash(1)]),
        new("rising-spiral-slash-2", WeaponType.InsectGlaive, "Rising Spiral Slash Lv2",
            "Eight hits of 64 MV (element x0.8); it takes the level of the Strong Descending attack before it." + ExtractNote, 2, [RisingSpiralSlash(2)]),
        new("leaping-thrust", WeaponType.InsectGlaive, "Leaping Thrust", "All four hits; the last is blunt (counted at the slash hitzone)." + ExtractNote, 1.5,
            [new AttackHit("Leaping Thrust 1", 16), new AttackHit("Leaping Thrust 2", 18, 0.8), new AttackHit("Leaping Thrust Extra Hit", 18, 0.8), new AttackHit("Leaping Thrust 3", 8, 0.8)]),
        new("seikret-attack", WeaponType.InsectGlaive, "Seikret Attack", "Both attacks from the Seikret.", 1.5, [new AttackHit("Seikret Attack I", 12), new AttackHit("Seikret Attack II", 16)]),
        new("seikret-hold-attack", WeaponType.InsectGlaive, "Seikret Hold Attack", "All four hits from the Seikret.", 2,
            [new AttackHit("Seikret Hold Attack 1", 5), new AttackHit("Seikret Hold Attack 2", 5), new AttackHit("Seikret Hold Attack 3", 5), new AttackHit("Seikret Hold Attack 4", 10)]),
        new("dismount-attack", WeaponType.InsectGlaive, "Dismount Attack", "All three hits of the attack off the Seikret.", 1.5,
            [new AttackHit("Dismount Attack 1", 12), new AttackHit("Dismount Attack 2", 8), new AttackHit("Dismount Attack 3", 24)]),
        new("mount-attack", WeaponType.InsectGlaive, "Mount Attack", "Both mounted hits; repeat it for the rounds you land.", 1, [new AttackHit("Mount Attack 1", 25), new AttackHit("Mount Attack 2", 20)]),
        new("mount-finisher", WeaponType.InsectGlaive, "Mount Finisher", "All ten hits.", 4,
            [new AttackHit("Mount Finisher 1", 40), new AttackHit("Mount Finisher 2-9 (6 MV)", 6) { Count = 4 }, new AttackHit("Mount Finisher 2-9 (8 MV)", 8) { Count = 4 },
             new AttackHit("Mount Finisher 10", 50)]),
        // Insect Glaive combos
        new("strong-ground-combo", WeaponType.InsectGlaive, "Strong combo into Tornado Slash",
            "With red extract: Strong Rising Slash Combo, Strong Reaping Slash, Strong Double Slash, Tornado Slash." + ExtractNote, 4.7,
            [.. StrongRisingSlashCombo, .. StrongReapingSlash, .. StrongDoubleSlash, .. TornadoSlash]) { Combo = true },
        new("descending-rising-spiral", WeaponType.InsectGlaive, "Strong Descending Slash Lv2 into Rising Spiral Slash",
            "A Lv2 Strong Descending Slash (all six hits), then the Lv2 Rising Spiral Slash." + ExtractNote, 3.8,
            [.. StrongDescendingSlash.Select((mv, i) => new AttackHit($"Strong Descending Slash {i + 1} Lv2", mv)), RisingSpiralSlash(2)]) { Combo = true },
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
        new("echo-dragon-loop", WeaponType.HuntingHorn, "Echo bubbles into Echo Wave (dragon) performances",
            "Three Echo Bubbles on the monster, Overhead Smash, Hilt Stab, Backwards Strike, a Forward Perform of the dragon Echo Wave melody, a timed Performance Beat I and a timed Encore, which fires the Lv1 Echo Wave. Each attack gets one bubble follow-up per bubble (assumed: damage grows with the bubbles on the monster). Edit the steps to your horn and rotation.",
            [
                new SequenceStep { Attack = "echo-bubble", Repeat = 3 },
                new SequenceStep { Attack = "overhead-smash" },
                new SequenceStep { Attack = "echo-bubble-note", Repeat = 3 },
                new SequenceStep { Attack = "hilt-stab" },
                new SequenceStep { Attack = "echo-bubble-note", Repeat = 3 },
                new SequenceStep { Attack = "backwards-strike" },
                new SequenceStep { Attack = "echo-bubble-note", Repeat = 3 },
                new SequenceStep { Attack = "forward-perform" },
                new SequenceStep { Attack = "echo-bubble-song", Repeat = 3 },
                new SequenceStep { Attack = "performance-beat-i-timed" },
                new SequenceStep { Attack = "echo-bubble-song", Repeat = 3 },
                new SequenceStep { Attack = "encore-timed" },
                new SequenceStep { Attack = "echo-bubble-song", Repeat = 3 },
                new SequenceStep { Attack = "echo-wave-dragon-1" },
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
        WeaponType.SwordAndShield => "perfect-rush-timed",
        WeaponType.DualBlades => "demon-combo",
        WeaponType.Hammer => "mighty-spinslam",
        WeaponType.HuntingHorn => "beat-encore",
        WeaponType.Lance => "mid-thrust-combo",
        WeaponType.Gunlance => "rising-slash-shell-combo",
        WeaponType.ChargeBlade => "saed",
        WeaponType.InsectGlaive => "strong-ground-combo",
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

    /// <summary>The Charge Blade phial follows the Gogma focus (attack: Impact, affinity and element: Element); plain stats: by element, like <see cref="PhialOf"/>.</summary>
    public static ChargeBladePhial ChargeBladePhialOf(GogmaFocus? focus, Element element) => focus switch
    {
        GogmaFocus.Attack => ChargeBladePhial.Impact,
        GogmaFocus.Affinity or GogmaFocus.Element => ChargeBladePhial.Element,
        _ => element == Element.None ? ChargeBladePhial.Impact : ChargeBladePhial.Element,
    };

    /// <summary>The Gunlance shelling follows the Gogma focus (attack: Normal, affinity: Long, element: Wide); plain stats: Normal.</summary>
    public static GunlanceShelling ShellingOf(GogmaFocus? focus) => focus switch
    {
        GogmaFocus.Affinity => GunlanceShelling.Long,
        GogmaFocus.Element => GunlanceShelling.Wide,
        _ => GunlanceShelling.Normal,
    };
}
