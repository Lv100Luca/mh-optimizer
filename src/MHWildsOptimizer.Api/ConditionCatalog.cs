using System.Reflection;
using System.Text.Json;
using MHWildsOptimizer.Core.Damage;

namespace MHWildsOptimizer.Api;

/// <summary>One toggle of <see cref="Conditions"/>, with the text the UI shows for it.</summary>
public sealed record ConditionDto(string Key, string Property, string Label, string Group, string Description, IReadOnlyList<string> Skills, bool Default);

/// <summary>Labels, grouping and the skills behind every boolean condition. Properties without an entry still show up with a generated label.</summary>
public static class ConditionCatalog
{
    public const string GroupMonster = "Monster";
    public const string GroupHunter = "Hunter";
    public const string GroupSkills = "Skill triggers";
    public const string GroupBonuses = "Set bonus and group skill triggers";
    /// <summary>Not a hunt condition: whether proc damage is added to the score (the client renders it with the attack profile).</summary>
    public const string GroupProcs = "Proc damage";

    private sealed record Meta(string Label, string Group, string Description, string[] Skills);

    private static readonly Dictionary<string, Meta> Table = new()
    {
        [nameof(Conditions.MonsterEnraged)] = new("Monster enraged", GroupMonster, "The monster is enraged.", ["Agitator", "Gogmapocalypse (Mutual Hostility)"]),
        [nameof(Conditions.HittingWeakPoint)] = new("Hitting weak points", GroupMonster, "Hits land on a weak point (hitzone 45+). Only counts when the target's raw hitzone is 45 or more.", ["Weakness Exploit"]),
        [nameof(Conditions.HittingWound)] = new("Hitting wounds", GroupMonster, "Hits land on a wound, which adds the Weakness Exploit wound bonus.", ["Weakness Exploit"]),
        [nameof(Conditions.MonsterStatused)] = new("Monster poisoned or paralyzed", GroupMonster, "The monster suffers from poison or paralysis.", ["Foray"]),

        [nameof(Conditions.FullHealth)] = new("Full health", GroupHunter, "Your health bar is full. Cannot coexist with red or low health: with both on, each build is scored at whichever is better for it.", ["Peak Performance"]),
        [nameof(Conditions.RedHealth)] = new("Red health", GroupHunter, "You have recoverable (red) health, so not full health: with both on, each build is scored at whichever is better for it.", ["Resentment", "Soul of the Dark Knight (Dark Arts)"]),
        [nameof(Conditions.LowHealth)] = new("Low health (35% or less)", GroupHunter, "Health at or below 35%, so not full health: with both on, each build is scored at whichever is better for it.", ["Heroics"]),
        [nameof(Conditions.StaminaFull)] = new("Stamina full", GroupHunter, "Stamina has been full for at least 3 seconds.", ["Maximum Might"]),
        [nameof(Conditions.FrenzyOvercome)] = new("Frenzy overcome", GroupHunter, "You overcame the Frenzy virus (Gore Magala set bonus): +15% affinity, Antivirus bonus, Black Eclipse II recovery.", ["Gore Magala's Tyranny (Black Eclipse)", "Antivirus"]),

        [nameof(Conditions.BurstActive)] = new("Burst at full stacks", GroupSkills, "Five consecutive hits landed; Burst gives its full boost and Burst Boost (Ebony Odogaron) rides on it.", ["Burst", "Ebony Odogaron's Power (Burst Boost)"]),
        [nameof(Conditions.LatentPowerActive)] = new("Latent Power triggered", GroupSkills, "Latent Power is active after taking damage or 3 minutes of fighting.", ["Latent Power"]),
        [nameof(Conditions.AdrenalineRushActive)] = new("Adrenaline Rush active", GroupSkills, "A perfectly timed evade triggered Adrenaline Rush.", ["Adrenaline Rush"]),
        [nameof(Conditions.AdrenalineRushRetriggered)] = new("Adrenaline Rush re-triggered", GroupSkills, "Adrenaline Rush triggered again while active (Razor's Edge II from Seregios's Tenacity: attack x1.05).", ["Seregios's Tenacity (Razor's Edge)"]),
        [nameof(Conditions.CounterstrikeActive)] = new("Counterstrike active", GroupSkills, "You were knocked back and Counterstrike kicked in.", ["Counterstrike"]),
        [nameof(Conditions.OffensiveGuardActive)] = new("Offensive Guard active", GroupSkills, "A perfectly timed guard activated Offensive Guard.", ["Offensive Guard"]),
        [nameof(Conditions.DrawAttack)] = new("Draw attacks", GroupSkills, "The attack is a draw attack.", ["Punishing Draw"]),
        [nameof(Conditions.ChargedAttack)] = new("Charged attacks", GroupSkills, "The attack is a charged attack.", ["Charge Master"]),
        [nameof(Conditions.CoalescenceActive)] = new("Coalescence active", GroupSkills, "The Frenzy cure triggered Coalescence. Needs a natural status recovery, so it is only counted with the Gore Magala set bonus and Frenzy overcome.", ["Coalescence"]),
        [nameof(Conditions.ElementalAbsorptionActive)] = new("Elemental Absorption active", GroupSkills, "You took elemental damage recently.", ["Elemental Absorption"]),

        [nameof(Conditions.AzureBoltActive)] = new("Azure Bolt window", GroupBonuses, "Leviathan's Fury (Lagiacrus): the Azure Bolt affinity window is up.", ["Leviathan's Fury (Azure Bolt)"]),
        [nameof(Conditions.PowerhouseActive)] = new("Powerhouse active", GroupBonuses, "Doshaguma's Might: after a Power Clash or Offset attack.", ["Doshaguma's Might (Powerhouse)"]),
        [nameof(Conditions.ProteinFiendActive)] = new("Protein Fiend active", GroupBonuses, "Xu Wu's Vigor: after eating a well-done steak.", ["Xu Wu's Vigor (Protein Fiend)"]),
        [nameof(Conditions.BindingCounterActive)] = new("Binding Counter active", GroupBonuses, "Jin Dahaad's Revolt: after recovering from a bind.", ["Jin Dahaad's Revolt (Binding Counter)"]),
        [nameof(Conditions.WarCryActive)] = new("War Cry active", GroupBonuses, "Blangonga's Spirit: after the To Victory! gesture.", ["Blangonga's Spirit (War Cry)"]),
        [nameof(Conditions.FestivalActive)] = new("Festival running", GroupBonuses, "The four Prayer set bonuses (Festival Boon II) only work while their festival runs.", ["Blossomdance / Flamefete / Dreamspell / Lumenhymn Prayer"]),
        [nameof(Conditions.ResuscitateActive)] = new("Resuscitate active", GroupBonuses, "Lord's Fury: you are afflicted with an abnormal status.", ["Lord's Fury (Resuscitate)"]),
        [nameof(Conditions.InspirationActive)] = new("Inspiration active", GroupBonuses, "Lord's Favor: companion effects are up.", ["Lord's Favor (Inspiration)"]),
        [nameof(Conditions.AffinitySlidingActive)] = new("Affinity Sliding active", GroupBonuses, "Buttery Leathercraft: you slid recently.", ["Buttery Leathercraft (Affinity Sliding)"]),
        [nameof(Conditions.GutsNotYetTriggered)] = new("Guts not yet used", GroupBonuses, "Lord's Soul: Guts keeps its attack bonus until it saves you once.", ["Lord's Soul (Guts)"]),

        [nameof(Conditions.ProcDamage)] = new("Count proc damage", GroupProcs,
            "Extra damage instances that do not scale the hit: the Dark Arts shockwave (Great Sword Lv3 charged slashes) and Bad Blood (needs Resentment and red health). The attack profile converts them to damage per 100 MV and adds them to the score. Azure Bolt bursts and Scorcher are too unreliable to build around and are never counted.",
            ["Soul of the Dark Knight (Dark Arts)", "Nu Udra's Mutiny (Bad Blood)"]),
    };

    public static IReadOnlyList<PropertyInfo> BoolProperties() =>
        typeof(Conditions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
            .ToList();

    public static IReadOnlyList<ConditionDto> Build()
    {
        var defaults = Conditions.Default;
        var order = new[] { GroupMonster, GroupHunter, GroupSkills, GroupBonuses, GroupProcs };
        return BoolProperties()
            .Select(p =>
            {
                var meta = Table.GetValueOrDefault(p.Name) ?? new Meta(GeneratedLabel(p.Name), GroupSkills, "", []);
                return new ConditionDto(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name), p.Name, meta.Label, meta.Group, meta.Description, meta.Skills, (bool)p.GetValue(defaults)!);
            })
            .OrderBy(c => Array.IndexOf(order, c.Group))
            .ToList();
    }

    private static readonly Lazy<Dictionary<string, string>> Labels = new(() => Build().ToDictionary(c => c.Key, c => c.Label));

    /// <summary>The label of a condition by its snake_case key ("stamina_full": "Stamina full").</summary>
    public static string LabelOf(string key) => Labels.Value.GetValueOrDefault(key) ?? key;

    private static string GeneratedLabel(string propertyName) =>
        string.Concat(propertyName.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : ch.ToString()));
}
