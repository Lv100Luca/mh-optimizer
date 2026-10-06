import { useMemo, useState } from 'react';
import { useCatalog } from '../state';
import type { Skill, SkillKind } from '../types';
import { Button, SearchBox, Segmented, SkillIcon, Stepper } from './common';

type KindFilter = 'all' | SkillKind;

/** Pieces a set bonus tier / group skill needs, from the rank data (same fallback as RequestLoader.PiecesRequired). */
export function piecesRequired(s: Skill, level: number) {
  return s.ranks.find((r) => r.level === level)?.pieces_required ?? (s.kind === 'group' ? 3 : level >= 2 ? 4 : 2);
}

export const roman = (n: number) => ['', 'I', 'II', 'III'][n] ?? String(n);

const isBonus = (s: Skill) => s.kind === 'set' || s.kind === 'group';

/** "I = 2 pieces · II = 4 pieces" for a set bonus, "3 pieces" for a group skill. */
function piecesText(s: Skill) {
  return s.kind === 'set'
    ? s.ranks.map((r) => `${roman(r.level)} = ${piecesRequired(s, r.level)} pieces`).join(' · ')
    : `${piecesRequired(s, 1)} pieces`;
}

/**
 * Pick skills with a level. mode "target" = minimum level the build must reach (1..max); set bonuses and group skills can be
 * targets too, with the tier as the level (set I = 2 pieces, II = 4 pieces; group = 3 pieces; the weapon counts when it rolled it).
 * mode "limit" = cap for the score (0 = exclude the skill, n = value it only up to n); armor and weapon skills only.
 */
export function SkillPicker({ values, onChange, mode, highlight }: {
  values: Record<string, number>;
  onChange: (values: Record<string, number>) => void;
  mode: 'target' | 'limit';
  /** skills, set bonuses and group skills to mark (e.g. the ones the optimizer values under the current conditions) */
  highlight?: Set<string>;
}) {
  const { catalog } = useCatalog();
  const [query, setQuery] = useState('');
  const [kind, setKind] = useState<KindFilter>('all');

  const skills = useMemo(
    () => (catalog?.skills ?? []).filter((s) => mode === 'target' || s.kind === 'armor' || s.kind === 'weapon'),
    [catalog, mode],
  );
  const selected = useMemo(() => skills.filter((s) => s.name in values), [skills, values]);
  const available = useMemo(() => {
    const q = query.trim().toLowerCase();
    return skills
      .filter((s) => !(s.name in values))
      .filter((s) => kind === 'all' || s.kind === kind)
      .filter((s) => !q || s.name.toLowerCase().includes(q) || (s.description ?? '').toLowerCase().includes(q) || s.ranks.some((r) => (r.name ?? '').toLowerCase().includes(q)))
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
  const kinds: { value: KindFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'weapon', label: 'Weapon skills' },
    { value: 'armor', label: 'Armor skills' },
    ...(mode === 'target' ? [{ value: 'set' as const, label: 'Set bonuses' }, { value: 'group' as const, label: 'Group skills' }] : []),
  ];

  return (
    <div className="skillpicker">
      <div className="selected">
        {selected.length === 0 && <p className="muted">Nothing selected yet. Pick from the list below.</p>}
        {selected.map((s) => {
          const level = values[s.name];
          const rank = s.ranks.find((r) => r.level === level);
          const bonus = isBonus(s);
          return (
            <div key={s.name} className={`skillrow ${s.kind}`}>
              <SkillIcon name={s.name} size={28} />
              <div className="skillrow-main">
                <div className="skillrow-title">
                  <b>{s.name}</b> <KindTag kind={s.kind} /> <span className="muted">{bonus ? piecesText(s) : `max ${s.max_level}`}</span>
                </div>
                <div className="muted small">
                  {mode === 'limit' && level === 0
                    ? 'Excluded from the optimization entirely.'
                    : bonus && rank?.name
                      ? `${rank.name}: ${rank.description ?? ''}`
                      : rank?.description ?? s.description}
                  {mode === 'limit' && level > 0 && <> · levels above {level} are shown but not valued</>}
                  {bonus && <> · every build must carry {piecesRequired(s, level)} pieces; the weapon counts when it rolled this bonus</>}
                </div>
              </div>
              <div className="skillrow-actions">
                {s.kind === 'set' ? (
                  <Segmented<number> small value={level} onChange={(v) => set(s.name, v)}
                    options={s.ranks.map((r) => ({ value: r.level, label: `${roman(r.level)} · ${piecesRequired(s, r.level)} pc`, hint: r.name ?? undefined }))} />
                ) : s.kind === 'group' ? (
                  <span className="muted small">{piecesRequired(s, 1)} pieces</span>
                ) : (
                  <Stepper value={level} min={min} max={s.max_level} onChange={(v) => set(s.name, v)} />
                )}
                {mode === 'limit' && level > 0 && <Button small kind="ghost" onClick={() => set(s.name, 0)}>exclude</Button>}
                <button type="button" className="iconbtn" title="remove" onClick={() => set(s.name, null)}>×</button>
              </div>
            </div>
          );
        })}
      </div>

      <div className="picker-tools">
        <SearchBox value={query} onChange={setQuery} placeholder={mode === 'target' ? 'Search skills, set bonuses, group skills…' : 'Search skills…'} />
        <Segmented<KindFilter> small value={kind} onChange={setKind} options={kinds} />
        <span className="muted small">{available.length} {kind === 'set' ? 'set bonuses' : kind === 'group' ? 'group skills' : 'skills'}</span>
      </div>
      <div className="available">
        {available.map((s) => (
          <button key={s.name} type="button" className={`skillopt ${s.kind}` + (highlight?.has(s.name) ? ' hl' : '')} onClick={() => add(s)} title={s.description ?? s.ranks[0]?.description ?? ''}>
            <SkillIcon name={s.name} size={22} />
            <span className="name">{s.name}</span>
            <KindTag kind={s.kind} />
            <span className="muted">{isBonus(s) ? piecesText(s) : `max ${s.max_level}`}</span>
            <span className="desc muted">{s.description ?? s.ranks[0]?.description}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

export function KindTag({ kind }: { kind: SkillKind }) {
  return <span className={`kindtag ${kind}`}>{kind}</span>;
}
