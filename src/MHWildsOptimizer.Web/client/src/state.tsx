import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api } from './api';
import { follow, weaponKind } from './presets';
import { parseRoute, routeUrl, type Tab } from './routes';
import type {
  ArmorSetPiece, BuildInput, Catalog, Conditions, ConfigPayload, EvaluatedBuild, InventoryEntry, OptimizationRequest, OptimizationResult, OptimizeEvent,
  ProfileSummary, Resolved, TalismanInput, WeaponInput, WeaponSummary,
} from './types';

export type { Tab } from './routes';

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

/** Session key of the unsaved new weapon; weapon names start with a letter or digit, so it never clashes. */
export const NEW_WEAPON = '(new)';

/**
 * A weapon open in this browser: kept while switching weapons and tabs, so nothing is loaded twice and unsaved edits,
 * the selected build and the last run stay where they were.
 */
interface WeaponSession {
  key: string;
  /** null for the unsaved new weapon */
  name: string | null;
  request: OptimizationRequest;
  builds: BuildInput[];
  buildIndex: number;
  compareKey: string | null;
  run: RunState;
  dirty: boolean;
  /** the weapon's saved last run still has to be fetched (a draft restored after a reload) */
  resultsPending: boolean;
}

// ---------------------------------------------------------------- browser storage (per viewer conveniences)

const PROFILE_KEY = 'mhwo.profile';
const readStoredProfile = () => { try { return localStorage.getItem(PROFILE_KEY); } catch { return null; } };
const storeProfile = (name: string) => { try { localStorage.setItem(PROFILE_KEY, name); } catch { /* private mode */ } };

/** Unsaved edits of a weapon, kept in the browser so a reload or a profile switch does not lose them. */
interface Draft { request: OptimizationRequest; builds: BuildInput[] }
const draftPrefix = (profile: string) => `mhwo.draft:${profile}:`;

function readDrafts(profile: string): Map<string, Draft> {
  const drafts = new Map<string, Draft>();
  try {
    const prefix = draftPrefix(profile);
    for (let i = 0; i < localStorage.length; i++) {
      const k = localStorage.key(i);
      if (!k?.startsWith(prefix)) continue;
      try { drafts.set(k.slice(prefix.length), JSON.parse(localStorage.getItem(k)!) as Draft); } catch { /* broken entry: ignore */ }
    }
  } catch { /* storage blocked */ }
  return drafts;
}

function writeDraft(profile: string, key: string, json: string | null) {
  try {
    if (json === null) localStorage.removeItem(draftPrefix(profile) + key);
    else localStorage.setItem(draftPrefix(profile) + key, json);
  } catch { /* storage full or blocked: the edits only live in this page */ }
}

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
  /** the open weapon's name; null = the unsaved new weapon */
  name: string | null;
  request: OptimizationRequest | null;
  dirty: boolean;
  /** session keys of weapons with unsaved edits (weapon names, NEW_WEAPON for the new one) */
  unsaved: string[];
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
  /** the profile's weapons with picked build and last run; null while loading */
  inventory: InventoryEntry[] | null;
  inventoryError: string | null;
  /** weapons ticked for the side-by-side comparison in the inventory */
  inventoryCompare: string[];
  /** background runs of saved weapons, by weapon name */
  batch: Record<string, BatchMark>;
  notice: { text: string; kind: 'info' | 'error' } | null;
}

