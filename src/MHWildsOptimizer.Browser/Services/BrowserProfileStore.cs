using System.Text.Json;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// Profiles in browser storage, one entry per file of the server's layout:
/// <c>mhwo:p:&lt;profile&gt;:settings</c> (profile.json), <c>:talismans</c>, and per weapon <c>:w:&lt;name&gt;:request</c>,
/// <c>:builds</c>, <c>:results</c> (gzipped) and <c>:modified</c>. Names never contain ':' (<see cref="ProfileRules.IsValidName"/>).
/// </summary>
public sealed class BrowserProfileStore(BrowserStorage storage) : IProfileStore
{
    public const string Prefix = "mhwo:p:";
    /// <summary>The request files' conventions, without the indentation (storage is limited).</summary>
    public static readonly JsonSerializerOptions Json = new(RequestFiles.WriteOptions) { WriteIndented = false };

    /// <summary>Requests carry their talismans in every payload here, so there is nothing to resolve files against.</summary>
    public string Directory => "/";

    private static string P(string profile) => $"{Prefix}{profile}:";
    private static string W(string profile, string name) => $"{P(profile)}w:{name}:";

    // ---------------------------------------------------------------- profiles

    private IEnumerable<string> ProfileNames() =>
        storage.Keys(Prefix).Where(k => k.EndsWith(":settings", StringComparison.Ordinal))
            .Select(k => k[Prefix.Length..^":settings".Length]);

    public IReadOnlyList<ProfileSummaryDto> ListProfiles() =>
        ProfileNames()
            .Select(p => new ProfileSummaryDto(p, WeaponNames(p).Count, LoadTalismans(p).Count, Modified($"{P(p)}modified")))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool ProfileExists(string profile) => storage.GetString($"{P(profile)}settings") is not null;

    public ProfileDto? LoadProfile(string profile) =>
        ProfileExists(profile) ? new ProfileDto(profile, LoadTalismans(profile), Settings(profile).ConditionPresets) : null;

    public ProfileDto CreateProfile(string profile)
    {
        if (!ProfileExists(profile))
        {
            storage.Set($"{P(profile)}settings", new ProfileSettings(), Json);
            storage.Set($"{P(profile)}talismans", new List<TalismanInput>(), Json);
            Touch(profile);
        }
        return LoadProfile(profile)!;
    }

    /// <summary>Removes a profile and everything in it.</summary>
    public void DeleteProfile(string profile)
    {
        foreach (var key in storage.Keys(P(profile))) storage.Remove(key);
    }

    public ProfileDto SaveTalismans(string profile, IReadOnlyList<TalismanInput> talismans, IReadOnlyDictionary<string, string>? renames = null)
    {
        storage.Set($"{P(profile)}talismans", talismans, Json);
        if (renames is { Count: > 0 })
            foreach (var name in WeaponNames(profile))
                if (ProfileRules.RenameTalismans(LoadBuilds(profile, name), renames) is { } renamed)
                    storage.Set($"{W(profile, name)}builds", renamed, Json);
        Touch(profile);
        return LoadProfile(profile)!;
    }

    public List<TalismanInput> LoadTalismans(string profile) => storage.Get<List<TalismanInput>>($"{P(profile)}talismans", Json) ?? [];

    public PresetSavedDto SavePreset(string profile, string weaponKind, Conditions preset)
    {
        var settings = Settings(profile);
        var old = settings.PresetFor(weaponKind);
        storage.Set($"{P(profile)}settings", settings with { ConditionPresets = new(settings.ConditionPresets) { [weaponKind] = preset } }, Json);

        var updated = new List<string>();
        foreach (var name in WeaponNames(profile))
        {
            if (LoadRequest(profile, name) is not { } request || ProfileRules.WeaponKind(request) != weaponKind) continue;
            var conditions = ConditionPresets.Follow(request.Conditions, old, preset);
            if (ConditionPresets.Same(conditions, request.Conditions)) continue;
            storage.Set($"{W(profile, name)}request", request with { Conditions = conditions }, Json);
            TouchWeapon(profile, name);
            updated.Add(name);
        }
        Touch(profile);
        return new PresetSavedDto(LoadProfile(profile)!, updated);
    }

