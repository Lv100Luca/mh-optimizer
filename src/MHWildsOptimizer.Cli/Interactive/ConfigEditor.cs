using System.Reflection;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using Spectre.Console;

namespace MHWildsOptimizer.Cli.Interactive;

/// <summary>Interactive (Spectre.Console) editor for optimizer requests: weapon, skill pair, targets, conditions, talismans, options; load/save as JSON.</summary>
public sealed class ConfigEditor
{
    private const string None = "(none)";
    private readonly GameData _data;
    private OptimizationRequest _request;
    private List<TalismanInput> _talismans;
    private string? _path;
    private bool _dirty;

    public ConfigEditor(GameData data, OptimizationRequest? request = null, List<TalismanInput>? talismans = null, string? path = null)
    {
        _data = data;
        _request = request ?? NewRequest();
        _talismans = talismans ?? [];
        _path = path;
    }

    public static OptimizationRequest NewRequest() => new()
    {
        Weapon = new WeaponStatsInput
        {
            Spec = new GogmaWeaponSpecInput { Type = "great-sword", Focus = GogmaFocus.Attack, Element = Element.None, Infused = false, AttackParts = 3 },
        },
        Talismans = new TalismanSettings { IncludeCraftable = true },
    };

    /// <summary>Opens a request chosen from <paramref name="inputsDirectory"/> or a new one, then runs the editor loop.</summary>
    public static ConfigEditor Open(GameData data, string inputsDirectory, string? path)
    {
        if (path is null)
        {
            var files = RequestFiles.ListRequests(inputsDirectory);
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Which configuration?")
                .PageSize(15)
                .AddChoices(files.Select(Path.GetFileName).Append("New configuration")!));
            path = choice == "New configuration" ? null : Path.Combine(inputsDirectory, choice);
        }
        if (path is null) return new ConfigEditor(data);

