using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

public class RequestFilesTests
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    [Fact]
    public void RequestAndTalismansRoundTripThroughJson()
    {
        var examplePath = Path.Combine(RepoRoot, "inputs", "request.example.json");
        var request = RequestLoader.Read(examplePath);
        var talismans = RequestFiles.LoadTalismansFor(request, examplePath);
        Assert.Equal(2, talismans.Count);

        var dir = Path.Combine(Path.GetTempPath(), "mhwo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "copy.json");
            var edited = request with
            {
                Conditions = request.Conditions with { HittingWound = true, Resonance = ResonanceMode.Remote },
                Talismans = new TalismanSettings { File = RequestFiles.DefaultTalismanFileName(path), IncludeCraftable = false },
                TargetSkills = new(request.TargetSkills) { ["Critical Eye"] = 2 },
            };
            RequestFiles.SaveRequest(edited, path);
            RequestFiles.SaveTalismans(talismans, RequestFiles.TalismanPathFor(edited, path)!);

            var reloaded = RequestLoader.Read(path);
            Assert.Equal("great-sword", reloaded.Weapon.Spec!.Type);
            Assert.Equal(GogmaFocus.Attack, reloaded.Weapon.Spec.Focus);
            Assert.Equal(Element.Dragon, reloaded.Weapon.Spec.Element);
            Assert.Equal(5, reloaded.Weapon.Spec.Reinforcements.Count);
            Assert.Equal("Soul of the Dark Knight", reloaded.Weapon.SetBonus);
            Assert.True(reloaded.Conditions.HittingWound);
            Assert.Equal(ResonanceMode.Remote, reloaded.Conditions.Resonance);
            Assert.Equal(2, reloaded.TargetSkills["Critical Eye"]);
            Assert.False(reloaded.Talismans.IncludeCraftable);
            Assert.Equal("copy.talismans.json", reloaded.Talismans.File);

            var reloadedTalismans = RequestFiles.LoadTalismansFor(reloaded, path);
            Assert.Equal(2, reloadedTalismans.Count);
            Assert.Equal(["weapon1"], reloadedTalismans.Single(t => t.Name == "Secret Charm").Slots);

            var resolved = RequestLoader.Load(path, TestData.Data);
            Assert.Empty(resolved.Errors);
            Assert.Equal(2, resolved.Talismans.Count);

            Assert.Equal([path], RequestFiles.ListRequests(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveCanUseUnsavedTalismans()
    {
        var request = new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660 },
            Talismans = new TalismanSettings { File = "missing.json", IncludeCraftable = false },
        };
        var unsaved = new List<TalismanInput> { new() { Name = "Draft", Rarity = 7, Skills = new() { ["Attack Boost"] = 3 }, Slots = ["weapon1"] } };
        var resolved = RequestLoader.Resolve(request, TestData.Data, RepoRoot, unsaved);
        Assert.Empty(resolved.Errors);
        Assert.Single(resolved.Talismans);
        Assert.Equal("Draft", resolved.Talismans[0].Name);
    }
}
