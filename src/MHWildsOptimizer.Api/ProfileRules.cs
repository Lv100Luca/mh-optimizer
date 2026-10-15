using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Api;

/// <summary>Names, summaries and fingerprints every profile store shares.</summary>
public static partial class ProfileRules
{
    public static bool IsValidName(string name) =>
        NamePattern().IsMatch(name) && !new[] { ".talismans", ".results", ".builds" }.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    public const string NameRules = "Use letters, digits, spaces, '-', '_' or '.' for names.";

    public static string WeaponKind(OptimizationRequest r) => r.Weapon.Spec?.Type ?? r.Weapon.Type ?? "?";

    public static string Summary(OptimizationRequest r)
    {
        var w = r.Weapon;
        var targets = r.TargetSkills.Count == 0 ? "no targets" : string.Join(", ", r.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}"));
        if (w.TechTree is { } tree) return $"{tree}; {targets}";
        var element = w.Spec is { } spec && spec.Element != Core.Domain.Element.None ? spec.Element.ToString().ToLowerInvariant() + " " : "";
        return $"{element}{WeaponKind(r)}, {(r.SkillPair.Mode == SkillPairMode.Fixed ? $"{w.SetBonus ?? "-"} / {w.GroupSkill ?? "-"}" : "optimize pair")}; {targets}";
    }

    /// <summary>Hand-entered builds after talismans were renamed (old name to new name); null when none refers to a renamed one.</summary>
    public static List<BuildInput>? RenameTalismans(IReadOnlyList<BuildInput> builds, IReadOnlyDictionary<string, string> renames)
    {
        if (!builds.Any(b => b.Talisman is { } t && renames.ContainsKey(t.Name))) return null;
        return builds.Select(b => b.Talisman is { } t && renames.TryGetValue(t.Name, out var name) ? b with { Talisman = t with { Name = name } } : b).ToList();
    }

    /// <summary>Talismans as the game labels them (see <see cref="TalismanInputLoader.WithGameRarity"/>); null when nothing changes.</summary>
    public static List<TalismanInput>? FixTalismanRarities(IReadOnlyList<TalismanInput> talismans)
    {
        var fixedList = talismans.Select(TalismanInputLoader.WithGameRarity).ToList();
        return fixedList.Where((t, i) => !ReferenceEquals(t, talismans[i])).Any() ? fixedList : null;
    }

    /// <summary>
    /// Builds whose possible talisman (spelled out with its rarity) was saved with the pool's old labels, R4-R7 for the game's
    /// 5-8: its rarity goes up by one. Null when no build has one.
    /// </summary>
    public static List<BuildInput>? FixTalismanRarities(IReadOnlyList<BuildInput> builds)
    {
        if (!builds.Any(b => b.Talisman is { Skills: not null, Rarity: >= 4 and <= 7 })) return null;
        return builds.Select(b => b.Talisman is { Skills: not null, Rarity: >= 4 and <= 7 } t ? b with { Talisman = t with { Rarity = t.Rarity + 1 } } : b).ToList();
    }

    /// <summary>Saved results whose possible talismans carry the pool's old labels; null when none does.</summary>
    public static ResultDto? FixTalismanRarities(ResultDto results)
    {
        static bool Old(BuildTalismanDto? t) => t is { Source: TalismanSource.Possible, Rarity: >= 4 and <= 7 };
        if (!results.Pairs.Any(p => p.Builds.Any(b => Old(b.Talisman)))) return null;
        return results with
        {
            Pairs = results.Pairs.Select(p => p with
            {
                Builds = p.Builds.Select(b => Old(b.Talisman) ? b with { Talisman = b.Talisman! with { Rarity = b.Talisman.Rarity + 1 } } : b).ToList(),
            }).ToList(),
        };
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

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]{0,79}$")]
    private static partial Regex NamePattern();
}
