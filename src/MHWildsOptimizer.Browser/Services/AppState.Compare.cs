using System.Runtime.CompilerServices;
using System.Text.Json;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// Comparing builds across the profile: any weapon's own builds and last-run builds looked up by <see cref="BuildRef"/> and
/// scored now (cached), the compare tray (the Compare tab's columns) and each weapon's baseline, the build its results and
/// builds are measured against.
/// </summary>
public sealed partial class AppState
{
    public const int TrayLimit = 5;

    // ---------------------------------------------------------------- the compare tray

    /// <summary>Builds collected for the Compare tab, in column order.</summary>
    public IReadOnlyList<BuildRef> Tray { get; private set; } = [];

    /// <summary>The column every difference is measured against: the chosen one while it is in the tray, else the first.</summary>
    public BuildRef? TrayBaseline => _trayBaseline is { } b && Tray.Contains(b) ? b : Tray.FirstOrDefault();

    public CompareBasis Basis { get; private set; } = CompareBasis.Own;

    private BuildRef? _trayBaseline;

    /// <summary>The tray as kept in browser storage, per profile.</summary>
    public sealed record TrayState(List<BuildRef> Refs, BuildRef? Baseline, CompareBasis Basis);

    private static string TrayKey(string profile) => $"mhwo.compare:{profile}";

    private void LoadTray(string profile)
    {
        TrayState? state;
        try { state = _storage.Get<TrayState>(TrayKey(profile), BrowserProfileStore.Json); }
        catch (JsonException) { state = null; }
        Tray = state?.Refs ?? [];
        _trayBaseline = state?.Baseline;
        Basis = state?.Basis ?? CompareBasis.Own;
    }

    private void SaveTray()
    {
        if (Profile is null) return;
        try
        {
            if (Tray.Count == 0) _storage.Remove(TrayKey(Profile));
            else _storage.Set(TrayKey(Profile), new TrayState(Tray.ToList(), _trayBaseline, Basis), BrowserProfileStore.Json);
        }
        catch (StorageFullException)
        {
            // the tray only lives in this page then
        }
    }

    private void SetTray(IReadOnlyList<BuildRef> refs)
    {
        Tray = refs;
        SaveTray();
        Notify();
    }

    public bool InTray(BuildRef r) => Tray.Contains(r);

    /// <summary>Adds a build to the tray or takes it out again.</summary>
    public void ToggleTray(BuildRef r)
    {
        if (Tray.Contains(r)) { SetTray(Tray.Where(x => x != r).ToList()); return; }
        if (Tray.Count >= TrayLimit) { Toast($"The comparison holds {TrayLimit} builds; take one out first.", MudBlazor.Severity.Warning); return; }
        SetTray([.. Tray, r]);
    }

    public void RemoveFromTray(BuildRef r) => SetTray(Tray.Where(x => x != r).ToList());

    public void ClearTray()
    {
        _trayBaseline = null;
        SetTray([]);
    }

    public void SetTrayBaseline(BuildRef r)
    {
        _trayBaseline = r;
        SaveTray();
        Notify();
    }

    /// <summary>Moves a column left (-1) or right (+1).</summary>
    public void MoveInTray(BuildRef r, int by)
    {
        var list = Tray.ToList();
        var i = list.IndexOf(r);
        var j = i + by;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        SetTray(list);
    }

    public void SetBasis(CompareBasis basis)
    {
        Basis = basis;
        SaveTray();
        Notify();
    }

    /// <summary>
    /// Opens the Compare tab with these builds in it (added to what the tray holds, the first one as the baseline);
    /// with none, just opens it.
    /// </summary>
    public void OpenCompare(params BuildRef[] refs)
    {
        if (refs.Length > 0)
        {
            var list = Tray.ToList();
            foreach (var r in refs.Where(r => !list.Contains(r))) list.Add(r);
            // the new ones win over older columns when the tray overflows
            while (list.Count > TrayLimit && list.FirstOrDefault(x => !refs.Contains(x)) is { } old) list.Remove(old);
            Tray = list;
            _trayBaseline = refs[0];
            SaveTray();
        }
        SetTab(Tab.Compare);
    }

