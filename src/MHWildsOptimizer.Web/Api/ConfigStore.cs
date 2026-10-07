using System.Text.Json;
using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Web.Api;

public sealed record ConfigSummaryDto(string Name, DateTimeOffset Modified, bool HasResults, string? Summary);

public sealed record ConfigDto(string Name, OptimizationRequest Request, IReadOnlyList<TalismanInput> Talismans, IReadOnlyList<BuildInput> Builds, bool HasResults);

/// <summary>
/// What the client sends to validate, save, run or evaluate: the request plus the random talismans and the hand-entered builds
/// (both kept in sibling files on disk).
/// </summary>
public sealed record ConfigPayload(OptimizationRequest Request, List<TalismanInput>? Talismans, List<BuildInput>? Builds = null);

/// <summary>
/// Saved configurations in the inputs directory, using the CLI's file layout:
/// &lt;name&gt;.json (request), &lt;name&gt;.talismans.json (random talismans), &lt;name&gt;.builds.json (hand-entered builds),
/// &lt;name&gt;.results.txt / .results.json (last run).
/// </summary>
public sealed partial class ConfigStore(AppPaths paths)
{
    public string Directory => paths.Inputs;

    public static bool IsValidName(string name) =>
        NamePattern().IsMatch(name) && !new[] { ".talismans", ".results", ".builds" }.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ConfigSummaryDto> List()
    {
        var result = new List<ConfigSummaryDto>();
        foreach (var file in RequestFiles.ListRequests(Directory))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (IsJsonArray(file)) continue; // a talisman list under another name (e.g. talismans.example.json), not a request
            string? summary = null;
            try
            {
                var r = RequestLoader.Read(file);
                var w = r.Weapon;
                var type = w.Spec?.Type ?? w.Type ?? "?";
                var targets = r.TargetSkills.Count == 0 ? "no targets" : string.Join(", ", r.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}"));
                summary = $"{type}, {(r.SkillPair.Mode == SkillPairMode.Fixed ? $"{w.SetBonus ?? "-"} / {w.GroupSkill ?? "-"}" : "optimize pair")}; {targets}";
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException)
            {
                summary = "(cannot read: " + e.Message + ")";
            }
            result.Add(new ConfigSummaryDto(name, File.GetLastWriteTimeUtc(file), File.Exists(ResultsJsonPath(name)), summary));
        }
        return result.OrderByDescending(c => c.Modified).ToList();
    }

    public ConfigDto? Load(string name)
    {
        var path = RequestPath(name);
        if (!File.Exists(path)) return null;
        var request = RequestLoader.Read(path);
        var talismans = RequestFiles.LoadTalismansFor(request, path);
        return new ConfigDto(name, request, talismans, RequestFiles.LoadBuildsFor(path), File.Exists(ResultsJsonPath(name)));
    }

    /// <summary>Saves request + talismans + builds; the request's talisman file is pointed at the sibling file.</summary>
    public ConfigDto Save(string name, OptimizationRequest request, IReadOnlyList<TalismanInput> talismans, IReadOnlyList<BuildInput> builds)
    {
        var path = RequestPath(name);
        var talismanFile = RequestFiles.DefaultTalismanFileName(path);
        request = request with { Talismans = request.Talismans with { File = talismanFile } };
        RequestFiles.SaveRequest(request, path);
        RequestFiles.SaveTalismans(talismans, RequestFiles.TalismanPathFor(request, path)!);
        RequestFiles.SaveBuildsFor(builds, path);
        return new ConfigDto(name, request, talismans, builds, File.Exists(ResultsJsonPath(name)));
    }

    public bool Delete(string name)
    {
        var any = false;
        foreach (var p in new[] { RequestPath(name), Path.Combine(Directory, name + RequestFiles.TalismanFileSuffix), RequestFiles.BuildsPathFor(RequestPath(name)), ResultsJsonPath(name), ResultsTextPath(name) })
        {
            if (!File.Exists(p)) continue;
            File.Delete(p);
            any = true;
        }
        return any;
    }

    public void SaveResults(string name, string text, ResultDto results)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(ResultsTextPath(name), text);
        File.WriteAllText(ResultsJsonPath(name), JsonSerializer.Serialize(results, ApiJson.Options));
    }

    public ResultDto? LoadResults(string name)
    {
        var path = ResultsJsonPath(name);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ResultDto>(stream, ApiJson.Options);
    }

    public string? ResultsText(string name)
    {
        var path = ResultsTextPath(name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static bool IsJsonArray(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            int ch;
            while ((ch = reader.Read()) >= 0)
                if (!char.IsWhiteSpace((char)ch)) return ch == '[';
            return false;
        }
        catch (IOException) { return false; }
    }

    private string RequestPath(string name) => Path.Combine(Directory, name + ".json");
    private string ResultsJsonPath(string name) => Path.Combine(Directory, name + RequestFiles.ResultsFileSuffix);
    private string ResultsTextPath(string name) => Path.Combine(Directory, name + ".results.txt");

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]{0,79}$")]
    private static partial Regex NamePattern();
}
