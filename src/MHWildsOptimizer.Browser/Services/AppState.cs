using System.Text.Json;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// Everything the UI shows and does, in one place (the React client's state.tsx): the profile with its talismans and
/// presets, the open weapons as sessions (unsaved edits survive weapon switches and reloads), live validation and build
/// scoring, optimizer runs and the URL. Components read the properties and re-render on <see cref="Changed"/>.
/// </summary>
public sealed class AppState : IDisposable
{
    /// <summary>Session key of the unsaved new weapon; weapon names start with a letter or digit, so it never clashes.</summary>
    public const string NewWeapon = "(new)";

    private readonly IProfileStore _store;
    private readonly BrowserStorage _storage;
    private readonly CpSatBridge _solver;
    private readonly NavigationManager _nav;
    private readonly ISnackbar _snackbar;
    private readonly IDialogService _dialogs;
    private readonly IJSRuntime _js;

    private Dictionary<string, WeaponSession> _sessions = new();
    private string _activeKey = NewWeapon;
    private readonly Dictionary<string, CancellationTokenSource> _runs = new();
    private CancellationTokenSource? _batchRun;
    /// <summary>Drafts in browser storage as last written (session key -> JSON), for the current profile.</summary>
    private Dictionary<string, string> _writtenDrafts = new();
    /// <summary>Validation and scores per session, shown at once when switching back to a weapon.</summary>
    private readonly Dictionary<string, ResolveDto> _resolvedCache = new();
    private readonly Dictionary<string, IReadOnlyList<EvaluatedBuildDto>> _evaluatedCache = new();
    private int _validationVersion, _draftVersion, _inventoryVersion;
    private bool _pushNext;
    private string? _lastUrl;

    public AppState(GameData data, Catalog catalog, IProfileStore store, BrowserStorage storage, CpSatBridge solver, NavigationManager nav,
        ISnackbar snackbar, IDialogService dialogs, IJSRuntime js)
    {
        Data = data;
        Catalog = catalog;
        Index = new CatalogIndex(catalog);
        _store = store;
        _storage = storage;
        _solver = solver;
        _nav = nav;
        _snackbar = snackbar;
        _dialogs = dialogs;
        _js = js;
    }

    /// <summary>Raised after every state change; components call StateHasChanged.</summary>
    public event Action? Changed;

    private void Notify() => Changed?.Invoke();

    // ---------------------------------------------------------------- state

    public GameData Data { get; }
    public Catalog Catalog { get; }
    public CatalogIndex Index { get; }
    public IProfileStore Store => _store;
    public bool Ready { get; private set; }
    public string? LoadError { get; private set; }
    public SolverEnvironment? Solver { get; private set; }
    public IReadOnlyList<ProfileSummaryDto> Profiles { get; private set; } = [];

    /// <summary>The account: owns the talisman pool, the weapon-type condition presets and the weapons.</summary>
    public string? Profile { get; private set; }

    /// <summary>Condition preset per weapon type of the profile.</summary>
    public IReadOnlyDictionary<string, Conditions> Presets { get; private set; } = new Dictionary<string, Conditions>();

    /// <summary>The profile's random talismans, shared by every weapon; changes are saved right away.</summary>
    public IReadOnlyList<TalismanInput> Talismans { get; private set; } = [];

    public IReadOnlyList<WeaponSummaryDto> Weapons { get; private set; } = [];

    public WeaponSession? Active => _sessions.GetValueOrDefault(_activeKey);
    public string ActiveKey => _activeKey;

    /// <summary>The open weapon's name; null = the unsaved new weapon.</summary>
    public string? Name => Active?.Name;
    public OptimizationRequest? Request => Active?.Request;
    public bool Dirty => Active?.Dirty ?? false;

    /// <summary>Session keys of weapons with unsaved edits (weapon names, <see cref="NewWeapon"/> for the new one).</summary>
    public IReadOnlyList<string> Unsaved => _sessions.Values.Where(s => s.Dirty).Select(s => s.Key).ToList();

    /// <summary>Hand-entered builds of the weapon.</summary>
    public IReadOnlyList<BuildInput> Builds => Active?.Builds ?? [];

    /// <summary>The build shown in My builds and compared against in Results.</summary>
    public int BuildIndex => Active?.BuildIndex ?? 0;

