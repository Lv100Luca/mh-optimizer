using System.Text.Json.Serialization;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Damage;

public enum ResonanceMode { None, Local, Remote }

/// <summary>
/// User-toggleable hunt conditions. A toggle only has an effect when the evaluated loadout actually carries the skill
/// it drives (the calculator checks skill levels / set tiers first), so an "always on" toggle never invents a skill.
/// Defaults describe the usual optimizer assumption: enraged monster, hitting a weak spot, full health and stamina.
/// </summary>
public sealed record Conditions
{
    // ----- shared hunter / monster state -----
    /// <summary>Agitator, Mutual Hostility (Gogmapocalypse).</summary>
    public bool MonsterEnraged { get; init; } = true;
    /// <summary>Weakness Exploit base bonus; only counts when the <see cref="Target"/> is a weak point (raw hitzone 45 or more).</summary>
    public bool HittingWeakPoint { get; init; } = true;
    /// <summary>Weakness Exploit wound bonus.</summary>
    public bool HittingWound { get; init; } = false;
    /// <summary>Peak Performance. Excludes <see cref="RedHealth"/> and <see cref="LowHealth"/>: with both on, each loadout is scored at the better side.</summary>
    public bool FullHealth { get; init; } = true;
    /// <summary>Resentment, Dark Arts.</summary>
    public bool RedHealth { get; init; } = false;
    /// <summary>Heroics (health at or below 35%).</summary>
    public bool LowHealth { get; init; } = false;
    /// <summary>Maximum Might (stamina full for 3 s).</summary>
    public bool StaminaFull { get; init; } = true;
    /// <summary>Foray (monster poisoned or paralyzed).</summary>
    public bool MonsterStatused { get; init; } = false;
    /// <summary>Frenzy has been overcome: base +15% affinity, Antivirus, Black Eclipse II recovery bonus. Only used when the Gore set bonus is active.</summary>
    public bool FrenzyOvercome { get; init; } = true;

    // ----- skill-specific triggers -----
    /// <summary>Burst at the full (5-hit) boost; Burst Boost (Ebony Odogaron) rides on it.</summary>
    public bool BurstActive { get; init; } = true;
    public bool LatentPowerActive { get; init; } = false;
    /// <summary>
    /// Share of the hunt Latent Power is active (0 to 1) when <see cref="LatentPowerActive"/>; null: the build's default without
    /// taking damage, by its Thunderous Roar tier (<see cref="DamageConstants.LatentPowerUptime"/>). See <see cref="UptimeParts"/>.
    /// </summary>
    public double? LatentPowerUptime { get; init; }
    /// <summary>A part of the hunt where Latent Power only counts from this Thunderous Roar (Rey Dau's Voltage) tier on; see <see cref="UptimeParts"/>.</summary>
    [JsonIgnore] public int LatentPowerMinRoarTier { get; init; }
    public bool AdrenalineRushActive { get; init; } = false;
    /// <summary>Razor's Edge II: Adrenaline Rush re-triggered while active (attack x1.05).</summary>
    public bool AdrenalineRushRetriggered { get; init; } = false;
    public bool CounterstrikeActive { get; init; } = false;
    public bool OffensiveGuardActive { get; init; } = false;
    /// <summary>Punishing Draw applies to draw attacks only.</summary>
    public bool DrawAttack { get; init; } = false;
    /// <summary>Charge Master applies to charged attacks only.</summary>
    public bool ChargedAttack { get; init; } = false;
    /// <summary>Coalescence after the Frenzy cure; only counted with the Gore set bonus and <see cref="FrenzyOvercome"/>.</summary>
    public bool CoalescenceActive { get; init; } = false;
    public bool ElementalAbsorptionActive { get; init; } = false;
    /// <summary>Azure Bolt (Leviathan's Fury) affinity window.</summary>
    public bool AzureBoltActive { get; init; } = true;
    /// <summary>
    /// Share of the hunt the Azure Bolt window is up (0 to 1) when <see cref="AzureBoltActive"/>; null: the build's default by its
    /// Leviathan's Fury tier (<see cref="DamageConstants.AzureBoltUptime"/>). See <see cref="UptimeParts"/>.
    /// </summary>
    public double? AzureBoltUptime { get; init; }
    /// <summary>A part of the hunt where the Azure Bolt window only counts from this Leviathan's Fury tier on; see <see cref="UptimeParts"/>.</summary>
    [JsonIgnore] public int AzureBoltMinTier { get; init; }
    /// <summary>Omega Resonance alternates Local (affinity) and Remote (attack) every 90 s.</summary>
    public ResonanceMode Resonance { get; init; } = ResonanceMode.Local;
    /// <summary>Powerhouse (Doshaguma's Might): after a Power Clash / Offset.</summary>
    public bool PowerhouseActive { get; init; } = false;
    /// <summary>Protein Fiend (Xu Wu's Vigor): after eating a steak.</summary>
    public bool ProteinFiendActive { get; init; } = false;
    /// <summary>Binding Counter (Jin Dahaad's Revolt).</summary>
    public bool BindingCounterActive { get; init; } = false;
    /// <summary>War Cry (Blangonga's Spirit).</summary>
    public bool WarCryActive { get; init; } = false;
    /// <summary>Festival Boon II (the four Prayer set bonuses) only works while that festival runs.</summary>
    public bool FestivalActive { get; init; } = false;
    /// <summary>Resuscitate (Lord's Fury): afflicted with an ailment.</summary>
    public bool ResuscitateActive { get; init; } = false;
    /// <summary>Inspiration (Lord's Favor).</summary>
    public bool InspirationActive { get; init; } = false;
    /// <summary>Affinity Sliding (Buttery Leathercraft).</summary>
    public bool AffinitySlidingActive { get; init; } = false;
    /// <summary>Guts (Lord's Soul) keeps its attack bonus until it saves you once.</summary>
    public bool GutsNotYetTriggered { get; init; } = true;

