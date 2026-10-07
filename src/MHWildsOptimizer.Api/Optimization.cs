using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Api;

/// <summary>What an optimizer run reports, in order: validation, progress lines, then a result or an error.</summary>
public abstract record OptimizeEvent;

public sealed record ValidationEvent(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) : OptimizeEvent;

public sealed record ProgressEvent(string Message) : OptimizeEvent;

public sealed record ResultEvent(ResultDto Result) : OptimizeEvent;

public sealed record ErrorEvent(string Message) : OptimizeEvent;

public static class Optimization
{
    /// <summary>
    /// Validates the payload, runs the optimizer and maps the result; with <paramref name="profile"/> and
    /// <paramref name="save"/> the result becomes that weapon's last run. Cancelling throws <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="cpSat">Where CP-SAT solves run (null = the native library).</param>
    public static async Task<ResultDto?> RunAsync(ConfigPayload payload, string? profile, string? save, GameData data, IProfileStore store,
        ICpSatBackend? cpSat, Action<OptimizeEvent> onEvent, CancellationToken ct)
    {
        var resolved = Resolving.Resolve(payload, data, store.Directory);
        onEvent(new ValidationEvent(resolved.Errors, resolved.Warnings));
        if (!resolved.IsValid)
        {
            onEvent(new ErrorEvent("The request has errors; fix them and run again."));
            return null;
        }

        OptimizationResult result;
        try
        {
            result = await new Optimizer(data, resolved, cpSat).RunAsync(new CallbackProgress(m => onEvent(new ProgressEvent(m))), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            onEvent(new ErrorEvent(e.Message));
            return null;
        }

        var dto = ResultMapper.Map(result, resolved, data) with { InputsHash = ProfileRules.InputsHash(payload.Request, payload.Talismans ?? []) };
        if (profile is { Length: > 0 } && save is { Length: > 0 } && ProfileRules.IsValidName(profile) && ProfileRules.IsValidName(save) && store.ProfileExists(profile))
        {
            try { store.SaveResults(profile, save, dto.Text, dto); }
            catch (Exception e) when (e is IOException or InvalidOperationException)
            {
                onEvent(new ProgressEvent("Could not save the results: " + e.Message));
            }
        }
        onEvent(new ResultEvent(dto));
        return dto;
    }

    /// <summary>Reports right away on the reporting thread (<see cref="Progress{T}"/> would post through the synchronization context).</summary>
    private sealed class CallbackProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