    public string? CompareKey => Active?.CompareKey;
    public RunState Run => Active?.Run ?? RunState.Idle;
    public ResolveDto? Resolved { get; private set; }
    public bool Resolving { get; private set; }

    /// <summary>Builds scored under the current request, index-aligned with <see cref="Builds"/>; null until the first evaluation.</summary>
    public IReadOnlyList<EvaluatedBuildDto>? Evaluated { get; private set; }

    public Tab Tab { get; private set; } = Tab.Inventory;

    /// <summary>The profile's weapons with picked build and last run; null while loading.</summary>
    public IReadOnlyList<InventoryEntryDto>? Inventory { get; private set; }
    public string? InventoryError { get; private set; }

    /// <summary>Weapons ticked for the side-by-side comparison in the inventory.</summary>
    public IReadOnlyList<string> InventoryCompare { get; private set; } = [];

    /// <summary>Background runs of saved weapons, by weapon name.</summary>
    public IReadOnlyDictionary<string, BatchMark> Batch { get; private set; } = new Dictionary<string, BatchMark>();

    public string WeaponKind => Request is { } r ? ProfileRules.WeaponKind(r) : "great-sword";

    /// <summary>The condition preset of a weapon type (the catalog defaults when the profile has none).</summary>
    public Conditions PresetFor(string kind) => Presets.GetValueOrDefault(kind) ?? Catalog.ConditionsDefault;

    // ---------------------------------------------------------------- start-up and routing

    public async Task InitializeAsync()
    {
        try
        {
            Profiles = _store.ListProfiles();
            if (Profiles.Count == 0)
            {
                _store.CreateProfile("Default");
                Profiles = _store.ListProfiles();
            }
            await ApplyRouteAsync(Route.Parse(_nav.ToBaseRelativePath(_nav.Uri)), null);
            Ready = true;
            _nav.LocationChanged += OnLocationChanged;
            SyncUrl();
            ScheduleValidation();
        }
        catch (Exception e)
        {
            LoadError = e.Message;
        }
        Notify();
        _ = PrepareSolverAsync();
    }

    /// <summary>Checks the browser and loads the solver runtime in the background, so the first run starts right away.</summary>
    private async Task PrepareSolverAsync()
    {
        try
        {
            Solver = await _solver.EnvironmentAsync();
            Notify();
            if (Solver.Problem is not null) return;
            await _solver.WarmUpAsync();
            await _solver.WarmUpLanesAsync(OptimizerOptions.ProcessorCount);
        }
        catch (Exception e)
        {
            Toast($"The solver could not start: {e.Message}", Severity.Error);
        }
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var path = _nav.ToBaseRelativePath(e.Location);
        if (path == _lastUrl) return; // our own navigation
        _lastUrl = path;
        await ApplyRouteAsync(Route.Parse(path), Profile);
        Notify();
    }

    /// <summary>Goes to a route (start-up, back / forward): profile, weapon and tab.</summary>
    private async Task ApplyRouteAsync(Route target, string? currentProfile)
    {
        var profileName = target.Profile is { } p && Profiles.Any(x => x.Name == p)
            ? p
            : currentProfile ?? Profiles.FirstOrDefault(x => x.Name == _storage.GetString(ProfileKey))?.Name ?? Profiles[0].Name;
        if (profileName != currentProfile) LoadProfile(profileName);
        if (target.WeaponGiven && target.Weapon is { } weapon)
        {
            if (!ActivateWeapon(profileName, weapon)) { Tab = Tab.Inventory; return; }
        }
        else if (target.WeaponGiven) _activeKey = NewWeapon;
        Tab = target.Tab ?? (profileName != currentProfile ? Tab.Inventory : Tab);
        await Task.CompletedTask;
    }

    /// <summary>The URL follows the app: user steps are pushed (back / forward work), everything else replaces the current entry.</summary>
    private void SyncUrl()
    {
        if (!Ready || Profile is null) return;
        var url = Route.Url(Profile, _activeKey == NewWeapon ? null : _activeKey, Tab);
        if (url != _nav.ToBaseRelativePath(_nav.Uri))
        {
            _lastUrl = url;
            _nav.NavigateTo(url, new NavigationOptions { ReplaceHistoryEntry = !_pushNext });
        }
        _pushNext = false;
    }

    /// <summary>Called after state changes that move the URL (profile, weapon, tab).</summary>
    private void Moved()
    {
        SyncUrl();
        ScheduleValidation();
        Notify();
    }

