import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import { elementCss, icons } from '../icons';
import { useApp, useCatalog } from '../state';
import type { Build, InventoryEntry } from '../types';
import { Alert, Button, Section, fmt, titleCase } from './common';
import { BuildCompare, Delta } from './BuildCompare';

/** The build a weapon is compared by: its pick (scored now) or else the best build of its last run. */
function representative(e: InventoryEntry): { build: Build; label: string; source: 'picked' | 'run' } | null {
  if (e.picked?.build) return { build: e.picked.build, label: `${e.name}: ${e.picked.name}`, source: 'picked' };
  if (e.last_run) return { build: e.last_run.build, label: `${e.name}: last run`, source: 'run' };
  return null;
}

const ago = (iso: string) => {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  return hours < 48 ? `${hours} h ago` : `${Math.round(hours / 24)} days ago`;
};

export function InventoryPanel() {
  const { profile, name: openName, savedVersion, openWeapon, newWeapon, dirty, batch: runs, runWeapons: runAll, stopWeapons: cancelRuns } = useApp();
  const { catalog, weaponTypes } = useCatalog();
  const [entries, setEntries] = useState<InventoryEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [compare, setCompare] = useState<string[]>([]);
  const [newKind, setNewKind] = useState('great-sword');

  useEffect(() => {
    if (!profile) return;
    const controller = new AbortController();
    api.inventory(profile, controller.signal)
      .then((list) => { setEntries(list); setError(null); })
      .catch((e) => { if (!controller.signal.aborted) setError((e as Error).message); });
    return () => controller.abort();
  }, [profile, savedVersion]);

  const groups = useMemo(() => {
    const byType = new Map<string, InventoryEntry[]>();
    for (const e of entries ?? []) byType.set(e.type, [...(byType.get(e.type) ?? []), e]);
    return [...byType.entries()];
  }, [entries]);

  if (!catalog || !profile) return null;

  const running = Object.values(runs).some((r) => r.status === 'running' || r.status === 'queued');
  const stale = (entries ?? []).filter((e) => (e.stale || !e.last_run) && e.errors.length === 0);
  const typeLabel = (kind: string) => weaponTypes.get(kind)?.label ?? titleCase(kind);

  const toggleCompare = (weapon: string) =>
    setCompare((c) => (c.includes(weapon) ? c.filter((x) => x !== weapon) : [...c.slice(-1), weapon]));

  const open = (weapon: string, tab?: 'builds' | 'results') => {
    if (dirty && !window.confirm('Discard unsaved changes?')) return;
    void openWeapon(weapon, tab);
  };

  const compared = compare.map((n) => entries?.find((e) => e.name === n)).filter((e): e is InventoryEntry => !!e);
  const [ca, cb] = compared.map(representative);

  return (
    <div className="panel">
      <Section
        title={`Weapons of ${profile}`}
        hint="Each weapon keeps its own targets, builds and last optimizer run; talismans and the weapon-type condition presets are shared by the whole profile. A weapon is compared by its picked build (★, scored with today's conditions and talismans) or else by the best build of its last run."
        actions={
          <div className="btn-row">
            {running ? (
              <Button small kind="danger" onClick={cancelRuns}>Stop runs</Button>
            ) : (
              <Button small onClick={() => void runAll(stale.map((e) => e.name))} disabled={stale.length === 0}
                title="Run the optimizer again for every weapon whose last run no longer matches its request or the talismans (one after another, saved setups only)">
                ▶ Run stale ({stale.length})
              </Button>
            )}
            <select className="select" value={newKind} onChange={(e) => setNewKind(e.target.value)} title="Weapon type of the new weapon">
              {catalog.weapon_types.filter((w) => w.supported).map((w) => <option key={w.kind} value={w.kind}>{w.label}</option>)}
            </select>
            <Button small kind="primary" onClick={() => { if (!dirty || window.confirm('Discard unsaved changes?')) newWeapon(newKind); }}>+ New weapon</Button>
          </div>
        }
      >
        {error && <Alert kind="error">Could not load the inventory: {error}</Alert>}
        {entries === null && !error && <p className="muted">Loading…</p>}
        {entries?.length === 0 && (
          <div className="empty">
            <p>No weapons yet. Set one up (Weapon, targets, conditions), then <b>Save as…</b> to add it to {profile}.</p>
          </div>
        )}
        {groups.map(([kind, list]) => {
          const scored = list.map((e) => representative(e)?.build.score ?? null);
          const top = Math.max(...scored.map((s) => s ?? -Infinity));
          return (
            <div key={kind} className="inv-group">
              <h3 className="with-icon"><img src={icons.weapon(kind)} alt="" width={22} height={22} />{typeLabel(kind)} <span className="muted small">· {list.length} weapon{list.length > 1 ? 's' : ''}</span></h3>
              <table className="table inv-table">
                <thead>
                  <tr>
                    <th title="Pick two to compare">⇄</th>
                    <th>Weapon</th>
                    <th>Picked build (scored now)</th>
                    <th className="num">Score</th>
                    <th>Last run</th>
                    <th className="num" title="Best build of the last run minus the picked build">Run vs pick</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {list.map((e, i) => {
                    const rep = representative(e);
                    const pickedBuild = e.picked?.build ?? null;
                    const runBuild = e.last_run?.build ?? null;
                    const mark = runs[e.name];
                    const unmet = e.picked?.targets.filter((t) => !t.met).length ?? 0;
                    const element = e.weapon?.element ?? 'none';
                    return (
                      <tr key={e.name} className={(e.name === openName ? 'inv-open ' : '') + (compare.includes(e.name) ? 'inv-compared' : '')}>
                        <td><input type="checkbox" checked={compare.includes(e.name)} onChange={() => toggleCompare(e.name)} disabled={!rep} aria-label={`Compare ${e.name}`} /></td>
                        <td>
                          <button className="linklike inv-name" onClick={() => open(e.name)}>{e.name}</button>
                          {element !== 'none' && <span className="elem-dot" style={{ background: elementCss[element] }} title={titleCase(element)} />}
                          <div className="muted small">{e.weapon ? `${e.weapon.set_bonus ?? '-'} / ${e.weapon.group_skill ?? '-'}` : ''}</div>
                          <div className="muted tiny">{e.targets.filter((t) => !t.from_core).map((t) => t.label).join(', ') || 'no targets'}</div>
                          {e.errors.map((x) => <div key={x} className="err small">{x}</div>)}
                        </td>
                        <td>
                          {e.picked ? (
                            <>
                              <span className="pickstar">★</span> {e.picked.name}
                              {unmet > 0 && <div className="warn small">{unmet} target{unmet > 1 ? 's' : ''} not met</div>}
                              {e.picked.errors.map((x) => <div key={x} className="err small">{x}</div>)}
                            </>
                          ) : (
                            <span className="muted small">{e.builds ? `${e.builds} build${e.builds > 1 ? 's' : ''}, none picked` : 'none'}</span>
                          )}
                        </td>
                        <td className="num">
                          {rep ? <b className={scored[i] === top && list.length > 1 ? 'ok' : undefined}>{fmt(rep.build.score)}</b> : <span className="muted">—</span>}
                          {rep && <div className="muted tiny">EFR {fmt(rep.build.efr, 0)} + EFE {fmt(rep.build.efe, 0)}{rep.build.procs > 0 ? ` + ${fmt(rep.build.procs, 0)}` : ''}{rep.source === 'run' ? ' (run)' : ''}</div>}
                        </td>
                        <td>
                          {mark && mark.status !== 'done' ? (
                            <span className={mark.status === 'error' ? 'err small' : 'small'}>
                              {mark.status === 'queued' ? 'queued' : mark.status === 'running' ? <><span className="spinner" /> {mark.message ?? 'running…'}</> : mark.message}
                            </span>
                          ) : e.last_run ? (
                            <>
                              <b>{fmt(e.last_run.build.score)}</b> <span className="muted small">{ago(e.last_run.completed_at)}</span>
                              {e.stale && <span className="stale-badge" title="The request or the talismans changed since this run">stale</span>}
                            </>
                          ) : (
                            <span className="muted small">never run</span>
                          )}
                        </td>
                        <td className="num">{pickedBuild && runBuild ? <Delta value={runBuild.score - pickedBuild.score} /> : <span className="muted">—</span>}</td>
                        <td>
                          <div className="btn-row nowrap">
                            <Button small onClick={() => open(e.name, 'builds')}>Builds</Button>
                            {e.last_run && <Button small kind="ghost" onClick={() => open(e.name, 'results')}>Results</Button>}
                            <Button small kind="ghost" onClick={() => void runAll([e.name])} disabled={running || e.errors.length > 0} title="Run the optimizer for this weapon (saved setup)">▶</Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          );
        })}
        {groups.length > 1 && (
          <p className="muted small">
            Scores are damage per 100 motion value, so they rank weapons of the same type. Across types they ignore attack speed and motion values,
            so compare a Great Sword with a Bow by their builds, not by the number.
          </p>
        )}
      </Section>

      {compared.length > 0 && (
        <Section
          title="Compare weapons"
          hint="Each side is the weapon's picked build, or the best build of its last run when nothing is picked. Differences are left minus right."
          actions={<Button small kind="ghost" onClick={() => setCompare([])}>Clear</Button>}
        >
          {compared.length < 2 && <p className="muted">Tick a second weapon to compare with {compared[0].name}.</p>}
          {ca && cb && compared[0].type !== compared[1].type && (
            <Alert kind="info">{typeLabel(compared[0].type)} vs {typeLabel(compared[1].type)}: the scores are per 100 MV, so the difference says little about real damage.</Alert>
          )}
          {ca && cb && <BuildCompare a={ca.build} aLabel={ca.label} b={cb.build} bLabel={cb.label} />}
        </Section>
      )}
    </div>
  );
}
