using Google.OrTools.Sat;
using Google.Protobuf;
using MHWildsOptimizer.Core.Optimize;
using Microsoft.JSInterop;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>What the browser offers the solver: whether threads work, how many cores, and JSPI (else the slower asyncify build runs).</summary>
public sealed record SolverEnvironment(bool CrossOriginIsolated, int Cores, bool Jspi);

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

    /// <summary>Loads the WebAssembly runtime ahead of the first solve (a few seconds on the first visit).</summary>
    public async Task WarmUpAsync() => await (await Module()).InvokeVoidAsync("warmUp");

    /// <summary>
    /// Starts the lanes an optimize-mode run uses on this many threads (one per <see cref="CpSatParameters.MaxWorkersPerSolve"/>
    /// workers), so their runtimes are loaded before the first run instead of during it.
    /// </summary>
    public async Task WarmUpLanesAsync(int threads) =>
        await (await Module()).InvokeVoidAsync("warmUpLanes", Math.Max(1, threads / CpSatParameters.For(threads, 1).Workers));

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

    /// <summary>The parameters as or-tools-wasm's options object (SatParameters fields in camelCase).</summary>
    private static Dictionary<string, object> Options(CpSatParameters p)
    {
        var options = new Dictionary<string, object> { ["numWorkers"] = p.Workers, ["maxTimeInSeconds"] = p.TimeLimitSeconds };
        if (p.Subsolvers.Count > 0) options["subsolvers"] = p.Subsolvers;
        if (p.LinearizationLevel is { } level) options["linearizationLevel"] = level;
        return options;
    }
}
