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

    public const string NuUdrasMutiny = "Nu Udra's Mutiny";
    public const string ThunderAttack = "Thunder Attack";

    public static string? ElementAttackSkill(Element element) => element switch
    {
        Element.Fire => "Fire Attack",
        Element.Water => "Water Attack",
        Element.Thunder => ThunderAttack,
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
    double ProcDamage,
    double CriticalFactor,
    ResolvedAttackProfile Attack,
    IReadOnlyList<string> Breakdown)
{
    /// <summary>The optimizer's ranking metric: EFR + EFE + proc damage, per 100 motion value of the attack, on the raw-hitzone-100 scale.</summary>
    public double Total => EffectiveRaw + EffectiveElement + ProcDamage;

    /// <summary>Damage of one execution of the attack (or sequence) against the target; ranks like <see cref="Total"/>.</summary>
    public double DamagePerExecution => Attack.DamagePerExecution(Total);

    /// <summary>Damage per minute of attacking at the attack profile's hits per minute.</summary>
    public double DamagePerMinute => Attack.DamagePerMinute(Total);
}

/// <summary>
/// Effective Raw / Effective Element calculator for a Gogma weapon loadout, per 100 MV of the chosen attack:
/// raw = (baseRaw * prod(percent) + sum(flat)) * critFactor * rawFactor (the hits' MV-weighted sharpness and phial);
/// element = min(ele, cap) * critElementFactor * elementFactor (sum of the hits' element modifiers x sharpness x phial,
/// x element hitzone / raw hitzone, x 100 / total MV). Damage is divided by the target's raw hitzone / 100, so EFR keeps its
/// usual raw-hitzone-100 scale and element counts at the target's element hitzone share of it. Proc damage (extra damage
/// instances) is converted to per 100 MV of the attack the same way.
/// </summary>
public static class DamageCalculator
{
    /// <summary>The trace label of the Dark Arts shockwave proc.</summary>
    public const string ShockwaveLabel = "Dark Arts shockwave";

    public static DamageResult Calculate(Loadout loadout, GameData data, Conditions? conditions = null)
    {
        var skills = SkillAggregator.Aggregate(loadout, data);
        return Calculate(loadout.Weapon.Stats, skills, conditions ?? Conditions.Default);
    }

