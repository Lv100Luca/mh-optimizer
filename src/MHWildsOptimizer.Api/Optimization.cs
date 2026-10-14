using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Api;

/// <summary>What an optimizer run reports, in order: validation, progress lines, then a result or an error.</summary>
public abstract record OptimizeEvent;

public sealed record ValidationEvent(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) : OptimizeEvent;

public sealed record ProgressEvent(string Message) : OptimizeEvent;

/// <summary>Where the run is, as one line for people: "Weapon start 2 of 3 (Element focus) · armor search 1 (at most 4) · build 3 of 5".</summary>
public sealed record StageEvent(string Stage) : OptimizeEvent;

public sealed record ResultEvent(ResultDto Result) : OptimizeEvent;

public sealed record ErrorEvent(string Message) : OptimizeEvent;

public static class Optimization
{
    /// <summary>
    /// Validates the payload, runs the optimizer and maps the result; with <paramref name="profile"/> and
    /// <paramref name="save"/> the result becomes that weapon's last run. Cancelling throws <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="cpSat">Where CP-SAT solves run (null = the native library).</param>
    /// <param name="engine">Runs with this engine whatever the request says (the browser has only CP-SAT); the saved result's
    /// inputs hash stays that of the request as saved, so the run does not look stale.</param>
    public static async Task<ResultDto?> RunAsync(ConfigPayload payload, string? profile, string? save, GameData data, IProfileStore store,
        ICpSatBackend? cpSat, Action<OptimizeEvent> onEvent, CancellationToken ct, OptimizerEngine? engine = null)
    {
        var request = engine is { } forced ? payload.Request with { Options = payload.Request.Options with { Engine = forced } } : payload.Request;
        var resolved = Resolving.Resolve(payload with { Request = request }, data, store.Directory);
        onEvent(new ValidationEvent(resolved.Errors, resolved.Warnings));
        if (!resolved.IsValid)
        {
            onEvent(new ErrorEvent("The request has errors; fix them and run again."));
            return null;
        }

        OptimizationResult result;
        try
        {
            result = await new Optimizer(data, resolved, cpSat) { Stage = s => onEvent(new StageEvent(s)) }
                .RunAsync(new CallbackProgress(m => onEvent(new ProgressEvent(m))), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            onEvent(new ErrorEvent(e.Message));
            return null;
        }

        // in the browser the search's last steps, mapping the result and showing it share the page's thread (100-300 ms
        // each): in one stretch they froze the page for about half a second
        if (cpSat is not null) await cpSat.YieldAsync();
        var dto = ResultMapper.Map(result, resolved, data) with { InputsHash = ProfileRules.InputsHash(payload.Request, payload.Talismans ?? []) };
        if (profile is { Length: > 0 } && save is { Length: > 0 } && ProfileRules.IsValidName(profile) && ProfileRules.IsValidName(save) && store.ProfileExists(profile))
        {
            try { store.SaveResults(profile, save, dto.Text, dto); }
            catch (Exception e) when (e is IOException or InvalidOperationException)
            {
                onEvent(new ProgressEvent("Could not save the results: " + e.Message));
            }
        }
        if (cpSat is not null) await cpSat.YieldAsync();
        onEvent(new ResultEvent(dto));
        return dto;
    }

    /// <summary>Reports right away on the reporting thread (<see cref="Progress{T}"/> would post through the synchronization context).</summary>
    private sealed class CallbackProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
