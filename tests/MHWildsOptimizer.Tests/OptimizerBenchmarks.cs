using System.Diagnostics;
using System.Text.RegularExpressions;
using MHWildsOptimizer.Core.Build;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;
using Xunit.Abstractions;

namespace MHWildsOptimizer.Tests;

/// <summary>
/// Optimizer performance benchmarks on fixed, self-contained requests (no dependency on files in inputs/).
/// Each scenario does a small untimed warm-up search, then a timed one, and prints total and per-phase times.
/// The best score must not drop below the recorded baseline, so a speed-up cannot silently lose builds.
/// Run only these:  dotnet test --filter Category=Benchmark
/// Skip them:       dotnet test --filter Category!=Benchmark
/// Thread count:     set BENCH_THREADS (e.g. 1, 8); default is the request's max_threads (0 = all processors).
/// </summary>
[Trait("Category", "Benchmark")]
public partial class OptimizerBenchmarks(ITestOutputHelper output)
{
    private static string RepoRoot => Directory.GetParent(GameDataLoader.FindDataDirectory())!.FullName;

    /// <param name="MinBestScore">Baseline best score; a change may raise it but never lower it.</param>
    private sealed record Scenario(string Name, OptimizationRequest Request, double MinBestScore);

    private static readonly List<TalismanInput> Talismans =
    [
        new() { Name = "Historical Charm (Offensive Guard 2, Maximum Might 1, Counterstrike 1)", Rarity = 6, Skills = new() { ["Offensive Guard"] = 2, ["Maximum Might"] = 1, ["Counterstrike"] = 1 }, Slots = ["armor2", "armor1"] },
        new() { Name = "Historical Charm (Attack Boost 2, Maximum Might 2)", Rarity = 6, Skills = new() { ["Attack Boost"] = 2, ["Maximum Might"] = 2 }, Slots = ["armor1", "armor1"] },
        new() { Name = "Golden Age Charm (Attack Boost 3, Iron Skin 3)", Rarity = 8, Skills = new() { ["Attack Boost"] = 3, ["Iron Skin"] = 3 }, Slots = ["weapon1", "armor1"] },
        new() { Name = "Golden Age Charm (Focus 3, Fire Resistance 2, Defense Boost 2)", Rarity = 8, Skills = new() { ["Focus"] = 3, ["Fire Resistance"] = 2, ["Defense Boost"] = 2 }, Slots = ["weapon1", "armor1", "armor1"] },
        new() { Name = "Secret Charm (Attack Boost 3, Stun Resistance 2, Survival Expert 1)", Rarity = 7, Skills = new() { ["Attack Boost"] = 3, ["Stun Resistance"] = 2, ["Survival Expert"] = 1 }, Slots = ["armor1"] },
        new() { Name = "Secret Charm (Attack Boost 3, Burst 1)", Rarity = 7, Skills = new() { ["Attack Boost"] = 3, ["Burst"] = 1 }, Slots = ["armor1", "armor1"] },
        new() { Name = "Secret Charm (Focus 3, Stun Resistance 2, Hunger Resistance 1)", Rarity = 7, Skills = new() { ["Focus"] = 3, ["Stun Resistance"] = 2, ["Hunger Resistance"] = 1 }, Slots = ["armor1"] },
    ];

    private static WeaponStatsInput DragonGreatSword => new()
    {
        Spec = new GogmaWeaponSpecInput
        {
            Type = "great-sword", Focus = GogmaFocus.Attack, Element = Element.Dragon, Infused = true, AttackParts = 3,
            Reinforcements = ["sharpness EX", "attack III", "attack III", "affinity III", "affinity EX"],
        },
        SetBonus = "Soul of the Dark Knight",
        GroupSkill = "Lord's Favor",
    };

    private static Conditions RedHealthConditions => Conditions.AllOff with
    {
        MonsterEnraged = true, HittingWeakPoint = true, RedHealth = true, StaminaFull = true, FrenzyOvercome = true,
        BurstActive = true, CounterstrikeActive = true, AzureBoltActive = true, GutsNotYetTriggered = true, ProcDamage = true,
        SkillLimits = new() { ["Burst"] = 0 },
    };

    private static readonly Scenario[] All =
    [
        // barely constrained: the widest search (mirrors a typical "just give me damage" request)
        new Scenario("GS dragon, loose targets", new OptimizationRequest
        {
            Weapon = DragonGreatSword,
            TargetSkills = new() { ["Stun Resistance"] = 3 },
            Conditions = RedHealthConditions,
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 5, MinRarity = 5 },
        }, MinBestScore: 676.30),