    // ----- items and meals: flat attack after the percentage bonuses, for every build -----
    /// <summary>A Powercharm in the item pouch: +<see cref="DamageConstants.PowercharmAttack"/> attack.</summary>
    public bool Powercharm { get; init; } = true;
    /// <summary>Attack from the meal (its "Attack +N" in the meal effects; 0 without one).</summary>
    public int MealAttack { get; init; }

    // ----- proc damage: extra damage instances, converted to per 100 MV with the attack profile (Azure Bolt bursts and Scorcher are never counted) -----
    /// <summary>Count the Dark Arts shockwave (Soul of the Dark Knight, Great Sword Lv3 charged slashes); it fires at any health.</summary>
    public bool DarkArtsShockwave { get; init; } = true;
    /// <summary>Count Bad Blood hits (Nu Udra's Mutiny); they need Resentment, so <see cref="RedHealth"/> as well.</summary>
    public bool BadBlood { get; init; } = true;
    /// <summary>The attack the score is computed for.</summary>
    public AttackProfile AttackProfile { get; init; } = new();
    /// <summary>Where the hits land: raw and element hitzone.</summary>
    public Target Target { get; init; } = new();

    /// <summary>The attack profile resolved for <paramref name="weapon"/> against the <see cref="Target"/> (a whole sequence, ignoring step overrides).</summary>
    public ResolvedAttackProfile Attack(GogmaWeaponStats weapon) => AttackProfile.Resolve(weapon, Target);

    /// <summary>
    /// The parts of the attack scored under different conditions: one per distinct set of sequence step overrides (steps with the
    /// same overrides are scored together, the order does not change the score). Each segment carries the base conditions with its
    /// overrides applied and an attack profile of just its steps, at the hits per minute of the whole sequence. Without step
    /// overrides there is one segment: these conditions. The score is the segments' scores weighted by their share of the
    /// sequence's motion value (<see cref="DamageCalculator"/>).
    /// </summary>
    public IReadOnlyList<Conditions> Segments(GogmaWeaponStats weapon)
    {
        var p = AttackProfile;
        if (!p.IsSequence || p.Sequence is not { Count: > 0 } steps) return [this];
        // only overrides that change something count; keys in a fixed order so equal override sets group together
        var effective = steps
            .Select(st => (Step: st, Overrides: st.Conditions
                .Where(kv => ConditionToggles.StepKeys.Contains(kv.Key) && ConditionToggles.Get(this, kv.Key) != kv.Value)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList()))
            .ToList();
        if (effective.All(e => e.Overrides.Count == 0)) return [this];
        var hitsPerMinute = p.HitsPerMinute ?? Attack(weapon).HitsPerMinute;
        return effective
            .GroupBy(e => string.Join(",", e.Overrides.Select(kv => $"{kv.Key}={kv.Value}")))
            .Select(g => ConditionToggles.With(this, g.First().Overrides.ToDictionary(kv => kv.Key, kv => kv.Value)) with
            {
                AttackProfile = p with { HitsPerMinute = hitsPerMinute, Sequence = g.Select(e => e.Step with { Conditions = new() }).ToList() },
            })
            .ToList();
    }

