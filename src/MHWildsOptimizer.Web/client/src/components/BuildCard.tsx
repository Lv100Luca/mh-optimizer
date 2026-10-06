import { useState } from 'react';
import { icons, elementCss, sharpnessCss } from '../icons';
import type { Build, Stats } from '../types';
import { DecoIcon, RarityBadge, SkillChip, SkillIcon, Slots, fmt, titleCase } from './common';

const pieceLabel: Record<string, string> = { head: 'Head', chest: 'Chest', arms: 'Arms', waist: 'Waist', legs: 'Legs' };

export function BuildCard({ build: b, best }: { build: Build; best?: boolean }) {
  const [open, setOpen] = useState(false);
  const s = b.summary;
  const w = b.weapon;

  return (
    <article className={'build' + (best ? ' best' : '')}>
      <header className="build-head">
        <div className="build-rank">Build {b.rank}</div>
        <div className="score">
          <span className="efr">EFR {fmt(b.efr)}</span>
          <span className="plus">+</span>
          <span className="efe">EFE {fmt(b.efe)}</span>
          {b.procs > 0 && (
            <>
              <span className="plus">+</span>
              <span className="procs" title={s.proc_sources.join(', ')}>procs {fmt(b.procs)}</span>
            </>
          )}
          <span className="eq">=</span>
          <span className="total">{fmt(b.score)}</span>
          <span className="muted small">every condition on: {fmt(s.total_all_conditions)}</span>
        </div>
        <div className="statchips">
          <span className="statchip atk" title={`base ${s.base_attack} true`}><img src={icons.attack} alt="" />ATK <b>{fmt(s.attack, 0)}</b> <small>{s.display_attack} display</small></span>
          <span className="statchip aff" title={`base ${s.base_affinity}%`}><img src={icons.affinity} alt="" />AFF <b>{s.affinity}%</b></span>
          <span className="statchip crit">CRIT <b>x{fmt(s.crit_multiplier, 2)}</b></span>
          {s.sharpness && <span className="statchip sharp" style={{ ['--tint' as string]: sharpnessCss[s.sharpness] }}><img src={icons.sharpness} alt="" />{titleCase(s.sharpness)}</span>}
          {s.element !== 'none' && (
            <span className="statchip ele" style={{ ['--tint' as string]: elementCss[s.element] }}>
              {icons.element(s.element) && <img src={icons.element(s.element)!} alt="" />}{titleCase(s.element)} <b>{fmt(s.element_true, 0)}</b> <small>{s.element_display} display</small>
            </span>
          )}
        </div>
      </header>

      <div className="build-body">
        <div className="build-row">
          <span className="label">Sets</span>
          <span className="chip-row">
            {s.active_set_bonuses.length === 0 && <span className="muted">-</span>}
            {s.active_set_bonuses.map((x) => <span key={x} className="chip skill set"><SkillIcon name={x.replace(/ (I|II)( \(.*\))?$/, '')} kind="set" size={18} />{x}</span>)}
          </span>
          <span className="label">Groups</span>
          <span className="chip-row">
            {s.active_group_skills.length === 0 && <span className="muted">-</span>}
            {s.active_group_skills.map((x) => <span key={x} className="chip skill group"><SkillIcon name={x.replace(/ \(.*\)$/, '')} kind="group" size={18} />{x}</span>)}
          </span>
        </div>
        <div className="build-row">
          <span className="label">Skills</span>
          <span className="chip-row">
            {b.skills.map((x) => <SkillChip key={x.skill} name={x.skill} level={x.level} effective={x.effective} />)}
          </span>
        </div>
        <p className="why muted">{s.description}</p>

        <table className="table equip">
          <tbody>
            <tr>
              <td className="icon-cell"><img className="icon" src={icons.weapon(w.type)} alt="" width={32} height={32} /></td>
              <td className="piece">Weapon</td>
              <td className="name"><b>{w.label}</b><div className="muted small">{w.true_raw} raw ({w.display_attack} display), {w.affinity}% affinity, {w.element === 'none' ? 'no element' : `${w.element_display} ${titleCase(w.element)}`}, {w.sharpness ? titleCase(w.sharpness) : '-'} sharpness</div></td>
              <td className="skills"><span className="muted small">rolled: {w.set_bonus ?? '-'} / {w.group_skill ?? '-'}</span></td>
              <td className="decos"><SlotDecos slots={w.slots} decos={w.decorations} /></td>
            </tr>
            {b.armor.map((a) => (
              <tr key={a.kind}>
                <td className="icon-cell"><img className="icon" src={icons.armor(a.kind, a.rarity)} alt="" width={32} height={32} /></td>
                <td className="piece">{pieceLabel[a.kind] ?? a.kind}</td>
                <td className="name">
                  <b>{a.name}</b> <RarityBadge rarity={a.rarity} transcended={a.transcended} />
                  <div className="muted small">
                    {a.set_bonus.map((x) => <span key={x} className="with-icon"><SkillIcon name={x} kind="set" size={14} />{x}</span>)}
                    {a.group_skill && <span className="with-icon"><SkillIcon name={a.group_skill} kind="group" size={14} />{a.group_skill}</span>}
                  </div>
                </td>
                <td className="skills"><span className="chip-row">{a.skills.map((g) => <SkillChip key={g.skill} name={g.skill} level={g.level} />)}</span></td>
                <td className="decos"><SlotDecos slots={a.slots} decos={a.decorations} /></td>
              </tr>
            ))}
            <tr>
              <td className="icon-cell">{b.talisman ? <img className="icon" src={icons.talisman(b.talisman.rarity)} alt="" width={32} height={32} /> : null}</td>
              <td className="piece">Talisman</td>
              {b.talisman ? (
                <>
                  <td className="name"><b>{b.talisman.name}</b> <RarityBadge rarity={b.talisman.rarity} /><div className="muted small">{b.talisman.source === 'random' ? 'your random talisman' : 'craftable charm'}</div></td>
                  <td className="skills"><span className="chip-row">{b.talisman.skills.map((g) => <SkillChip key={g.skill} name={g.skill} level={g.level} />)}</span></td>
                  <td className="decos"><SlotDecos slots={b.talisman.slots} decos={b.talisman.decorations} /></td>
                </>
              ) : <td colSpan={3} className="muted">-</td>}
            </tr>
          </tbody>
        </table>

        <div className="build-row">
          <span className="label">Decorations ({b.decorations.reduce((n, d) => n + d.count, 0)})</span>
          <span className="chip-row">
            {b.decorations.length === 0 && <span className="muted">none</span>}
            {b.decorations.map((d) => (
              <span key={d.name} className="chip deco" title={d.skills.map((g) => `${g.skill} ${g.level}`).join(', ')}>
                <DecoIcon deco={d} size={20} />
                <b>{d.count}×</b> {d.name}
                <span className="muted small">{d.skills.map((g) => `${g.skill} ${g.level}`).join(', ')}</span>
              </span>
            ))}
          </span>
        </div>

        <button className="linklike details-toggle" onClick={() => setOpen((o) => !o)}>{open ? '▾ Hide details' : '▸ Skill sources, stats and the text report'}</button>

        {open && (
          <div className="details">
            <h4>Skills with sources</h4>
            <table className="table sources">
              <tbody>
                {b.skills.map((x) => (
                  <tr key={x.skill}>
                    <td><SkillChip name={x.skill} level={x.level} effective={x.effective} /></td>
                    <td className="muted small">{x.sources.join(', ')}{x.wasted > 0 && <span className="warn"> · {x.wasted} wasted</span>}</td>
                  </tr>
                ))}
                <tr>
                  <td className="muted">Set bonuses</td>
                  <td className="small">{b.set_bonuses.length === 0 ? '-' : b.set_bonuses.map((x) => <span key={x.name} className={'pc' + (x.active ? ' active' : '')}>{x.name} {x.pieces}pc{x.active ? ` → ${['', 'I', 'II'][x.tier]}${x.tier_name ? ` (${x.tier_name})` : ''}` : ''}</span>)}</td>
                </tr>
                <tr>
                  <td className="muted">Group skills</td>
                  <td className="small">{b.group_skills.length === 0 ? '-' : b.group_skills.map((x) => <span key={x.name} className={'pc' + (x.active ? ' active' : '')}>{x.name} {x.pieces}pc{x.active ? ` ACTIVE${x.rank_name ? ` (${x.rank_name})` : ''}` : ''}</span>)}</td>
                </tr>
              </tbody>
            </table>

            <div className="stats-compare">
              <StatsBlock title="Under the requested conditions" stats={b.stats_requested} />
              <StatsBlock title="Every conditional skill active" stats={b.stats_all_on} />
            </div>

            <details>
              <summary>Plain-text report</summary>
              <pre className="rawtext">{b.text}</pre>
            </details>
          </div>
        )}
      </div>
    </article>
  );
}

