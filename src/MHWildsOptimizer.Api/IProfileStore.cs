using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Api;

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
/// Where profiles live: files under inputs/profiles for the server (<see cref="ProfileStore"/>), browser storage for the
/// browser app. A profile owns a talisman pool, a condition preset per weapon type and weapons (request, hand-entered builds,
/// last run).
/// </summary>
public interface IProfileStore
{
    /// <summary>What relative talisman files in a request resolve against.</summary>
    string Directory { get; }

    IReadOnlyList<ProfileSummaryDto> ListProfiles();
    bool ProfileExists(string profile);
    ProfileDto? LoadProfile(string profile);

    /// <summary>Creates an empty profile; an existing one is returned unchanged.</summary>
    ProfileDto CreateProfile(string profile);

    /// <param name="renames">Old name to new name of renamed talismans: hand-entered builds of every weapon refer to talismans by name and follow.</param>
    ProfileDto SaveTalismans(string profile, IReadOnlyList<TalismanInput> talismans, IReadOnlyDictionary<string, string>? renames = null);

    List<TalismanInput> LoadTalismans(string profile);

    /// <summary>
    /// Saves a weapon type's preset and moves every saved weapon of that type along (<see cref="ConditionPresets.Follow"/>):
    /// values that matched the old preset take the new one, the weapons' own overrides stay.
    /// </summary>
    PresetSavedDto SavePreset(string profile, string weaponKind, Conditions preset);

    IReadOnlyList<WeaponSummaryDto> ListWeapons(string profile);
    WeaponDto? LoadWeapon(string profile, string name);

    /// <summary>Saves request + builds; the request's talisman file is pointed at the profile's pool.</summary>
    WeaponDto SaveWeapon(string profile, string name, OptimizationRequest request, IReadOnlyList<BuildInput> builds);

    bool DeleteWeapon(string profile, string name);
    void SaveResults(string profile, string name, string text, ResultDto results);
    ResultDto? LoadResults(string profile, string name);
    string? ResultsText(string profile, string name);
}
