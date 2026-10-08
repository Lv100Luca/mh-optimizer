using System.Text.Json.Serialization;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>An armor piece of a hand-entered build: decorations by name in slot order, null for an empty slot.</summary>
public sealed record BuildArmorInput
{
    public required string Piece { get; init; }
    /// <summary>Use the piece's transcended slots (rarity 5/6 armor).</summary>
    public bool Transcended { get; init; }
    public List<string?> Decorations { get; init; } = [];
}

/// <summary>The talisman of a hand-entered build: one of the configuration's random talismans or a craftable charm, by name.</summary>
public sealed record BuildTalismanInput
{
    public required string Name { get; init; }
    public List<string?> Decorations { get; init; } = [];
}

/// <summary>
/// A build entered by hand (e.g. the one currently worn) to score under the request's conditions and compare with the
/// optimizer's builds, as in inputs/&lt;name&gt;.builds.json:
/// { "name": "Current", "weapon_decorations": ["Attack Jewel III [3]", null, null],
///   "head": { "piece": "Bale Burgeonet α", "decorations": ["Protection Jewel [1]"] }, ...,
///   "talisman": { "name": "Secret Charm", "decorations": ["Steadfast Jewel [1]"] } }
/// The weapon is the request's; <see cref="SetBonus"/> / <see cref="GroupSkill"/> replace its rolled pair when given.
/// </summary>
public sealed record BuildInput
{
    public required string Name { get; init; }
    public string? SetBonus { get; init; }
    public string? GroupSkill { get; init; }
    public List<string?> WeaponDecorations { get; init; } = [];
    public BuildArmorInput? Head { get; init; }
    public BuildArmorInput? Chest { get; init; }
    public BuildArmorInput? Arms { get; init; }
    public BuildArmorInput? Waist { get; init; }
    public BuildArmorInput? Legs { get; init; }
    public BuildTalismanInput? Talisman { get; init; }
    /// <summary>The build chosen as the weapon's build: the one the profile's weapon comparison uses (at most one per weapon).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Picked { get; init; }

    /// <summary>
    /// Hunt conditions this build is scored under where they differ from the weapon's, by condition key (snake_case, see
    /// <see cref="ConditionToggles.AllKeys"/>): <c>{ "burst_active": true }</c>. Empty: the weapon's conditions. Lets a build be
    /// scored as it is played (or as the game's status screen shows it) without changing what the optimizer searches for.
    /// </summary>
    public Dictionary<string, bool> Conditions { get; init; } = new();
    /// <summary>The Omega Resonance phase this build is scored at; null: the weapon's.</summary>
    public ResonanceMode? Resonance { get; init; }
    /// <summary>Where this build's hits land; null: the weapon's target.</summary>
    public Target? Target { get; init; }

    [JsonIgnore]
    public bool HasOwnConditions => Conditions.Count > 0 || Resonance is not null || Target is not null;

    /// <summary>The conditions this build is scored under: <paramref name="weapon"/>'s with the build's overrides applied (the same object when it has none).</summary>
    public Conditions ConditionsFor(Conditions weapon)
    {
        if (!HasOwnConditions) return weapon;
        var c = ConditionToggles.With(weapon, Conditions);
        if (Resonance is { } r) c = c with { Resonance = r };
        if (Target is { } t) c = c with { Target = t };
        return c;
    }

    /// <summary>The build scored under the weapon's conditions (its overrides dropped).</summary>
    public BuildInput WithoutOwnConditions() => HasOwnConditions ? this with { Conditions = new(), Resonance = null, Target = null } : this;

    /// <summary>Condition keys that are not on/off conditions (they are ignored when scoring).</summary>
    public IReadOnlyList<string> ConditionErrors() => Conditions.Keys.Where(k => !ConditionToggles.AllKeys.Contains(k)).ToList();

    public BuildArmorInput? Armor(ArmorPieceKind kind) => kind switch
    {
        ArmorPieceKind.Head => Head,
        ArmorPieceKind.Chest => Chest,
        ArmorPieceKind.Arms => Arms,
        ArmorPieceKind.Waist => Waist,
        ArmorPieceKind.Legs => Legs,
        _ => null,
    };

