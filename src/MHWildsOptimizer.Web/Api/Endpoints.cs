using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MHWildsOptimizer.Core.Build;
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

        api.MapGet("/configs", (ConfigStore store) => store.List());

        api.MapGet("/configs/{name}", (string name, ConfigStore store) =>
            !ConfigStore.IsValidName(name) ? Results.BadRequest(new { message = "Invalid configuration name." })
            : store.Load(name) is { } config ? Results.Ok(config)
            : Results.NotFound());

        api.MapPut("/configs/{name}", (string name, ConfigPayload payload, ConfigStore store) =>
            !ConfigStore.IsValidName(name) ? Results.BadRequest(new { message = "Use letters, digits, spaces, '-', '_' or '.' for the name." })
            : Results.Ok(store.Save(name, payload.Request, payload.Talismans ?? [], payload.Builds ?? [])));

        api.MapDelete("/configs/{name}", (string name, ConfigStore store) =>
            !ConfigStore.IsValidName(name) ? Results.BadRequest(new { message = "Invalid configuration name." })
            : store.Delete(name) ? Results.NoContent()
            : Results.NotFound());

        api.MapGet("/configs/{name}/results", (string name, ConfigStore store) =>
            !ConfigStore.IsValidName(name) ? Results.BadRequest(new { message = "Invalid configuration name." })
            : store.LoadResults(name) is { } results ? Results.Ok(results)
            : Results.NotFound());

        api.MapGet("/configs/{name}/results.txt", (string name, ConfigStore store) =>
            !ConfigStore.IsValidName(name) ? Results.BadRequest(new { message = "Invalid configuration name." })
            : store.ResultsText(name) is { } text ? Results.Text(text, "text/plain; charset=utf-8")
            : Results.NotFound());

        api.MapPost("/resolve", (ConfigPayload payload, GameData data, ConfigStore store) =>
            Resolving.Describe(Resolving.Resolve(payload, data, store.Directory), payload.Request.TargetSkills, data));

        // Scores the payload's hand-entered builds with the request's weapon and conditions.
        api.MapPost("/evaluate", (ConfigPayload payload, GameData data, ConfigStore store) =>
            BuildEvaluation.Evaluate(payload, Resolving.Resolve(payload, data, store.Directory), data));

        // Server-sent events: "validation" {errors, warnings}, then "progress" {message}..., then "result" ResultDto or "error" {message}.
        api.MapPost("/optimize", (ConfigPayload payload, string? save, GameData data, ConfigStore store, HttpContext http) =>
            TypedResults.ServerSentEvents(Optimize(payload, save, data, store, http.RequestAborted)));
    }

    private static async IAsyncEnumerable<SseItem<string>> Optimize(
        ConfigPayload payload, string? save, GameData data, ConfigStore store, [EnumeratorCancellation] CancellationToken ct)
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

        var dto = ResultMapper.Map(result, resolved, data);
        if (save is { Length: > 0 } && ConfigStore.IsValidName(save))
        {
            string? saveError = null;
            try { store.SaveResults(save, dto.Text, dto); }
            catch (IOException e) { saveError = e.Message; }
            if (saveError is not null) yield return Event("progress", new { message = "Could not save the results: " + saveError });
        }
        yield return Event("result", dto);
    }

    private static SseItem<string> Event(string type, object payload) =>
        new(JsonSerializer.Serialize(payload, ApiJson.Options), type);

    private sealed class ChannelProgress(ChannelWriter<string> writer) : IProgress<string>
    {
        public void Report(string value) => writer.TryWrite(value);
    }
}
