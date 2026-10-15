using System.Reflection;
using System.Text.Json;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// How the hunter attacks: the attack the score is computed for (<see cref="Attacks"/>), or for weapon types without attack data
/// the average-hit model (every hit one element hit of <see cref="AverageMv"/>). Unset values fall back to the weapon type's
/// default attack and preset (<see cref="Preset"/>). The score is damage per 100 MV of the attack on the raw-hitzone-100 scale
/// of EFR: the target's element hitzone / raw hitzone weighs the element. With <see cref="Attacks.Sequence"/> the attack is the
/// <see cref="Sequence"/> of steps, each step scored under its own condition overrides (see <see cref="Conditions.Segments"/>).
/// For one attack or sequence the score is its real damage times a constant (<see cref="ResolvedAttackProfile.DamagePerExecution"/>),
/// so it ranks builds like the damage the whole attack or sequence deals.
/// </summary>
public sealed record AttackProfile
{
    /// <summary>An attack id of the weapon type (<see cref="Attacks.All"/>), <see cref="Attacks.Average"/> or <see cref="Attacks.Sequence"/>; null: the weapon type's default.</summary>
    public string? Attack { get; init; }
    /// <summary>The steps of a custom sequence (used when <see cref="Attack"/> is <see cref="Attacks.Sequence"/>).</summary>
    public IReadOnlyList<SequenceStep>? Sequence { get; init; }
    /// <summary>Landed hits per minute of attacking; cooldown-limited procs fire at most once per cooldown. Default: from the attack's duration.</summary>
    public double? HitsPerMinute { get; init; }
    /// <summary>Average-hit model only: average motion value of a landed hit.</summary>
    public double? AverageMv { get; init; }
    /// <summary>Average-hit model only: share of landed hits that are Lv3 charged slashes other than the Rising Slash (Great Sword, Dark Arts shockwave).</summary>
    public double? ChargedLv3Share { get; init; }
    /// <summary>Bow with the Pierce coating: hits each arrow lands as it passes through the monster (1..8; more on big monsters). Default <see cref="DamageConstants.DefaultPierceHits"/>.</summary>
    public int? PierceHits { get; init; }

    public bool IsSequence => string.Equals(Attack, Attacks.Sequence, StringComparison.OrdinalIgnoreCase);

    /// <summary>What the name of a sequence starts with, before its <see cref="Attacks.StepList"/>.</summary>
    public const string SequencePrefix = "Sequence: ";

    /// <summary>The attack definition this profile scores <paramref name="type"/> with; null for the average-hit model and for sequences.</summary>
    public AttackDefinition? AttackFor(WeaponType type) =>
        string.Equals(Attack, Attacks.Average, StringComparison.OrdinalIgnoreCase) || IsSequence ? null : Attacks.Find(type, Attack ?? Attacks.DefaultFor(type));

    /// <summary>The hits, duration and name of the attack or sequence; null for the average-hit model.</summary>
    private (string Name, double Seconds, List<AttackHit> Hits)? HitsFor(WeaponType type)
    {
        if (IsSequence)
        {
            var steps = (Sequence ?? []).Select(st => (Step: st, Def: Attacks.Find(type, st.Attack))).Where(x => x.Def is not null && x.Step.Repeat > 0).ToList();
            if (steps.Count == 0) return null;
            var hits = steps.SelectMany(x => Enumerable.Repeat(x.Def!.Hits, x.Step.Repeat).SelectMany(h => h)).ToList();
            var name = SequencePrefix + Attacks.StepList(type, steps.Select(x => x.Step));
            return (name, steps.Sum(x => x.Def!.Seconds * x.Step.Repeat), hits);
        }
        return AttackFor(type) is { } a ? (a.Name, a.Seconds, a.Hits.ToList()) : null;
    }

