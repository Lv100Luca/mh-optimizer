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
            s.Name, s.Description, s.WeaponOnly ? "weapon only" : null, s.GogmaPool ? "gogma" : null,
            .. s.Ranks.Select(r => $"{r.Name} {r.Description} {r.Game}"),
            s.Effects?.Trigger, .. s.Effects?.Levels.Values.SelectMany(l => l) ?? [], .. s.Effects?.Notes ?? [],
        ]).ToLowerInvariant());
        SetText = data.ArmorSets.ToDictionary(s => s.Name, s => string.Join('\n', [
            s.Name, s.GroupSkill, .. s.SetBonus, .. s.Pieces.Select(p => p.Name), .. s.Pieces.SelectMany(p => p.Skills).Select(k => k.Skill),
        ]).ToLowerInvariant());
        Items = (data.Items ?? []).ToDictionary(i => i.Name);
        ItemText = Items.Values.ToDictionary(i => i.Name, i => string.Join('\n', [
            i.Name, i.Description, i.Boost, i.Stacking, .. i.Effects, .. i.Notes ?? [], .. i.Recipes?.SelectMany(r => r) ?? [],
            (data.ItemCategories ?? []).FirstOrDefault(c => c.Key == i.Category)?.Title, SourceSummary(i),
            .. i.Sources?.Gather?.Select(g => g.Region) ?? [], .. i.Sources?.Retrieval ?? [], .. i.Sources?.Monsters?.Select(m => m.Monster) ?? [],
        ]).ToLowerInvariant());
        MealSkills = (data.Meals?.Skills ?? []).ToDictionary(m => m.Name);
        MealText = MealSkills.Values.ToDictionary(m => m.Name, m => string.Join('\n', [
            m.Name, m.Description, .. m.Effects, .. m.Notes ?? [],
        ]).ToLowerInvariant());
    }

    public WikiData Data { get; }
    public IReadOnlyDictionary<string, WikiSkill> Skills { get; }
    public IReadOnlyDictionary<string, WikiArmorSet> Sets { get; }

    /// <summary>Lower-case text a search matches: names, game texts, effect lines, "weapon only" and "gogma" for those.</summary>
    public IReadOnlyDictionary<string, string> SkillText { get; }

    /// <summary>Lower-case text a search matches: set, piece, skill and bonus names.</summary>
    public IReadOnlyDictionary<string, string> SetText { get; }

    public IReadOnlyDictionary<string, WikiItem> Items { get; }

    /// <summary>Lower-case text a search matches: name, game text, effects, ingredients, category, where it comes from.</summary>
    public IReadOnlyDictionary<string, string> ItemText { get; }

    /// <summary>Where an item comes from in a few words: "Combine · gather · support ship".</summary>
    public static string SourceSummary(WikiItem item)
    {
        var s = item.Sources;
        string?[] parts =
        [
            s?.Other is not null ? "quest or cooking" : null,
            item.Recipes is { Count: > 0 } ? "combine" : null,
            s?.Gather is not null ? "gather" : null,
            s?.Retrieval is not null ? "material retrieval" : null,
            s?.Shop == true ? "item shop" : null,
            s?.SupportShip == true ? "support ship" : null,
            s?.Monsters is { } monsters ? string.Join(", ", monsters.Select(m => m.Monster).Distinct()) : null,
        ];
        var text = string.Join(" · ", parts.OfType<string>());
        return text.Length == 0 ? "" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public IReadOnlyDictionary<string, WikiMealSkill> MealSkills { get; }

    /// <summary>Lower-case text a search matches: name, game text, effects.</summary>
    public IReadOnlyDictionary<string, string> MealText { get; }
}
