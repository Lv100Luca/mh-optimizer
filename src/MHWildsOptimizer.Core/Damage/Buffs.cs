namespace MHWildsOptimizer.Core.Damage;

public enum BuffCategory { Item, Meal, Support }

/// <summary>What a buff changes: flat raw and raw multipliers average over their uptime; affinity and element do not, so they split the hunt.</summary>
public enum BuffEffect { RawFlat, RawMultiplier, Affinity, ElementMultiplier }

public readonly record struct BuffPart(BuffEffect Effect, double Value);

/// <summary>
/// An external buff: an item, a meal skill or what a teammate gives. Unlike a skill it does not depend on the build, so it
/// counts for every build alike. <see cref="Conditions.Buffs"/> holds the active ones by <see cref="Id"/> with their uptime.
/// </summary>
public sealed record BuffDef(string Id, string Name, BuffCategory Category, string Description, BuffPart[] Parts)
{
    /// <summary>Buffs of one slot replace each other; the UI keeps one per slot and the calculator counts only the strongest.</summary>
    public string? Slot { get; init; }
    /// <summary>The share of the hunt it is up when the player cannot choose it (Caprice Meal).</summary>
    public double? FixedUptime { get; init; }
    /// <summary>Whether the player sets an uptime. Flat raw and raw multipliers average over it; others split the hunt (<see cref="Splits"/>).</summary>
    public bool Timed { get; init; }
    /// <summary>
    /// A timed buff whose affinity or element does not average (the caps): the hunt is scored in a part with it and one without
    /// (<see cref="Conditions.UptimeParts"/>), so within a part it is fully on.
    /// </summary>
    public bool Splits => Timed && Parts.Any(p => p.Effect is BuffEffect.Affinity or BuffEffect.ElementMultiplier);
    /// <summary>The uptime a newly picked timed buff starts with.</summary>
    public double DefaultUptime { get; init; } = 1;
    /// <summary>A hunt condition (snake_case key) that must be on as well, e.g. low health for Piping-Hot Meal.</summary>
    public string? Requires { get; init; }
    public string Source { get; init; } = "";
}

/// <summary>A meal the Buffs tab offers as a shortcut: its attack and the damage meal skills it always brings.</summary>
public sealed record MealPreset(string Name, int Attack, string[] Skills, string Description);

/// <summary>The external buffs the calculator knows. Numbers from the game data (PlayerItemParam, PlayerMealSkillParam) unless noted.</summary>
public static class BuffCatalog
{
    public const string SlotDrink = "drink";
    /// <summary>Might Seed, Might Pill and the Protein Fiend set bonus share one boost: only the highest applies.</summary>
    public const string SlotMight = "might";
    public const string SlotCaprice = "caprice";
    public const string SlotHornAttack = "hh-attack";
    public const string SlotHornElement = "hh-element";
    public const string SlotPalicoHorn = "palico-horn";

    // Caprice: 10 s every 180 / 240 / 300 s (34 / 33 / 33 %), so about 4% of the time
    public static readonly double CapriceUptime = 10.0 / (0.34 * 180 + 0.33 * 240 + 0.33 * 300);

    public const string MightSeed = "might-seed";
    public const string MightPill = "might-pill";

