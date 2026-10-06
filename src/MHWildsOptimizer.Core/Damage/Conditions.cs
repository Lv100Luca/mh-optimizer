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
    /// <summary>Weakness Exploit base bonus. With the hitzone-100 assumption every hit is a weak-point hit.</summary>
    public bool HittingWeakPoint { get; init; } = true;
    /// <summary>Weakness Exploit wound bonus.</summary>
    public bool HittingWound { get; init; } = false;
    /// <summary>Peak Performance.</summary>
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
    public bool AdrenalineRushActive { get; init; } = false;
    /// <summary>Razor's Edge II: Adrenaline Rush re-triggered while active (attack x1.05).</summary>
    public bool AdrenalineRushRetriggered { get; init; } = false;
    public bool CounterstrikeActive { get; init; } = false;
    public bool OffensiveGuardActive { get; init; } = false;
    /// <summary>Punishing Draw applies to draw attacks only.</summary>
    public bool DrawAttack { get; init; } = false;
    /// <summary>Charge Master applies to charged attacks only.</summary>
    public bool ChargedAttack { get; init; } = false;
    public bool CoalescenceActive { get; init; } = false;
    public bool ElementalAbsorptionActive { get; init; } = false;
    /// <summary>Azure Bolt (Leviathan's Fury) affinity window.</summary>
    public bool AzureBoltActive { get; init; } = true;
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

    public static Conditions Default => new();

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
    };
}
