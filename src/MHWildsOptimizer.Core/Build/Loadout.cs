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

public sealed record EquippedWeapon(GogmaWeaponSpec Spec, IReadOnlyList<Decoration?>? Decorations = null)
{
    public IReadOnlyList<Decoration?> Decos => Decorations ?? [];
}

public sealed class Loadout
{
    public required EquippedWeapon Weapon { get; init; }
    public EquippedArmor? Head { get; init; }
    public EquippedArmor? Chest { get; init; }
    public EquippedArmor? Arms { get; init; }
    public EquippedArmor? Waist { get; init; }
    public EquippedArmor? Legs { get; init; }
    public Charm? Charm { get; init; }

    public IEnumerable<EquippedArmor> ArmorPieces =>
        new[] { Head, Chest, Arms, Waist, Legs }.Where(a => a is not null)!;

    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();
        errors.AddRange(Weapon.Spec.Validate(data).Select(e => "Weapon: " + e));

        var weaponSlots = data.GogmaWeapons.TryGetValue(Weapon.Spec.Type, out var byFocus) && byFocus.TryGetValue(Weapon.Spec.Focus, out var v)
            ? v.Slots
            : [];
        errors.AddRange(CheckDecorations("Weapon", weaponSlots, Weapon.Decos, SkillKind.Weapon));

        Check(Head, ArmorPieceKind.Head);
        Check(Chest, ArmorPieceKind.Chest);
        Check(Arms, ArmorPieceKind.Arms);
        Check(Waist, ArmorPieceKind.Waist);
        Check(Legs, ArmorPieceKind.Legs);

        if (Charm is { IsMaxRank: false })
            errors.Add($"Charm '{Charm.Name}' is not the max rank of its line.");

        return errors;

        void Check(EquippedArmor? equipped, ArmorPieceKind expected)
        {
            if (equipped is null) return;
            if (equipped.Piece.Piece != expected)
                errors.Add($"{expected}: '{equipped.Piece.Name}' is a {equipped.Piece.Piece} piece.");
            errors.AddRange(CheckDecorations(expected.ToString(), equipped.EffectiveSlots, equipped.Decos, SkillKind.Armor));
        }
    }

    private static IEnumerable<string> CheckDecorations(string where, IReadOnlyList<int> slots, IReadOnlyList<Decoration?> decos, SkillKind kind)
    {
        if (decos.Count > slots.Count)
            yield return $"{where}: {decos.Count} decorations but only {slots.Count} slots.";

        for (var i = 0; i < Math.Min(decos.Count, slots.Count); i++)
        {
            var d = decos[i];
            if (d is null) continue;
            if (d.Kind != kind)
                yield return $"{where}: '{d.Name}' is a {d.Kind} decoration.";
            if (d.Slot > slots[i])
                yield return $"{where}: '{d.Name}' needs a level {d.Slot} slot but slot {i + 1} is level {slots[i]}.";
        }
    }
}
