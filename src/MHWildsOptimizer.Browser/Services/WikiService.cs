using System.Net.Http.Json;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// The wiki's data (data/wiki.json), fetched the first time a wiki page opens rather than at start-up: the optimizer does not
/// need it, and it is the biggest file of the dataset.
/// </summary>
public sealed class WikiService(HttpClient http)
{
    private Task<WikiIndex>? _load;

    public WikiIndex? Loaded { get; private set; }

    public Task<WikiIndex> LoadAsync() => _load ??= FetchAsync();

    private async Task<WikiIndex> FetchAsync()
    {
        try
        {
            var data = await http.GetFromJsonAsync<WikiData>("data/wiki.json", GameDataLoader.JsonOptions)
                       ?? throw new InvalidDataException("data/wiki.json is empty.");
            return Loaded = new WikiIndex(data);
        }
        catch
        {
            _load = null; // let the next visit try again
            throw;
        }
    }
}

/// <summary>Lookups and search text over the wiki data.</summary>
public sealed class WikiIndex
{
    public WikiIndex(WikiData data)
    {
        Data = data;
        Skills = data.Skills.ToDictionary(s => s.Name);
        Sets = data.ArmorSets.ToDictionary(s => s.Name);
        SkillText = data.Skills.ToDictionary(s => s.Name, s => string.Join('\n', [
            s.Name, s.Description, .. s.Ranks.Select(r => $"{r.Name} {r.Description}"),
            s.Effects?.Trigger, .. s.Effects?.Levels.Values.SelectMany(l => l) ?? [], .. s.Effects?.Notes ?? [],
        ]).ToLowerInvariant());
        SetText = data.ArmorSets.ToDictionary(s => s.Name, s => string.Join('\n', [
            s.Name, s.GroupSkill, .. s.SetBonus, .. s.Pieces.Select(p => p.Name), .. s.Pieces.SelectMany(p => p.Skills).Select(k => k.Skill),
        ]).ToLowerInvariant());
    }

    public WikiData Data { get; }
    public IReadOnlyDictionary<string, WikiSkill> Skills { get; }
    public IReadOnlyDictionary<string, WikiArmorSet> Sets { get; }

    /// <summary>Lower-case text a search matches: names, game texts and effect lines.</summary>
    public IReadOnlyDictionary<string, string> SkillText { get; }

    /// <summary>Lower-case text a search matches: set, piece, skill and bonus names.</summary>
    public IReadOnlyDictionary<string, string> SetText { get; }
}
