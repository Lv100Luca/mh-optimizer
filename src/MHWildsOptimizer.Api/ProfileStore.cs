using System.Text.Json;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Api;

/// <summary>
/// Profiles under inputs/profiles (layout in <see cref="ProfileFiles"/>). Each weapon is a request file the CLI can open
/// directly; its talismans.file points at the profile's shared pool.
/// </summary>
/// <param name="inputs">The inputs directory (profiles live in its profiles/ folder).</param>
public sealed class ProfileStore(string inputs) : IProfileStore
{
    public string Directory => inputs;
    private string Root => ProfileFiles.ProfilesDirectory(inputs);

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

    public ProfileDto SaveTalismans(string profile, IReadOnlyList<TalismanInput> talismans, IReadOnlyDictionary<string, string>? renames = null)
    {
        RequestFiles.SaveTalismans(talismans, TalismansPath(profile));
        if (renames is { Count: > 0 })
        {
            foreach (var file in RequestFiles.ListRequests(WeaponsDir(profile)))
                if (ProfileRules.RenameTalismans(RequestFiles.LoadBuildsFor(file), renames) is { } renamed)
                    RequestFiles.SaveBuildsFor(renamed, file);
        }
        return LoadProfile(profile)!;
    }

    public List<TalismanInput> LoadTalismans(string profile)
    {
        var path = TalismansPath(profile);
        return File.Exists(path) ? TalismanInputLoader.Read(path).ToList() : [];
    }

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
            if (ProfileRules.WeaponKind(request) != weaponKind) continue;
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
                type = ProfileRules.WeaponKind(r);
                summary = ProfileRules.Summary(r);
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

    private static DateTimeOffset LastWrite(string directory) =>
        System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(System.IO.Directory.GetLastWriteTimeUtc(directory)).Max();

    private string ProfileDir(string profile) => ProfileFiles.ProfileDirectory(inputs, profile);
    private string WeaponsDir(string profile) => ProfileFiles.WeaponsDirectory(ProfileDir(profile));
    private string TalismansPath(string profile) => Path.Combine(ProfileDir(profile), ProfileFiles.TalismansFileName);
    private string WeaponPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + ".json");
    private string ResultsJsonPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + RequestFiles.ResultsFileSuffix);
    private string ResultsTextPath(string profile, string name) => Path.Combine(WeaponsDir(profile), name + ".results.txt");
}
