using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// Which skills, set bonuses and group skills can change the score under the requested conditions, plus the ones that are
/// required. Everything else is ignored by the search (it neither helps the targets nor the damage).
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
    /// <summary>Pieces required per set bonus (0 = not required, the set is only tracked for its score).</summary>
    public int[] SetTargets { get; }
    public IReadOnlyList<string> GroupSkills { get; }
    public IReadOnlyDictionary<string, int> GroupIndex { get; }
    /// <summary>Pieces required per group skill (0 = not required, the group is only tracked for its score).</summary>
    public int[] GroupTargets { get; }

    private Relevance(List<string> skills, HashSet<string> offensive, GameData data, IReadOnlyDictionary<string, int> targets,
        IReadOnlyDictionary<string, int> limits, List<string> sets, IReadOnlyDictionary<string, int> setTargets,
        List<string> groups, IReadOnlyDictionary<string, int> groupTargets)
    {
        Skills = skills;
        SkillIndex = skills.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        MaxLevels = skills.Select(s => data.Skill(s).MaxLevel).ToArray();
        Targets = skills.Select(s => targets.GetValueOrDefault(s)).ToArray();
        IsWeaponSkill = skills.Select(s => data.Skill(s).Kind == SkillKind.Weapon).ToArray();
        IsOffensive = skills.Select(offensive.Contains).ToArray();
        Caps = skills.Select((s, i) =>
        {
            var cap = IsOffensive[i] ? MaxLevels[i] : Targets[i];
            if (limits.TryGetValue(s, out var limit)) cap = Math.Max(Targets[i], Math.Min(cap, limit));
            return cap;
        }).ToArray();
        SetBonuses = sets;
        SetIndex = sets.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        SetTargets = sets.Select(s => setTargets.GetValueOrDefault(s)).ToArray();
        GroupSkills = groups;
        GroupIndex = groups.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        GroupTargets = groups.Select(g => groupTargets.GetValueOrDefault(g)).ToArray();
    }

    public static Relevance Build(GogmaWeaponStats weapon, ResolvedRequest request, GameData data) =>
        Build(weapon, request.TargetSkills, request.Conditions, data, request.TargetSetBonuses, request.TargetGroupSkills);

    /// <param name="setTargets">Required set bonuses with the pieces they need; tracked even when they do not change the score.</param>
    /// <param name="groupTargets">Required group skills with the pieces they need; tracked even when they do not change the score.</param>
    public static Relevance Build(GogmaWeaponStats weapon, IReadOnlyDictionary<string, int> targets, Conditions c, GameData data,
        IReadOnlyDictionary<string, int>? setTargets = null, IReadOnlyDictionary<string, int>? groupTargets = null)
    {
        setTargets ??= new Dictionary<string, int>();
        groupTargets ??= new Dictionary<string, int>();
        var skills = new List<string>();
        var offensive = new HashSet<string>();
        void Add(string name, bool when = true)
        {
            if (!when || !data.SkillsByName.ContainsKey(name)) return;
            if (c.SkillLimits.TryGetValue(name, out var limit) && limit <= 0) return; // excluded from the optimization
            if (!skills.Contains(name)) skills.Add(name);
            offensive.Add(name);
        }

        foreach (var t in targets.Keys)
            if (data.SkillsByName.TryGetValue(t, out var ts) && (ts.Kind is SkillKind.Armor or SkillKind.Weapon) && !skills.Contains(t)) skills.Add(t);

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
        Add(SkillNames.Coalescence, hasElement && c.CoalescenceActive && c.FrenzyOvercome); // only with the Gore set bonus
        Add(SkillNames.ChargeMaster, hasElement && c.ChargedAttack);
        Add(SkillNames.ElementalAbsorption, hasElement && c.ElementalAbsorptionActive);

        var sets = new List<string>();
        void Set(string name, bool when = true) { if (when && !sets.Contains(name)) sets.Add(name); }
        Set(SkillNames.GoreMagalasTyranny);
        Set(SkillNames.LeviathansFury, c.AzureBoltActive);
        Set(SkillNames.SeregiossTenacity, c.AdrenalineRushActive && c.AdrenalineRushRetriggered);
        Set(SkillNames.OmegaResonance, c.Resonance != ResonanceMode.None);
        Set(SkillNames.Gogmapocalypse, hasElement && c.MonsterEnraged);
        var shockwave = c.ProcDamage && weapon.Type == WeaponType.GreatSword && c.AttackProfile.Resolve(weapon.Type).ChargedLv3Share > 0;
        Set(SkillNames.SoulOfTheDarkKnight, (hasElement && c.RedHealth) || shockwave);
        Set(SkillNames.NuUdrasMutiny, c.ProcDamage && c.RedHealth);
        Set(SkillNames.EbonyOdogaronsPower, c.BurstActive);
        Set(SkillNames.DoshagumasMight, c.PowerhouseActive);
        Set(SkillNames.XuWusVigor, c.ProteinFiendActive);
        Set(SkillNames.JinDahaadsRevolt, c.BindingCounterActive);
        Set(SkillNames.BlangongasSpirit, c.WarCryActive);
        foreach (var p in SkillNames.FestivalPrayers) Set(p, c.FestivalActive);
        foreach (var s in setTargets.Keys) Set(s, data.SkillsByName.TryGetValue(s, out var ss) && ss.Kind == SkillKind.Set);

        var groups = new List<string>();
        void Group(string name, bool when) { if (when && !groups.Contains(name)) groups.Add(name); }
        Group(SkillNames.LordsSoul, c.GutsNotYetTriggered);
        Group(SkillNames.LordsFury, c.ResuscitateActive);
        Group(SkillNames.LordsFavor, c.InspirationActive);
        Group(SkillNames.ButteryLeathercraft, c.AffinitySlidingActive);
        foreach (var g in groupTargets.Keys) Group(g, data.SkillsByName.TryGetValue(g, out var gs) && gs.Kind == SkillKind.Group);

        return new Relevance(skills, offensive, data, targets, c.SkillLimits, sets, setTargets, groups, groupTargets);
    }

    public int IndexOf(string skill) => SkillIndex.TryGetValue(skill, out var i) ? i : -1;
}