export interface AppActions {
  setTab(tab: Tab): void;
  openProfile(name: string): Promise<void>;
  createProfile(name: string): Promise<void>;
  refreshWeapons(): Promise<void>;
  /**
   * Shows the unsaved new weapon. With a type, starts it over as a weapon of that type (with that type's condition
   * preset), after asking when it has unsaved edits.
   */
  newWeapon(kind?: string): void;
  /** Shows a saved weapon (loaded once, then kept); without a tab it stays on the current one. */
  openWeapon(name: string, tab?: Tab): Promise<void>;
  save(name?: string): Promise<boolean>;
  /** Drops the open weapon's unsaved edits (back to the saved file, or a blank new weapon). */
  revert(): Promise<void>;
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
  setInventoryCompare(next: string[] | ((names: string[]) => string[])): void;
  /** Reloads the inventory (after saved profile data changed). */
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

/** A new weapon request of a type, starting from the catalog default and the type's preset. */
function freshRequest(cat: Catalog, kind: string, typePresets: Record<string, Conditions>): OptimizationRequest {
  const r = structuredClone(cat.default_request);
  if (r.weapon.spec) r.weapon.spec.type = kind;
  else r.weapon.type = kind;
  r.conditions = structuredClone(typePresets[kind] ?? cat.conditions_default);
  return r;
}

function newSession(key: string, name: string | null, request: OptimizationRequest, builds: BuildInput[], dirty: boolean): WeaponSession {
  const picked = builds.findIndex((b) => b.picked);
  return { key, name, request, builds, buildIndex: Math.max(0, picked), compareKey: null, run: idleRun, dirty, resultsPending: false };
}

const renameTalismans = (builds: BuildInput[], renames: Record<string, string>) =>
  builds.map((b) => (b.talisman && b.talisman.name in renames ? { ...b, talisman: { ...b.talisman, name: renames[b.talisman.name] } } : b));

export function AppStateProvider({ children }: { children: ReactNode }) {
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfile] = useState<string | null>(null);
  const [presets, setPresets] = useState<Record<string, Conditions>>({});
  const [talismans, setTalismansState] = useState<TalismanInput[]>([]);
  const [weapons, setWeapons] = useState<WeaponSummary[]>([]);
  const [sessions, setSessions] = useState<Record<string, WeaponSession>>({});
  const [activeKey, setActiveKey] = useState<string>(NEW_WEAPON);
  const [resolved, setResolved] = useState<Resolved | null>(null);
  const [resolving, setResolving] = useState(false);
  const [evaluated, setEvaluated] = useState<EvaluatedBuild[] | null>(null);
  const [tab, setTabState] = useState<Tab>('inventory');
  const [savedVersion, setSavedVersion] = useState(0);
  const [inventory, setInventory] = useState<InventoryEntry[] | null>(null);
  const [inventoryError, setInventoryError] = useState<string | null>(null);
  const [inventoryCompare, setInventoryCompare] = useState<string[]>([]);
  const [toastState, setToastState] = useState<AppState['notice']>(null);
  const [batch, setBatch] = useState<Record<string, BatchMark>>({});

  const runAborts = useRef(new Map<string, AbortController>());
  const abortBatch = useRef<AbortController | null>(null);
  const talismanSaves = useRef<Promise<unknown>>(Promise.resolve());
  const talismansRef = useRef<TalismanInput[]>([]);
  const presetsRef = useRef<Record<string, Conditions>>({});
  presetsRef.current = presets;
  const sessionsRef = useRef(sessions);
  sessionsRef.current = sessions;
  const tabRef = useRef(tab);
  tabRef.current = tab;
  const activeKeyRef = useRef(activeKey);
  activeKeyRef.current = activeKey;
  /** drafts in browser storage as last written (session key -> JSON), for the current profile */
  const writtenDrafts = useRef(new Map<string, string>());
  /** results of resolve / evaluate per session, shown at once when switching back to a weapon */
  const resolvedCache = useRef(new Map<string, Resolved>());
  const evaluatedCache = useRef(new Map<string, EvaluatedBuild[]>());
  /** the next URL change is a user step (pushed to the history); otherwise the URL is replaced */
  const pushNext = useRef(false);
  const routeReady = useRef(false);

