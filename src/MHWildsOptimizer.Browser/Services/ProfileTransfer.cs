using System.Text.Json;
using System.Text.Json.Nodes;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>A weapon read from files: request, hand-entered builds and (optionally) its last run.</summary>
public sealed record WeaponImport(OptimizationRequest Request, List<BuildInput> Builds, ResultDto? Results);

/// <summary>What a set of files contains; <see cref="Problems"/> lists files that could not be read.</summary>
public sealed record ImportPlan(string? ProfileName, ProfileSettings? Settings, List<TalismanInput>? Talismans, IReadOnlyDictionary<string, WeaponImport> Weapons,
    IReadOnlyList<string> Problems)
{
    public bool IsEmpty => Settings is null && Talismans is null && Weapons.Count == 0;
}

/// <summary>
/// Moves profiles in and out of browser storage. Export writes one JSON bundle holding the files of the server's profile
/// layout (profile.json, talismans.json, weapons/&lt;name&gt;.json, .builds.json, .results.json); import reads such a
/// bundle or the loose files of a profile folder (inputs/profiles/&lt;profile&gt;/ and its weapons/).
/// </summary>
public sealed class ProfileTransfer(BrowserProfileStore store)
{
    public const string Format = "mhwilds-optimizer-profile";

    public string Export(string profile)
    {
        var files = new JsonObject
        {
            [ProfileFiles.SettingsFileName] = JsonSerializer.SerializeToNode(store.LoadSettings(profile), RequestFiles.WriteOptions),
            [ProfileFiles.TalismansFileName] = JsonSerializer.SerializeToNode(store.LoadTalismans(profile), RequestFiles.WriteOptions),
        };
        foreach (var w in store.ListWeapons(profile))
        {
            if (store.LoadWeapon(profile, w.Name) is not { } weapon) continue;
            var path = $"{ProfileFiles.WeaponsDirectoryName}/{w.Name}";
            files[path + ".json"] = JsonSerializer.SerializeToNode(weapon.Request, RequestFiles.WriteOptions);
            if (weapon.Builds.Count > 0) files[path + RequestFiles.BuildsFileSuffix] = JsonSerializer.SerializeToNode(weapon.Builds, RequestFiles.WriteOptions);
            if (store.LoadResults(profile, w.Name) is { } results) files[path + RequestFiles.ResultsFileSuffix] = JsonSerializer.SerializeToNode(results, ApiJson.Options);
        }
        var bundle = new JsonObject
        {
            ["format"] = Format,
            ["version"] = 1,
            ["profile"] = profile,
            ["exported_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["files"] = files,
        };
        return bundle.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Reads an export bundle or the loose files of a profile folder (by file name; folders do not matter).</summary>
    public ImportPlan Read(IEnumerable<(string FileName, string Text)> input)
    {
        var problems = new List<string>();
        var files = new List<(string Name, string Text)>();
        string? profileName = null;
        foreach (var (fileName, text) in input)
        {
            JsonNode? node;
            try { node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException e) { problems.Add($"{fileName}: not JSON ({e.Message})"); continue; }
            if (node is JsonObject o && (string?)o["format"] == Format && o["files"] is JsonObject bundled)
            {
                profileName ??= (string?)o["profile"];
                files.AddRange(bundled.Select(kv => (kv.Key, kv.Value?.ToJsonString() ?? "null")));
            }
            else files.Add((fileName, text));
        }

        ProfileSettings? settings = null;
        List<TalismanInput>? talismans = null;
        var requests = new Dictionary<string, OptimizationRequest>();
        var builds = new Dictionary<string, List<BuildInput>>();
        var results = new Dictionary<string, ResultDto>();
        foreach (var (path, text) in files)
        {
            var name = path.Replace('\\', '/').Split('/')[^1];
            try
            {
                if (name.Equals(ProfileFiles.SettingsFileName, StringComparison.OrdinalIgnoreCase))
                    settings = JsonSerializer.Deserialize<ProfileSettings>(text, GameDataLoader.JsonOptions);
                else if (name.Equals(ProfileFiles.TalismansFileName, StringComparison.OrdinalIgnoreCase) || name.EndsWith(RequestFiles.TalismanFileSuffix, StringComparison.OrdinalIgnoreCase))
                    talismans = JsonSerializer.Deserialize<List<TalismanInput>>(text, GameDataLoader.JsonOptions);
                else if (name.EndsWith(RequestFiles.BuildsFileSuffix, StringComparison.OrdinalIgnoreCase))
                    builds[name[..^RequestFiles.BuildsFileSuffix.Length]] = JsonSerializer.Deserialize<List<BuildInput>>(text, GameDataLoader.JsonOptions) ?? [];
                else if (name.EndsWith(RequestFiles.ResultsFileSuffix, StringComparison.OrdinalIgnoreCase))
                    results[name[..^RequestFiles.ResultsFileSuffix.Length]] = JsonSerializer.Deserialize<ResultDto>(text, ApiJson.Options)!;
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var weapon = name[..^".json".Length];
                    if (!ProfileRules.IsValidName(weapon)) { problems.Add($"{path}: '{weapon}' is not a usable weapon name. {ProfileRules.NameRules}"); continue; }
                    requests[weapon] = JsonSerializer.Deserialize<OptimizationRequest>(text, GameDataLoader.JsonOptions)!;
                }
                else if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) problems.Add($"{path}: not a profile file");
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
            {
                problems.Add($"{path}: {e.Message}");
            }
        }
        foreach (var orphan in builds.Keys.Concat(results.Keys).Distinct().Where(w => !requests.ContainsKey(w)))
            problems.Add($"{orphan}: builds or results without the weapon file {orphan}.json");

        var weapons = requests.ToDictionary(kv => kv.Key, kv => new WeaponImport(kv.Value, builds.GetValueOrDefault(kv.Key) ?? [], results.GetValueOrDefault(kv.Key)));
        return new ImportPlan(profileName, settings, talismans, weapons, problems);
    }

    /// <summary>
    /// Writes the plan into a profile (created when missing): condition presets are merged, a talisman pool replaces the
    /// profile's, weapons replace ones with the same name.
    /// </summary>
    public void Apply(ImportPlan plan, string profile)
    {
        store.CreateProfile(profile);
        if (plan.Settings is { } settings)
        {
            var current = store.LoadSettings(profile);
            var presets = new Dictionary<string, Core.Damage.Conditions>(current.ConditionPresets);
            foreach (var (kind, preset) in settings.ConditionPresets) presets[kind] = preset;
            store.SaveSettings(profile, current with { ConditionPresets = presets });
        }
        if (plan.Talismans is { } talismans) store.SaveTalismans(profile, talismans);
        foreach (var (name, weapon) in plan.Weapons)
        {
            store.SaveWeapon(profile, name, weapon.Request, weapon.Builds);
            if (weapon.Results is { } results) store.SaveResults(profile, name, results.Text, results);
        }
    }
}
