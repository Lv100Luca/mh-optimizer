using System.Globalization;
using MHWildsOptimizer.Cli.Interactive;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using Spectre.Console;

// Usage:
//   MHWildsOptimizer.Cli [edit [request.json]]   - interactive editor for a request (default command)
//   MHWildsOptimizer.Cli request <request.json>  - load + validate a request and show the resolved inputs
//   MHWildsOptimizer.Cli example                 - evaluate a hard-coded example loadout
//   (--data <dir> overrides the dataset directory, --inputs <dir> the configuration directory)
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var argList = args.ToList();
string? TakeOption(string name)
{
    var i = argList.IndexOf(name);
    if (i < 0 || i + 1 >= argList.Count) return null;
    var value = argList[i + 1];
    argList.RemoveRange(i, 2);
    return value;
}
var dataDir = TakeOption("--data") ?? GameDataLoader.FindDataDirectory();
var inputsDir = TakeOption("--inputs") ?? Path.Combine(Directory.GetParent(dataDir)!.FullName, "inputs");

var data = GameDataLoader.Load(dataDir);
AnsiConsole.MarkupLine($"[grey]Loaded {data.Armor.Count} armor pieces, {data.Skills.Count} skills, {data.Decorations.Count} decorations, {data.CraftableTalismans.Count} craftable charms, {data.GogmaSkillPairs.Count} Gogma skill pairs from {Markup.Escape(dataDir)}[/]");

var command = argList.Count > 0 ? argList[0] : "edit";
switch (command)
{
    case "edit":
        AnsiConsole.Console = new EscapeCancellingConsole(AnsiConsole.Console);
        try
        {
            var editor = ConfigEditor.Open(data, inputsDir, argList.Count > 1 ? argList[1] : null);
            editor.Run(inputsDir);
        }
        catch (PromptCancelledException) { }
        AnsiConsole.Cursor.Show();
        return 0;
    case "request":
        if (argList.Count < 2) { Console.Error.WriteLine("usage: request <request.json>"); return 2; }
        return ShowRequest(argList[1]);
    case "run":
        if (argList.Count < 2) { Console.Error.WriteLine("usage: run <request.json>"); return 2; }
        return RunRequest(argList[1]);
    case "example":
        RunExample();
        return 0;
    default:
        Console.Error.WriteLine($"unknown command '{command}'");
        return 2;
}

int RunRequest(string path)
{
    var r = RequestLoader.Load(path, data);
    foreach (var e in r.Errors) Console.WriteLine("ERROR   " + e);
    foreach (var w in r.Warnings) Console.WriteLine("warning " + w);
    if (!r.IsValid) return 1;

    var result = new MHWildsOptimizer.Core.Optimize.Optimizer(data, r).Run(new ConsoleProgress());
    var text = new ConfigEditor(data).RenderResults(result, r);
    ResultsRenderer.Write(result, r, data);
    var outPath = Path.ChangeExtension(path, null) + ".results.txt";
    File.WriteAllText(outPath, text);
    Console.WriteLine($"Written to {outPath} ({result.Elapsed.TotalSeconds:0.0} s)");
    return 0;
}

