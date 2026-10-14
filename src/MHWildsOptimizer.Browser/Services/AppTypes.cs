using System.Text.Json.Serialization;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>The sections of the app; the URL carries them in lower case.</summary>
public enum Tab { Inventory, Compare, Weapon, Attack, Targets, Conditions, Talismans, Options, Review, Results, Builds, WikiSkills, WikiArmor, WikiItems, WikiMeals }

public static class Tabs
{
    public static string Slug(this Tab tab) => tab.ToString().ToLowerInvariant();

    /// <summary>The tab of a URL slug; "limits" is the old tab of the skill limits, now on the targets tab, and "pair" the old skill pair tab, now on the weapon tab.</summary>
    public static Tab? Parse(string? slug) =>
        string.Equals(slug, "limits", StringComparison.OrdinalIgnoreCase) ? Tab.Targets
        : string.Equals(slug, "pair", StringComparison.OrdinalIgnoreCase) ? Tab.Weapon
        : Enum.TryParse<Tab>(slug, ignoreCase: true, out var t) && slug!.All(char.IsLetter) ? t : null;

    /// <summary>Tabs of the whole profile rather than of the open weapon (the wiki belongs to neither, but behaves the same).</summary>
    public static bool IsProfileTab(this Tab tab) => tab is Tab.Inventory or Tab.Compare || tab.IsWikiTab();

    /// <summary>The game reference: skills (set and group bonuses among them), armor, items, meals; the URL carries them as wiki/&lt;section&gt;.</summary>
    public static bool IsWikiTab(this Tab tab) => tab is Tab.WikiSkills or Tab.WikiArmor or Tab.WikiItems or Tab.WikiMeals;

    public static readonly IReadOnlyDictionary<string, Tab> WikiSections = new Dictionary<string, Tab>
    {
        // "bonuses": the old set & group section, kept so its links still open
        ["skills"] = Tab.WikiSkills, ["armor"] = Tab.WikiArmor, ["items"] = Tab.WikiItems, ["meals"] = Tab.WikiMeals, ["bonuses"] = Tab.WikiSkills,
    };

    public static string WikiSection(this Tab tab) => WikiSections.First(kv => kv.Value == tab).Key;
}

/// <summary>
/// Page URLs, so back / forward, reloads and bookmarks work: <c>&lt;profile&gt;/inventory</c>, <c>&lt;profile&gt;/compare</c>,
/// <c>&lt;profile&gt;/weapon/&lt;weapon&gt;/&lt;tab&gt;</c>, <c>&lt;profile&gt;/new/&lt;tab&gt;</c> (the unsaved new weapon) and
/// <c>&lt;profile&gt;/wiki/&lt;section&gt;/&lt;entry&gt;</c>.
/// </summary>
/// <param name="WeaponGiven">Whether the URL names the weapon: false = keep the open one; true with a null weapon = the new weapon.</param>
/// <param name="WikiEntry">The skill or armor set a wiki page shows.</param>
public sealed record Route(string? Profile, string? Weapon, bool WeaponGiven, Tab? Tab, string? WikiEntry = null)
{
    public static string Url(string profile, string? weapon, Tab tab, string? wikiEntry = null)
    {
        var p = Uri.EscapeDataString(profile);
        if (tab.IsWikiTab()) return $"{p}/wiki/{tab.WikiSection()}" + (wikiEntry is null ? "" : "/" + Uri.EscapeDataString(wikiEntry));
        if (tab.IsProfileTab()) return $"{p}/{tab.Slug()}";
        return weapon is null ? $"{p}/new/{tab.Slug()}" : $"{p}/weapon/{Uri.EscapeDataString(weapon)}/{tab.Slug()}";
    }

    /// <param name="path">The URL path relative to the app's base.</param>
    public static Route Parse(string path)
    {
        var parts = path.Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
        string? At(int i) => i < parts.Length ? parts[i] : null;
        var profile = At(0);
        if (profile is null) return new Route(null, null, false, null);
        var tab = (string? s) => Tabs.Parse(s) is { } t && !t.IsProfileTab() ? t : Services.Tab.Weapon;
        return At(1) switch
        {
            "inventory" => new Route(profile, null, false, Services.Tab.Inventory),
            "compare" => new Route(profile, null, false, Services.Tab.Compare),
            "wiki" => new Route(profile, null, false, Tabs.WikiSections.GetValueOrDefault(At(2) ?? "", Services.Tab.WikiSkills), At(3)),
            "new" => new Route(profile, null, true, tab(At(2))),
            "weapon" when At(2) is { } weapon => new Route(profile, weapon, true, tab(At(3))),
            _ => new Route(profile, null, false, null),
        };
    }
}

public enum RunStatus { Idle, Running, Done, Error, Cancelled }

/// <param name="FromDisk">The result is the weapon's saved last run rather than produced in this session.</param>
/// <param name="Stage">While running: where the run is, for people ("Weapon start 2 of 3 (Element focus) · armor search 1 · build 3 of 5").</param>
public sealed record RunState(RunStatus Status, IReadOnlyList<string> Log, ResultDto? Result, string? Error, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, bool FromDisk,
    string? Stage = null)
{
    public static readonly RunState Idle = new(RunStatus.Idle, [], null, null, null, null, false);

    public static RunState Saved(ResultDto result) => Idle with { Status = RunStatus.Done, Result = result, FromDisk = true };
}