    // ---------------------------------------------------------------- profile

    private const string ProfileKey = "mhwo.profile";

    private static string DraftPrefix(string profile) => $"mhwo.draft:{profile}:";

    /// <summary>A new weapon request of a type, starting from the catalog default and the type's preset.</summary>
    private OptimizationRequest FreshRequest(string kind)
    {
        var r = Clone(Catalog.DefaultRequest);
        var weapon = r.Weapon.Spec is { } spec ? r.Weapon with { Spec = spec with { Type = kind } } : r.Weapon with { Type = kind };
        return r with { Weapon = weapon, Conditions = Clone(PresetFor(kind)) };
    }

    /// <summary>Opens a profile: its data, the weapons with unsaved drafts from browser storage, and the new weapon.</summary>
    private void LoadProfile(string profileName)
    {
        var p = _store.LoadProfile(profileName) ?? _store.CreateProfile(profileName);
        var list = _store.ListWeapons(profileName);
        foreach (var cts in _runs.Values) cts.Cancel();
        _runs.Clear();
        _batchRun?.Cancel();

        Profile = p.Name;
        Presets = p.ConditionPresets;
        Talismans = p.Talismans;
        Weapons = list;

        var restored = new Dictionary<string, WeaponSession>();
        var written = new Dictionary<string, string>();
        var prefix = DraftPrefix(p.Name);
        foreach (var key in _storage.Keys(prefix))
        {
            var sessionKey = key[prefix.Length..];
            var summary = list.FirstOrDefault(w => w.Name == sessionKey);
            if (sessionKey != NewWeapon && summary is null) { _storage.Remove(key); continue; } // the weapon is gone
            Draft? draft;
            try { draft = _storage.Get<Draft>(key, BrowserProfileStore.Json); }
            catch (JsonException) { draft = null; }
            if (draft is null) continue;
            restored[sessionKey] = WeaponSession.Create(sessionKey, sessionKey == NewWeapon ? null : sessionKey, draft.Request, draft.Builds, true)
                with { ResultsPending = summary?.HasResults ?? false };
            written[sessionKey] = JsonSerializer.Serialize(draft, BrowserProfileStore.Json);
        }
        if (!restored.ContainsKey(NewWeapon))
            restored[NewWeapon] = WeaponSession.Create(NewWeapon, null, FreshRequest("great-sword"), [], false);

        _writtenDrafts = written;
        _resolvedCache.Clear();
        _evaluatedCache.Clear();
        _storage.SetString(ProfileKey, p.Name);
        _sessions = restored;
        _activeKey = NewWeapon;
        Tab = Tab.Inventory;
        Batch = new Dictionary<string, BatchMark>();
        Inventory = null;
        InventoryCompare = [];
        Resolved = null;
        Evaluated = null;
        TouchSaved();
    }

    /// <summary>Makes a saved weapon the open one, loading it (and its last run) the first time.</summary>
    private bool ActivateWeapon(string profileName, string weaponName)
    {
        if (_sessions.TryGetValue(weaponName, out var existing))
        {
            _activeKey = weaponName;
            if (existing.ResultsPending)
            {
                var results = TryLoadResults(profileName, weaponName);
                Update(weaponName, s => s with { ResultsPending = false, Run = s.Run.Status == RunStatus.Idle && results is not null ? RunState.Saved(results) : s.Run });
            }
            ShowCached();
            return true;
        }
        try
        {
            var file = _store.LoadWeapon(profileName, weaponName);
            if (file is null) { Toast($"There is no weapon {weaponName}", Severity.Error); return false; }
            var session = WeaponSession.Create(file.Name, file.Name, file.Request, file.Builds, false);
            if (file.HasResults && TryLoadResults(profileName, file.Name) is { } saved) session = session with { Run = RunState.Saved(saved) };
            _sessions[file.Name] = session;
            _activeKey = file.Name;
            ShowCached();
            return true;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            Toast($"Could not open {weaponName}: {e.Message}", Severity.Error);
            return false;
        }
    }

    private ResultDto? TryLoadResults(string profile, string weapon)
    {
        try { return _store.LoadResults(profile, weapon); }
        catch (Exception e) when (e is JsonException or IOException or FormatException) { return null; }
    }

