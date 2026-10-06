using System.Text;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Core.Build;

/// <summary>The plain-text results report written next to a request file (inputs/&lt;name&gt;.results.txt).</summary>
public static class ResultsText
{
    public static string Render(OptimizationResult result, ResolvedRequest resolved, GameData data)
    {
        var sb = new StringBuilder();
        var rank = 0;
        foreach (var pr in result.PairResults)
        {
            rank++;
            sb.AppendLine($"##### #{rank} skill pair: {pr.Label}   best {pr.BestScore:0.0}   ({pr.StatesEvaluated} final states scored; {pr.CandidateSummary})");
            if (pr.Builds.Count == 0) sb.AppendLine("  no build satisfies the targets");
            var i = 0;
            foreach (var b in pr.Builds)
            {
                i++;
                sb.AppendLine();
                sb.AppendLine(LoadoutReport.Render(b.Loadout, data, resolved.Conditions, $"Build {i}  -  EFR {b.Result.EffectiveRaw:0.0} + EFE {b.Result.EffectiveElement:0.0} = {b.Score:0.0}").TrimEnd());
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
