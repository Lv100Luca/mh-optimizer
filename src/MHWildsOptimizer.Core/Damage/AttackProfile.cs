using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// How the hunter attacks: the attack the score is computed for (<see cref="Attacks"/>), or for weapon types without attack data
/// the average-hit model (every hit one element hit of <see cref="AverageMv"/>). Unset values fall back to the weapon type's
/// default attack and preset (<see cref="Preset"/>). The score is damage per 100 MV of the attack on the raw-hitzone-100 scale
/// of EFR: the target's element hitzone / raw hitzone weighs the element.
/// </summary>
public sealed record AttackProfile
{
    /// <summary>An attack id of the weapon type (<see cref="Attacks.All"/>) or <see cref="Attacks.Average"/>; null: the weapon type's default.</summary>
    public string? Attack { get; init; }
    /// <summary>Landed hits per minute of attacking; cooldown-limited procs fire at most once per cooldown. Default: from the attack's duration.</summary>
    public double? HitsPerMinute { get; init; }
    /// <summary>Average-hit model only: average motion value of a landed hit.</summary>
    public double? AverageMv { get; init; }
    /// <summary>Average-hit model only: share of landed hits that are Lv3 charged slashes other than the Rising Slash (Great Sword, Dark Arts shockwave).</summary>
    public double? ChargedLv3Share { get; init; }

    /// <summary>The attack definition this profile scores <paramref name="type"/> with; null for the average-hit model.</summary>
    public AttackDefinition? AttackFor(WeaponType type) =>
        string.Equals(Attack, Attacks.Average, StringComparison.OrdinalIgnoreCase) ? null : Attacks.Find(type, Attack ?? Attacks.DefaultFor(type));

    public ResolvedAttackProfile Resolve(GogmaWeaponStats weapon, Target target)
    {
        var p = Preset(weapon.Type);
        var ratio = target.ElementRatio(weapon.Element);
        var sharpRaw = weapon.TopSharpness is { } s ? DamageConstants.SharpnessRaw(s) : 1.0;
        var sharpEle = weapon.TopSharpness is { } s2 ? DamageConstants.SharpnessElement(s2) : 1.0;

        if (AttackFor(weapon.Type) is not { } attack)
        {
            var mv = AverageMv ?? p.AverageMv;
            return new ResolvedAttackProfile("Average hit", HitsPerMinute ?? p.HitsPerMinute, 1, mv, sharpRaw, sharpEle * ratio * 100.0 / mv,
                ChargedLv3Share ?? p.ChargedLv3Share, ratio);
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
            shockwaves, ratio);
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
public readonly record struct ResolvedAttackProfile(
    string Name, double HitsPerMinute, double Hits, double TotalMv, double RawFactor, double ElementFactor, double Shockwaves, double ElementHitzoneRatio)
{
    public double SecondsPerHit => 60.0 / HitsPerMinute;

    public double AverageMv => TotalMv / Hits;

    /// <summary>Converts damage dealt once per execution into damage per 100 MV of landed attacks.</summary>
    public double PerHundredMv => 100.0 / TotalMv;

    /// <summary>Procs per landed hit for an effect that triggers on a hit and then waits <paramref name="cooldownSeconds"/> before it can trigger again.</summary>
    public double ProcsPerHit(double cooldownSeconds) => Math.Min(1.0, SecondsPerHit / cooldownSeconds);
}