/// <summary>
/// A weapon open in this browser: kept while switching weapons and tabs, so nothing is loaded twice and unsaved edits, the
/// selected build and the last run stay where they were.
/// </summary>
/// <param name="Name">Null for the unsaved new weapon.</param>
/// <param name="CompareKey">What My builds compares the selected build with: "opt:&lt;pair rank&gt;:&lt;build rank&gt;" or "mine:&lt;index&gt;"; null = the weapon's baseline (or the best optimizer build).</param>
/// <param name="ResultsPending">The weapon's saved last run still has to be loaded (a draft restored after a reload).</param>
public sealed record WeaponSession(
    string Key, string? Name, OptimizationRequest Request, IReadOnlyList<BuildInput> Builds, int BuildIndex, string? CompareKey,
    RunState Run, bool Dirty, bool ResultsPending)
{
    public static WeaponSession Create(string key, string? name, OptimizationRequest request, IReadOnlyList<BuildInput> builds, bool dirty)
    {
        var picked = builds.ToList().FindIndex(b => b.Picked);
        return new WeaponSession(key, name, request, builds, Math.Max(0, picked), null, RunState.Idle, dirty, false);
    }
}

public enum BatchStatus { Queued, Running, Done, Error }

/// <summary>A weapon in a batch of background runs (the inventory's "Run stale").</summary>
public sealed record BatchMark(BatchStatus Status, string? Message = null);

/// <summary>Unsaved edits of a weapon, kept in the browser so a reload or a profile switch does not lose them.</summary>
public sealed record Draft(OptimizationRequest Request, List<BuildInput> Builds);

/// <summary>
/// A build anywhere in the profile: one of a weapon's own builds (<paramref name="Mine"/>, by name) or build
/// <paramref name="Rank"/> of skill pair <paramref name="Pair"/> in the weapon's last run.
/// </summary>
/// <param name="Weapon">The weapon's session key: its name, or <see cref="AppState.NewWeapon"/>.</param>
public sealed record BuildRef(string Weapon, string? Mine, int Pair = 0, int Rank = 0)
{
    private const char Sep = '';

    public static BuildRef Own(string weapon, string name) => new(weapon, name);

    public static BuildRef Run(string weapon, int pair, int rank) => new(weapon, null, pair, rank);

    [JsonIgnore]
    public bool IsRun => Mine is null;

    /// <summary>A string form for select boxes and storage.</summary>
    [JsonIgnore]
    public string Key => Mine is not null ? $"mine{Sep}{Weapon}{Sep}{Mine}" : $"run{Sep}{Weapon}{Sep}{Pair}{Sep}{Rank}";

    public static BuildRef? Parse(string? key)
    {
        var parts = key?.Split(Sep);
        return parts switch
        {
            ["mine", var weapon, var name] => Own(weapon, name),
            ["run", var weapon, var pair, var rank] when int.TryParse(pair, out var p) && int.TryParse(rank, out var r) => Run(weapon, p, r),
            _ => null,
        };
    }
}

/// <summary>What the columns of a comparison are scored under.</summary>
public enum CompareBasis
{
    /// <summary>Each build under its own weapon's conditions (and its own overrides).</summary>
    Own,
    /// <summary>Every build under the baseline's conditions (builds of another weapon type keep their own).</summary>
    Baseline,
    /// <summary>Every hunt condition on.</summary>
    AllOn,
}

/// <summary>Where the hits of a comparison's columns land, on top of <see cref="CompareBasis"/>.</summary>
public enum CompareTarget
{
    /// <summary>Each column's target as its conditions give it.</summary>
    Own,
    /// <summary>Every column on the training dummy's weak point.</summary>
    Dummy,
    /// <summary>Every column on the baseline's target (a monster part at the hitzone of each column's weapon type).</summary>
    Baseline,
}

/// <summary>
/// A build of the profile looked up and scored now. <paramref name="Request"/> and <paramref name="Input"/> are what it was
/// scored with (after the comparison basis), so its damage per attack can be worked out the same way.
/// </summary>
/// <param name="RunScore">For a build of a last run: its score in that run.</param>
/// <param name="Stale">For a build of a last run: the weapon's request or the talismans changed since.</param>
public sealed record ScoredRef(
    BuildRef Ref, string Name, string Weapon, string WeaponType, OptimizationRequest Request, BuildInput Input, EvaluatedBuildDto? Evaluated,
    bool Picked, double? RunScore, DateTimeOffset? RunAt, bool Stale, bool BasisApplied)
{
    public BuildDto? Build => Evaluated?.Build;

    /// <summary>"Current · In game".</summary>
    public string Label => $"{Weapon} · {Name}";

    public IReadOnlyList<string> Errors => Evaluated?.Errors ?? ["This build cannot be scored."];
}