    /// <summary>Switching weapons shows that weapon's last validation and scores right away; the debounced checks refresh them.</summary>
    private void ShowCached()
    {
        Resolved = _resolvedCache.GetValueOrDefault(_activeKey);
        Evaluated = _evaluatedCache.GetValueOrDefault(_activeKey);
    }

    public async Task OpenProfileAsync(string profileName)
    {
        if (_sessions.Values.Any(s => s.Run.Status == RunStatus.Running)
            && !await ConfirmAsync("Switch profile", "An optimizer run is in progress; switching the profile stops it. Switch anyway?", "Switch"))
            return;
        _pushNext = true;
        LoadProfile(profileName);
        Moved();
    }

    public void CreateProfile(string profileName)
    {
        _store.CreateProfile(profileName);
        Profiles = _store.ListProfiles();
        _pushNext = true;
        LoadProfile(profileName);
        Toast($"Created the profile {profileName}");
        Moved();
    }

    public void RefreshProfiles()
    {
        Profiles = _store.ListProfiles();
        Notify();
    }

    public void RefreshWeapons()
    {
        if (Profile is null) return;
        Weapons = _store.ListWeapons(Profile);
        Profiles = _store.ListProfiles();
    }

    // ---------------------------------------------------------------- navigation

    public void SetTab(Tab tab)
    {
        if (tab != Tab) _pushNext = true;
        Tab = tab;
        Moved();
    }

    /// <summary>Leaves the inventory for a weapon tab; any other tab stays (switching weapons keeps the view).</summary>
    private Tab WeaponTab(Tab? tab = null) => tab ?? (Tab == Tab.Inventory ? Tab.Weapon : Tab);

    /// <summary>
    /// Shows the unsaved new weapon. With a type, starts it over as a weapon of that type (with that type's condition preset),
    /// after asking when it has unsaved edits.
    /// </summary>
    public async Task NewWeaponAsync(string? kind = null)
    {
        _sessions.TryGetValue(NewWeapon, out var current);
        if (kind is not null || current is null)
        {
            if (current?.Dirty == true && !await ConfirmAsync("New weapon", "Start over with a new weapon? The unsaved new weapon is discarded.", "Start over")) return;
            if (_runs.Remove(NewWeapon, out var cts)) cts.Cancel();
            _resolvedCache.Remove(NewWeapon);
            _evaluatedCache.Remove(NewWeapon);
            _sessions[NewWeapon] = WeaponSession.Create(NewWeapon, null, FreshRequest(kind ?? WeaponKind), [], false);
        }
        _pushNext = true;
        _activeKey = NewWeapon;
        ShowCached();
        Tab = WeaponTab();
        Moved();
    }

    /// <summary>Shows a saved weapon (loaded once, then kept); without a tab it stays on the current one.</summary>
    public void OpenWeapon(string weaponName, Tab? tab = null)
    {
        if (Profile is null || !ActivateWeapon(Profile, weaponName)) return;
        _pushNext = true;
        Tab = WeaponTab(tab);
        Moved();
    }

    // ---------------------------------------------------------------- weapon actions

    private void Update(string key, Func<WeaponSession, WeaponSession> fn)
    {
        if (_sessions.TryGetValue(key, out var s)) _sessions[key] = fn(s);
    }

    /// <summary>Changes the open weapon (its edits are kept as a draft until saved or reverted).</summary>
    private void UpdateActive(Func<WeaponSession, WeaponSession> fn, bool validate = true)
    {
        Update(_activeKey, fn);
        ScheduleDrafts();
        if (validate) ScheduleValidation();
        Notify();
    }

    public async Task<bool> SaveAsync(string? saveAs = null)
    {
        var s = Active;
        if (s is null || Profile is null) return false;
        var target = saveAs ?? s.Name;
        if (target is null) return false;
        if (!ProfileRules.IsValidName(target)) { Toast(ProfileRules.NameRules, Severity.Error); return false; }
        try
        {
            var file = _store.SaveWeapon(Profile, target, s.Request, s.Builds);
            // saved under another name: the edits moved there; the old weapon reloads from storage when opened again
            if (s.Key != target)
            {
                _sessions.Remove(s.Key);
                if (_resolvedCache.Remove(s.Key, out var r)) _resolvedCache[target] = r;
                if (_evaluatedCache.Remove(s.Key, out var ev)) _evaluatedCache[target] = ev;
                if (s.Key == NewWeapon) _sessions[NewWeapon] = WeaponSession.Create(NewWeapon, null, FreshRequest(ProfileRules.WeaponKind(s.Request)), [], false);
            }
            _sessions[target] = s with { Key = target, Name = target, Request = file.Request, Dirty = false };
            _activeKey = target;
            RefreshWeapons();
            TouchSaved();
            ScheduleDrafts();
            Toast($"Saved {file.Name}");
            Moved();
            await Task.CompletedTask;
            return true;
        }
        catch (IOException e)
        {
            Toast($"Save failed: {e.Message}", Severity.Error);
            return false;
        }
    }