int ShowRequest(string path)
{
    var r = RequestLoader.Load(path, data);
    foreach (var e in r.Errors) Console.WriteLine("ERROR   " + e);
    foreach (var w in r.Warnings) Console.WriteLine("warning " + w);
    if (r.Errors.Count + r.Warnings.Count > 0) Console.WriteLine();

    var s = r.Weapon;
    Console.WriteLine($"Weapon: {s.Type}{(s.Focus is { } f ? $" / {f} focus" : "")} -> {s.TrueRaw} true raw ({s.DisplayAttack} display), {s.Affinity}% affinity, {s.ElementDisplay} {s.Element} (display), {s.TopSharpness} sharpness, slots [{string.Join(",", s.Slots)}]");
    Console.WriteLine($"Skill pair: {r.SkillPair.Mode} -> {r.SkillPairCandidates.Count} candidate(s)" +
                      (r.SkillPair.Mode == SkillPairMode.Fixed ? $": {s.SetBonus ?? "-"} + {s.GroupSkill ?? "-"}" : $", report top {r.SkillPair.TopN}"));
    Console.WriteLine("Targets: " + string.Join(", ", r.TargetLabels) +
                      (r.AppliedCoreSkills.Count > 0 ? $"  (weapon core skills added: {string.Join(", ", r.AppliedCoreSkills.Select(c => $"{c.Skill} {c.Level}"))})" : ""));
    Console.WriteLine($"Talismans: {r.Talismans.Count} ({r.Talismans.Count(t => t.Source == TalismanSource.Random)} random, {r.Talismans.Count(t => t.Source == TalismanSource.Crafted)} craftable)");
    foreach (var t in r.Talismans.Where(t => t.Source == TalismanSource.Random)) Console.WriteLine("  " + t);
    Console.WriteLine($"Options: transcendence={r.Options.AllowTranscendence}, top {r.Options.TopN}, min rarity {r.Options.MinRarity}, excluded sets [{string.Join(", ", r.Options.ExcludeSets)}]");
    Console.WriteLine($"Conditions: enraged={r.Conditions.MonsterEnraged} weakpoint={r.Conditions.HittingWeakPoint} wound={r.Conditions.HittingWound} fullHP={r.Conditions.FullHealth} redHP={r.Conditions.RedHealth} stamina={r.Conditions.StaminaFull} burst={r.Conditions.BurstActive} frenzy={r.Conditions.FrenzyOvercome} resonance={r.Conditions.Resonance}");
    Console.WriteLine();

    var bare = new Loadout { Weapon = new EquippedWeapon(s) };
    Console.WriteLine(LoadoutReport.Render(bare, data, r.Conditions, "weapon-only baseline"));
    return r.IsValid ? 0 : 1;
}

void RunExample()
{
    ArmorPiece Piece(string set, ArmorPieceKind kind) => data.Armor.First(a => a.Set == set && a.Piece == kind);
    MHWildsOptimizer.Core.Data.Decoration Deco(string name) => data.Decoration(name);

    // Raw Great Sword that rolled Gore Magala's Tyranny + Lord's Soul, 3 Gore pieces + 2 gamma pieces.
    var weapon = new GogmaWeaponSpec
    {
        Type = WeaponType.GreatSword,
        Focus = GogmaFocus.Attack,
        AttackParts = 3,
        Reinforcements =
        [
            new(ReinforcementType.Attack, ReinforcementTier.EX), new(ReinforcementType.Attack, ReinforcementTier.EX),
            new(ReinforcementType.Affinity, ReinforcementTier.EX), new(ReinforcementType.Affinity, ReinforcementTier.EX),
            new(ReinforcementType.Attack, ReinforcementTier.III),
        ],
        SetBonus = "Gore Magala's Tyranny",
        GroupSkill = "Lord's Soul",
    };

    var loadout = new Loadout
    {
        Weapon = new EquippedWeapon(weapon, data, [Deco("Critical Jewel III [3]"), Deco("Critical Jewel III [3]"), Deco("Expert Jewel III [3]")]),
        Head = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Head)),
        Chest = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Chest)),
        Arms = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Arms)),
        Waist = new EquippedArmor(Piece("Rey Dau γ", ArmorPieceKind.Waist)),
        Legs = new EquippedArmor(Piece("Dahaad γ", ArmorPieceKind.Legs)),
        Talisman = data.Charm("Challenger Charm III"),
    };

    var errors = loadout.Validate(data);
    if (errors.Count > 0)
    {
        Console.WriteLine("Loadout problems:");
        foreach (var e in errors) Console.WriteLine("  - " + e);
        Console.WriteLine();
    }

    Console.WriteLine(LoadoutReport.Render(loadout, data, Conditions.Default, "example build"));
}

sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string value) => Console.WriteLine(value);
}
