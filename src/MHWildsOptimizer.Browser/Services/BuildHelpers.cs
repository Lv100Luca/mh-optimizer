using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>A slot as the build editor sees it: level plus the decoration kind it takes.</summary>
public sealed record SlotSpec(int Level, SkillKind Kind);

/// <summary>Helpers for hand-entered builds (<see cref="BuildInput"/>) and how they relate to the optimizer's builds (builds.ts).</summary>
public static class BuildHelpers
{
    public static readonly ArmorPieceKind[] ArmorKinds = [ArmorPieceKind.Head, ArmorPieceKind.Chest, ArmorPieceKind.Arms, ArmorPieceKind.Waist, ArmorPieceKind.Legs];

    public static string PieceLabel(ArmorPieceKind kind) => kind.ToString();

    public static BuildInput Empty(string name) => new() { Name = name };

    public static BuildInput WithArmor(this BuildInput b, ArmorPieceKind kind, BuildArmorInput? armor) => kind switch
    {
        ArmorPieceKind.Head => b with { Head = armor },
        ArmorPieceKind.Chest => b with { Chest = armor },
        ArmorPieceKind.Arms => b with { Arms = armor },
        ArmorPieceKind.Waist => b with { Waist = armor },
        _ => b with { Legs = armor },
    };

    /// <summary>The hand-entered form of an optimizer build, so it can be edited and compared.</summary>
    public static BuildInput FromBuild(BuildDto b, string name)
    {
        static List<string?> Names(IEnumerable<DecoDto?> decos) => decos.Select(d => d?.Name).ToList();
        BuildArmorInput? Armor(ArmorPieceKind kind) =>
            b.Armor.FirstOrDefault(x => x.Kind == kind) is { } a ? new BuildArmorInput { Piece = a.Name, Transcended = a.Transcended, Decorations = Names(a.Decorations) } : null;
        return new BuildInput
        {
            Name = name,
            SetBonus = b.Weapon.SetBonus,
            GroupSkill = b.Weapon.GroupSkill,
            WeaponDecorations = Names(b.Weapon.Decorations),
            Head = Armor(ArmorPieceKind.Head),
            Chest = Armor(ArmorPieceKind.Chest),
            Arms = Armor(ArmorPieceKind.Arms),
            Waist = Armor(ArmorPieceKind.Waist),
            Legs = Armor(ArmorPieceKind.Legs),
            Talisman = b.Talisman is { } t
                ? new BuildTalismanInput
                {
                    Name = t.Name, Decorations = Names(t.Decorations),
                    // a possible roll is in nobody's talisman list, so it carries its skills and slots
                    Rarity = t.Source == TalismanSource.Possible ? t.Rarity : null,
                    Skills = t.Source == TalismanSource.Possible ? t.Skills.ToDictionary(s => s.Skill, s => s.Level) : null,
                    Slots = t.Source == TalismanSource.Possible ? [.. t.Slots] : null,
                }
                : null,
        };
    }

    /// <summary>
    /// <paramref name="b"/> with one slot taken from <paramref name="source"/>: the weapon's roll and decorations, an armor piece
    /// with its decorations, or the talisman with its decorations.
    /// </summary>
    public static BuildInput WithSlotFrom(this BuildInput b, BuildInput source, BuildSlot slot) => slot switch
    {
        BuildSlot.Weapon => b with { SetBonus = source.SetBonus, GroupSkill = source.GroupSkill, WeaponDecorations = [.. source.WeaponDecorations] },
        BuildSlot.Talisman => b with { Talisman = source.Talisman is { } t ? t with { Decorations = [.. t.Decorations] } : null },
        _ => b.WithArmor(BuildDiff.ArmorSlots[slot], source.Armor(BuildDiff.ArmorSlots[slot]) is { } a ? a with { Decorations = [.. a.Decorations] } : null),
    };

    /// <summary>"weapon1" / "armor2" (talisman slot notation) -> slot spec.</summary>
    public static SlotSpec ParseSlot(string text) =>
        new(int.TryParse(new string(text.Where(char.IsDigit).ToArray()), out var n) && n > 0 ? n : 1, text.StartsWith('w') ? SkillKind.Weapon : SkillKind.Armor);

    public static bool Fits(DecorationDto? deco, SlotSpec slot) => deco is not null && deco.Kind == slot.Kind && deco.Slot <= slot.Level;

    /// <summary>Keeps each decoration that still fits its slot (by position) after the slots changed; one entry per slot.</summary>
    public static List<string?> FitDecorations(IReadOnlyList<string?> names, IReadOnlyList<SlotSpec> slots, IReadOnlyDictionary<string, DecorationDto> decorations) =>
        slots.Select((slot, i) =>
        {
            var name = i < names.Count ? names[i] : null;
            return name is not null && Fits(decorations.GetValueOrDefault(name), slot) ? name : null;
        }).ToList();

    /// <summary>A name for a new build that is not taken yet: "My build", "My build 2", ...</summary>
    public static string UniqueName(string baseName, IEnumerable<string> taken)
    {
        var set = taken.ToHashSet();
        if (!set.Contains(baseName)) return baseName;
        for (var i = 2; ; i++)
            if (!set.Contains($"{baseName} {i}")) return $"{baseName} {i}";
    }
}
