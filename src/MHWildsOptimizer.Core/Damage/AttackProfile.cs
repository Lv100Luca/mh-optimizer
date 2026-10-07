using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// How the hunter attacks. Puts everything that does not scale with motion value on the per-100-MV scale of EFR:
/// element (applied once per hit, whatever the MV, against the element hitzone) and proc damage (the Dark Arts shockwave
/// and Bad Blood). Unset values fall back to the weapon-type preset (<see cref="Preset"/>).
/// </summary>
public sealed record AttackProfile
{
    /// <summary>Landed hits per minute of attacking; cooldown-limited procs fire at most once per cooldown.</summary>
    public double? HitsPerMinute { get; init; }
    /// <summary>Average motion value of a landed hit; converts element and proc damage per hit into damage per 100 MV.</summary>
    public double? AverageMv { get; init; }
    /// <summary>Share of landed hits that are Lv3 charged slashes other than the Rising Slash (Great Sword, Dark Arts shockwave).</summary>
    public double? ChargedLv3Share { get; init; }
    /// <summary>Element hitzone divided by raw hitzone where you hit (weak point raw 70 / element 25 = 0.36); EFR stays at raw hitzone 100.</summary>
    public double? ElementHitzoneRatio { get; init; }

    public ResolvedAttackProfile Resolve(WeaponType type)
    {
        var p = Preset(type);
        return new ResolvedAttackProfile(HitsPerMinute ?? p.HitsPerMinute, AverageMv ?? p.AverageMv, ChargedLv3Share ?? p.ChargedLv3Share,
            ElementHitzoneRatio ?? p.ElementHitzoneRatio);
    }

    /// <summary>Typical weak point hit with the monster's weak element: raw hitzone around 70, element hitzone around 25-30.</summary>
    public const double DefaultElementHitzoneRatio = 0.4;

    /// <summary>
    /// Starting points: Great Sword True Charged Slash play, Long Sword spirit-combo play (rough). Great Sword damage comes from
    /// fully charged slashes, so every hit counts as one, valued as the Lv3 True Charged Slash finisher: 190 MV (datamine 1.0.11;
    /// the training dummy agrees: 912 damage, with the 30 MV Dark Arts shockwave at 149).
    /// </summary>
    public static ResolvedAttackProfile Preset(WeaponType type) => type switch
    {
        WeaponType.GreatSword => new(24, 190, 1.0, DefaultElementHitzoneRatio),
        WeaponType.LongSword => new(50, 35, 0, DefaultElementHitzoneRatio),
        _ => new(40, 50, 0, DefaultElementHitzoneRatio),
    };
}

public readonly record struct ResolvedAttackProfile(double HitsPerMinute, double AverageMv, double ChargedLv3Share, double ElementHitzoneRatio)
{
    public double SecondsPerHit => 60.0 / HitsPerMinute;

    /// <summary>Converts damage dealt once per hit into damage per 100 MV of landed attacks.</summary>
    public double PerHundredMv => 100.0 / AverageMv;

    /// <summary>Procs per landed hit for an effect that triggers on a hit and then waits <paramref name="cooldownSeconds"/> before it can trigger again.</summary>
    public double ProcsPerHit(double cooldownSeconds) => Math.Min(1.0, SecondsPerHit / cooldownSeconds);
}
