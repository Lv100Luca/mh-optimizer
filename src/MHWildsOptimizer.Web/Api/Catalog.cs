using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Web.Api;

public sealed record SkillDto(int Id, string Name, SkillKind Kind, int MaxLevel, string? Description, string? Icon, IReadOnlyList<SkillRank> Ranks);

public sealed record DecorationDto(int Id, string Name, SkillKind Kind, int Slot, int Rarity, string? IconColor, IReadOnlyList<SkillGrant> Skills);

public sealed record ArmorSetPieceDto(int Id, string Name, ArmorPieceKind Kind, IReadOnlyList<SkillGrant> Skills, IReadOnlyList<int> Slots, IReadOnlyList<int> SlotsTranscended);

public sealed record ArmorSetDto(string Name, int Rarity, IReadOnlyList<string> SetBonus, string? GroupSkill, IReadOnlyList<ArmorSetPieceDto> Pieces);

public sealed record SharpnessBarDto(int Red, int Orange, int Yellow, int Green, int Blue, int White, int Purple);

public sealed record GogmaVariantDto(string Name, int Raw, int DisplayAttack, int Affinity, SharpnessBarDto? Sharpness, IReadOnlyList<int> Slots);

/// <summary>The attack profile preset of a weapon type (what an unset <c>conditions.attack_profile</c> value falls back to).</summary>
public sealed record AttackProfileDto(double HitsPerMinute, double AverageMv, double ChargedLv3Share);

public sealed record WeaponTypeDto(
    string Kind,
    string Label,
    bool Gunner,
    bool Supported,
    double Bloat,
    IReadOnlyList<SkillGrantDto> CoreSkills,
    int ElementBaseDisplay,
    int InfusionBonusDisplay,
    AttackProfileDto AttackProfile,
    IReadOnlyDictionary<string, GogmaVariantDto> Variants);

public sealed record SkillGrantDto(string Skill, int Level);

public sealed record ReinforcementOptionDto(ReinforcementType Type, ReinforcementTier Tier, string Value, string Label, bool NeedsElement, bool MeleeOnly, bool GunnerOnly);

public sealed record SkillPoolDto(IReadOnlyList<string> SetBonuses, IReadOnlyList<string> GroupSkills, IReadOnlyList<GogmaSkillPair> Pairs);

public sealed record PoolSkillDto(string Skill, int MaxLevel);

public sealed record TalismanPoolDto(IReadOnlyList<PoolSkillDto> Primary, IReadOnlyList<PoolSkillDto> Secondary, IReadOnlyList<IReadOnlyList<string>> SlotPatterns, IReadOnlyList<int> Rarities);

