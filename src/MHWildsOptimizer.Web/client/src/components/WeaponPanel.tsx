import { useApp, useCatalog } from '../state';
import { icons, elementCss } from '../icons';
import type { Element, GogmaFocus, SharpnessColor, WeaponSpecInput } from '../types';
import { Alert, Field, NumberInput, Section, Segmented, Select, SkillIcon, Slots, Toggle, fmt, titleCase } from './common';
import { SharpnessBar } from './SharpnessBar';

const elements: Element[] = ['none', 'fire', 'water', 'thunder', 'ice', 'dragon'];

export function WeaponPanel() {
  const { request, patchWeapon, patchRequest, resolved } = useApp();
  const { catalog, weaponTypes } = useCatalog();
  if (!request || !catalog) return null;

  const w = request.weapon;
  const mode: 'spec' | 'stats' = w.spec ? 'spec' : 'stats';
  const kind = w.spec?.type ?? w.type ?? 'great-sword';
  const wt = weaponTypes.get(kind);
  const weaponErrors = resolved?.errors.filter((e) => e.startsWith('Weapon')) ?? [];

  const setMode = (m: 'spec' | 'stats') => {
    if (m === mode) return;
    if (m === 'spec') {
      patchWeapon({
        spec: { type: kind, focus: 'attack', element: w.element ?? 'none', infused: (w.element ?? 'none') !== 'none', attack_parts: 3, reinforcements: [] },
        type: null, attack: null,
      });
    } else {
      const variant = wt?.variants[w.spec?.focus ?? 'attack'];
      patchWeapon({
        spec: null, type: kind, attack: resolved?.weapon?.display_attack ?? variant?.display_attack ?? 1000, attack_is_display: true,
        affinity: resolved?.weapon?.affinity ?? variant?.affinity ?? 0,
        element: w.spec?.element ?? 'none', element_display: resolved?.weapon?.element_display ?? 0, sharpness: 'white',
      });
    }
  };

  const patchSpec = (patch: Partial<WeaponSpecInput>) => patchWeapon({ spec: { ...w.spec!, ...patch } });

  const setType = (k: string) => {
    if (mode === 'spec') patchSpec({ type: k });
    else patchWeapon({ type: k });
  };

  const spec = w.spec;
  const reinforcementSlots = Array.from({ length: 5 }, (_, i) => spec?.reinforcements[i] ?? '');
  const reinforcementOptions = catalog.reinforcements
    .filter((r) => !r.gunner_only && (!r.needs_element || (spec?.element ?? 'none') !== 'none'))
    .map((r) => ({ value: r.value, label: r.label, group: titleCase(r.type) }));
  const setReinforcement = (i: number, value: string | null) => {
    const next = [...reinforcementSlots];
    next[i] = value ?? '';
    patchSpec({ reinforcements: next.filter(Boolean) });
  };
  const reinforcementCounts = reinforcementSlots.filter(Boolean).reduce<Record<string, number>>((acc, r) => ({ ...acc, [r]: (acc[r] ?? 0) + 1 }), {});

  const rw = resolved?.weapon ?? null;

  return (
    <div className="panel two-col">
      <div className="col">
        <Section title="Weapon type" hint="Gogma Artian weapons only. Great Sword and Long Sword are fully modeled (core skills Focus 3 / Quick Sheathe 3); other melee types run without core skills; gunner weapons are out of scope.">
          <div className="type-grid">
            {catalog.weapon_types.map((t) => (
              <button key={t.kind} type="button" disabled={!t.supported} className={'type-card' + (t.kind === kind ? ' active' : '')}
                onClick={() => setType(t.kind)} title={t.supported ? `${t.label}: base ${t.variants.attack?.raw ?? '?'} raw (attack focus), bloat x${t.bloat}` : `${t.label}: out of scope`}>
                <img src={t.supported ? icons.weapon(t.kind) : icons.weaponBase(t.kind)} alt="" />
                <span>{t.label}</span>
                {t.core_skills.length > 0 && <small>{t.core_skills.map((c) => `${c.skill} ${c.level}`).join(', ')}</small>}
              </button>
            ))}
          </div>
        </Section>

        <Section
          title="Stats"
          hint={mode === 'spec'
            ? 'Describe the weapon the way the game shows it on the Production Bonus and Reinforcement Bonus screens; the true stats are computed.'
            : 'Enter the final numbers from the equipment screen.'}
          actions={<Segmented small value={mode} onChange={setMode} options={[{ value: 'spec', label: 'Describe' }, { value: 'stats', label: 'Enter stats' }]} />}
        >
          {mode === 'spec' && spec && (
            <div className="form-grid">
              <Field label="Focus" hint="The Artian focus decides the base raw / affinity variant.">
                <Segmented<GogmaFocus>
                  value={spec.focus}
                  onChange={(focus) => patchSpec({ focus })}
                  options={(['attack', 'affinity', 'element'] as GogmaFocus[]).map((f) => {
                    const v = wt?.variants[f];
                    return { value: f, label: <span>{titleCase(f)}{v && <small> {v.raw} raw · {v.affinity > 0 ? '+' : ''}{v.affinity}%</small>}</span> };
                  })}
                />
              </Field>

              <Field label="Element">
                <div className="chip-row">
                  {elements.map((e) => (
                    <button key={e} type="button" className={'chip pick' + (spec.element === e ? ' active' : '')} style={{ ['--tint' as string]: elementCss[e] }}
                      onClick={() => patchSpec({ element: e, infused: e === 'none' ? false : spec.infused, reinforcements: e === 'none' ? spec.reinforcements.filter((r) => !r.startsWith('element')) : spec.reinforcements })}>
                      {icons.element(e) && <img src={icons.element(e)!} alt="" />}
                      {titleCase(e)}
                    </button>
                  ))}
                </div>
              </Field>

              <Field label="Element infusion" hint={wt ? `All three parts share the element: +${wt.infusion_bonus_display} display element on top of the base ${wt.element_base_display}.` : undefined}>
                <Toggle checked={spec.infused && spec.element !== 'none'} disabled={spec.element === 'none'} onChange={(infused) => patchSpec({ infused })} label="Element Infusion line present" />
              </Field>

              <Field label="Attack Infusion lines" hint="Each Artian part gives +5 true attack or +5% affinity; count the 'Attack Infusion +5' lines.">
                <Segmented<number> value={spec.attack_parts} onChange={(attack_parts) => patchSpec({ attack_parts })}
                  options={[0, 1, 2, 3].map((n) => ({ value: n, label: <span>{n}<small> +{n * 5} atk · +{(3 - n) * 5}% aff</small></span> }))} />
              </Field>

              <Field label="Reinforcement bonuses" hint="Up to five; at most two identical lines, sharpness at most twice. Element lines need an element.">
                <div className="reinforcements">
                  {reinforcementSlots.map((r, i) => (
                    <Select key={i} value={r || null} placeholder="— empty —" options={reinforcementOptions} onChange={(v) => setReinforcement(i, v)} />
                  ))}
                </div>
                {Object.entries(reinforcementCounts).some(([, n]) => n > 2) && <Alert kind="error">More than two identical reinforcements.</Alert>}
              </Field>
            </div>
          )}

          {mode === 'stats' && (
            <div className="form-grid">
              <Field label="Attack (display value)">
                <NumberInput value={w.attack ?? 0} min={1} max={9999} onChange={(attack) => patchWeapon({ attack, attack_is_display: true })} />
              </Field>
              <Field label="Affinity %">
                <NumberInput value={w.affinity} min={-100} max={100} onChange={(affinity) => patchWeapon({ affinity })} />
              </Field>
              <Field label="Element">
                <div className="chip-row">
                  {elements.map((e) => (
                    <button key={e} type="button" className={'chip pick' + (w.element === e ? ' active' : '')} style={{ ['--tint' as string]: elementCss[e] }}
                      onClick={() => patchWeapon({ element: e, element_display: e === 'none' ? 0 : w.element_display || 300 })}>
                      {icons.element(e) && <img src={icons.element(e)!} alt="" />}
                      {titleCase(e)}
                    </button>
                  ))}
                </div>
              </Field>
              {w.element !== 'none' && (
                <Field label="Element value (display)">
                  <NumberInput value={w.element_display} min={10} max={9999} step={10} onChange={(element_display) => patchWeapon({ element_display })} />
                </Field>
              )}
              <Field label="Sharpness the weapon attacks at">
                <Segmented<SharpnessColor> value={w.sharpness} onChange={(sharpness) => patchWeapon({ sharpness })}
                  options={catalog.sharpness_colors.map((c) => ({ value: c, label: <span className={'sharp-dot ' + c}>{titleCase(c)}</span> }))} />
              </Field>
              <Field label="Decoration slots" hint="Gogma weapons have three level-3 slots.">
                <div className="chip-row">
                  {[0, 1, 2].map((i) => (
                    <Segmented<number> key={i} small value={w.slots[i] ?? 0} onChange={(lv) => { const slots = [...w.slots]; slots[i] = lv; patchWeapon({ slots: slots.filter((x) => x > 0) }); }}
                      options={[0, 1, 2, 3].map((n) => ({ value: n, label: n === 0 ? '—' : String(n) }))} />
                  ))}
                </div>
              </Field>
            </div>
          )}
        </Section>

        <Section title="Rolled skill pair" hint="The two lines under 'Active Skills' on the weapon: one set bonus and one group skill. The weapon counts as one armor piece for each. Ignored when the skill pair mode is 'optimize'.">
          <div className="form-grid">
            <Field label={<span className="with-icon"><SkillIcon name="set" kind="set" /> Set bonus</span>}>
              <Select value={w.set_bonus} placeholder="(none)" options={catalog.skill_pool.set_bonuses.map((s) => ({ value: s, label: s }))} onChange={(set_bonus) => patchWeapon({ set_bonus })} />
            </Field>
            <Field label={<span className="with-icon"><SkillIcon name="group" kind="group" /> Group skill</span>}>
              <Select value={w.group_skill} placeholder="(none)" options={catalog.skill_pool.group_skills.map((s) => ({ value: s, label: s }))} onChange={(group_skill) => patchWeapon({ group_skill })} />
            </Field>
          </div>
          {request.skill_pair.mode === 'optimize' && (
            <Alert kind="info">Skill pair mode is <b>optimize</b>: every rollable pair is searched, so the rolled pair above is not used. <button className="linklike" onClick={() => patchRequest((r) => ({ ...r, skill_pair: { ...r.skill_pair, mode: 'fixed' } }))}>Switch to fixed</button></Alert>
          )}
        </Section>
      </div>

      <aside className="col side">
        <Section title="Resolved weapon" hint="What the optimizer will use (before any skills).">
          {weaponErrors.length > 0 && weaponErrors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
          {rw && (
            <div className="weapon-preview">
              <div className="weapon-head">
                <img src={icons.weapon(rw.type)} alt="" />
                <div>
                  <div className="big">{rw.label}</div>
                  <div className="muted">{rw.focus ? `${titleCase(rw.focus)} focus · ` : ''}rarity 8 Gogma Artian</div>
                </div>
              </div>
              <dl className="stats">
                <dt><img src={icons.attack} alt="" /> Attack</dt><dd><b>{rw.display_attack}</b> display <span className="muted">({rw.true_raw} true)</span></dd>
                <dt><img src={icons.affinity} alt="" /> Affinity</dt><dd><b>{rw.affinity > 0 ? '+' : ''}{rw.affinity}%</b></dd>
                <dt>{icons.element(rw.element) ? <img src={icons.element(rw.element)!} alt="" /> : null} Element</dt>
                <dd>{rw.element === 'none' ? <span className="muted">none</span> : <><b style={{ color: elementCss[rw.element] }}>{rw.element_display} {titleCase(rw.element)}</b> <span className="muted">({fmt(rw.element_true, 0)} true)</span></>}</dd>
                <dt><img src={icons.sharpness} alt="" /> Sharpness</dt>
                <dd>
                  <b>{rw.sharpness ? titleCase(rw.sharpness) : '-'}</b>
                  {rw.sharpness_bar && <SharpnessBar bar={rw.sharpness_bar} bonus={rw.sharpness_bonus} />}
                </dd>
                <dt>Slots</dt><dd><Slots slots={rw.slots} kind="weapon" /></dd>
                <dt>Rolled pair</dt><dd>{rw.set_bonus ?? <span className="muted">no set bonus</span>} / {rw.group_skill ?? <span className="muted">no group skill</span>}</dd>
              </dl>
              {resolved?.baseline && (
                <div className="baseline">
                  <span className="muted">Weapon-only baseline</span>
                  <b>EFR {fmt(resolved.baseline.efr)}</b> + <b>EFE {fmt(resolved.baseline.efe)}</b> = <b className="total">{fmt(resolved.baseline.total)}</b>
                  <span className="muted"> under your conditions</span>
                </div>
              )}
            </div>
          )}
          {!rw && weaponErrors.length === 0 && <p className="muted">Validating…</p>}
        </Section>
      </aside>
    </div>
  );
}
