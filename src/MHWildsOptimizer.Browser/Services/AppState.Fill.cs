using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MudBlazor;

namespace MHWildsOptimizer.Browser.Services;

public enum FillStatus { Idle, Running, Done, Error, Cancelled }

/// <summary>
/// "Fill the rest" on My builds: the optimizer completes a hand-entered build around the pieces marked as kept. One at a
/// time, for the open weapon; the result is not saved anywhere, the user takes a completion into the build or drops it.
/// </summary>
/// <param name="Weapon">The session key of the weapon the fill ran for.</param>
/// <param name="ForBuild">The name of the build it ran for.</param>
/// <param name="Kept">The slots whose pieces had to stay.</param>
public sealed record FillState(FillStatus Status, string? Weapon, string? ForBuild, IReadOnlySet<BuildSlot> Kept, string? Stage,
    IReadOnlyList<BuildDto> Completions, string? Error, double Seconds)
{
    public static readonly FillState Idle = new(FillStatus.Idle, null, null, new HashSet<BuildSlot>(), null, [], null, 0);
}

public sealed partial class AppState
{
    private CancellationTokenSource? _fillCts;

    public FillState Fill { get; private set; } = FillState.Idle;

    public void CancelFill()
    {
        _fillCts?.Cancel();
        _fillCts = null;
        if (Fill.Status == FillStatus.Running) Fill = Fill with { Status = FillStatus.Cancelled };
        Notify();
    }

    public void DismissFill()
    {
        if (Fill.Status == FillStatus.Running) CancelFill();
        Fill = FillState.Idle;
        Notify();
    }

    /// <summary>
    /// The request a fill runs with: the weapon's, with the build's targets and conditions on top, the kept armor pieces as
    /// the only allowed ones of their kind (and their rarity and set allowed), the kept talisman as the only talisman, and the
    /// build's rolled pair when it overrides the weapon's. Jewels are the optimizer's everywhere: it has no per-slot lock.
    /// </summary>
    public (OptimizationRequest Request, List<TalismanInput> Talismans) FillRequest(BuildInput build, IReadOnlySet<BuildSlot> kept)
    {
        var request = Active!.Request;
        var options = request.Options;
        var include = options.IncludeArmor.ToList();
        var exclude = options.ExcludeSets.ToList();
        var excludeArmor = options.ExcludeArmor.ToList();
        var minRarity = options.MinRarity;
        foreach (var slot in kept)
        {
            if (!BuildDiff.ArmorSlots.TryGetValue(slot, out var kind)) continue;
            if (build.Armor(kind) is not { } a || !Index.ArmorByName.TryGetValue(a.Piece, out var p)) continue;
            include.RemoveAll(n => Index.ArmorByName.GetValueOrDefault(n)?.Kind == kind);
            include.Add(p.Name);
            exclude.Remove(p.Set);
            excludeArmor.Remove(p.Name);
            minRarity = Math.Min(minRarity, p.Rarity);
        }
        var talismans = Talismans.ToList();
        var talismanSettings = request.Talismans;
        if (kept.Contains(BuildSlot.Talisman) && build.Talisman is { } t)
        {
            var own = Talismans.FirstOrDefault(x => x.Name == t.Name);
            var charm = own is null ? Index.CharmsByName.GetValueOrDefault(t.Name) : null;
            var only = own
                ?? (charm is not null ? new TalismanInput { Name = charm.Name, Rarity = charm.Rarity, Skills = charm.Skills.ToDictionary(g => g.Skill, g => g.Level) } : null)
                ?? (t.Skills is not null ? new TalismanInput { Name = t.Name, Rarity = t.Rarity ?? 8, Skills = new(t.Skills), Slots = t.Slots?.ToList() ?? [] } : null);
            if (only is not null)
            {
                talismans = [only];
                talismanSettings = talismanSettings with { IncludeCraftable = false, IncludeAllPossible = false };
            }
        }
        var weapon = request.Weapon;
        var pair = request.SkillPair;
        if (build.SetBonus is not null || build.GroupSkill is not null)
        {
            weapon = weapon with { SetBonus = build.SetBonus ?? weapon.SetBonus, GroupSkill = build.GroupSkill ?? weapon.GroupSkill };
            pair = pair with { Mode = SkillPairMode.Fixed };
        }
        var filled = request with
        {
            Weapon = weapon,
            SkillPair = pair,
            TargetSkills = build.TargetsWith(request.TargetSkills),
            Conditions = build.ConditionsFor(request.Conditions),
            Talismans = talismanSettings,
            Options = options with { IncludeArmor = include, ExcludeSets = exclude, ExcludeArmor = excludeArmor, MinRarity = minRarity, TopN = 3 },
        };
        return (filled, talismans);
    }

    public async Task FillRestAsync(BuildInput build, IReadOnlySet<BuildSlot> kept)
    {
        if (Active is not { } s) return;
        if (_sessions.Values.Any(x => x.Run.Status == RunStatus.Running))
        {
            Toast("An optimizer run is going; wait for it or cancel it first.", Severity.Warning);
            return;
        }
        _fillCts?.Cancel();
        var cts = new CancellationTokenSource();
        _fillCts = cts;
        var keptCopy = kept.ToHashSet();
        void Set(Func<FillState, FillState> fn) { Fill = fn(Fill); Notify(); }

        Set(_ => FillState.Idle with { Status = FillStatus.Running, Weapon = s.Key, ForBuild = build.Name, Kept = keptCopy });
        var (request, talismans) = FillRequest(build, kept);
        var payload = new ConfigPayload(request, talismans);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await NextPaintAsync();
            cts.Token.ThrowIfCancellationRequested();
            await Optimization.RunAsync(payload, null, null, Data, _store, _solver, e =>
            {
                if (cts.IsCancellationRequested) return;
                switch (e)
                {
                    case ValidationEvent v when v.Errors.Count > 0:
                        Set(f => f with { Status = FillStatus.Error, Error = string.Join(" ", v.Errors), Seconds = clock.Elapsed.TotalSeconds });
                        break;
                    case StageEvent st:
                        Set(f => f with { Stage = st.Stage });
                        break;
                    case ResultEvent result:
                        Set(f => f with { Status = FillStatus.Done, Completions = result.Result.Pairs.FirstOrDefault()?.Builds ?? [], Seconds = clock.Elapsed.TotalSeconds });
                        break;
                    case ErrorEvent error when Fill.Status == FillStatus.Running:
                        Set(f => f with { Status = FillStatus.Error, Error = error.Message, Seconds = clock.Elapsed.TotalSeconds });
                        break;
                }
            }, cts.Token, Engine);
            if (Fill.Status == FillStatus.Running)
                Set(f => f with { Status = FillStatus.Error, Error = "The fill ended without a result.", Seconds = clock.Elapsed.TotalSeconds });
        }
        catch (OperationCanceledException)
        {
            // CancelFill has marked it
        }
        catch (Exception e)
        {
            if (!cts.IsCancellationRequested) Set(f => f with { Status = FillStatus.Error, Error = e.Message, Seconds = clock.Elapsed.TotalSeconds });
        }
        finally
        {
            if (_fillCts == cts) _fillCts = null;
        }
    }
}