    /// <summary>Drops the open weapon's unsaved edits (back to the saved weapon, or a blank new weapon).</summary>
    public void Revert()
    {
        var s = Active;
        if (s is null || Profile is null) return;
        if (s.Name is null)
        {
            UpdateActive(cur => WeaponSession.Create(cur.Key, null, FreshRequest(ProfileRules.WeaponKind(cur.Request)), [], false) with { Run = cur.Run });
            return;
        }
        var file = _store.LoadWeapon(Profile, s.Name);
        if (file is null) { Toast($"Could not reload {s.Name}", Severity.Error); return; }
        UpdateActive(cur => WeaponSession.Create(cur.Key, cur.Name, file.Request, file.Builds, false) with { Run = cur.Run });
        Toast($"Reverted {s.Name} to the saved version");
    }

    public void Remove(string weaponName)
    {
        if (Profile is null) return;
        if (!_store.DeleteWeapon(Profile, weaponName)) { Toast($"Delete failed: there is no weapon {weaponName}", Severity.Error); return; }
        if (_runs.Remove(weaponName, out var cts)) cts.Cancel();
        _resolvedCache.Remove(weaponName);
        _evaluatedCache.Remove(weaponName);
        _sessions.Remove(weaponName);
        if (!_sessions.ContainsKey(NewWeapon)) _sessions[NewWeapon] = WeaponSession.Create(NewWeapon, null, FreshRequest("great-sword"), [], false);
        // the open weapon is gone: show the new one, but stay on the current tab (e.g. the inventory)
        if (_activeKey == weaponName) { _activeKey = NewWeapon; ShowCached(); }
        InventoryCompare = InventoryCompare.Where(x => x != weaponName).ToList();
        Toast($"Deleted {weaponName}");
        RefreshWeapons();
        TouchSaved();
        ScheduleDrafts();
        Moved();
    }

    /// <summary>Changes the open weapon's request; a new weapon type brings its own preset (the conditions the weapon overrides stay).</summary>
    public void PatchRequest(Func<OptimizationRequest, OptimizationRequest> fn) => UpdateActive(s =>
    {
        var next = fn(s.Request);
        var from = ProfileRules.WeaponKind(s.Request);
        var to = ProfileRules.WeaponKind(next);
        if (from != to) next = next with { Conditions = ConditionPresets.Follow(next.Conditions, PresetFor(from), PresetFor(to)) };
        return s with { Request = next, Dirty = true };
    });

    public void PatchWeapon(Func<WeaponStatsInput, WeaponStatsInput> fn) => PatchRequest(r => r with { Weapon = fn(r.Weapon) });

    public void PatchConditions(Func<Conditions, Conditions> fn) => PatchRequest(r => r with { Conditions = fn(r.Conditions) });

    public void PatchOptions(Func<OptimizerOptions, OptimizerOptions> fn) => PatchRequest(r => r with { Options = fn(r.Options) });

    /// <summary>Changes the profile's talisman pool and saves it; renames (old -> new) are followed by the builds of every weapon.</summary>
    public void SetTalismans(Func<IReadOnlyList<TalismanInput>, IReadOnlyList<TalismanInput>> fn, IReadOnlyDictionary<string, string>? renames = null)
    {
        if (Profile is null) return;
        Talismans = fn(Talismans);
        // open weapons follow a rename right away (the store renames it in the saved builds)
        if (renames is { Count: > 0 })
            foreach (var key in _sessions.Keys.ToList())
                if (ProfileRules.RenameTalismans(_sessions[key].Builds, renames) is { } renamed)
                    Update(key, s => s with { Builds = renamed });
        try
        {
            _store.SaveTalismans(Profile, Talismans.ToList(), renames);
            TouchSaved();
        }
        catch (IOException e)
        {
            Toast($"Could not save the talismans: {e.Message}", Severity.Error);
        }
        ScheduleValidation();
        Notify();
    }

