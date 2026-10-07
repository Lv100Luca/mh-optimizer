import type { ReactNode } from 'react';
import { armorKinds, pieceLabel } from '../builds';
import type { Build, Deco } from '../types';
import { DecoIcon, SkillChip, SkillHover, SkillIcon, fmt, titleCase } from './common';

/** "+12.3" / "−4.0" / "±0" with the up / down tone (up = the first build is better). */
export function Delta({ value, digits = 1, suffix, children }: { value: number; digits?: number; suffix?: string; children?: ReactNode }) {
  const rounded = Number(value.toFixed(digits));
  const tone = rounded > 0 ? 'up' : rounded < 0 ? 'down' : 'same';
  const text = rounded === 0 ? '±0' : (rounded > 0 ? '+' : '−') + fmt(Math.abs(rounded), digits);
  return <span className={'delta ' + tone}>{text}{suffix}{children}</span>;
}

/** Score difference of a build against a reference: "+12.3 (+1.7%) vs Optimizer build 1". */
export function ScoreDelta({ score, reference, label }: { score: number; reference: number; label: ReactNode }) {
  const pct = reference !== 0 ? ((score - reference) / reference) * 100 : 0;
  return (
    <span className="score-delta">
      <Delta value={score - reference} /> <span className="muted">({pct >= 0 ? '+' : '−'}{fmt(Math.abs(pct), 1)}%) vs {label}</span>
    </span>
  );
}

interface Metric { label: string; a: number; b: number; digits: number; suffix?: string }

