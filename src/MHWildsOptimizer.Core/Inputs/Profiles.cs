using System.Reflection;
using System.Text.Json;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>
/// Account-wide settings of a profile (profile.json): the hunt conditions each weapon type starts from. Talismans are
/// account-wide too and live next to it in talismans.json.
/// </summary>
public sealed record ProfileSettings
{
    /// <summary>Condition preset per weapon type (API kind, e.g. "great-sword"); types without one use <see cref="Conditions.Default"/>.</summary>
    public Dictionary<string, Conditions> ConditionPresets { get; init; } = new();

    public Conditions PresetFor(string weaponKind) => ConditionPresets.TryGetValue(weaponKind, out var c) ? c : Conditions.Default;
}

/// <summary>
/// A weapon's conditions follow its type's preset value by value: a value equal to the preset follows it when the preset
/// changes, a value that differs is the weapon's own override and stays.
/// </summary>
public static class ConditionPresets
{
    private static readonly PropertyInfo[] Properties = typeof(Conditions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite)
        .ToArray();

    /// <summary>Moves <paramref name="current"/> from <paramref name="oldPreset"/> to <paramref name="newPreset"/>, keeping its overrides.</summary>
    public static Conditions Follow(Conditions current, Conditions oldPreset, Conditions newPreset)
    {
        var result = current with { };
        foreach (var p in Properties)
            if (ValueEquals(p.GetValue(current), p.GetValue(oldPreset)))
                p.SetValue(result, Copy(p.GetValue(newPreset)));
        return result;
    }

    /// <summary>The properties (C# names) where <paramref name="current"/> overrides <paramref name="preset"/>.</summary>
    public static IReadOnlyList<string> Overrides(Conditions current, Conditions preset) =>
        Properties.Where(p => !ValueEquals(p.GetValue(current), p.GetValue(preset))).Select(p => p.Name).ToList();

    public static bool Same(Conditions a, Conditions b) => Overrides(a, b).Count == 0;

    private static bool ValueEquals(object? a, object? b) => (a, b) switch
    {
        (Dictionary<string, int> x, Dictionary<string, int> y) => x.Count == y.Count && x.All(kv => y.TryGetValue(kv.Key, out var v) && v == kv.Value),
        _ => Equals(a, b),
    };

    private static object? Copy(object? value) => value is Dictionary<string, int> d ? new Dictionary<string, int>(d) : value;
}

/// <summary>
/// The profile layout under the inputs directory:
/// profiles/&lt;profile&gt;/profile.json, talismans.json and weapons/&lt;weapon&gt;.json (+ .builds.json, .results.json/.txt).
/// A weapon's request points at the shared pool with talismans.file = "../talismans.json", so the CLI reads it unchanged.
/// </summary>
public static class ProfileFiles
{
    public const string ProfilesDirectoryName = "profiles";
    public const string SettingsFileName = "profile.json";
    public const string TalismansFileName = "talismans.json";
    public const string WeaponsDirectoryName = "weapons";
    /// <summary>talismans.file of every weapon request in a profile.</summary>
    public const string SharedTalismanReference = "../" + TalismansFileName;

    public static string ProfilesDirectory(string inputsDirectory) => Path.Combine(inputsDirectory, ProfilesDirectoryName);
    public static string ProfileDirectory(string inputsDirectory, string profile) => Path.Combine(ProfilesDirectory(inputsDirectory), profile);
    public static string WeaponsDirectory(string profileDirectory) => Path.Combine(profileDirectory, WeaponsDirectoryName);

    public static ProfileSettings ReadSettings(string profileDirectory)
    {
        var path = Path.Combine(profileDirectory, SettingsFileName);
        return File.Exists(path) ? GameDataLoader.ReadJson<ProfileSettings>(path) : new ProfileSettings();
    }

    public static void SaveSettings(ProfileSettings settings, string profileDirectory)
    {
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(Path.Combine(profileDirectory, SettingsFileName), JsonSerializer.Serialize(settings, RequestFiles.WriteOptions));
    }

    /// <summary>The profile directory a weapon request belongs to (…/profiles/&lt;p&gt;/weapons/&lt;w&gt;.json), or null for a stand-alone request.</summary>
    public static string? ProfileDirectoryOf(string requestPath)
    {
        var weapons = Path.GetDirectoryName(Path.GetFullPath(requestPath));
        if (weapons is null || !string.Equals(Path.GetFileName(weapons), WeaponsDirectoryName, StringComparison.OrdinalIgnoreCase)) return null;
        var profile = Path.GetDirectoryName(weapons);
        return profile is not null && File.Exists(Path.Combine(profile, SettingsFileName)) ? profile : null;
    }

    /// <summary>Weapon request files of every profile, for pickers that list all configurations.</summary>
    public static IReadOnlyList<string> AllWeaponRequests(string inputsDirectory)
    {
        var root = ProfilesDirectory(inputsDirectory);
        if (!Directory.Exists(root)) return [];
        return Directory.GetDirectories(root).OrderBy(d => d)
            .SelectMany(d => RequestFiles.ListRequests(WeaponsDirectory(d)))
            .ToList();
    }
}
