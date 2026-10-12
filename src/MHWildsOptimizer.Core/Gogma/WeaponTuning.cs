using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Gogma;

/// <summary>
/// Which parts of the Gogma weapon the optimizer may choose; the others stay as the weapon spec has them. The skill pair is
/// chosen by <see cref="Inputs.SkillPairSettings"/>. Needs the weapon described by a spec.
/// </summary>
public sealed record WeaponOptimizeSettings
{
    public bool Focus { get; init; }
    /// <summary>Picks one of the five elements, with the infusion (an optimized element is always infused).</summary>
    public bool Element { get; init; }
    /// <summary>How many of the three Artian parts give +5 attack rather than +5% affinity.</summary>
    public bool AttackParts { get; init; }
    /// <summary>All five reinforcement lines: which types, at the best tiers up to <see cref="MaxTier"/>.</summary>
    public bool Reinforcements { get; init; }
    /// <summary>The best reinforcement tier to assume (EX is rare: III gives a weapon you can expect to roll).</summary>
    public ReinforcementTier MaxTier { get; init; } = ReinforcementTier.EX;

    public bool Any => Focus || Element || AttackParts || Reinforcements;
}

/// <summary>One choice for a part of the weapon and the best score the build reaches with it (the other optimized parts at their best).</summary>
/// <param name="Part">"Focus", "Element", "Attack parts" or "Reinforcements".</param>
public sealed record WeaponOption(string Part, string Label, double Score, bool Chosen);

/// <summary>
/// The Gogma weapons the optimizer may choose from (<see cref="WeaponOptimizeSettings"/>) and the best of them for a fixed set of
/// armor, talisman and decorations. Reinforcements are chosen by type: n lines of a type take its n best tiers up to the
/// max tier, at most two of each tier (the game's limit on identical lines), sharpness at most twice.
/// </summary>
public sealed class WeaponSpace
{
    public const string FocusPart = "Focus", ElementPart = "Element", PartsPart = "Attack parts", ReinforcementsPart = "Reinforcements";

    private readonly GameData _data;
    private readonly GogmaWeaponSpec _base;

    public WeaponOptimizeSettings Settings { get; }
    /// <summary>Every weapon the settings allow, resolved (rolled pair from the base spec).</summary>
    public IReadOnlyList<GogmaWeaponStats> Variants { get; }

    /// <param name="slots">Decoration slots every variant keeps (the weapon input's, which may override the data).</param>
    public WeaponSpace(GogmaWeaponSpec baseSpec, WeaponOptimizeSettings settings, GameData data, IReadOnlyList<int> slots)
    {
        _data = data;
        _base = baseSpec;
        Settings = settings;
        Variants = Specs().Select(s => s.Resolve(data) with { Slots = slots, SetBonus = baseSpec.SetBonus, GroupSkill = baseSpec.GroupSkill }).ToList();
    }

    private IEnumerable<GogmaWeaponSpec> Specs()
    {
        var type = _base.Type;
        GogmaFocus[] foci = Settings.Focus ? Enum.GetValues<GogmaFocus>() : [_base.Focus];
        Element[] elements = Settings.Element && !type.IsBowgun() ? Enum.GetValues<Element>().Where(e => e != Element.None).ToArray() : [_base.Element];
        int[] parts = Settings.AttackParts ? [.. Enumerable.Range(0, GogmaConstants.PartsCount + 1)] : [_base.AttackParts];
        foreach (var focus in foci)
            foreach (var element in elements)
            {
                var infused = Settings.Element || _base.Infused;
                var lines = Settings.Reinforcements ? ReinforcementSets(type, element, Settings.MaxTier).ToList() : [_base.Reinforcements];
                foreach (var attackParts in parts)
                    foreach (var r in lines)
                        yield return _base with { Focus = focus, Element = element, Infused = infused, AttackParts = attackParts, Reinforcements = r };
            }
    }

