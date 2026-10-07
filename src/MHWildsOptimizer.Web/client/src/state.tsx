import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api } from './api';
import { follow, weaponKind } from './presets';
import type {
  ArmorSetPiece, BuildInput, Catalog, Conditions, ConfigPayload, EvaluatedBuild, OptimizationRequest, OptimizationResult, OptimizeEvent, ProfileSummary, Resolved,
  TalismanInput, WeaponInput, WeaponSummary,
} from './types';

export type Tab = 'inventory' | 'weapon' | 'pair' | 'targets' | 'limits' | 'conditions' | 'talismans' | 'options' | 'review' | 'results' | 'builds';

export type RunStatus = 'idle' | 'running' | 'done' | 'error' | 'cancelled';

export interface RunState {
  status: RunStatus;
  log: string[];
  result: OptimizationResult | null;
  error: string | null;
  startedAt: number | null;
  finishedAt: number | null;
  /** true when the result is the weapon's saved last run rather than produced in this session */
  fromDisk: boolean;
}

const idleRun: RunState = { status: 'idle', log: [], result: null, error: null, startedAt: null, finishedAt: null, fromDisk: false };

const PROFILE_KEY = 'mhwo.profile';
const readStoredProfile = () => { try { return localStorage.getItem(PROFILE_KEY); } catch { return null; } };
const storeProfile = (name: string) => { try { localStorage.setItem(PROFILE_KEY, name); } catch { /* private mode */ } };

/** A weapon in a batch of background runs (the inventory's "Run stale"). */
export interface BatchMark { status: 'queued' | 'running' | 'done' | 'error'; message?: string }

export interface AppState {
  catalog: Catalog | null;
  loadError: string | null;
  profiles: ProfileSummary[];
  /** the account: owns the talisman pool, the weapon-type condition presets and the weapons */
  profile: string | null;
  /** condition preset per weapon type of the profile */
  presets: Record<string, Conditions>;
  /** the profile's random talismans, shared by every weapon; changes are saved right away */
  talismans: TalismanInput[];
  weapons: WeaponSummary[];
  /** the open weapon (inputs/profiles/<profile>/weapons/<name>.json); null = not saved yet */
  name: string | null;
  request: OptimizationRequest | null;
  dirty: boolean;
  resolved: Resolved | null;
  resolving: boolean;
  /** hand-entered builds of the weapon (<name>.builds.json) */
  builds: BuildInput[];
  /** builds scored under the current request, index-aligned with builds; null until the first evaluation */
  evaluated: EvaluatedBuild[] | null;
  /** the build shown in My builds and compared against in Results */
  buildIndex: number;
  /** what My builds compares the selected build with: "opt:<pair rank>:<build rank>" or "mine:<index>"; null = the best optimizer build */
  compareKey: string | null;
  run: RunState;
  tab: Tab;
  /** background runs of saved weapons, by weapon name */
  batch: Record<string, BatchMark>;
  /** bumped whenever saved profile data changes, so the inventory reloads */
  savedVersion: number;
  notice: { text: string; kind: 'info' | 'error' } | null;
}