    public void SetBuilds(Func<IReadOnlyList<BuildInput>, IReadOnlyList<BuildInput>> fn) => UpdateActive(s => s with { Builds = fn(s.Builds), Dirty = true });

    public void SetBuildIndex(int index) => UpdateActive(s => s with { BuildIndex = index }, validate: false);

    public void SetCompareKey(string? key) => UpdateActive(s => s with { CompareKey = key }, validate: false);

    /// <summary>Appends a build, selects it and opens My builds (optionally comparing it with <paramref name="compareKey"/>). A picked build unpicks the others.</summary>
    public void AddBuild(BuildInput build, string? compareKey = null)
    {
        UpdateActive(s =>
        {
            var others = build.Picked ? s.Builds.Select(b => b with { Picked = false }).ToList() : s.Builds.ToList();
            return s with { Builds = [.. others, build], BuildIndex = s.Builds.Count, CompareKey = compareKey, Dirty = true };
        });
        SetTab(Tab.Builds);
    }

    /// <summary>Marks one build as the weapon's pick (null = none).</summary>
    public void PickBuild(int? index) => SetBuilds(list => list.Select((b, i) => b with { Picked = i == index }).ToList());

    /// <summary>Saves the open weapon's conditions as its type's preset; other weapons of that type follow it.</summary>
    public void SavePreset()
    {
        if (Profile is null || Request is null) return;
        var kind = WeaponKind;
        var label = Index.WeaponTypes.GetValueOrDefault(kind)?.Label ?? kind;
        var old = PresetFor(kind);
        try
        {
            var saved = _store.SavePreset(Profile, kind, Request.Conditions);
            var next = saved.Profile.ConditionPresets.GetValueOrDefault(kind) ?? Request.Conditions;
            Presets = saved.Profile.ConditionPresets;
            // open weapons of that type follow too: saved ones reload from storage, unsaved ones move along
            foreach (var (key, s) in _sessions.ToList())
            {
                if (key == _activeKey || ProfileRules.WeaponKind(s.Request) != kind) continue;
                if (s.Dirty || s.Name is null) _sessions[key] = s with { Request = s.Request with { Conditions = ConditionPresets.Follow(s.Request.Conditions, old, next) } };
                else _sessions.Remove(key); // a clean saved weapon loads again from its updated version
            }
            var others = saved.UpdatedWeapons.Where(w => w != Name).ToList();
            TouchSaved();
            Toast($"Saved the {label} preset" + (others.Count > 0 ? $"; {string.Join(", ", others)} followed it" : ""));
        }
        catch (IOException e)
        {
            Toast($"Could not save the preset: {e.Message}", Severity.Error);
        }
        Notify();
    }

    public void SetInventoryCompare(Func<IReadOnlyList<string>, IReadOnlyList<string>> fn)
    {
        InventoryCompare = fn(InventoryCompare);
        Notify();
    }

    // ---------------------------------------------------------------- debounced work

    /// <summary>Reloads the inventory (after saved profile data changed).</summary>
    public void TouchSaved() => _ = ReloadInventoryAsync(++_inventoryVersion);

    private async Task ReloadInventoryAsync(int version)
    {
        await Task.Yield();
        if (version != _inventoryVersion || Profile is null) return;
        try
        {
            Inventory = MHWildsOptimizer.Api.Inventory.Build(_store, Profile, Data);
            InventoryError = null;
        }
        catch (Exception e)
        {
            InventoryError = e.Message;
        }
        Notify();
    }

    /// <summary>Live validation and scoring of the open weapon under the current request (debounced).</summary>
    private void ScheduleValidation() => _ = ValidateAsync(++_validationVersion);