    /// <param name="gauge">Long Sword: the Spirit Gauge of the conditions, for hits without their own (<see cref="AttackHit.Spirit"/>).</param>
    public ResolvedAttackProfile Resolve(GogmaWeaponStats weapon, Target target, SpiritGauge gauge = SpiritGauge.None)
    {
        var p = Preset(weapon.Type);
        var ratio = target.ElementRatio(weapon.Element);
        var sharpRaw = weapon.TopSharpness is { } s ? DamageConstants.SharpnessRaw(s) : 1.0;
        var sharpEle = weapon.TopSharpness is { } s2 ? DamageConstants.SharpnessElement(s2) : 1.0;

        if (HitsFor(weapon.Type) is not { } attack)
        {
            var mv = AverageMv ?? p.AverageMv;
            return new ResolvedAttackProfile("Average hit", HitsPerMinute ?? p.HitsPerMinute, 1, mv, sharpRaw, sharpEle * ratio * 100.0 / mv,
                ChargedLv3Share ?? p.ChargedLv3Share, ratio, target.RawHitzone, Execution: "hit");
        }

        double hits = 0, totalMv = 0, raw = 0, ele = 0, rawNoCrit = 0, eleNoCrit = 0, fixedDamage = 0, shockwaves = 0;
        var resolved = ResolveHits(weapon, target, attack.Hits, PierceHits, gauge);
        foreach (var h in resolved)
        {
            hits += h.Count;
            totalMv += h.Mv * h.Count;
            if (h.NoCrit)
            {
                rawNoCrit += h.Mv * h.Count * h.RawModifier;
                eleNoCrit += h.Count * h.ElementModifier;
            }
            else
            {
                raw += h.Mv * h.Count * h.RawModifier;
                ele += h.Count * h.ElementModifier;
            }
            fixedDamage += h.Fixed * h.Count;
            if (h.ChargedLv3) shockwaves += h.Count;
        }

        var name = weapon.Type switch
        {
            WeaponType.SwitchAxe => $"{attack.Name} ({Attacks.PhialOf(weapon.Focus, weapon.Element)} phial)",
            WeaponType.ChargeBlade => $"{attack.Name} ({Attacks.ChargeBladePhialOf(weapon.Focus, weapon.Element)} phial)",
            WeaponType.Gunlance => $"{attack.Name} ({Attacks.ShellingOf(weapon.Focus)} shelling Lv{DamageConstants.ArtianShellLevel})",
            WeaponType.Bow when weapon.Coating is { } coating => $"{attack.Name} ({CoatingName(coating)} coating)",
            _ => attack.Name,
        };
        // fixed damage ignores the hitzone: on the raw-hitzone-100 scale of the score it counts as more on a softer target
        return new ResolvedAttackProfile(name, HitsPerMinute ?? hits * 60.0 / attack.Seconds, hits, totalMv, raw / totalMv, ele * ratio * 100.0 / totalMv,
            shockwaves, ratio, target.RawHitzone, IsSequence ? "sequence" : "attack", ShotShares.Of(resolved.Where(h => !h.NoCrit)))
        {
            NoCritRawFactor = rawNoCrit / totalMv,
            NoCritElementFactor = eleNoCrit * ratio * 100.0 / totalMv,
            FixedDamage = fixedDamage * 100.0 / target.RawHitzone,
        };
    }

    /// <summary>"Close-range" / "Power" / "Pierce", as the game names the coatings.</summary>
    public static string CoatingName(BowCoating coating) => coating switch
    {
        BowCoating.CloseRange => "Close-range",
        BowCoating.Power => "Power",
        _ => "Pierce",
    };