    /// <summary>Every split of the five lines over the types that change the score, each type at its best tiers.</summary>
    public static IEnumerable<IReadOnlyList<Reinforcement>> ReinforcementSets(WeaponType type, Element element, ReinforcementTier maxTier)
    {
        var types = new List<ReinforcementType> { ReinforcementType.Attack, ReinforcementType.Affinity };
        if (element != Element.None && !type.IsBowgun()) types.Add(ReinforcementType.Element);
        if (type.HasSharpness()) types.Add(ReinforcementType.Sharpness);

        var counts = new int[types.Count];
        foreach (var split in Splits(0, GogmaConstants.ReinforcementSlots))
            yield return split;

        IEnumerable<IReadOnlyList<Reinforcement>> Splits(int index, int left)
        {
            var t = types[index];
            if (index == types.Count - 1)
            {
                counts[index] = left;
                if (Lines(counts) is { } lines) yield return lines;
                yield break;
            }
            for (var n = 0; n <= left; n++)
            {
                counts[index] = n;
                foreach (var s in Splits(index + 1, left - n)) yield return s;
            }
        }

        List<Reinforcement>? Lines(int[] n)
        {
            var lines = new List<Reinforcement>();
            for (var i = 0; i < types.Count; i++)
            {
                if (n[i] > GogmaConstants.MaxReinforcementsOfType(types[i])) return null;
                var best = BestTiers(types[i], maxTier).Take(n[i]).ToList();
                if (best.Count < n[i]) return null; // not enough distinct tiers below the max tier
                lines.AddRange(best.Select(tier => new Reinforcement(types[i], tier)));
            }
            return lines;
        }
    }

    /// <summary>The tiers of a type at or below the max tier, best first, each twice.</summary>
    private static IEnumerable<ReinforcementTier> BestTiers(ReinforcementType type, ReinforcementTier maxTier) =>
        Enum.GetValues<ReinforcementTier>().Where(t => t <= maxTier && GogmaConstants.TierExists(type, t))
            .OrderByDescending(t => t).SelectMany(t => new[] { t, t });

    /// <summary>The variants carrying another rolled pair (the skill pair classes).</summary>
    public IReadOnlyList<GogmaWeaponStats> WithPair(string? setBonus, string? groupSkill) =>
        Variants.Select(v => v with { SetBonus = setBonus, GroupSkill = groupSkill }).ToList();

    /// <summary>The best variant for the build's armor, talisman and decorations, with the per-part options.</summary>
    /// <param name="variants">This space's variants with the build's rolled pair (<see cref="WithPair"/>).</param>
    public Tuned Tune(Loadout loadout, IReadOnlyList<GogmaWeaponStats> variants, Conditions cond)
    {
        var skills = SkillAggregator.Aggregate(loadout, _data);
        var scores = new double[variants.Count];
        var best = 0;
        for (var i = 0; i < variants.Count; i++)
        {
            scores[i] = DamageCalculator.Calculate(variants[i], skills, cond, trace: false).Total;
            if (scores[i] > scores[best]) best = i;
        }

        var chosen = variants[best];
        var tuned = WithWeapon(loadout, chosen);
        return new Tuned(tuned, DamageCalculator.Calculate(tuned, _data, cond), Options(variants, scores, chosen.Spec!));
    }

    /// <summary>For each optimized part, every choice with the best score it reaches; best first.</summary>
    private List<WeaponOption> Options(IReadOnlyList<GogmaWeaponStats> variants, double[] scores, GogmaWeaponSpec chosen)
    {
        var options = new List<WeaponOption>();
        void Part(bool optimized, string part, Func<GogmaWeaponSpec, string> label)
        {
            if (!optimized) return;
            var mine = label(chosen);
            options.AddRange(variants.Select((v, i) => (Label: label(v.Spec!), Score: scores[i]))
                .GroupBy(x => x.Label)
                .Select(g => new WeaponOption(part, g.Key, g.Max(x => x.Score), g.Key == mine))
                .OrderByDescending(o => o.Score));
        }
        Part(Settings.Focus, FocusPart, s => s.Focus.ToString());
        Part(Settings.Element, ElementPart, s => s.Element.ToString());
        Part(Settings.AttackParts, PartsPart, s => s.PartsLabel);
        Part(Settings.Reinforcements, ReinforcementsPart, s => s.ReinforcementsLabel);
        return options;
    }

    /// <summary>A starting weapon per focus the settings allow: the best variant on the bare weapon, no armor.</summary>
    public IReadOnlyList<GogmaWeaponStats> Starts(Conditions cond)
    {
        var bare = new Loadout { Weapon = new EquippedWeapon(Variants[0]) };
        var skills = SkillAggregator.Aggregate(bare, _data);
        return Variants.GroupBy(v => v.Focus)
            .Select(g => g.MaxBy(v => DamageCalculator.Calculate(v, skills, cond, trace: false).Total)!)
            .OrderBy(v => v.Focus == _base.Focus ? 0 : 1) // the weapon's own focus first
            .ToList();
    }

    public static Loadout WithWeapon(Loadout l, GogmaWeaponStats weapon) => new()
    {
        Weapon = new EquippedWeapon(weapon, l.Weapon.Decorations),
        Head = l.Head, Chest = l.Chest, Arms = l.Arms, Waist = l.Waist, Legs = l.Legs, Talisman = l.Talisman,
    };

    public sealed record Tuned(Loadout Loadout, DamageResult Result, IReadOnlyList<WeaponOption> Options);
}
