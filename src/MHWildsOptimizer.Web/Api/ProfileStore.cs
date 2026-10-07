using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Web.Api;

public sealed record ProfileSummaryDto(string Name, int Weapons, int Talismans, DateTimeOffset Modified);

/// <summary>Account-wide data: the talisman pool every weapon draws from and the condition preset of each weapon type.</summary>
public sealed record ProfileDto(string Name, IReadOnlyList<TalismanInput> Talismans, IReadOnlyDictionary<string, Conditions> ConditionPresets);

public sealed record WeaponSummaryDto(string Name, string Type, DateTimeOffset Modified, bool HasResults, string? Summary);

public sealed record WeaponDto(string Profile, string Name, OptimizationRequest Request, IReadOnlyList<BuildInput> Builds, bool HasResults);

/// <summary>A weapon as the client saves it: the request plus its hand-entered builds (the talismans are the profile's).</summary>
public sealed record WeaponPayload(OptimizationRequest Request, List<BuildInput>? Builds = null);

/// <summary>
/// What the client sends to validate, run or evaluate: the request plus the profile's random talismans and the weapon's
/// hand-entered builds (possibly unsaved edits).
/// </summary>
public sealed record ConfigPayload(OptimizationRequest Request, List<TalismanInput>? Talismans, List<BuildInput>? Builds = null);

/// <summary>The profile's talisman pool as the client saves it; <paramref name="Renames"/> maps old to new names of renamed talismans.</summary>
public sealed record TalismansPayload(List<TalismanInput> Talismans, Dictionary<string, string>? Renames = null);

/// <summary>Result of saving a weapon-type preset: the profile and the weapons whose conditions followed the preset.</summary>
public sealed record PresetSavedDto(ProfileDto Profile, IReadOnlyList<string> UpdatedWeapons);

/// <summary>
/// Profiles under inputs/profiles (layout in <see cref="ProfileFiles"/>). Each weapon is a request file the CLI can open
/// directly; its talismans.file points at the profile's shared pool.
/// </summary>
public sealed partial class ProfileStore(AppPaths paths)
{
    public string Directory => paths.Inputs;
    private string Root => ProfileFiles.ProfilesDirectory(paths.Inputs);

    public static bool IsValidName(string name) =>
        NamePattern().IsMatch(name) && !new[] { ".talismans", ".results", ".builds" }.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    // ---------------------------------------------------------------- profiles