/// <summary>Everything the client needs to render pickers: skills, decorations, armor sets, weapon types, pools and condition texts.</summary>
public sealed record Catalog(
    string GameVersion,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<DecorationDto> Decorations,
    IReadOnlyList<ArmorSetDto> ArmorSets,
    IReadOnlyList<WeaponTypeDto> WeaponTypes,
    IReadOnlyList<ReinforcementOptionDto> Reinforcements,
    IReadOnlyList<string> Elements,
    IReadOnlyList<string> SharpnessColors,
    SkillPoolDto SkillPool,
    TalismanPoolDto? TalismanPool,
    IReadOnlyList<ConditionDto> Conditions,
    IReadOnlyList<string> ResonanceModes,
    int CraftableTalismanCount,
    OptimizationRequest DefaultRequest,
    Conditions ConditionsDefault,
    Conditions ConditionsAllOn,
    Conditions ConditionsAllOff)
{
    public static Catalog Build(GameData data)
    {
        var skills = data.Skills
            .Select(s => new SkillDto(s.Id, s.Name, s.Kind, s.MaxLevel, s.Description, s.Icon, s.Ranks))
            .OrderBy(s => s.Kind).ThenBy(s => s.Name)
            .ToList();

        var decorations = data.Decorations
            .Select(d => new DecorationDto(d.Id, d.Name, d.Kind, d.Slot, d.Rarity, d.IconColor, d.Skills))
            .ToList();

        var sets = data.Armor
            .Where(a => a.Set is not null)
            .GroupBy(a => a.Set!)
            .Select(g => new ArmorSetDto(
                g.Key,
                g.Max(a => a.Rarity),
                g.SelectMany(a => a.SetBonus).Distinct().OrderBy(x => x).ToList(),
                g.Select(a => a.GroupSkill).FirstOrDefault(x => x is not null),
                g.OrderBy(a => a.Piece).Select(a => new ArmorSetPieceDto(a.Id, a.Name, a.Piece, a.Skills, a.Slots, a.SlotsTranscended)).ToList()))
            .OrderByDescending(s => s.Rarity).ThenBy(s => s.Name)
            .ToList();

        var weaponTypes = Enum.GetValues<WeaponType>()
            .Select(t => new WeaponTypeDto(
                t.ApiKind(),
                WeaponLabel(t),
                t.IsGunner(),
                !t.IsGunner() && data.GogmaWeapons.ContainsKey(t),
                t.Bloat(),
                WeaponCoreSkills.For(t).Select(c => new SkillGrantDto(c.Skill, c.Level)).ToList(),
                GogmaConstants.ElementBaseDisplay(t),
                GogmaConstants.InfusionBonusDisplay(t),
                Profile(AttackProfile.Preset(t)),
                data.GogmaWeapons.TryGetValue(t, out var byFocus)
                    ? byFocus.ToDictionary(
                        kv => kv.Key.ToString().ToLowerInvariant(),
                        kv => new GogmaVariantDto(kv.Value.Name, kv.Value.Raw, kv.Value.DisplayAttack, kv.Value.Affinity,
                            kv.Value.Sharpness is { } sb ? new SharpnessBarDto(sb.Red, sb.Orange, sb.Yellow, sb.Green, sb.Blue, sb.White, sb.Purple) : null,
                            kv.Value.Slots))
                    : new Dictionary<string, GogmaVariantDto>()))
            .ToList();

        var reinforcements = new List<ReinforcementOptionDto>();
        foreach (var rt in Enum.GetValues<ReinforcementType>())
            foreach (var tier in Enum.GetValues<ReinforcementTier>())
                if (GogmaConstants.TierExists(rt, tier))
                    reinforcements.Add(new ReinforcementOptionDto(rt, tier, $"{rt.ToString().ToLowerInvariant()} {tier}", $"{rt} {tier}",
                        rt == ReinforcementType.Element, rt == ReinforcementType.Sharpness, rt == ReinforcementType.Ammo));

        var pool = new SkillPoolDto(
            data.GogmaSkillPairs.Select(p => p.SetBonus).Distinct().OrderBy(x => x).ToList(),
            data.GogmaSkillPairs.Select(p => p.GroupSkill).Distinct().OrderBy(x => x).ToList(),
            data.GogmaSkillPairs);

        TalismanPoolDto? talismanPool = null;
        if (data.TalismanPool is { } tp)
        {
            talismanPool = new TalismanPoolDto(
                tp.Skills.Where(kv => kv.Value.Group == "primary").Select(kv => new PoolSkillDto(kv.Key, kv.Value.MaxLevel)).OrderBy(x => x.Skill).ToList(),
                tp.Skills.Where(kv => kv.Value.Group == "secondary").Select(kv => new PoolSkillDto(kv.Key, kv.Value.MaxLevel)).OrderBy(x => x.Skill).ToList(),
                tp.SlotPatterns.Select(sp => (IReadOnlyList<string>)sp.Slots.Select(s => s.ToString()).ToList()).ToList(),
                tp.RarityByType.Values.Where(r => r > 0).Distinct().OrderBy(r => r).ToList());
        }

        return new Catalog(
            "1.041",
            skills,
            decorations,
            sets,
            weaponTypes,
            reinforcements,
            Enum.GetNames<Element>().Select(e => e.ToLowerInvariant()).ToList(),
            Enum.GetNames<SharpnessColor>().Select(e => e.ToLowerInvariant()).ToList(),
            pool,
            talismanPool,
            ConditionCatalog.Build(),
            Enum.GetNames<ResonanceMode>().Select(e => e.ToLowerInvariant()).ToList(),
            data.CraftableTalismans.Count,
            NewRequest(),
            Core.Damage.Conditions.Default,
            Core.Damage.Conditions.AllOn,
            Core.Damage.Conditions.AllOff);
    }

    /// <summary>The same starting point as the CLI editor: a raw attack-focus Great Sword with no rolled pair.</summary>
    public static OptimizationRequest NewRequest() => new()
    {
        Weapon = new WeaponStatsInput
        {
            Spec = new GogmaWeaponSpecInput { Type = "great-sword", Focus = GogmaFocus.Attack, Element = Element.None, Infused = false, AttackParts = 3 },
        },
        Talismans = new TalismanSettings { IncludeCraftable = true },
    };

    private static AttackProfileDto Profile(ResolvedAttackProfile p) => new(p.HitsPerMinute, p.AverageMv, p.ChargedLv3Share);

    public static string WeaponLabel(WeaponType type) => type switch
    {
        WeaponType.GreatSword => "Great Sword",
        WeaponType.SwordAndShield => "Sword & Shield",
        WeaponType.DualBlades => "Dual Blades",
        WeaponType.LongSword => "Long Sword",
        WeaponType.Hammer => "Hammer",
        WeaponType.HuntingHorn => "Hunting Horn",
        WeaponType.Lance => "Lance",
        WeaponType.Gunlance => "Gunlance",
        WeaponType.SwitchAxe => "Switch Axe",
        WeaponType.ChargeBlade => "Charge Blade",
        WeaponType.InsectGlaive => "Insect Glaive",
        WeaponType.Bow => "Bow",
        WeaponType.HeavyBowgun => "Heavy Bowgun",
        WeaponType.LightBowgun => "Light Bowgun",
        _ => type.ToString(),
    };
}
