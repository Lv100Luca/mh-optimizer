using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Cli;

/// <summary>Experiment: which damage factors of the score model are the same across its sides (parts of the attack / uptime).</summary>
internal static class SidesDump
{
    public static int Run(GameData data, ResolvedRequest request)
    {
        var pair = request.SkillPairCandidates.FirstOrDefault();
        var weapon = request.Weapon with { SetBonus = pair?.SetBonus ?? request.Weapon.SetBonus, GroupSkill = pair?.GroupSkill ?? request.Weapon.GroupSkill };
        var rel = Relevance.Build(weapon, request, data);
        var score = ScoreDecomposition.Build(weapon, rel, request.Conditions);
        Console.WriteLine($"{score.Units.Count} units, {score.Sides.Count} sides, {score.HealthSides} health sides");
        string Sig(int side, params Func<Channels, double>[] chs) =>
            string.Join(";", score.Units.Select(u => string.Join(",", u.Tables[side].Select(c => string.Join(" ", chs.Select(ch => ch(c).ToString("R")))))));
        var kinds = new (string Name, Func<Channels, double>[] Ch)[]
        {
            ("rawPct", [c => c.RawPct]), ("rawFlat", [c => c.RawFlat]), ("aff", [c => c.Affinity]), ("critMult", [c => c.CritMult]),
            ("elePct", [c => c.ElePct]), ("eleFlat", [c => c.EleFlat]), ("critEle", [c => c.CritEleMult]), ("procs", [c => c.Procs]), ("rawMult", [c => c.RawMult]),
        };
        foreach (var (name, ch) in kinds)
        {
            var groups = Enumerable.Range(0, score.Sides.Count).GroupBy(s => Sig(s, ch)).Select(g => g.ToList()).ToList();
            Console.WriteLine($"{name,-9}: {groups.Count} distinct across sides: {string.Join(" | ", groups.Select(g => string.Join(",", g)))}");
        }
        for (var s = 0; s < score.Sides.Count; s++)
        {
            var sd = score.Sides[s];
            Console.WriteLine($"side {s}: health {sd.Health} weight {sd.Weight:0.000} rawFactor {sd.RawFactor:0.000} baseAff {sd.BaseAffinity} baseRawFlat {sd.BaseRawFlat} baseRawPct {sd.BaseRawPct} critMult {sd.BaseCritMult} eleScale {sd.ElementScale:0.000} attack {sd.Attack.TotalMv}");
        }
        return 0;
    }
}
