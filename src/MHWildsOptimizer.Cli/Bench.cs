using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Cli;

/// <summary>
/// Fixed optimizer benchmarks: runs request files (bench/scenarios) in this process and writes what each run took and found,
/// so two solver versions can be compared on speed and on the builds they return.
///   bench [scenario names or request paths] [--repeat n] [--threads n] [--label text] [--out file.json]
///   bench compare &lt;baseline.json&gt; &lt;candidate.json&gt;
/// Every scenario runs on --threads (default 8) whatever its file says.
/// </summary>
internal static class Bench
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public sealed record BuildRow(string Pair, double Score, string Proof, string Signature);
    public sealed record RunRow(double Seconds, double Best, List<BuildRow> Builds, Dictionary<string, int> Proofs, long Solves, List<string> Log);
    public sealed record ScenarioRow(string Name, string Path, List<RunRow> Runs);
    public sealed record Report(string Label, string Commit, DateTime Started, int Threads, string Machine, List<ScenarioRow> Scenarios);

    public static int Run(GameData data, List<string> args, string repoRoot)
    {
        if (args.Count > 0 && args[0] == "probe")
        {
            if (args.Count < 3) { Console.Error.WriteLine("usage: bench probe <request.json> <best score>"); return 2; }
            return Probe(data, RequestLoader.Load(args[1], data), double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (args.Count > 0 && args[0] == "sides")
            return SidesDump.Run(data, RequestLoader.Load(args[1], data));
        if (args.Count > 0 && args[0] == "bb")
        {
            if (args.Count < 3) { Console.Error.WriteLine("usage: bench bb <request.json> <bar> [--threads n]"); return 2; }
            var t = args.IndexOf("--threads") is var ti && ti >= 0 ? int.Parse(args[ti + 1]) : 8;
            return BbExperiment.Run(data, RequestLoader.Load(args[1], data), double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), t);
        }
        if (args.Count > 0 && args[0] == "compare")
        {
            if (args.Count < 3) { Console.Error.WriteLine("usage: bench compare <baseline.json> <candidate.json>"); return 2; }
            return Compare(Load(args[1]), Load(args[2]));
        }

        string? Option(string name)
        {
            var i = args.IndexOf(name);
            if (i < 0 || i + 1 >= args.Count) return null;
            var v = args[i + 1];
            args.RemoveRange(i, 2);
            return v;
        }
        var repeat = int.Parse(Option("--repeat") ?? "1");
        var threads = int.Parse(Option("--threads") ?? "8");
        var label = Option("--label") ?? Commit(repoRoot);
        var dir = Path.Combine(repoRoot, "bench", "scenarios");
        var outPath = Option("--out") ?? Path.Combine(repoRoot, "bench", "results", $"{DateTime.Now:yyyy-MM-dd_HHmm}_{Safe(label)}.json");

        var paths = args.Count == 0
            ? Directory.GetFiles(dir, "*.json").Where(p => !p.EndsWith(".talismans.json")).Order().ToList()
            : args.Select(a => File.Exists(a) ? Path.GetFullPath(a) : Path.Combine(dir, a.EndsWith(".json") ? a : a + ".json")).ToList();

        // warm-up: JIT, the native solver and the data caches
        var first = RequestLoader.Load(paths[0], data);
        new Optimizer(data, first with { Options = first.Options with { MaxThreads = threads, CpSatTimeLimitSeconds = 1, TopN = 1 } }).Run();

        var report = new Report(label, Commit(repoRoot), DateTime.Now, threads, Environment.MachineName, []);
        foreach (var path in paths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var request = RequestLoader.Load(path, data);
            if (!request.IsValid) { Console.WriteLine($"{name}: invalid: {string.Join("; ", request.Errors)}"); continue; }
            request = request with { Options = request.Options with { MaxThreads = threads } };
            var row = new ScenarioRow(name, Path.GetRelativePath(repoRoot, path), []);
            for (var r = 0; r < repeat; r++)
            {
                var log = new Lines();
                var sw = Stopwatch.StartNew();
                var result = new Optimizer(data, request).Run(log);
                sw.Stop();
                var builds = result.PairResults.SelectMany(p => p.Builds.Select(b => new BuildRow(p.Label, Math.Round(b.Score, 4),
                        b.Proof?.Kind.ToString() ?? "-", Signature(b))))
                    .OrderByDescending(b => b.Score).ToList();
                var run = new RunRow(Math.Round(sw.Elapsed.TotalSeconds, 2), builds.Count > 0 ? builds[0].Score : 0, builds,
                    builds.GroupBy(b => b.Proof).ToDictionary(g => g.Key, g => g.Count()), result.PairResults.Sum(p => p.StatesEvaluated), log.All);
                row.Runs.Add(run);
                Console.WriteLine($"{name,-22} run {r + 1}/{repeat}: {run.Seconds,7:0.0} s  best {run.Best,8:0.00}  " +
                                  $"{builds.Count} builds ({string.Join(", ", run.Proofs.Select(kv => $"{kv.Value} {kv.Key}"))}), {run.Solves} solves");
            }
            report.Scenarios.Add(row);
            Save(report, outPath);
        }
        Console.WriteLine($"Written to {outPath}");
        return 0;
    }

    /// <summary>Speed and results of a candidate against a baseline, per scenario (medians over repeats).</summary>
    private static int Compare(Report a, Report b)
    {
        Console.WriteLine($"baseline  {a.Label} ({a.Commit}, {a.Started:yyyy-MM-dd HH:mm})");
        Console.WriteLine($"candidate {b.Label} ({b.Commit}, {b.Started:yyyy-MM-dd HH:mm})");
        Console.WriteLine();
        Console.WriteLine($"{"scenario",-22} {"base s",8} {"cand s",8} {"speed",7}  {"base best",10} {"cand best",10}  top-N");
        var worse = 0;
        foreach (var sa in a.Scenarios)
        {
            if (b.Scenarios.FirstOrDefault(s => s.Name == sa.Name) is not { } sb) continue;
            double Median(IEnumerable<double> xs) { var l = xs.Order().ToList(); return l[l.Count / 2]; }
            var ta = Median(sa.Runs.Select(r => r.Seconds));
            var tb = Median(sb.Runs.Select(r => r.Seconds));
            var ra = sa.Runs[0];
            // every repeat of the candidate against the baseline's first run: a build lost in one repeat is lost
            var verdicts = sb.Runs.Select((rb, k) => (Run: rb, K: k, Text: Verdict(ra, rb))).ToList();
            var (rb, k, verdict) = verdicts.FirstOrDefault(v => v.Text.StartsWith("WORSE"), verdicts.FirstOrDefault(v => v.Text != "same", verdicts[0]));
            if (sb.Runs.Count > 1 && verdict != "same") verdict += $" (run {k + 1} of {sb.Runs.Count})";
            if (verdict.StartsWith("WORSE")) worse++;
            Console.WriteLine($"{sa.Name,-22} {ta,8:0.0} {tb,8:0.0} {ta / Math.Max(tb, 0.01),6:0.00}x  {ra.Best,10:0.00} {rb.Best,10:0.00}  {verdict}");
        }
        return worse > 0 ? 1 : 0;
    }

    /// <summary>The score lists, best first: equal, better or worse at the first place they differ, and the proofs.</summary>
    private static string Verdict(RunRow ra, RunRow rb)
    {
        var sca = ra.Builds.Select(x => x.Score).ToList();
        var scb = rb.Builds.Select(x => x.Score).ToList();
        var verdict = "same";
        for (var i = 0; i < Math.Min(sca.Count, scb.Count); i++)
        {
            if (Math.Abs(sca[i] - scb[i]) < 0.005) continue;
            verdict = scb[i] > sca[i] ? $"better at #{i + 1}" : $"WORSE at #{i + 1} ({scb[i] - sca[i]:+0.00;-0.00})";
            break;
        }
        if (verdict == "same" && scb.Count < sca.Count) verdict = $"WORSE: {scb.Count} of {sca.Count} builds";
        else if (verdict == "same" && scb.Count > sca.Count) verdict = $"{scb.Count} vs {sca.Count} builds";
        // proofs: builds proved in the baseline must stay proved
        var pa = ra.Proofs.GetValueOrDefault("Proved"); var pb = rb.Proofs.GetValueOrDefault("Proved");
        if (pb != pa) verdict = verdict == "same" ? $"proved {pb} vs {pa}" : verdict + $", proved {pb} vs {pa}";
        return verdict;
    }

    /// <summary>
    /// The score model of a request's fixed class seen as a function of the skill levels: its size, whether it only ever rises
    /// with a level, and how many level vectors score at least <paramref name="best"/> (what a search in skill space must rule out).
    /// </summary>
    private static int Probe(GameData data, ResolvedRequest request, double best)
    {
        var weapon = request.Weapon;
        var rel = Relevance.Build(weapon, request, data);
        var sw = Stopwatch.StartNew();
        var score = ScoreDecomposition.Build(weapon, rel, request.Conditions);
        Console.WriteLine($"model built in {sw.ElapsedMilliseconds} ms: {score.Features.Count} features, {score.Units.Count} units, {score.Sides.Count} sides");
        var inUnits = score.Units.SelectMany(u => u.Features).Distinct().Order().ToArray();
        foreach (var u in score.Units)
            Console.WriteLine($"  unit {u.States,5} states: {string.Join(", ", u.Features.Select(f => $"{score.Features[f].Name} 0-{score.Features[f].Max}"))}");
        var targetOnly = score.Features.Select((f, i) => (f, i)).Where(x => !inUnits.Contains(x.i)).Select(x => x.f.Name);
        Console.WriteLine($"  not scored (targets, conditions off): {string.Join(", ", targetOnly)}");
        var lattice = inUnits.Aggregate(1.0, (acc, f) => acc * (score.Features[f].Max + 1));
        Console.WriteLine($"  level vectors that score differently: {lattice:0.###e0}");

        // monotone: raising one level never lowers the score
        var rng = new Random(1);
        var values = new int[score.Features.Count];
        var violations = 0; var worst = 0.0; string? example = null;
        sw.Restart();
        var samples = 0;
        for (var n = 0; n < 20_000; n++)
        {
            foreach (var f in inUnits) values[f] = rng.Next(score.Features[f].Max + 1);
            var at = score.Evaluate(values);
            samples++;
            foreach (var f in inUnits)
            {
                if (values[f] == score.Features[f].Max) continue;
                values[f]++;
                var up = score.Evaluate(values);
                values[f]--;
                if (up < at - 1e-9)
                {
                    violations++;
                    if (at - up > worst) { worst = at - up; example = $"{score.Features[f].Name} {values[f]} -> {values[f] + 1}: {at:0.00} -> {up:0.00}"; }
                }
            }
        }
        Console.WriteLine($"  monotone check: {violations} drops in {samples} samples x {inUnits.Length} raises{(example is null ? "" : $", worst {example}")}");
        var perEval = sw.Elapsed.TotalMicroseconds / (samples * (inUnits.Length + 1));
        Console.WriteLine($"  one evaluation: {perEval:0.00} us");

        // the vectors scoring at least the best build: depth first, levels from the top, cut where even the rest at max falls short
        var order = inUnits.OrderByDescending(f => score.Features[f].Max).ToArray();
        long above = 0, minimal = 0;
        const long cap = 50_000_000;
        sw.Restart();
        var v = new int[score.Features.Count];
        foreach (var f in order) v[f] = score.Features[f].Max;
        Count(0);
        Console.WriteLine($"  vectors scoring >= {best:0.00}: {(above >= cap ? $">{cap:N0}" : above.ToString("N0"))}, of them minimal (lowering any level drops below): {minimal:N0} ({sw.Elapsed.TotalSeconds:0.0} s)");
        return 0;

        void Count(int depth)
        {
            if (above >= cap) return;
            if (depth == order.Length)
            {
                if (score.Evaluate(v) < best) return;
                above++;
                var isMinimal = true;
                foreach (var f in order)
                {
                    if (v[f] == 0) continue;
                    v[f]--;
                    var lower = score.Evaluate(v);
                    v[f]++;
                    if (lower >= best) { isMinimal = false; break; }
                }
                if (isMinimal) minimal++;
                return;
            }
            var feature = order[depth];
            for (var level = score.Features[feature].Max; level >= 0; level--)
            {
                v[feature] = level;
                // the rest are still at their max: below the bar here means below it for every lower level too
                if (score.Evaluate(v) < best) break;
                Count(depth + 1);
            }
            v[feature] = score.Features[feature].Max;
        }
    }

    private static string Signature(RankedBuild b)
    {
        var l = b.Loadout;
        return string.Join("|", new[] { l.Weapon.Stats.Spec?.Key ?? "", l.Talisman?.Talisman.Name ?? "" }
            .Concat(l.ArmorPieces.Select(a => a.Piece.Name + (a.Transcended ? "*" : ""))));
    }

    private static void Save(Report report, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Json));
    }

    private static Report Load(string path) => JsonSerializer.Deserialize<Report>(File.ReadAllText(path), Json)!;

    private static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-'));

    private static string Commit(string repoRoot)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD") { WorkingDirectory = repoRoot, RedirectStandardOutput = true, UseShellExecute = false })!;
            var s = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return s;
        }
        catch { return "unknown"; }
    }

    /// <summary>Progress lines with the time since the run started.</summary>
    private sealed class Lines : IProgress<string>
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly List<string> _lines = [];
        public void Report(string value) { lock (_lines) _lines.Add($"{_sw.Elapsed.TotalSeconds,7:0.0}  {value}"); }
        public List<string> All { get { lock (_lines) return _lines.ToList(); } }
    }
}
