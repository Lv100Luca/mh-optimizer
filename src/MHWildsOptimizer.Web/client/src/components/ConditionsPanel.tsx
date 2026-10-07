import { overrides } from '../presets';
import { useApp, useCatalog } from '../state';
import type { AttackProfile, AttackProfilePreset, ConditionMeta, Conditions, OptimizationRequest, ResonanceMode, WeaponType } from '../types';
import { Alert, Button, Field, OptionalNumberInput, Section, Segmented, SkillHover, SkillIcon } from './common';

/** The condition group that holds the proc damage toggle; it is rendered with the attack profile, not as a hunt condition. */
export const PROC_GROUP = 'Proc damage';

const emptyProfile: AttackProfile = { hits_per_minute: null, average_mv: null, charged_lv3_share: null, element_hitzone_ratio: null };
const isCustom = (p: AttackProfile) => Object.values(p).some((v) => v != null);

/** The attack profile preset of the request's weapon type. */
export function attackProfilePreset(request: OptimizationRequest, weaponTypes: Map<string, WeaponType>): AttackProfilePreset {
  const kind = request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword';
  return weaponTypes.get(kind)?.attack_profile ?? { hits_per_minute: 40, average_mv: 50, charged_lv3_share: 0, element_hitzone_ratio: 0.4 };
}

/** "24 hits/min · 190 MV per hit · 100% Lv3 charged · element hitzone 40% of raw (preset)": the profile the calculator will use. */
export function attackProfileText(request: OptimizationRequest, weaponTypes: Map<string, WeaponType>) {
  const p = request.conditions.attack_profile ?? emptyProfile;
  const preset = attackProfilePreset(request, weaponTypes);
  const custom = isCustom(p);
  return `${p.hits_per_minute ?? preset.hits_per_minute} hits/min · ${p.average_mv ?? preset.average_mv} MV per hit · ` +
    `${Math.round((p.charged_lv3_share ?? preset.charged_lv3_share) * 100)}% Lv3 charged · ` +
    `element hitzone ${Math.round((p.element_hitzone_ratio ?? preset.element_hitzone_ratio) * 100)}% of raw${custom ? '' : ' (preset)'}`;
}

