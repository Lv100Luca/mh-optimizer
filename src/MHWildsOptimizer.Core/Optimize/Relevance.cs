using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// Which skills, set bonuses and group skills can change the score under the requested conditions.
/// Everything else is ignored by the search (it neither helps the targets nor the damage).
/// </summary>
public sealed class Relevance
{
    public IReadOnlyList<string> Skills { get; }
    public IReadOnlyDictionary<string, int> SkillIndex { get; }
    public int[] MaxLevels { get; }
    public int[] Targets { get; }
    public bool[] IsWeaponSkill { get; }
    /// <summary>True when the skill changes the score; false for skills that are only targets (e.g. Focus), where levels above the target are worthless.</summary>
    public bool[] IsOffensive { get; }
    /// <summary>Highest level worth tracking: the skill max for offensive skills, the target for target-only skills.</summary>
    public int[] Caps { get; }
    public IReadOnlyList<string> SetBonuses { get; }
    public IReadOnlyDictionary<string, int> SetIndex { get; }
    public IReadOnlyList<string> GroupSkills { get; }
    public IReadOnlyDictionary<string, int> GroupIndex { get; }

    private Relevance(List<string> skills, HashSet<string> offensive, GameData data, IReadOnlyDictionary<string, int> targets, List<string> sets, List<string> groups)
    {
        Skills = skills;
        SkillIndex = skills.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        MaxLevels = skills.Select(s => data.Skill(s).MaxLevel).ToArray();
        Targets = skills.Select(s => targets.GetValueOrDefault(s)).ToArray();
        IsWeaponSkill = skills.Select(s => data.Skill(s).Kind == SkillKind.Weapon).ToArray();
        IsOffensive = skills.Select(offensive.Contains).ToArray();
        Caps = skills.Select((s, i) => IsOffensive[i] ? MaxLevels[i] : Targets[i]).ToArray();
        SetBonuses = sets;
        SetIndex = sets.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        GroupSkills = groups;
        GroupIndex = groups.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
    }

    public static Relevance Build(GogmaWeaponStats weapon, IReadOnlyDictionary<string, int> targets, Conditions c, GameData data)
    {
        var skills = new List<string>();
        var offensive = new HashSet<string>();
        void Add(string name, bool when = true)
        {
            if (!when || !data.SkillsByName.ContainsKey(name)) return;
            if (!skills.Contains(name)) skills.Add(name);
            offensive.Add(name);
        }

        foreach (var t in targets.Keys)
            if (data.SkillsByName.ContainsKey(t) && !skills.Contains(t)) skills.Add(t);

        var hasElement = weapon.ElementTrue > 0;
        Add(SkillNames.AttackBoost);
        Add(SkillNames.CriticalEye);
        Add(SkillNames.CriticalBoost);
        Add(SkillNames.CriticalElement, hasElement);
        if (hasElement && SkillNames.ElementAttackSkill(weapon.Element) is { } ele) Add(ele);
        Add(SkillNames.WeaknessExploit, c.HittingWeakPoint);
        Add(SkillNames.Agitator, c.MonsterEnraged);
        Add(SkillNames.PeakPerformance, c.FullHealth);
        Add(SkillNames.MaximumMight, c.StaminaFull);
        Add(SkillNames.LatentPower, c.LatentPowerActive);
        Add(SkillNames.AdrenalineRush, c.AdrenalineRushActive);
        Add(SkillNames.Counterstrike, c.CounterstrikeActive);
        Add(SkillNames.Resentment, c.RedHealth);
        Add(SkillNames.Heroics, c.LowHealth);
        Add(SkillNames.Foray, c.MonsterStatused);
        Add(SkillNames.Burst, c.BurstActive);
        Add(SkillNames.OffensiveGuard, c.OffensiveGuardActive);
        Add(SkillNames.PunishingDraw, c.DrawAttack);
        Add(SkillNames.Antivirus, c.FrenzyOvercome);
        Add(SkillNames.Coalescence, hasElement && c.CoalescenceActive);
        Add(SkillNames.ChargeMaster, hasElement && c.ChargedAttack);
        Add(SkillNames.ElementalAbsorption, hasElement && c.ElementalAbsorptionActive);

        var sets = new List<string>();
        void Set(string name, bool when = true) { if (when && !sets.Contains(name)) sets.Add(name); }
        Set(SkillNames.GoreMagalasTyranny);
        Set(SkillNames.LeviathansFury, c.AzureBoltActive);
        Set(SkillNames.SeregiossTenacity, c.AdrenalineRushActive && c.AdrenalineRushRetriggered);
        Set(SkillNames.OmegaResonance, c.Resonance != ResonanceMode.None);
        Set(SkillNames.Gogmapocalypse, hasElement && c.MonsterEnraged);
        Set(SkillNames.SoulOfTheDarkKnight, hasElement && c.RedHealth);
        Set(SkillNames.EbonyOdogaronsPower, c.BurstActive);
        Set(SkillNames.DoshagumasMight, c.PowerhouseActive);
        Set(SkillNames.XuWusVigor, c.ProteinFiendActive);
        Set(SkillNames.JinDahaadsRevolt, c.BindingCounterActive);
        Set(SkillNames.BlangongasSpirit, c.WarCryActive);
        foreach (var p in SkillNames.FestivalPrayers) Set(p, c.FestivalActive);

        var groups = new List<string>();
        void Group(string name, bool when) { if (when && !groups.Contains(name)) groups.Add(name); }
        Group(SkillNames.LordsSoul, c.GutsNotYetTriggered);
        Group(SkillNames.LordsFury, c.ResuscitateActive);
        Group(SkillNames.LordsFavor, c.InspirationActive);
        Group(SkillNames.ButteryLeathercraft, c.AffinitySlidingActive);

        return new Relevance(skills, offensive, data, targets, sets, groups);
    }

    public int IndexOf(string skill) => SkillIndex.TryGetValue(skill, out var i) ? i : -1;
}
