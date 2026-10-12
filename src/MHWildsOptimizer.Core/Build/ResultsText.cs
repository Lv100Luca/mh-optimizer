using System.Globalization;
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
            sb.AppendLine($"##### #{rank} skill pair: {pr.Label}   best {pr.BestScore.ToString(BuildSummary.ScoreFormat, CultureInfo.InvariantCulture)}   ({pr.StatesEvaluated} {pr.WorkLabel}; {pr.CandidateSummary})");
            if (pr.Builds.Count == 0) sb.AppendLine("  no build satisfies the targets");
            var i = 0;
            foreach (var b in pr.Builds)
            {
                i++;
                sb.AppendLine();
                sb.AppendLine(LoadoutReport.Render(b.Loadout, data, resolved.Conditions, BuildTitle(i, b)).TrimEnd());
                sb.Append(WeaponOptionsText(b));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>With weapon tuning: the weapon the build got and, per optimized part, the best score of each choice on its armor.</summary>
    public static string WeaponOptionsText(RankedBuild b)
    {
        if (b.WeaponOptions is not { Count: > 0 } options) return "";
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"  Optimized weapon: {b.Loadout.Weapon.Stats.Spec?.Label()}");
        foreach (var part in options.GroupBy(o => o.Part))
        {
            sb.AppendLine($"    {part.Key} on this armor:");
            foreach (var o in part.Take(6))
                sb.AppendLine(string.Format(inv, "      {0} {1,-48} {2:0.00}{3}", o.Chosen ? "*" : " ", o.Label, o.Score,
                    o.Chosen ? "" : string.Format(inv, "  ({0:+0.00;-0.00})", o.Score - b.Score)));
        }
        return sb.ToString();
    }

    /// <summary>"Build 1  -  EFR 569.00 + EFE 59.80 + procs 12.30 = 641.10" (the proc term only when there is one).</summary>
    public static string BuildTitle(int rank, RankedBuild b) => BuildTitle($"Build {rank}", b);

    /// <summary>"My build  -  EFR 569.00 + EFE 59.80 = 628.80"</summary>
    public static string BuildTitle(string label, RankedBuild b) =>
        string.Format(CultureInfo.InvariantCulture, "{0}  -  EFR {1:0.00} + EFE {2:0.00}{3} = {4:0.00}",
            label, b.Result.EffectiveRaw, b.Result.EffectiveElement,
            b.Result.ProcDamage > 0 ? string.Format(CultureInfo.InvariantCulture, " + procs {0:0.00}", b.Result.ProcDamage) : "",
            b.Score);
}
