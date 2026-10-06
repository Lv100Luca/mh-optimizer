using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>
/// The Gogma weapon described the way the game shows it (Production Bonus + Reinforcement Bonus screens):
/// { "type": "great-sword", "focus": "attack", "element": "dragon", "infused": true, "attack_parts": 3,
///   "reinforcements": ["sharpness EX", "affinity III", "attack III", "attack III", "affinity EX"] }
/// </summary>
public sealed record GogmaWeaponSpecInput
{
    public required string Type { get; init; }
    public required GogmaFocus Focus { get; init; }
    public Element Element { get; init; } = Element.None;
    /// <summary>"Element Infusion" line present on the Production Bonus screen.</summary>
    public bool Infused { get; init; } = true;
    /// <summary>Number of "Attack Infusion +5" lines (0..3); the others are affinity infusions.</summary>
    public int AttackParts { get; init; } = 3;
    /// <summary>"attack EX", "affinity III", "element II", "sharpness EX", "ammo I" (case-insensitive).</summary>
    public List<string> Reinforcements { get; init; } = [];

    public (GogmaWeaponSpec? Spec, IReadOnlyList<string> Errors) ToSpec()
    {
        var errors = new List<string>();
        WeaponType type;
        try { type = WeaponTypeInfo.FromApiKind(Type); }
        catch (ArgumentException) { return (null, [$"Weapon spec: unknown type '{Type}'."]); }

        var reinforcements = new List<Reinforcement>();
        foreach (var text in Reinforcements)
        {
            var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && Enum.TryParse<ReinforcementType>(parts[0], ignoreCase: true, out var rt)
                && Enum.TryParse<ReinforcementTier>(parts[1], ignoreCase: true, out var tier))
                reinforcements.Add(new Reinforcement(rt, tier));
            else
                errors.Add($"Weapon spec: cannot read reinforcement '{text}' (use e.g. 'attack EX', 'affinity III').");
        }

        var spec = new GogmaWeaponSpec
        {
            Type = type, Focus = Focus, Element = Element, Infused = Infused, AttackParts = AttackParts, Reinforcements = reinforcements,
        };
        return (spec, errors);
    }
}

/// <summary>
/// The Gogma weapon as an input. These stats are never optimized. Either give the in-game stats directly
/// (type, attack, affinity, element, element_display, sharpness) or give <see cref="Spec"/> and let the resolver compute them.
/// set_bonus / group_skill are the currently rolled pair (used in "fixed" skill-pair mode).
/// </summary>
public sealed record WeaponStatsInput
{
    public GogmaWeaponSpecInput? Spec { get; init; }

    /// <summary>API weapon kind, e.g. "great-sword" or "long-sword". Required without <see cref="Spec"/>.</summary>
    public string? Type { get; init; }
    /// <summary>Attack as shown in game (display) unless <see cref="AttackIsDisplay"/> is false. Required without <see cref="Spec"/>.</summary>
    public int? Attack { get; init; }
    public bool AttackIsDisplay { get; init; } = true;
    public int Affinity { get; init; }
    public Element Element { get; init; } = Element.None;
    /// <summary>Element value as shown in game (display = true x 10).</summary>
    public int ElementDisplay { get; init; }
    /// <summary>Sharpness color the weapon attacks at. Defaults to white (all Gogma GS/LS variants top out at white).</summary>
    public SharpnessColor Sharpness { get; init; } = SharpnessColor.White;
    public List<int> Slots { get; init; } = [3, 3, 3];
    public string? SetBonus { get; init; }
    public string? GroupSkill { get; init; }

    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();

        if (Spec is not null)
        {
            var (spec, specErrors) = Spec.ToSpec();
            errors.AddRange(specErrors);
            if (spec is not null)
            {
                errors.AddRange(spec.Validate(data).Select(e => "Weapon spec: " + e));
                if (spec.Type.IsGunner()) errors.Add("Weapon: gunner weapons are out of scope for now.");
            }
        }
        else
        {
            if (Type is null) { errors.Add("Weapon: 'type' is required when no spec is given."); return errors; }
            WeaponType type;
            try { type = WeaponTypeInfo.FromApiKind(Type); }
            catch (ArgumentException) { errors.Add($"Weapon: unknown type '{Type}'."); return errors; }

            if (Attack is null or <= 0) errors.Add("Weapon: 'attack' must be a positive number when no spec is given.");
            if (Affinity is < -100 or > 100) errors.Add("Weapon: affinity must be within -100..100.");
            if (Element != Element.None && ElementDisplay <= 0) errors.Add("Weapon: element set but element_display is 0.");
            if (Element == Element.None && ElementDisplay > 0) errors.Add("Weapon: element_display given but element is none.");
            if (type.IsGunner()) errors.Add("Weapon: gunner weapons are out of scope for now.");
        }

        if (Slots.Any(l => l is < 1 or > 3)) errors.Add("Weapon: slot levels must be 1..3.");
        if (SetBonus is { } sb && (!data.SkillsByName.TryGetValue(sb, out var s1) || s1.Kind != SkillKind.Set))
            errors.Add($"Weapon: '{sb}' is not a set-bonus skill.");
        if (GroupSkill is { } gs && (!data.SkillsByName.TryGetValue(gs, out var s2) || s2.Kind != SkillKind.Group))
            errors.Add($"Weapon: '{gs}' is not a group skill.");
        return errors;
    }

    /// <summary>Resolves the stats; call <see cref="Validate"/> first.</summary>
    public GogmaWeaponStats ToStats(GameData data)
    {
        if (Spec is not null)
        {
            var (spec, errors) = Spec.ToSpec();
            if (spec is null) throw new InvalidOperationException(string.Join(" ", errors));
            var resolved = (spec with { SetBonus = SetBonus, GroupSkill = GroupSkill }).Resolve(data);
            return Slots.Count > 0 ? resolved with { Slots = Slots } : resolved;
        }

        var type = WeaponTypeInfo.FromApiKind(Type ?? throw new InvalidOperationException("Weapon type missing."));
        var attack = Attack ?? throw new InvalidOperationException("Weapon attack missing.");
        var trueRaw = AttackIsDisplay ? (int)Math.Round(attack / type.Bloat()) : attack;
        return new GogmaWeaponStats(
            type, null, trueRaw, Affinity, Element, Element == Element.None ? 0 : ElementDisplay,
            type.HasSharpness() ? Sharpness : null, null, 0, Slots, SetBonus, GroupSkill);
    }
}