function SlotDecos({ slots, decos }: { slots: (number | string)[]; decos: (import('../types').Deco | null)[] }) {
  if (slots.length === 0) return <span className="muted small">no slots</span>;
  return (
    <span className="slotdecos">
      {slots.map((slot, i) => {
        const level = Number(String(slot).replace(/\D/g, '')) || 0;
        return (
          <span key={i} className="slotdeco">
            <DecoIcon deco={decos[i] ?? null} slotLevel={level} size={28} />
            <span className="muted tiny">{decos[i]?.name ?? ''}</span>
          </span>
        );
      })}
      <span className="muted small"><Slots slots={slots as number[]} /></span>
    </span>
  );
}

function StatsBlock({ title, stats: r }: { title: string; stats: Stats }) {
  return (
    <div className="statsblock">
      <h4>{title}</h4>
      <dl>
        <dt>Attack</dt><dd>{fmt(r.true_raw, 1)} true <span className="muted">({r.display_attack} display)</span></dd>
        <dt>Affinity</dt><dd>{r.affinity}% <span className="muted">crit x{fmt(r.crit_multiplier, 2)}, factor {fmt(r.crit_factor, 3)}</span></dd>
        <dt>Sharpness</dt><dd>{r.sharpness ? titleCase(r.sharpness) : '-'} <span className="muted">raw x{fmt(r.sharpness_raw, 3)}, element x{fmt(r.sharpness_element, 4)}</span></dd>
        {r.element_true > 0 && <><dt>Element</dt><dd>{fmt(r.element_true, 1)} true <span className="muted">({r.element_display} display, cap {fmt(r.element_cap, 1)}, crit element x{fmt(r.crit_element, 2)})</span></dd></>}
        <dt>Score</dt><dd><b>EFR {fmt(r.efr)}</b> + <b>EFE {fmt(r.efe)}</b>{r.procs > 0 && <> + <b>procs {fmt(r.procs)}</b></>} = <b className="total">{fmt(r.total)}</b></dd>
      </dl>
      {r.modifiers.length > 0 && <ul className="mods">{r.modifiers.map((m) => <li key={m}>{m}</li>)}</ul>}
    </div>
  );
}