export interface AppActions {
  setTab(tab: Tab): void;
  openProfile(name: string): Promise<void>;
  createProfile(name: string): Promise<void>;
  refreshWeapons(): Promise<void>;
  /** A new, unsaved weapon of the given type (default: the open weapon's type) with that type's condition preset. */
  newWeapon(kind?: string): void;
  openWeapon(name: string, tab?: Tab): Promise<void>;
  save(name?: string): Promise<boolean>;
  remove(name: string): Promise<void>;
  patchRequest(fn: (r: OptimizationRequest) => OptimizationRequest): void;
  patchWeapon(patch: Partial<WeaponInput>): void;
  /** Changes the profile's talisman pool and saves it; renames (old -> new) are followed by the builds of every weapon. */
  setTalismans(fn: (t: TalismanInput[]) => TalismanInput[], renames?: Record<string, string>): void;
  setBuilds(fn: (b: BuildInput[]) => BuildInput[]): void;
  setBuildIndex(index: number): void;
  setCompareKey(key: string | null): void;
  /** Appends a build, selects it and opens My builds (optionally comparing it with compareKey). A picked build unpicks the others. */
  addBuild(build: BuildInput, compareKey?: string | null): void;
  /** Marks one build as the weapon's pick (null = none). */
  pickBuild(index: number | null): void;
  /** The condition preset of a weapon type (the catalog defaults when the profile has none). */
  presetFor(kind: string): Conditions;
  /** Saves the open weapon's conditions as its type's preset; other weapons of that type follow it. */
  savePreset(): Promise<void>;
  /** Marks saved profile data as changed (e.g. after a background run), so the inventory reloads. */
  touchSaved(): void;
  runOptimizer(): Promise<void>;
  cancelRun(): void;
  /** Runs the optimizer for saved weapons one after another (saved request, profile talismans); each result becomes that weapon's last run. */
  runWeapons(names: string[]): Promise<void>;
  stopWeapons(): void;
  toast(text: string, kind?: 'info' | 'error'): void;
}

const StateContext = createContext<(AppState & AppActions) | null>(null);

export function useApp() {
  const ctx = useContext(StateContext);
  if (!ctx) throw new Error('useApp outside provider');
  return ctx;
}

