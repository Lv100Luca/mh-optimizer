using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace MHWildsOptimizer.Tests;

public class BuildInputTests
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    private static ResolvedRequest Example() => RequestLoader.Load(Path.Combine(RepoRoot, "inputs", "request.example.json"), TestData.Data);

    private static IReadOnlyList<Talisman> RandomTalismans(ResolvedRequest r) => r.Talismans.Where(t => t.Source == TalismanSource.Random).ToList();

    [Fact]
    public void OptimizerBuildsScoreTheSameWhenEnteredByHand()
    {
        var data = TestData.Data;
        var request = Example();
        request = request with { Options = request.Options with { MaxStatesPerDepth = 20_000, TopN = 3 } };
        var builds = new Optimizer(data, request).Run().AllBuilds.ToList();
        Assert.NotEmpty(builds);

        foreach (var b in builds)
        {
            var input = BuildInput.FromLoadout("copy", b.Loadout);
            var c = BuildInputLoader.Convert(input, request.Weapon, RandomTalismans(request), data);
            Assert.Empty(c.Errors);
            Assert.Empty(c.Warnings);
            Assert.Equal(b.Score, DamageCalculator.Calculate(c.Loadout, data, request.Conditions).Total, 6);
            Assert.Equal(b.Loadout.ArmorPieces.Select(a => (a.Piece.Name, a.Transcended)), c.Loadout.ArmorPieces.Select(a => (a.Piece.Name, a.Transcended)));
        }
    }

    [Fact]
    public void BuildEnteredByHandScoresWithTheRequestWeapon()
    {
        var data = TestData.Data;
        var request = Example();
        var input = new BuildInput
        {
            Name = "Worn",
            WeaponDecorations = ["Attack Jewel III [3]", "Critical Jewel III [3]", null],
            Head = new BuildArmorInput { Piece = "Bale Burgeonet α", Decorations = ["Protection Jewel [1]", "Protection Jewel [1]"] },
            Chest = new BuildArmorInput { Piece = "Udra Miremail γ", Decorations = ["Challenger Jewel [3]", "Challenger Jewel [3]"] },
            Arms = new BuildArmorInput { Piece = "G. Fulgur Vambraces β", Transcended = true, Decorations = ["Chain Jewel [3]", "Tenderizer Jewel [3]"] },
            Waist = new BuildArmorInput { Piece = "Dahaad Shardcoil γ", Decorations = ["Protection Jewel [1]"] },
            Legs = new BuildArmorInput { Piece = "Udra Miregreaves γ", Decorations = ["Furor Jewel [2]"] },
            Talisman = new BuildTalismanInput { Name = "Secret Charm", Decorations = ["Critical Jewel [1]"] },
        };

        var c = BuildInputLoader.Convert(input, request.Weapon, RandomTalismans(request), data);
        Assert.Empty(c.Errors);
        Assert.Empty(c.Warnings);
        Assert.True(c.Loadout.Arms!.Transcended);
        Assert.Equal([3, 3], c.Loadout.Arms.EffectiveSlots);
        Assert.Equal(TalismanSource.Random, c.Loadout.Talisman!.Talisman.Source);
        Assert.Equal("Soul of the Dark Knight", c.Loadout.Weapon.Stats.SetBonus);

        var skills = Core.Build.SkillAggregator.Aggregate(c.Loadout, data);
        Assert.Equal(5, skills.Level("Agitator")); // Dahaad Shardcoil γ 3 + 2x Challenger Jewel
        Assert.Equal(2, skills.SetBonusPieces["Nu Udra's Mutiny"]);
    }

    [Fact]
    public void UnknownNamesAndMisfitsAreReported()
    {
        var data = TestData.Data;
        var request = Example();
        var input = new BuildInput
        {
            Name = "Broken",
            SetBonus = "Lord's Soul", // a group skill, not a set bonus
            WeaponDecorations = ["Not A Jewel", "Challenger Jewel [3]"], // armor jewel in a weapon slot
            Head = new BuildArmorInput { Piece = "No Such Helm" },
            Chest = new BuildArmorInput { Piece = "Bale Burgeonet α" }, // a head piece
            Waist = new BuildArmorInput { Piece = "Dahaad Shardcoil γ", Transcended = true, Decorations = ["Challenger Jewel [3]"] }, // rarity 8: no transcended slots; level 1 slot
            Talisman = new BuildTalismanInput { Name = "Nobody's Charm" },
        };

        var c = BuildInputLoader.Convert(input, request.Weapon, RandomTalismans(request), data);
        Assert.Contains(c.Errors, e => e.Contains("Not A Jewel"));
        Assert.Contains(c.Errors, e => e.StartsWith("Weapon") && e.Contains("Challenger Jewel [3]"));
        Assert.Contains(c.Errors, e => e.Contains("No Such Helm"));
        Assert.Contains(c.Errors, e => e.StartsWith("Chest") && e.Contains("Head piece"));
        Assert.Contains(c.Errors, e => e.StartsWith("Waist") && e.Contains("level 3"));
        Assert.Contains(c.Errors, e => e.Contains("Nobody's Charm"));
        Assert.Contains(c.Errors, e => e.Contains("'Lord's Soul' is not a set-bonus skill"));
        Assert.Contains(c.Warnings, w => w.StartsWith("Waist") && w.Contains("no transcended slots"));
        Assert.False(c.Loadout.Waist!.Transcended);
        Assert.Null(c.Loadout.Head);
        Assert.Null(c.Loadout.Talisman);
    }

    [Fact]
    public void TalismansResolveToYourOwnFirstThenToCraftableCharms()
    {
        var data = TestData.Data;
        var request = Example();
        var charm = data.MaxRankCharms[0];
        var c = BuildInputLoader.Convert(new BuildInput { Name = "c", Talisman = new BuildTalismanInput { Name = charm.Name } }, request.Weapon, RandomTalismans(request), data);
        Assert.Empty(c.Errors);
        Assert.Equal(TalismanSource.Crafted, c.Loadout.Talisman!.Talisman.Source);
        Assert.Equal(charm.Skills, c.Loadout.Talisman.Talisman.Skills);

        var lowerRank = data.Charms.First(x => !x.IsMaxRank);
        Assert.Empty(BuildInputLoader.Convert(new BuildInput { Name = "c", Talisman = new BuildTalismanInput { Name = lowerRank.Name } }, request.Weapon, [], data).Errors);
    }

    [Fact]
    public void BuildRollOverridesTheWeaponPair()
    {
        var data = TestData.Data;
        var request = Example();
        var input = new BuildInput { Name = "pair", SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" };
        var c = BuildInputLoader.Convert(input, request.Weapon, [], data);
        Assert.Empty(c.Errors);
        Assert.Equal("Gore Magala's Tyranny", c.Loadout.Weapon.Stats.SetBonus);
        Assert.Equal("Lord's Soul", c.Loadout.Weapon.Stats.GroupSkill);
        Assert.Equal(request.Weapon.TrueRaw, c.Loadout.Weapon.Stats.TrueRaw);
    }

    [Fact]
    public void BuildsRoundTripNextToTheRequestFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mhwo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "mine.json");
            RequestFiles.SaveRequest(RequestLoader.Read(Path.Combine(RepoRoot, "inputs", "request.example.json")), path);
            var builds = new List<BuildInput>
            {
                new()
                {
                    Name = "Worn",
                    WeaponDecorations = ["Attack Jewel III [3]", null, null],
                    Arms = new BuildArmorInput { Piece = "G. Fulgur Vambraces β", Transcended = true, Decorations = [null, "Tenderizer Jewel [3]"] },
                    Talisman = new BuildTalismanInput { Name = "Secret Charm" },
                },
            };
            RequestFiles.SaveBuildsFor(builds, path);
            Assert.True(File.Exists(Path.Combine(dir, "mine.builds.json")));
            Assert.Equal([path], RequestFiles.ListRequests(dir));

            var loaded = Assert.Single(RequestFiles.LoadBuildsFor(path));
            Assert.Equal("Worn", loaded.Name);
            Assert.Equal(["Attack Jewel III [3]", null, null], loaded.WeaponDecorations);
            Assert.True(loaded.Arms!.Transcended);
            Assert.Equal([null, "Tenderizer Jewel [3]"], loaded.Arms.Decorations);
            Assert.Null(loaded.Head);
            Assert.Equal("Secret Charm", loaded.Talisman!.Name);

            RequestFiles.SaveBuildsFor([], path);
            Assert.False(File.Exists(Path.Combine(dir, "mine.builds.json")));
            Assert.Empty(RequestFiles.LoadBuildsFor(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
