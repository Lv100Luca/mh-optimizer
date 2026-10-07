import { icons, elementCss } from '../icons';
import { useApp, useCatalog } from '../state';
import type { ResolvedTarget } from '../types';
import { Alert, Button, Section, SkillChip, SkillHover, SkillIcon, Slots, fmt, titleCase } from './common';
import { PROC_GROUP, attackProfileText } from './ConditionsPanel';
import { roman } from './SkillPicker';
import { SharpnessBar } from './SharpnessBar';

export function ReviewPanel() {
  const { request, talismans, resolved, resolving, runOptimizer, run, setTab, name, dirty } = useApp();
  const { catalog, skillsByName, weaponTypes } = useCatalog();
  if (!request || !catalog) return null;
  const r = resolved;
  const w = r?.weapon;
  const conditionsOn = catalog.conditions.filter((c) => c.group !== PROC_GROUP && request.conditions[c.key] === true);
  const limits = Object.entries(request.conditions.skill_limits ?? {});
  // before the first validation answer, show the request's own targets
  const targets: ResolvedTarget[] = r?.targets ?? Object.entries(request.target_skills).map(([skill, level]) => ({
    skill, level, kind: skillsByName.get(skill)?.kind ?? 'armor', pieces: null, label: `${skill} ${level}`, from_core: false,
  }));

  return (
    <div className="panel">
      <Section
        title="Review"
        hint="The resolved request, exactly as the optimizer will see it."
        actions={
          <Button kind="primary" onClick={() => void runOptimizer()} disabled={!r?.is_valid || run.status === 'running'}>▶ Run optimizer</Button>
        }
      >
        {resolving && !r && <p className="muted">Validating…</p>}
        {r?.errors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        {r?.warnings.map((e) => <Alert key={e} kind="warning">{e}</Alert>)}
        {r?.is_valid && r.warnings.length === 0 && <Alert kind="ok">Everything checks out.</Alert>}
        {name && dirty && <Alert kind="info">Unsaved changes: results of a run are written to inputs/{name}.results.txt, the configuration itself only when you save.</Alert>}

        <div className="review-grid">
          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('weapon')}>Weapon</button></h4>
            {w ? (
              <div className="weapon-head">
                <img src={icons.weapon(w.type)} alt="" />
                <div>
                  <div><b>{w.label}</b> {w.focus ? <span className="muted">· {titleCase(w.focus)} focus</span> : null}</div>
                  <div className="small">
                    <b>{w.display_attack}</b> attack <span className="muted">({w.true_raw} true)</span> · <b>{w.affinity > 0 ? '+' : ''}{w.affinity}%</b> affinity
                    {w.element !== 'none' && <> · <b style={{ color: elementCss[w.element] }}>{w.element_display} {titleCase(w.element)}</b></>}
                    {' '}· {w.sharpness ? titleCase(w.sharpness) : '-'} sharpness · <Slots slots={w.slots} kind="weapon" />
                  </div>
                  {w.sharpness_bar && <SharpnessBar bar={w.sharpness_bar} bonus={w.sharpness_bonus} />}
                  <div className="small muted">rolled: {w.set_bonus ?? '-'} / {w.group_skill ?? '-'}</div>
                </div>
              </div>
            ) : <span className="muted">invalid</span>}
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('pair')}>Skill pair</button></h4>
            <div>{request.skill_pair.mode === 'fixed' ? 'fixed: use the rolled pair' : `optimize over all rollable pairs, report the best ${request.skill_pair.top_n}`}</div>
            {r && <div className="muted small">{r.skill_pair_candidates} candidate pair{r.skill_pair_candidates === 1 ? '' : 's'}</div>}
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('targets')}>Targets</button></h4>
            <div className="chip-row">
              {targets.map((t) => t.kind === 'set' || t.kind === 'group' ? (
                <SkillHover key={t.skill} name={t.skill} level={t.level} className={`chip skill ${t.kind}`}>
                  <SkillIcon name={t.skill} kind={t.kind} size={18} />
                  <span>{t.skill}</span>
                  {t.kind === 'set' && <b>{roman(t.level)}</b>}
                  {t.pieces !== null && <span className="muted small">{t.pieces} pieces</span>}
                </SkillHover>
              ) : (
                <SkillChip key={t.skill} name={t.skill} level={t.level} muted={t.from_core} />
              ))}
              {targets.length === 0 && <span className="muted">none: pure damage optimization</span>}
            </div>
            {targets.some((t) => t.from_core) && <div className="muted small">greyed = weapon core skill added by the options</div>}
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('limits')}>Skill limits</button></h4>
            <div className="chip-row">
              {limits.length === 0 && <span className="muted">none</span>}
              {limits.map(([s, n]) => <SkillHover key={s} name={s} className="chip skill armor"><SkillIcon name={s} size={18} />{s} <b>{n === 0 ? 'excluded' : `≤ ${n}`}</b></SkillHover>)}
            </div>
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('conditions')}>Conditions</button></h4>
            <div className="small">{conditionsOn.map((c) => c.label).join(', ') || 'nothing conditional'}; Resonance {request.conditions.resonance}</div>
            <div className="small muted">proc damage {request.conditions.proc_damage ? `on · ${attackProfileText(request, weaponTypes)}` : 'off'}</div>
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('talismans')}>Talismans</button></h4>
            <div>{talismans.length} random{request.talismans.include_craftable ? ` + ${catalog.craftable_talisman_count} craftable charm lines` : ''}</div>
            {r && <div className="muted small">{r.talismans.total} usable after validation</div>}
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('options')}>Options</button></h4>
            <div className="small">
              transcendence {request.options.allow_transcendence ? 'on' : 'off'} · core skills {request.options.require_weapon_core_skills ? 'on' : 'off'} · top {request.options.top_n} · rarity {request.options.min_rarity}+ ·
              {request.options.engine === 'cp_sat'
                ? `CP-SAT (exact, ${request.options.cp_sat_time_limit_seconds ?? 120} s per solve)`
                : `beam ${request.options.max_states_per_depth.toLocaleString('en-US')}`} · {request.options.max_threads ? `${request.options.max_threads} threads` : 'all threads'} · {request.options.exclude_sets.length} sets excluded
            </div>
          </div>

          {r?.baseline && (
            <div className="kv">
              <h4>Weapon-only baseline</h4>
              <div><b>EFR {fmt(r.baseline.efr)}</b> + <b>EFE {fmt(r.baseline.efe)}</b>{r.baseline.procs > 0 && <> + <b>procs {fmt(r.baseline.procs)}</b></>} = <b className="total">{fmt(r.baseline.total)}</b> <span className="muted small">(every condition on: {fmt(r.baseline.total_all_on)})</span></div>
              <div className="muted small">{fmt(r.baseline.attack, 0)} attack, {r.baseline.affinity}% affinity, crit x{fmt(r.baseline.crit_multiplier, 2)}</div>
            </div>
          )}
        </div>
      </Section>

      {r?.relevance && (
        <Section title="Skills in play" hint="Everything the search tracks: targets plus the skills, set bonuses and group skills that change the score under your conditions.">
          <div className="chip-row">
            {r.relevance.skills.map((s) => <SkillChip key={s} name={s} muted={!(s in request.target_skills)} />)}
            {r.relevance.set_bonuses.map((s) => <SkillHover key={s} name={s} className="chip skill set"><SkillIcon name={s} kind="set" size={18} />{s}</SkillHover>)}
            {r.relevance.group_skills.map((s) => <SkillHover key={s} name={s} className="chip skill group"><SkillIcon name={s} kind="group" size={18} />{s}</SkillHover>)}
          </div>
        </Section>
      )}
    </div>
  );
}
