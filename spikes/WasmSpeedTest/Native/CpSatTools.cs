using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using Google.Protobuf;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace WasmSpeedTest;

/// <summary>Hooks called by the generated CpSatDumpSearch around every solve.</summary>
internal static class CpSatDump
{
    public static string Directory = ".";
    public static string Prefix = "model";
    private static int _index;

    public static void Before(CpModel model) =>
        File.WriteAllBytes(Path.Combine(Directory, $"{Prefix}-{++_index}.pb"), model.Model.ToByteArray());

    public static void After(CpSolver solver, CpSolverStatus status, TimeSpan elapsed) =>
        Console.WriteLine($"{Prefix}-{_index}.pb  native (model built in C#): {elapsed.TotalSeconds,6:F2}s  {status,-8}  objective {solver.ObjectiveValue}");
}

/// <summary>
/// "dump": runs a fixed-mode config's CP-SAT search, saving each solve's model as &lt;case&gt;-&lt;n&gt;.pb.
/// "solve": solves saved .pb models with the native solver, for comparison with the same bytes in WebAssembly.
/// </summary>
internal static class CpSatTools
{
    public static void Dump(GameData data, string weaponsDirectory, BenchCase c, int threads, string outDirectory)
    {
        var r = Bench.Resolve(data, weaponsDirectory, c, threads);
        var s = CpSatSetup.ForFixedPair(data, r);

        Directory.CreateDirectory(outDirectory);
        CpSatDump.Directory = outDirectory;
        CpSatDump.Prefix = c.Config.Replace(' ', '_');
        var sw = Stopwatch.StartNew();
        var search = new CpSatDumpSearch(data, s.Rel, s.Weapon, r.Conditions, s.Filler, s.Armor, s.Kinds, s.Talismans, r.Options.TopN,
            r.Options.EffectiveThreads, r.Options.CpSatTimeLimitSeconds, null, CancellationToken.None);
        var builds = search.Run();
        Console.WriteLine($"{c.Config}: {search.Summary}, best {builds.FirstOrDefault()?.Score:F1}, {sw.Elapsed.TotalSeconds:F2}s total, " +
                          $"{r.Options.EffectiveThreads} threads, time limit {r.Options.CpSatTimeLimitSeconds}s per solve");
    }

    /// <param name="filter">Only files whose name contains this.</param>
    /// <param name="extra">More SatParameters in text format, e.g. "log_search_progress:true".</param>
    public static void Solve(string directory, int threads, double timeLimit, string filter = "", string extra = "")
    {
        var verbose = extra.Contains("log_search_progress");
        var times = new List<string>();
        double total = 0;
        var statuses = new List<string>();
        foreach (var file in System.IO.Directory.GetFiles(directory, "*.pb").Where(f => Path.GetFileName(f).Contains(filter)).Order(StringComparer.Ordinal))
        {
            var model = new CpModel();
            model.Model.MergeFrom(CpModelProto.Parser.ParseFrom(File.ReadAllBytes(file)));
            using var solver = new CpSolver();
            solver.StringParameters = $"num_workers:{threads}, max_time_in_seconds:{timeLimit.ToString(CultureInfo.InvariantCulture)}" +
                                      (extra.Length > 0 ? ", " + extra : "");
            var sw = Stopwatch.StartNew();
            var status = solver.Solve(model);
            if (verbose) Console.WriteLine($"{Path.GetFileName(file)}  native: {sw.Elapsed.TotalSeconds,6:F2}s  {status,-8}  objective {solver.ObjectiveValue}");
            total += sw.Elapsed.TotalSeconds;
            times.Add(sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture));
            statuses.Add(status == CpSolverStatus.Optimal ? "opt" : $"{status}:{solver.ObjectiveValue}");
        }
        var notOptimal = statuses.Count(s => s != "opt");
        Console.WriteLine($"{threads,2} workers  total {total,6:F1}s  [{string.Join(" ", times)}]  {(notOptimal == 0 ? "all optimal" : $"{notOptimal} NOT optimal: {string.Join(" ", statuses)}")}  {extra}");
    }
}