    /// <summary>
    /// The hits as they land on <paramref name="target"/> with <paramref name="weapon"/>: the power True Charged Slash finisher on a
    /// weak spot (raw hitzone x sharpness of 45 or more), fixed sharpness (the tackle), the Switch Axe phial and the bow's coating
    /// (with the Pierce coating every arrow lands <paramref name="pierceHits"/> times, each hit weaker than the one before), the Long Sword's
    /// Spirit Gauge (the hit's own or <paramref name="gauge"/>), the Gunlance shelling and the Charge Blade phial. A hit that ignores the
    /// hitzone gets 100 / raw hitzone on its raw modifier. Raw
    /// damage of a hit is true raw x MV / 100 x <see cref="ResolvedHit.RawModifier"/>, element damage true element x
    /// <see cref="ResolvedHit.ElementModifier"/>, both before hitzones, crits and shot skills.
    /// </summary>
    public static IReadOnlyList<ResolvedHit> ResolveHits(GogmaWeaponStats weapon, Target target, IEnumerable<AttackHit> hits, int? pierceHits = null, SpiritGauge gauge = SpiritGauge.None)
    {
        var sharpRaw = weapon.TopSharpness is { } s ? DamageConstants.SharpnessRaw(s) : 1.0;
        var sharpEle = weapon.TopSharpness is { } s2 ? DamageConstants.SharpnessElement(s2) : 1.0;
        SwitchAxePhial? phial = weapon.Type == WeaponType.SwitchAxe ? Attacks.PhialOf(weapon.Focus, weapon.Element) : null;
        BowCoating? coating = weapon.Type == WeaponType.Bow ? weapon.Coating : null;
        ChargeBladePhial? cbPhial = weapon.Type == WeaponType.ChargeBlade ? Attacks.ChargeBladePhialOf(weapon.Focus, weapon.Element) : null;
        GunlanceShelling? shelling = weapon.Type == WeaponType.Gunlance ? Attacks.ShellingOf(weapon.Focus) : null;
        var weakSpot = target.RawHitzone * sharpRaw >= DamageConstants.WeakPointHitzone;
        var result = new List<ResolvedHit>();
        foreach (var hit in hits)
        {
            var notes = new List<string>();
            var h = hit;
            if (weakSpot && hit.Power is { } power) { h = power; notes.Add("power version on a weak spot"); }
            var sr = sharpRaw;
            var se = sharpEle;
            double fixedDamage = 0;
            if (h.Shell is { } shot && shelling is { } shellType)
            {
                var (mv, fire) = DamageConstants.Shell(shellType, shot, DamageConstants.ArtianShellLevel);
                if (shot == GunlanceShot.WyrmstakeTick)
                    h = h with { Mv = mv };
                else
                {
                    h = h with { Mv = mv, Element = 0, NoCrit = true, IgnoresHitzone = true };
                    sr = 1.0;
                    fixedDamage = fire;
                    notes.Add(FormattableString.Invariant($"{shellType} shelling Lv{DamageConstants.ArtianShellLevel}: {mv} MV without sharpness, + {fire} fixed fire"));
                }
            }
            if (h.PhialBurstElement is { } burstElement && cbPhial is { } cb)
            {
                h = cb == ChargeBladePhial.Element ? h with { Mv = 0, Element = burstElement } : h with { Element = 0 };
                notes.Add(cb == ChargeBladePhial.Element ? FormattableString.Invariant($"Element phial: element x{burstElement}, no raw") : "Impact phial: raw, no element");
            }
            if (h.IgnoresSharpness)
            {
                sr = se = 1.0;
                notes.Add("ignores sharpness");
            }
            if (h.FixedElement is { } fe)
            {
                var hz = target.ElementHitzoneFor(fe.Element);
                fixedDamage += fe.Value * hz / 100.0;
                notes.Add(FormattableString.Invariant($"fixed {fe.Value} {fe.Element.ToString().ToLowerInvariant()} element at hitzone {hz:0.#}"));
            }
            if (h.FixedSharpness is { } fixedSharpness)
            {
                sr = DamageConstants.SharpnessRaw(fixedSharpness);
                se = DamageConstants.SharpnessElement(fixedSharpness);
                notes.Add($"{fixedSharpness.ToString().ToLowerInvariant()} sharpness");
            }
            var count = h.Count;
            var rawMult = phial == SwitchAxePhial.Power && h.SwordMode ? DamageConstants.PowerPhialRaw : 1.0;
            var eleMult = phial == SwitchAxePhial.Element && h.SwordMode ? DamageConstants.ElementPhialElement : 1.0;
            var eleMod = phial == SwitchAxePhial.Element && h.ElementPhialElement is { } ep ? ep : h.Element;
            if (rawMult != 1.0) notes.Add(FormattableString.Invariant($"Power phial raw x{rawMult}"));
            if (eleMult != 1.0) notes.Add(FormattableString.Invariant($"Element phial element x{eleMult}"));
            if (h.Coated && coating is { } c)
                (h, count, rawMult, eleMult, eleMod) = Coat(h, c, pierceHits ?? DamageConstants.DefaultPierceHits, notes);
            if (h.CriticalDistance)
            {
                rawMult *= DamageConstants.CriticalDistanceRaw;
                notes.Add(FormattableString.Invariant($"critical distance raw x{DamageConstants.CriticalDistanceRaw}"));
            }
            if (weapon.Type == WeaponType.LongSword && DamageConstants.SpiritGaugeRaw(h.Spirit ?? gauge) is var spirit && spirit != 1.0)
            {
                rawMult *= spirit;
                notes.Add(FormattableString.Invariant($"{(h.Spirit ?? gauge).ToString().ToLowerInvariant()} Spirit Gauge raw x{spirit}"));
            }
            if (h.IgnoresHitzone)
            {
                rawMult *= 100.0 / target.RawHitzone;
                notes.Add("raw ignores the hitzone");
            }
            if (h.NoCrit) notes.Add("cannot crit");
            if (h.ChargedLv3) notes.Add("Lv3 charged slash");
            result.Add(new ResolvedHit(h.Name, count, h.Mv, sr * rawMult, eleMod * se * eleMult, h.ChargedLv3, notes, h.Shot, h.NoCrit, fixedDamage));
        }
        return result;
    }