  const active: WeaponSession | null = sessions[activeKey] ?? null;
  const request = active?.request ?? null;
  const builds = useMemo(() => active?.builds ?? [], [active?.builds]);
  const name = active?.name ?? null;
  const dirty = active?.dirty ?? false;
  const run = active?.run ?? idleRun;
  const unsaved = useMemo(() => Object.values(sessions).filter((s) => s.dirty).map((s) => s.key), [sessions]);

  const toast = useCallback((text: string, kind: 'info' | 'error' = 'info') => {
    setToastState({ text, kind });
    window.setTimeout(() => setToastState((t) => (t?.text === text ? null : t)), kind === 'error' ? 6000 : 3000);
  }, []);

  const touchSaved = useCallback(() => setSavedVersion((v) => v + 1), []);

  const updateSession = useCallback((key: string, fn: (s: WeaponSession) => WeaponSession) => {
    setSessions((all) => (all[key] ? { ...all, [key]: fn(all[key]) } : all));
  }, []);

  const setTab = useCallback((t: Tab) => {
    if (t !== tabRef.current) pushNext.current = true;
    setTabState(t);
  }, []);

  const presetFor = useCallback((kind: string): Conditions =>
    presetsRef.current[kind] ?? catalog?.conditions_default ?? ({} as Conditions), [catalog, presets]); // eslint-disable-line react-hooks/exhaustive-deps

  const refreshWeapons = useCallback(async () => {
    if (!profile) return;
    try { setWeapons(await api.weapons(profile)); } catch (e) { toast(`Could not list weapons: ${(e as Error).message}`, 'error'); }
  }, [profile, toast]);

  // ---------------------------------------------------------------- profile

  /** Opens a profile: its data, the weapons with unsaved drafts from browser storage, and the new weapon. */
  const loadProfile = useCallback(async (cat: Catalog, profileName: string) => {
    const [p, list] = await Promise.all([api.profile(profileName), api.weapons(profileName)]);
    for (const c of runAborts.current.values()) c.abort();
    runAborts.current.clear();
    abortBatch.current?.abort();

    const drafts = readDrafts(p.name);
    const restored: Record<string, WeaponSession> = {};
    const written = new Map<string, string>();
    for (const [key, draft] of drafts) {
      const summary = list.find((w) => w.name === key);
      if (key !== NEW_WEAPON && !summary) { writeDraft(p.name, key, null); continue; } // the weapon is gone
      restored[key] = { ...newSession(key, key === NEW_WEAPON ? null : key, draft.request, draft.builds, true), resultsPending: !!summary?.has_results };
      written.set(key, JSON.stringify(draft));
    }
    restored[NEW_WEAPON] ??= newSession(NEW_WEAPON, null, freshRequest(cat, 'great-sword', p.condition_presets), [], false);

    writtenDrafts.current = written;
    resolvedCache.current.clear();
    evaluatedCache.current.clear();
    storeProfile(p.name);
    presetsRef.current = p.condition_presets;
    talismansRef.current = p.talismans;
    setProfile(p.name);
    setPresets(p.condition_presets);
    setTalismansState(p.talismans);
    setWeapons(list);
    setSessions(restored);
    sessionsRef.current = restored;
    setActiveKey(NEW_WEAPON);
    setTabState('inventory');
    setBatch({});
    setInventory(null);
    setInventoryCompare([]);
    setSavedVersion((v) => v + 1);
  }, []);

