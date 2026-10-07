using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        var element = w.Spec is { } spec && spec.Element != Core.Domain.Element.None ? spec.Element.ToString().ToLowerInvariant() + " " : "";
        return $"{element}{WeaponKind(r)}, {(r.SkillPair.Mode == SkillPairMode.Fixed ? $"{w.SetBonus ?? "-"} / {w.GroupSkill ?? "-"}" : "optimize pair")}; {targets}";
    }

    /// <summary>Hand-entered builds after talismans were renamed (old name to new name); null when none refers to a renamed one.</summary>
    public static List<BuildInput>? RenameTalismans(IReadOnlyList<BuildInput> builds, IReadOnlyDictionary<string, string> renames)
    {
        if (!builds.Any(b => b.Talisman is { } t && renames.ContainsKey(t.Name))) return null;
        return builds.Select(b => b.Talisman is { } t && renames.TryGetValue(t.Name, out var name) ? b with { Talisman = t with { Name = name } } : b).ToList();
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
