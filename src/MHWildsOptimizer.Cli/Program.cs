using System.Globalization;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

// Usage:
//   MHWildsOptimizer.Cli example                      - evaluate a hard-coded example loadout
//   MHWildsOptimizer.Cli request <request.json>       - load + validate an optimizer request and show the resolved inputs
//   (--data <dir> overrides the dataset directory)
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var argList = args.ToList();
string? dataDir = null;
var dataIdx = argList.IndexOf("--data");
if (dataIdx >= 0 && dataIdx + 1 < argList.Count) { dataDir = argList[dataIdx + 1]; argList.RemoveRange(dataIdx, 2); }
dataDir ??= GameDataLoader.FindDataDirectory();

var data = GameDataLoader.Load(dataDir);
Console.WriteLine($"Loaded {data.Armor.Count} armor pieces, {data.Skills.Count} skills, {data.Decorations.Count} decorations, {data.CraftableTalismans.Count} craftable charms, {data.GogmaSkillPairs.Count} Gogma skill pairs from {dataDir}");
Console.WriteLine();

var command = argList.Count > 0 ? argList[0] : "example";
switch (command)
{
    case "request":
        if (argList.Count < 2) { Console.Error.WriteLine("usage: request <request.json>"); return 2; }
        return ShowRequest(argList[1]);
    case "example":
        RunExample();
        return 0;
    default:
        Console.Error.WriteLine($"unknown command '{command}'");
        return 2;
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
    Console.WriteLine("Targets: " + string.Join(", ", r.TargetSkills.Select(kv => $"{kv.Key} {kv.Value}")) +
                      (r.AppliedCoreSkills.Count > 0 ? $"  (weapon core skills added: {string.Join(", ", r.AppliedCoreSkills.Select(c => $"{c.Skill} {c.Level}"))})" : ""));
    Console.WriteLine($"Talismans: {r.Talismans.Count} ({r.Talismans.Count(t => t.Source == TalismanSource.Random)} random, {r.Talismans.Count(t => t.Source == TalismanSource.Crafted)} craftable)");
    foreach (var t in r.Talismans.Where(t => t.Source == TalismanSource.Random)) Console.WriteLine("  " + t);
    Console.WriteLine($"Options: transcendence={r.Options.AllowTranscendence}, top {r.Options.TopN}, min rarity {r.Options.MinRarity}, excluded sets [{string.Join(", ", r.Options.ExcludeSets)}]");
    Console.WriteLine($"Conditions: enraged={r.Conditions.MonsterEnraged} weakpoint={r.Conditions.HittingWeakPoint} wound={r.Conditions.HittingWound} fullHP={r.Conditions.FullHealth} redHP={r.Conditions.RedHealth} stamina={r.Conditions.StaminaFull} burst={r.Conditions.BurstActive} frenzy={r.Conditions.FrenzyOvercome} resonance={r.Conditions.Resonance}");
    Console.WriteLine();

    // weapon-only baseline so the numbers are comparable before the optimizer exists
    var bare = new Loadout { Weapon = new EquippedWeapon(s) };
    var result = DamageCalculator.Calculate(bare, data, r.Conditions);
    Console.WriteLine("Weapon-only baseline (no armor, no decorations):");
    foreach (var line in result.Breakdown) Console.WriteLine("  " + line);
    Console.WriteLine($"  EFR {result.EffectiveRaw:0.0} + EFE {result.EffectiveElement:0.0} = {result.Total:0.0}");
    return r.IsValid ? 0 : 1;
}

void RunExample()
{
    ArmorPiece Piece(string set, ArmorPieceKind kind) => data.Armor.First(a => a.Set == set && a.Piece == kind);
    Decoration Deco(string name) => data.Decoration(name);

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

    var stats = loadout.Weapon.Stats;
    Console.WriteLine($"Weapon: {stats.Type} / {stats.Focus} focus -> {stats.TrueRaw} true raw ({stats.DisplayAttack} display), {stats.Affinity}% affinity, {stats.TopSharpness} sharpness, slots [{string.Join(",", stats.Slots)}]");
    Console.WriteLine($"        rolled: {stats.SetBonus} + {stats.GroupSkill}");
    foreach (var piece in loadout.ArmorPieces)
        Console.WriteLine($"{piece.Piece.Piece,-6} {piece.Piece.Name,-28} slots [{string.Join(",", piece.EffectiveSlots)}]  set: {string.Join(" / ", piece.Piece.SetBonus)}  group: {piece.Piece.GroupSkill ?? "-"}");
    Console.WriteLine($"Talisman {loadout.Talisman?.Talisman}");
    Console.WriteLine();

    var skills = SkillAggregator.Aggregate(loadout, data);
    Console.WriteLine("Skills:");
    foreach (var (name, level) in skills.Levels.OrderBy(kv => data.Skill(kv.Key).Kind).ThenBy(kv => kv.Key))
        Console.WriteLine($"  {name,-24} Lv{level} ({data.Skill(name).Kind})");
    Console.WriteLine("Set bonuses: " + string.Join(", ", skills.SetBonusPieces.Select(kv => $"{kv.Key} {kv.Value}pc -> {skills.SetTier(kv.Key)}")));
    Console.WriteLine("Group skills: " + string.Join(", ", skills.GroupSkillPieces.Select(kv => $"{kv.Key} {kv.Value}pc -> {(skills.GroupActive(kv.Key) ? "active" : "inactive")}")));
    Console.WriteLine();

    foreach (var (label, cond) in new[] { ("default conditions", Conditions.Default), ("everything off", Conditions.AllOff) })
    {
        var result = DamageCalculator.Calculate(loadout, data, cond);
        Console.WriteLine($"== {label} ==");
        foreach (var line in result.Breakdown) Console.WriteLine("  " + line);
        Console.WriteLine($"  EFR {result.EffectiveRaw:0.0} + EFE {result.EffectiveElement:0.0} = {result.Total:0.0}");
        Console.WriteLine();
    }
}
