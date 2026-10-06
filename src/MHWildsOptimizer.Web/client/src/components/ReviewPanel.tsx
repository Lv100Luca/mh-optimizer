import { icons, elementCss } from '../icons';
import { useApp, useCatalog } from '../state';
import { Alert, Button, Section, SkillChip, SkillIcon, Slots, fmt, titleCase } from './common';
import { SharpnessBar } from './SharpnessBar';

export function ReviewPanel() {
  const { request, talismans, resolved, resolving, runOptimizer, run, setTab, name, dirty } = useApp();
  const { catalog } = useCatalog();
  if (!request || !catalog) return null;
  const r = resolved;
  const w = r?.weapon;
  const conditionsOn = catalog.conditions.filter((c) => request.conditions[c.key] === true);
  const limits = Object.entries(request.conditions.skill_limits ?? {});

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
              {(r?.targets ?? Object.entries(request.target_skills).map(([skill, level]) => ({ skill, level, from_core: false }))).map((t) => (
                <SkillChip key={t.skill} name={t.skill} level={t.level} muted={t.from_core} />
              ))}
              {r?.targets.length === 0 && <span className="muted">none: pure damage optimization</span>}
            </div>
            {r?.targets.some((t) => t.from_core) && <div className="muted small">greyed = weapon core skill added by the options</div>}
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('limits')}>Skill limits</button></h4>
            <div className="chip-row">
              {limits.length === 0 && <span className="muted">none</span>}
              {limits.map(([s, n]) => <span key={s} className="chip skill armor"><SkillIcon name={s} size={18} />{s} <b>{n === 0 ? 'excluded' : `≤ ${n}`}</b></span>)}
            </div>
          </div>

          <div className="kv">
            <h4><button className="linklike" onClick={() => setTab('conditions')}>Conditions</button></h4>
            <div className="small">{conditionsOn.map((c) => c.label).join(', ') || 'nothing conditional'}; Resonance {request.conditions.resonance}</div>
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
              beam {request.options.max_states_per_depth.toLocaleString('en-US')} · {request.options.exclude_sets.length} sets excluded
            </div>
          </div>

          {r?.baseline && (
            <div className="kv">
              <h4>Weapon-only baseline</h4>
              <div><b>EFR {fmt(r.baseline.efr)}</b> + <b>EFE {fmt(r.baseline.efe)}</b> = <b className="total">{fmt(r.baseline.total)}</b> <span className="muted small">(every condition on: {fmt(r.baseline.total_all_on)})</span></div>
              <div className="muted small">{fmt(r.baseline.attack, 0)} attack, {r.baseline.affinity}% affinity, crit x{fmt(r.baseline.crit_multiplier, 2)}</div>
            </div>
          )}
        </div>
      </Section>

      {r?.relevance && (
        <Section title="Skills in play" hint="Everything the search tracks: targets plus the skills, set bonuses and group skills that change the score under your conditions.">
          <div className="chip-row">
            {r.relevance.skills.map((s) => <SkillChip key={s} name={s} muted={!(s in request.target_skills)} />)}
            {r.relevance.set_bonuses.map((s) => <span key={s} className="chip skill set"><SkillIcon name={s} kind="set" size={18} />{s}</span>)}
            {r.relevance.group_skills.map((s) => <span key={s} className="chip skill group"><SkillIcon name={s} kind="group" size={18} />{s}</span>)}
          </div>
        </Section>
      )}
    </div>
  );
}
