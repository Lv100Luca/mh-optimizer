using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Web.Api;

public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/catalog", (Catalog catalog) => catalog);

        // ---- profiles: account-wide talisman pool and per-weapon-type condition presets
        api.MapGet("/profiles", (ProfileStore store) => store.ListProfiles());

        api.MapGet("/profiles/{profile}", (string profile, ProfileStore store) =>
            !ProfileStore.IsValidName(profile) ? BadName()
            : store.LoadProfile(profile) is { } p ? Results.Ok(p)
            : Results.NotFound());

        api.MapPost("/profiles/{profile}", (string profile, ProfileStore store) =>
            !ProfileStore.IsValidName(profile) ? BadName() : Results.Ok(store.CreateProfile(profile)));

        api.MapPut("/profiles/{profile}/talismans", (string profile, TalismansPayload payload, ProfileStore store) =>
            !ProfileStore.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SaveTalismans(profile, payload.Talismans, payload.Renames)));

        // Saves a weapon type's preset; saved weapons of that type follow it except where they override it.
        api.MapPut("/profiles/{profile}/presets/{weaponKind}", (string profile, string weaponKind, Conditions conditions, ProfileStore store) =>
            !ProfileStore.IsValidName(profile) || !ProfileStore.IsValidName(weaponKind) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SavePreset(profile, weaponKind, conditions)));

        // Every weapon with its picked build (scored now) and the best build of its last run.
        api.MapGet("/profiles/{profile}/inventory", (string profile, ProfileStore store, GameData data) =>
            !ProfileStore.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(Inventory.Build(store, profile, data)));

        // ---- weapons of a profile
        api.MapGet("/profiles/{profile}/weapons", (string profile, ProfileStore store) =>
            !ProfileStore.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.ListWeapons(profile)));

        api.MapGet("/profiles/{profile}/weapons/{name}", (string profile, string name, ProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.LoadWeapon(profile, name) is { } weapon ? Results.Ok(weapon)
            : Results.NotFound());

        api.MapPut("/profiles/{profile}/weapons/{name}", (string profile, string name, WeaponPayload payload, ProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SaveWeapon(profile, name, payload.Request, payload.Builds ?? [])));

        api.MapDelete("/profiles/{profile}/weapons/{name}", (string profile, string name, ProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.DeleteWeapon(profile, name) ? Results.NoContent()
            : Results.NotFound());

        api.MapGet("/profiles/{profile}/weapons/{name}/results", (string profile, string name, ProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.LoadResults(profile, name) is { } results ? Results.Ok(results)
            : Results.NotFound());

        api.MapGet("/profiles/{profile}/weapons/{name}/results.txt", (string profile, string name, ProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.ResultsText(profile, name) is { } text ? Results.Text(text, "text/plain; charset=utf-8")
            : Results.NotFound());

        api.MapPost("/resolve", (ConfigPayload payload, GameData data, ProfileStore store) =>
            Resolving.Describe(Resolving.Resolve(payload, data, store.Directory), payload.Request.TargetSkills, data));

        // Scores the payload's hand-entered builds with the request's weapon and conditions.
        api.MapPost("/evaluate", (ConfigPayload payload, GameData data, ProfileStore store) =>
            BuildEvaluation.Evaluate(payload, Resolving.Resolve(payload, data, store.Directory), data));

        // Server-sent events: "validation" {errors, warnings}, then "progress" {message}..., then "result" ResultDto or "error" {message}.
        // ?profile=&save= stores the result as that weapon's last run.
        api.MapPost("/optimize", (ConfigPayload payload, string? profile, string? save, GameData data, ProfileStore store, HttpContext http) =>
            TypedResults.ServerSentEvents(Optimize(payload, profile, save, data, store, http.RequestAborted)));
    }

    private static async IAsyncEnumerable<SseItem<string>> Optimize(
        ConfigPayload payload, string? profile, string? save, GameData data, ProfileStore store, [EnumeratorCancellation] CancellationToken ct)
    {
        var resolved = Resolving.Resolve(payload, data, store.Directory);
        yield return Event("validation", new { errors = resolved.Errors, warnings = resolved.Warnings });
        if (!resolved.IsValid)
        {
            yield return Event("error", new { message = "The request has errors; fix them and run again." });
            yield break;
        }

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        var progress = new ChannelProgress(channel.Writer);
        var run = Task.Run(() => new Optimizer(data, resolved).Run(progress, ct), ct);
        _ = run.ContinueWith(_ => channel.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var line in channel.Reader.ReadAllAsync(ct))
            yield return Event("progress", new { message = line });

        OptimizationResult? result = null;
        string? error = null;
        var cancelled = false;
        try { result = await run; }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception e) { error = e.Message; }

        if (cancelled) yield break;
        if (result is null)
        {
            yield return Event("error", new { message = error ?? "The optimizer failed." });
            yield break;
        }

        var dto = ResultMapper.Map(result, resolved, data) with { InputsHash = ProfileStore.InputsHash(payload.Request, payload.Talismans ?? []) };
        if (profile is { Length: > 0 } && save is { Length: > 0 } && ValidNames(profile, save) && store.ProfileExists(profile))
        {
            string? saveError = null;
            try { store.SaveResults(profile, save, dto.Text, dto); }
            catch (IOException e) { saveError = e.Message; }
            if (saveError is not null) yield return Event("progress", new { message = "Could not save the results: " + saveError });
        }
        yield return Event("result", dto);
    }

    private static bool ValidNames(string profile, string name) => ProfileStore.IsValidName(profile) && ProfileStore.IsValidName(name);

    private static IResult BadName() => Results.BadRequest(new { message = "Use letters, digits, spaces, '-', '_' or '.' for names." });

    private static SseItem<string> Event(string type, object payload) =>
        new(JsonSerializer.Serialize(payload, ApiJson.Options), type);

    private sealed class ChannelProgress(ChannelWriter<string> writer) : IProgress<string>
    {
        public void Report(string value) => writer.TryWrite(value);
    }
}