    /// <summary>
    /// Timed skills that are active only part of the hunt (<see cref="LatentPowerUptime"/>, <see cref="AzureBoltUptime"/>) split it
    /// into parts, each with its share of the time and the skills in it fixed on or off (uptime 1); the score is the parts' scores
    /// weighted by their share. The two skills are independent, so their parts cross. A default uptime depends on a set bonus tier,
    /// so it splits into slices that only count from a tier on: Azure Bolt on from Leviathan's Fury I for 60% of the time, from II
    /// for another 25%, off for the rest gives 60% at tier I and 85% at tier II with fixed weights. With <paramref name="skills"/>,
    /// parts that score alike for that loadout are merged (one part when it has neither skill).
    /// </summary>
    public IReadOnlyList<(Conditions Part, double Share)> UptimeParts(ActiveSkills? skills = null)
    {
        var hasLatent = skills is null || Effective(SkillNames.LatentPower, skills.Level(SkillNames.LatentPower)) > 0;
        var fury = skills is null ? 2 : (int)skills.SetTier(SkillNames.LeviathansFury);
        if (!(LatentPowerActive && hasLatent) && !(AzureBoltActive && fury > 0)) return [(this, 1)];

        var latent = LatentPowerActive ? Slices(LatentPowerUptime, t => DamageConstants.LatentPowerUptime(t), from: 0) : null;
        var bolt = AzureBoltActive ? Slices(AzureBoltUptime, t => DamageConstants.AzureBoltUptime(t), from: 1) : null;
        if (latent is null && bolt is null) return [(this, 1)];

        var parts = new List<(Conditions Part, double Share)>();
        foreach (var (lpOn, lpTier, lpShare) in latent ?? [(LatentPowerActive, LatentPowerMinRoarTier, 1.0)])
        foreach (var (abOn, abTier, abShare) in bolt ?? [(AzureBoltActive, AzureBoltMinTier, 1.0)])
        {
            var share = lpShare * abShare;
            if (share <= 0) continue;
            parts.Add((this with
            {
                LatentPowerActive = lpOn, LatentPowerMinRoarTier = lpTier, LatentPowerUptime = latent is null ? LatentPowerUptime : 1,
                AzureBoltActive = abOn, AzureBoltMinTier = abTier, AzureBoltUptime = bolt is null ? AzureBoltUptime : 1,
            }, share));
        }
        if (skills is null) return parts;

        // what each part turns on for this loadout; parts that turn on the same score the same
        var roar = (int)skills.SetTier(SkillNames.ReyDausVoltage);
        return parts
            .GroupBy(p => (Latent: p.Part.LatentPowerActive && hasLatent && roar >= p.Part.LatentPowerMinRoarTier,
                           Bolt: p.Part.AzureBoltActive && fury >= Math.Max(1, p.Part.AzureBoltMinTier)))
            .Select(g => (g.First().Part with
            {
                LatentPowerActive = g.Key.Latent, LatentPowerMinRoarTier = 0, AzureBoltActive = g.Key.Bolt, AzureBoltMinTier = 0,
            }, g.Sum(p => p.Share)))
            .ToList();
    }

    /// <summary>
    /// A timed skill's parts as (on, from tier, share): none when it is always on (uptime 1), off for an uptime of 0, on and off for
    /// a set uptime, and for the default one slice per tier from <paramref name="from"/> to II (the extra time that tier adds) plus off.
    /// </summary>
    private static List<(bool On, int Tier, double Share)>? Slices(double? uptime, Func<SetBonusTier, double> byTier, int from)
    {
        switch (uptime)
        {
            case >= 1: return null;
            case <= 0: return [(false, 0, 1.0)];
            case { } u: return [(true, 0, u), (false, 0, 1 - u)];
        }
        var slices = new List<(bool On, int Tier, double Share)>();
        var before = 0.0;
        for (var t = from; t <= 2; t++)
        {
            var at = byTier((SetBonusTier)t);
            slices.Add((true, t, at - before));
            before = at;
        }
        slices.Add((false, 0, 1 - before));
        return slices;
    }

