using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Api;
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

    private static string ProfileDir(WebFixture f, string profile) => Path.Combine(f.InputsDirectory, "profiles", profile);

    private async Task CreateProfile(string profile, IReadOnlyList<TalismanInput> talismans)
    {
        (await _fixture.Client.PostAsync($"/api/profiles/{profile}", null)).EnsureSuccessStatusCode();
        (await _fixture.Client.PutAsJsonAsync($"/api/profiles/{profile}/talismans", new TalismansPayload([.. talismans]), _json)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Weapons_round_trip_through_the_profile_store()
    {
        var client = _fixture.Client;
        var payload = ExamplePayload();
        await CreateProfile("roundtrip", payload.Talismans!);

        var put = await client.PutAsJsonAsync("/api/profiles/roundtrip/weapons/gs", new WeaponPayload(payload.Request), _json);
        put.EnsureSuccessStatusCode();
        var dir = ProfileDir(_fixture, "roundtrip");
        Assert.True(File.Exists(Path.Combine(dir, "profile.json")));
        Assert.True(File.Exists(Path.Combine(dir, "talismans.json")));
        Assert.True(File.Exists(Path.Combine(dir, "weapons", "gs.json")));

        // the weapon file stays a plain request the CLI can run: its talismans come from the profile's pool
        var resolved = RequestLoader.Load(Path.Combine(dir, "weapons", "gs.json"), TestGameData());
        Assert.Equal(2, resolved.Talismans.Count(t => t.Source == TalismanSource.Random));

        var profiles = await client.GetFromJsonAsync<List<ProfileSummaryDto>>("/api/profiles", _json);
        Assert.Contains(profiles!, p => p.Name == "roundtrip" && p.Weapons == 1 && p.Talismans == 2);
        var list = await client.GetFromJsonAsync<List<WeaponSummaryDto>>("/api/profiles/roundtrip/weapons", _json);
        Assert.Equal("great-sword", Assert.Single(list!).Type);

        var loaded = await client.GetFromJsonAsync<WeaponDto>("/api/profiles/roundtrip/weapons/gs", _json);
        Assert.NotNull(loaded);
        Assert.Equal(payload.Request.TargetSkills, loaded.Request.TargetSkills);
        Assert.Equal(ProfileFiles.SharedTalismanReference, loaded.Request.Talismans.File);
        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/roundtrip", _json);
        Assert.Equal(2, profile!.Talismans.Count);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/profiles/roundtrip/weapons/gs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/profiles/roundtrip/weapons/gs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/profiles/nobody/weapons/gs", new WeaponPayload(payload.Request), _json)).StatusCode);
    }

    private static GameData TestGameData() => GameDataLoader.Load(GameDataLoader.FindDataDirectory());

    [Fact]
    public async Task Saving_a_preset_moves_the_weapons_of_that_type_but_keeps_their_overrides()
    {
        var client = _fixture.Client;
        var payload = ExamplePayload();
        await CreateProfile("presets", payload.Talismans!);
        var preset = Conditions.Default;
        (await client.PutAsJsonAsync("/api/profiles/presets/presets/great-sword", preset, _json)).EnsureSuccessStatusCode();

        var follows = payload.Request with { Conditions = preset with { } };
        var overrides = payload.Request with { Conditions = preset with { CounterstrikeActive = true } };
        (await client.PutAsJsonAsync("/api/profiles/presets/weapons/follows", new WeaponPayload(follows), _json)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/profiles/presets/weapons/overrides", new WeaponPayload(overrides), _json)).EnsureSuccessStatusCode();

        // the new preset turns on red health and counterstrike and turns off full health
        var next = preset with { RedHealth = true, FullHealth = false, CounterstrikeActive = true, SkillLimits = new() { ["Burst"] = 1 } };
        var response = await client.PutAsJsonAsync("/api/profiles/presets/presets/great-sword", next, _json);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<PresetSavedDto>(_json);
        Assert.Equal(["follows", "overrides"], saved!.UpdatedWeapons.Order().ToList());
        Assert.True(saved.Profile.ConditionPresets["great-sword"].RedHealth);

        var a = (await client.GetFromJsonAsync<WeaponDto>("/api/profiles/presets/weapons/follows", _json))!.Request.Conditions;
        Assert.True(ConditionPresets.Same(a, next));
        var b = (await client.GetFromJsonAsync<WeaponDto>("/api/profiles/presets/weapons/overrides", _json))!.Request.Conditions;
        Assert.True(b.RedHealth);
        Assert.False(b.FullHealth);
        Assert.Equal(1, b.SkillLimits["Burst"]);
        Assert.True(b.CounterstrikeActive); // its own override, which the preset now agrees with
        Assert.Empty(ConditionPresets.Overrides(b, next));
    }

    [Fact]
    public async Task Renaming_a_talisman_renames_it_in_the_builds_of_every_weapon()
    {
        var client = _fixture.Client;
        var payload = ExamplePayload();
        await CreateProfile("renames", payload.Talismans!);
        var build = WornBuild();
        (await client.PutAsJsonAsync("/api/profiles/renames/weapons/one", new WeaponPayload(payload.Request, [build]), _json)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/profiles/renames/weapons/two", new WeaponPayload(payload.Request, [build with { Name = "Other" }]), _json)).EnsureSuccessStatusCode();

        var renamed = payload.Talismans!.Select(t => t.Name == "Secret Charm" ? t with { Name = "My best charm" } : t).ToList();
        (await client.PutAsJsonAsync("/api/profiles/renames/talismans", new TalismansPayload(renamed, new() { ["Secret Charm"] = "My best charm" }), _json)).EnsureSuccessStatusCode();

        foreach (var weapon in new[] { "one", "two" })
        {
            var loaded = await client.GetFromJsonAsync<WeaponDto>($"/api/profiles/renames/weapons/{weapon}", _json);
            Assert.Equal("My best charm", Assert.Single(loaded!.Builds).Talisman!.Name);
        }
    }

    private static BuildInput WornBuild() => new()
    {
        Name = "Worn",
        WeaponDecorations = ["Attack Jewel III [3]", "Critical Jewel III [3]", null],
        Head = new BuildArmorInput { Piece = "Bale Burgeonet α", Decorations = ["Protection Jewel [1]", "Protection Jewel [1]"] },
        Chest = new BuildArmorInput { Piece = "Udra Miremail γ", Decorations = ["Challenger Jewel [3]", "Challenger Jewel [3]"] },
        Arms = new BuildArmorInput { Piece = "G. Fulgur Vambraces β", Transcended = true, Decorations = ["Chain Jewel [3]", "Tenderizer Jewel [3]"] },
        Waist = new BuildArmorInput { Piece = "Dahaad Shardcoil γ", Decorations = ["Protection Jewel [1]"] },
        Legs = new BuildArmorInput { Piece = "Udra Miregreaves γ", Decorations = ["Furor Jewel [2]"] },
        Talisman = new BuildTalismanInput { Name = "Secret Charm" },
    };

    [Fact]
    public async Task Evaluate_scores_hand_entered_builds_and_checks_the_targets()
    {
        var payload = ExamplePayload();
        payload = payload with
        {
            Request = payload.Request with { TargetSkills = new Dictionary<string, int> { ["Agitator"] = 5, ["Weakness Exploit"] = 5, ["Lord's Soul"] = 1 } },
            Builds = [WornBuild(), new BuildInput { Name = "Typo", Head = new BuildArmorInput { Piece = "No Such Helm" } }],
        };
        var response = await _fixture.Client.PostAsJsonAsync("/api/evaluate", payload, _json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<List<EvaluatedBuildDto>>(_json);
        Assert.NotNull(dto);
        Assert.Equal(2, dto.Count);

        var worn = dto[0];
        Assert.Empty(worn.Errors);
        Assert.NotNull(worn.Build);
        Assert.True(worn.Build.Score > 0);
        Assert.Equal(worn.Build.Efr + worn.Build.Efe + worn.Build.Procs, worn.Build.Score, 6);
        Assert.Equal(5, worn.Build.Armor.Count);
        Assert.True(worn.Build.Armor.Single(a => a.Kind == ArmorPieceKind.Arms).Transcended);
        Assert.StartsWith("=== Worn  -  EFR", worn.Build.Text);
        Assert.Contains(worn.Targets, t => t.Skill == "Agitator" && t.Met && t.Actual == 5);
        Assert.Contains(worn.Targets, t => t.Skill == "Weakness Exploit" && !t.Met && t.Actual == 1);
        Assert.Contains(worn.Targets, t => t.Skill == "Lord's Soul" && t.Kind == SkillKind.Group && t.Met && t.Required == 3);
        Assert.Contains(worn.Targets, t => t.Skill == "Focus" && t.FromCore && !t.Met);

        var typo = dto[1];
        Assert.Contains(typo.Errors, e => e.Contains("No Such Helm"));
        Assert.NotNull(typo.Build); // still scored (weapon only) so the editor keeps showing numbers
    }

    [Fact]
    public void Attack_breakdown_scores_the_build_on_every_attack_and_matches_the_score_on_its_own()
    {
        var data = TestGameData();
        var payload = ExamplePayload() with { Builds = [WornBuild()] };
        var resolved = Resolving.Resolve(payload, data, RepoInputs);
        var build = Assert.Single(BuildEvaluation.Evaluate(payload, resolved, data)).Build!;

        var rows = BuildEvaluation.AttackBreakdown(resolved, WornBuild(), data);
        Assert.Equal(Attacks.For(resolved.Weapon.Type).Count + Attacks.SequencePresetsFor(resolved.Weapon.Type).Count, rows.Count);
        var current = Assert.Single(rows, r => r.Current);
        Assert.Equal(build.Score, current.Score, 6);
        Assert.Equal(build.StatsRequested.DamagePerExecution, current.DamagePerExecution, 6);
        Assert.Contains(rows, r => r.Group == BuildEvaluation.GroupCombos);
        Assert.All(rows, r => Assert.True(r.DamagePerExecution > 0 && r.DamagePerMinute > 0, r.Name));
        // every attack's damage is its score (per 100 MV, raw-hitzone-100 scale) times its MV and the target's raw hitzone
        var hitzone = (resolved.Conditions.Target ?? new Target()).RawHitzone;
        Assert.All(rows, r => Assert.Equal(r.Score * r.Mv / 100 * hitzone / 100, r.DamagePerExecution, 6));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Attack_detail_adds_up_to_the_row_for_every_attack(bool bothHealthSides)
    {
        var data = TestGameData();
        var payload = ExamplePayload();
        if (bothHealthSides)
            payload = payload with { Request = payload.Request with { Conditions = payload.Request.Conditions with { FullHealth = true, RedHealth = true } } };
        var resolved = Resolving.Resolve(payload, data, RepoInputs);
        var rows = BuildEvaluation.AttackBreakdown(resolved, WornBuild(), data);
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var detail = BuildEvaluation.AttackDetail(resolved, WornBuild(), data, row.Id);
            Assert.NotNull(detail);
            Assert.Equal(row.DamagePerExecution, detail.Total, 6);
            Assert.All(detail.Steps.SelectMany(s => s.Hits), h =>
            {
                Assert.True(h.RawCrit >= h.Raw && h.ElementCrit >= h.Element, h.Name);
                Assert.InRange(h.Expected, h.Raw + h.Element - 1e-9, h.RawCrit + h.ElementCrit + 1e-9);
            });
            Assert.NotEmpty(detail.Breakdown);
        }
        // the example sequence loses Maximum Might after the tackle: that step says so and has less affinity
        var offset = BuildEvaluation.AttackDetail(resolved, WornBuild(), data, "preset:offset-into-tcs")!;
        var afterTackle = Assert.Single(offset.Steps, s => s.Conditions.Count > 0);
        Assert.False(afterTackle.Conditions["stamina_full"]);
        Assert.True(afterTackle.Affinity < offset.Steps[0].Affinity);
        Assert.Null(BuildEvaluation.AttackDetail(resolved, WornBuild(), data, "no-such-attack"));
    }

    [Fact]
    public void Attack_detail_lists_the_dark_arts_shockwave_under_each_lv3_charged_slash()
    {
        var data = TestGameData();
        var payload = ExamplePayload();
        // the weapon's rolled Soul of the Dark Knight plus the Bale helm: two pieces, Dark Arts
        payload = payload with
        {
            Request = payload.Request with
            {
                Weapon = payload.Request.Weapon with { SetBonus = "Soul of the Dark Knight" },
                Conditions = payload.Request.Conditions with { ProcDamage = true },
            },
        };
        var resolved = Resolving.Resolve(payload, data, RepoInputs);
        var detail = BuildEvaluation.AttackDetail(resolved, WornBuild(), data, "charge-combo")!;
        var hits = detail.Steps.Single().Hits;
        // Charged Slash, Strong Charged Slash and the True Charged Slash finisher are Lv3 charged slashes; the tackle and TCS 1 are not
        Assert.Equal(3, hits.Count(h => h.Shockwave));
        for (var i = 0; i < hits.Count; i++)
            if (hits[i].Shockwave) Assert.Contains("Lv3 charged slash", hits[i - 1].Notes);
        Assert.All(hits.Where(h => h.Shockwave), h => Assert.True(h.RawCrit > h.Raw && h.ElementCrit == h.Element));
        var row = BuildEvaluation.AttackBreakdown(resolved, WornBuild(), data).Single(r => r.Id == "charge-combo");
        Assert.Equal(row.DamagePerExecution, detail.Total, 6);

        var off = resolved with { Conditions = resolved.Conditions with { ProcDamage = false } };
        Assert.DoesNotContain(BuildEvaluation.AttackDetail(off, WornBuild(), data, "charge-combo")!.Steps.Single().Hits, h => h.Shockwave);
    }

    [Fact]
    public async Task Builds_round_trip_with_the_weapon()
    {
        var client = _fixture.Client;
        var payload = ExamplePayload();
        await CreateProfile("builds", payload.Talismans!);
        (await client.PutAsJsonAsync("/api/profiles/builds/weapons/withbuilds", new WeaponPayload(payload.Request, [WornBuild() with { Picked = true }]), _json)).EnsureSuccessStatusCode();
        var buildsFile = Path.Combine(ProfileDir(_fixture, "builds"), "weapons", "withbuilds.builds.json");
        Assert.True(File.Exists(buildsFile));

        var list = await client.GetFromJsonAsync<List<WeaponSummaryDto>>("/api/profiles/builds/weapons", _json);
        Assert.NotNull(list);
        Assert.Contains(list, c => c.Name == "withbuilds");
        Assert.DoesNotContain(list, c => c.Name.EndsWith(".builds"));

        var loaded = await client.GetFromJsonAsync<WeaponDto>("/api/profiles/builds/weapons/withbuilds", _json);
        Assert.NotNull(loaded);
        var build = Assert.Single(loaded.Builds);
        Assert.Equal("Worn", build.Name);
        Assert.True(build.Picked);
        Assert.Equal("G. Fulgur Vambraces β", build.Arms!.Piece);
        Assert.Equal(["Attack Jewel III [3]", "Critical Jewel III [3]", null], build.WeaponDecorations);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/profiles/builds/weapons/withbuilds")).StatusCode);
        Assert.False(File.Exists(buildsFile));
    }

    [Fact]
    public async Task Catalog_lists_charms_and_piece_rarities()
    {
        var catalog = await _fixture.Client.GetFromJsonAsync<JsonElement>("/api/catalog", _json);
        var charms = catalog.GetProperty("charms").EnumerateArray().ToList();
        Assert.Contains(charms, c => c.GetProperty("is_max_rank").GetBoolean());
        Assert.Contains(charms, c => !c.GetProperty("is_max_rank").GetBoolean());
        var fulgur = catalog.GetProperty("armor_sets").EnumerateArray().SelectMany(s => s.GetProperty("pieces").EnumerateArray())
            .Single(p => p.GetProperty("name").GetString() == "G. Fulgur Vambraces β");
        Assert.Equal(6, fulgur.GetProperty("rarity").GetInt32());
    }

    [Fact]
    public async Task Invalid_names_are_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _fixture.Client.GetAsync("/api/profiles/..%2Fsecret")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _fixture.Client.GetAsync("/api/profiles/ok/weapons/..%2Fsecret")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _fixture.Client.PutAsJsonAsync("/api/profiles/ok/weapons/a%2Fb", new WeaponPayload(ExamplePayload().Request), _json)).StatusCode);
    }

    [Fact]
    public async Task Optimize_streams_progress_and_a_result()
    {
        // Small, fast search: fixed pair, one target, tiny beam.
        var payload = ExamplePayload();
        await CreateProfile("runs", payload.Talismans!);
        payload = payload with
        {
            Request = payload.Request with
            {
                TargetSkills = new Dictionary<string, int> { ["Weakness Exploit"] = 3 },
                Options = payload.Request.Options with { TopN = 2, MaxStatesPerDepth = 1000 },
            },
        };

        (await _fixture.Client.PutAsJsonAsync("/api/profiles/runs/weapons/streamed", new WeaponPayload(payload.Request, [WornBuild() with { Picked = true }]), _json)).EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/optimize?profile=runs&save=streamed") { Content = JsonContent.Create(payload, options: _json) };
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
        Assert.True(File.Exists(Path.Combine(ProfileDir(_fixture, "runs"), "weapons", "streamed.results.txt")));

        var saved = await _fixture.Client.GetFromJsonAsync<ResultDto>("/api/profiles/runs/weapons/streamed/results", _json);
        Assert.NotNull(saved);
        Assert.Equal(dto.Pairs[0].BestScore, saved.Pairs[0].BestScore);
        Assert.NotNull(saved.InputsHash);

        // the inventory shows the run (fresh) and the picked build scored now
        var entry = Assert.Single((await _fixture.Client.GetFromJsonAsync<List<InventoryEntryDto>>("/api/profiles/runs/inventory", _json))!);
        Assert.Equal("streamed", entry.Name);
        Assert.False(entry.Stale);
        Assert.Equal(dto.Pairs[0].BestScore, entry.LastRun!.Build.Score, 6);
        Assert.Equal("Worn", entry.Picked!.Name);
        Assert.True(entry.Picked.Build!.Score > 0);

        // new talismans make the run stale
        (await _fixture.Client.PutAsJsonAsync("/api/profiles/runs/talismans", new TalismansPayload([.. payload.Talismans!.Take(1)]), _json)).EnsureSuccessStatusCode();
        entry = Assert.Single((await _fixture.Client.GetFromJsonAsync<List<InventoryEntryDto>>("/api/profiles/runs/inventory", _json))!);
        Assert.True(entry.Stale);
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
