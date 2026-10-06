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
        Assert.Equal(6, r.TargetSkills.Count);
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
            TargetSkills = new() { ["Nope"] = 1, ["Gogmapocalypse"] = 1, ["Attack Boost"] = 9 },
            Talismans = new TalismanSettings { File = "does-not-exist.json" },
            Options = new OptimizerOptions { TopN = 0, ExcludeSets = ["Imaginary Set"] },
        };
        var r = RequestLoader.Resolve(request, TestData.Data, RepoRoot);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("not a set-bonus skill"));
        Assert.Contains(r.Errors, e => e.Contains("'Nope'"));
        Assert.Contains(r.Errors, e => e.Contains("Gogmapocalypse"));
        Assert.Contains(r.Errors, e => e.Contains("Attack Boost"));
        Assert.Contains(r.Errors, e => e.Contains("does-not-exist.json"));
        Assert.Contains(r.Errors, e => e.Contains("top_n"));
        Assert.Contains(r.Warnings, w => w.Contains("Imaginary Set"));
    }
}
