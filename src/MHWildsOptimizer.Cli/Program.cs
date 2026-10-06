using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

// Usage: MHWildsOptimizer.Cli [--data <dir>]
var dataDir = args.Length >= 2 && args[0] == "--data" ? args[1] : GameDataLoader.FindDataDirectory();
var data = GameDataLoader.Load(dataDir);
Console.WriteLine($"Loaded {data.Armor.Count} armor pieces, {data.Skills.Count} skills, {data.Decorations.Count} decorations, {data.MaxRankCharms.Count} charms from {dataDir}");
Console.WriteLine();

ArmorPiece Piece(string set, ArmorPieceKind kind) => data.Armor.First(a => a.Set == set && a.Piece == kind);
Decoration Deco(string name) => data.Decoration(name);

// Example: raw Great Sword that rolled Gore Magala's Tyranny + Lord's Soul, 3 Gore pieces + 2 gamma pieces.
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
    Weapon = new EquippedWeapon(weapon, [Deco("Critical Jewel III [3]"), Deco("Critical Jewel III [3]"), Deco("Expert Jewel III [3]")]),
    Head = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Head)),
    Chest = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Chest)),
    Arms = new EquippedArmor(Piece("Gore α", ArmorPieceKind.Arms)),
    Waist = new EquippedArmor(Piece("Rey Dau γ", ArmorPieceKind.Waist)),
    Legs = new EquippedArmor(Piece("Dahaad γ", ArmorPieceKind.Legs)),
    Charm = data.Charm("Challenger Charm III"),
};

var errors = loadout.Validate(data);
if (errors.Count > 0)
{
    Console.WriteLine("Loadout problems:");
    foreach (var e in errors) Console.WriteLine("  - " + e);
    Console.WriteLine();
}

var stats = weapon.Resolve(data);
Console.WriteLine($"Weapon: {stats.Type} / {stats.Focus} focus -> {stats.TrueRaw} true raw ({stats.DisplayAttack} display), {stats.Affinity}% affinity, {stats.TopSharpness} sharpness, slots [{string.Join(",", stats.Slots)}]");
Console.WriteLine($"        rolled: {weapon.SetBonus} + {weapon.GroupSkill}");
foreach (var piece in loadout.ArmorPieces)
    Console.WriteLine($"{piece.Piece.Piece,-6} {piece.Piece.Name,-28} slots [{string.Join(",", piece.EffectiveSlots)}]  set: {string.Join(" / ", piece.Piece.SetBonus)}  group: {piece.Piece.GroupSkill ?? "-"}");
Console.WriteLine($"Charm  {loadout.Charm?.Name}");
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
