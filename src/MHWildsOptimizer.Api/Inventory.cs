using System.Text.Json;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Api;

/// <summary>The best build of a weapon's last optimizer run, as it was scored then.</summary>
public sealed record LastRunDto(DateTimeOffset CompletedAt, string PairLabel, BuildDto Build);

/// <summary>
/// One weapon of a profile for the inventory overview: its picked build scored now (current conditions and talismans),
/// and the best build of its last run. <paramref name="Stale"/> = the request or the talismans changed since that run.
/// </summary>
public sealed record InventoryEntryDto(
    string Name,
    string Type,
    DateTimeOffset Modified,
    string Summary,
    WeaponStatsDto? Weapon,
    IReadOnlyList<string> Errors,
    IReadOnlyList<TargetDto> Targets,
    int Builds,
    EvaluatedBuildDto? Picked,
    LastRunDto? LastRun,
    bool Stale);

public static class Inventory
{
    public static IReadOnlyList<InventoryEntryDto> Build(IProfileStore store, string profile, GameData data)
    {
        var talismans = store.LoadTalismans(profile);
        return store.ListWeapons(profile).Select(summary =>
        {
            WeaponDto? weapon;
            try { weapon = store.LoadWeapon(profile, summary.Name); }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException) { weapon = null; }
            if (weapon is null)
                return new InventoryEntryDto(summary.Name, summary.Type, summary.Modified, summary.Summary ?? "", null, [summary.Summary ?? "Cannot read the weapon."], [], 0, null, null, false);

            var payload = new ConfigPayload(weapon.Request, talismans, weapon.Builds.Where(b => b.Picked).Take(1).ToList());
            var resolved = Resolving.Resolve(payload, data, store.Directory);
            var described = Resolving.Describe(resolved, weapon.Request.TargetSkills, data);
            var picked = BuildEvaluation.Evaluate(payload, resolved, data).FirstOrDefault();

            LastRunDto? lastRun = null;
            var stale = false;
            ResultDto? results = null;
            try { results = store.LoadResults(profile, summary.Name); }
            catch (Exception e) when (e is JsonException or IOException) { }
            if (results is not null)
            {
                var best = results.Pairs.SelectMany(p => p.Builds.Select(b => (Pair: p, Build: b))).MaxBy(x => x.Build.Score);
                if (best.Build is not null) lastRun = new LastRunDto(results.CompletedAt, best.Pair.Label, best.Build);
                stale = results.InputsHash != ProfileRules.InputsHash(weapon.Request, talismans);
            }

            return new InventoryEntryDto(summary.Name, summary.Type, summary.Modified, summary.Summary ?? "", described.Weapon, described.Errors,
                described.Targets, weapon.Builds.Count, picked, lastRun, stale);
        }).ToList();
    }
}
