using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Damage;

/// <summary>Skill names as they appear in the dataset.</summary>
public static class SkillNames
{
    public const string AttackBoost = "Attack Boost";
    public const string CriticalEye = "Critical Eye";
    public const string CriticalBoost = "Critical Boost";
    public const string CriticalElement = "Critical Element";
    public const string WeaknessExploit = "Weakness Exploit";
    public const string Agitator = "Agitator";
    public const string PeakPerformance = "Peak Performance";
    public const string MaximumMight = "Maximum Might";
    public const string LatentPower = "Latent Power";
    public const string AdrenalineRush = "Adrenaline Rush";
    public const string Counterstrike = "Counterstrike";
    public const string Resentment = "Resentment";
    public const string Heroics = "Heroics";
    public const string Foray = "Foray";
    public const string Burst = "Burst";
    public const string OffensiveGuard = "Offensive Guard";
    public const string PunishingDraw = "Punishing Draw";
    public const string Antivirus = "Antivirus";
    public const string Coalescence = "Coalescence";
    public const string ChargeMaster = "Charge Master";
    public const string ElementalAbsorption = "Elemental Absorption";

    public const string GoreMagalasTyranny = "Gore Magala's Tyranny";
    public const string LeviathansFury = "Leviathan's Fury";
    public const string SeregiossTenacity = "Seregios's Tenacity";
    public const string OmegaResonance = "Omega Resonance";
    public const string Gogmapocalypse = "Gogmapocalypse";
    public const string SoulOfTheDarkKnight = "Soul of the Dark Knight";
    public const string EbonyOdogaronsPower = "Ebony Odogaron's Power";
    public const string DoshagumasMight = "Doshaguma's Might";
    public const string XuWusVigor = "Xu Wu's Vigor";
    public const string JinDahaadsRevolt = "Jin Dahaad's Revolt";
    public const string BlangongasSpirit = "Blangonga's Spirit";
    public static readonly string[] FestivalPrayers = ["Blossomdance Prayer", "Flamefete Prayer", "Dreamspell Prayer", "Lumenhymn Prayer"];

    public const string LordsSoul = "Lord's Soul";
    public const string LordsFury = "Lord's Fury";
    public const string LordsFavor = "Lord's Favor";
    public const string ButteryLeathercraft = "Buttery Leathercraft";

    public static string? ElementAttackSkill(Element element) => element switch
    {
        Element.Fire => "Fire Attack",
        Element.Water => "Water Attack",
        Element.Thunder => "Thunder Attack",
        Element.Ice => "Ice Attack",
        Element.Dragon => "Dragon Attack",
        _ => null,
    };
}

public sealed record DamageResult(
    int BaseTrueRaw,
    double TrueRaw,
    int Affinity,
    double CriticalMultiplier,
    SharpnessColor? Sharpness,
    double SharpnessRawModifier,
    double EffectiveRaw,
    double BaseElementTrue,
    double ElementTrue,
    double ElementCap,
    double CriticalElementMultiplier,
    double SharpnessElementModifier,
    double EffectiveElement,
    IReadOnlyList<string> Breakdown)
{
    /// <summary>The optimizer's ranking metric: EFR + EFE per 100 motion value at hitzone 100.</summary>
    public double Total => EffectiveRaw + EffectiveElement;
}

/// <summary>
/// Effective Raw / Effective Element calculator for a Gogma weapon loadout.
/// raw = (baseRaw * prod(percent) + sum(flat)) * sharpness * critFactor ; element = min(ele, cap) * sharpness * critElementFactor.
/// Hitzone is fixed at 100 and monster resistances are ignored (project assumption).
/// </summary>
public static class DamageCalculator
{
    public static DamageResult Calculate(Loadout loadout, GameData data, Conditions? conditions = null)
    {
        var skills = SkillAggregator.Aggregate(loadout, data);
        var weapon = loadout.Weapon.Spec.Resolve(data);
        return Calculate(weapon, skills, conditions ?? Conditions.Default);
    }