    private async Task ValidateAsync(int version)
    {
        if (Active is null) return;
        Resolving = true;
        await Task.Delay(250);
        if (version != _validationVersion || Active is not { } s) return;
        var key = s.Key;
        var payload = new ConfigPayload(s.Request, Talismans.ToList(), s.Builds.ToList());
        try
        {
            var resolved = MHWildsOptimizer.Api.Resolving.Resolve(payload, Data, _store.Directory);
            Resolved = MHWildsOptimizer.Api.Resolving.Describe(resolved, s.Request.TargetSkills, Data);
            Evaluated = s.Builds.Count == 0 ? [] : BuildEvaluation.Evaluate(payload, resolved, Data);
        }
        catch (Exception e)
        {
            Resolved = new ResolveDto(false, [$"Validation failed: {e.Message}"], [], null, [], s.Request.SkillPair.Mode, 0, new TalismanCountsDto(0, 0, 0), null, null);
            Evaluated = s.Builds.Select(b => new EvaluatedBuildDto(b.Name, [$"Scoring failed: {e.Message}"], [], null, [])).ToList();
        }
        _resolvedCache[key] = Resolved;
        _evaluatedCache[key] = Evaluated;
        Resolving = false;
        Notify();
    }

    /// <summary>Unsaved edits go to browser storage (debounced); saved or dropped weapons leave it.</summary>
    private void ScheduleDrafts() => _ = WriteDraftsAsync(++_draftVersion);

    private async Task WriteDraftsAsync(int version)
    {
        await Task.Delay(300);
        if (version != _draftVersion || Profile is null) return;
        var prefix = DraftPrefix(Profile);
        try
        {
            foreach (var s in _sessions.Values)
            {
                if (s.Dirty)
                {
                    var json = JsonSerializer.Serialize(new Draft(s.Request, s.Builds.ToList()), BrowserProfileStore.Json);
                    if (_writtenDrafts.GetValueOrDefault(s.Key) != json) { _storage.SetString(prefix + s.Key, json); _writtenDrafts[s.Key] = json; }
                }
                else if (_writtenDrafts.Remove(s.Key)) _storage.Remove(prefix + s.Key);
            }
            foreach (var key in _writtenDrafts.Keys.Where(k => !_sessions.ContainsKey(k)).ToList())
            {
                _storage.Remove(prefix + key);
                _writtenDrafts.Remove(key);
            }
        }
        catch (StorageFullException)
        {
            // the edits only live in this page
        }
        await _js.InvokeVoidAsync("mhwo.setLeaveWarning", _sessions.Values.Any(s => s.Run.Status == RunStatus.Running));
    }

    // ---------------------------------------------------------------- runs

    /// <summary>The browser runs CP-SAT only: the beam search does not fit WebAssembly's memory and has no threads there.</summary>
    private const OptimizerEngine Engine = OptimizerEngine.CpSat;

    public void CancelRun()
    {
        var key = _activeKey;
        if (_runs.Remove(key, out var cts)) cts.Cancel();
        Update(key, s => s.Run.Status == RunStatus.Running ? s with { Run = s.Run with { Status = RunStatus.Cancelled, FinishedAt = DateTimeOffset.Now } } : s);
        Notify();
    }