    /// <remarks>
    /// Full health excludes red and low health. When both sides are toggled on, the loadout is scored at whichever side
    /// is better for it, so the search considers Peak Performance builds and Resentment / Dark Arts / Heroics builds alike.
    /// </remarks>
    public static DamageResult Calculate(GogmaWeaponStats weapon, ActiveSkills skills, Conditions cond, bool trace = true)
    {
        if (!cond.FullHealth || !(cond.RedHealth || cond.LowHealth))
            return CalculateSequence(weapon, skills, cond, trace);

        var hurt = cond with { FullHealth = false };
        var full = cond with { RedHealth = false, LowHealth = false };
        // skip the second pass when only one side has skills to trigger
        if (cond.Effective(SkillNames.PeakPerformance, skills.Level(SkillNames.PeakPerformance)) == 0)
            return CalculateSequence(weapon, skills, hurt, trace);
        var hurtSkills = cond.Effective(SkillNames.Resentment, skills.Level(SkillNames.Resentment)) > 0
                         || cond.Effective(SkillNames.Heroics, skills.Level(SkillNames.Heroics)) > 0
                         || skills.SetTier(SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None;
        if (!hurtSkills)
            return CalculateSequence(weapon, skills, full, trace);

        var atFull = CalculateSequence(weapon, skills, full, trace);
        var atHurt = CalculateSequence(weapon, skills, hurt, trace);
        var fullWins = atFull.Total >= atHurt.Total;
        var (best, other) = fullWins ? (atFull, atHurt) : (atHurt, atFull);
        if (!trace) return best;
        var (side, otherSide) = fullWins ? ("full health", "red/low health") : ("red/low health", "full health");
        return best with { Breakdown = [.. best.Breakdown, Inv($"Full health excludes red/low health, scored at {side} ({best.Total:0.#} vs {other.Total:0.#} at {otherSide})")] };
    }

    /// <summary>
    /// The conditions <see cref="Calculate(GogmaWeaponStats, ActiveSkills, Conditions, bool)"/> scores the loadout at: with both
    /// full and red/low health on, the side that is better for it (the other side switched off); otherwise <paramref name="cond"/>.
    /// </summary>
    public static Conditions HealthSide(GogmaWeaponStats weapon, ActiveSkills skills, Conditions cond)
    {
        if (!cond.FullHealth || !(cond.RedHealth || cond.LowHealth)) return cond;
        var hurt = cond with { FullHealth = false };
        var full = cond with { RedHealth = false, LowHealth = false };
        if (cond.Effective(SkillNames.PeakPerformance, skills.Level(SkillNames.PeakPerformance)) == 0) return hurt;
        var hurtSkills = cond.Effective(SkillNames.Resentment, skills.Level(SkillNames.Resentment)) > 0
                         || cond.Effective(SkillNames.Heroics, skills.Level(SkillNames.Heroics)) > 0
                         || skills.SetTier(SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None;
        if (!hurtSkills) return full;
        return CalculateSequence(weapon, skills, full, false).Total >= CalculateSequence(weapon, skills, hurt, false).Total ? full : hurt;
    }

    /// <summary>
    /// A sequence whose steps change conditions is scored per segment (<see cref="Conditions.Segments"/>) and the segments are
    /// weighted by their share of the motion value: damage per 100 MV of the whole sequence. The result shows the stats of the
    /// segment with the most motion value, with EFR, EFE and procs of the whole sequence.
    /// </summary>
    private static DamageResult CalculateSequence(GogmaWeaponStats weapon, ActiveSkills skills, Conditions cond, bool trace)
    {
        var segments = cond.Segments(weapon);
        if (segments.Count == 1) return CalculateAt(weapon, skills, segments[0], trace);

        var results = segments.Select(c => CalculateAt(weapon, skills, c, trace)).ToList();
        var totalMv = results.Sum(r => r.Attack.TotalMv);
        double Weighted(Func<DamageResult, double> value) => results.Sum(r => value(r) * r.Attack.TotalMv / totalMv);
        var main = results.MaxBy(r => r.Attack.TotalMv)!;
        var notes = new List<string>();
        if (trace)
        {
            notes.AddRange(main.Breakdown);
            for (var i = 0; i < segments.Count; i++)
            {
                var r = results[i];
                var changed = ConditionToggles.StepKeys.Where(k => ConditionToggles.Get(segments[i], k) != ConditionToggles.Get(cond, k))
                    .Select(k => $"{k} {(ConditionToggles.Get(segments[i], k) ? "on" : "off")}");
                var steps = string.Join(", ", segments[i].AttackProfile.Sequence!.Select(s => s.Repeat > 1 ? $"{s.Attack} x{s.Repeat}" : s.Attack));
                notes.Add(Inv($"Sequence part {steps} ({r.Attack.TotalMv / totalMv:0%} of the MV{(changed.Any() ? ", " + string.Join(", ", changed) : "")}): affinity {r.Affinity}%, EFR {r.EffectiveRaw:0.#}, EFE {r.EffectiveElement:0.#}, procs {r.ProcDamage:0.#}"));
            }
        }
        return main with
        {
            EffectiveRaw = Weighted(r => r.EffectiveRaw),
            EffectiveElement = Weighted(r => r.EffectiveElement),
            ProcDamage = Weighted(r => r.ProcDamage),
            Attack = cond.Attack(weapon),
            Breakdown = notes,
        };
    }

    private static DamageResult CalculateAt(GogmaWeaponStats weapon, ActiveSkills skills, Conditions cond, bool trace)
    {
        var notes = new List<string>();
        var type = weapon.Type;
        double rawPct = 1.0, rawFlat = 0, elePct = 1.0, eleFlat = 0;
        var affinity = weapon.Affinity;
        if (trace) notes.Add(Inv($"Weapon: {weapon.TrueRaw} raw, {weapon.Affinity}% affinity, {weapon.ElementDisplay} {weapon.Element} (display), {weapon.TopSharpness?.ToString() ?? "no"} sharpness"));

        int L(string skill) => cond.Effective(skill, skills.Level(skill));
        void Pct(string label, double m) { if (m != 1.0) { rawPct *= m; if (trace) notes.Add(Inv($"{label}: raw x{m:0.###}")); } }
        void Flat(string label, int v) { if (v != 0) { rawFlat += v; if (trace) notes.Add(Inv($"{label}: raw +{v}")); } }
        void Aff(string label, int v) { if (v != 0) { affinity += v; if (trace) notes.Add(Inv($"{label}: affinity {(v >= 0 ? "+" : "")}{v}%")); } }
        var hasElement = weapon.ElementTrue > 0;
        void ElePct(string label, double m) { if (hasElement && m != 1.0) { elePct *= m; if (trace) notes.Add(Inv($"{label}: element x{m:0.###}")); } }
        void EleFlat(string label, double v) { if (hasElement && v != 0) { eleFlat += v; if (trace) notes.Add(Inv($"{label}: element +{v:0.#} (true)")); } }

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
        if (cond.HittingWeakPoint && cond.Target.WeakPoint && L(SkillNames.WeaknessExploit) > 0)
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
            var lv = Math.Min(L(eleSkill), 3);
            var (pct, flat) = DamageConstants.ElementAttack(lv);
            ElePct($"{eleSkill} {lv}", pct);
            EleFlat($"{eleSkill} {lv}", flat);
        }
        var gore = skills.SetTier(SkillNames.GoreMagalasTyranny);
        // Coalescence needs a natural recovery from a status; in practice that is the Frenzy cure of the Gore set bonus
        if (gore != SetBonusTier.None && cond.FrenzyOvercome && cond.CoalescenceActive && L(SkillNames.Coalescence) > 0)
            ElePct("Coalescence", DamageConstants.Coalescence(type, L(SkillNames.Coalescence)));
        if (cond.ChargedAttack && L(SkillNames.ChargeMaster) > 0)
            ElePct("Charge Master", DamageConstants.ChargeMaster(L(SkillNames.ChargeMaster)));
        if (cond.ElementalAbsorptionActive && L(SkillNames.ElementalAbsorption) > 0)
        {
            ElePct("Elemental Absorption", DamageConstants.ElementalAbsorptionPercent(L(SkillNames.ElementalAbsorption)));
            EleFlat("Elemental Absorption", DamageConstants.ElementalAbsorptionFlat(type, L(SkillNames.ElementalAbsorption)));
        }

        // ---------------- set bonuses ----------------
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
        var profile = cond.Attack(weapon);
        var efr = trueRaw * critFactor * profile.RawFactor;

        var baseEle = weapon.ElementTrue;
        double ele = 0, cap = 0, critEleMult = 1.0, critEleFactor = 1.0;
        if (baseEle > 0)
        {
            cap = Math.Max(baseEle * DamageConstants.ElementCapRate, baseEle + DamageConstants.ElementCapAdd);
            ele = baseEle * elePct + eleFlat;
            if (ele > cap) { if (trace) notes.Add(Inv($"Element capped at {cap:0.#} (was {ele:0.#})")); ele = cap; }
            critEleMult = DamageConstants.CriticalElement(type, L(SkillNames.CriticalElement));
            critEleFactor = aff > 0 ? 1.0 + aff / 100.0 * (critEleMult - 1.0) : 1.0;
        }
        // element lands per hit with the hit's element modifier at the element hitzone: put it on the per-100-MV, raw-hitzone-100 scale of EFR
        var efe = ele * critEleFactor * profile.ElementFactor;

        // ---------------- proc damage: extra damage instances per 100 MV of landed attacks ----------------
        double procs = 0;
        if (cond.ProcDamage)
        {
            void Proc(string label, double damage, double perExecution, string how)
            {
                if (damage <= 0 || perExecution <= 0) return;
                var v = damage * perExecution * profile.PerHundredMv;
                procs += v;
                if (trace) notes.Add(Inv($"{label}: proc +{v:0.#} per 100 MV ({damage:0.#} damage {how})"));
            }

            // Azure Bolt bursts and Scorcher are not counted: too rare and unreliable to build around (decided 2026-10-07)
            if (CountsShockwave(type, skills, cond))
                Proc(ShockwaveLabel, Shockwave(trueRaw * sharpRaw * critFactor, sharpEle, profile),
                    profile.Shockwaves, Inv($"on {profile.Shockwaves:0.##} Lv3 charged slashes per attack"));
            var badBlood = skills.SetTier(SkillNames.NuUdrasMutiny);
            if (badBlood != SetBonusTier.None && cond.RedHealth && L(SkillNames.Resentment) > 0)
                Proc($"Bad Blood {badBlood}", DamageConstants.BadBlood(badBlood),
                    profile.ProcsPerHit(DamageConstants.BadBloodCooldownSeconds) * profile.Hits, Inv($"every {DamageConstants.BadBloodCooldownSeconds:0} s at most"));
        }

        if (trace) notes.Add(Inv($"Attack: {profile.Name}, {profile.Hits:0.##} hits, {profile.TotalMv:0.#} MV; target {cond.Target.Name}, raw hitzone {cond.Target.RawHitzone:0.#}, element hitzone {cond.Target.ElementHitzoneFor(weapon.Element):0.#}"));
        if (trace) notes.Add(Inv($"Raw {weapon.TrueRaw} x{rawPct:0.###} +{rawFlat:0.#} = {trueRaw:0.#}; affinity {aff}% (crit x{critMult:0.##}) -> factor {critFactor:0.####}; sharpness x{sharpRaw:0.###}, over the attack x{profile.RawFactor:0.###}; EFR {efr:0.#}"));
        if (trace && baseEle > 0)
            notes.Add(Inv($"Element {baseEle:0.#} x{elePct:0.###} +{eleFlat:0.#} = {ele:0.#} (cap {cap:0.#}); crit element x{critEleMult:0.##} -> factor {critEleFactor:0.####}; per 100 MV of the attack at element hitzone x{profile.ElementHitzoneRatio:0.##} of raw x{profile.ElementFactor:0.###}; EFE {efe:0.#}"));

        return new DamageResult(
            weapon.TrueRaw, trueRaw, aff, critMult, weapon.TopSharpness, sharpRaw, efr,
            baseEle, ele, cap, critEleMult, sharpEle, efe, procs, critFactor, profile, notes);
    }

    /// <summary>Damage of one Dark Arts shockwave (30 MV, crits, uses sharpness, plus its dragon element) at raw hitzone 100.</summary>
    /// <param name="effectiveRawPerMv">True raw x sharpness x crit factor.</param>
    public static double Shockwave(double effectiveRawPerMv, double sharpEle, ResolvedAttackProfile profile) =>
        effectiveRawPerMv * DamageConstants.DarkArtsShockwaveMv / 100.0 + DamageConstants.DarkArtsShockwaveElement * sharpEle * profile.ElementHitzoneRatio;

    /// <summary>
    /// One Dark Arts shockwave without a crit, at raw hitzone 100: its raw (30 MV of <paramref name="trueRaw"/> at the weapon's
    /// sharpness; crits like a hit) and its element (fixed, does not crit). <see cref="Shockwave"/> is raw x crit factor + element.
    /// </summary>
    public static (double Raw, double Element) ShockwaveParts(double trueRaw, double sharpRaw, double sharpEle, double elementHitzoneRatio) =>
        (trueRaw * sharpRaw * DamageConstants.DarkArtsShockwaveMv / 100.0, DamageConstants.DarkArtsShockwaveElement * sharpEle * elementHitzoneRatio);

    /// <summary>Whether a Lv3 charged slash of this loadout sets off a Dark Arts shockwave that the score counts.</summary>
    public static bool CountsShockwave(WeaponType type, ActiveSkills skills, Conditions cond) =>
        cond.ProcDamage && type == WeaponType.GreatSword && skills.SetTier(SkillNames.SoulOfTheDarkKnight) != SetBonusTier.None;

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);
}