    public IReadOnlyList<ProfileSummaryDto> ListProfiles()
    {
        if (!System.IO.Directory.Exists(Root)) return [];
        return System.IO.Directory.GetDirectories(Root)
            .Where(d => File.Exists(Path.Combine(d, ProfileFiles.SettingsFileName)))
            .Select(d => new ProfileSummaryDto(
                Path.GetFileName(d),
                RequestFiles.ListRequests(ProfileFiles.WeaponsDirectory(d)).Count,
                LoadTalismans(Path.GetFileName(d)).Count,
                LastWrite(d)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool ProfileExists(string profile) => File.Exists(Path.Combine(ProfileDir(profile), ProfileFiles.SettingsFileName));

    public ProfileDto? LoadProfile(string profile)
    {
        if (!ProfileExists(profile)) return null;
        return new ProfileDto(profile, LoadTalismans(profile), ProfileFiles.ReadSettings(ProfileDir(profile)).ConditionPresets);
    }

    /// <summary>Creates an empty profile; an existing one is returned unchanged.</summary>
    public ProfileDto CreateProfile(string profile)
    {
        if (!ProfileExists(profile))
        {
            ProfileFiles.SaveSettings(new ProfileSettings(), ProfileDir(profile));
            RequestFiles.SaveTalismans([], TalismansPath(profile));
            System.IO.Directory.CreateDirectory(WeaponsDir(profile));
        }
        return LoadProfile(profile)!;
    }

    /// <param name="renames">Old name to new name of renamed talismans: hand-entered builds of every weapon refer to talismans by name and follow.</param>
    public ProfileDto SaveTalismans(string profile, IReadOnlyList<TalismanInput> talismans, IReadOnlyDictionary<string, string>? renames = null)
    {
        RequestFiles.SaveTalismans(talismans, TalismansPath(profile));
        if (renames is { Count: > 0 })
        {
            foreach (var file in RequestFiles.ListRequests(WeaponsDir(profile)))
            {
                var builds = RequestFiles.LoadBuildsFor(file);
                if (!builds.Any(b => b.Talisman is { } t && renames.ContainsKey(t.Name))) continue;
                RequestFiles.SaveBuildsFor(builds.Select(b => b.Talisman is { } t && renames.TryGetValue(t.Name, out var name)
                    ? b with { Talisman = t with { Name = name } } : b).ToList(), file);
            }
        }
        return LoadProfile(profile)!;
    }

    public List<TalismanInput> LoadTalismans(string profile)
    {
        var path = TalismansPath(profile);
        return File.Exists(path) ? TalismanInputLoader.Read(path).ToList() : [];
    }

    /// <summary>
    /// Saves a weapon type's preset and moves every saved weapon of that type along (<see cref="ConditionPresets.Follow"/>):
    /// values that matched the old preset take the new one, the weapons' own overrides stay.
    /// </summary>
    public PresetSavedDto SavePreset(string profile, string weaponKind, Conditions preset)
    {
        var dir = ProfileDir(profile);
        var settings = ProfileFiles.ReadSettings(dir);
        var old = settings.PresetFor(weaponKind);
        var presets = new Dictionary<string, Conditions>(settings.ConditionPresets) { [weaponKind] = preset };
        ProfileFiles.SaveSettings(settings with { ConditionPresets = presets }, dir);

        var updated = new List<string>();
        foreach (var file in RequestFiles.ListRequests(WeaponsDir(profile)))
        {
            var request = RequestLoader.Read(file);
            if (WeaponKind(request) != weaponKind) continue;
            var conditions = ConditionPresets.Follow(request.Conditions, old, preset);
            if (ConditionPresets.Same(conditions, request.Conditions)) continue;
            RequestFiles.SaveRequest(request with { Conditions = conditions }, file);
            updated.Add(Path.GetFileNameWithoutExtension(file));
        }
        return new PresetSavedDto(LoadProfile(profile)!, updated);
    }

    // ---------------------------------------------------------------- weapons

    public IReadOnlyList<WeaponSummaryDto> ListWeapons(string profile)
    {
        var result = new List<WeaponSummaryDto>();
        foreach (var file in RequestFiles.ListRequests(WeaponsDir(profile)))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            string type = "?";
            string? summary;
            try
            {
                var r = RequestLoader.Read(file);
                type = WeaponKind(r);
                summary = Summary(r);
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException)
            {
                summary = "(cannot read: " + e.Message + ")";
            }
            result.Add(new WeaponSummaryDto(name, type, File.GetLastWriteTimeUtc(file), File.Exists(ResultsJsonPath(profile, name)), summary));
        }
        return result.OrderBy(w => w.Type).ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public WeaponDto? LoadWeapon(string profile, string name)
    {
        var path = WeaponPath(profile, name);
        if (!File.Exists(path)) return null;
        return new WeaponDto(profile, name, RequestLoader.Read(path), RequestFiles.LoadBuildsFor(path), File.Exists(ResultsJsonPath(profile, name)));
    }

    /// <summary>Saves request + builds; the request's talisman file is pointed at the profile's pool.</summary>
    public WeaponDto SaveWeapon(string profile, string name, OptimizationRequest request, IReadOnlyList<BuildInput> builds)
    {
        var path = WeaponPath(profile, name);
        request = request with { Talismans = request.Talismans with { File = ProfileFiles.SharedTalismanReference } };
        RequestFiles.SaveRequest(request, path);
        RequestFiles.SaveBuildsFor(builds, path);
        return new WeaponDto(profile, name, request, builds, File.Exists(ResultsJsonPath(profile, name)));
    }

    public bool DeleteWeapon(string profile, string name)
    {
        var path = WeaponPath(profile, name);
        var any = false;
        foreach (var p in new[] { path, RequestFiles.BuildsPathFor(path), ResultsJsonPath(profile, name), ResultsTextPath(profile, name) })
        {
            if (!File.Exists(p)) continue;
            File.Delete(p);
            any = true;
        }
        return any;
    }

    public void SaveResults(string profile, string name, string text, ResultDto results)
    {
        System.IO.Directory.CreateDirectory(WeaponsDir(profile));
        File.WriteAllText(ResultsTextPath(profile, name), text);
        File.WriteAllText(ResultsJsonPath(profile, name), JsonSerializer.Serialize(results, ApiJson.Options));
    }

    public ResultDto? LoadResults(string profile, string name)
    {
        var path = ResultsJsonPath(profile, name);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ResultDto>(stream, ApiJson.Options);
    }

    public string? ResultsText(string profile, string name)
    {
        var path = ResultsTextPath(profile, name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    // ---------------------------------------------------------------- helpers

    public static string WeaponKind(OptimizationRequest r) => r.Weapon.Spec?.Type ?? r.Weapon.Type ?? "?";

    public static string Summary(OptimizationRequest r)
    {
        var w = r.Weapon;
        var targets = r.TargetSkills.Count == 0 ? "no targets" : string.Join(", ", r.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}"));
        var element = w.Spec is { } spec && spec.Element != Core.Domain.Element.None ? spec.Element.ToString().ToLowerInvariant() + " " : "";
        return $"{element}{WeaponKind(r)}, {(r.SkillPair.Mode == SkillPairMode.Fixed ? $"{w.SetBonus ?? "-"} / {w.GroupSkill ?? "-"}" : "optimize pair")}; {targets}";
    }

    /// <summary>
    /// Fingerprint of what a run depends on: the request (minus where its talismans live) and the random talismans. A saved
    /// result whose hash differs from the weapon's current one is stale.
    /// </summary>
    public static string InputsHash(OptimizationRequest request, IReadOnlyList<TalismanInput> talismans)
    {
        var node = new JsonObject
        {
            ["request"] = JsonSerializer.SerializeToNode(request with { Talismans = request.Talismans with { File = null } }, ApiJson.Options),
            ["talismans"] = JsonSerializer.SerializeToNode(talismans, ApiJson.Options),
        };
        var canonical = Canonical(node)!.ToJsonString();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16].ToLowerInvariant();
    }

    /// <summary>Object properties sorted by name, so the hash does not depend on the order skills were entered in.</summary>
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, Canonical(kv.Value)))),
        JsonArray a => new JsonArray(a.Select(Canonical).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };

    private static DateTimeOffset LastWrite(string directory) =>
        System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(System.IO.Directory.GetLastWriteTimeUtc(directory)).Max();

    private string ProfileDir(string profile) => ProfileFiles.ProfileDirectory(paths.Inputs, profile);
    private string WeaponsDir(string profile) => ProfileFiles.WeaponsDirectory(ProfileDir(profile));
    private string TalismansPath(string profile) => Path.Combine(ProfileDir(profile), ProfileFiles.TalismansFileName);
    private string WeaponPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + ".json");
    private string ResultsJsonPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + RequestFiles.ResultsFileSuffix);
    private string ResultsTextPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + ".results.txt");

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]{0,79}$")]
    private static partial Regex NamePattern();
}
