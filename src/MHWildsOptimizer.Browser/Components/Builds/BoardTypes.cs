using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Browser.Services;

namespace MHWildsOptimizer.Browser.Components.Builds;

/// <summary>What is selected on the loadout board: a tile, or one decoration slot of a tile (<paramref name="DecoIndex"/>).</summary>
public sealed record BoardSelection(BuildSlot Slot, int? DecoIndex = null);

public enum PaletteMode { Pieces, Jewels, Talismans }

/// <summary>Slot arithmetic the board, the sheet and the palette share.</summary>
public static class BoardSlots
{
    public static IReadOnlyList<SlotSpec> ArmorSlots(AppState app, BuildArmorInput a)
    {
        if (app.Index.ArmorByName.GetValueOrDefault(a.Piece) is not { } p) return [];
        var transcended = a.Transcended && CanTranscend(p);
        return (transcended ? p.Piece.SlotsTranscended : p.Piece.Slots).Select(l => new SlotSpec(l, SkillKind.Armor)).ToList();
    }

    public static IReadOnlyList<SlotSpec> WeaponSlots(AppState app) =>
        (app.Resolved?.Weapon?.Slots ?? [3, 3, 3]).Select(l => new SlotSpec(l, SkillKind.Weapon)).ToList();

    public static IReadOnlyList<SlotSpec> TalismanSlots(AppState app, BuildTalismanInput t)
    {
        var own = app.Talismans.FirstOrDefault(x => x.Name == t.Name);
        var slots = own?.Slots ?? t.Slots ?? [];
        return slots.Select(BuildHelpers.ParseSlot).ToList();
    }

    public static IReadOnlyList<SlotSpec> SlotsOf(AppState app, BuildInput b, BuildSlot slot) => slot switch
    {
        BuildSlot.Weapon => WeaponSlots(app),
        BuildSlot.Talisman => b.Talisman is { } t ? TalismanSlots(app, t) : [],
        _ => b.Armor(BuildDiff.ArmorSlots[slot]) is { } a ? ArmorSlots(app, a) : [],
    };

    public static IReadOnlyList<string?> DecorationsOf(BuildInput b, BuildSlot slot) => slot switch
    {
        BuildSlot.Weapon => b.WeaponDecorations,
        BuildSlot.Talisman => b.Talisman?.Decorations ?? [],
        _ => b.Armor(BuildDiff.ArmorSlots[slot])?.Decorations ?? [],
    };

    public static BuildInput WithDecoration(this BuildInput b, BuildSlot slot, int index, string? name, IReadOnlyList<SlotSpec> slots)
    {
        var current = DecorationsOf(b, slot);
        var next = slots.Select((_, j) => j == index ? name : j < current.Count ? current[j] : null).ToList();
        return slot switch
        {
            BuildSlot.Weapon => b with { WeaponDecorations = next },
            BuildSlot.Talisman => b.Talisman is { } t ? b with { Talisman = t with { Decorations = next } } : b,
            _ => b.Armor(BuildDiff.ArmorSlots[slot]) is { } a ? b.WithArmor(BuildDiff.ArmorSlots[slot], a with { Decorations = next }) : b,
        };
    }

    public static bool CanTranscend(ArmorEntry p) =>
        p.Piece.SlotsTranscended.Count > 0 && !p.Piece.SlotsTranscended.SequenceEqual(p.Piece.Slots);

    /// <summary>Skill levels the build's jewels alone give, by skill.</summary>
    public static Dictionary<string, int> JewelLevels(AppState app, BuildInput b)
    {
        var levels = new Dictionary<string, int>();
        foreach (var slot in BuildDiff.AllSlots)
            foreach (var name in DecorationsOf(b, slot))
                if (name is not null && app.Index.DecorationsByName.GetValueOrDefault(name) is { } d)
                    foreach (var g in d.Skills) levels[g.Skill] = levels.GetValueOrDefault(g.Skill) + g.Level;
        return levels;
    }

    /// <summary>Empty slots of the build by kind and level, most common first.</summary>
    public static List<(SlotSpec Slot, int Count)> FreeSlots(AppState app, BuildInput b)
    {
        var free = new Dictionary<SlotSpec, int>();
        foreach (var slot in BuildDiff.AllSlots)
        {
            var specs = SlotsOf(app, b, slot);
            var decos = DecorationsOf(b, slot);
            for (var i = 0; i < specs.Count; i++)
                if (i >= decos.Count || decos[i] is null) free[specs[i]] = free.GetValueOrDefault(specs[i]) + 1;
        }
        return free.OrderByDescending(kv => kv.Key.Level).ThenBy(kv => kv.Key.Kind).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public static string Label(BuildSlot slot) => slot switch
    {
        BuildSlot.Weapon => "Weapon",
        BuildSlot.Talisman => "Talisman",
        _ => BuildHelpers.PieceLabel(BuildDiff.ArmorSlots[slot]),
    };

    /// <summary>How many wanted levels a set of skill grants adds on top of what the build has.</summary>
    public static int Fit(IEnumerable<SkillGrant> grants, IReadOnlyDictionary<string, int> want, IReadOnlyDictionary<string, int> have)
    {
        var fit = 0;
        foreach (var g in grants)
            if (want.TryGetValue(g.Skill, out var w))
                fit += Math.Max(0, Math.Min(g.Level, w - have.GetValueOrDefault(g.Skill)));
        return fit;
    }
}