    /// <summary>Opens a build where it lives: one of a weapon's own builds in My builds, a run build in Results.</summary>
    public void ShowBuild(BuildRef r)
    {
        if (r.Weapon == NewWeapon) { _activeKey = NewWeapon; ShowCached(); }
        else if (Profile is null || !ActivateWeapon(Profile, r.Weapon)) return;
        if (!r.IsRun && Builds.ToList().FindIndex(b => b.Name == r.Mine) is >= 0 and var index)
            Update(_activeKey, s => s with { BuildIndex = index });
        _pushNext = true;
        Tab = r.IsRun ? Tab.Results : Tab.Builds;
        Moved();
    }

    // ---------------------------------------------------------------- baselines

    private static string BaselineKey(string profile, string weapon) => $"mhwo.baseline:{profile}:{weapon}";

    /// <summary>Each weapon's chosen baseline as stored (null = never chosen), read once per profile.</summary>
    private readonly Dictionary<string, BuildRef?> _baselines = new();

    private BuildRef? StoredBaseline(string weapon)
    {
        if (Profile is null) return null;
        if (_baselines.TryGetValue(weapon, out var known)) return known;
        BuildRef? stored;
        try { stored = BuildRef.Parse(_storage.GetString(BaselineKey(Profile, weapon))); }
        catch (Exception) { stored = null; }
        return _baselines[weapon] = stored;
    }

    /// <summary>
    /// What a weapon's results and builds are measured against: the build chosen for it while it exists (it can be another
    /// weapon's), else the weapon's picked build, else its first build. Drafts are never a default.
    /// </summary>
    public BuildRef? BaselineOf(string weapon)
    {
        if (StoredBaseline(weapon) is { } stored && Exists(stored)) return stored;
        var builds = BuildsOf(weapon);
        var b = builds.FirstOrDefault(x => x.Picked && x.DraftOf is null) ?? builds.FirstOrDefault(x => x.DraftOf is null);
        return b is null ? null : BuildRef.Own(weapon, b.Name);
    }

    /// <summary>The open weapon's baseline.</summary>
    public BuildRef? Baseline => BaselineOf(_activeKey);

    /// <summary>Chooses the open weapon's baseline (null = back to the default).</summary>
    public void SetBaseline(BuildRef? r)
    {
        if (Profile is null) return;
        _baselines[_activeKey] = r;
        try
        {
            if (r is null) _storage.Remove(BaselineKey(Profile, _activeKey));
            else _storage.SetString(BaselineKey(Profile, _activeKey), r.Key);
        }
        catch (StorageFullException)
        {
            // kept for this page only
        }
        Notify();
    }

    /// <summary>A build of the open weapon was renamed: the tray, baselines and drafts that point at it follow.</summary>
    public void BuildRenamed(string from, string to)
    {
        if (from == to) return;
        BuildRef Follow(BuildRef r) => r.Mine == from && r.Weapon == _activeKey ? r with { Mine = to } : r;
        Tray = Tray.Select(Follow).ToList();
        if (_trayBaseline is { } tb) _trayBaseline = Follow(tb);
        SaveTray();
        foreach (var (weapon, b) in _baselines.ToList())
            if (b is not null && Follow(b) != b)
            {
                _baselines[weapon] = Follow(b);
                if (Profile is not null) _storage.SetString(BaselineKey(Profile, weapon), Follow(b).Key);
            }
    }

    // ---------------------------------------------------------------- looking builds up

    /// <summary>Saved weapons and last runs as read from storage, for weapons not open in this page; dropped on every save.</summary>
    private readonly Dictionary<string, WeaponDto?> _savedWeapons = new();
    private readonly Dictionary<string, ResultDto?> _savedResults = new();

    private void ForgetSaved()
    {
        _savedWeapons.Clear();
        _savedResults.Clear();
    }

