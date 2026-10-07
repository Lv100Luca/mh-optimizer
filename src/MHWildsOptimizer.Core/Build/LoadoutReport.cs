using System.Globalization;
using System.Text;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Build;

/// <summary>Human-readable build printout: equipment with decorations per piece, all decorations, final skills with sources, final stats.</summary>
public static class LoadoutReport
{
    public static string Render(Loadout loadout, GameData data, Conditions? conditions = null, string? title = null)
    {
        var cond = conditions ?? Conditions.Default;
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        var w = loadout.Weapon.Stats;

        if (title is not null) sb.AppendLine($"=== {title} ===");

        // ---------------- TL;DR ----------------
        var s = BuildSummary.Create(loadout, data, cond);
        sb.AppendLine(string.Format(inv, "TL;DR  Attack {0:0} ({1} display)  Affinity {2}%  Crit x{3:0.00}  {4} sharpness{5}  ->  {6} = {7:0.00}  (all conditions on: {8:0.00})",
            s.Attack, s.DisplayAttack, s.Affinity, s.CritMultiplier, s.Sharpness?.ToString() ?? "no",
            s.Element == Element.None ? "" : string.Format(inv, "  {0} {1:0} ({2} display)", s.Element, s.ElementTrue, s.ElementDisplay),
            s.ScoreTerms(), s.Total, s.TotalAllConditions));
        sb.AppendLine($"       Sets: {(s.ActiveSetBonuses.Count == 0 ? "-" : string.Join(", ", s.ActiveSetBonuses))}   Groups: {(s.ActiveGroupSkills.Count == 0 ? "-" : string.Join(", ", s.ActiveGroupSkills))}");
        sb.AppendLine($"       Skills: {string.Join(", ", s.Skills.Select(x => x.Effective < x.Level ? $"{x.Skill} {x.Level} (valued at {x.Effective})" : $"{x.Skill} {x.Level}"))}");
        sb.AppendLine($"       Why: {s.Description}");
        sb.AppendLine();

        sb.Append(RenderDetails(loadout, data));

        // ---------------- stats ----------------
        var skills = SkillAggregator.Aggregate(loadout, data);
        var allOn = DamageCalculator.Calculate(w, skills, Conditions.AllOn);
        AppendStats(sb, "Stats with every conditional skill active", w, allOn, inv);
        var chosen = DamageCalculator.Calculate(w, skills, cond);
        if (Math.Abs(chosen.Total - allOn.Total) > 0.05 || chosen.Affinity != allOn.Affinity)
            AppendStats(sb, "Stats under the requested conditions", w, chosen, inv);

        return sb.ToString();
    }

