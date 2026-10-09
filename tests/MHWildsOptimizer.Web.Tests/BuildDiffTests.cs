using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Web.Tests;

public class BuildDiffTests
{
    private static readonly GameData Data = GameDataLoader.Load(GameDataLoader.FindDataDirectory());

    private static string RepoInputs => Path.Combine(Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName, "inputs");

    private static BuildInput Worn() => new()
    {
        Name = "Worn",
        WeaponDecorations = ["Attack Jewel III [3]", "Critical Jewel III [3]", null],
        Head = new BuildArmorInput { Piece = "Bale Burgeonet α", Decorations = ["Protection Jewel [1]", "Protection Jewel [1]"] },
        Chest = new BuildArmorInput { Piece = "Udra Miremail γ", Decorations = ["Challenger Jewel [3]", "Challenger Jewel [3]"] },
        Arms = new BuildArmorInput { Piece = "G. Fulgur Vambraces β", Transcended = true, Decorations = ["Chain Jewel [3]", "Tenderizer Jewel [3]"] },
        Waist = new BuildArmorInput { Piece = "Dahaad Shardcoil γ", Decorations = ["Protection Jewel [1]"] },
        Legs = new BuildArmorInput { Piece = "Udra Miregreaves γ", Decorations = ["Furor Jewel [2]"] },
        Talisman = new BuildTalismanInput { Name = "Secret Charm" },
    };

    private static List<BuildDto> Score(params BuildInput[] builds)
    {
        var path = Path.Combine(RepoInputs, "request.example.json");
        var request = RequestLoader.Read(path);
        var payload = new ConfigPayload(request, RequestFiles.LoadTalismansFor(request, path), builds.ToList());
        var resolved = Resolving.Resolve(payload, Data, RepoInputs);
        return BuildEvaluation.Evaluate(payload, resolved, Data).Select(e => e.Build!).ToList();
    }

    [Fact]
    public void A_build_does_not_differ_from_itself()
    {
        var b = Score(Worn())[0];
        var diff = BuildDiff.Between(b, b);
        Assert.True(diff.Identical);
        Assert.Empty(diff.Skills);
        Assert.Empty(diff.BonusesGained);
        Assert.Empty(diff.BonusesLost);
    }

    [Fact]
    public void Tells_another_piece_from_other_decorations_and_lists_the_skills_that_move()
    {
        var worn = Worn();
        var other = worn with
        {
            Name = "Other",
            // same chest, one Challenger Jewel swapped for a Tenderizer: decorations only
            Chest = worn.Chest! with { Decorations = ["Challenger Jewel [3]", "Tenderizer Jewel [3]"] },
            // other legs: Counterstrike 1, Agitator 1 instead of Counterstrike 1, Speed Eating 3, Resentment 2 (+1 from the Furor Jewel)
            Legs = new BuildArmorInput { Piece = "Blango Greaves α" },
            // the weapon's jewels in another order: the same
            WeaponDecorations = ["Critical Jewel III [3]", "Attack Jewel III [3]", null],
        };
        var scored = Score(worn, other);
        var diff = BuildDiff.Between(scored[0], scored[1]);

        Assert.Equal(SlotChange.Same, diff.Slots[BuildSlot.Weapon]);
        Assert.Equal(SlotChange.Decorations, diff.Slots[BuildSlot.Chest]);
        Assert.Equal(SlotChange.Piece, diff.Slots[BuildSlot.Legs]);
        Assert.Equal([BuildSlot.Legs], diff.Swapped);
        Assert.Equal([BuildSlot.Chest], diff.Regemmed);
        Assert.Contains(diff.Gains, s => s is { Skill: "Weakness Exploit", Delta: 1 });
        Assert.Contains(diff.Losses, s => s is { Skill: "Resentment", Delta: -3 });
        Assert.Contains(diff.Losses, s => s is { Skill: "Speed Eating", From: 3, To: 0 });
        // the Challenger Jewel's Agitator comes back with the legs
        Assert.DoesNotContain(diff.Skills, s => s.Skill == "Agitator");
        Assert.Contains(diff.BonusesLost, b => b.StartsWith("Nu Udra", StringComparison.Ordinal));
    }

    [Fact]
    public void Valued_skills_come_first()
    {
        var worn = Worn();
        var other = worn with { Name = "Other", Head = worn.Head! with { Decorations = [] }, Legs = worn.Legs! with { Decorations = [] } };
        var scored = Score(worn, other);
        var diff = BuildDiff.Between(scored[0], scored[1], valued: ["Resentment"]);
        Assert.Equal("Resentment", diff.Skills[0].Skill);
        Assert.Equal(-1, diff.Skills[0].Delta);
    }
}
