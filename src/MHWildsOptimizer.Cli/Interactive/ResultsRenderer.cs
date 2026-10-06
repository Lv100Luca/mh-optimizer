using System.Globalization;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MHWildsOptimizer.Cli.Interactive;

/// <summary>Colored console output for optimizer results: one panel per build with a TL;DR header, separated by rules.</summary>
public static class ResultsRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(OptimizationResult result, ResolvedRequest resolved, GameData data)
    {
        var rank = 0;
        foreach (var pr in result.PairResults)
        {
            rank++;
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[bold yellow]#{rank}  {Markup.Escape(pr.Label)}[/]   [grey]best {pr.BestScore.ToString("0.0", Inv)}  |  {pr.StatesEvaluated} final states scored[/]").LeftJustified().RuleStyle("yellow"));
            if (pr.Builds.Count == 0) AnsiConsole.MarkupLine("  [red]no build satisfies the targets[/]");
            var i = 0;
            foreach (var b in pr.Builds)
                AnsiConsole.Write(BuildPanel(++i, b, resolved, data));
        }
        AnsiConsole.WriteLine();
    }

    public static Panel BuildPanel(int index, RankedBuild b, ResolvedRequest resolved, GameData data)
    {
        var s = BuildSummary.Create(b.Loadout, data, resolved.Conditions);
        var e = Markup.Escape;

        var lines = new List<IRenderable>
        {
            new Markup(string.Format(Inv,
                "[bold white on grey23] EFR {0:0.0} + EFE {1:0.0} = [/][bold black on green] {2:0.0} [/]   [grey]every condition on: {3:0.0}[/]",
                s.Efr, s.Efe, s.Total, s.TotalAllConditions)),
            new Markup(string.Format(Inv,
                "[red]ATK {0:0}[/] [grey]({1} display, base {2})[/]   [yellow]AFF {3}%[/] [grey](base {4}%)[/]   [orange1]CRIT x{5:0.00}[/]   [white]{6} sharpness[/]{7}",
                s.Attack, s.DisplayAttack, s.BaseAttack, s.Affinity, s.BaseAffinity, s.CritMultiplier, s.Sharpness?.ToString() ?? "no",
                s.Element == Element.None ? "" : string.Format(Inv, "   [blue]{0} {1:0}[/] [grey]({2} display)[/]", s.Element, s.ElementTrue, s.ElementDisplay))),
            new Markup($"[green]Sets:[/] {(s.ActiveSetBonuses.Count == 0 ? "[grey]-[/]" : e(string.Join(", ", s.ActiveSetBonuses)))}   [green]Groups:[/] {(s.ActiveGroupSkills.Count == 0 ? "[grey]-[/]" : e(string.Join(", ", s.ActiveGroupSkills)))}"),
            new Markup("[cyan]Skills:[/] " + string.Join("  ", s.Skills.Select(x => (x.Kind == SkillKind.Weapon ? $"[deepskyblue1]{e(x.Skill)} {x.Level}[/]" : $"[cyan]{e(x.Skill)} {x.Level}[/]") + (x.Effective < x.Level ? $"[grey](valued at {x.Effective})[/]" : "")))),
            new Markup($"[grey]{e(s.Description)}[/]"),
            new Rule().RuleStyle("grey23"),
            new Text(LoadoutReport.RenderDetails(b.Loadout, data).TrimEnd(), new Style(Color.Grey70)),
        };

        return new Panel(new Rows(lines))
            .Header($"[bold] Build {index} [/]", Justify.Left)
            .Border(BoxBorder.Rounded)
            .BorderColor(index == 1 ? Color.Green : Color.Grey)
            .Padding(1, 0)
            .Expand();
    }
}