export function AppStateProvider({ children }: { children: ReactNode }) {
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfile] = useState<string | null>(null);
  const [presets, setPresets] = useState<Record<string, Conditions>>({});
  const [talismans, setTalismansState] = useState<TalismanInput[]>([]);
  const [weapons, setWeapons] = useState<WeaponSummary[]>([]);
  const [name, setName] = useState<string | null>(null);
  const [request, setRequest] = useState<OptimizationRequest | null>(null);
  const [dirty, setDirty] = useState(false);
  const [resolved, setResolved] = useState<Resolved | null>(null);
  const [resolving, setResolving] = useState(false);
  const [builds, setBuildsState] = useState<BuildInput[]>([]);
  const [evaluated, setEvaluated] = useState<EvaluatedBuild[] | null>(null);
  const [buildIndex, setBuildIndex] = useState(0);
  const [compareKey, setCompareKey] = useState<string | null>(null);
  const [run, setRun] = useState<RunState>(idleRun);
  const [tab, setTab] = useState<Tab>('inventory');
  const [savedVersion, setSavedVersion] = useState(0);
  const [toastState, setToastState] = useState<AppState['notice']>(null);
  const abortRun = useRef<AbortController | null>(null);
  const [batch, setBatch] = useState<Record<string, BatchMark>>({});
  const abortBatch = useRef<AbortController | null>(null);
  const talismanSaves = useRef<Promise<unknown>>(Promise.resolve());
  const talismansRef = useRef<TalismanInput[]>([]);
  const presetsRef = useRef<Record<string, Conditions>>({});
  presetsRef.current = presets;

  const toast = useCallback((text: string, kind: 'info' | 'error' = 'info') => {
    setToastState({ text, kind });
    window.setTimeout(() => setToastState((t) => (t?.text === text ? null : t)), kind === 'error' ? 6000 : 3000);
  }, []);

  const touchSaved = useCallback(() => setSavedVersion((v) => v + 1), []);

  const presetFor = useCallback((kind: string): Conditions =>
    presetsRef.current[kind] ?? catalog?.conditions_default ?? ({} as Conditions), [catalog, presets]); // eslint-disable-line react-hooks/exhaustive-deps

  const refreshWeapons = useCallback(async () => {
    if (!profile) return;
    try { setWeapons(await api.weapons(profile)); } catch (e) { toast(`Could not list weapons: ${(e as Error).message}`, 'error'); }
  }, [profile, toast]);

  /** A new weapon request of a type, starting from the catalog default and the type's preset. */
  const freshRequest = useCallback((cat: Catalog, kind: string, typePresets: Record<string, Conditions>): OptimizationRequest => {
    const r = structuredClone(cat.default_request);
    if (r.weapon.spec) r.weapon.spec.type = kind;
    else r.weapon.type = kind;
    r.conditions = structuredClone(typePresets[kind] ?? cat.conditions_default);
    return r;
  }, []);

  const resetWeaponState = useCallback(() => {
    abortRun.current?.abort();
    setBuildsState([]);
    setBuildIndex(0);
    setCompareKey(null);
    setDirty(false);
    setRun(idleRun);
  }, []);

  const loadProfile = useCallback(async (cat: Catalog, profileName: string) => {
    const [p, list] = await Promise.all([api.profile(profileName), api.weapons(profileName)]);
    setProfile(p.name);
    storeProfile(p.name);
    setPresets(p.condition_presets);
    presetsRef.current = p.condition_presets;
    setTalismansState(p.talismans);
    talismansRef.current = p.talismans;
    setWeapons(list);
    setName(null);
    resetWeaponState();
    setRequest(freshRequest(cat, 'great-sword', p.condition_presets));
    setTab('inventory');
    setSavedVersion((v) => v + 1);
  }, [freshRequest, resetWeaponState]);

  // initial load: catalog + profiles (a first "Default" profile is created when there is none); open the last used profile
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [cat, initial] = await Promise.all([api.catalog(), api.profiles()]);
        let list = initial;
        if (list.length === 0) {
          await api.createProfile('Default');
          list = await api.profiles();
        }
        if (cancelled) return;
        setCatalog(cat);
        setProfiles(list);
        const stored = readStoredProfile();
        await loadProfile(cat, list.find((p) => p.name === stored)?.name ?? list[0].name);
      } catch (e) {
        if (!cancelled) setLoadError((e as Error).message);
      }
    })();
    return () => { cancelled = true; };
  }, [loadProfile]);

  // live validation: resolve the request whenever it changes (debounced)
  useEffect(() => {
    if (!request) return;
    const controller = new AbortController();
    setResolving(true);
    const handle = window.setTimeout(async () => {
      try {
        const r = await api.resolve({ request, talismans }, controller.signal);
        if (!controller.signal.aborted) setResolved(r);
      } catch (e) {
        if (!controller.signal.aborted) setResolved({
          is_valid: false, errors: [`Validation failed: ${(e as Error).message}`], warnings: [], weapon: null, targets: [], skill_pair_mode: request.skill_pair.mode,
          skill_pair_candidates: 0, talismans: { total: 0, random: 0, craftable: 0 }, baseline: null, relevance: null,
        });
      } finally {
        if (!controller.signal.aborted) setResolving(false);
      }
    }, 250);
    return () => { controller.abort(); window.clearTimeout(handle); };
  }, [request, talismans]);

  // live scoring of the hand-entered builds under the current request (debounced)
  useEffect(() => {
    if (!request) return;
    if (builds.length === 0) { setEvaluated([]); return; }
    const controller = new AbortController();
    const handle = window.setTimeout(async () => {
      try {
        const r = await api.evaluate({ request, talismans, builds }, controller.signal);
        if (!controller.signal.aborted) setEvaluated(r);
      } catch (e) {
        if (!controller.signal.aborted) setEvaluated(builds.map((b) => ({ name: b.name, errors: [`Scoring failed: ${(e as Error).message}`], warnings: [], build: null, targets: [] })));
      }
    }, 250);
    return () => { controller.abort(); window.clearTimeout(handle); };
  }, [request, talismans, builds]);

  const openProfile = useCallback(async (profileName: string) => {
    if (!catalog) return;
    try { await loadProfile(catalog, profileName); }
    catch (e) { toast(`Could not open the profile ${profileName}: ${(e as Error).message}`, 'error'); }
  }, [catalog, loadProfile, toast]);

  const createProfile = useCallback(async (profileName: string) => {
    if (!catalog) return;
    try {
      await api.createProfile(profileName);
      setProfiles(await api.profiles());
      await loadProfile(catalog, profileName);
      toast(`Created the profile ${profileName}`);
    } catch (e) {
      toast(`Could not create the profile: ${(e as Error).message}`, 'error');
    }
  }, [catalog, loadProfile, toast]);

  const newWeapon = useCallback((kind?: string) => {
    if (!catalog) return;
    resetWeaponState();
    setName(null);
    setRequest((r) => freshRequest(catalog, kind ?? (r ? weaponKind(r) : 'great-sword'), presetsRef.current));
    setTab('weapon');
  }, [catalog, freshRequest, resetWeaponState]);

  const openWeapon = useCallback(async (weaponName: string, openTab: Tab = 'weapon') => {
    if (!profile) return;
    try {
      const file = await api.loadWeapon(profile, weaponName);
      resetWeaponState();
      setName(file.name);
      setRequest(file.request);
      setBuildsState(file.builds ?? []);
      const picked = (file.builds ?? []).findIndex((b) => b.picked);
      setBuildIndex(Math.max(0, picked));
      if (file.has_results) {
        try {
          const result = await api.results(profile, file.name);
          setRun({ ...idleRun, status: 'done', result, fromDisk: true });
        } catch {
          setRun(idleRun);
        }
      }
      setTab(openTab);
    } catch (e) {
      toast(`Could not open ${weaponName}: ${(e as Error).message}`, 'error');
    }
  }, [profile, resetWeaponState, toast]);

  const save = useCallback(async (saveAs?: string) => {
    if (!request || !profile) return false;
    const target = saveAs ?? name;
    if (!target) return false;
    try {
      const file = await api.saveWeapon(profile, target, { request, builds });
      setName(file.name);
      setRequest(file.request);
      setDirty(false);
      await refreshWeapons();
      touchSaved();
      toast(`Saved ${file.name}`);
      return true;
    } catch (e) {
      toast(`Save failed: ${(e as Error).message}`, 'error');
      return false;
    }
  }, [profile, name, request, builds, refreshWeapons, touchSaved, toast]);

  const remove = useCallback(async (weaponName: string) => {
    if (!profile) return;
    try {
      await api.deleteWeapon(profile, weaponName);
      toast(`Deleted ${weaponName}`);
      if (weaponName === name) newWeapon();
      await refreshWeapons();
      touchSaved();
    } catch (e) {
      toast(`Delete failed: ${(e as Error).message}`, 'error');
    }
  }, [profile, name, newWeapon, refreshWeapons, touchSaved, toast]);

  const patchRequest = useCallback((fn: (r: OptimizationRequest) => OptimizationRequest) => {
    setRequest((r) => {
      if (!r) return r;
      const next = fn(r);
      // a new weapon type brings its own preset; the conditions this weapon overrides stay
      const from = weaponKind(r);
      const to = weaponKind(next);
      if (from === to || !catalog) return next;
      const preset = (kind: string) => presetsRef.current[kind] ?? catalog.conditions_default;
      return { ...next, conditions: follow(next.conditions, preset(from), preset(to)) };
    });
    setDirty(true);
  }, [catalog]);

  const patchWeapon = useCallback((patch: Partial<WeaponInput>) => {
    patchRequest((r) => ({ ...r, weapon: { ...r.weapon, ...patch } }));
  }, [patchRequest]);

  const setTalismans = useCallback((fn: (t: TalismanInput[]) => TalismanInput[], renames?: Record<string, string>) => {
    if (!profile) return;
    const next = fn(talismansRef.current);
    talismansRef.current = next;
    setTalismansState(next);
    // the pool is account-wide: save right away (in order), not with the weapon
    talismanSaves.current = talismanSaves.current
      .then(() => api.saveTalismans(profile, next, renames))
      .then(() => { touchSaved(); })
      .catch((e) => toast(`Could not save the talismans: ${(e as Error).message}`, 'error'));
  }, [profile, touchSaved, toast]);

  const setBuilds = useCallback((fn: (b: BuildInput[]) => BuildInput[]) => {
    setBuildsState((b) => fn(b));
    setDirty(true);
  }, []);

  const addBuild = useCallback((build: BuildInput, compare?: string | null) => {
    const others = build.picked ? builds.map((b) => ({ ...b, picked: false })) : builds;
    setBuildsState([...others, build]);
    setBuildIndex(builds.length);
    setDirty(true);
    setCompareKey(compare ?? null);
    setTab('builds');
  }, [builds]);

  const pickBuild = useCallback((index: number | null) => {
    setBuilds((list) => list.map((b, i) => ({ ...b, picked: i === index })));
  }, [setBuilds]);

  const savePreset = useCallback(async () => {
    if (!profile || !request || !catalog) return;
    const kind = weaponKind(request);
    const label = catalog.weapon_types.find((w) => w.kind === kind)?.label ?? kind;
    try {
      const saved = await api.savePreset(profile, kind, request.conditions);
      setPresets(saved.profile.condition_presets);
      presetsRef.current = saved.profile.condition_presets;
      const others = saved.updated_weapons.filter((w) => w !== name);
      touchSaved();
      toast(`Saved the ${label} preset` + (others.length ? `; ${others.join(', ')} followed it` : ''));
    } catch (e) {
      toast(`Could not save the preset: ${(e as Error).message}`, 'error');
    }
  }, [profile, request, catalog, name, touchSaved, toast]);

  const cancelRun = useCallback(() => {
    abortRun.current?.abort();
    setRun((r) => (r.status === 'running' ? { ...r, status: 'cancelled', finishedAt: Date.now() } : r));
  }, []);

  const runOptimizer = useCallback(async () => {
    if (!request) return;
    abortRun.current?.abort();
    const controller = new AbortController();
    abortRun.current = controller;
    setRun({ ...idleRun, status: 'running', startedAt: Date.now() });
    setTab('results');
    const payload: ConfigPayload = { request, talismans };
    try {
      await api.optimize(payload, profile && name ? { profile, weapon: name } : null, (e: OptimizeEvent) => {
        if (controller.signal.aborted) return;
        if (e.type === 'validation') {
          setRun((r) => ({ ...r, log: [...r.log, ...e.errors.map((x) => 'error   ' + x), ...e.warnings.map((x) => 'warning ' + x)] }));
        } else if (e.type === 'progress') {
          setRun((r) => ({ ...r, log: [...r.log, e.message] }));
        } else if (e.type === 'result') {
          setRun((r) => ({ ...r, status: 'done', result: e.result, finishedAt: Date.now(), fromDisk: false }));
        } else if (e.type === 'error') {
          setRun((r) => ({ ...r, status: 'error', error: e.message, finishedAt: Date.now() }));
        }
      }, controller.signal);
      setRun((r) => (r.status === 'running' ? { ...r, status: 'error', error: 'The server closed the stream without a result.', finishedAt: Date.now() } : r));
      if (name) { refreshWeapons(); touchSaved(); }
    } catch (e) {
      if (controller.signal.aborted) return;
      setRun((r) => ({ ...r, status: 'error', error: (e as Error).message, finishedAt: Date.now() }));
    }
  }, [profile, name, request, talismans, refreshWeapons, touchSaved]);

  const runWeapons = useCallback(async (names: string[]) => {
    if (!profile) return;
    abortBatch.current?.abort();
    const controller = new AbortController();
    abortBatch.current = controller;
    setBatch(Object.fromEntries(names.map((n) => [n, { status: 'queued' } as BatchMark])));
    let failed = 0;
    for (const weapon of names) {
      if (controller.signal.aborted) break;
      setBatch((b) => ({ ...b, [weapon]: { status: 'running' } }));
      try {
        const file = await api.loadWeapon(profile, weapon);
        let outcome: BatchMark = { status: 'error', message: 'The server closed the stream without a result.' };
        await api.optimize({ request: file.request, talismans: talismansRef.current }, { profile, weapon }, (e) => {
          if (e.type === 'progress') setBatch((b) => ({ ...b, [weapon]: { status: 'running', message: e.message } }));
          else if (e.type === 'result') outcome = { status: 'done' };
          else if (e.type === 'error') outcome = { status: 'error', message: e.message };
        }, controller.signal);
        if (outcome.status === 'error') failed++;
        setBatch((b) => ({ ...b, [weapon]: outcome }));
        // the open weapon shows its new last run unless it has unsaved edits the run did not use
        if (outcome.status === 'done' && weapon === name && !dirty) {
          try { setRun({ ...idleRun, status: 'done', result: await api.results(profile, weapon), fromDisk: true }); } catch { /* keep the old one */ }
        }
      } catch (e) {
        if (controller.signal.aborted) break;
        failed++;
        setBatch((b) => ({ ...b, [weapon]: { status: 'error', message: (e as Error).message } }));
      }
      touchSaved();
    }
    if (!controller.signal.aborted)
      toast(failed ? `${failed} of ${names.length} runs failed` : `Ran ${names.length} weapon${names.length > 1 ? 's' : ''}`, failed ? 'error' : 'info');
  }, [profile, name, dirty, touchSaved, toast]);

  const stopWeapons = useCallback(() => {
    abortBatch.current?.abort();
    setBatch((b) => Object.fromEntries(Object.entries(b).filter(([, m]) => m.status === 'done' || m.status === 'error')));
  }, []);

  // background runs belong to the profile they were started in
  useEffect(() => () => { abortBatch.current?.abort(); setBatch({}); }, [profile]);

  const value = useMemo<AppState & AppActions>(() => ({
    catalog, loadError, profiles, profile, presets, talismans, weapons, name, request, dirty, resolved, resolving, builds, evaluated, buildIndex, compareKey, run, tab,
    batch, savedVersion, notice: toastState,
    setTab, openProfile, createProfile, refreshWeapons, newWeapon, openWeapon, save, remove, patchRequest, patchWeapon, setTalismans, setBuilds, setBuildIndex,
    setCompareKey, addBuild, pickBuild, presetFor, savePreset, touchSaved, runOptimizer, cancelRun, runWeapons, stopWeapons, toast,
  }), [batch, catalog, loadError, profiles, profile, presets, talismans, weapons, name, request, dirty, resolved, resolving, builds, evaluated, buildIndex, compareKey, run, tab,
    savedVersion, toastState, openProfile, createProfile, refreshWeapons, newWeapon, openWeapon, save, remove, patchRequest, patchWeapon, setTalismans, setBuilds,
    addBuild, pickBuild, presetFor, savePreset, touchSaved, runOptimizer, cancelRun, runWeapons, stopWeapons, toast]);

  return <StateContext.Provider value={value}>{children}</StateContext.Provider>;
}


/** An armor piece with what its set contributes (set bonuses, group skill). */
export interface ArmorEntry extends ArmorSetPiece { set: string; set_bonus: string[]; group_skill: string | null }

/** Lookups over the catalog that panels share. */
export function useCatalog() {
  const { catalog } = useApp();
  return useMemo(() => {
    const skillsByName = new Map(catalog?.skills.map((s) => [s.name, s]) ?? []);
    const setsByName = new Map(catalog?.armor_sets.map((s) => [s.name, s]) ?? []);
    const weaponTypes = new Map(catalog?.weapon_types.map((w) => [w.kind, w]) ?? []);
    const armorByName = new Map<string, ArmorEntry>();
    for (const set of catalog?.armor_sets ?? [])
      for (const p of set.pieces) armorByName.set(p.name, { ...p, set: set.name, set_bonus: set.set_bonus, group_skill: set.group_skill });
    const decorationsByName = new Map(catalog?.decorations.map((d) => [d.name, d]) ?? []);
    const charmsByName = new Map(catalog?.charms.map((c) => [c.name, c]) ?? []);
    return { catalog, skillsByName, setsByName, weaponTypes, armorByName, decorationsByName, charmsByName };
  }, [catalog]);
}
