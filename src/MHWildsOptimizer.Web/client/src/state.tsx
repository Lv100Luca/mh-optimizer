import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api } from './api';
import type { Catalog, ConfigPayload, ConfigSummary, OptimizationRequest, OptimizationResult, OptimizeEvent, Resolved, TalismanInput, WeaponInput } from './types';

export type Tab = 'weapon' | 'pair' | 'targets' | 'limits' | 'conditions' | 'talismans' | 'options' | 'review' | 'results';

export type RunStatus = 'idle' | 'running' | 'done' | 'error' | 'cancelled';

export interface RunState {
  status: RunStatus;
  log: string[];
  result: OptimizationResult | null;
  error: string | null;
  startedAt: number | null;
  finishedAt: number | null;
  /** true when the result was loaded from inputs/<name>.results.json rather than produced in this session */
  fromDisk: boolean;
}

const idleRun: RunState = { status: 'idle', log: [], result: null, error: null, startedAt: null, finishedAt: null, fromDisk: false };

export interface AppState {
  catalog: Catalog | null;
  loadError: string | null;
  configs: ConfigSummary[];
  name: string | null;
  request: OptimizationRequest | null;
  talismans: TalismanInput[];
  dirty: boolean;
  resolved: Resolved | null;
  resolving: boolean;
  run: RunState;
  tab: Tab;
  notice: { text: string; kind: 'info' | 'error' } | null;
}

export interface AppActions {
  setTab(tab: Tab): void;
  refreshConfigs(): Promise<void>;
  newConfig(): void;
  openConfig(name: string): Promise<void>;
  save(name?: string): Promise<boolean>;
  remove(name: string): Promise<void>;
  patchRequest(fn: (r: OptimizationRequest) => OptimizationRequest): void;
  patchWeapon(patch: Partial<WeaponInput>): void;
  setTalismans(fn: (t: TalismanInput[]) => TalismanInput[]): void;
  runOptimizer(): Promise<void>;
  cancelRun(): void;
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
  const [configs, setConfigs] = useState<ConfigSummary[]>([]);
  const [name, setName] = useState<string | null>(null);
  const [request, setRequest] = useState<OptimizationRequest | null>(null);
  const [talismans, setTalismansState] = useState<TalismanInput[]>([]);
  const [dirty, setDirty] = useState(false);
  const [resolved, setResolved] = useState<Resolved | null>(null);
  const [resolving, setResolving] = useState(false);
  const [run, setRun] = useState<RunState>(idleRun);
  const [tab, setTab] = useState<Tab>('weapon');
  const [toastState, setToastState] = useState<AppState['notice']>(null);
  const abortRun = useRef<AbortController | null>(null);

  const toast = useCallback((text: string, kind: 'info' | 'error' = 'info') => {
    setToastState({ text, kind });
    window.setTimeout(() => setToastState((t) => (t?.text === text ? null : t)), kind === 'error' ? 6000 : 3000);
  }, []);

  const refreshConfigs = useCallback(async () => {
    try { setConfigs(await api.configs()); } catch (e) { toast(`Could not list configurations: ${(e as Error).message}`, 'error'); }
  }, [toast]);

  // initial load: catalog + configs; start from a new request
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [cat, list] = await Promise.all([api.catalog(), api.configs()]);
        if (cancelled) return;
        setCatalog(cat);
        setConfigs(list);
        setRequest(structuredClone(cat.default_request));
      } catch (e) {
        if (!cancelled) setLoadError((e as Error).message);
      }
    })();
    return () => { cancelled = true; };
  }, []);

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

  const newConfig = useCallback(() => {
    if (!catalog) return;
    abortRun.current?.abort();
    setName(null);
    setRequest(structuredClone(catalog.default_request));
    setTalismansState([]);
    setDirty(false);
    setRun(idleRun);
    setTab('weapon');
  }, [catalog]);

  const openConfig = useCallback(async (configName: string) => {
    try {
      const file = await api.loadConfig(configName);
      abortRun.current?.abort();
      setName(file.name);
      setRequest(file.request);
      setTalismansState(file.talismans);
      setDirty(false);
      if (file.has_results) {
        try {
          const result = await api.results(file.name);
          setRun({ ...idleRun, status: 'done', result, fromDisk: true });
        } catch {
          setRun(idleRun);
        }
      } else setRun(idleRun);
      setTab('weapon');
    } catch (e) {
      toast(`Could not open ${configName}: ${(e as Error).message}`, 'error');
    }
  }, [toast]);

  const save = useCallback(async (saveAs?: string) => {
    if (!request) return false;
    const target = saveAs ?? name;
    if (!target) return false;
    try {
      const file = await api.saveConfig(target, { request, talismans });
      setName(file.name);
      setRequest(file.request);
      setDirty(false);
      await refreshConfigs();
      toast(`Saved inputs/${file.name}.json`);
      return true;
    } catch (e) {
      toast(`Save failed: ${(e as Error).message}`, 'error');
      return false;
    }
  }, [name, request, talismans, refreshConfigs, toast]);

  const remove = useCallback(async (configName: string) => {
    try {
      await api.deleteConfig(configName);
      toast(`Deleted ${configName}`);
      if (configName === name) newConfig();
      await refreshConfigs();
    } catch (e) {
      toast(`Delete failed: ${(e as Error).message}`, 'error');
    }
  }, [name, newConfig, refreshConfigs, toast]);

  const patchRequest = useCallback((fn: (r: OptimizationRequest) => OptimizationRequest) => {
    setRequest((r) => (r ? fn(r) : r));
    setDirty(true);
  }, []);

  const patchWeapon = useCallback((patch: Partial<WeaponInput>) => {
    patchRequest((r) => ({ ...r, weapon: { ...r.weapon, ...patch } }));
  }, [patchRequest]);

  const setTalismans = useCallback((fn: (t: TalismanInput[]) => TalismanInput[]) => {
    setTalismansState((t) => fn(t));
    setDirty(true);
  }, []);

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
      await api.optimize(payload, name, (e: OptimizeEvent) => {
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
      if (name) refreshConfigs();
    } catch (e) {
      if (controller.signal.aborted) return;
      setRun((r) => ({ ...r, status: 'error', error: (e as Error).message, finishedAt: Date.now() }));
    }
  }, [name, request, talismans, refreshConfigs]);

  const value = useMemo<AppState & AppActions>(() => ({
    catalog, loadError, configs, name, request, talismans, dirty, resolved, resolving, run, tab, notice: toastState,
    setTab, refreshConfigs, newConfig, openConfig, save, remove, patchRequest, patchWeapon, setTalismans, runOptimizer, cancelRun,
    toast,
  }), [catalog, loadError, configs, name, request, talismans, dirty, resolved, resolving, run, tab, toastState,
    refreshConfigs, newConfig, openConfig, save, remove, patchRequest, patchWeapon, setTalismans, runOptimizer, cancelRun, toast]);

  return <StateContext.Provider value={value}>{children}</StateContext.Provider>;
}

/** Lookups over the catalog that panels share. */
export function useCatalog() {
  const { catalog } = useApp();
  return useMemo(() => {
    const skillsByName = new Map(catalog?.skills.map((s) => [s.name, s]) ?? []);
    const setsByName = new Map(catalog?.armor_sets.map((s) => [s.name, s]) ?? []);
    const weaponTypes = new Map(catalog?.weapon_types.map((w) => [w.kind, w]) ?? []);
    return { catalog, skillsByName, setsByName, weaponTypes };
  }, [catalog]);
}
