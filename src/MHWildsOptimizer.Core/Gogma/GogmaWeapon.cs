using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Gogma;

public sealed record Reinforcement(ReinforcementType Type, ReinforcementTier Tier)
{
    public override string ToString() => $"{Type} {Tier}";
}

/// <summary>Everything that defines one concrete Gogma Artian weapon: type, focus, parts, reinforcements and the rolled skill pair.</summary>
public sealed record GogmaWeaponSpec
{
    public required WeaponType Type { get; init; }
    public required GogmaFocus Focus { get; init; }
    public Element Element { get; init; } = Element.None;
    /// <summary>All three parts share the element (+30/+20 display element).</summary>
    public bool Infused { get; init; } = true;
    /// <summary>How many of the 3 Artian parts carry the +5 attack bonus; the rest carry +5% affinity.</summary>
    public int AttackParts { get; init; } = 3;
    public IReadOnlyList<Reinforcement> Reinforcements { get; init; } = [];
    /// <summary>Rolled set-bonus skill name (counts as one equipped piece), e.g. "Gore Magala's Tyranny".</summary>
    public string? SetBonus { get; init; }
    /// <summary>Rolled group skill name (counts as one equipped piece), e.g. "Lord's Soul".</summary>
    public string? GroupSkill { get; init; }

    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();

        if (AttackParts is < 0 or > GogmaConstants.PartsCount)
            errors.Add($"AttackParts must be 0..{GogmaConstants.PartsCount}, got {AttackParts}.");

        if (Reinforcements.Count > GogmaConstants.ReinforcementSlots)
            errors.Add($"At most {GogmaConstants.ReinforcementSlots} reinforcements, got {Reinforcements.Count}.");

        foreach (var r in Reinforcements)
        {
            if (!GogmaConstants.TierExists(r.Type, r.Tier))
                errors.Add($"Reinforcement {r} does not exist in the game.");
            if (r.Type == ReinforcementType.Element && (Element == Element.None || Type.IsBowgun()))
                errors.Add($"Element reinforcement on a weapon without an element ({r}).");
            if (r.Type == ReinforcementType.Sharpness && Type.IsGunner())
                errors.Add("Sharpness reinforcement on a gunner weapon.");
            if (r.Type == ReinforcementType.Ammo && !Type.IsGunner())
                errors.Add("Ammo reinforcement on a melee weapon.");
        }

        foreach (var g in Reinforcements.GroupBy(r => r))
            if (g.Count() > GogmaConstants.MaxIdenticalReinforcements)
                errors.Add($"More than {GogmaConstants.MaxIdenticalReinforcements}x {g.Key}.");

        foreach (var g in Reinforcements.GroupBy(r => r.Type))
            if (g.Count() > GogmaConstants.MaxReinforcementsOfType(g.Key))
                errors.Add($"More than {GogmaConstants.MaxReinforcementsOfType(g.Key)} {g.Key} reinforcements.");

        if (Element != Element.None && Type.IsBowgun())
            errors.Add("Bowguns are ammo based and carry no element value.");

        if (SetBonus is not null)
        {
            if (!data.SkillsByName.TryGetValue(SetBonus, out var s) || s.Kind != SkillKind.Set)
                errors.Add($"'{SetBonus}' is not a set-bonus skill.");
        }

        if (GroupSkill is not null)
        {
            if (!data.SkillsByName.TryGetValue(GroupSkill, out var s) || s.Kind != SkillKind.Group)
                errors.Add($"'{GroupSkill}' is not a group skill.");
        }

        if (!data.GogmaWeapons.ContainsKey(Type))
            errors.Add($"No Gogma weapon data for {Type}.");

        return errors;
    }

    /// <summary>Resolves the final weapon stats before any skills.</summary>
    public GogmaWeaponStats Resolve(GameData data)
    {
        var variant = data.GogmaVariant(Type, Focus);

        var raw = variant.Raw
                  + AttackParts * GogmaConstants.PartAttackBonus
                  + Reinforcements.Where(r => r.Type == ReinforcementType.Attack).Sum(r => GogmaConstants.AttackReinforcement(r.Tier));

        var affinity = variant.Affinity
                       + (GogmaConstants.PartsCount - AttackParts) * GogmaConstants.PartAffinityBonus
                       + Reinforcements.Where(r => r.Type == ReinforcementType.Affinity).Sum(r => GogmaConstants.AffinityReinforcement(r.Tier));

        var elementDisplay = 0;
        if (Element != Element.None && !Type.IsBowgun())
        {
            elementDisplay = GogmaConstants.ElementBaseDisplay(Type)
                             + (Infused ? GogmaConstants.InfusionBonusDisplay(Type) : 0)
                             + GogmaConstants.FocusElementDeltaDisplay(Type, Focus)
                             + Reinforcements.Where(r => r.Type == ReinforcementType.Element).Sum(r => GogmaConstants.ElementReinforcementDisplay(Type, r.Tier));
        }

        var sharpnessBonus = Reinforcements.Where(r => r.Type == ReinforcementType.Sharpness).Sum(r => GogmaConstants.SharpnessReinforcement(r.Tier));

        return new GogmaWeaponStats(
            Type, Focus, raw, affinity, Element, elementDisplay,
            variant.Sharpness?.TopColor, variant.Sharpness, sharpnessBonus, variant.Slots, SetBonus, GroupSkill);
    }
}

public sealed record GogmaWeaponStats(
    WeaponType Type,
    GogmaFocus Focus,
    int TrueRaw,
    int Affinity,
    Element Element,
    int ElementDisplay,
    SharpnessColor? TopSharpness,
    SharpnessBar? SharpnessBar,
    int SharpnessBonus,
    IReadOnlyList<int> Slots,
    string? SetBonus,
    string? GroupSkill)
{
    public double ElementTrue => ElementDisplay / 10.0;
    public int DisplayAttack => (int)Math.Round(TrueRaw * Type.Bloat());
}
