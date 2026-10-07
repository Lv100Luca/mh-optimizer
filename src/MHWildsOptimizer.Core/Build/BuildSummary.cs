using System.Globalization;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Build;

/// <summary>The digest of a build under given conditions: headline numbers, what is active, and why it scores.</summary>
public sealed record BuildSummary(
    int BaseAttack,
    double Attack,
    int DisplayAttack,
    int BaseAffinity,
    int Affinity,
    double CritMultiplier,
    double CritFactor,
    SharpnessColor? Sharpness,
    Element Element,
    double ElementTrue,
    int ElementDisplay,
    double Efr,
    double Efe,
    double Procs,
    double Total,
    double TotalAllConditions,
    IReadOnlyList<string> ActiveSetBonuses,
    IReadOnlyList<string> ActiveGroupSkills,
    IReadOnlyList<(string Skill, int Level, int Effective, SkillKind Kind)> Skills,
    IReadOnlyList<string> AffinitySources,
    IReadOnlyList<string> RawSources,
    IReadOnlyList<string> ElementSources,
    IReadOnlyList<string> ProcSources,
    IReadOnlyList<string> DependsOn,
    string Description)
{
    /// <summary>"EFR a + EFE b", plus the proc term when there is one.</summary>
    /// <summary>Format for scores and their EFR / EFE / procs terms: two decimals, so near-ties are visible.</summary>
    public const string ScoreFormat = "0.00";

    public string ScoreTerms(string format = ScoreFormat) =>
        $"EFR {Efr.ToString(format, CultureInfo.InvariantCulture)} + EFE {Efe.ToString(format, CultureInfo.InvariantCulture)}"
        + (Procs > 0 ? $" + procs {Procs.ToString(format, CultureInfo.InvariantCulture)}" : "");

    public static BuildSummary Create(Loadout loadout, GameData data, Conditions conditions)
    {
        var inv = CultureInfo.InvariantCulture;
        var skills = SkillAggregator.Aggregate(loadout, data);
        var w = loadout.Weapon.Stats;
        var r = DamageCalculator.Calculate(w, skills, conditions);
        var allOn = DamageCalculator.Calculate(w, skills, Conditions.AllOn, trace: false);

        var sets = skills.ActiveSetBonuses
            .Select(x => $"{x.Name} {x.Tier}{RankName(data, x.Name, (int)x.Tier)}")
            .ToList();
        var groups = skills.ActiveGroupSkills.Select(g => $"{g}{RankName(data, g, 1)}").ToList();
        var skillList = skills.Levels
            .Select(kv => (kv.Key, kv.Value, conditions.Effective(kv.Key, kv.Value), data.SkillsByName.TryGetValue(kv.Key, out var s) ? s.Kind : SkillKind.Armor))
            .OrderBy(x => x.Item4 == SkillKind.Weapon ? 0 : 1).ThenByDescending(x => x.Item3).ThenBy(x => x.Key)
            .ToList();

        // modifier lines look like "Label: affinity +15%", "Label: raw +20", "Label: raw x1.05", "Label: element x1.2", "Label: proc +12.3 per 100 MV (...)"
        var mods = r.Breakdown.Skip(1).Where(l => l.Contains(": ") && !l.StartsWith("Raw ") && !l.StartsWith("Element ")).ToList();
        var aff = Sources(mods, "affinity");
        var raw = Sources(mods, "raw");
        var ele = Sources(mods, "element");
        var procs = Sources(mods, "proc");
        var depends = mods.Where(m => !m.Contains(": proc ")).Select(m => ConditionFor(m[..m.IndexOf(':')])).Where(d => d is not null).Distinct().ToList()!;
        if (procs.Count > 0) depends.Add("attack profile");

        var parts = new List<string>();
        parts.Add(string.Format(inv, "{0}% affinity with x{1:0.00} crits", r.Affinity, r.CriticalMultiplier) + (aff.Count > 0 ? $" from {Join(aff)}" : ""));
        parts.Add(string.Format(inv, "raw {0} -> {1:0}", w.TrueRaw, r.TrueRaw) + (raw.Count > 0 ? $" via {Join(raw)}" : " (no raw skills)"));
        if (w.ElementTrue > 0)
            parts.Add(string.Format(inv, "{0} {1:0} -> {2:0} true", w.Element, w.ElementTrue, r.ElementTrue) + (ele.Count > 0 ? $" via {Join(ele)}" : ""));
        if (procs.Count > 0)
            parts.Add(string.Format(inv, "procs +{0:0.0} per 100 MV from {1}", r.ProcDamage, Join(procs)));
        var description = string.Join("; ", parts) + "."
                          + (depends.Count > 0 ? $" Depends on: {string.Join(", ", depends)}." : " Nothing conditional.");

        return new BuildSummary(
            w.TrueRaw, r.TrueRaw, (int)Math.Round(r.TrueRaw * w.Type.Bloat()),
            w.Affinity, r.Affinity, r.CriticalMultiplier, r.EffectiveRaw / (r.TrueRaw * r.SharpnessRawModifier),
            r.Sharpness, w.Element, r.ElementTrue, (int)Math.Round(r.ElementTrue * 10),
            r.EffectiveRaw, r.EffectiveElement, r.ProcDamage, r.Total, allOn.Total,
            sets, groups, skillList, aff, raw, ele, procs, depends!, description);
    }

    private static string RankName(GameData data, string skill, int level)
    {
        var rank = data.SkillsByName.TryGetValue(skill, out var s) ? s.Ranks.FirstOrDefault(x => x.Level == level) : null;
        return rank?.Name is { } n ? $" ({n})" : "";
    }

    private static List<string> Sources(IEnumerable<string> mods, string stat)
    {
        var result = new List<string>();
        foreach (var m in mods)
        {
            var colon = m.IndexOf(':');
            var label = m[..colon];
            var effect = m[(colon + 2)..];
            if (!effect.StartsWith(stat + " ")) continue;
            var value = effect[(stat.Length + 1)..].Replace(" (true)", "");
            if (stat == "proc") value = value[..value.IndexOf(' ')]; // "+12.3 per 100 MV (...)" -> "+12.3"
            if (label.StartsWith("Weakness Exploit (wound)")) label = "wound hits";
            result.Add($"{label} ({value})");
        }
        return result;
    }

    private static string Join(IReadOnlyList<string> items) => string.Join(", ", items);

    private static string? ConditionFor(string label) => label switch
    {
        _ when label.StartsWith("Agitator") => "enraged monster",
        _ when label.StartsWith("Mutual Hostility") => "enraged monster",
        _ when label.StartsWith("Weakness Exploit (wound)") => "hitting wounds",
        _ when label.StartsWith("Weakness Exploit") => "weak-point hits",
        _ when label.StartsWith("Peak Performance") => "full health",
        _ when label.StartsWith("Maximum Might") => "full stamina",
        _ when label.StartsWith("Burst") => "5 consecutive hits",
        _ when label.StartsWith("Frenzy overcome") || label.StartsWith("Antivirus") || label.StartsWith("Black Eclipse") => "overcoming Frenzy",
        _ when label.StartsWith("Latent Power") => "Latent Power trigger",
        _ when label.StartsWith("Adrenaline Rush") || label.StartsWith("Razor's Edge") => "perfect dodge",
        _ when label.StartsWith("Counterstrike") => "getting knocked back",
        _ when label.StartsWith("Resentment") || label.StartsWith("Dark Arts") => "red health",
        _ when label.StartsWith("Heroics") => "low health",
        _ when label.StartsWith("Foray") => "poisoned/paralyzed monster",
        _ when label.StartsWith("Offensive Guard") => "perfect guard",
        _ when label.StartsWith("Punishing Draw") => "draw attacks",
        _ when label.StartsWith("Charge Master") => "charged attacks",
        _ when label.StartsWith("Coalescence") => "Frenzy cure (Gore set)",
        _ when label.StartsWith("Elemental Absorption") => "taking elemental damage",
        _ when label.StartsWith("Azure Bolt") => "Azure Bolt window",
        _ when label.StartsWith("Resonance") => "Resonance phase",
        _ when label.StartsWith("Guts") => "Guts not yet triggered",
        _ when label.StartsWith("Powerhouse") => "Power Clash / Offset",
        _ when label.StartsWith("Protein Fiend") => "eating a steak",
        _ when label.StartsWith("Binding Counter") => "recovering from a bind",
        _ when label.StartsWith("War Cry") => "To Victory! gesture",
        _ when label.StartsWith("Festival") => "festival running",
        _ when label.StartsWith("Resuscitate") => "being afflicted",
        _ when label.StartsWith("Inspiration") => "companion effects",
        _ when label.StartsWith("Affinity Sliding") => "sliding",
        _ => null,
    };
}