    public static readonly IReadOnlyList<BuffDef> All =
    [
        new("demondrug", "Demondrug", BuffCategory.Item, "Attack +5 for 30 minutes.", [new(BuffEffect.RawFlat, 5)])
            { Slot = SlotDrink, Source = "PlayerItemParam _KijinDrink_AddAttack 5" },
        new("mega-demondrug", "Mega Demondrug", BuffCategory.Item, "Attack +7 for 30 minutes. Replaces a Demondrug.", [new(BuffEffect.RawFlat, 7)])
            { Slot = SlotDrink, Source = "PlayerItemParam _KijinDrink_G_AddAttack 7" },
        new(MightSeed, "Might Seed", BuffCategory.Item, "Attack +10 for 3 minutes. Shares one boost with the Might Pill and Protein Fiend: only the highest counts.",
            [new(BuffEffect.RawFlat, 10)]) { Slot = SlotMight, Timed = true, Source = "PlayerItemParam _Kairiki_AddAttack 10" },
        new(MightPill, "Might Pill", BuffCategory.Item, "Attack +25 for 21 seconds. Overrides a Might Seed and Protein Fiend while it runs.",
            [new(BuffEffect.RawFlat, 25)]) { Slot = SlotMight, Timed = true, DefaultUptime = 0.05, Source = "PlayerItemParam _Kairiki_G_AddAttack 25" },
        new("demon-powder", "Demon Powder", BuffCategory.Item, "Attack +10 for 3 minutes for everyone nearby. Stacks with the drinks, seeds and pills.",
            [new(BuffEffect.RawFlat, 10)]) { Timed = true, Source = "PlayerItemParam _KijinPowder_AddAttack 10; stacking from community reports" },

        new("caprice-lo", "Caprice Meal (Lo)", BuffCategory.Meal, "Attack +7 for 10 s every 3 to 5 minutes: about 4% of the time.",
            [new(BuffEffect.RawFlat, 7)]) { Slot = SlotCaprice, FixedUptime = CapriceUptime, Source = "PlayerMealSkillParam _Uneven_S_AttackAdd 7, _Uneven_IntervalList" },
        new("caprice-hi", "Caprice Meal (Hi)", BuffCategory.Meal, "Attack +15 for 10 s every 3 to 5 minutes: about 4% of the time.",
            [new(BuffEffect.RawFlat, 15)]) { Slot = SlotCaprice, FixedUptime = CapriceUptime, Source = "PlayerMealSkillParam _Uneven_L_AttackAdd 15, _Uneven_IntervalList" },
        new("swimmer", "Swimmer Meal", BuffCategory.Meal, "Attack +3 while wet. The uptime is the share of the hunt you are wet.",
            [new(BuffEffect.RawFlat, 3)]) { Timed = true, DefaultUptime = 0.5, Source = "PlayerMealSkillParam _WaterAdjust_AddAttack 3" },
        new("break-time", "Break-time Meal", BuffCategory.Meal, "Sitting (gesture) for 5 s gives attack +15 for 30 s.",
            [new(BuffEffect.RawFlat, 15)]) { Timed = true, DefaultUptime = 0.25, Source = "PlayerMealSkillParam _RestBuff_AttackAdd 15, _RestBuff_Time 30" },
        new("bulldozer", "Bulldozer Meal", BuffCategory.Meal, "Attack x1.05 for 5 s after an attack bounces.",
            [new(BuffEffect.RawMultiplier, 1.05)]) { Timed = true, DefaultUptime = 0.1, Source = "PlayerMealSkillParam _RecoilAttack_Rate 1.05, _RecoilAttack_Time 5" },
        new("piping-hot", "Piping-Hot Meal", BuffCategory.Meal, "Attack x1.35 while health is very low (10 or less). Counts on the low health side: turn on Low health under Conditions.",
            [new(BuffEffect.RawMultiplier, 1.35)]) { Requires = "low_health", Source = "PlayerMealSkillParam _Heroics_AttackRate 1.35, _Heroics_HealthBorder 10" },

        // Hunting Horn: a melody played twice (or by an encore) reaches its second tier; the same melody refreshes, never stacks
        new("hh-attack-s", "Attack Up (S)", BuffCategory.Support, "Hunting Horn melody: raw x1.03.",
            [new(BuffEffect.RawMultiplier, 1.03)]) { Slot = SlotHornAttack, Timed = true, Source = "PlayerMusicSkillParam Attack Up (S) _Value" },
        new("hh-attack-s2", "Attack Up (S), encore", BuffCategory.Support, "Hunting Horn melody at its second tier: raw x1.05.",
            [new(BuffEffect.RawMultiplier, 1.05)]) { Slot = SlotHornAttack, Timed = true, Source = "PlayerMusicSkillParam Attack Up (S) _WValue" },
        new("hh-attack-l", "Attack Up (L)", BuffCategory.Support, "Hunting Horn melody: raw x1.05.",
            [new(BuffEffect.RawMultiplier, 1.05)]) { Slot = SlotHornAttack, Timed = true, Source = "PlayerMusicSkillParam Attack Up (L) _Value" },
        new("hh-attack-l2", "Attack Up (L), encore", BuffCategory.Support, "Hunting Horn melody at its second tier: raw x1.10.",
            [new(BuffEffect.RawMultiplier, 1.10)]) { Slot = SlotHornAttack, Timed = true, Source = "PlayerMusicSkillParam Attack Up (L) _WValue" },
        new("hh-affinity", "Affinity Up", BuffCategory.Support, "Hunting Horn melody (with Health Recovery S): affinity +15 at either tier.",
            [new(BuffEffect.Affinity, 15)]) { Timed = true, Source = "PlayerMusicSkillParam Affinity Up _Value / _WValue 15" },
        new("hh-element", "Elemental Attack Boost", BuffCategory.Support, "Hunting Horn melody: element x1.08.",
            [new(BuffEffect.ElementMultiplier, 1.08)]) { Slot = SlotHornElement, Timed = true, Source = "PlayerMusicSkillParam Elemental Attack Boost _Value" },
        new("hh-element2", "Elemental Attack Boost, encore", BuffCategory.Support, "Hunting Horn melody at its second tier: element x1.10.",
            [new(BuffEffect.ElementMultiplier, 1.10)]) { Slot = SlotHornElement, Timed = true, Source = "PlayerMusicSkillParam Elemental Attack Boost _WValue" },
        new("hh-bubble", "Echo Bubble: Attack & Affinity Up", BuffCategory.Support, "Raw x1.10 and affinity +25 while standing in the bubble. Whether it stacks with the Attack Up melodies is not confirmed.",
            [new(BuffEffect.RawMultiplier, 1.10), new(BuffEffect.Affinity, 25)]) { Timed = true, DefaultUptime = 0.25, Source = "PlayerMusicSkillParam HIBIKI_ATK_UP_CRT_UP" },
        new("palico-horn-s", "Palico horn: Attack Up (S)", BuffCategory.Support, "Raw x1.10 for 100 s; the Palico plays it at random, at most every 10 minutes (8 when powered up).",
            [new(BuffEffect.RawMultiplier, 1.10)]) { Slot = SlotPalicoHorn, Timed = true, DefaultUptime = 0.1, Source = "PlayerOtomoMusicSkillParam Attack Up S" },
        new("palico-horn-l", "Palico horn: Attack Up (L)", BuffCategory.Support, "Raw x1.15 for 120 s; the Palico plays it at random, at most every 10 minutes (8 when powered up).",
            [new(BuffEffect.RawMultiplier, 1.15)]) { Slot = SlotPalicoHorn, Timed = true, DefaultUptime = 0.1, Source = "PlayerOtomoMusicSkillParam Attack Up L" },
        new("demon-ammo", "Demon Ammo", BuffCategory.Support, "A teammate's bowgun: attack +10 for 3 minutes. Probably stacks with Demon Powder (separate in the game data, not confirmed).",
            [new(BuffEffect.RawFlat, 10)]) { Timed = true, Source = "PlayerItemParam _KijinAmmo" },
    ];

