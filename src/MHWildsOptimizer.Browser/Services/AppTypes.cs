using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>The sections of the app; the URL carries them in lower case.</summary>
public enum Tab { Inventory, Weapon, Pair, Targets, Limits, Conditions, Talismans, Options, Review, Results, Builds }

public static class Tabs
{
    public static string Slug(this Tab tab) => tab.ToString().ToLowerInvariant();

    public static Tab? Parse(string? slug) => Enum.TryParse<Tab>(slug, ignoreCase: true, out var t) && slug!.All(char.IsLetter) ? t : null;
}

/// <summary>
/// Page URLs, so back / forward, reloads and bookmarks work: <c>&lt;profile&gt;/inventory</c>,
/// <c>&lt;profile&gt;/weapon/&lt;weapon&gt;/&lt;tab&gt;</c> and <c>&lt;profile&gt;/new/&lt;tab&gt;</c> (the unsaved new weapon).
/// </summary>
/// <param name="WeaponGiven">Whether the URL names the weapon: false = keep the open one; true with a null weapon = the new weapon.</param>
public sealed record Route(string? Profile, string? Weapon, bool WeaponGiven, Tab? Tab)
{
    public static string Url(string profile, string? weapon, Tab tab)
    {
        var p = Uri.EscapeDataString(profile);
        if (tab == Services.Tab.Inventory) return $"{p}/inventory";
        return weapon is null ? $"{p}/new/{tab.Slug()}" : $"{p}/weapon/{Uri.EscapeDataString(weapon)}/{tab.Slug()}";
    }

    /// <param name="path">The URL path relative to the app's base.</param>
    public static Route Parse(string path)
    {
        var parts = path.Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
        string? At(int i) => i < parts.Length ? parts[i] : null;
        var profile = At(0);
        if (profile is null) return new Route(null, null, false, null);
        var tab = (string? s) => Tabs.Parse(s) is { } t && t != Services.Tab.Inventory ? t : Services.Tab.Weapon;
        return At(1) switch
        {
            "inventory" => new Route(profile, null, false, Services.Tab.Inventory),
            "new" => new Route(profile, null, true, tab(At(2))),
            "weapon" when At(2) is { } weapon => new Route(profile, weapon, true, tab(At(3))),
            _ => new Route(profile, null, false, null),
        };
    }
}

public enum RunStatus { Idle, Running, Done, Error, Cancelled }

/// <param name="FromDisk">The result is the weapon's saved last run rather than produced in this session.</param>
public sealed record RunState(RunStatus Status, IReadOnlyList<string> Log, ResultDto? Result, string? Error, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, bool FromDisk)
{
    public static readonly RunState Idle = new(RunStatus.Idle, [], null, null, null, null, false);

    public static RunState Saved(ResultDto result) => Idle with { Status = RunStatus.Done, Result = result, FromDisk = true };
}

/// <summary>
/// A weapon open in this browser: kept while switching weapons and tabs, so nothing is loaded twice and unsaved edits, the
/// selected build and the last run stay where they were.
/// </summary>
/// <param name="Name">Null for the unsaved new weapon.</param>
/// <param name="CompareKey">What My builds compares the selected build with: "opt:&lt;pair rank&gt;:&lt;build rank&gt;" or "mine:&lt;index&gt;"; null = the best optimizer build.</param>
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
