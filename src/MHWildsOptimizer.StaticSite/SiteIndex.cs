using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.StaticSite;

/// <summary>The wiki data with the lookups and groupings the pages need: armor families, own-page items, links.</summary>
public sealed partial class SiteIndex
{
    public const string Name = "Wilds Builds";
    public const string Game = "Monster Hunter Wilds";

    /// <summary>
    /// The sections listed in sitemap.xml. Armor is left out at first so Google spends its attention on the skill pages; the
    /// pages exist and are linked either way, the sitemap only says which ones to look at first.
    /// </summary>
    public static readonly IReadOnlySet<string> SitemapSections = new HashSet<string> { "wiki", "skills", "items", "meals" };

    /// <summary>Items people search for by name get a page of their own; the rest are shown in full on their category's page.</summary>
    private static readonly string[] OwnPageItemNames =
    [
        "Demondrug", "Mega Demondrug", "Might Seed", "Might Pill", "Demon Powder", "Powercharm",
        "Armorskin", "Mega Armorskin", "Adamant Seed", "Adamant Pill", "Hardshell Powder", "Armorcharm",
        "Mega Potion", "Max Potion", "Ancient Potion", "Lifepowder", "Dust of Life", "Dash Juice",
        "Nourishing Extract", "Catalyst", "Godbug Essence",
    ];

    public SiteIndex(WikiData data)
    {
        Data = data;
        Skills = data.Skills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        SkillsByName = data.Skills.ToDictionary(s => s.Name);
        Sets = data.ArmorSets.ToDictionary(s => s.Name);
        Families = data.ArmorSets.GroupBy(s => FamilyOf(s.Name))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WikiArmorSet>)g.OrderBy(s => s.Rank != "low").ThenBy(s => s.Name[^1]).ToList());
        Items = data.Items ?? [];
        ItemsByName = Items.ToDictionary(i => i.Name);
        Categories = data.ItemCategories ?? [];
        OwnPageItems = OwnPageItemNames.Where(ItemsByName.ContainsKey).Select(n => ItemsByName[n]).ToList();
        _ownPage = OwnPageItems.Select(i => i.Name).ToHashSet();
        Meals = data.Meals;
    }

    private readonly HashSet<string> _ownPage;

    public WikiData Data { get; }
    public IReadOnlyList<WikiSkill> Skills { get; }
    public IReadOnlyDictionary<string, WikiSkill> SkillsByName { get; }
    public IReadOnlyDictionary<string, WikiArmorSet> Sets { get; }

    /// <summary>The low rank set and the high rank α / β / γ sets of one armor ("Rey Dau"), low rank first.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WikiArmorSet>> Families { get; }

    public IReadOnlyList<WikiItem> Items { get; }
    public IReadOnlyDictionary<string, WikiItem> ItemsByName { get; }
    public IReadOnlyList<WikiItemCategory> Categories { get; }
    public IReadOnlyList<WikiItem> OwnPageItems { get; }
    public WikiMeals? Meals { get; }

    public bool HasOwnPage(WikiItem item) => _ownPage.Contains(item.Name);

    public string ItemUrl(WikiItem item) => HasOwnPage(item) ? Urls.Item(item.Name) : Urls.Category(item.Category) + "#" + Urls.Slug(item.Name);

    public WikiItemCategory CategoryOf(WikiItem item) => Categories.First(c => c.Key == item.Category);

    /// <summary>"Attack items", but "Materials".</summary>
    public static string CategoryHeading(WikiItemCategory category) => category.Key == "materials" ? category.Title : category.Title + " items";

    /// <summary>'Rey Dau γ' -> 'Rey Dau': the low rank set and the high rank α / β / γ sets share one page.</summary>
    public static string FamilyOf(string setName) => GreekSuffix().Replace(setName, "");

    /// <summary>"Rey Dau (low rank)" for a low rank set, its own name otherwise.</summary>
    public static string SetLabel(WikiArmorSet set) => set.Rank == "low" ? set.Name + " (low rank)" : set.Name;

    /// <summary>A set's set bonuses, its pieces' own ones included (Gogmazios pieces each carry a different one).</summary>
    public static IReadOnlyList<string> SetBonuses(WikiArmorSet set) =>
        set.SetBonus.Concat(set.Pieces.SelectMany(p => p.SetBonus ?? [])).Distinct().ToList();

    /// <summary>The set bonuses and group skills of a family, once each.</summary>
    public static IReadOnlyList<string> Bonuses(IEnumerable<WikiArmorSet> sets) =>
        sets.SelectMany(s => SetBonuses(s).Concat(s.GroupSkill is { } g ? [g] : [])).Distinct().ToList();

    /// <summary>One line for lists and descriptions: the game's description, or the top level's text.</summary>
    public static string Summary(WikiSkill skill) => skill.Description ?? skill.Ranks.LastOrDefault()?.Description ?? "";

    /// <summary>Where an item comes from in a few words: "Combine · gather · support ship" (as WikiIndex.SourceSummary in the app).</summary>
    public static string SourceSummary(WikiItem item)
    {
        var s = item.Sources;
        string?[] parts =
        [
            s?.Other is not null ? "quest or cooking" : null,
            item.Recipes is { Count: > 0 } ? "combine" : null,
            s?.Gather is not null ? "gather" : null,
            s?.Retrieval is not null ? "material retrieval" : null,
            s?.Shop == true ? "item shop" : null,
            s?.SupportShip == true ? "support ship" : null,
            s?.Monsters is { } monsters ? string.Join(", ", monsters.Select(m => m.Monster).Distinct()) : null,
        ];
        var text = string.Join(" · ", parts.OfType<string>());
        return text.Length == 0 ? "" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    [GeneratedRegex(@"\s*[αβγ]$")]
    private static partial Regex GreekSuffix();
}

