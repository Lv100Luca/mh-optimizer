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

    /// <summary>Loads the normalized dataset from a directory containing armor_hr.json, skills.json, decorations.json, charms.json and gogma_weapons_base.json.</summary>
    public static GameData Load(string dataDirectory)
    {
        var skills = ReadFile<List<Skill>>(dataDirectory, "skills.json");
        var armor = ReadFile<List<ArmorPiece>>(dataDirectory, "armor_hr.json");
        var decorations = ReadFile<List<Decoration>>(dataDirectory, "decorations.json");
        var charms = ReadFile<List<Charm>>(dataDirectory, "charms.json");
        var gogmaRaw = ReadFile<Dictionary<string, Dictionary<string, GogmaWeaponVariant>>>(dataDirectory, "gogma_weapons_base.json");

        var gogma = gogmaRaw.ToDictionary(
            kv => WeaponTypeInfo.FromApiKind(kv.Key),
            kv => (IReadOnlyDictionary<GogmaFocus, GogmaWeaponVariant>)kv.Value.ToDictionary(
                f => Enum.Parse<GogmaFocus>(f.Key, ignoreCase: true),
                f => f.Value));

        return new GameData(skills, armor, decorations, charms, gogma);
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

    private static T ReadFile<T>(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidDataException($"'{path}' deserialized to null.");
    }
}
