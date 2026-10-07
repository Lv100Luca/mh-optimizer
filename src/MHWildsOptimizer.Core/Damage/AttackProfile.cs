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

    public bool IsSequence => string.Equals(Attack, Attacks.Sequence, StringComparison.OrdinalIgnoreCase);

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
            var name = "Sequence: " + string.Join(" + ", steps.Select(x => x.Def!.Name + (x.Step.Repeat > 1 ? $" x{x.Step.Repeat}" : "")));
            return (name, steps.Sum(x => x.Def!.Seconds * x.Step.Repeat), hits);
        }
        return AttackFor(type) is { } a ? (a.Name, a.Seconds, a.Hits.ToList()) : null;
    }

    public ResolvedAttackProfile Resolve(GogmaWeaponStats weapon, Target target)
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

        SwitchAxePhial? phial = weapon.Type == WeaponType.SwitchAxe ? Attacks.PhialOf(weapon.Focus, weapon.Element) : null;
        // the power True Charged Slash needs a weak spot: raw hitzone x sharpness of 45 or more
        var weakSpot = target.RawHitzone * sharpRaw >= DamageConstants.WeakPointHitzone;
        double hits = 0, totalMv = 0, raw = 0, ele = 0, shockwaves = 0;
        foreach (var hit in attack.Hits)
        {
            var h = weakSpot && hit.Power is { } power ? power : hit;
            var sr = h.FixedSharpness is { } fr ? DamageConstants.SharpnessRaw(fr) : sharpRaw;
            var se = h.FixedSharpness is { } fe ? DamageConstants.SharpnessElement(fe) : sharpEle;
            var rawMult = phial == SwitchAxePhial.Power && h.SwordMode ? DamageConstants.PowerPhialRaw : 1.0;
            var eleMult = phial == SwitchAxePhial.Element && h.SwordMode ? DamageConstants.ElementPhialElement : 1.0;
            var eleMod = phial == SwitchAxePhial.Element && h.ElementPhialElement is { } ep ? ep : h.Element;
            hits += h.Count;
            totalMv += h.Mv * h.Count;
            raw += h.Mv * h.Count * sr * rawMult;
            ele += eleMod * h.Count * se * eleMult;
            if (h.ChargedLv3) shockwaves += h.Count;
        }

        var name = phial is { } ph ? $"{attack.Name} ({ph} phial)" : attack.Name;
        return new ResolvedAttackProfile(name, HitsPerMinute ?? hits * 60.0 / attack.Seconds, hits, totalMv, raw / totalMv, ele * ratio * 100.0 / totalMv,
            shockwaves, ratio, target.RawHitzone, IsSequence ? "sequence" : "attack");
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
        && ChargedLv3Share == other.ChargedLv3Share && (Sequence ?? []).SequenceEqual(other.Sequence ?? []);

    public override int GetHashCode() => HashCode.Combine(Attack, HitsPerMinute, AverageMv, ChargedLv3Share, (Sequence ?? []).Count);
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

/// <summary>The on/off hunt conditions of <see cref="Damage.Conditions"/> by snake_case key, for sequence step overrides.</summary>
public static class ConditionToggles
{
    private static readonly string[] NotPerStep = [nameof(Damage.Conditions.FullHealth), nameof(Damage.Conditions.RedHealth), nameof(Damage.Conditions.LowHealth), nameof(Damage.Conditions.ProcDamage)];

    private static readonly Dictionary<string, PropertyInfo> ByKey = typeof(Conditions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(bool) && p.CanWrite && !NotPerStep.Contains(p.Name))
        .ToDictionary(p => Key(p.Name), p => p);

    /// <summary>The condition keys a sequence step can change. Health stays the same for the whole sequence; proc damage is all or nothing.</summary>
    public static IReadOnlyCollection<string> StepKeys => ByKey.Keys;

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
public readonly record struct ResolvedAttackProfile(
    string Name, double HitsPerMinute, double Hits, double TotalMv, double RawFactor, double ElementFactor, double Shockwaves, double ElementHitzoneRatio,
    double RawHitzone = 100, string Execution = "attack")
{
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
