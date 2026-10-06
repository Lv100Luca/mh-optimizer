import { useApp, useCatalog } from '../state';
import { Field, NumberInput, Section, SkillIcon } from './common';

export function SkillPairPanel() {
  const { request, patchRequest, resolved } = useApp();
  const { catalog } = useCatalog();
  if (!request || !catalog) return null;
  const sp = request.skill_pair;
  const w = request.weapon;
  const pairs = catalog.skill_pool.pairs.length;

  return (
    <div className="panel">
      <Section title="Skill pair mode" hint="A Gogma weapon rolls one set bonus and one group skill. Either keep the pair your weapon has, or let the optimizer tell you which pair to roll for.">
        <div className="choice-grid">
          <button type="button" className={'choice' + (sp.mode === 'fixed' ? ' active' : '')} onClick={() => patchRequest((r) => ({ ...r, skill_pair: { ...r.skill_pair, mode: 'fixed' } }))}>
            <h3>Fixed</h3>
            <p>Use the rolled pair from the Weapon tab.</p>
            <p className="pair">
              <span className="with-icon"><SkillIcon name="set" kind="set" /> {w.set_bonus ?? <span className="muted">no set bonus</span>}</span>
              <span className="with-icon"><SkillIcon name="group" kind="group" /> {w.group_skill ?? <span className="muted">no group skill</span>}</span>
            </p>
          </button>
          <button type="button" className={'choice' + (sp.mode === 'optimize' ? ' active' : '')} onClick={() => patchRequest((r) => ({ ...r, skill_pair: { ...r.skill_pair, mode: 'optimize' } }))}>
            <h3>Optimize</h3>
            <p>Search all {pairs} rollable pairs ({catalog.skill_pool.set_bonuses.length} set bonuses × {catalog.skill_pool.group_skills.length} group skills) and report the best ones.</p>
            <p className="muted">Pairs that score identically under your conditions are grouped into classes and searched in parallel; the report lists the best classes.</p>
          </button>
        </div>

        {sp.mode === 'optimize' && (
          <Field label="How many of the best pairs to report" inline>
            <NumberInput value={sp.top_n} min={1} max={50} width={90} onChange={(top_n) => patchRequest((r) => ({ ...r, skill_pair: { ...r.skill_pair, top_n } }))} />
          </Field>
        )}

        <p className="muted">
          {resolved ? `${resolved.skill_pair_candidates} candidate pair${resolved.skill_pair_candidates === 1 ? '' : 's'} will be searched.` : ''}
          {' '}Every pair has the same roll chance (1/{pairs}); a specific set bonus comes up in {Math.round((1 / catalog.skill_pool.set_bonuses.length) * 1000) / 10}% of rolls.
        </p>
      </Section>

      <Section title="What is in the pool" hint="Set bonuses and group skills a Gogma weapon can roll (from the game's ArtianSkillGroupData table).">
        <div className="two-col">
          <div>
            <h4 className="with-icon"><SkillIcon name="set" kind="set" /> Set bonuses ({catalog.skill_pool.set_bonuses.length})</h4>
            <ul className="plain">{catalog.skill_pool.set_bonuses.map((s) => <li key={s}>{s}</li>)}</ul>
          </div>
          <div>
            <h4 className="with-icon"><SkillIcon name="group" kind="group" /> Group skills ({catalog.skill_pool.group_skills.length})</h4>
            <ul className="plain">{catalog.skill_pool.group_skills.map((s) => <li key={s}>{s}</li>)}</ul>
          </div>
        </div>
      </Section>
    </div>
  );
}