    private ProfileSettings Settings(string profile) => storage.Get<ProfileSettings>($"{P(profile)}settings", Json) ?? new ProfileSettings();

    public ProfileSettings LoadSettings(string profile) => Settings(profile);

    /// <summary>Replaces profile.json (import); <see cref="SavePreset"/> is the editing path.</summary>
    public void SaveSettings(string profile, ProfileSettings settings)
    {
        storage.Set($"{P(profile)}settings", settings, Json);
        Touch(profile);
    }

    /// <summary>Characters this app keeps in browser storage (about 5 million fit).</summary>
    public long StorageUsed() => storage.Size("mhwo");

    // ---------------------------------------------------------------- weapons

    private List<string> WeaponNames(string profile)
    {
        var prefix = $"{P(profile)}w:";
        return storage.Keys(prefix).Where(k => k.EndsWith(":request", StringComparison.Ordinal))
            .Select(k => k[prefix.Length..^":request".Length]).ToList();
    }

    public IReadOnlyList<WeaponSummaryDto> ListWeapons(string profile)
    {
        var result = new List<WeaponSummaryDto>();
        foreach (var name in WeaponNames(profile))
        {
            string type = "?";
            string? summary;
            try
            {
                var r = LoadRequest(profile, name)!;
                type = ProfileRules.WeaponKind(r);
                summary = ProfileRules.Summary(r);
            }
            catch (Exception e) when (e is JsonException or NullReferenceException)
            {
                summary = "(cannot read: " + e.Message + ")";
            }
            result.Add(new WeaponSummaryDto(name, type, Modified($"{W(profile, name)}modified"), HasResults(profile, name), summary));
        }
        return result.OrderBy(w => w.Type).ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private OptimizationRequest? LoadRequest(string profile, string name) => storage.Get<OptimizationRequest>($"{W(profile, name)}request", Json);

    private List<BuildInput> LoadBuilds(string profile, string name) => storage.Get<List<BuildInput>>($"{W(profile, name)}builds", Json) ?? [];

    private bool HasResults(string profile, string name) => storage.GetString($"{W(profile, name)}results") is not null;

    public WeaponDto? LoadWeapon(string profile, string name) =>
        LoadRequest(profile, name) is { } request ? new WeaponDto(profile, name, request, LoadBuilds(profile, name), HasResults(profile, name)) : null;

    public WeaponDto SaveWeapon(string profile, string name, OptimizationRequest request, IReadOnlyList<BuildInput> builds)
    {
        request = request with { Talismans = request.Talismans with { File = ProfileFiles.SharedTalismanReference } };
        storage.Set($"{W(profile, name)}request", request, Json);
        if (builds.Count > 0) storage.Set($"{W(profile, name)}builds", builds, Json);
        else storage.Remove($"{W(profile, name)}builds");
        TouchWeapon(profile, name);
        Touch(profile);
        return new WeaponDto(profile, name, request, builds, HasResults(profile, name));
    }

    public bool DeleteWeapon(string profile, string name)
    {
        var keys = storage.Keys(W(profile, name));
        foreach (var key in keys) storage.Remove(key);
        if (keys.Count > 0) Touch(profile);
        return keys.Count > 0;
    }

    public void SaveResults(string profile, string name, string text, ResultDto results)
    {
        // the text report is part of the result
        storage.SetCompressed($"{W(profile, name)}results", results, ApiJson.Options);
        Touch(profile);
    }

    public ResultDto? LoadResults(string profile, string name) => storage.Get<ResultDto>($"{W(profile, name)}results", ApiJson.Options);

    public string? ResultsText(string profile, string name) => LoadResults(profile, name)?.Text;

    // ---------------------------------------------------------------- modification times

    private DateTimeOffset Modified(string key) =>
        DateTimeOffset.TryParse(storage.GetString(key), out var t) ? t : DateTimeOffset.MinValue;

    private void Touch(string profile) => storage.SetString($"{P(profile)}modified", DateTimeOffset.UtcNow.ToString("O"));

    private void TouchWeapon(string profile, string name) => storage.SetString($"{W(profile, name)}modified", DateTimeOffset.UtcNow.ToString("O"));
}