        // many skill targets: feasibility pruning does most of the work
        new Scenario("GS dragon, heavy skill targets", new OptimizationRequest
        {
            Weapon = DragonGreatSword,
            TargetSkills = new() { ["Weakness Exploit"] = 5, ["Agitator"] = 5, ["Maximum Might"] = 3, ["Critical Boost"] = 5, ["Stun Resistance"] = 3 },
            Conditions = RedHealthConditions,
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 5, MinRarity = 5 },
        }, MinBestScore: 680.63),

        // required set bonus tier II on a raw weapon (the weapon counts as one of the four pieces)
        new Scenario("LS raw, set bonus target", new OptimizationRequest
        {
            Weapon = new WeaponStatsInput { Type = "long-sword", Attack = 660, Affinity = 10, SetBonus = "Gore Magala's Tyranny", GroupSkill = "Lord's Soul" },
            TargetSkills = new() { ["Gore Magala's Tyranny"] = 2, ["Weakness Exploit"] = 5 },
            Conditions = Conditions.AllOff with { HittingWeakPoint = true, MonsterEnraged = true, GutsNotYetTriggered = true, FrenzyOvercome = true, StaminaFull = true },
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 5, MinRarity = 5 },
        }, MinBestScore: 470.13),

        // every rollable skill pair, one search per score-equivalent class (smaller beam: 16 full searches otherwise take minutes)
        new Scenario("GS dragon, optimize skill pair", new OptimizationRequest
        {
            Weapon = DragonGreatSword,
            SkillPair = new SkillPairSettings { Mode = SkillPairMode.Optimize, TopN = 3 },
            TargetSkills = new() { ["Weakness Exploit"] = 5, ["Agitator"] = 3 },
            Conditions = RedHealthConditions,
            Talismans = new TalismanSettings { IncludeCraftable = true },
            Options = new OptimizerOptions { TopN = 3, MinRarity = 5, MaxStatesPerDepth = 20_000 },
        }, MinBestScore: 696.22),
    ];

    public static TheoryData<string> Scenarios => new(All.Select(s => s.Name));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Benchmark(string name)
    {
        var scenario = All.Single(s => s.Name == name);
        var data = TestData.Data;
        var resolved = RequestLoader.Resolve(scenario.Request, data, RepoRoot, Talismans);
        Assert.True(resolved.IsValid, string.Join("; ", resolved.Errors));
        if (int.TryParse(Environment.GetEnvironmentVariable("BENCH_THREADS"), out var threads))
            resolved = resolved with { Options = resolved.Options with { MaxThreads = threads } };

        // warm-up: JIT and data caches, on a tiny beam so it stays cheap
        var warmup = resolved with { Options = resolved.Options with { MaxStatesPerDepth = 1000 } };
        new Optimizer(data, warmup).Run();

        var progress = new LineProgress();
        var sw = Stopwatch.StartNew();
        var result = new Optimizer(data, resolved).Run(progress);
        sw.Stop();

        var lines = progress.Lines;
        var expandMs = lines.Sum(l => PhaseMs(ExpandRegex(), l));
        var pruneMs = lines.Sum(l => PhaseMs(PruneRegex(), l));
        var finalMs = lines.Sum(l => PhaseMs(FinalRegex(), l));
        var best = result.PairResults.Count == 0 ? 0 : result.PairResults.Max(p => p.BestScore);
        var states = result.PairResults.Sum(p => p.StatesEvaluated);

        output.WriteLine($"=== {scenario.Name} ({resolved.Options.EffectiveThreads} threads, {(System.Runtime.GCSettings.IsServerGC ? "server" : "workstation")} GC) ===");
        output.WriteLine($"total {sw.ElapsedMilliseconds} ms | expand {expandMs} ms, prune {pruneMs} ms, final {finalMs} ms (single-search phases)");
        output.WriteLine($"best {best:0.00} | {result.PairResults.Count} pair result(s) | {states} final states scored");
        output.WriteLine("--- progress ---");
        foreach (var line in lines) output.WriteLine(line);

        Assert.NotEmpty(result.PairResults);
        foreach (var pr in result.PairResults)
        {
            Assert.NotEmpty(pr.Builds);
            foreach (var b in pr.Builds)
            {
                Assert.Empty(b.Loadout.Validate(data));
                var skills = SkillAggregator.Aggregate(b.Loadout, data);
                foreach (var (skill, level) in resolved.TargetSkills)
                    Assert.True(skills.Level(skill) >= level, $"{skill} {skills.Level(skill)} < {level}");
            }
        }
        Assert.True(best >= scenario.MinBestScore - 0.05, $"best score {best:0.00} fell below the baseline {scenario.MinBestScore:0.00}");
    }

    private static long PhaseMs(Regex regex, string line) => regex.Match(line) is { Success: true } m ? long.Parse(m.Groups[1].Value) : 0;

    [GeneratedRegex(@"expand (\d+) ms")] private static partial Regex ExpandRegex();
    [GeneratedRegex(@"prune (\d+) ms")] private static partial Regex PruneRegex();
    [GeneratedRegex(@"final evaluation (\d+) ms")] private static partial Regex FinalRegex();

    /// <summary>Synchronous progress sink (<see cref="Progress{T}"/> posts to the thread pool and can reorder or drop lines at the end).</summary>
    private sealed class LineProgress : IProgress<string>
    {
        private readonly List<string> _lines = [];
        public void Report(string value) { lock (_lines) _lines.Add(value); }
        public IReadOnlyList<string> Lines { get { lock (_lines) return _lines.ToList(); } }
    }
}