    /// <summary>
    /// A coated bow arrow: Close-range and Power coatings multiply its raw; with the Pierce coating it becomes the pierce arrow, which
    /// lands <paramref name="pierceHits"/> times per arrow, each hit weaker (<see cref="DamageConstants.PierceHitMultipliers"/>).
    /// </summary>
    private static (AttackHit Hit, int Count, double RawMult, double EleMult, double EleMod) Coat(AttackHit h, BowCoating coating, int pierceHits, List<string> notes)
    {
        switch (coating)
        {
            case BowCoating.Pierce when h.Pierce is { } p:
            {
                var n = Math.Clamp(pierceHits, 1, DamageConstants.PierceHitMultipliers.Length);
                var curve = DamageConstants.PierceHitMultipliers.Take(n).Average();
                notes.Add(FormattableString.Invariant($"Pierce coating: {n} hits per arrow, x{curve:0.###} on average"));
                return (p, p.Count * n, curve, curve, p.Element);
            }
            case BowCoating.CloseRange:
                notes.Add(FormattableString.Invariant($"Close-range coating raw x{DamageConstants.CloseRangeCoatingRaw}"));
                return (h, h.Count, DamageConstants.CloseRangeCoatingRaw, 1.0, h.Element);
            case BowCoating.Power:
                notes.Add(FormattableString.Invariant($"Power coating raw x{DamageConstants.PowerCoatingRaw}"));
                return (h, h.Count, DamageConstants.PowerCoatingRaw, 1.0, h.Element);
            default:
                return (h, h.Count, 1.0, 1.0, h.Element);
        }
    }

    /// <summary>
    /// Average-hit model starting points: Great Sword True Charged Slash play, Long Sword spirit-combo play (rough). Great Sword
    /// damage comes from fully charged slashes, so every hit counts as one, valued as the Lv3 True Charged Slash finisher: 209 MV
    /// (datamine 1.040).
    /// </summary>
    public static AveragePreset Preset(WeaponType type) => type switch
    {
        WeaponType.GreatSword => new(24, 209, 1.0),
        WeaponType.LongSword => new(50, 35, 0),
        _ => new(40, 50, 0),
    };

