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
        Assert.Equal(3, r.TargetSkills["Focus"]);            // Great Sword core skill, a plain target
        Assert.Equal(5, r.TargetSkills.Count);
        Assert.Equal(1, r.Conditions.SkillLimits["Burst"]);
        Assert.Empty(r.AppliedCoreSkills);

        Assert.Equal(2, r.Talismans.Count(t => t.Source == TalismanSource.Random));
        Assert.Equal(TestData.Data.CraftableTalismans.Count, r.Talismans.Count(t => t.Source == TalismanSource.Crafted));

        Assert.True(r.Conditions.MonsterEnraged);
        Assert.False(r.Conditions.HittingWound);
        Assert.Equal(ResonanceMode.None, r.Conditions.Resonance);
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
        Assert.False(r.TargetSkills.ContainsKey("Quick Sheathe")); // core skills are plain targets now
        Assert.Equal(3, r.TargetSkills["Critical Eye"]);

        var core = RequestLoader.Resolve(request with { Options = new OptimizerOptions { RequireWeaponCoreSkills = true } }, TestData.Data, RepoRoot);
        Assert.Equal(3, core.TargetSkills["Quick Sheathe"]);  // the option still adds the Long Sword core skill
        Assert.Equal([("Quick Sheathe", 3)], core.AppliedCoreSkills);
    }

    [Fact]
    public void InvalidRequestsAreReportedNotThrown()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000, SetBonus = "Lord's Soul" },
            TargetSkills = new() { ["Nope"] = 1, ["Gogmapocalypse"] = 3, ["Attack Boost"] = 9, ["Burst"] = 5 },
            Conditions = Conditions.Default with { SkillLimits = new() { ["Burst"] = 1, ["Imaginary"] = 1, ["Agitator"] = 9, ["Gogmapocalypse"] = 3 }, AttackProfile = new AttackProfile { HitsPerMinute = 0, ChargedLv3Share = 1.5 } },
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
        Assert.Contains(r.Errors, e => e.Contains("'Gogmapocalypse' 3 is outside 0..2"));
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

    [Fact]
    public void ExcludedArmorLeavesOutSinglePieces()
    {
        var data = TestData.Data;
        var blango = data.Armor.Where(a => a.Set == "Blango α").ToList();
        var head = blango.Single(a => a.Piece == ArmorPieceKind.Head);
        var options = new OptimizerOptions { MinRarity = 5, ExcludeArmor = [head.Name] };

        var allowed = options.AllowedArmor(data.Armor);
        Assert.DoesNotContain(head, allowed);
        Assert.All(blango.Where(a => a != head), a => Assert.Contains(a, allowed));

        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
            Options = options with { ExcludeArmor = [head.Name, "Imaginary Helm"], IncludeArmor = [head.Name] },
        };
        var r = RequestLoader.Resolve(request, data, RepoRoot);
        Assert.Contains(r.Warnings, w => w.Contains("options.exclude_armor: no armor piece named 'Imaginary Helm'"));
        Assert.Contains(r.Warnings, w => w.Contains($"'{head.Name}' is excluded"));
    }

    [Fact]
    public void AllowedArmorRestrictsOnlyTheKindsItLists()
    {
        var data = TestData.Data;
        var blango = data.Armor.Where(a => a.Set == "Blango α").ToList();
        var head = blango.Single(a => a.Piece == ArmorPieceKind.Head);
        var chest = blango.Single(a => a.Piece == ArmorPieceKind.Chest);
        var options = new OptimizerOptions { MinRarity = 5, IncludeArmor = [head.Name, chest.Name] };

        var allowed = options.AllowedArmor(data.Armor);
        Assert.Equal([head], allowed.Where(a => a.Piece == ArmorPieceKind.Head));
        Assert.Equal([chest], allowed.Where(a => a.Piece == ArmorPieceKind.Chest));
        Assert.Equal(new OptimizerOptions { MinRarity = 5 }.AllowedArmor(data.Armor).Count(a => a.Piece == ArmorPieceKind.Legs),
                     allowed.Count(a => a.Piece == ArmorPieceKind.Legs));

        // the excluded sets still apply: excluding the set leaves no head, which is an error
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "great-sword", Attack = 1000 },
            Talismans = new TalismanSettings { IncludeCraftable = false },
            Options = options with { ExcludeSets = ["Blango α"], IncludeArmor = [head.Name, "Imaginary Helm"] },
        };
        var r = RequestLoader.Resolve(request, data, RepoRoot);
        Assert.Contains(r.Warnings, w => w.Contains($"'{head.Name}' is in an excluded set"));
        Assert.Contains(r.Warnings, w => w.Contains("no armor piece named 'Imaginary Helm'"));
        Assert.Contains(r.Errors, e => e.Contains("No head armor is left"));
        Assert.Empty(RequestLoader.Resolve(request with { Options = options }, data, RepoRoot).Errors);
    }

    [Fact]
    public void WeaponTypesStartWithTheirCoreSkillsAsTargets()
    {
        Assert.Equal([("Focus", 3), ("Stun Resistance", 3)], WeaponCoreSkills.StartingTargets("great-sword"));
        Assert.Equal([("Quick Sheathe", 3)], WeaponCoreSkills.StartingTargets("long-sword"));
        Assert.Empty(WeaponCoreSkills.StartingTargets("hammer"));
        Assert.Empty(WeaponCoreSkills.StartingTargets("no-such-kind"));
        Assert.Equal(new Dictionary<string, int> { ["Burst"] = 1 }, Conditions.DefaultFor("great-sword").SkillLimits);
        Assert.Empty(Conditions.DefaultFor("long-sword").SkillLimits);

        var none = new Dictionary<string, int>();
        Assert.Equal(new Dictionary<string, int> { ["Focus"] = 3, ["Stun Resistance"] = 3 }, WeaponCoreSkills.FollowType(none, null, "great-sword", none));
        // to Long Sword: the Great Sword's untouched starting targets go, a changed one stays
        var gs = new Dictionary<string, int> { ["Focus"] = 3, ["Stun Resistance"] = 2, ["Agitator"] = 5 };
        Assert.Equal(new Dictionary<string, int> { ["Stun Resistance"] = 2, ["Agitator"] = 5, ["Quick Sheathe"] = 3 },
            WeaponCoreSkills.FollowType(gs, "great-sword", "long-sword", none));
        // a limit below the starting level keeps it out
        Assert.Equal(new Dictionary<string, int> { ["Focus"] = 3 },
            WeaponCoreSkills.FollowType(none, "hammer", "great-sword", new Dictionary<string, int> { ["Stun Resistance"] = 0 }));
    }

    [Fact]
    public void TheOldCoreSkillOptionIsWrittenIntoTheTargets()
    {
        var legacy = RequestLoader.Read(Path.Combine(RepoRoot, "inputs", "request.example.json")) with
        {
            TargetSkills = new() { ["Agitator"] = 5 },
            Options = new OptimizerOptions { RequireWeaponCoreSkills = true },
        };
        var folded = WeaponCoreSkills.FoldLegacyOption(legacy);
        Assert.Equal(new Dictionary<string, int> { ["Agitator"] = 5, ["Focus"] = 3 }, folded.TargetSkills); // no Stun Resistance: it was never a core skill
        Assert.False(folded.Options.RequireWeaponCoreSkills);
        Assert.Same(folded, WeaponCoreSkills.FoldLegacyOption(folded));
        Assert.Equal(folded.TargetSkills, RequestLoader.Resolve(legacy, TestData.Data, RepoRoot).TargetSkills);

        var json = System.Text.Json.JsonSerializer.Serialize(folded, RequestFiles.WriteOptions);
        Assert.DoesNotContain("require_weapon_core_skills", json);
        Assert.Contains("\"require_weapon_core_skills\": true", System.Text.Json.JsonSerializer.Serialize(legacy, RequestFiles.WriteOptions));
    }
}