    public static readonly IReadOnlyDictionary<string, BuffDef> ById = All.ToDictionary(b => b.Id);

    public static readonly IReadOnlyList<MealPreset> Meals =
    [
        new("No meal, or a fish or veggie grill", 0, [], "No meal attack. A grill's ingredients add their skills: pick them below."),
        new("Grill: meat ration", 2, [], "Attack +2. The ingredients add their skills: pick them below."),
        new("Grill: meat ration + Airy Egg", 2, ["caprice-hi"], "Attack +2 and Caprice Meal (Hi)."),
        new("Settlement or Canteen meal", 5, [], "Attack +5: Kunafa, Azuz and Sild cuisine, Blossomdance and Lumenhymn meals. Their fixed skills do not add damage."),
        new("Colorful Suja Cuisine", 5, ["caprice-hi"], "Attack +5 and Caprice Meal (Hi)."),
        new("Flamefete Barbecue Meal", 5, ["swimmer"], "Attack +5 and Swimmer Meal."),
        new("Dreamspell Tea Time Stand", 5, ["break-time"], "Attack +5 and Break-time Meal."),
    ];

    /// <summary>"Mega Demondrug, Might Seed 50%": the buffs set, in catalog order, with the uptime of timed ones; "none" without any.</summary>
    public static string Text(IReadOnlyDictionary<string, double> buffs)
    {
        var text = string.Join(", ", All.Where(b => buffs.ContainsKey(b.Id))
            .Select(b => b.Timed && buffs[b.Id] < 1 ? FormattableString.Invariant($"{b.Name} {buffs[b.Id]:0%}") : b.Name));
        return text.Length > 0 ? text : "none";
    }

    /// <summary>
    /// The share of the hunt a buff counts at with <paramref name="uptime"/> as set. A buff that splits the hunt is fully on in its
    /// part (the parts carry it at uptime 1, see <see cref="Conditions.UptimeParts"/>).
    /// </summary>
    public static double Share(BuffDef buff, double uptime) =>
        buff.FixedUptime ?? (!buff.Timed ? 1 : buff.Splits ? (uptime > 0 ? 1 : 0) : Math.Clamp(uptime, 0, 1));

    /// <summary>
    /// The buffs that split the hunt and are up only part of it, by uptime: buffs with the same uptime are taken to run at the same
    /// time (one horn player keeps Affinity Up and Elemental Attack Boost up together), so each uptime is one on / off split.
    /// </summary>
    public static IReadOnlyList<(double Uptime, string[] Ids)> Splitting(Conditions cond) =>
        Active(cond)
            .Where(x => x.Buff.Splits && cond.Buffs[x.Buff.Id] is > 0 and < 1)
            .GroupBy(x => cond.Buffs[x.Buff.Id])
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Select(x => x.Buff.Id).ToArray()))
            .ToList();

    /// <summary>
    /// The buffs that count under <paramref name="cond"/> with their share of the hunt: known, on (share above 0), their required
    /// condition on, and per slot the strongest only.
    /// </summary>
    public static IEnumerable<(BuffDef Buff, double Share)> Active(Conditions cond) =>
        cond.Buffs
            .Select(kv => (Buff: ById.GetValueOrDefault(kv.Key), kv.Value))
            .Where(x => x.Buff is not null && (x.Buff.Requires is null || ConditionToggles.Get(cond, x.Buff.Requires)))
            .Select(x => (Buff: x.Buff!, Share: Share(x.Buff!, x.Value)))
            .Where(x => x.Share > 0)
            .GroupBy(x => x.Buff.Slot ?? x.Buff.Id)
            .Select(g => g.OrderByDescending(x => x.Buff.Parts.Sum(p => p.Effect is BuffEffect.RawMultiplier or BuffEffect.ElementMultiplier ? p.Value - 1 : p.Value) * x.Share).First());
}
