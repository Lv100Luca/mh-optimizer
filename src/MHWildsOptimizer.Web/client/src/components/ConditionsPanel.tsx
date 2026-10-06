import { useApp, useCatalog } from '../state';
import type { AttackProfile, AttackProfilePreset, ConditionMeta, Conditions, OptimizationRequest, ResonanceMode, WeaponType } from '../types';
import { Alert, Button, Field, OptionalNumberInput, Section, Segmented, SkillIcon } from './common';

/** The condition group that holds the proc damage toggle; it is rendered with the attack profile, not as a hunt condition. */
export const PROC_GROUP = 'Proc damage';

const emptyProfile: AttackProfile = { hits_per_minute: null, average_mv: null, charged_lv3_share: null };

/** The attack profile preset of the request's weapon type. */
export function attackProfilePreset(request: OptimizationRequest, weaponTypes: Map<string, WeaponType>): AttackProfilePreset {
  const kind = request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword';
  return weaponTypes.get(kind)?.attack_profile ?? { hits_per_minute: 40, average_mv: 50, charged_lv3_share: 0 };
}

/** "24 hits/min · 120 MV per hit · 30% Lv3 charged (preset)": the profile the calculator will use. */
export function attackProfileText(request: OptimizationRequest, weaponTypes: Map<string, WeaponType>) {
  const p = request.conditions.attack_profile ?? emptyProfile;
  const preset = attackProfilePreset(request, weaponTypes);
  const custom = p.hits_per_minute != null || p.average_mv != null || p.charged_lv3_share != null;
  return `${p.hits_per_minute ?? preset.hits_per_minute} hits/min · ${p.average_mv ?? preset.average_mv} MV per hit · ` +
    `${Math.round((p.charged_lv3_share ?? preset.charged_lv3_share) * 100)}% Lv3 charged${custom ? '' : ' (preset)'}`;
}

export function ConditionsPanel() {
  const { request, patchRequest, resolved } = useApp();
  const { catalog, skillsByName, weaponTypes } = useCatalog();
  if (!request || !catalog) return null;
  const c = request.conditions;
  const profile = c.attack_profile ?? emptyProfile;
  const preset = attackProfilePreset(request, weaponTypes);
  const kind = request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword';
  const weaponLabel = weaponTypes.get(kind)?.label ?? kind;

  const set = (key: string, value: boolean) => patchRequest((r) => ({ ...r, conditions: { ...r.conditions, [key]: value } }));
  const setProfile = (patch: Partial<AttackProfile>) =>
    patchRequest((r) => ({ ...r, conditions: { ...r.conditions, attack_profile: { ...(r.conditions.attack_profile ?? emptyProfile), ...patch } } }));
  // presets only cover the hunt conditions: skill limits and the attack profile stay as they are
  const applyPreset = (preset: Conditions) =>
    patchRequest((r) => ({ ...r, conditions: { ...preset, skill_limits: r.conditions.skill_limits, attack_profile: r.conditions.attack_profile } }));

  const hunt = catalog.conditions.filter((x) => x.group !== PROC_GROUP);
  const procToggle = catalog.conditions.find((x) => x.group === PROC_GROUP);
  const groups = [...new Set(hunt.map((x) => x.group))];
  const onCount = hunt.filter((x) => c[x.key] === true).length;
  const profileErrors = resolved?.errors.filter((e) => e.includes('attack_profile')) ?? [];
  const customProfile = profile.hits_per_minute != null || profile.average_mv != null || profile.charged_lv3_share != null;

  const condition = (x: ConditionMeta) => {
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
  };

  return (
    <div className="panel">
      <Section
        title="Hunt conditions"
        hint="A toggle only matters when a build actually carries the skill behind it: the calculator never invents a skill. Off means the skill scores nothing, so the optimizer will not chase it."
        actions={
          <div className="btn-row">
            <span className="muted small">{onCount} of {hunt.length} on</span>
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
              {hunt.filter((x) => x.group === group).map(condition)}
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

      <Section
        title="Proc damage and attack profile"
        hint="Some set bonuses add extra damage instances that do not scale the hit. The attack profile turns them into damage per 100 MV of landed attacks so they can join EFR + EFE: procs per hit = chance × min(1, seconds per hit ÷ cooldown); per 100 MV = damage × procs per hit ÷ (average MV ÷ 100)."
        actions={customProfile && <Button small kind="ghost" onClick={() => setProfile(emptyProfile)}>Back to the {weaponLabel} preset</Button>}
      >
        {profileErrors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        {procToggle && <div className="cond-grid">{condition(procToggle)}</div>}
        <div className={'form-grid profile' + (c.proc_damage ? '' : ' off')}>
          <Field label="Landed hits per minute" hint="Cooldown-limited procs (Azure Bolt every 30 s, Bad Blood every 2 s) fire at most once per cooldown, so slow weapons get more out of them per hit.">
            <OptionalNumberInput value={profile.hits_per_minute} preset={preset.hits_per_minute} min={1} max={600} onChange={(hits_per_minute) => setProfile({ hits_per_minute })} />
          </Field>
          <Field label="Average motion value per hit" hint="Converts proc damage per hit into damage per 100 MV.">
            <OptionalNumberInput value={profile.average_mv} preset={preset.average_mv} min={1} max={1000} onChange={(average_mv) => setProfile({ average_mv })} />
          </Field>
          <Field label="Share of hits that are Lv3 charged slashes" hint="Great Sword only: the Dark Arts shockwave (Soul of the Dark Knight) rides on them. 0 to 1.">
            <OptionalNumberInput value={profile.charged_lv3_share} preset={preset.charged_lv3_share} min={0} max={1} step={0.05} onChange={(charged_lv3_share) => setProfile({ charged_lv3_share })} />
          </Field>
        </div>
        <p className="muted small">
          Empty fields use the {weaponLabel} preset: {preset.hits_per_minute} hits/min, {preset.average_mv} MV per hit, {Math.round(preset.charged_lv3_share * 100)}% Lv3 charged slashes.
          Presets are rough starting points, not measurements.
          {!c.proc_damage && ' The profile is ignored while proc damage is off.'}
        </p>
      </Section>
    </div>
  );
}
