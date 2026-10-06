using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Web.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MHWildsOptimizer.Web.Tests;

/// <summary>Hosts the web app in-process with the real dataset and a temporary inputs directory.</summary>
public sealed class WebFixture : WebApplicationFactory<Program>, IDisposable
{
    public string InputsDirectory { get; } = Path.Combine(Path.GetTempPath(), "mhwilds-web-tests-" + Guid.NewGuid().ToString("N"));

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Directory.CreateDirectory(InputsDirectory);
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paths:Data"] = GameDataLoader.FindDataDirectory(),
            ["Paths:Inputs"] = InputsDirectory,
        }));
        return base.CreateHost(builder);
    }

    public HttpClient Client => CreateClient();

    public new void Dispose()
    {
        base.Dispose();
        try { Directory.Delete(InputsDirectory, recursive: true); } catch (IOException) { }
    }
}

public class ApiTests : IClassFixture<WebFixture>
{
    private readonly WebFixture _fixture;
    private readonly JsonSerializerOptions _json = ApiJson.Options;

    public ApiTests(WebFixture fixture) => _fixture = fixture;

    private static string RepoInputs => Path.Combine(Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName, "inputs");

    private static ConfigPayload ExamplePayload()
    {
        var path = Path.Combine(RepoInputs, "request.example.json");
        var request = RequestLoader.Read(path);
        var talismans = RequestFiles.LoadTalismansFor(request, path);
        return new ConfigPayload(request, talismans);
    }

    [Fact]
    public async Task Catalog_lists_skills_with_icons_and_weapon_types()
    {
        var catalog = await _fixture.Client.GetFromJsonAsync<JsonElement>("/api/catalog", _json);
        var skills = catalog.GetProperty("skills");
        Assert.True(skills.GetArrayLength() >= 170);
        Assert.All(skills.EnumerateArray(), s => Assert.False(string.IsNullOrEmpty(s.GetProperty("icon").GetString())));

        var types = catalog.GetProperty("weapon_types").EnumerateArray().ToList();
        Assert.Equal(14, types.Count);
        var gs = types.Single(t => t.GetProperty("kind").GetString() == "great-sword");
        Assert.True(gs.GetProperty("supported").GetBoolean());
        Assert.Equal("Focus", gs.GetProperty("core_skills")[0].GetProperty("skill").GetString());
        Assert.True(gs.GetProperty("variants").TryGetProperty("attack", out _));

        Assert.Equal(294, catalog.GetProperty("skill_pool").GetProperty("pairs").GetArrayLength());
        Assert.True(catalog.GetProperty("conditions").GetArrayLength() >= 25);
        Assert.True(catalog.GetProperty("decorations").EnumerateArray().All(d => d.GetProperty("icon_color").GetString() is { Length: > 0 }));
    }