    private WeaponDto? SavedWeapon(string weapon)
    {
        if (Profile is null || weapon == NewWeapon) return null;
        if (_savedWeapons.TryGetValue(weapon, out var known)) return known;
        WeaponDto? file;
        try { file = _store.LoadWeapon(Profile, weapon); }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException) { file = null; }
        return _savedWeapons[weapon] = file;
    }

    /// <summary>The weapon's request: as open in this page (unsaved edits included), else as saved.</summary>
    public OptimizationRequest? RequestOf(string weapon) => _sessions.GetValueOrDefault(weapon)?.Request ?? SavedWeapon(weapon)?.Request;

    /// <summary>The weapon's own builds: as open in this page, else as saved.</summary>
    public IReadOnlyList<BuildInput> BuildsOf(string weapon) => _sessions.GetValueOrDefault(weapon)?.Builds ?? SavedWeapon(weapon)?.Builds ?? [];

    /// <summary>The weapon's last run: the one shown in this page, else the saved one.</summary>
    public ResultDto? ResultsOf(string weapon)
    {
        if (_sessions.GetValueOrDefault(weapon) is { ResultsPending: false } s) return s.Run.Result;
        if (Profile is null || weapon == NewWeapon) return null;
        if (_savedResults.TryGetValue(weapon, out var known)) return known;
        return _savedResults[weapon] = TryLoadResults(Profile, weapon);
    }

    /// <summary>Whether the weapon's last run was made with another request or other talismans than it has now.</summary>
    public bool IsStale(string weapon)
    {
        if (ResultsOf(weapon) is not { } results || RequestOf(weapon) is not { } request) return false;
        return results.InputsHash is not { } hash || hash != InputsHash(request);
    }

    // requests and talisman lists are replaced on every edit, never changed, so what is worked out from one is kept with it
    private readonly ConditionalWeakTable<OptimizationRequest, string> _hashes = new();
    private IReadOnlyList<TalismanInput>? _hashTalismans;

    private string InputsHash(OptimizationRequest request)
    {
        if (!ReferenceEquals(_hashTalismans, Talismans)) { _hashes.Clear(); _hashTalismans = Talismans; }
        return _hashes.GetValue(request, r => ProfileRules.InputsHash(r, Talismans));
    }

    public BuildDto? RunBuild(BuildRef r) =>
        r.IsRun ? ResultsOf(r.Weapon)?.Pairs.FirstOrDefault(p => p.Rank == r.Pair)?.Builds.FirstOrDefault(b => b.Rank == r.Rank) : null;

    private bool Exists(BuildRef r) => r.IsRun ? RunBuild(r) is not null : BuildsOf(r.Weapon).Any(b => b.Name == r.Mine);

    /// <summary>The name of a weapon session key ("New weapon" for the unsaved one).</summary>
    public static string WeaponTitle(string weapon) => weapon == NewWeapon ? "New weapon" : weapon;

    /// <summary>"In game", "Run build 3" or, with several skill pairs, "Pair #2 build 1".</summary>
    public string RefName(BuildRef r) =>
        !r.IsRun ? r.Mine! : (ResultsOf(r.Weapon)?.Pairs.Count ?? 1) > 1 ? $"Pair #{r.Pair} build {r.Rank}" : $"Run build {r.Rank}";

    /// <summary>"Current · In game".</summary>
    public string RefLabel(BuildRef r) => $"{WeaponTitle(r.Weapon)} · {RefName(r)}";

    /// <summary>The weapons builds can come from: the saved ones and the new one when it has builds or a run.</summary>
    public IReadOnlyList<string> CompareWeapons
    {
        get
        {
            var list = Weapons.Select(w => w.Name).ToList();
            if (_sessions.GetValueOrDefault(NewWeapon) is { } n && (n.Builds.Count > 0 || n.Run.Result is not null)) list.Add(NewWeapon);
            return list;
        }
    }

    /// <summary>A weapon's builds that can be compared: its own (drafts too), then its last run's.</summary>
    public IReadOnlyList<BuildRef> RefsOf(string weapon)
    {
        var refs = BuildsOf(weapon).Select(b => BuildRef.Own(weapon, b.Name)).Distinct().ToList();
        foreach (var p in ResultsOf(weapon)?.Pairs ?? [])
            refs.AddRange(p.Builds.Select(b => BuildRef.Run(weapon, p.Rank, b.Rank)));
        return refs;
    }

    // ---------------------------------------------------------------- scoring

    private const int CacheLimit = 600;
    private readonly Dictionary<string, ResolvedRequest> _resolvedRequests = new();
    private readonly Dictionary<string, EvaluatedBuildDto?> _scores = new();
    private readonly Dictionary<string, IReadOnlyList<AttackRowDto>> _attackRows = new();
    private IReadOnlyList<TalismanInput>? _scoredTalismans;

    /// <summary>The caches hold scores for one talisman pool; a pool change (or a lot of editing) starts them over.</summary>
    private void CheckScoringCaches()
    {
        if (ReferenceEquals(_scoredTalismans, Talismans) && _scores.Count + _attackRows.Count < CacheLimit && _resolvedRequests.Count < 100) return;
        _resolvedRequests.Clear();
        _scores.Clear();
        _attackRows.Clear();
        _scoredTalismans = Talismans;
    }

    /// <summary>JSON of requests and builds, kept with the object: the cache keys of the scores.</summary>
    private readonly ConditionalWeakTable<object, string> _json = new();

    private string Json(object o) => _json.GetValue(o, x => JsonSerializer.Serialize(x, x.GetType(), BrowserProfileStore.Json));

    // the same derived request or build for the same inputs, so their JSON (and score) is found again
    private readonly ConditionalWeakTable<OptimizationRequest, ConditionalWeakTable<Conditions, OptimizationRequest>> _withConditions = new();
    private readonly ConditionalWeakTable<Conditions, Conditions> _allOn = new();
    private readonly ConditionalWeakTable<BuildInput, ConditionalWeakTable<BuildInput, BuildInput>> _likeInputs = new();
    private readonly ConditionalWeakTable<BuildInput, BuildInput> _withoutOwn = new();
    private readonly ConditionalWeakTable<BuildDto, BuildInput> _runInputs = new();

    private OptimizationRequest WithConditions(OptimizationRequest r, Conditions c) =>
        ReferenceEquals(r.Conditions, c) ? r : _withConditions.GetOrCreateValue(r).GetValue(c, x => r with { Conditions = x });

    /// <summary>The build with <paramref name="of"/>'s own conditions instead of its own.</summary>
    private BuildInput WithConditionsOf(BuildInput input, BuildInput of) => !input.HasOwnConditions && !of.HasOwnConditions
        ? input
        : _likeInputs.GetOrCreateValue(input).GetValue(of, b => input with { Conditions = new(b.Conditions), Resonance = b.Resonance, Target = b.Target, MealAttack = b.MealAttack });

    private BuildInput WithoutOwn(BuildInput input) => input.HasOwnConditions ? _withoutOwn.GetValue(input, i => i.WithoutOwnConditions()) : input;

    private (ResolvedRequest Resolved, string Key) ResolveFor(OptimizationRequest request)
    {
        CheckScoringCaches();
        var key = Json(request);
        if (!_resolvedRequests.TryGetValue(key, out var resolved))
            _resolvedRequests[key] = resolved = MHWildsOptimizer.Api.Resolving.Resolve(new ConfigPayload(request, Talismans.ToList()), Data, _store.Directory);
        return (resolved, key);
    }

    /// <summary>A build scored under a request (any weapon's), cached; null when the weapon cannot be scored at all.</summary>
    public EvaluatedBuildDto? Score(OptimizationRequest request, BuildInput input)
    {
        try
        {
            var (resolved, requestKey) = ResolveFor(request);
            var key = requestKey + "\n" + Json(input);
            if (_scores.TryGetValue(key, out var cached)) return cached;
            var result = BuildEvaluation.Evaluate(new ConfigPayload(request, Talismans.ToList(), [input]), resolved, Data).FirstOrDefault();
            _scores[key] = result;
            return result;
        }
        catch (Exception e)
        {
            return new EvaluatedBuildDto(input.Name, [$"Scoring failed: {e.Message}"], [], null, []);
        }
    }

    /// <summary>A build's damage on every attack of its weapon type, cached; empty when it cannot be scored.</summary>
    public IReadOnlyList<AttackRowDto> AttackRowsOf(OptimizationRequest request, BuildInput input)
    {
        try
        {
            var (resolved, requestKey) = ResolveFor(request);
            var key = requestKey + "\n" + Json(input);
            if (_attackRows.TryGetValue(key, out var cached)) return cached;
            return _attackRows[key] = BuildEvaluation.AttackBreakdown(resolved, input, Data);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Looks a build of the profile up and scores it now. Under <see cref="CompareBasis.Baseline"/> it takes
    /// <paramref name="baseline"/>'s conditions (weapons of the same type only); under <see cref="CompareBasis.AllOn"/> every
    /// hunt condition is on. Null when the build is gone.
    /// </summary>
    public ScoredRef? ScoreRef(BuildRef r, CompareBasis basis = CompareBasis.Own, ScoredRef? baseline = null)
    {
        if (Lookup(r) is not { } found) return null;
        var (request, input, runScore, runAt, stale) = found;
        var type = ProfileRules.WeaponKind(request);
        var applied = false;
        switch (basis)
        {
            case CompareBasis.AllOn:
                request = WithConditions(request, _allOn.GetValue(input.ConditionsFor(request.Conditions), Conditions.AllOnLike));
                input = WithoutOwn(input);
                applied = true;
                break;
            case CompareBasis.Baseline when baseline is not null && baseline.Ref != r && baseline.WeaponType == type:
                request = WithConditions(request, baseline.Request.Conditions);
                input = WithConditionsOf(input, baseline.Input);
                applied = true;
                break;
        }
        return new ScoredRef(r, RefName(r), WeaponTitle(r.Weapon), type, request, input, Score(request, input), !r.IsRun && input.Picked, runScore, runAt, stale, applied);
    }

    /// <summary>
    /// A build scored under <paramref name="conditionsOf"/>'s conditions without its own overrides, the way Results scores the
    /// open weapon's run. Null when the build is gone or belongs to a weapon of another type.
    /// </summary>
    public ScoredRef? ScoreUnder(BuildRef r, OptimizationRequest conditionsOf)
    {
        if (Lookup(r) is not { } found) return null;
        var (request, input, runScore, runAt, stale) = found;
        var type = ProfileRules.WeaponKind(request);
        if (type != ProfileRules.WeaponKind(conditionsOf)) return null;
        var applied = input.HasOwnConditions || !ReferenceEquals(request.Conditions, conditionsOf.Conditions);
        request = WithConditions(request, conditionsOf.Conditions);
        input = WithoutOwn(input);
        return new ScoredRef(r, RefName(r), WeaponTitle(r.Weapon), type, request, input, Score(request, input), !r.IsRun && input.Picked, runScore, runAt, stale, applied);
    }

    /// <summary>A build of the open weapon's last run as Results shows it: as the run scored it, or scored again when the run is stale.</summary>
    public (BuildDto Build, bool Rescored) RunBuildNow(int pair, BuildDto b)
    {
        if (!IsStale(_activeKey) && b.StatsRequested.DamagePerExecution > 0) return (b, false);
        return ScoreRef(BuildRef.Run(_activeKey, pair, b.Rank))?.Build is { } now ? (now with { Rank = b.Rank }, true) : (b, false);
    }

    /// <summary>
    /// The best build of a weapon's last run by damage (score for runs saved before damage): as the run scored it, or scored again
    /// when the run is stale. Null without a run.
    /// </summary>
    public (BuildRef Ref, BuildDto Build)? RunBest(string weapon)
    {
        if (ResultsOf(weapon) is not { } results) return null;
        var stale = IsStale(weapon);
        (BuildRef Ref, BuildDto Build)? best = null;
        foreach (var p in results.Pairs)
            foreach (var b in p.Builds)
            {
                var r = BuildRef.Run(weapon, p.Rank, b.Rank);
                var now = stale || b.StatsRequested.DamagePerExecution <= 0 ? ScoreRef(r)?.Build : b;
                if (now is not null && (best is null || Value(now) > Value(best.Value.Build))) best = (r, now);
            }
        return best;
    }

    /// <summary>The weapon's best own build now (drafts left out), by damage.</summary>
    public (BuildRef Ref, BuildDto Build)? OwnBest(string weapon)
    {
        (BuildRef Ref, BuildDto Build)? best = null;
        foreach (var b in BuildsOf(weapon).Where(b => b.DraftOf is null))
        {
            var r = BuildRef.Own(weapon, b.Name);
            if (ScoreRef(r)?.Build is { } now && (best is null || Value(now) > Value(best.Value.Build))) best = (r, now);
        }
        return best;
    }

    private static double Value(BuildDto b) => BuildDamage.Of(b) ?? b.Score;

    private (OptimizationRequest Request, BuildInput Input, double? RunScore, DateTimeOffset? RunAt, bool Stale)? Lookup(BuildRef r)
    {
        if (RequestOf(r.Weapon) is not { } request) return null;
        if (r.IsRun)
            return RunBuild(r) is { } run ? (request, _runInputs.GetValue(run, b => BuildHelpers.FromBuild(b, RefName(r))), run.Score, ResultsOf(r.Weapon)?.CompletedAt, IsStale(r.Weapon)) : null;
        return BuildsOf(r.Weapon).FirstOrDefault(b => b.Name == r.Mine) is { } own ? (request, own, null, null, false) : null;
    }
}
