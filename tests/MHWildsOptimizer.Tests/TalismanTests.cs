using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

public class TalismanTests
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    [Fact]
    public void PoolIsLoadedFromTheDataset()
    {
        var pool = TestData.Data.TalismanPool;
        Assert.NotNull(pool);
        Assert.Equal(3, pool.MaxLevel("Attack Boost"));
        Assert.Equal(1, pool.MaxLevel("Agitator"));
        Assert.Equal(1, pool.MaxLevel("Weakness Exploit"));
        Assert.Equal(2, pool.MaxLevel("Maximum Might"));
        Assert.Null(pool.MaxLevel("Gogmapocalypse"));
        Assert.True(pool.IsKnownSlotPattern([new TalismanSlot(1, SkillKind.Weapon)]));
        Assert.True(pool.IsKnownSlotPattern([new TalismanSlot(1, SkillKind.Armor), new TalismanSlot(1, SkillKind.Armor)]));
        Assert.False(pool.IsKnownSlotPattern([new TalismanSlot(3, SkillKind.Weapon)]));
    }

    [Fact]
    public void ExampleTalismanFileParsesCleanly()
    {
        var path = Path.Combine(RepoRoot, "inputs", "talismans.example.json");
        var (talismans, errors, warnings) = TalismanInputLoader.Convert(TalismanInputLoader.Read(path), TestData.Data);
        Assert.Empty(errors);
        Assert.Empty(warnings);
        Assert.Equal(2, talismans.Count);

        var historical = talismans.Single(t => t.Name == "Historical Charm");
        Assert.Equal(6, historical.Rarity);
        Assert.Equal(TalismanSource.Random, historical.Source);
        Assert.Contains(historical.Skills, s => s.Skill == "Attack Boost" && s.Level == 2);
        Assert.Contains(historical.Skills, s => s.Skill == "Maximum Might" && s.Level == 2);
        Assert.Equal([new TalismanSlot(1, SkillKind.Armor), new TalismanSlot(1, SkillKind.Armor)], historical.Slots);

        var secret = talismans.Single(t => t.Name == "Secret Charm");
        Assert.Equal(3, secret.Skills.Count);
        Assert.Equal([new TalismanSlot(1, SkillKind.Weapon)], secret.Slots);
    }

    [Fact]
    public void ValidationFlagsImpossibleTalismans()
    {
        var data = TestData.Data;
        var c = TalismanInputLoader.Convert(new TalismanInput
        {
            Name = "Fake", Rarity = 7,
            Skills = new() { ["Agitator"] = 2, ["Gogmapocalypse"] = 1, ["Nope"] = 1, ["Attack Boost"] = 3 },
            Slots = ["weapon1", "x9"],
        }, data);
        Assert.NotNull(c.Talisman);
        Assert.Contains(c.Warnings, w => w.Contains("Agitator") && w.Contains("exceeds"));
        Assert.Contains(c.Errors, e => e.Contains("Gogmapocalypse"));
        Assert.Contains(c.Errors, e => e.Contains("Nope"));
        Assert.Contains(c.Errors, e => e.Contains("x9"));
        Assert.Equal(2, c.Talisman!.Skills.Count); // Agitator 2 and Attack Boost 3 survive, the invalid ones are dropped
    }

    [Fact]
    public void SlotNotationIsFlexible()
    {
        Assert.True(TalismanInputLoader.TryParseSlot("armor2", out var a) && a == new TalismanSlot(2, SkillKind.Armor));
        Assert.True(TalismanInputLoader.TryParseSlot("W3", out var w) && w == new TalismanSlot(3, SkillKind.Weapon));
        Assert.True(TalismanInputLoader.TryParseSlot("a 1", out _));
        Assert.False(TalismanInputLoader.TryParseSlot("armor4", out _));
        Assert.False(TalismanInputLoader.TryParseSlot("charm1", out _));
    }

    [Fact]
    public void TalismanSkillsAndDecorationsCountInTheLoadout()
    {
        var data = TestData.Data;
        var secret = new Talisman("Secret Charm", 7,
            [new SkillGrant("Attack Boost", data.Skill("Attack Boost").Id, 3)],
            [new TalismanSlot(1, SkillKind.Weapon)], TalismanSource.Random);

        var loadout = new Loadout
        {
            Weapon = new EquippedWeapon(new GogmaWeaponSpec { Type = WeaponType.GreatSword, Focus = GogmaFocus.Attack }, data),
            Talisman = new EquippedTalisman(secret, [data.Decoration("Expert Jewel [1]")]),
        };
        Assert.Empty(loadout.Validate(data));
        var skills = SkillAggregator.Aggregate(loadout, data);
        Assert.Equal(3, skills.Level("Attack Boost"));
        Assert.Equal(1, skills.Level("Critical Eye"));

        var wrongKind = new Loadout
        {
            Weapon = loadout.Weapon,
            Talisman = new EquippedTalisman(secret, [data.Decoration("Tenderizer Jewel [3]")]),
        };
        var errors = wrongKind.Validate(data);
        Assert.Contains(errors, e => e.Contains("Armor decoration"));
        Assert.Contains(errors, e => e.Contains("level 3 slot"));
    }

    [Fact]
    public void CraftableCharmsAreAvailableAsTalismans()
    {
        var data = TestData.Data;
        Assert.Equal(data.MaxRankCharms.Count, data.CraftableTalismans.Count);
        var challenger = data.CraftableTalismans.Single(t => t.Name == "Challenger Charm III");
        Assert.Equal(TalismanSource.Crafted, challenger.Source);
        Assert.Empty(challenger.Slots);
        Assert.Contains(challenger.Skills, s => s.Skill == "Agitator" && s.Level == 3);
    }
}
