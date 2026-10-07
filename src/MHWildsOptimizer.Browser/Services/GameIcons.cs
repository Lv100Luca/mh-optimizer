using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>Paths into wwwroot/icons (the wiki icons the web client ships), relative to the app's base.</summary>
public static class GameIcons
{
    /// <summary>wilds.mhdb.io decoration icon color -> wiki icon color file name.</summary>
    private static readonly Dictionary<string, string> DecoColor = new()
    {
        ["purple"] = "purple", ["white"] = "white", ["emerald"] = "emerald", ["sky"] = "light-blue", ["pink"] = "pink", ["yellow"] = "yellow",
        ["blue"] = "blue", ["gray"] = "gray", ["red"] = "red", ["ivory"] = "tan", ["brown"] = "brown", ["lemon"] = "lemon", ["moss-green"] = "moss",
        ["rose"] = "rose", ["green"] = "green", ["ultramarine"] = "dark-blue", ["vermilion"] = "vermilion", ["dark-purple"] = "dark-purple",
        ["sage-green"] = "light-green",
    };

    public static string Skill(string? icon, SkillKind? kind = null) =>
        $"icons/skills/{icon ?? (kind == SkillKind.Set ? "set" : kind == SkillKind.Group ? "group" : "utility")}.png";

    /// <summary>The generic icon for a kind name used by the nav ("set", "offense", "utility", "attack", "item").</summary>
    public static string SkillGeneric(string name) => $"icons/skills/{name}.png";

    public static string Decoration(int slot, SkillKind kind, string? color) =>
        $"icons/decorations/l{Math.Clamp(slot, 1, 3)}-{(kind == SkillKind.Weapon ? "weapon" : "armor")}-{DecoColor.GetValueOrDefault(color ?? "", "white")}.png";

    public static string Armor(ArmorPieceKind kind, int rarity) => $"icons/armor/{kind.ToString().ToLowerInvariant()}-r{Math.Clamp(rarity, 1, 8)}.png";

    public static string ArmorBase(ArmorPieceKind kind) => $"icons/armor/{kind.ToString().ToLowerInvariant()}-base.webp";

    public static string Weapon(string kind) => $"icons/weapons/{kind}-r8.png";

    public static string WeaponBase(string kind) => $"icons/weapons/{kind}-base.webp";

    public static string Talisman(int rarity) => $"icons/talisman/r{Math.Clamp(rarity, 0, 8)}.png";

    public static string? Element(Element element) => element == Core.Domain.Element.None ? null : $"icons/elements/{element.ToString().ToLowerInvariant()}.png";

    public const string Affinity = "icons/ui/affinity-up.png";
    public const string Attack = "icons/ui/attack-up.png";
    public const string Defense = "icons/ui/defense-up.png";
    public const string Sharpness = "icons/ui/sharpness.png";
    public const string DecorationGeneric = "icons/ui/decoration.png";

    public static readonly IReadOnlyDictionary<SharpnessColor, string> SharpnessCss = new Dictionary<SharpnessColor, string>
    {
        [SharpnessColor.Red] = "#d9342b", [SharpnessColor.Orange] = "#e8862a", [SharpnessColor.Yellow] = "#e6d22d", [SharpnessColor.Green] = "#55c43a",
        [SharpnessColor.Blue] = "#3c8df0", [SharpnessColor.White] = "#f2f2f2", [SharpnessColor.Purple] = "#b56bf5",
    };

    public static readonly IReadOnlyDictionary<Element, string> ElementCss = new Dictionary<Element, string>
    {
        [Core.Domain.Element.None] = "#9aa0a6", [Core.Domain.Element.Fire] = "#f26b3a", [Core.Domain.Element.Water] = "#4aa8f0",
        [Core.Domain.Element.Thunder] = "#f2d23c", [Core.Domain.Element.Ice] = "#8fd8f5", [Core.Domain.Element.Dragon] = "#9b6df0",
    };

    public static readonly IReadOnlyDictionary<int, string> RarityCss = new Dictionary<int, string>
    {
        [1] = "#c8c8c8", [2] = "#c8c8c8", [3] = "#cde0a8", [4] = "#8dd36a", [5] = "#5fbf7a", [6] = "#4fa3e6", [7] = "#b07be8", [8] = "#ff8c42",
    };
}
