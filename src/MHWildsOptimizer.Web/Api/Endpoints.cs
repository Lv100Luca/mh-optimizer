using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;

namespace MHWildsOptimizer.Web.Api;

public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/catalog", (Catalog catalog) => catalog);

        // ---- profiles: account-wide talisman pool and per-weapon-type condition presets
        api.MapGet("/profiles", (IProfileStore store) => store.ListProfiles());

        api.MapGet("/profiles/{profile}", (string profile, IProfileStore store) =>
            !ProfileRules.IsValidName(profile) ? BadName()
            : store.LoadProfile(profile) is { } p ? Results.Ok(p)
            : Results.NotFound());

        api.MapPost("/profiles/{profile}", (string profile, IProfileStore store) =>
            !ProfileRules.IsValidName(profile) ? BadName() : Results.Ok(store.CreateProfile(profile)));

        api.MapPut("/profiles/{profile}/talismans", (string profile, TalismansPayload payload, IProfileStore store) =>
            !ProfileRules.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SaveTalismans(profile, payload.Talismans, payload.Renames)));

        // Saves a weapon type's preset; saved weapons of that type follow it except where they override it.
        api.MapPut("/profiles/{profile}/presets/{weaponKind}", (string profile, string weaponKind, Conditions conditions, IProfileStore store) =>
            !ProfileRules.IsValidName(profile) || !ProfileRules.IsValidName(weaponKind) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SavePreset(profile, weaponKind, conditions)));

        // Every weapon with its picked build (scored now) and the best build of its last run.
        api.MapGet("/profiles/{profile}/inventory", (string profile, IProfileStore store, GameData data) =>
            !ProfileRules.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(Inventory.Build(store, profile, data)));

        // ---- weapons of a profile
        api.MapGet("/profiles/{profile}/weapons", (string profile, IProfileStore store) =>
            !ProfileRules.IsValidName(profile) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.ListWeapons(profile)));

        api.MapGet("/profiles/{profile}/weapons/{name}", (string profile, string name, IProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.LoadWeapon(profile, name) is { } weapon ? Results.Ok(weapon)
            : Results.NotFound());

        api.MapPut("/profiles/{profile}/weapons/{name}", (string profile, string name, WeaponPayload payload, IProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : !store.ProfileExists(profile) ? Results.NotFound()
            : Results.Ok(store.SaveWeapon(profile, name, payload.Request, payload.Builds ?? [])));

        api.MapDelete("/profiles/{profile}/weapons/{name}", (string profile, string name, IProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.DeleteWeapon(profile, name) ? Results.NoContent()
            : Results.NotFound());

        api.MapGet("/profiles/{profile}/weapons/{name}/results", (string profile, string name, IProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.LoadResults(profile, name) is { } results ? Results.Ok(results)
            : Results.NotFound());

        api.MapGet("/profiles/{profile}/weapons/{name}/results.txt", (string profile, string name, IProfileStore store) =>
            !ValidNames(profile, name) ? BadName()
            : store.ResultsText(profile, name) is { } text ? Results.Text(text, "text/plain; charset=utf-8")
            : Results.NotFound());

        api.MapPost("/resolve", (ConfigPayload payload, GameData data, IProfileStore store) =>
            Resolving.Describe(Resolving.Resolve(payload, data, store.Directory), payload.Request.TargetSkills, data));

        // Scores the payload's hand-entered builds with the request's weapon and conditions.
        api.MapPost("/evaluate", (ConfigPayload payload, GameData data, IProfileStore store) =>
            BuildEvaluation.Evaluate(payload, Resolving.Resolve(payload, data, store.Directory), data));

        // Server-sent events: "validation" {errors, warnings}, then "progress" {message}..., then "result" ResultDto or "error" {message}.
        // ?profile=&save= stores the result as that weapon's last run.
        api.MapPost("/optimize", (ConfigPayload payload, string? profile, string? save, GameData data, IProfileStore store, HttpContext http) =>
            TypedResults.ServerSentEvents(Optimize(payload, profile, save, data, store, http.RequestAborted)));
    }

    private static async IAsyncEnumerable<SseItem<string>> Optimize(
        ConfigPayload payload, string? profile, string? save, GameData data, IProfileStore store, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<OptimizeEvent>(new UnboundedChannelOptions { SingleReader = true });
        // the beam engine runs synchronously: keep it off the request thread
        var run = Task.Run(() => Optimization.RunAsync(payload, profile, save, data, store, null, e => channel.Writer.TryWrite(e), ct), ct);
        _ = run.ContinueWith(_ => channel.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var e in channel.Reader.ReadAllAsync(ct))
            yield return e switch
            {
                ValidationEvent v => Event("validation", new { errors = v.Errors, warnings = v.Warnings }),
                ProgressEvent p => Event("progress", new { message = p.Message }),
                ResultEvent r => Event("result", r.Result),
                ErrorEvent x => Event("error", new { message = x.Message }),
                _ => throw new InvalidOperationException($"Unknown event {e}"),
            };

        string? error = null;
        try { await run; }
        catch (OperationCanceledException) { yield break; }
        catch (Exception e) { error = e.Message; }
        if (error is not null) yield return Event("error", new { message = error });
    }

    private static bool ValidNames(string profile, string name) => ProfileRules.IsValidName(profile) && ProfileRules.IsValidName(name);

    private static IResult BadName() => Results.BadRequest(new { message = ProfileRules.NameRules });

    private static SseItem<string> Event(string type, object payload) =>
        new(JsonSerializer.Serialize(payload, ApiJson.Options), type);
}
