using System.Diagnostics;
using System.Globalization;
using MHWildsOptimizer.Core.Data;
using WasmSpeedTest;

// Native baseline: each case on 1 thread and on all threads, twice (the second run is warmed up).
// dotnet run -- <case> [threads] runs one case once, e.g. to measure its memory in a fresh process.
// dotnet run -- dump <config> <threads> <dir> saves the models of a CP-SAT run; solve <dir> <threads> <limit> solves them natively.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
if (args is ["solve", var solveDir, var solveThreads, var solveLimit, ..])
{
    CpSatTools.Solve(solveDir, int.Parse(solveThreads), double.Parse(solveLimit, CultureInfo.InvariantCulture),
        args.ElementAtOrDefault(4) ?? "", args.ElementAtOrDefault(5) ?? "");
    return;
}

var dataDir = GameDataLoader.FindDataDirectory();
var weapons = Path.Combine(Path.GetDirectoryName(dataDir)!, "inputs", "profiles", "Luca", "weapons");
var data = GameDataLoader.Load(dataDir);

if (args is ["dump", var config, var dumpThreads, var dumpDir])
{
    CpSatTools.Dump(data, weapons, new BenchCase(config, CpSat: true), int.Parse(dumpThreads), dumpDir);
    return;
}

// optimize <config> <threads> <baseline|two-phase|cutoff> [sat parameters] [lanes]
if (args is ["optimize", var optConfig, var optThreads, var strategy, ..])
{
    OptimizeExperiment.Run(data, weapons, optConfig, int.Parse(optThreads), strategy, args.ElementAtOrDefault(4) ?? "",
        int.Parse(args.ElementAtOrDefault(5) ?? "1"));
    return;
}

if (args.Length > 0)
{
    // BENCH_VERBOSE=1 prints the optimizer's progress (per-depth timings, per-class results)
    var progress = Environment.GetEnvironmentVariable("BENCH_VERBOSE") == "1" ? new ConsoleProgress() : null;
    Report(Bench.Run(data, weapons, Bench.Cases.Concat(Bench.CpSatCases).First(c => c.Name == args[0]), args.Length > 1 ? int.Parse(args[1]) : 1, progress), 1);
    return;
}

foreach (var c in Bench.Cases)
foreach (var threads in new[] { 1, Environment.ProcessorCount })
for (var run = 1; run <= 2; run++)
    Report(Bench.Run(data, weapons, c, threads), run);

static void Report(BenchResult r, int run)
{
    var peak = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
    Console.WriteLine(r.Error ?? $"{r.Case,-10} threads {r.Threads,2} run {run}: {r.Seconds,8:F2}s  states {r.States,10}  best {r.Best:F1}  peak {peak} MB");
}

sealed class ConsoleProgress : IProgress<string>
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public void Report(string value) => Console.WriteLine($"[{_sw.Elapsed.TotalSeconds,6:F1}s] {value}");
}