    public static DamageResult Calculate(GogmaWeaponStats weapon, ActiveSkills skills, Conditions cond)
    {
        var notes = new List<string>();
        var type = weapon.Type;
        double rawPct = 1.0, rawFlat = 0, elePct = 1.0, eleFlat = 0;
        var affinity = weapon.Affinity;
        notes.Add(Inv($"Weapon: {weapon.TrueRaw} raw, {weapon.Affinity}% affinity, {weapon.ElementDisplay} {weapon.Element} (display), {weapon.TopSharpness?.ToString() ?? "no"} sharpness"));

        int L(string skill) => skills.Level(skill);
        void Pct(string label, double m) { if (m != 1.0) { rawPct *= m; notes.Add(Inv($"{label}: raw x{m:0.###}")); } }
        void Flat(string label, int v) { if (v != 0) { rawFlat += v; notes.Add(Inv($"{label}: raw +{v}")); } }
        void Aff(string label, int v) { if (v != 0) { affinity += v; notes.Add(Inv($"{label}: affinity {(v >= 0 ? "+" : "")}{v}%")); } }
        void ElePct(string label, double m) { if (m != 1.0) { elePct *= m; notes.Add(Inv($"{label}: element x{m:0.###}")); } }
        void EleFlat(string label, double v) { if (v != 0) { eleFlat += v; notes.Add(Inv($"{label}: element +{v:0.#} (true)")); } }

        // ---------------- weapon skills ----------------
        switch (L(SkillNames.AttackBoost))
        {
            case 1: Flat("Attack Boost 1", 3); break;
            case 2: Flat("Attack Boost 2", 5); break;
            case 3: Flat("Attack Boost 3", 7); break;
            case 4: Pct("Attack Boost 4", 1.02); Flat("Attack Boost 4", 8); break;
            case >= 5: Pct("Attack Boost 5", 1.04); Flat("Attack Boost 5", 9); break;
        }
        Aff("Critical Eye", 4 * L(SkillNames.CriticalEye));
        var critBoostLevel = L(SkillNames.CriticalBoost);

        if (cond.OffensiveGuardActive && L(SkillNames.OffensiveGuard) > 0)
            Pct("Offensive Guard", 1.0 + 0.05 * Math.Min(L(SkillNames.OffensiveGuard), 3));
        if (cond.DrawAttack && L(SkillNames.PunishingDraw) > 0)
            Flat("Punishing Draw", L(SkillNames.PunishingDraw) switch { 1 => 3, 2 => 5, _ => 7 });

        // ---------------- armor skills ----------------
        if (cond.HittingWeakPoint && L(SkillNames.WeaknessExploit) > 0)
        {
            var lv = L(SkillNames.WeaknessExploit);
            Aff("Weakness Exploit", lv switch { 1 => 5, 2 => 10, 3 => 15, 4 => 20, _ => 30 });
            if (cond.HittingWound)
                Aff("Weakness Exploit (wound)", lv switch { 1 => 3, 2 => 5, 3 => 10, 4 => 15, _ => 20 });
        }
        if (cond.MonsterEnraged && L(SkillNames.Agitator) > 0)
        {
            var lv = L(SkillNames.Agitator);
            Flat("Agitator", 4 * lv);
            Aff("Agitator", lv switch { 1 => 3, 2 => 5, 3 => 7, 4 => 10, _ => 15 });
        }
        if (cond.FullHealth && L(SkillNames.PeakPerformance) > 0)
            Flat("Peak Performance", L(SkillNames.PeakPerformance) switch { 1 => 3, 2 => 6, 3 => 10, 4 => 15, _ => 20 });
        if (cond.StaminaFull && L(SkillNames.MaximumMight) > 0)
            Aff("Maximum Might", 10 * Math.Min(L(SkillNames.MaximumMight), 3));
        if (cond.LatentPowerActive && L(SkillNames.LatentPower) > 0)
            Aff("Latent Power", 10 * Math.Min(L(SkillNames.LatentPower), 5));
        if (cond.AdrenalineRushActive && L(SkillNames.AdrenalineRush) > 0)
        {
            Flat("Adrenaline Rush", 5 + 5 * Math.Min(L(SkillNames.AdrenalineRush), 5));
            if (cond.AdrenalineRushRetriggered && skills.SetTier(SkillNames.SeregiossTenacity) == SetBonusTier.II)
                Pct("Razor's Edge II", 1.05);
        }
        if (cond.CounterstrikeActive && L(SkillNames.Counterstrike) > 0)
            Flat("Counterstrike", L(SkillNames.Counterstrike) switch { 1 => 10, 2 => 15, _ => 25 });
        if (cond.RedHealth && L(SkillNames.Resentment) > 0)
            Flat("Resentment", 5 * Math.Min(L(SkillNames.Resentment), 5));
        if (cond.LowHealth && L(SkillNames.Heroics) > 0)
            Pct("Heroics", L(SkillNames.Heroics) switch { 1 => 1.0, 2 or 3 => 1.05, 4 => 1.10, _ => 1.30 });
        if (cond.MonsterStatused && L(SkillNames.Foray) > 0)
        {
            var lv = L(SkillNames.Foray);
            Flat("Foray", lv switch { 1 => 6, 2 => 8, 3 => 10, 4 => 12, _ => 15 });
            Aff("Foray", lv switch { 1 => 0, 2 => 5, 3 => 10, 4 => 15, _ => 20 });
        }
        if (cond.BurstActive && L(SkillNames.Burst) > 0)
        {
            var b = DamageConstants.Burst(type, L(SkillNames.Burst));
            Flat($"Burst {L(SkillNames.Burst)}", b.Attack);
            EleFlat($"Burst {L(SkillNames.Burst)}", b.Element);
            switch (skills.SetTier(SkillNames.EbonyOdogaronsPower))
            {
                case SetBonusTier.I: Flat("Burst Boost I", 8); break;
                case SetBonusTier.II: Flat("Burst Boost II", 18); break;
            }
        }

        // ---------------- element ----------------
        if (weapon.Element != Element.None && SkillNames.ElementAttackSkill(weapon.Element) is { } eleSkill && L(eleSkill) > 0)
        {
            switch (L(eleSkill))
            {
                case 1: EleFlat(eleSkill + " 1", 4); break;
                case 2: ElePct(eleSkill + " 2", 1.10); EleFlat(eleSkill + " 2", 5); break;
                default: ElePct(eleSkill + " 3", 1.20); EleFlat(eleSkill + " 3", 6); break;
            }
        }
        if (cond.CoalescenceActive && L(SkillNames.Coalescence) > 0)
            ElePct("Coalescence", DamageConstants.Coalescence(type, L(SkillNames.Coalescence)));
        if (cond.ChargedAttack && L(SkillNames.ChargeMaster) > 0)
            ElePct("Charge Master", DamageConstants.ChargeMaster(L(SkillNames.ChargeMaster)));
        if (cond.ElementalAbsorptionActive && L(SkillNames.ElementalAbsorption) > 0)
        {
            ElePct("Elemental Absorption", DamageConstants.ElementalAbsorptionPercent(L(SkillNames.ElementalAbsorption)));
            EleFlat("Elemental Absorption", DamageConstants.ElementalAbsorptionFlat(type, L(SkillNames.ElementalAbsorption)));
        }

        // ---------------- set bonuses ----------------
        var gore = skills.SetTier(SkillNames.GoreMagalasTyranny);
        if (gore != SetBonusTier.None)
        {
            if (cond.FrenzyOvercome)
            {
                Aff("Frenzy overcome (Black Eclipse)", DamageConstants.FrenzyOvercomeAffinity);
                if (L(SkillNames.Antivirus) > 0)
                    Aff("Antivirus", L(SkillNames.Antivirus) switch { 1 => 3, 2 => 6, _ => 10 });
            }
            if (gore == SetBonusTier.II)
                Flat("Black Eclipse II", cond.FrenzyOvercome ? 15 : 10);
        }
        if (cond.AzureBoltActive && skills.SetTier(SkillNames.LeviathansFury) != SetBonusTier.None)
            Aff("Azure Bolt", 15);
        var omega = skills.SetTier(SkillNames.OmegaResonance);
        if (omega != SetBonusTier.None)
        {
            if (cond.Resonance == ResonanceMode.Local) Aff("Resonance (Local)", omega == SetBonusTier.II ? 40 : 20);
            if (cond.Resonance == ResonanceMode.Remote) Flat("Resonance (Remote)", omega == SetBonusTier.II ? 20 : 10);
        }
        var gogma = skills.SetTier(SkillNames.Gogmapocalypse);
        if (gogma != SetBonusTier.None && cond.MonsterEnraged)
        {
            ElePct("Mutual Hostility", gogma == SetBonusTier.II ? 1.30 : 1.20);
            EleFlat("Mutual Hostility", gogma == SetBonusTier.II ? 4 : 2);
        }
        if (cond.RedHealth && skills.SetTier(SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None)
            ElePct("Dark Arts", DamageConstants.DarkArts(type));
        if (cond.PowerhouseActive && skills.SetTier(SkillNames.DoshagumasMight) is var dosha && dosha != SetBonusTier.None)
            Flat("Powerhouse", dosha == SetBonusTier.II ? 25 : 10);
        if (cond.ProteinFiendActive && skills.SetTier(SkillNames.XuWusVigor) is var xuwu && xuwu != SetBonusTier.None)
            Flat("Protein Fiend", xuwu == SetBonusTier.II ? 30 : 15);
        if (cond.BindingCounterActive && skills.SetTier(SkillNames.JinDahaadsRevolt) is var jin && jin != SetBonusTier.None)
            Flat("Binding Counter", jin == SetBonusTier.II ? 50 : 25);
        if (cond.WarCryActive && skills.SetTier(SkillNames.BlangongasSpirit) is var blango && blango != SetBonusTier.None)
            Flat("War Cry", blango == SetBonusTier.II ? 20 : 10);
        if (cond.FestivalActive && SkillNames.FestivalPrayers.Any(p => skills.SetTier(p) == SetBonusTier.II))
            Pct("Festival Boon II", 1.09);

        // ---------------- group skills ----------------
        if (cond.GutsNotYetTriggered && skills.GroupActive(SkillNames.LordsSoul))
            Pct("Guts (Lord's Soul)", 1.05);
        if (cond.ResuscitateActive && skills.GroupActive(SkillNames.LordsFury))
            Flat("Resuscitate", 10);
        if (cond.InspirationActive && skills.GroupActive(SkillNames.LordsFavor))
            Flat("Inspiration", 10);
        if (cond.AffinitySlidingActive && skills.GroupActive(SkillNames.ButteryLeathercraft))
            Aff("Affinity Sliding", 30);

        // ---------------- combine ----------------
        var trueRaw = weapon.TrueRaw * rawPct + rawFlat;
        var aff = Math.Clamp(affinity, -DamageConstants.AffinityCap, DamageConstants.AffinityCap);
        var critMult = DamageConstants.CriticalBoost(critBoostLevel);
        var critFactor = aff >= 0
            ? 1.0 + aff / 100.0 * (critMult - 1.0)
            : 1.0 + (-aff) / 100.0 * (DamageConstants.NegativeCriticalMultiplier - 1.0);

        var sharpRaw = weapon.TopSharpness is { } s ? DamageConstants.SharpnessRaw(s) : 1.0;
        var sharpEle = weapon.TopSharpness is { } s2 ? DamageConstants.SharpnessElement(s2) : 1.0;
        var efr = trueRaw * sharpRaw * critFactor;

        var baseEle = weapon.ElementTrue;
        double ele = 0, cap = 0, critEleMult = 1.0, critEleFactor = 1.0;
        if (baseEle > 0)
        {
            cap = Math.Max(baseEle * DamageConstants.ElementCapRate, baseEle + DamageConstants.ElementCapAdd);
            ele = baseEle * elePct + eleFlat;
            if (ele > cap) { notes.Add(Inv($"Element capped at {cap:0.#} (was {ele:0.#})")); ele = cap; }
            critEleMult = DamageConstants.CriticalElement(type, L(SkillNames.CriticalElement));
            critEleFactor = aff > 0 ? 1.0 + aff / 100.0 * (critEleMult - 1.0) : 1.0;
        }
        var efe = ele * sharpEle * critEleFactor;

        notes.Add(Inv($"Raw {weapon.TrueRaw} x{rawPct:0.###} +{rawFlat:0.#} = {trueRaw:0.#}; affinity {aff}% (crit x{critMult:0.##}) -> factor {critFactor:0.####}; sharpness x{sharpRaw:0.###}; EFR {efr:0.#}"));
        if (baseEle > 0)
            notes.Add(Inv($"Element {baseEle:0.#} x{elePct:0.###} +{eleFlat:0.#} = {ele:0.#} (cap {cap:0.#}); crit element x{critEleMult:0.##} -> factor {critEleFactor:0.####}; sharpness x{sharpEle:0.###}; EFE {efe:0.#}"));

        return new DamageResult(
            weapon.TrueRaw, trueRaw, aff, critMult, weapon.TopSharpness, sharpRaw, efr,
            baseEle, ele, cap, critEleMult, sharpEle, efe, notes);
    }

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);
}