    /// <summary>Runs the open weapon; the run belongs to that weapon and keeps going while you look at another one.</summary>
    public async Task RunOptimizerAsync()
    {
        if (Active is not { } s) return;
        var key = s.Key;
        if (_runs.Remove(key, out var previous)) previous.Cancel();
        var cts = new CancellationTokenSource();
        _runs[key] = cts;
        void SetRun(Func<RunState, RunState> fn) { Update(key, cur => cur with { Run = fn(cur.Run) }); Notify(); }

        SetRun(_ => RunState.Idle with { Status = RunStatus.Running, StartedAt = DateTimeOffset.Now });
        SetTab(Tab.Results);
        ScheduleDrafts();
        var payload = new ConfigPayload(s.Request, Talismans.ToList());
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Optimization.RunAsync(payload, Profile, s.Name, Data, _store, _solver, e =>
            {
                if (cts.IsCancellationRequested) return;
                switch (e)
                {
                    case ValidationEvent v:
                        SetRun(r => r with { Log = [.. r.Log, .. v.Errors.Select(x => "error   " + x), .. v.Warnings.Select(x => "warning " + x)] });
                        break;
                    case ProgressEvent p:
                        SetRun(r => r with { Log = [.. r.Log, $"[{clock.Elapsed.TotalSeconds,5:0.0}s] {p.Message}"] });
                        break;
                    case ResultEvent result:
                        SetRun(r => r with { Status = RunStatus.Done, Result = result.Result, FinishedAt = DateTimeOffset.Now, FromDisk = false });
                        break;
                    case ErrorEvent error:
                        SetRun(r => r with { Status = RunStatus.Error, Error = error.Message, FinishedAt = DateTimeOffset.Now });
                        break;
                }
            }, cts.Token, Engine);
            SetRun(r => r.Status == RunStatus.Running ? r with { Status = RunStatus.Error, Error = "The run ended without a result.", FinishedAt = DateTimeOffset.Now } : r);
            if (s.Name is not null) { RefreshWeapons(); TouchSaved(); }
        }
        catch (OperationCanceledException)
        {
            // CancelRun has marked it
        }
        catch (Exception e)
        {
            if (!cts.IsCancellationRequested) SetRun(r => r with { Status = RunStatus.Error, Error = e.Message, FinishedAt = DateTimeOffset.Now });
        }
        finally
        {
            if (_runs.GetValueOrDefault(key) == cts) _runs.Remove(key);
            ScheduleDrafts();
        }
    }

    /// <summary>Runs saved weapons one after another (saved request, profile talismans); each result becomes that weapon's last run.</summary>
    public async Task RunWeaponsAsync(IReadOnlyList<string> names)
    {
        if (Profile is null) return;
        _batchRun?.Cancel();
        var cts = new CancellationTokenSource();
        _batchRun = cts;
        var profile = Profile;
        var batch = names.ToDictionary(n => n, _ => new BatchMark(BatchStatus.Queued));
        void Mark(string weapon, BatchMark mark) { batch[weapon] = mark; Batch = new Dictionary<string, BatchMark>(batch); Notify(); }
        Batch = new Dictionary<string, BatchMark>(batch);
        var failed = 0;
        foreach (var weapon in names)
        {
            if (cts.IsCancellationRequested) break;
            Mark(weapon, new BatchMark(BatchStatus.Running));
            try
            {
                var file = _store.LoadWeapon(profile, weapon) ?? throw new IOException($"There is no weapon {weapon}.");
                var outcome = new BatchMark(BatchStatus.Error, "The run ended without a result.");
                await Optimization.RunAsync(new ConfigPayload(file.Request, Talismans.ToList()), profile, weapon, Data, _store, _solver, e =>
                {
                    switch (e)
                    {
                        case ProgressEvent p: Mark(weapon, new BatchMark(BatchStatus.Running, p.Message)); break;
                        case ResultEvent: outcome = new BatchMark(BatchStatus.Done); break;
                        case ErrorEvent x: outcome = new BatchMark(BatchStatus.Error, x.Message); break;
                    }
                }, cts.Token, Engine);
                if (outcome.Status == BatchStatus.Error) failed++;
                Mark(weapon, outcome);
                // an open weapon shows its new last run (unless it is running one of its own)
                if (outcome.Status == BatchStatus.Done && _sessions.ContainsKey(weapon) && TryLoadResults(profile, weapon) is { } result)
                    Update(weapon, s => s.Run.Status == RunStatus.Running ? s : s with { Run = RunState.Saved(result) });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                failed++;
                Mark(weapon, new BatchMark(BatchStatus.Error, e.Message));
            }
            RefreshWeapons();
            TouchSaved();
        }
        if (!cts.IsCancellationRequested)
            Toast(failed > 0 ? $"{failed} of {names.Count} runs failed" : $"Ran {names.Count} weapon{(names.Count > 1 ? "s" : "")}", failed > 0 ? Severity.Error : Severity.Info);
        Notify();
    }

    public void StopWeapons()
    {
        _batchRun?.Cancel();
        Batch = Batch.Where(kv => kv.Value.Status is BatchStatus.Done or BatchStatus.Error).ToDictionary();
        Notify();
    }

    // ---------------------------------------------------------------- helpers

    public void Toast(string text, Severity severity = Severity.Info) => _snackbar.Add(text, severity);

    public async Task<bool> ConfirmAsync(string title, string text, string yes = "OK")
    {
        var answer = await _dialogs.ShowMessageBoxAsync(title, text, yesText: yes, cancelText: "Cancel");
        return answer == true;
    }

    /// <summary>A deep copy through JSON (requests hold mutable collections).</summary>
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, BrowserProfileStore.Json), BrowserProfileStore.Json)!;

    public void Dispose()
    {
        _nav.LocationChanged -= OnLocationChanged;
        foreach (var cts in _runs.Values) cts.Cancel();
        _batchRun?.Cancel();
    }
}
