using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>
/// How the hunter attacks. Used only to turn proc damage (extra damage instances such as Azure Bolt, the Dark Arts shockwave,
/// Bad Blood and Scorcher) into the per-100-MV score; EFR and EFE do not depend on it. Unset values fall back to the
/// weapon-type preset (<see cref="Preset"/>).
/// </summary>
public sealed record AttackProfile
{
    /// <summary>Landed hits per minute of attacking; cooldown-limited procs fire at most once per cooldown.</summary>
    public double? HitsPerMinute { get; init; }
    /// <summary>Average motion value of a landed hit; converts proc damage per hit into damage per 100 MV.</summary>
    public double? AverageMv { get; init; }
    /// <summary>Share of landed hits that are Lv3 charged slashes other than the Rising Slash (Great Sword, Dark Arts shockwave).</summary>
    public double? ChargedLv3Share { get; init; }

    public ResolvedAttackProfile Resolve(WeaponType type)
    {
        var p = Preset(type);
        return new ResolvedAttackProfile(HitsPerMinute ?? p.HitsPerMinute, AverageMv ?? p.AverageMv, ChargedLv3Share ?? p.ChargedLv3Share);
    }

    /// <summary>Rough starting points (not measured): Great Sword charge-slash play, Long Sword spirit-combo play.</summary>
    public static ResolvedAttackProfile Preset(WeaponType type) => type switch
    {
        WeaponType.GreatSword => new(24, 120, 0.3),
        WeaponType.LongSword => new(50, 35, 0),
        _ => new(40, 50, 0),
    };
}

public readonly record struct ResolvedAttackProfile(double HitsPerMinute, double AverageMv, double ChargedLv3Share)
{
    public double SecondsPerHit => 60.0 / HitsPerMinute;

    /// <summary>Procs per landed hit for an effect that rolls <paramref name="chance"/> on a hit and then waits <paramref name="cooldownSeconds"/> before it can roll again.</summary>
    public double ProcsPerHit(double cooldownSeconds, double chance = 1.0) => chance * Math.Min(1.0, SecondsPerHit / cooldownSeconds);
}