/** Side-by-side comparison of two scored builds; differences are "a − b" (positive = a is better). */
export function BuildCompare({ a, aLabel, b, bLabel }: { a: Build; aLabel: string; b: Build; bLabel: string }) {
  const sa = a.summary;
  const sb = b.summary;
  const metrics: Metric[] = [
    { label: 'Score', a: a.score, b: b.score, digits: 1 },
    { label: 'EFR', a: a.efr, b: b.efr, digits: 1 },
    { label: 'EFE', a: a.efe, b: b.efe, digits: 1 },
    ...(a.procs > 0 || b.procs > 0 ? [{ label: 'Procs', a: a.procs, b: b.procs, digits: 1 }] : []),
    { label: 'Every condition on', a: sa.total_all_conditions, b: sb.total_all_conditions, digits: 1 },
    { label: 'Attack (true)', a: sa.attack, b: sb.attack, digits: 1 },
    { label: 'Affinity', a: sa.affinity, b: sb.affinity, digits: 0, suffix: '%' },
    { label: 'Crit multiplier', a: sa.crit_multiplier, b: sb.crit_multiplier, digits: 2 },
    ...(sa.element !== 'none' || sb.element !== 'none' ? [{ label: `Element (true${sa.element !== 'none' ? ', ' + titleCase(sa.element) : ''})`, a: sa.element_true, b: sb.element_true, digits: 1 }] : []),
  ];

  const levelsA = new Map(a.skills.map((s) => [s.skill, s.level]));
  const levelsB = new Map(b.skills.map((s) => [s.skill, s.level]));
  const skills = [...new Set([...levelsA.keys(), ...levelsB.keys()])]
    .map((skill) => ({ skill, la: levelsA.get(skill) ?? 0, lb: levelsB.get(skill) ?? 0 }))
    .sort((x, y) => Math.abs(y.la - y.lb) - Math.abs(x.la - x.lb) || y.la + y.lb - (x.la + x.lb) || x.skill.localeCompare(y.skill));
  const changed = skills.filter((s) => s.la !== s.lb);
  const same = skills.filter((s) => s.la === s.lb);

  const bonusesA = [...sa.active_set_bonuses, ...sa.active_group_skills];
  const bonusesB = [...sb.active_set_bonuses, ...sb.active_group_skills];
  const groups = new Set([...sa.active_group_skills, ...sb.active_group_skills]);

  const rows: { label: string; a: EquipCell; b: EquipCell }[] = [
    { label: 'Weapon', a: weaponCell(a), b: weaponCell(b) },
    ...armorKinds.map((k) => ({ label: pieceLabel[k], a: armorCell(a, k), b: armorCell(b, k) })),
    { label: 'Talisman', a: talismanCell(a), b: talismanCell(b) },
  ];

  return (
    <div className="compare">
      <table className="table compare-metrics">
        <thead><tr><th></th><th className="num">{aLabel}</th><th className="num">{bLabel}</th><th className="num">Difference</th></tr></thead>
        <tbody>
          {metrics.map((m) => (
            <tr key={m.label} className={m.label === 'Score' ? 'headline' : ''}>
              <td className="muted">{m.label}</td>
              <td className="num">{fmt(m.a, m.digits)}{m.suffix}</td>
              <td className="num">{fmt(m.b, m.digits)}{m.suffix}</td>
              <td className="num"><Delta value={m.a - m.b} digits={m.digits} suffix={m.suffix} /></td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="compare-cols">
        <div>
          <h4>Skills that differ</h4>
          {changed.length === 0 ? <p className="muted small">Same skill levels.</p> : (
            <table className="table compare-skills">
              <thead><tr><th>Skill</th><th className="num">{aLabel}</th><th className="num">{bLabel}</th><th className="num"></th></tr></thead>
              <tbody>
                {changed.map((s) => (
                  <tr key={s.skill}>
                    <td><SkillChip name={s.skill} /></td>
                    <td className="num">{s.la || <span className="muted">–</span>}</td>
                    <td className="num">{s.lb || <span className="muted">–</span>}</td>
                    <td className="num"><Delta value={s.la - s.lb} digits={0} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {same.length > 0 && (
            <details className="small">
              <summary>{same.length} skill{same.length === 1 ? '' : 's'} at the same level</summary>
              <div className="chip-row">{same.map((s) => <SkillChip key={s.skill} name={s.skill} level={s.la} />)}</div>
            </details>
          )}
        </div>
        <div>
          <h4>Set bonuses and group skills</h4>
          <BonusList title={`Only ${aLabel}`} groups={groups} items={bonusesA.filter((x) => !bonusesB.includes(x))} />
          <BonusList title={`Only ${bLabel}`} groups={groups} items={bonusesB.filter((x) => !bonusesA.includes(x))} />
          <BonusList title="Both" groups={groups} items={bonusesA.filter((x) => bonusesB.includes(x))} />
        </div>
      </div>

      <h4>Equipment</h4>
      <table className="table compare-equip">
        <thead><tr><th></th><th>{aLabel}</th><th>{bLabel}</th></tr></thead>
        <tbody>
          {rows.map((r) => {
            const differs = r.a.key !== r.b.key;
            return (
              <tr key={r.label} className={differs ? 'differs' : ''}>
                <td className="muted piece">{r.label}</td>
                <td><EquipView cell={r.a} /></td>
                <td><EquipView cell={r.b} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function BonusList({ title, items, groups }: { title: string; items: string[]; groups: Set<string> }) {
  if (items.length === 0) return null;
  return (
    <div className="bonuslist">
      <span className="muted small">{title}</span>
      <span className="chip-row">
        {items.map((x) => {
          const bare = x.replace(/ \(.*\)$/, '').replace(/ (I|II)$/, '');
          const kind = groups.has(x) ? 'group' : 'set';
          return <SkillHover key={x} name={x} className={'chip skill ' + kind}><SkillIcon name={bare} kind={kind} size={18} />{x}</SkillHover>;
        })}
      </span>
    </div>
  );
}

interface EquipCell { key: string; name: string | null; decos: (Deco | null)[] }

const decoKey = (decos: (Deco | null)[]) => decos.map((d) => d?.name ?? '-').join(',');

function weaponCell(b: Build): EquipCell {
  const pair = `${b.weapon.set_bonus ?? '-'} / ${b.weapon.group_skill ?? '-'}`;
  return { key: pair + '|' + decoKey(b.weapon.decorations), name: pair, decos: b.weapon.decorations };
}

function armorCell(b: Build, kind: string): EquipCell {
  const a = b.armor.find((x) => x.kind === kind);
  return a ? { key: a.name + (a.transcended ? 'T' : '') + '|' + decoKey(a.decorations), name: a.name + (a.transcended ? ' (transcended)' : ''), decos: a.decorations } : { key: '-', name: null, decos: [] };
}

function talismanCell(b: Build): EquipCell {
  const t = b.talisman;
  return t ? { key: t.name + '|' + decoKey(t.decorations), name: t.name, decos: t.decorations } : { key: '-', name: null, decos: [] };
}

function EquipView({ cell }: { cell: EquipCell }) {
  if (!cell.name) return <span className="muted">–</span>;
  return (
    <span className="equipview">
      <span>{cell.name}</span>
      <span className="equipview-decos">
        {cell.decos.map((d, i) => <DecoIcon key={i} deco={d} size={20} />)}
      </span>
    </span>
  );
}
