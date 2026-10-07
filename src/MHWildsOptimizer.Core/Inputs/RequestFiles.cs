using System.Text.Json;
using System.Text.Json.Serialization;
using MHWildsOptimizer.Core.Data;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>Read/write of request and talisman files so configurations can be saved, reloaded and edited.</summary>
public static class RequestFiles
{
    public static readonly JsonSerializerOptions WriteOptions = new(GameDataLoader.JsonOptions)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const string TalismanFileSuffix = ".talismans.json";
    public const string ResultsFileSuffix = ".results.json";
    public const string BuildsFileSuffix = ".builds.json";

    public static void SaveRequest(OptimizationRequest request, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(request, WriteOptions));
    }

    public static void SaveTalismans(IEnumerable<TalismanInput> talismans, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(talismans.ToList(), WriteOptions));
    }

    /// <summary>The talisman file a request points to, resolved relative to the request file.</summary>
    public static string? TalismanPathFor(OptimizationRequest request, string requestPath)
    {
        if (request.Talismans.File is not { } file) return null;
        return Path.IsPathRooted(file) ? file : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(requestPath)) ?? ".", file);
    }

    public static List<TalismanInput> LoadTalismansFor(OptimizationRequest request, string requestPath)
    {
        var path = TalismanPathFor(request, requestPath);
        return path is not null && File.Exists(path) ? TalismanInputLoader.Read(path).ToList() : [];
    }

    /// <summary>Default talisman file name next to a request file: request.json -> request.talismans.json.</summary>
    public static string DefaultTalismanFileName(string requestPath) =>
        Path.GetFileNameWithoutExtension(requestPath) + TalismanFileSuffix;

    /// <summary>Hand-entered builds kept next to a request file: request.json -> request.builds.json.</summary>
    public static string BuildsPathFor(string requestPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(requestPath)) ?? ".", Path.GetFileNameWithoutExtension(requestPath) + BuildsFileSuffix);

    public static List<BuildInput> LoadBuildsFor(string requestPath)
    {
        var path = BuildsPathFor(requestPath);
        return File.Exists(path) ? BuildInputLoader.Read(path).ToList() : [];
    }

    /// <summary>Writes the builds next to the request file, or removes that file when there are none.</summary>
    public static void SaveBuildsFor(IReadOnlyCollection<BuildInput> builds, string requestPath)
    {
        var path = BuildsPathFor(requestPath);
        if (builds.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(builds.ToList(), WriteOptions));
    }

    /// <summary>Request files in a directory (talisman, builds and saved-results files excluded).</summary>
    public static IReadOnlyList<string> ListRequests(string directory) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json")
                .Where(f => !new[] { TalismanFileSuffix, ResultsFileSuffix, BuildsFileSuffix }.Any(s => f.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(f => f).ToList()
            : [];
}