    /// <summary>Equipment with decorations per piece, the decoration list and the skill list with sources (no stats).</summary>
    public static string RenderDetails(Loadout loadout, GameData data)
    {
        var sb = new StringBuilder();
        var w = loadout.Weapon.Stats;

        // ---------------- equipment ----------------
        sb.AppendLine("Equipment");
        var ele = w.Element == Element.None ? "no element" : $"{w.ElementDisplay} {w.Element}";
        sb.AppendLine($"  Weapon    {w.Type,-26} {w.TrueRaw} raw ({w.DisplayAttack} display), {w.Affinity}% affinity, {ele}, {w.TopSharpness?.ToString() ?? "-"} sharpness");
        sb.AppendLine($"            rolled: {w.SetBonus ?? "-"} / {w.GroupSkill ?? "-"}");
        sb.AppendLine($"            slots {SlotText(w.Slots.Select(l => new TalismanSlot(l, SkillKind.Weapon)))}  decos: {DecoText(loadout.Weapon.Decos)}");

        foreach (var a in loadout.ArmorPieces)
        {
            var p = a.Piece;
            var extras = string.Join("  ", new[]
            {
                p.SetBonus.Count > 0 ? "set: " + string.Join(" / ", p.SetBonus) : null,
                p.GroupSkill is not null ? "group: " + p.GroupSkill : null,
            }.Where(x => x is not null));
            sb.AppendLine($"  {p.Piece,-9} {p.Name,-26} R{p.Rarity}{(a.Transcended ? "T" : " ")} {SkillText(p.Skills)}");
            sb.AppendLine($"            slots {SlotText(a.EffectiveSlots.Select(l => new TalismanSlot(l, SkillKind.Armor)))}  decos: {DecoText(a.Decos)}{(extras.Length > 0 ? "  " + extras : "")}");
        }

        if (loadout.Talisman is { } t)
        {
            sb.AppendLine($"  Talisman  {t.Talisman.Name,-26} R{t.Talisman.Rarity}  {SkillText(t.Talisman.Skills)}");
            sb.AppendLine($"            slots {SlotText(t.Talisman.Slots)}  decos: {DecoText(t.Decos)}");
        }
        else sb.AppendLine("  Talisman  -");

        // ---------------- decorations ----------------
        var allDecos = loadout.Weapon.Decos
            .Concat(loadout.ArmorPieces.SelectMany(a => a.Decos))
            .Concat(loadout.Talisman?.Decos ?? [])
            .Where(d => d is not null).Select(d => d!).ToList();
        sb.AppendLine();
        sb.AppendLine($"Decorations ({allDecos.Count})");
        if (allDecos.Count == 0) sb.AppendLine("  none");
        foreach (var g in allDecos.GroupBy(d => d.Name).OrderByDescending(g => g.First().Slot).ThenBy(g => g.Key))
            sb.AppendLine($"  {g.Count()}x {g.Key,-30} {SkillText(g.First().Skills)}");

        // ---------------- skills with sources ----------------
        var skills = SkillAggregator.Aggregate(loadout, data);
        var sources = CollectSources(loadout);
        sb.AppendLine();
        sb.AppendLine("Skills (all decorations in effect)");
        foreach (var (name, level) in skills.Levels.OrderBy(kv => data.SkillsByName.TryGetValue(kv.Key, out var s) ? s.Kind : SkillKind.Armor).ThenByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
        {
            var kind = data.SkillsByName.TryGetValue(name, out var s) ? s.Kind.ToString().ToLowerInvariant() : "?";
            var raw = skills.RawLevels[name];
            var waste = raw > level ? $"  ({raw - level} wasted)" : "";
            sb.AppendLine($"  {name,-24} Lv{level} ({kind,-6}) <- {string.Join(", ", sources[name])}{waste}");
        }
        var activeSets = skills.ActiveSetBonuses.ToList();
        var activeGroups = skills.ActiveGroupSkills.ToList();
        sb.AppendLine("  Set bonuses: " + (skills.SetBonusPieces.Count == 0 ? "-" : string.Join(", ", skills.SetBonusPieces.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}pc{TierText(skills.SetTier(kv.Key), data, kv.Key)}"))));
        sb.AppendLine("  Group skills: " + (skills.GroupSkillPieces.Count == 0 ? "-" : string.Join(", ", skills.GroupSkillPieces.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}pc{(skills.GroupActive(kv.Key) ? " ACTIVE" : "")}"))));
        return sb.ToString();
    }

    private static void AppendStats(StringBuilder sb, string header, Gogma.GogmaWeaponStats w, DamageResult r, CultureInfo inv)
    {
        sb.AppendLine();
        sb.AppendLine(header);
        sb.AppendLine(string.Format(inv, "  Attack     {0} -> {1:0.#} true ({2:0} display)", w.TrueRaw, r.TrueRaw, r.TrueRaw * w.Type.Bloat()));
        sb.AppendLine(string.Format(inv, "  Affinity   {0}% -> {1}%   crit x{2:0.00} (factor {3:0.###})", w.Affinity, r.Affinity, r.CriticalMultiplier, r.EffectiveRaw / (r.TrueRaw * r.SharpnessRawModifier)));
        sb.AppendLine(string.Format(inv, "  Sharpness  {0} (raw x{1:0.###}, element x{2:0.####})", r.Sharpness?.ToString() ?? "-", r.SharpnessRawModifier, r.SharpnessElementModifier));
        if (w.Element != Element.None)
            sb.AppendLine(string.Format(inv, "  Element    {0} {1:0.#} -> {2:0.#} true ({3:0} display), cap {4:0.#}, crit element x{5:0.00}", w.Element, r.BaseElementTrue, r.ElementTrue, r.ElementTrue * 10, r.ElementCap, r.CriticalElementMultiplier));
        sb.AppendLine(string.Format(inv, "  EFR {0:0.00}   EFE {1:0.00}{2}   Total {3:0.00}", r.EffectiveRaw, r.EffectiveElement,
            r.ProcDamage > 0 ? string.Format(inv, "   procs {0:0.00}", r.ProcDamage) : "", r.Total));
        var mods = r.Breakdown.Skip(1).Where(l => !l.StartsWith("Raw ") && !l.StartsWith("Element ")).ToList();
        if (mods.Count > 0) sb.AppendLine("  Modifiers: " + string.Join("; ", mods));
    }

    private static Dictionary<string, List<string>> CollectSources(Loadout loadout)
    {
        var sources = new Dictionary<string, List<string>>();
        void Add(IEnumerable<SkillGrant> grants, string label)
        {
            foreach (var g in grants)
                sources.GetOrAdd(g.Skill).Add($"{label} {g.Level}");
        }
        void AddDecos(IEnumerable<Decoration?> decos)
        {
            foreach (var g in decos.Where(d => d is not null).GroupBy(d => d!.Name))
                foreach (var grant in g.First()!.Skills)
                    sources.GetOrAdd(grant.Skill).Add(g.Count() > 1 ? $"{g.Key} x{g.Count()} {grant.Level * g.Count()}" : $"{g.Key} {grant.Level}");
        }

        AddDecos(loadout.Weapon.Decos);
        foreach (var a in loadout.ArmorPieces)
        {
            Add(a.Piece.Skills, a.Piece.Name);
            AddDecos(a.Decos);
        }
        if (loadout.Talisman is { } t)
        {
            Add(t.Talisman.Skills, t.Talisman.Name);
            AddDecos(t.Decos);
        }
        return sources;
    }

    private static List<string> GetOrAdd(this Dictionary<string, List<string>> d, string key) =>
        d.TryGetValue(key, out var l) ? l : d[key] = [];

    private static string SkillText(IEnumerable<SkillGrant> grants) =>
        string.Join(", ", grants.Select(g => $"{g.Skill} {g.Level}"));

    private static string SlotText(IEnumerable<TalismanSlot> slots)
    {
        var list = slots.ToList();
        return list.Count == 0 ? "[-]" : "[" + string.Join(",", list.Select(s => (s.Kind == SkillKind.Weapon ? "w" : "a") + s.Level)) + "]";
    }

    private static string DecoText(IReadOnlyList<Decoration?> decos)
    {
        var names = decos.Where(d => d is not null).Select(d => d!.Name).ToList();
        return names.Count == 0 ? "-" : string.Join(", ", names);
    }

    private static string TierText(SetBonusTier tier, GameData data, string setBonus)
    {
        if (tier == SetBonusTier.None) return "";
        var rank = data.SkillsByName.TryGetValue(setBonus, out var s) ? s.Ranks.FirstOrDefault(r => r.Level == (int)tier) : null;
        return $" -> {tier}{(rank?.Name is { } n ? $" ({n})" : "")}";
    }
}
