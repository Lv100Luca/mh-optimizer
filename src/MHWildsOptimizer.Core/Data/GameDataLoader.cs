using System.Text.Json;
using System.Text.Json.Serialization;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Data;

public static class GameDataLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>The dataset files <see cref="Load(string)"/> reads; the last two are optional.</summary>
    public static readonly string[] FileNames =
        ["skills.json", "armor_hr.json", "decorations.json", "charms.json", "gogma_weapons_base.json", "gogma_skill_pool.json", "random_talisman_pool.json"];

    /// <summary>Loads the normalized dataset from a directory containing armor_hr.json, skills.json, decorations.json, charms.json and gogma_weapons_base.json.</summary>
    public static GameData Load(string dataDirectory) =>
        Load(name => Path.Combine(dataDirectory, name) is var path && File.Exists(path) ? File.OpenRead(path) : null, dataDirectory);

    /// <summary>Loads the dataset from streams, e.g. files the browser fetched; <paramref name="open"/> returns null for a missing file.</summary>
    /// <param name="source">Where the files come from, for error messages.</param>
    public static GameData Load(Func<string, Stream?> open, string source = "the dataset")
    {
        T Read<T>(string name) => TryRead<T>(name) ?? throw new FileNotFoundException($"'{name}' is missing from {source}.", name);
        T? TryRead<T>(string name)
        {
            using var stream = open(name);
            if (stream is null) return default;
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException($"'{name}' in {source} deserialized to null.");
        }

        var skills = Read<List<Skill>>("skills.json");
        var armor = Read<List<ArmorPiece>>("armor_hr.json");
        var decorations = Read<List<Decoration>>("decorations.json");
        var charms = Read<List<Charm>>("charms.json");
        var gogmaRaw = Read<Dictionary<string, Dictionary<string, GogmaWeaponVariant>>>("gogma_weapons_base.json");

        var gogma = gogmaRaw.ToDictionary(
            kv => WeaponTypeInfo.FromApiKind(kv.Key),
            kv => (IReadOnlyDictionary<GogmaFocus, GogmaWeaponVariant>)kv.Value.ToDictionary(
                f => Enum.Parse<GogmaFocus>(f.Key, ignoreCase: true),
                f => f.Value));

        var pairs = TryRead<SkillPoolFile>("gogma_skill_pool.json")?.Pairs
                        .Select(p => new GogmaSkillPair(p.SetBonus, p.GroupSkill)).ToList();

        var pool = TryRead<TalismanPoolFile>("random_talisman_pool.json") is { } tp
            ? new RandomTalismanPool { Skills = tp.Skills, SlotPatterns = tp.SlotPatterns, RarityByType = tp.RarityByType }
            : null;

        return new GameData(skills, armor, decorations, charms, gogma, pairs, pool);
    }

    /// <summary>Walks up from <paramref name="start"/> (default: the executable's directory) until a <c>data/armor_hr.json</c> is found.</summary>
    public static string FindDataDirectory(string? start = null)
    {
        var origin = start ?? AppContext.BaseDirectory;
        var dir = new DirectoryInfo(origin);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "armor_hr.json")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"No 'data' directory with armor_hr.json found above '{origin}'.");
    }

    public static T ReadJson<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidDataException($"'{path}' deserialized to null.");
    }

    private sealed record SkillPoolFile(List<SkillPoolPair> Pairs);
    private sealed record SkillPoolPair(string SetBonus, string GroupSkill);

    private sealed record TalismanPoolFile(
        Dictionary<string, RandomTalismanPool.PoolSkill> Skills,
        List<RandomTalismanPool.SlotPattern> SlotPatterns,
        Dictionary<string, int> RarityByType);
}
