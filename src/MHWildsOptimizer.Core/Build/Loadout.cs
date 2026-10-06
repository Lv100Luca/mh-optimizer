using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Build;

/// <summary>An armor piece as worn: optionally transcended (better slots on rarity 5/6) and with decorations slotted in order.</summary>
public sealed record EquippedArmor(ArmorPiece Piece, bool Transcended = false, IReadOnlyList<Decoration?>? Decorations = null)
{
    public IReadOnlyList<int> EffectiveSlots => Transcended ? Piece.SlotsTranscended : Piece.Slots;
    public IReadOnlyList<Decoration?> Decos => Decorations ?? [];
}

/// <summary>The weapon as equipped: fixed stats (never optimized) plus weapon decorations.</summary>
public sealed record EquippedWeapon(GogmaWeaponStats Stats, IReadOnlyList<Decoration?>? Decorations = null)
{
    public EquippedWeapon(GogmaWeaponSpec spec, GameData data, IReadOnlyList<Decoration?>? decorations = null)
        : this(spec.Resolve(data), decorations) { }

    public IReadOnlyList<Decoration?> Decos => Decorations ?? [];
}

/// <summary>A talisman as equipped, with decorations in its own slots (random talismans can have weapon- or armor-kind slots).</summary>
public sealed record EquippedTalisman(Talisman Talisman, IReadOnlyList<Decoration?>? Decorations = null)
{
    public IReadOnlyList<Decoration?> Decos => Decorations ?? [];

    public static implicit operator EquippedTalisman(Talisman talisman) => new(talisman);
    public static implicit operator EquippedTalisman(Charm charm) => new(Talisman.FromCharm(charm));
}

public sealed class Loadout
{
    public required EquippedWeapon Weapon { get; init; }
    public EquippedArmor? Head { get; init; }
    public EquippedArmor? Chest { get; init; }
    public EquippedArmor? Arms { get; init; }
    public EquippedArmor? Waist { get; init; }
    public EquippedArmor? Legs { get; init; }
    public EquippedTalisman? Talisman { get; init; }

    public IEnumerable<EquippedArmor> ArmorPieces =>
        new[] { Head, Chest, Arms, Waist, Legs }.Where(a => a is not null)!;

    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();

        errors.AddRange(CheckDecorations("Weapon", Weapon.Stats.Slots.Select(l => new TalismanSlot(l, SkillKind.Weapon)).ToList(), Weapon.Decos));
        if (Weapon.Stats.SetBonus is { } sb && (!data.SkillsByName.TryGetValue(sb, out var s1) || s1.Kind != SkillKind.Set))
            errors.Add($"Weapon: '{sb}' is not a set-bonus skill.");
        if (Weapon.Stats.GroupSkill is { } gs && (!data.SkillsByName.TryGetValue(gs, out var s2) || s2.Kind != SkillKind.Group))
            errors.Add($"Weapon: '{gs}' is not a group skill.");

        Check(Head, ArmorPieceKind.Head);
        Check(Chest, ArmorPieceKind.Chest);
        Check(Arms, ArmorPieceKind.Arms);
        Check(Waist, ArmorPieceKind.Waist);
        Check(Legs, ArmorPieceKind.Legs);

        if (Talisman is { } t)
            errors.AddRange(CheckDecorations($"Talisman '{t.Talisman.Name}'", t.Talisman.Slots, t.Decos));

        return errors;

        void Check(EquippedArmor? equipped, ArmorPieceKind expected)
        {
            if (equipped is null) return;
            if (equipped.Piece.Piece != expected)
                errors.Add($"{expected}: '{equipped.Piece.Name}' is a {equipped.Piece.Piece} piece.");
            errors.AddRange(CheckDecorations(expected.ToString(), equipped.EffectiveSlots.Select(l => new TalismanSlot(l, SkillKind.Armor)).ToList(), equipped.Decos));
        }
    }

    private static IEnumerable<string> CheckDecorations(string where, IReadOnlyList<TalismanSlot> slots, IReadOnlyList<Decoration?> decos)
    {
        if (decos.Count > slots.Count)
            yield return $"{where}: {decos.Count} decorations but only {slots.Count} slots.";

        for (var i = 0; i < Math.Min(decos.Count, slots.Count); i++)
        {
            var d = decos[i];
            if (d is null) continue;
            if (d.Kind != slots[i].Kind)
                yield return $"{where}: '{d.Name}' is a {d.Kind} decoration but slot {i + 1} is a {slots[i].Kind} slot.";
            if (d.Slot > slots[i].Level)
                yield return $"{where}: '{d.Name}' needs a level {d.Slot} slot but slot {i + 1} is level {slots[i].Level}.";
        }
    }
}