    public IReadOnlyList<string> Validate(WeaponType type)
    {
        var errors = new List<string>();
        if (Attack is { } id && !IsSequence && !string.Equals(id, Attacks.Average, StringComparison.OrdinalIgnoreCase) && Attacks.Find(type, id) is null)
            errors.Add($"conditions.attack_profile.attack '{id}' is not a {type} attack (known: {string.Join(", ", Attacks.For(type).Select(x => x.Id).Append(Attacks.Average).Append(Attacks.Sequence))}).");
        if (PierceHits is < 1 or > 8) errors.Add($"conditions.attack_profile.pierce_hits must be 1..{DamageConstants.PierceHitMultipliers.Length}.");
        if (!IsSequence) return errors;
        if (Sequence is not { Count: > 0 } steps)
        {
            errors.Add("conditions.attack_profile.sequence needs at least one step.");
            return errors;
        }
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (Attacks.Find(type, step.Attack) is null) errors.Add($"conditions.attack_profile.sequence step {i + 1}: '{step.Attack}' is not a {type} attack.");
            if (step.Repeat is < 1 or > 20) errors.Add($"conditions.attack_profile.sequence step {i + 1}: repeat must be 1..20.");
            foreach (var key in step.Conditions.Keys)
                if (!ConditionToggles.StepKeys.Contains(key))
                    errors.Add($"conditions.attack_profile.sequence step {i + 1}: '{key}' is not a condition a step can change (known: {string.Join(", ", ConditionToggles.StepKeys)}).");
        }
        return errors;
    }

    public bool Equals(AttackProfile? other) =>
        other is not null && Attack == other.Attack && HitsPerMinute == other.HitsPerMinute && AverageMv == other.AverageMv
        && ChargedLv3Share == other.ChargedLv3Share && PierceHits == other.PierceHits && (Sequence ?? []).SequenceEqual(other.Sequence ?? []);

    public override int GetHashCode() => HashCode.Combine(Attack, HitsPerMinute, AverageMv, ChargedLv3Share, PierceHits, (Sequence ?? []).Count);
}

/// <summary>
/// One step of an attack sequence: an attack (move or combo) of the weapon type, repeated <see cref="Repeat"/> times, under
/// <see cref="Conditions"/> overrides of the hunt toggles (e.g. <c>{ "stamina_full": false }</c> after a tackle used stamina).
/// </summary>
public sealed record SequenceStep
{
    public required string Attack { get; init; }
    public int Repeat { get; init; } = 1;
    /// <summary>Hunt conditions that differ during this step, by condition key (snake_case, see <see cref="ConditionToggles.StepKeys"/>).</summary>
    public Dictionary<string, bool> Conditions { get; init; } = new();

    public bool Equals(SequenceStep? other) =>
        other is not null && Attack == other.Attack && Repeat == other.Repeat
        && Conditions.Count == other.Conditions.Count && Conditions.All(kv => other.Conditions.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public override int GetHashCode() => HashCode.Combine(Attack, Repeat, Conditions.Count);
}

/// <summary>
/// A custom sequence saved under a name in the profile, per weapon type: the Conditions tab picks it again, and wherever a
/// sequence with the same steps is shown, it shows by this name.
/// </summary>
public sealed record SavedSequence
{
    public required string Name { get; init; }
    public required IReadOnlyList<SequenceStep> Steps { get; init; }
}

/// <summary>The on/off hunt conditions of <see cref="Damage.Conditions"/> by snake_case key, for sequence step and build overrides.</summary>
public static class ConditionToggles
{
    private static readonly string[] NotPerStep =
        [nameof(Damage.Conditions.FullHealth), nameof(Damage.Conditions.RedHealth), nameof(Damage.Conditions.LowHealth), nameof(Damage.Conditions.DarkArtsShockwave), nameof(Damage.Conditions.BadBlood),
         nameof(Damage.Conditions.Powercharm)];

