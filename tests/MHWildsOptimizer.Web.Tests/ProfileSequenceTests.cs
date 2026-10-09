using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;

namespace MHWildsOptimizer.Web.Tests;

/// <summary>Named sequences live in the profile per weapon type, next to the condition presets.</summary>
public sealed class ProfileSequenceTests : IDisposable
{
    private readonly string _inputs = Path.Combine(Path.GetTempPath(), "mhwilds-sequences-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_inputs, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Sequences_are_saved_per_weapon_type_and_an_empty_list_drops_them()
    {
        var store = new ProfileStore(_inputs);
        store.CreateProfile("p");
        var offset = new SavedSequence { Name = "Offset TCS", Steps = [new SequenceStep { Attack = "offset-rising-slash" }, new SequenceStep { Attack = "true-charged-slash" }] };
        var loop = new SavedSequence { Name = "Loop", Steps = [new SequenceStep { Attack = "full-release-slash", Repeat = 2 }] };

        store.SaveSequences("p", "great-sword", [offset]);
        var profile = store.SaveSequences("p", "switch-axe", [loop]);
        Assert.Equal(["Offset TCS"], profile.Sequences!["great-sword"].Select(s => s.Name));
        Assert.Equal(loop.Steps, store.LoadProfile("p")!.Sequences!["switch-axe"].Single().Steps);

        // the presets stay as they were
        store.SavePreset("p", "great-sword", Conditions.Default with { MealAttack = 5 });
        Assert.Equal(2, store.LoadProfile("p")!.Sequences!.Count);

        Assert.False(store.SaveSequences("p", "great-sword", []).Sequences!.ContainsKey("great-sword"));
        Assert.Equal(5, store.LoadProfile("p")!.ConditionPresets["great-sword"].MealAttack);
    }
}