    [Fact]
    public async Task Resolve_accepts_the_example_request()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/api/resolve", ExamplePayload(), _json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ResolveDto>(_json);
        Assert.NotNull(dto);
        Assert.True(dto.IsValid, string.Join("; ", dto.Errors));
        Assert.NotNull(dto.Weapon);
        Assert.Equal("great-sword", dto.Weapon.Type);
        Assert.Contains(dto.Targets, t => t.Skill == "Focus" && t.FromCore);
        Assert.Equal(2, dto.Talismans.Random);
        Assert.NotNull(dto.Baseline);
        Assert.True(dto.Baseline.Total > 0);
    }

    [Fact]
    public async Task Catalog_carries_attack_profile_presets_and_the_proc_damage_toggle()
    {
        var catalog = await _fixture.Client.GetFromJsonAsync<JsonElement>("/api/catalog", _json);
        var gs = catalog.GetProperty("weapon_types").EnumerateArray().Single(t => t.GetProperty("kind").GetString() == "great-sword");
        var preset = gs.GetProperty("attack_profile");
        Assert.Equal(AttackProfile.Preset(WeaponType.GreatSword).HitsPerMinute, preset.GetProperty("hits_per_minute").GetDouble());
        Assert.Equal(AttackProfile.Preset(WeaponType.GreatSword).ChargedLv3Share, preset.GetProperty("charged_lv3_share").GetDouble());

        var proc = Assert.Single(catalog.GetProperty("conditions").EnumerateArray(), c => c.GetProperty("key").GetString() == "proc_damage");
        Assert.Equal(ConditionCatalog.GroupProcs, proc.GetProperty("group").GetString());
        var conditions = catalog.GetProperty("default_request").GetProperty("conditions");
        Assert.True(conditions.GetProperty("proc_damage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, conditions.GetProperty("attack_profile").GetProperty("hits_per_minute").ValueKind);
    }

    [Fact]
    public async Task Resolve_accepts_set_bonus_and_group_skill_targets()
    {
        var payload = ExamplePayload();
        payload = payload with
        {
            Request = payload.Request with
            {
                TargetSkills = new Dictionary<string, int> { ["Weakness Exploit"] = 3, ["Gore Magala's Tyranny"] = 2, ["Lord's Soul"] = 1 },
            },
        };
        var dto = await (await _fixture.Client.PostAsJsonAsync("/api/resolve", payload, _json)).Content.ReadFromJsonAsync<ResolveDto>(_json);
        Assert.NotNull(dto);
        Assert.True(dto.IsValid, string.Join("; ", dto.Errors));

        var set = Assert.Single(dto.Targets, t => t.Skill == "Gore Magala's Tyranny");
        Assert.Equal(SkillKind.Set, set.Kind);
        Assert.Equal(2, set.Level);
        Assert.Equal(4, set.Pieces);
        Assert.Equal("Gore Magala's Tyranny II (4 pieces)", set.Label);

        var group = Assert.Single(dto.Targets, t => t.Skill == "Lord's Soul");
        Assert.Equal(SkillKind.Group, group.Kind);
        Assert.Equal(3, group.Pieces);
        Assert.Equal("Lord's Soul (3 pieces)", group.Label);

        var skill = Assert.Single(dto.Targets, t => t.Skill == "Weakness Exploit");
        Assert.Equal(SkillKind.Armor, skill.Kind);
        Assert.Null(skill.Pieces);

        // required bonuses are tracked by the search even when they add nothing to the score
        Assert.NotNull(dto.Relevance);
        Assert.Contains("Gore Magala's Tyranny", dto.Relevance.SetBonuses);
        Assert.Contains("Lord's Soul", dto.Relevance.GroupSkills);
    }

    [Fact]
    public async Task Resolve_rejects_an_invalid_attack_profile()
    {
        var payload = ExamplePayload();
        payload = payload with
        {
            Request = payload.Request with
            {
                Conditions = payload.Request.Conditions with { AttackProfile = new AttackProfile { HitsPerMinute = 0, ChargedLv3Share = 1.5 } },
            },
        };
        var dto = await (await _fixture.Client.PostAsJsonAsync("/api/resolve", payload, _json)).Content.ReadFromJsonAsync<ResolveDto>(_json);
        Assert.NotNull(dto);
        Assert.False(dto.IsValid);
        Assert.Contains(dto.Errors, e => e.Contains("hits_per_minute"));
        Assert.Contains(dto.Errors, e => e.Contains("charged_lv3_share"));
    }

    [Fact]
    public async Task Resolve_reports_errors_for_unknown_skills()
    {
        var payload = ExamplePayload();
        payload = payload with { Request = payload.Request with { TargetSkills = new Dictionary<string, int> { ["Not A Skill"] = 1 } } };
        var dto = await (await _fixture.Client.PostAsJsonAsync("/api/resolve", payload, _json)).Content.ReadFromJsonAsync<ResolveDto>(_json);
        Assert.NotNull(dto);
        Assert.False(dto.IsValid);
        Assert.Contains(dto.Errors, e => e.Contains("Not A Skill"));
    }

    [Fact]
    public async Task Configs_round_trip_through_the_store()
    {
        var client = _fixture.Client;
        var payload = ExamplePayload();

        var put = await client.PutAsJsonAsync("/api/configs/roundtrip", payload, _json);
        put.EnsureSuccessStatusCode();
        Assert.True(File.Exists(Path.Combine(_fixture.InputsDirectory, "roundtrip.json")));
        Assert.True(File.Exists(Path.Combine(_fixture.InputsDirectory, "roundtrip.talismans.json")));

        var list = await client.GetFromJsonAsync<List<ConfigSummaryDto>>("/api/configs", _json);
        Assert.NotNull(list);
        Assert.Contains(list, c => c.Name == "roundtrip");

        var loaded = await client.GetFromJsonAsync<ConfigDto>("/api/configs/roundtrip", _json);
        Assert.NotNull(loaded);
        Assert.Equal(payload.Request.TargetSkills, loaded.Request.TargetSkills);
        Assert.Equal(2, loaded.Talismans.Count);
        Assert.Equal("roundtrip.talismans.json", loaded.Request.Talismans.File);

        var delete = await client.DeleteAsync("/api/configs/roundtrip");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/configs/roundtrip")).StatusCode);
    }

    [Fact]
    public async Task Invalid_config_names_are_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _fixture.Client.GetAsync("/api/configs/..%2Fsecret")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _fixture.Client.PutAsJsonAsync("/api/configs/a%2Fb", ExamplePayload(), _json)).StatusCode);
    }

    [Fact]
    public async Task Optimize_streams_progress_and_a_result()
    {
        // Small, fast search: fixed pair, one target, tiny beam.
        var payload = ExamplePayload();
        payload = payload with
        {
            Request = payload.Request with
            {
                TargetSkills = new Dictionary<string, int> { ["Weakness Exploit"] = 3 },
                Options = payload.Request.Options with { TopN = 2, MaxStatesPerDepth = 1000 },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/optimize?save=streamed") { Content = JsonContent.Create(payload, options: _json) };
        using var response = await _fixture.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        Assert.StartsWith("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var events = ParseSse(body);
        Assert.Contains(events, e => e.Type == "validation");
        Assert.Contains(events, e => e.Type == "progress");
        var result = Assert.Single(events, e => e.Type == "result");
        var dto = JsonSerializer.Deserialize<ResultDto>(result.Data, _json);
        Assert.NotNull(dto);
        var pair = Assert.Single(dto.Pairs);
        Assert.NotEmpty(pair.Builds);
        var best = pair.Builds[0];
        Assert.Equal(5, best.Armor.Count);
        Assert.Contains(best.Skills, s => s.Skill == "Weakness Exploit" && s.Level >= 3);
        Assert.Contains(best.Skills, s => s.Skill == "Focus" && s.Level >= 3);
        Assert.True(best.Score > 0);
        Assert.True(File.Exists(Path.Combine(_fixture.InputsDirectory, "streamed.results.txt")));

        var saved = await _fixture.Client.GetFromJsonAsync<ResultDto>("/api/configs/streamed/results", _json);
        Assert.NotNull(saved);
        Assert.Equal(dto.Pairs[0].BestScore, saved.Pairs[0].BestScore);
    }

    private static List<(string Type, string Data)> ParseSse(string body)
    {
        var events = new List<(string, string)>();
        string type = "message";
        var data = new List<string>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (data.Count > 0) events.Add((type, string.Join("\n", data)));
                type = "message";
                data.Clear();
            }
            else if (line.StartsWith("event:")) type = line[6..].Trim();
            else if (line.StartsWith("data:")) data.Add(line[5..].TrimStart());
        }
        if (data.Count > 0) events.Add((type, string.Join("\n", data)));
        return events;
    }
}
