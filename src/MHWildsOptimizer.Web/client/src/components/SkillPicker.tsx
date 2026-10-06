import { useMemo, useState } from 'react';
import { useCatalog } from '../state';
import type { Skill, SkillKind } from '../types';
import { Button, SearchBox, Segmented, SkillIcon, Stepper } from './common';

type KindFilter = 'all' | 'weapon' | 'armor';

/**
 * Pick skills with a level. mode "target" = minimum level the build must reach (1..max);
 * mode "limit" = cap for the score (0 = exclude the skill, n = value it only up to n).
 */
export function SkillPicker({ values, onChange, mode, highlight }: {
  values: Record<string, number>;
  onChange: (values: Record<string, number>) => void;
  mode: 'target' | 'limit';
  /** skills to mark (e.g. the ones the optimizer values under the current conditions) */
  highlight?: Set<string>;
}) {
  const { catalog } = useCatalog();
  const [query, setQuery] = useState('');
  const [kind, setKind] = useState<KindFilter>('all');

  const skills = useMemo(() => (catalog?.skills ?? []).filter((s) => s.kind === 'armor' || s.kind === 'weapon'), [catalog]);
  const selected = useMemo(() => skills.filter((s) => s.name in values), [skills, values]);
  const available = useMemo(() => {
    const q = query.trim().toLowerCase();
    return skills
      .filter((s) => !(s.name in values))
      .filter((s) => kind === 'all' || s.kind === kind)
      .filter((s) => !q || s.name.toLowerCase().includes(q) || (s.description ?? '').toLowerCase().includes(q))
      .sort((a, b) => (highlight?.has(b.name) ? 1 : 0) - (highlight?.has(a.name) ? 1 : 0) || a.name.localeCompare(b.name));
  }, [skills, values, kind, query, highlight]);

  const set = (name: string, level: number | null) => {
    const next = { ...values };
    if (level === null) delete next[name];
    else next[name] = level;
    onChange(next);
  };

  const add = (s: Skill) => set(s.name, mode === 'target' ? s.max_level : 1);
  const min = mode === 'target' ? 1 : 0;

  return (
    <div className="skillpicker">
      <div className="selected">
        {selected.length === 0 && <p className="muted">Nothing selected yet. Pick skills from the list below.</p>}
        {selected.map((s) => {
          const level = values[s.name];
          const rank = s.ranks.find((r) => r.level === level);
          return (
            <div key={s.name} className={`skillrow ${s.kind}`}>
              <SkillIcon name={s.name} size={28} />
              <div className="skillrow-main">
                <div className="skillrow-title">
                  <b>{s.name}</b> <KindTag kind={s.kind} /> <span className="muted">max {s.max_level}</span>
                </div>
                <div className="muted small">
                  {mode === 'limit' && level === 0 ? 'Excluded from the optimization entirely.' : rank?.description ?? s.description}
                  {mode === 'limit' && level > 0 && <> · levels above {level} are shown but not valued</>}
                </div>
              </div>
              <div className="skillrow-actions">
                <Stepper value={level} min={min} max={s.max_level} onChange={(v) => set(s.name, v)} />
                {mode === 'limit' && level > 0 && <Button small kind="ghost" onClick={() => set(s.name, 0)}>exclude</Button>}
                <button type="button" className="iconbtn" title="remove" onClick={() => set(s.name, null)}>×</button>
              </div>
            </div>
          );
        })}
      </div>

      <div className="picker-tools">
        <SearchBox value={query} onChange={setQuery} placeholder="Search skills…" />
        <Segmented<KindFilter> small value={kind} onChange={setKind} options={[{ value: 'all', label: 'All' }, { value: 'weapon', label: 'Weapon skills' }, { value: 'armor', label: 'Armor skills' }]} />
        <span className="muted small">{available.length} skills</span>
      </div>
      <div className="available">
        {available.map((s) => (
          <button key={s.name} type="button" className={`skillopt ${s.kind}` + (highlight?.has(s.name) ? ' hl' : '')} onClick={() => add(s)} title={s.description ?? ''}>
            <SkillIcon name={s.name} size={22} />
            <span className="name">{s.name}</span>
            <KindTag kind={s.kind} />
            <span className="muted">max {s.max_level}</span>
            <span className="desc muted">{s.description}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

export function KindTag({ kind }: { kind: SkillKind }) {
  return <span className={`kindtag ${kind}`}>{kind}</span>;
}