    /// <summary>The input that reproduces a loadout, e.g. an optimizer build to edit by hand.</summary>
    public static BuildInput FromLoadout(string name, Loadout loadout) => new()
    {
        Name = name,
        SetBonus = loadout.Weapon.Stats.SetBonus,
        GroupSkill = loadout.Weapon.Stats.GroupSkill,
        WeaponDecorations = Names(loadout.Weapon.Decos),
        Head = ArmorOf(loadout.Head),
        Chest = ArmorOf(loadout.Chest),
        Arms = ArmorOf(loadout.Arms),
        Waist = ArmorOf(loadout.Waist),
        Legs = ArmorOf(loadout.Legs),
        Talisman = loadout.Talisman is { } t ? new BuildTalismanInput { Name = t.Talisman.Name, Decorations = Names(t.Decos) } : null,
    };

    private static BuildArmorInput? ArmorOf(EquippedArmor? a) =>
        a is null ? null : new BuildArmorInput { Piece = a.Piece.Name, Transcended = a.Transcended, Decorations = Names(a.Decos) };

    private static List<string?> Names(IEnumerable<Decoration?> decos) => decos.Select(d => d?.Name).ToList();
}

/// <summary>A hand-entered build as a loadout. Unknown names are reported and left out; slot mismatches are reported but kept.</summary>
public sealed record BuildConversion(Loadout Loadout, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public static class BuildInputLoader
{
    public static IReadOnlyList<BuildInput> Read(string path) => GameDataLoader.ReadJson<List<BuildInput>>(path);

    /// <param name="weapon">The request's resolved weapon; the build's rolled pair replaces the weapon's when given.</param>
    /// <param name="talismans">Talismans the build's talisman may name (the configuration's random talismans); craftable charms are found in the dataset.</param>
    public static BuildConversion Convert(BuildInput input, GogmaWeaponStats weapon, IReadOnlyList<Talisman> talismans, GameData data)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var stats = weapon with { SetBonus = input.SetBonus ?? weapon.SetBonus, GroupSkill = input.GroupSkill ?? weapon.GroupSkill };

        EquippedTalisman? talisman = null;
        if (input.Talisman is { } t)
        {
            var found = talismans.FirstOrDefault(x => x.Name == t.Name)
                        ?? (data.CharmsByName.TryGetValue(t.Name, out var charm) ? Talisman.FromCharm(charm) : null);
            if (found is null) errors.Add($"Talisman: '{t.Name}' is neither one of your talismans nor a craftable charm.");
            else talisman = new EquippedTalisman(found, Decorations($"Talisman '{t.Name}'", t.Decorations));
        }

        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(stats, Decorations("Weapon", input.WeaponDecorations)),
            Head = Armor(ArmorPieceKind.Head),
            Chest = Armor(ArmorPieceKind.Chest),
            Arms = Armor(ArmorPieceKind.Arms),
            Waist = Armor(ArmorPieceKind.Waist),
            Legs = Armor(ArmorPieceKind.Legs),
            Talisman = talisman,
        };
        errors.AddRange(loadout.Validate(data));
        return new BuildConversion(loadout, errors, warnings);

        EquippedArmor? Armor(ArmorPieceKind kind)
        {
            if (input.Armor(kind) is not { } a) return null;
            if (!data.ArmorByName.TryGetValue(a.Piece, out var piece))
            {
                errors.Add($"{kind}: unknown armor piece '{a.Piece}'.");
                return null;
            }
            var transcended = a.Transcended && piece.SlotsTranscended.Count > 0 && !piece.SlotsTranscended.SequenceEqual(piece.Slots);
            if (a.Transcended && !transcended) warnings.Add($"{kind}: '{piece.Name}' has no transcended slots; using its normal slots.");
            return new EquippedArmor(piece, transcended, Decorations(kind.ToString(), a.Decorations));
        }

        List<Decoration?> Decorations(string where, IReadOnlyList<string?> names)
        {
            var decos = new List<Decoration?>();
            foreach (var name in names)
            {
                if (name is null) { decos.Add(null); continue; }
                if (data.DecorationsByName.TryGetValue(name, out var d)) decos.Add(d);
                else
                {
                    errors.Add($"{where}: unknown decoration '{name}'.");
                    decos.Add(null);
                }
            }
            return decos;
        }
    }
}