export function ConditionsPanel() {
  const { request, patchRequest, resolved, profile: profileName, name, presets, presetFor, savePreset } = useApp();
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
  const customProfile = isCustom(profile);

  // the weapon type's preset: the conditions every weapon of this type starts from and follows, except where it overrides them
  const typePreset = presetFor(kind);
  const hasPreset = kind in presets;
  const overridden = overrides(c, typePreset);
  const keyLabel = (key: string) =>
    catalog.conditions.find((x) => x.key === key)?.label ?? ({ resonance: 'Omega Resonance phase', attack_profile: 'Attack profile', skill_limits: 'Skill limits' } as Record<string, string>)[key] ?? key;
  const resetToPreset = () => patchRequest((r) => ({ ...r, conditions: structuredClone(typePreset) }));
  const confirmSavePreset = () => {
    if (window.confirm(`Save these conditions, skill limits and attack profile as the ${weaponLabel} preset of ${profileName}? New ${weaponLabel} weapons start from it, and saved ones follow it except where they override it.`))
      void savePreset();
  };

  const condition = (x: ConditionMeta) => {
    const on = c[x.key] === true;
    const own = overridden.includes(x.key);
    return (
      <label key={x.key} className={'cond' + (on ? ' on' : '') + (own ? ' overridden' : '')}>
        <input type="checkbox" checked={on} onChange={(e) => set(x.key, e.target.checked)} />
        <span className="cond-text">
          <b>{x.label}{own && <span className="ovr" title={`The ${weaponLabel} preset has this ${typePreset[x.key] === true ? 'on' : 'off'}; this weapon overrides it.`}>override</span>}</b>
          <span className="muted small">{x.description}</span>
          <span className="cond-skills">
            {x.skills.map((s) => {
              const base = s.replace(/\s*\(.*\)$/, '');
              const skill = skillsByName.get(base);
              return <SkillHover key={s} name={base} className={`chip skill ${skill?.kind ?? 'armor'} tiny`}><SkillIcon name={base} kind={skill?.kind} size={14} />{s}</SkillHover>;
            })}
          </span>
        </span>
      </label>
    );
  };

  return (
    <div className="panel">
      <Section
        title={`${weaponLabel} preset`}
        hint={`Conditions are set per weapon type: every ${weaponLabel} of the profile starts from this preset (hunt conditions, skill limits and attack profile). A weapon can override single values; the rest follows the preset when it changes.`}
        actions={
          <div className="btn-row">
            <Button small kind="ghost" onClick={resetToPreset} disabled={overridden.length === 0} title={`Drop this weapon's overrides and use the ${weaponLabel} preset`}>Reset to preset</Button>
            <Button small onClick={confirmSavePreset} disabled={hasPreset && overridden.length === 0} title={`Make this weapon's conditions the ${weaponLabel} preset`}>Save as {weaponLabel} preset</Button>
          </div>
        }
      >
        {!hasPreset && <p className="muted small">The profile has no {weaponLabel} preset yet, so the defaults stand in. Set the conditions you usually play with and save them as the preset.</p>}
        {overridden.length === 0 ? (
          <p className="small"><span className="ok">✓</span> {name ?? 'This weapon'} follows the {weaponLabel} preset.</p>
        ) : (
          <div className="chip-row">
            <span className="muted small">{name ?? 'This weapon'} overrides:</span>
            {overridden.map((k) => <span key={k} className="chip ovr-chip">{keyLabel(k)}</span>)}
          </div>
        )}
      </Section>

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
        hint="Element and proc damage land once per hit, whatever the motion value. The attack profile turns them into damage per 100 MV of landed attacks so they compare with EFR: EFE = element × element hitzone ratio ÷ (average MV ÷ 100); procs per hit = chance × min(1, seconds per hit ÷ cooldown); procs per 100 MV = damage × procs per hit ÷ (average MV ÷ 100)."
        actions={customProfile && <Button small kind="ghost" onClick={() => setProfile(emptyProfile)}>Back to the {weaponLabel} preset</Button>}
      >
        {profileErrors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        {procToggle && <div className="cond-grid">{condition(procToggle)}</div>}
        <div className="form-grid profile">
          <Field label="Landed hits per minute" hint="Cooldown-limited procs (Azure Bolt every 30 s, Bad Blood every 2 s) fire at most once per cooldown, so slow weapons get more out of them per hit.">
            <OptionalNumberInput value={profile.hits_per_minute} preset={preset.hits_per_minute} min={1} max={600} onChange={(hits_per_minute) => setProfile({ hits_per_minute })} />
          </Field>
          <Field label="Average motion value per hit" hint="Converts element and proc damage per hit into damage per 100 MV. Big hits (Great Sword charged slashes) get less out of element per 100 MV than fast weapons.">
            <OptionalNumberInput value={profile.average_mv} preset={preset.average_mv} min={1} max={1000} onChange={(average_mv) => setProfile({ average_mv })} />
          </Field>
          <Field label="Share of hits that are Lv3 charged slashes" hint="Great Sword only: the Dark Arts shockwave (Soul of the Dark Knight) rides on them. 0 to 1.">
            <OptionalNumberInput value={profile.charged_lv3_share} preset={preset.charged_lv3_share} min={0} max={1} step={0.05} onChange={(charged_lv3_share) => setProfile({ charged_lv3_share })} />
          </Field>
          <Field label="Element hitzone ÷ raw hitzone" hint="Where you hit: a weak point with raw hitzone 70 and element hitzone 25 is 0.36. EFR is at raw hitzone 100, so element is scored at this share of it. 0 to 1.">
            <OptionalNumberInput value={profile.element_hitzone_ratio} preset={preset.element_hitzone_ratio} min={0} max={1} step={0.05} onChange={(element_hitzone_ratio) => setProfile({ element_hitzone_ratio })} />
          </Field>
        </div>
        <p className="muted small">
          Empty fields use the {weaponLabel} preset: {preset.hits_per_minute} hits/min, {preset.average_mv} MV per hit, {Math.round(preset.charged_lv3_share * 100)}% Lv3 charged slashes, element hitzone {Math.round(preset.element_hitzone_ratio * 100)}% of raw.
          Presets are rough starting points, not measurements.
          {!c.proc_damage && ' With proc damage off only the average MV and the element hitzone ratio are used.'}
        </p>
      </Section>
    </div>
  );
}