  /** Makes a saved weapon the open one, loading it (and its last run) the first time. */
  const activateWeapon = useCallback(async (profileName: string, weaponName: string): Promise<boolean> => {
    const existing = sessionsRef.current[weaponName];
    if (existing) {
      setActiveKey(weaponName);
      if (existing.resultsPending) {
        updateSession(weaponName, (s) => ({ ...s, resultsPending: false }));
        api.results(profileName, weaponName)
          .then((result) => updateSession(weaponName, (s) => (s.run.status === 'idle' ? { ...s, run: { ...idleRun, status: 'done', result, fromDisk: true } } : s)))
          .catch(() => { /* no saved run */ });
      }
      return true;
    }
    try {
      const file = await api.loadWeapon(profileName, weaponName);
      const session = newSession(file.name, file.name, file.request, file.builds ?? [], false);
      if (file.has_results) {
        try { session.run = { ...idleRun, status: 'done', result: await api.results(profileName, file.name), fromDisk: true }; } catch { /* keep idle */ }
      }
      setSessions((all) => (all[file.name] ? all : { ...all, [file.name]: session }));
      setActiveKey(file.name);
      return true;
    } catch (e) {
      toast(`Could not open ${weaponName}: ${(e as Error).message}`, 'error');
      return false;
    }
  }, [toast, updateSession]);

  /** Goes to a route (initial load, back / forward): profile, weapon and tab. */
  const applyRoute = useCallback(async (cat: Catalog, target: ReturnType<typeof parseRoute>, knownProfiles: ProfileSummary[], currentProfile: string | null) => {
    const profileName = target.profile && knownProfiles.some((p) => p.name === target.profile)
      ? target.profile
      : currentProfile ?? knownProfiles.find((p) => p.name === readStoredProfile())?.name ?? knownProfiles[0].name;
    if (profileName !== currentProfile) await loadProfile(cat, profileName);
    if (typeof target.weapon === 'string') {
      if (!(await activateWeapon(profileName, target.weapon))) { setTabState('inventory'); return; }
    } else if (target.weapon === null) {
      setActiveKey(NEW_WEAPON);
    }
    setTabState(target.tab ?? (profileName !== currentProfile ? 'inventory' : tabRef.current));
  }, [loadProfile, activateWeapon]);