    private static readonly Dictionary<string, PropertyInfo> ByKey = typeof(Conditions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
        .ToDictionary(p => Key(p.Name), p => p);

    /// <summary>Every on/off condition key (a hand-entered build can override any of them).</summary>
    public static IReadOnlyCollection<string> AllKeys => ByKey.Keys;

    /// <summary>The condition keys a sequence step can change. Health stays the same for the whole sequence; proc damage is all or nothing.</summary>
    public static IReadOnlyCollection<string> StepKeys { get; } = ByKey.Keys.Where(k => !NotPerStep.Contains(ByKey[k].Name)).ToList();

    public static string Key(string propertyName) => JsonNamingPolicy.SnakeCaseLower.ConvertName(propertyName);

    public static bool Get(Conditions c, string key) => ByKey.TryGetValue(key, out var p) && (bool)p.GetValue(c)!;

    /// <summary><paramref name="c"/> with the overrides applied; unknown keys are ignored (validation reports them).</summary>
    public static Conditions With(Conditions c, IReadOnlyDictionary<string, bool> overrides)
    {
        if (overrides.Count == 0) return c;
        var result = c with { };
        foreach (var (key, value) in overrides)
            if (ByKey.TryGetValue(key, out var p)) p.SetValue(result, value);
        return result;
    }
}

public readonly record struct AveragePreset(double HitsPerMinute, double AverageMv, double ChargedLv3Share);

/// <summary>Raw multipliers of the bow's shot skills per kind of arrow (1 where no skill applies); see <see cref="BowShot"/>.</summary>
public readonly record struct ShotBoosts(double Normal, double Power, double Dragon, double Tracer)
{
    public static ShotBoosts None => new(1, 1, 1, 1);

    public double For(BowShot? shot) => shot switch
    {
        BowShot.Normal => Normal,
        BowShot.Power => Power,
        BowShot.Dragon => Dragon,
        BowShot.Tracer => Tracer,
        _ => 1.0,
    };
}

/// <summary>Share of an attack's raw (MV x raw modifier) per kind of bow arrow; the rest gets no shot skill. All zero for melee attacks.</summary>
public readonly record struct ShotShares(double Normal, double Power, double Dragon, double Tracer)
{
    public bool Any => Normal > 0 || Power > 0 || Dragon > 0 || Tracer > 0;

    /// <summary>The attack's raw multiplier from the shot skills: each kind's share at its boost, the rest at 1.</summary>
    public double Multiplier(ShotBoosts b) => 1.0 + Normal * (b.Normal - 1) + Power * (b.Power - 1) + Dragon * (b.Dragon - 1) + Tracer * (b.Tracer - 1);

    public static ShotShares Of(IEnumerable<ResolvedHit> hits)
    {
        double total = 0, normal = 0, power = 0, dragon = 0, tracer = 0;
        foreach (var h in hits)
        {
            var raw = h.Mv * h.Count * h.RawModifier;
            total += raw;
            switch (h.Shot)
            {
                case BowShot.Normal: normal += raw; break;
                case BowShot.Power: power += raw; break;
                case BowShot.Dragon: dragon += raw; break;
                case BowShot.Tracer: tracer += raw; break;
            }
        }
        return total > 0 ? new ShotShares(normal / total, power / total, dragon / total, tracer / total) : default;
    }
}

/// <summary>One hit of an attack as it lands (<see cref="AttackProfile.ResolveHits"/>).</summary>
/// <param name="RawModifier">Raw sharpness modifier x phial or coating (multiplies true raw x MV / 100).</param>
/// <param name="ElementModifier">Element modifier x element sharpness x phial (multiplies true element).</param>
/// <param name="Notes">What changed the hit: power version, fixed sharpness, phial, coating, Lv3 charged slash.</param>
/// <param name="Shot">Bow arrows: the kind, which decides the shot skill that boosts its raw (<see cref="ShotBoosts"/>).</param>
/// <param name="NoCrit">The hit never crits (nor is feeble).</param>
/// <param name="Fixed">Fixed damage of one hit, the same for every build (Gunlance shell fire, Hunting Horn Echo Wave element).</param>
public sealed record ResolvedHit(string Name, int Count, double Mv, double RawModifier, double ElementModifier, bool ChargedLv3, IReadOnlyList<string> Notes,
    BowShot? Shot = null, bool NoCrit = false, double Fixed = 0);

/// <summary>
/// The attack as the calculator uses it, per execution of the attack:
/// EFR = true raw x crit factor x <see cref="RawFactor"/>; EFE = element x crit element factor x <see cref="ElementFactor"/>.
/// </summary>
/// <param name="Hits">Landed hits per execution.</param>
/// <param name="TotalMv">Motion value of one execution.</param>
/// <param name="RawFactor">MV-weighted raw sharpness modifier of the hits, with the Power phial on sword-mode slashes.</param>
/// <param name="ElementFactor">Element per true element point per 100 MV: sum over hits of element modifier x element sharpness x phial, x element hitzone ratio x 100 / total MV.</param>
/// <param name="Shockwaves">Lv3 charged slashes per execution (Dark Arts shockwave).</param>
/// <param name="ElementHitzoneRatio">The target's element hitzone / raw hitzone for the weapon's element.</param>
/// <param name="RawHitzone">The target's raw hitzone: takes a score off the raw-hitzone-100 scale (<see cref="DamagePerExecution"/>).</param>
/// <param name="Execution">What one execution is: "attack", "sequence" or "hit" (average-hit model).</param>
/// <param name="Shots">Bow attacks: the share of their raw per kind of arrow, for the shot skills.</param>
public readonly record struct ResolvedAttackProfile(
    string Name, double HitsPerMinute, double Hits, double TotalMv, double RawFactor, double ElementFactor, double Shockwaves, double ElementHitzoneRatio,
    double RawHitzone = 100, string Execution = "attack", ShotShares Shots = default)
{
    /// <summary>Like <see cref="RawFactor"/> for the hits that cannot crit: their raw per 100 MV is true raw x this.</summary>
    public double NoCritRawFactor { get; init; }
    /// <summary>Like <see cref="ElementFactor"/> for the hits that cannot crit.</summary>
    public double NoCritElementFactor { get; init; }
    /// <summary>Fixed damage of one execution (shell fire, Echo Wave element), on the raw-hitzone-100 scale; a proc of <see cref="DamageCalculator"/>.</summary>
    public double FixedDamage { get; init; }

    public double SecondsPerHit => 60.0 / HitsPerMinute;

    public double AverageMv => TotalMv / Hits;

    /// <summary>Converts damage dealt once per execution into damage per 100 MV of landed attacks.</summary>
    public double PerHundredMv => 100.0 / TotalMv;

    /// <summary>Executions of the attack (or sequence) per minute of attacking, from the landed hits per minute.</summary>
    public double ExecutionsPerMinute => HitsPerMinute / Hits;

    /// <summary>
    /// The damage one execution of the attack deals to the target for a <paramref name="score"/> (per 100 MV, raw-hitzone-100
    /// scale), before the monster's defense rate and per-hit rounding. A constant factor per attack and target, so it ranks
    /// builds exactly like the score.
    /// </summary>
    public double DamagePerExecution(double score) => score * TotalMv / 100.0 * RawHitzone / 100.0;

    /// <summary><see cref="DamagePerExecution"/> over a minute of attacking at <see cref="HitsPerMinute"/>.</summary>
    public double DamagePerMinute(double score) => DamagePerExecution(score) * ExecutionsPerMinute;

    /// <summary>Procs per landed hit for an effect that triggers on a hit and then waits <paramref name="cooldownSeconds"/> before it can trigger again.</summary>
    public double ProcsPerHit(double cooldownSeconds) => Math.Min(1.0, SecondsPerHit / cooldownSeconds);
}
