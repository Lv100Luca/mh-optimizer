using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Google.OrTools.Sat;
using Google.Protobuf;
using Microsoft.JSInterop;

namespace WasmSpeedTest;

/// <summary>
/// Stands in for OR-Tools' CpSolver in the browser: the model is built with the managed OR-Tools classes as usual, serialized,
/// and solved by or-tools-wasm through wwwroot/ortools/bridge.js. Offers the members CpSatSearch uses.
/// </summary>
public sealed partial class WasmCpSolver(IJSObjectReference bridge)
{
    private static int _jobs;
    private readonly int _job = Interlocked.Increment(ref _jobs);
    private CpSolverResponse? _response;

    /// <summary>SatParameters in text format, as CpSatSearch sets it: "num_workers:8, max_time_in_seconds:120, subsolvers:'max_lp'".</summary>
    public string StringParameters { get; set; } = "";

    /// <summary>Per solve: time spent serializing, in the bridge (solve + transfer), and parsing the response.</summary>
    public List<(double Serialize, double Solve, double Parse, double SolverWall)> Timings { get; } = [];

    /// <summary>Solve on this lane of the bridge's worker pool (lets several searches solve at once); null = the shared executor.</summary>
    public int? Lane { get; init; }

    public void StopSearch() => _ = bridge.InvokeVoidAsync("cancel", _job).AsTask();

    public async Task<CpSolverStatus> SolveAsync(CpModel model)
    {
        var sw = Stopwatch.StartNew();
        var bytes = model.Model.ToByteArray();
        var serialize = sw.Elapsed.TotalSeconds;
        var result = Lane is { } lane
            ? await bridge.InvokeAsync<byte[]>("solveOnLane", bytes, Parameters(StringParameters), lane)
            : await bridge.InvokeAsync<byte[]>("solveProto", bytes, Parameters(StringParameters), _job);
        var solve = sw.Elapsed.TotalSeconds - serialize;
        _response = CpSolverResponse.Parser.ParseFrom(result);
        Timings.Add((serialize, solve, sw.Elapsed.TotalSeconds - serialize - solve, _response.WallTime));
        return _response.Status;
    }

    /// <summary>The flat text-format fields CpSatSearch uses, as the camelCase options object or-tools-wasm takes; repeated fields become arrays.</summary>
    public static Dictionary<string, object> Parameters(string text)
    {
        var options = new Dictionary<string, object>();
        foreach (Match m in Field().Matches(text))
        {
            var name = Regex.Replace(m.Groups["name"].Value, "_([a-z])", x => x.Groups[1].Value.ToUpperInvariant());
            object value = m.Groups["quoted"].Success ? m.Groups["quoted"].Value
                : m.Groups["value"].Value is "true" or "false" ? m.Groups["value"].Value == "true"
                : double.Parse(m.Groups["value"].Value, CultureInfo.InvariantCulture);
            if (name is "subsolvers" or "extraSubsolvers" or "ignoreSubsolvers")
            {
                if (!options.TryGetValue(name, out var list)) options[name] = list = new List<object>();
                ((List<object>)list).Add(value);
            }
            else options[name] = value;
        }
        return options;
    }

    [GeneratedRegex(@"(?<name>[a-z_]+)\s*:\s*(?:['""](?<quoted>[^'""]*)['""]|(?<value>[^,\s]+))")]
    private static partial Regex Field();

    public bool BooleanValue(ILiteral literal)
    {
        var index = literal.GetIndex();
        return index >= 0 ? _response!.Solution[index] != 0 : _response!.Solution[-index - 1] == 0;
    }

    public long Value(IntVar variable) => _response!.Solution[variable.GetIndex()];

    public double ObjectiveValue => _response!.ObjectiveValue;

    public double BestObjectiveBound => _response!.BestObjectiveBound;
}