  // initial load: catalog + profiles (a first "Default" profile is created when there is none); then the URL's route
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
        await applyRoute(cat, parseRoute(window.location.pathname), list, null);
        routeReady.current = true;
      } catch (e) {
        if (!cancelled) setLoadError((e as Error).message);
      }
    })();
    return () => { cancelled = true; };
  }, [applyRoute]);

  // the URL follows the app: user steps are pushed (back / forward work), everything else replaces the current entry
  useEffect(() => {
    if (!routeReady.current || !profile) return;
    const url = routeUrl({ profile, weapon: activeKey === NEW_WEAPON ? null : activeKey, tab });
    if (url !== window.location.pathname) {
      if (pushNext.current) window.history.pushState(null, '', url);
      else window.history.replaceState(null, '', url);
    }
    pushNext.current = false;
  }, [profile, activeKey, tab]);

  // back / forward
  useEffect(() => {
    if (!catalog) return;
    const onPop = () => {
      pushNext.current = false;
      void applyRoute(catalog, parseRoute(window.location.pathname), profiles, profile);
    };
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, [catalog, profiles, profile, applyRoute]);

  // unsaved edits go to browser storage (debounced), saved or dropped weapons leave it
  useEffect(() => {
    if (!profile) return;
    const handle = window.setTimeout(() => {
      const written = writtenDrafts.current;
      for (const s of Object.values(sessions)) {
        if (s.dirty) {
          const json = JSON.stringify({ request: s.request, builds: s.builds } satisfies Draft);
          if (written.get(s.key) !== json) { writeDraft(profile, s.key, json); written.set(s.key, json); }
        } else if (written.has(s.key)) {
          writeDraft(profile, s.key, null);
          written.delete(s.key);
        }
      }
      for (const key of [...written.keys()])
        if (!sessions[key]) { writeDraft(profile, key, null); written.delete(key); }
    }, 300);
    return () => window.clearTimeout(handle);
  }, [profile, sessions]);

  // the inventory, reloaded whenever saved profile data changes
  useEffect(() => {
    if (!profile) return;
    const controller = new AbortController();
    api.inventory(profile, controller.signal)
      .then((list) => { setInventory(list); setInventoryError(null); })
      .catch((e) => { if (!controller.signal.aborted) setInventoryError((e as Error).message); });
    return () => controller.abort();
  }, [profile, savedVersion]);

  // switching weapons shows that weapon's last validation and scores right away; the effects below refresh them
  useEffect(() => {
    setResolved(resolvedCache.current.get(activeKey) ?? null);
    setEvaluated(evaluatedCache.current.get(activeKey) ?? null);
  }, [activeKey]);

  // live validation: resolve the request whenever it changes (debounced)
  useEffect(() => {
    if (!request) return;
    const key = activeKey;
    const controller = new AbortController();
    setResolving(true);
    const handle = window.setTimeout(async () => {
      try {
        const r = await api.resolve({ request, talismans }, controller.signal);
        if (controller.signal.aborted) return;
        resolvedCache.current.set(key, r);
        setResolved(r);
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
  }, [request, talismans]); // eslint-disable-line react-hooks/exhaustive-deps

  // live scoring of the hand-entered builds under the current request (debounced)
  useEffect(() => {
    if (!request) return;
    const key = activeKey;
    if (builds.length === 0) { setEvaluated([]); evaluatedCache.current.set(key, []); return; }
    const controller = new AbortController();
    const handle = window.setTimeout(async () => {
      try {
        const r = await api.evaluate({ request, talismans, builds }, controller.signal);
        if (controller.signal.aborted) return;
        evaluatedCache.current.set(key, r);
        setEvaluated(r);
      } catch (e) {
        if (!controller.signal.aborted) setEvaluated(builds.map((b) => ({ name: b.name, errors: [`Scoring failed: ${(e as Error).message}`], warnings: [], build: null, targets: [] })));
      }
    }, 250);
    return () => { controller.abort(); window.clearTimeout(handle); };
  }, [request, talismans, builds]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---------------------------------------------------------------- navigation actions

  const openProfile = useCallback(async (profileName: string) => {
    if (!catalog) return;
    const running = Object.values(sessionsRef.current).some((s) => s.run.status === 'running');
    if (running && !window.confirm('An optimizer run is in progress; switching the profile stops it. Switch anyway?')) return;
    try {
      pushNext.current = true;
      await loadProfile(catalog, profileName);
    } catch (e) {
      pushNext.current = false;
      toast(`Could not open the profile ${profileName}: ${(e as Error).message}`, 'error');
    }
  }, [catalog, loadProfile, toast]);

  const createProfile = useCallback(async (profileName: string) => {
    if (!catalog) return;
    try {
      await api.createProfile(profileName);
      setProfiles(await api.profiles());
      pushNext.current = true;
      await loadProfile(catalog, profileName);
      toast(`Created the profile ${profileName}`);
    } catch (e) {
      toast(`Could not create the profile: ${(e as Error).message}`, 'error');
    }
  }, [catalog, loadProfile, toast]);

  /** Leaves the inventory for a weapon tab; any other tab stays (switching weapons keeps the view). */
  const weaponTab = (t?: Tab): Tab => t ?? (tabRef.current === 'inventory' ? 'weapon' : tabRef.current);

  const newWeapon = useCallback((kind?: string) => {
    if (!catalog) return;
    const current = sessionsRef.current[NEW_WEAPON];
    if (kind !== undefined || !current) {
      if (current?.dirty && !window.confirm('Start over with a new weapon? The unsaved new weapon is discarded.')) return;
      const r = freshRequest(catalog, kind ?? (request ? weaponKind(request) : 'great-sword'), presetsRef.current);
      runAborts.current.get(NEW_WEAPON)?.abort();
      resolvedCache.current.delete(NEW_WEAPON);
      evaluatedCache.current.delete(NEW_WEAPON);
      setSessions((all) => ({ ...all, [NEW_WEAPON]: newSession(NEW_WEAPON, null, r, [], false) }));
    }
    pushNext.current = true;
    setActiveKey(NEW_WEAPON);
    setTabState(weaponTab());
  }, [catalog, request]);

  const openWeapon = useCallback(async (weaponName: string, openTab?: Tab) => {
    if (!profile) return;
    if (await activateWeapon(profile, weaponName)) {
      pushNext.current = true;
      setTabState(weaponTab(openTab));
    }
  }, [profile, activateWeapon]);

  // ---------------------------------------------------------------- weapon actions

  const save = useCallback(async (saveAs?: string) => {
    const s = sessionsRef.current[activeKeyRef.current];
    if (!s || !profile) return false;
    const target = saveAs ?? s.name;
    if (!target) return false;
    try {
      const file = await api.saveWeapon(profile, target, { request: s.request, builds: s.builds });
      setSessions((all) => {
        const next = { ...all };
        const cur = all[s.key] ?? s;
        // saved under another name: the edits moved there; the old weapon reloads from its file when opened again
        if (s.key !== target) delete next[s.key];
        next[target] = { ...cur, key: target, name: target, request: file.request, dirty: false };
        return next;
      });
      if (s.key !== target) {
        for (const cache of [resolvedCache.current, evaluatedCache.current] as Map<string, unknown>[]) {
          if (cache.has(s.key)) cache.set(target, cache.get(s.key));
          cache.delete(s.key);
        }
      }
      setActiveKey(target);
      await refreshWeapons();
      touchSaved();
      toast(`Saved ${file.name}`);
      return true;
    } catch (e) {
      toast(`Save failed: ${(e as Error).message}`, 'error');
      return false;
    }
  }, [profile, refreshWeapons, touchSaved, toast]);

  const revert = useCallback(async () => {
    const s = sessionsRef.current[activeKeyRef.current];
    if (!s || !profile || !catalog) return;
    if (s.name === null) {
      updateSession(s.key, (cur) => ({ ...newSession(cur.key, null, freshRequest(catalog, weaponKind(cur.request), presetsRef.current), [], false), run: cur.run }));
      return;
    }
    try {
      const file = await api.loadWeapon(profile, s.name);
      updateSession(s.key, (cur) => ({ ...newSession(cur.key, cur.name, file.request, file.builds ?? [], false), run: cur.run }));
      toast(`Reverted ${s.name} to the saved file`);
    } catch (e) {
      toast(`Could not reload ${s.name}: ${(e as Error).message}`, 'error');
    }
  }, [profile, catalog, updateSession, toast]);

  const remove = useCallback(async (weaponName: string) => {
    if (!profile || !catalog) return;
    try {
      await api.deleteWeapon(profile, weaponName);
      runAborts.current.get(weaponName)?.abort();
      resolvedCache.current.delete(weaponName);
      evaluatedCache.current.delete(weaponName);
      setSessions((all) => {
        const next = { ...all };
        delete next[weaponName];
        next[NEW_WEAPON] ??= newSession(NEW_WEAPON, null, freshRequest(catalog, 'great-sword', presetsRef.current), [], false);
        return next;
      });
      // the open weapon is gone: show the new one, but stay on the current tab (e.g. the inventory)
      if (activeKeyRef.current === weaponName) setActiveKey(NEW_WEAPON);
      setInventoryCompare((c) => c.filter((x) => x !== weaponName));
      toast(`Deleted ${weaponName}`);
      await refreshWeapons();
      touchSaved();
    } catch (e) {
      toast(`Delete failed: ${(e as Error).message}`, 'error');
    }
  }, [profile, catalog, refreshWeapons, touchSaved, toast]);

  const patchRequest = useCallback((fn: (r: OptimizationRequest) => OptimizationRequest) => {
    updateSession(activeKeyRef.current, (s) => {
      const next = fn(s.request);
      // a new weapon type brings its own preset; the conditions this weapon overrides stay
      const from = weaponKind(s.request);
      const to = weaponKind(next);
      if (from === to || !catalog) return { ...s, request: next, dirty: true };
      const preset = (kind: string) => presetsRef.current[kind] ?? catalog.conditions_default;
      return { ...s, request: { ...next, conditions: follow(next.conditions, preset(from), preset(to)) }, dirty: true };
    });
  }, [catalog, updateSession]);

  const patchWeapon = useCallback((patch: Partial<WeaponInput>) => {
    patchRequest((r) => ({ ...r, weapon: { ...r.weapon, ...patch } }));
  }, [patchRequest]);

  const setTalismans = useCallback((fn: (t: TalismanInput[]) => TalismanInput[], renames?: Record<string, string>) => {
    if (!profile) return;
    const next = fn(talismansRef.current);
    talismansRef.current = next;
    setTalismansState(next);
    // open weapons follow a rename right away (the server renames it in the saved builds files)
    if (renames && Object.keys(renames).length > 0)
      setSessions((all) => Object.fromEntries(Object.entries(all).map(([k, s]) => [k, { ...s, builds: renameTalismans(s.builds, renames) }])));
    // the pool is account-wide: save right away (in order), not with the weapon
    talismanSaves.current = talismanSaves.current
      .then(() => api.saveTalismans(profile, next, renames))
      .then(() => { touchSaved(); })
      .catch((e) => toast(`Could not save the talismans: ${(e as Error).message}`, 'error'));
  }, [profile, touchSaved, toast]);

  const setBuilds = useCallback((fn: (b: BuildInput[]) => BuildInput[]) => {
    updateSession(activeKeyRef.current, (s) => ({ ...s, builds: fn(s.builds), dirty: true }));
  }, [updateSession]);

  const setBuildIndex = useCallback((index: number) => updateSession(activeKeyRef.current, (s) => ({ ...s, buildIndex: index })), [updateSession]);

  const setCompareKey = useCallback((key: string | null) => updateSession(activeKeyRef.current, (s) => ({ ...s, compareKey: key })), [updateSession]);

  const addBuild = useCallback((build: BuildInput, compare?: string | null) => {
    updateSession(activeKeyRef.current, (s) => {
      const others = build.picked ? s.builds.map((b) => ({ ...b, picked: false })) : s.builds;
      return { ...s, builds: [...others, build], buildIndex: s.builds.length, compareKey: compare ?? null, dirty: true };
    });
    setTab('builds');
  }, [updateSession, setTab]);

  const pickBuild = useCallback((index: number | null) => {
    setBuilds((list) => list.map((b, i) => ({ ...b, picked: i === index })));
  }, [setBuilds]);

  const savePreset = useCallback(async () => {
    if (!profile || !request || !catalog) return;
    const kind = weaponKind(request);
    const label = catalog.weapon_types.find((w) => w.kind === kind)?.label ?? kind;
    const old = presetsRef.current[kind] ?? catalog.conditions_default;
    try {
      const saved = await api.savePreset(profile, kind, request.conditions);
      const next = saved.profile.condition_presets[kind] ?? request.conditions;
      setPresets(saved.profile.condition_presets);
      presetsRef.current = saved.profile.condition_presets;
      // open weapons of that type follow too: saved ones reload from their (updated) files, unsaved ones move along
      setSessions((all) => {
        const out: Record<string, WeaponSession> = {};
        for (const [k, s] of Object.entries(all)) {
          if (k === activeKeyRef.current || weaponKind(s.request) !== kind) out[k] = s;
          else if (s.dirty || s.name === null) out[k] = { ...s, request: { ...s.request, conditions: follow(s.request.conditions, old, next) } };
          // a clean saved weapon is dropped and loads again from its updated file
        }
        return out;
      });
      const others = saved.updated_weapons.filter((w) => w !== name);
      touchSaved();
      toast(`Saved the ${label} preset` + (others.length ? `; ${others.join(', ')} followed it` : ''));
    } catch (e) {
      toast(`Could not save the preset: ${(e as Error).message}`, 'error');
    }
  }, [profile, request, catalog, name, touchSaved, toast]);

  // ---------------------------------------------------------------- runs

  const cancelRun = useCallback(() => {
    const key = activeKeyRef.current;
    runAborts.current.get(key)?.abort();
    updateSession(key, (s) => (s.run.status === 'running' ? { ...s, run: { ...s.run, status: 'cancelled', finishedAt: Date.now() } } : s));
  }, [updateSession]);

  /** Runs the open weapon; the run belongs to that weapon and keeps going while you look at another one. */
  const runOptimizer = useCallback(async () => {
    const s = sessionsRef.current[activeKeyRef.current];
    if (!s) return;
    const key = s.key;
    runAborts.current.get(key)?.abort();
    const controller = new AbortController();
    runAborts.current.set(key, controller);
    const setRun = (fn: (r: RunState) => RunState) => updateSession(key, (cur) => ({ ...cur, run: fn(cur.run) }));
    setRun(() => ({ ...idleRun, status: 'running', startedAt: Date.now() }));
    setTab('results');
    const payload: ConfigPayload = { request: s.request, talismans: talismansRef.current };
    try {
      await api.optimize(payload, profile && s.name ? { profile, weapon: s.name } : null, (e: OptimizeEvent) => {
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
      if (s.name) { refreshWeapons(); touchSaved(); }
    } catch (e) {
      if (controller.signal.aborted) return;
      setRun((r) => ({ ...r, status: 'error', error: (e as Error).message, finishedAt: Date.now() }));
    } finally {
      if (runAborts.current.get(key) === controller) runAborts.current.delete(key);
    }
  }, [profile, updateSession, setTab, refreshWeapons, touchSaved]);

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
        // an open weapon shows its new last run (unless it is running one of its own)
        if (outcome.status === 'done' && sessionsRef.current[weapon]) {
          try {
            const result = await api.results(profile, weapon);
            updateSession(weapon, (s) => (s.run.status === 'running' ? s : { ...s, run: { ...idleRun, status: 'done', result, fromDisk: true } }));
          } catch { /* keep the old one */ }
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
  }, [profile, updateSession, touchSaved, toast]);

  const stopWeapons = useCallback(() => {
    abortBatch.current?.abort();
    setBatch((b) => Object.fromEntries(Object.entries(b).filter(([, m]) => m.status === 'done' || m.status === 'error')));
  }, []);

  const value = useMemo<AppState & AppActions>(() => ({
    catalog, loadError, profiles, profile, presets, talismans, weapons, name, request, dirty, unsaved, resolved, resolving, builds, evaluated,
    buildIndex: active?.buildIndex ?? 0, compareKey: active?.compareKey ?? null, run, tab, inventory, inventoryError, inventoryCompare, batch, notice: toastState,
    setTab, openProfile, createProfile, refreshWeapons, newWeapon, openWeapon, save, revert, remove, patchRequest, patchWeapon, setTalismans, setBuilds,
    setBuildIndex, setCompareKey, addBuild, pickBuild, presetFor, savePreset, setInventoryCompare, touchSaved, runOptimizer, cancelRun, runWeapons,
    stopWeapons, toast,
  }), [catalog, loadError, profiles, profile, presets, talismans, weapons, name, request, dirty, unsaved, resolved, resolving, builds, evaluated, active,
    run, tab, inventory, inventoryError, inventoryCompare, batch, toastState, setTab, openProfile, createProfile, refreshWeapons, newWeapon, openWeapon,
    save, revert, remove, patchRequest, patchWeapon, setTalismans, setBuilds, setBuildIndex, setCompareKey, addBuild, pickBuild, presetFor, savePreset,
    touchSaved, runOptimizer, cancelRun, runWeapons, stopWeapons, toast]);

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