    /// <summary>
    /// Per-skill caps for the score: 0 removes the skill from the optimization entirely, n counts it only up to level n
    /// (e.g. { "Burst": 1 } for Great Sword, where the five-hit boost is rarely reached). Levels above the cap are shown but not valued.
    /// </summary>
    public Dictionary<string, int> SkillLimits { get; init; } = new();

    /// <summary>The level a skill is valued at under these conditions.</summary>
    public int Effective(string skill, int level) => SkillLimits.TryGetValue(skill, out var limit) ? Math.Min(level, limit) : level;

    public static Conditions Default => new();

    /// <summary>
    /// The defaults for a weapon type (API kind), where they differ from <see cref="Default"/>. Bow: charging and the Charging
    /// Sidestep cost stamina, so the dash dance never has full stamina (Maximum Might); its charged shots, Power Shots and Power
    /// Volleys are charged attacks (Charge Master).
    /// </summary>
    public static Conditions DefaultFor(string weaponKind) => weaponKind switch
    {
        "bow" => Default with { StaminaFull = false, ChargedAttack = true },
        _ => Default,
    };

    /// <summary>Every conditional skill counted as active all the time (a theoretical maximum: the better of full and red/low health, Resonance on its Local phase).</summary>
    public static Conditions AllOn => new()
    {
        LatentPowerUptime = 1,
        AzureBoltUptime = 1,
        MonsterEnraged = true,
        HittingWeakPoint = true,
        HittingWound = true,
        FullHealth = true,
        RedHealth = true,
        LowHealth = true,
        StaminaFull = true,
        MonsterStatused = true,
        FrenzyOvercome = true,
        BurstActive = true,
        LatentPowerActive = true,
        AdrenalineRushActive = true,
        AdrenalineRushRetriggered = true,
        CounterstrikeActive = true,
        OffensiveGuardActive = true,
        DrawAttack = true,
        ChargedAttack = true,
        CoalescenceActive = true,
        ElementalAbsorptionActive = true,
        AzureBoltActive = true,
        Resonance = ResonanceMode.Local,
        PowerhouseActive = true,
        ProteinFiendActive = true,
        BindingCounterActive = true,
        WarCryActive = true,
        FestivalActive = true,
        ResuscitateActive = true,
        InspirationActive = true,
        AffinitySlidingActive = true,
        GutsNotYetTriggered = true,
        Powercharm = true,
        DarkArtsShockwave = true,
        BadBlood = true,
    };

    /// <summary><see cref="AllOn"/> with the attack and target of <paramref name="basis"/>, so the two compare on the same attack.</summary>
    public static Conditions AllOnLike(Conditions basis) => AllOn with { AttackProfile = basis.AttackProfile, Target = basis.Target, MealAttack = basis.MealAttack };

    /// <summary><paramref name="basis"/> with every toggle on that is on in any of <paramref name="parts"/> (the segments of a sequence): what can matter somewhere.</summary>
    public static Conditions Union(Conditions basis, IReadOnlyList<Conditions> parts)
    {
        var on = ConditionToggles.StepKeys.Where(k => parts.Any(c => ConditionToggles.Get(c, k))).ToDictionary(k => k, _ => true);
        return ConditionToggles.With(basis, on);
    }

    /// <summary>Everything off: unconditional skills only.</summary>
    public static Conditions AllOff => new()
    {
        MonsterEnraged = false,
        HittingWeakPoint = false,
        HittingWound = false,
        FullHealth = false,
        RedHealth = false,
        LowHealth = false,
        StaminaFull = false,
        MonsterStatused = false,
        FrenzyOvercome = false,
        BurstActive = false,
        LatentPowerActive = false,
        AdrenalineRushActive = false,
        AdrenalineRushRetriggered = false,
        CounterstrikeActive = false,
        OffensiveGuardActive = false,
        DrawAttack = false,
        ChargedAttack = false,
        CoalescenceActive = false,
        ElementalAbsorptionActive = false,
        AzureBoltActive = false,
        Resonance = ResonanceMode.None,
        PowerhouseActive = false,
        ProteinFiendActive = false,
        BindingCounterActive = false,
        WarCryActive = false,
        FestivalActive = false,
        ResuscitateActive = false,
        InspirationActive = false,
        AffinitySlidingActive = false,
        GutsNotYetTriggered = false,
        Powercharm = false,
        DarkArtsShockwave = false,
        BadBlood = false,
    };
}