        var request = RequestLoader.Read(path);
        var talismans = RequestFiles.LoadTalismansFor(request, path);
        AnsiConsole.MarkupLine($"Loaded [green]{Markup.Escape(path)}[/] ({talismans.Count} talismans).");
        return new ConfigEditor(data, request, talismans, path);
    }

    public void Run(string inputsDirectory)
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            ShowSummary();
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold]{Markup.Escape(_path is null ? "unsaved configuration" : Path.GetFileName(_path))}{(_dirty ? " *" : "")}[/] - what do you want to do?")
                .PageSize(14)
                .AddChoices("Weapon", "Skill pair", "Target skills", "Conditions", "Talismans", "Options", "Show resolved request", "Save", "Save as...", "Quit"));
            switch (choice)
            {
                case "Weapon": EditWeapon(); break;
                case "Skill pair": EditSkillPair(); break;
                case "Target skills": EditTargets(); break;
                case "Conditions": EditConditions(); break;
                case "Talismans": EditTalismans(); break;
                case "Options": EditOptions(); break;
                case "Show resolved request": ShowResolved(); break;
                case "Save": Save(inputsDirectory, saveAs: false); break;
                case "Save as...": Save(inputsDirectory, saveAs: true); break;
                case "Quit":
                    if (!_dirty || AnsiConsole.Confirm("Unsaved changes - quit anyway?", false)) return;
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- summary / resolved

    private void ShowSummary()
    {
        var r = _request;
        var w = r.Weapon;
        var table = new Table().Border(TableBorder.Rounded).HideHeaders().AddColumn("k").AddColumn("v");
        var weaponText = w.Spec is { } s
            ? $"{s.Type}, {s.Focus} focus, {s.Element}{(s.Infused ? " (infused)" : "")}, {s.AttackParts} attack parts, [{string.Join(", ", s.Reinforcements)}]"
            : $"{w.Type}, attack {w.Attack}{(w.AttackIsDisplay ? " (display)" : "")}, {w.Affinity}% aff, {w.Element} {w.ElementDisplay}, {w.Sharpness}";
        table.AddRow("Weapon", Markup.Escape(weaponText));
        table.AddRow("Rolled pair", Markup.Escape($"{w.SetBonus ?? None} / {w.GroupSkill ?? None}"));
        table.AddRow("Skill pair", Markup.Escape(r.SkillPair.Mode == SkillPairMode.Fixed ? "fixed (use the rolled pair)" : $"optimize over all 294 pairs, report top {r.SkillPair.TopN}"));
        table.AddRow("Targets", Markup.Escape(r.TargetSkills.Count == 0 ? None : string.Join(", ", r.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}"))));
        table.AddRow("Conditions", Markup.Escape(ConditionSummary(r.Conditions)));
        table.AddRow("Talismans", Markup.Escape($"{_talismans.Count} random{(r.Talismans.IncludeCraftable ? " + craftable charms" : "")}"));
        table.AddRow("Options", Markup.Escape($"transcendence {(r.Options.AllowTranscendence ? "on" : "off")}, top {r.Options.TopN}, min rarity {r.Options.MinRarity}, core skills {(r.Options.RequireWeaponCoreSkills ? "on" : "off")}, excluded sets {r.Options.ExcludeSets.Count}"));
        AnsiConsole.Write(table);
    }

    private static string ConditionSummary(Conditions c)
    {
        var on = BoolConditionProperties().Where(p => (bool)p.GetValue(c)!).Select(p => Label(p.Name)).ToList();
        return $"{on.Count} on ({string.Join(", ", on.Take(6))}{(on.Count > 6 ? ", ..." : "")}), resonance {c.Resonance}";
    }

    private void ShowResolved()
    {
        var baseDir = _path is null ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(Path.GetFullPath(_path))!;
        var resolved = RequestLoader.Resolve(_request, _data, baseDir, _talismans);
        foreach (var e in resolved.Errors) AnsiConsole.MarkupLine($"[red]error[/]   {Markup.Escape(e)}");
        foreach (var w in resolved.Warnings) AnsiConsole.MarkupLine($"[yellow]warning[/] {Markup.Escape(w)}");
        if (!resolved.IsValid) return;

        var s = resolved.Weapon;
        AnsiConsole.MarkupLine($"[green]Weapon[/] {s.Type}: {s.TrueRaw} true raw ({s.DisplayAttack} display), {s.Affinity}% affinity, {s.ElementDisplay} {s.Element}, {s.TopSharpness} sharpness, {s.SetBonus ?? None} / {s.GroupSkill ?? None}");
        AnsiConsole.MarkupLine($"[green]Targets[/] {Markup.Escape(string.Join(", ", resolved.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}")))}" +
                               (resolved.AppliedCoreSkills.Count > 0 ? $"  [grey](core: {Markup.Escape(string.Join(", ", resolved.AppliedCoreSkills.Select(c => $"{c.Skill} {c.Level}")))})[/]" : ""));
        AnsiConsole.MarkupLine($"[green]Skill pairs[/] {resolved.SkillPairCandidates.Count} candidate(s)   [green]Talismans[/] {resolved.Talismans.Count}");

        var bare = new Loadout { Weapon = new EquippedWeapon(s) };
        AnsiConsole.Write(new Panel(new Text(LoadoutReport.Render(bare, _data, resolved.Conditions))).Header("Weapon-only baseline").Expand());
    }

    // ---------------------------------------------------------------- weapon

    private void EditWeapon()
    {
        var mode = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("How do you want to enter the weapon?")
            .AddChoices("Describe it (Production Bonus + Reinforcement Bonus screens)", "Enter stats directly (attack, affinity, element, sharpness)", "Only change the rolled set bonus / group skill", "Back"));
        switch (mode)
        {
            case "Back": return;
            case "Only change the rolled set bonus / group skill":
                _request = _request with { Weapon = _request.Weapon with { SetBonus = PickSetBonus(_request.Weapon.SetBonus), GroupSkill = PickGroupSkill(_request.Weapon.GroupSkill) } };
                _dirty = true;
                return;
        }

        var types = Enum.GetValues<WeaponType>().Where(t => !t.IsGunner()).Select(t => t.ApiKind()).ToList();
        var current = _request.Weapon;
        var type = Pick("Weapon type", types, current.Spec?.Type ?? current.Type ?? "great-sword");

        if (mode.StartsWith("Describe"))
        {
            var spec = current.Spec ?? new GogmaWeaponSpecInput { Type = type, Focus = GogmaFocus.Attack };
            var focus = PickEnum("Focus", spec.Focus);
            var element = PickEnum("Element type", spec.Element);
            var infused = element != Element.None && AnsiConsole.Confirm("Element Infusion line present (all three parts share the element)?", spec.Infused);
            var attackParts = int.Parse(Pick("How many 'Attack Infusion +5' lines? (the others are Affinity Infusion)", ["0", "1", "2", "3"], spec.AttackParts.ToString()));

            var options = new List<string> { None };
            foreach (var rt in Enum.GetValues<ReinforcementType>())
            {
                if (rt == ReinforcementType.Ammo) continue;
                if (rt == ReinforcementType.Element && element == Element.None) continue;
                foreach (var tier in Enum.GetValues<ReinforcementTier>())
                    if (Core.Gogma.GogmaConstants.TierExists(rt, tier)) options.Add($"{rt.ToString().ToLowerInvariant()} {tier}");
            }
            var reinforcements = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var existing = i < spec.Reinforcements.Count ? spec.Reinforcements[i].ToLowerInvariant() : None;
                var def = options.FirstOrDefault(o => string.Equals(o, existing, StringComparison.OrdinalIgnoreCase)) ?? None;
                var pick = Pick($"Reinforcement slot {i + 1}", options, def);
                if (pick != None) reinforcements.Add(pick);
            }

            _request = _request with
            {
                Weapon = current with
                {
                    Spec = new GogmaWeaponSpecInput { Type = type, Focus = focus, Element = element, Infused = infused, AttackParts = attackParts, Reinforcements = reinforcements },
                    Type = null, Attack = null,
                    SetBonus = PickSetBonus(current.SetBonus), GroupSkill = PickGroupSkill(current.GroupSkill),
                },
            };
        }
        else
        {
            var attack = AnsiConsole.Prompt(new TextPrompt<int>("Attack as shown in game (display value):").DefaultValue(current.Attack ?? 1000));
            var affinity = AnsiConsole.Prompt(new TextPrompt<int>("Affinity %:").DefaultValue(current.Affinity));
            var element = PickEnum("Element", current.Element);
            var elementDisplay = element == Element.None ? 0 : AnsiConsole.Prompt(new TextPrompt<int>("Element value as shown in game:").DefaultValue(current.ElementDisplay));
            var sharpness = PickEnum("Sharpness color the weapon attacks at", current.Sharpness);
            _request = _request with
            {
                Weapon = current with
                {
                    Spec = null, Type = type, Attack = attack, AttackIsDisplay = true, Affinity = affinity, Element = element, ElementDisplay = elementDisplay, Sharpness = sharpness,
                    SetBonus = PickSetBonus(current.SetBonus), GroupSkill = PickGroupSkill(current.GroupSkill),
                },
            };
        }
        _dirty = true;
    }

    private string? PickSetBonus(string? current)
    {
        var names = _data.GogmaSkillPairs.Select(p => p.SetBonus).Distinct().OrderBy(n => n).ToList();
        var pick = Pick("Rolled set bonus", [None, .. names], current ?? None);
        return pick == None ? null : pick;
    }

    private string? PickGroupSkill(string? current)
    {
        var names = _data.GogmaSkillPairs.Select(p => p.GroupSkill).Distinct().OrderBy(n => n).ToList();
        var pick = Pick("Rolled group skill", [None, .. names], current ?? None);
        return pick == None ? null : pick;
    }

    // ---------------------------------------------------------------- skill pair / targets / conditions / options

    private void EditSkillPair()
    {
        var mode = PickEnum("Skill pair mode", _request.SkillPair.Mode);
        var topN = mode == SkillPairMode.Optimize
            ? AnsiConsole.Prompt(new TextPrompt<int>("How many of the best pairs to report?").DefaultValue(_request.SkillPair.TopN).Validate(v => v >= 1 ? ValidationResult.Success() : ValidationResult.Error("at least 1")))
            : _request.SkillPair.TopN;
        _request = _request with { SkillPair = new SkillPairSettings { Mode = mode, TopN = topN } };
        _dirty = true;
    }

    private void EditTargets()
    {
        var skills = _data.Skills.Where(s => s.Kind is SkillKind.Armor or SkillKind.Weapon).OrderBy(s => s.Kind).ThenBy(s => s.Name).ToList();
        var labels = skills.ToDictionary(s => $"{s.Name}  ({s.Kind.ToString().ToLowerInvariant()}, max {s.MaxLevel})", s => s);
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Target skills (space toggles, enter accepts)")
            .NotRequired()
            .PageSize(20)
            .MoreChoicesText("[grey](move up and down to reveal more skills)[/]")
            .AddChoices(labels.Keys);
        foreach (var kv in labels.Where(kv => _request.TargetSkills.ContainsKey(kv.Value.Name)))
            prompt.Select(kv.Key);
        var selected = AnsiConsole.Prompt(prompt).Select(l => labels[l]).ToList();

        var targets = new Dictionary<string, int>();
        foreach (var s in selected)
        {
            var def = _request.TargetSkills.TryGetValue(s.Name, out var lv) ? lv : s.MaxLevel;
            var level = AnsiConsole.Prompt(new TextPrompt<int>($"{Markup.Escape(s.Name)} minimum level (1-{s.MaxLevel}):")
                .DefaultValue(def)
                .Validate(v => v >= 1 && v <= s.MaxLevel ? ValidationResult.Success() : ValidationResult.Error($"1..{s.MaxLevel}")));
            targets[s.Name] = level;
        }
        _request = _request with { TargetSkills = targets };
        _dirty = true;
    }

    private void EditConditions()
    {
        var props = BoolConditionProperties();
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Active conditions (space toggles, enter accepts)")
            .NotRequired()
            .PageSize(20)
            .MoreChoicesText("[grey](move up and down to reveal more)[/]")
            .AddChoices(props.Select(p => Label(p.Name)));
        foreach (var p in props.Where(p => (bool)p.GetValue(_request.Conditions)!))
            prompt.Select(Label(p.Name));
        var on = AnsiConsole.Prompt(prompt).ToHashSet();

        var conditions = new Conditions();
        foreach (var p in props)
            p.SetValue(conditions, on.Contains(Label(p.Name)));
        typeof(Conditions).GetProperty(nameof(Conditions.Resonance))!.SetValue(conditions, PickEnum("Omega Resonance phase", _request.Conditions.Resonance));
        _request = _request with { Conditions = conditions };
        _dirty = true;
    }

    private void EditOptions()
    {
        var o = _request.Options;
        var transcendence = AnsiConsole.Confirm("Allow transcended slots on rarity 5/6 armor (HR 100+)?", o.AllowTranscendence);
        var core = AnsiConsole.Confirm("Require the weapon's core skills (GS Focus 3, LS Quick Sheathe 3)?", o.RequireWeaponCoreSkills);
        var topN = AnsiConsole.Prompt(new TextPrompt<int>("How many builds to report?").DefaultValue(o.TopN).Validate(v => v >= 1 ? ValidationResult.Success() : ValidationResult.Error("at least 1")));
        var minRarity = int.Parse(Pick("Minimum armor rarity", ["5", "6", "7", "8"], o.MinRarity.ToString()));

        var sets = _data.Armor.Where(a => a.Rarity >= minRarity).Select(a => a.Set!).Distinct().OrderBy(s => s).ToList();
        var prompt = new MultiSelectionPrompt<string>().Title("Armor sets to EXCLUDE (space toggles, enter accepts)").NotRequired().PageSize(20)
            .MoreChoicesText("[grey](move up and down to reveal more sets)[/]").AddChoices(sets);
        foreach (var s in o.ExcludeSets.Where(sets.Contains)) prompt.Select(s);
        var excluded = AnsiConsole.Prompt(prompt).ToList();

        _request = _request with { Options = new OptimizerOptions { AllowTranscendence = transcendence, RequireWeaponCoreSkills = core, TopN = topN, MinRarity = minRarity, ExcludeSets = excluded } };
        _dirty = true;
    }

    // ---------------------------------------------------------------- talismans

    private void EditTalismans()
    {
        while (true)
        {
            var table = new Table().Border(TableBorder.Rounded).AddColumn("Name").AddColumn("R").AddColumn("Skills").AddColumn("Slots");
            foreach (var t in _talismans)
                table.AddRow(Markup.Escape(t.Name), t.Rarity.ToString(), Markup.Escape(string.Join(", ", t.Skills.Select(kv => $"{kv.Key} {kv.Value}"))), Markup.Escape(string.Join(",", t.Slots)));
            AnsiConsole.Write(table);

            var choices = new List<string> { "Add talisman" };
            choices.AddRange(_talismans.Select(t => "Edit " + t.Name));
            choices.AddRange(_talismans.Select(t => "Remove " + t.Name));
            choices.Add($"Craftable charm lines: {(_request.Talismans.IncludeCraftable ? "included" : "excluded")} (toggle)");
            choices.Add("Back");
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Talismans").PageSize(15).AddChoices(choices.Select(Markup.Escape)));

            if (choice == "Back") return;
            if (choice.StartsWith("Craftable"))
            {
                _request = _request with { Talismans = _request.Talismans with { IncludeCraftable = !_request.Talismans.IncludeCraftable } };
                _dirty = true;
                continue;
            }
            if (choice == "Add talisman")
            {
                var t = EditTalisman(null);
                if (t is not null) { _talismans.Add(t); _dirty = true; }
                continue;
            }
            var name = Markup.Remove(choice[(choice.IndexOf(' ') + 1)..]);
            var idx = _talismans.FindIndex(t => t.Name == name);
            if (idx < 0) continue;
            if (choice.StartsWith("Remove "))
            {
                _talismans.RemoveAt(idx);
                _dirty = true;
            }
            else
            {
                var t = EditTalisman(_talismans[idx]);
                if (t is not null) { _talismans[idx] = t; _dirty = true; }
            }
        }
    }

    private TalismanInput? EditTalisman(TalismanInput? existing)
    {
        var pool = _data.TalismanPool;
        var name = AnsiConsole.Prompt(new TextPrompt<string>("Talisman name:").DefaultValue(existing?.Name ?? $"Talisman {_talismans.Count + 1}"));
        var rarity = int.Parse(Pick("Rarity", ["4", "5", "6", "7"], (existing?.Rarity ?? 7).ToString()));

        var skills = new Dictionary<string, int>();
        var primary = pool is null
            ? _data.Skills.Where(s => s.Kind == SkillKind.Weapon).Select(s => s.Name).OrderBy(n => n).ToList()
            : pool.Skills.Where(kv => kv.Value.Group == "primary").Select(kv => kv.Key).OrderBy(n => n).ToList();
        var secondary = pool is null
            ? _data.Skills.Where(s => s.Kind == SkillKind.Armor).Select(s => s.Name).OrderBy(n => n).ToList()
            : pool.Skills.Where(kv => kv.Value.Group == "secondary").Select(kv => kv.Key).OrderBy(n => n).ToList();

        var existingPrimary = existing?.Skills.Keys.FirstOrDefault(primary.Contains);
        var existingSecondary = existing?.Skills.Keys.Where(secondary.Contains).ToList() ?? [];

        var p = Pick("Skill 1 (weapon-kind skill; the first line on the talisman)", [None, .. primary], existingPrimary ?? None);
        if (p != None) skills[p] = AskLevel(p, existing?.Skills.GetValueOrDefault(p));
        for (var i = 0; i < 2; i++)
        {
            var def = i < existingSecondary.Count ? existingSecondary[i] : None;
            var s = Pick($"Skill {i + 2} (armor-kind skill, or none)", [None, .. secondary.Where(n => !skills.ContainsKey(n))], def);
            if (s == None) break;
            skills[s] = AskLevel(s, existing?.Skills.GetValueOrDefault(s));
        }

        var slotOptions = pool is null
            ? new List<string> { None, "armor1", "armor1,armor1", "armor1,armor1,armor1", "armor2", "armor2,armor1", "armor3", "weapon1", "weapon1,armor1", "weapon1,armor1,armor1" }
            : [None, .. pool.SlotPatterns.Where(sp => sp.Slots.Count > 0).Select(sp => string.Join(",", sp.Slots.Select(s => s.ToString())))];
        var currentSlots = existing is { Slots.Count: > 0 } ? string.Join(",", existing.Slots) : None;
        var slots = Pick("Decoration slots", slotOptions.Distinct().ToList(), slotOptions.Contains(currentSlots) ? currentSlots : None);

        return new TalismanInput { Name = name, Rarity = rarity, Skills = skills, Slots = slots == None ? [] : slots.Split(',').ToList() };

        int AskLevel(string skill, int? current)
        {
            var max = _data.Skill(skill).MaxLevel;
            var poolMax = pool?.MaxLevel(skill);
            var hint = poolMax is { } pm && pm < max ? $" (random talismans roll at most {pm})" : "";
            return AnsiConsole.Prompt(new TextPrompt<int>($"{Markup.Escape(skill)} level (1-{max}){Markup.Escape(hint)}:")
                .DefaultValue(current ?? Math.Min(max, poolMax ?? max))
                .Validate(v => v >= 1 && v <= max ? ValidationResult.Success() : ValidationResult.Error($"1..{max}")));
        }
    }

    // ---------------------------------------------------------------- save

    private void Save(string inputsDirectory, bool saveAs)
    {
        if (_path is null || saveAs)
        {
            var defaultName = _path is null ? "my-request" : Path.GetFileNameWithoutExtension(_path);
            var name = AnsiConsole.Prompt(new TextPrompt<string>("File name (saved under inputs/):").DefaultValue(defaultName));
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
            _path = Path.Combine(inputsDirectory, name);
        }

        var talismanFile = _request.Talismans.File ?? RequestFiles.DefaultTalismanFileName(_path);
        _request = _request with { Talismans = _request.Talismans with { File = talismanFile } };
        RequestFiles.SaveRequest(_request, _path);
        RequestFiles.SaveTalismans(_talismans, RequestFiles.TalismanPathFor(_request, _path)!);
        _dirty = false;
        AnsiConsole.MarkupLine($"Saved [green]{Markup.Escape(_path)}[/] and [green]{Markup.Escape(talismanFile)}[/].");
    }

    // ---------------------------------------------------------------- helpers

    private static string Pick(string title, IReadOnlyList<string> choices, string current)
    {
        var ordered = choices.Contains(current) ? choices.OrderBy(c => c == current ? 0 : 1).ToList() : choices.ToList();
        return AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"{Markup.Escape(title)} [grey](current: {Markup.Escape(current)})[/]")
            .PageSize(15)
            .MoreChoicesText("[grey](move up and down to reveal more)[/]")
            .AddChoices(ordered));
    }

    private static T PickEnum<T>(string title, T current) where T : struct, Enum
    {
        var names = Enum.GetNames<T>().Select(n => n.ToLowerInvariant()).ToList();
        var pick = Pick(title, names, current.ToString().ToLowerInvariant());
        return Enum.Parse<T>(pick, ignoreCase: true);
    }

    private static List<PropertyInfo> BoolConditionProperties() =>
        typeof(Conditions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
            .ToList();

    private static string Label(string propertyName) =>
        string.Concat(propertyName.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : ch.ToString()));
}
