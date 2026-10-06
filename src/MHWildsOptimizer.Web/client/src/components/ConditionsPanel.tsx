import { useApp, useCatalog } from '../state';
import type { Conditions, ResonanceMode } from '../types';
import { Button, Section, Segmented, SkillIcon } from './common';

export function ConditionsPanel() {
  const { request, patchRequest } = useApp();
  const { catalog, skillsByName } = useCatalog();
  if (!request || !catalog) return null;
  const c = request.conditions;

  const set = (key: string, value: boolean) => patchRequest((r) => ({ ...r, conditions: { ...r.conditions, [key]: value } }));
  const applyPreset = (preset: Conditions) => patchRequest((r) => ({ ...r, conditions: { ...preset, skill_limits: r.conditions.skill_limits } }));
  const groups = [...new Set(catalog.conditions.map((x) => x.group))];
  const onCount = catalog.conditions.filter((x) => c[x.key] === true).length;

  return (
    <div className="panel">
      <Section
        title="Hunt conditions"
        hint="A toggle only matters when a build actually carries the skill behind it: the calculator never invents a skill. Off means the skill scores nothing, so the optimizer will not chase it."
        actions={
          <div className="btn-row">
            <span className="muted small">{onCount} of {catalog.conditions.length} on</span>
            <Button small onClick={() => applyPreset(catalog.conditions_default)}>Defaults</Button>
            <Button small onClick={() => applyPreset(catalog.conditions_all_on)}>All on</Button>
            <Button small onClick={() => applyPreset(catalog.conditions_all_off)}>All off</Button>
          </div>
        }
      >
        {groups.map((group) => (
          <div key={group} className="cond-group">
            <h3>{group}</h3>
            <div className="cond-grid">
              {catalog.conditions.filter((x) => x.group === group).map((x) => {
                const on = c[x.key] === true;
                return (
                  <label key={x.key} className={'cond' + (on ? ' on' : '')}>
                    <input type="checkbox" checked={on} onChange={(e) => set(x.key, e.target.checked)} />
                    <span className="cond-text">
                      <b>{x.label}</b>
                      <span className="muted small">{x.description}</span>
                      <span className="cond-skills">
                        {x.skills.map((s) => {
                          const base = s.replace(/\s*\(.*\)$/, '');
                          const skill = skillsByName.get(base);
                          return <span key={s} className={`chip skill ${skill?.kind ?? 'armor'} tiny`}><SkillIcon name={base} kind={skill?.kind} size={14} />{s}</span>;
                        })}
                      </span>
                    </span>
                  </label>
                );
              })}
              {group === 'Set bonus and group skill triggers' && (
                <div className="cond resonance">
                  <span className="cond-text">
                    <b>Omega Resonance phase</b>
                    <span className="muted small">Omega Resonance alternates a Local (affinity) and a Remote (attack) phase every 90 s.</span>
                    <Segmented<ResonanceMode> small value={c.resonance} onChange={(resonance) => patchRequest((r) => ({ ...r, conditions: { ...r.conditions, resonance } }))}
                      options={catalog.resonance_modes.map((m) => ({ value: m, label: m === 'none' ? 'Off' : m === 'local' ? 'Local (affinity)' : 'Remote (attack)' }))} />
                  </span>
                </div>
              )}
            </div>
          </div>
        ))}
      </Section>
    </div>
  );
}
