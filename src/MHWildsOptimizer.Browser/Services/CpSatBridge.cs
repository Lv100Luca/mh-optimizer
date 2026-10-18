using Google.OrTools.Sat;
using Google.Protobuf;
using MHWildsOptimizer.Core.Optimize;
using Microsoft.JSInterop;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// What the browser offers the solver: whether threads work (cross-origin isolation, workers that start workers), how many
/// cores, and JSPI (else the slower asyncify build runs).
/// </summary>
public sealed record SolverEnvironment(bool CrossOriginIsolated, int Cores, bool Jspi, bool NestedWorkers)
{
    /// <summary>Why CP-SAT cannot run in this browser, or null.</summary>
    public string? Problem =>
        !CrossOriginIsolated ? "The page is not cross-origin isolated, so CP-SAT cannot start (serve it with COOP/COEP headers or over HTTPS)."
        : !NestedWorkers ? "This browser cannot start workers from a worker, which CP-SAT's threads need. Try a current Chrome, Edge or Firefox."
        : null;
}

/// <summary>
/// CP-SAT solves in or-tools-wasm (wwwroot/solver/bridge.js, bundled from solver/): the model is built with the managed
/// OR-Tools classes as usual, serialized, solved in WebAssembly and the response parsed back.
/// </summary>
public sealed class CpSatBridge(IJSRuntime js) : ICpSatBackend
{
    private Task<IJSObjectReference>? _module;
    private int _jobs;

    private Task<IJSObjectReference> Module() => _module ??= js.InvokeAsync<IJSObjectReference>("import", "./solver/bridge.js").AsTask();

    public async Task<SolverEnvironment> EnvironmentAsync() => await (await Module()).InvokeAsync<SolverEnvironment>("environment");

    /// <summary>
    /// Loads the WebAssembly runtime ahead of the first solve (a few seconds on the first visit), then solves a tiny model the
    /// way a run does: the first call through <see cref="SolveAsync"/> sets up its interop and serializers on the page's
    /// thread (about 70 ms), which would otherwise freeze the page as a run starts.
    /// </summary>
    public async Task WarmUpAsync()
    {
        await (await Module()).InvokeVoidAsync("warmUp");
        var model = new CpModel();
        model.Maximize(model.NewBoolVar("x"));
        await SolveAsync(model, CpSatParameters.For(CpSatParameters.MaxWorkersPerSolve, 1), 0, CancellationToken.None);
    }

    /// <summary>
    /// Starts the lanes a run uses on this many threads (<see cref="CpSatParameters.LaneWorkers"/>), so their runtimes are
    /// loaded before the first run instead of during it.
    /// </summary>
    public async Task WarmUpLanesAsync(int threads) =>
        await (await Module()).InvokeVoidAsync("warmUpLanes", CpSatParameters.LaneWorkers(threads).Count);

    public async Task<CpSolverResponse> SolveAsync(CpModel model, CpSatParameters parameters, int lane, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var module = await Module();
        var job = Interlocked.Increment(ref _jobs);
        using var registration = ct.Register(() => _ = module.InvokeVoidAsync("cancel", job).AsTask());
        try
        {
            var bytes = await module.InvokeAsync<byte[]>("solve", model.Model.ToByteArray(), Options(parameters), lane, job);
            return CpSolverResponse.Parser.ParseFrom(bytes);
        }
        catch (JSException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }

    /// <summary>
    /// Lets the page update between stretches of a search, without a timer (app.js <c>yieldToPage</c>): .NET's own yield
    /// resumes through one, and Chrome runs a background tab's timers about once a minute, so a run crawled while the tab
    /// was hidden.
    /// </summary>
    public async Task YieldAsync() => await js.InvokeVoidAsync("mhwo.yieldToPage");

    /// <summary>The parameters as or-tools-wasm's options object (SatParameters fields in camelCase).</summary>
    private static Dictionary<string, object> Options(CpSatParameters p)
    {
        var options = new Dictionary<string, object> { ["numWorkers"] = p.Workers, ["maxTimeInSeconds"] = p.TimeLimitSeconds };
        if (p.Subsolvers.Count > 0) options["subsolvers"] = p.Subsolvers;
        if (p.LinearizationLevel is { } level) options["linearizationLevel"] = level;
        if (p.StopAtFirst) options["stopAfterFirstSolution"] = true;
        return options;
    }
}
