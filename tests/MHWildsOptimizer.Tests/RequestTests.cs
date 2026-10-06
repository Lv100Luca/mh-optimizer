using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

public class RequestTests
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    [Fact]
    public void ExampleRequestResolvesTheRealWeapon()
    {
        var r = RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), TestData.Data);
        Assert.Empty(r.Errors);
        Assert.True(r.IsValid);

        var w = r.Weapon;
        Assert.Equal(WeaponType.GreatSword, w.Type);
        Assert.Equal(GogmaFocus.Attack, w.Focus);
        Assert.Equal(200 + 15 + 9 + 9, w.TrueRaw);          // attack focus 200, 3 attack parts, 2x Attack III
        Assert.Equal(-10 + 8 + 10, w.Affinity);              // attack focus -10, Affinity III + EX
        Assert.Equal(Element.Dragon, w.Element);
        Assert.Equal(450 + 30, w.ElementDisplay);            // base + infusion, attack focus adds nothing
        Assert.Equal(50, w.SharpnessBonus);
        Assert.Equal(SharpnessColor.White, w.TopSharpness);
        Assert.Equal("Soul of the Dark Knight", w.SetBonus);
        Assert.Equal("Lord's Favor", w.GroupSkill);

        Assert.Equal(SkillPairMode.Fixed, r.SkillPair.Mode);
        Assert.Single(r.SkillPairCandidates);
        Assert.Equal(new GogmaSkillPair("Soul of the Dark Knight", "Lord's Favor"), r.SkillPairCandidates[0]);

        Assert.Equal(5, r.TargetSkills["Weakness Exploit"]);
        Assert.Equal(3, r.TargetSkills["Focus"]);            // Great Sword core skill, added by default
        Assert.Equal(5, r.TargetSkills.Count);
        Assert.Equal(1, r.Conditions.SkillLimits["Burst"]);
        Assert.Equal([("Focus", 3)], r.AppliedCoreSkills);

        Assert.Equal(2, r.Talismans.Count(t => t.Source == TalismanSource.Random));
        Assert.Equal(TestData.Data.CraftableTalismans.Count, r.Talismans.Count(t => t.Source == TalismanSource.Crafted));

        Assert.True(r.Conditions.MonsterEnraged);
        Assert.False(r.Conditions.HittingWound);
        Assert.Equal(ResonanceMode.Local, r.Conditions.Resonance);
        Assert.True(r.Options.AllowTranscendence);
        Assert.Equal(5, r.Options.TopN);
    }

    [Fact]
    public void DirectStatsAndOptimizeMode()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 5, Element = Element.Thunder, ElementDisplay = 300 },
            SkillPair = new SkillPairSettings { Mode = SkillPairMode.Optimize, TopN = 5 },
            TargetSkills = new() { ["Critical Eye"] = 3 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
        };
        var r = RequestLoader.Resolve(request, TestData.Data, RepoRoot);
        Assert.Empty(r.Errors);
        Assert.Equal(WeaponType.LongSword, r.Weapon.Type);
        Assert.Null(r.Weapon.Focus);
        Assert.Equal(200, r.Weapon.TrueRaw);                 // 660 / 3.3
        Assert.Equal(30.0, r.Weapon.ElementTrue);
        Assert.Equal(294, r.SkillPairCandidates.Count);
        Assert.Contains(r.Warnings, w => w.Contains("No talismans"));
        Assert.Equal(3, r.TargetSkills["Quick Sheathe"]);    // Long Sword core skill
        Assert.Equal(3, r.TargetSkills["Critical Eye"]);

        var noCore = RequestLoader.Resolve(request with { Options = new OptimizerOptions { RequireWeaponCoreSkills = false } }, TestData.Data, RepoRoot);
        Assert.False(noCore.TargetSkills.ContainsKey("Quick Sheathe"));
        Assert.Empty(noCore.AppliedCoreSkills);
    }

    [Fact]
    public void InvalidRequestsAreReportedNotThrown()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000, SetBonus = "Lord's Soul" },
            TargetSkills = new() { ["Nope"] = 1, ["Gogmapocalypse"] = 3, ["Attack Boost"] = 9, ["Burst"] = 5 },
            Conditions = Conditions.Default with { SkillLimits = new() { ["Burst"] = 1, ["Imaginary"] = 1, ["Agitator"] = 9 }, AttackProfile = new AttackProfile { HitsPerMinute = 0, ChargedLv3Share = 1.5 } },
            Talismans = new TalismanSettings { File = "does-not-exist.json" },
            Options = new OptimizerOptions { TopN = 0, ExcludeSets = ["Imaginary Set"] },
        };
        var r = RequestLoader.Resolve(request, TestData.Data, RepoRoot);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("not a set-bonus skill"));
        Assert.Contains(r.Errors, e => e.Contains("'Nope'"));
        Assert.Contains(r.Errors, e => e.Contains("'Gogmapocalypse' level 3 is outside 1..2"));
        Assert.Contains(r.Errors, e => e.Contains("Attack Boost"));
        Assert.Contains(r.Errors, e => e.Contains("does-not-exist.json"));
        Assert.Contains(r.Errors, e => e.Contains("top_n"));
        Assert.Contains(r.Errors, e => e.Contains("'Burst' 5 is above its limit 1"));
        Assert.Contains(r.Errors, e => e.Contains("'Imaginary' is unknown"));
        Assert.Contains(r.Errors, e => e.Contains("'Agitator' 9 is outside"));
        Assert.Contains(r.Errors, e => e.Contains("hits_per_minute"));
        Assert.Contains(r.Errors, e => e.Contains("charged_lv3_share"));
        Assert.DoesNotContain(r.Errors, e => e.Contains("average_mv"));
        Assert.Contains(r.Warnings, w => w.Contains("Imaginary Set"));
    }

    [Fact]
    public void SetBonusesAndGroupSkillsAreRequiredByTier()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000, SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" },
            TargetSkills = new() { ["Gore Magala's Tyranny"] = 2, ["Lord's Soul"] = 1, ["Weakness Exploit"] = 3 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
        };
        var r = RequestLoader.Resolve(request, TestData.Data, RepoRoot);
        Assert.Empty(r.Errors);
        Assert.Equal(4, r.TargetSetBonuses["Gore Magala's Tyranny"]);   // tier II = 4 pieces
        Assert.Equal(3, r.TargetGroupSkills["Lord's Soul"]);            // 3 pieces
        Assert.Equal(3, r.TargetSkills["Weakness Exploit"]);
        Assert.False(r.TargetSkills.ContainsKey("Gore Magala's Tyranny"));
        Assert.False(r.TargetSkills.ContainsKey("Lord's Soul"));
        Assert.Contains("Gore Magala's Tyranny II (4 pieces)", r.TargetLabels);
        Assert.Contains("Lord's Soul (3 pieces)", r.TargetLabels);

        // Blangonga's Spirit sits on arms only at rarity 7+, so two pieces need the weapon to have rolled it
        var minRarity7 = new OptimizerOptions { MinRarity = 7 };
        var unreachable = RequestLoader.Resolve(request with { TargetSkills = new() { ["Blangonga's Spirit"] = 1 }, Options = minRarity7 }, TestData.Data, RepoRoot);
        Assert.Contains(unreachable.Errors, e => e.Contains("'Blangonga's Spirit' needs 2 pieces but only 1 can carry it"));

        var viaWeapon = request with { Weapon = request.Weapon with { SetBonus = "Blangonga's Spirit" }, TargetSkills = new() { ["Blangonga's Spirit"] = 1 }, Options = minRarity7 };
        Assert.Empty(RequestLoader.Resolve(viaWeapon, TestData.Data, RepoRoot).Errors);

        // in optimize mode any rollable pair may supply the piece
        var optimize = viaWeapon with { Weapon = request.Weapon, SkillPair = new SkillPairSettings { Mode = SkillPairMode.Optimize } };
        Assert.Empty(RequestLoader.Resolve(optimize, TestData.Data, RepoRoot).Errors);

        // at rarity 5+ the Blango sets carry it on every piece; excluding them leaves only the Gogmazios arms again
        var excluded = viaWeapon with { Weapon = request.Weapon, Options = new OptimizerOptions { ExcludeSets = ["Blango α", "Blango β"] } };
        Assert.Contains(RequestLoader.Resolve(excluded, TestData.Data, RepoRoot).Errors, e => e.Contains("'Blangonga's Spirit' needs 2 pieces but only 1 can carry it"));
        Assert.Empty(RequestLoader.Resolve(excluded with { Options = new OptimizerOptions() }, TestData.Data, RepoRoot).Errors);
    }
}