/// <summary>Page URLs (all ending in a slash) and icon paths.</summary>
public static partial class Urls
{
    public const string Site = "https://wildsbuilds.com";

    public static string Skill(string name) => $"/wiki/skills/{Slug(name)}/";
    public static string Family(string family) => $"/wiki/armor/{Slug(family)}/";
    public static string Set(string setName) => Family(SiteIndex.FamilyOf(setName)) + "#" + Slug(setName);
    public static string Category(string key) => $"/wiki/items/{key}/";
    public static string Item(string name) => $"/wiki/items/{Slug(name)}/";

    /// <summary>"Rey Dau's Voltage" -> "rey-daus-voltage", "Rey Dau γ" -> "rey-dau-gamma".</summary>
    public static string Slug(string name)
    {
        name = name.Replace("α", " alpha").Replace("β", " beta").Replace("γ", " gamma");
        var ascii = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
            if (c < 128) ascii.Append(c);
        var text = ascii.ToString().Replace("&", " and ").Replace("'", "").Replace(".", "").ToLowerInvariant();
        return NotSlug().Replace(text, "-").Trim('-');
    }

    public static string SkillIcon(WikiSkill skill) =>
        $"/icons/skills/{(string.IsNullOrEmpty(skill.Icon) ? skill.Kind switch { SkillKind.Set => "set", SkillKind.Group => "group", _ => "utility" } : skill.Icon)}.png";

    public static string Armor(ArmorPieceKind piece, int rarity) => $"/icons/armor/{piece.ToString().ToLowerInvariant()}-r{Math.Clamp(rarity, 1, 8)}.png";

    public static string ItemIcon(WikiItem item) => $"/icons/items/{item.Icon}-{item.IconColor}.png";

    public static string Weapon(string kind) => $"/icons/weapons/{kind}-r8.png";

    public static string Talisman(int rarity) => $"/icons/talisman/r{Math.Clamp(rarity, 0, 8)}.png";

    public static string Element(string element) => $"/icons/elements/{element}.png";

    public static string Decoration(WikiDecorationSource d) =>
        $"/icons/decorations/l{Math.Clamp(d.Slot, 1, 3)}-{(d.Kind == SkillKind.Weapon ? "weapon" : "armor")}-{DecoColor.GetValueOrDefault(d.IconColor ?? "", "white")}.png";

    /// <summary>wilds.mhdb.io decoration icon colours -> the wiki icon file names (as GameIcons.DecoColor in the app).</summary>
    private static readonly Dictionary<string, string> DecoColor = new()
    {
        ["purple"] = "purple", ["white"] = "white", ["emerald"] = "emerald", ["sky"] = "light-blue", ["pink"] = "pink", ["yellow"] = "yellow",
        ["blue"] = "blue", ["gray"] = "gray", ["red"] = "red", ["ivory"] = "tan", ["brown"] = "brown", ["lemon"] = "lemon", ["moss-green"] = "moss",
        ["rose"] = "rose", ["green"] = "green", ["ultramarine"] = "dark-blue", ["vermilion"] = "vermilion", ["dark-purple"] = "dark-purple",
        ["sage-green"] = "light-green",
    };

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlug();
}

/// <summary>Wording shared by the pages.</summary>
public static class Text
{
    public static readonly IReadOnlyDictionary<SkillKind, string> KindLabel = new Dictionary<SkillKind, string>
    {
        [SkillKind.Armor] = "Armor skill", [SkillKind.Weapon] = "Weapon skill", [SkillKind.Set] = "Set bonus", [SkillKind.Group] = "Group skill",
    };

    /// <summary>The skill list's sections, in order.</summary>
    public static readonly IReadOnlyList<(SkillKind Kind, string Key, string Title, string About)> KindSections =
    [
        (SkillKind.Armor, "armor", "Armor skills", "Skills on armor pieces, decorations and charms."),
        (SkillKind.Set, "set", "Set bonuses", "Bonuses for wearing 2 or 4 pieces of one armor set."),
        (SkillKind.Group, "group", "Group skills", "Bonuses for wearing 3 pieces that share a group skill."),
        (SkillKind.Weapon, "weapon", "Weapon skills", "Skills on weapons and weapon decorations."),
    ];

    public static readonly IReadOnlyList<string> Elements = ["fire", "water", "thunder", "ice", "dragon"];

    public static readonly IReadOnlyDictionary<string, string> WeaponLabel = new Dictionary<string, string>
    {
        ["great-sword"] = "Great Sword", ["long-sword"] = "Long Sword", ["sword-shield"] = "Sword and Shield", ["dual-blades"] = "Dual Blades",
        ["hammer"] = "Hammer", ["hunting-horn"] = "Hunting Horn", ["lance"] = "Lance", ["gunlance"] = "Gunlance", ["switch-axe"] = "Switch Axe",
        ["charge-blade"] = "Charge Blade", ["insect-glaive"] = "Insect Glaive", ["light-bowgun"] = "Light Bowgun",
        ["heavy-bowgun"] = "Heavy Bowgun", ["bow"] = "Bow",
    };

    /// <summary>A meta description: whole words, at most about <paramref name="limit"/> characters.</summary>
    public static string Cut(string text, int limit = 300)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= limit) return text;
        var cut = text[..limit];
        var space = cut.LastIndexOf(' ');
        return (space > 0 ? cut[..space] : cut).TrimEnd(',', ';', ':') + "…";
    }

    public static string JoinAnd(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    /// <summary>" for 30 min", " while carried", or nothing for instant effects.</summary>
    public static string DurationWords(string? duration) =>
        string.IsNullOrEmpty(duration) || duration == "—" ? "" : char.IsDigit(duration[0]) ? $" for {duration}" : $" {duration}";

    public static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
}
