import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import { armorKinds, emptyBuild, fitDecorations, fits, fromBuild, parseSlot, pieceLabel, uniqueName, type SlotSpec } from '../builds';
import { elementCss, icons } from '../icons';
import { useApp, useCatalog, type ArmorEntry } from '../state';
import type { ArmorPieceKind, Build, BuildArmorInput, BuildInput, EvaluatedBuild, SkillGrant, TargetCheck } from '../types';
import { Alert, Button, DecoIcon, RarityBadge, Section, Select, SkillChip, SkillHover, SkillIcon, Slots, fmt, titleCase } from './common';
import { BuildCard } from './BuildCard';
import { BuildCompare, ScoreDelta } from './BuildCompare';
import { Picker, type PickerOption } from './Picker';

/** Something to compare the selected build with: an optimizer build (re-scored under the current request) or another of your builds. */
interface Reference { key: string; label: string; group: string; input: BuildInput | null; build: Build | null; runScore?: number }

const canTranscend = (p: ArmorEntry) => p.slots_transcended.length > 0 && p.slots_transcended.join() !== p.slots.join();

const grantsText = (grants: SkillGrant[]) => grants.map((g) => `${g.skill} ${g.level}`).join(', ');

/** Scores one build under the current request (debounced); used for optimizer builds so both sides of a comparison share the conditions. */
function useScored(input: BuildInput | null): EvaluatedBuild | null {
  const { request, talismans } = useApp();
  const [state, setState] = useState<EvaluatedBuild | null>(null);
  const key = input ? JSON.stringify(input) : null;
  useEffect(() => {
    setState(null);
    if (!request || !input) return;
    const controller = new AbortController();
    const handle = window.setTimeout(async () => {
      try {
        const [r] = await api.evaluate({ request, talismans, builds: [input] }, controller.signal);
        if (!controller.signal.aborted) setState(r ?? null);
      } catch (e) {
        if (!controller.signal.aborted) setState({ name: input.name, errors: [`Scoring failed: ${(e as Error).message}`], warnings: [], build: null, targets: [] });
      }
    }, 250);
    return () => { controller.abort(); window.clearTimeout(handle); };
    // input is captured through its JSON key
  }, [request, talismans, key]); // eslint-disable-line react-hooks/exhaustive-deps
  return state;
}

export function BuildsPanel() {
  const { request, resolved, talismans, builds, evaluated, buildIndex, setBuildIndex, setBuilds, pickBuild, compareKey, setCompareKey, run, setTab } = useApp();
  const { catalog } = useCatalog();

  const index = Math.max(0, Math.min(buildIndex, builds.length - 1));
  const current = builds[index] ?? null;
  const ev = current ? evaluated?.[index] ?? null : null;

  // comparison candidates
  const references = useMemo<Reference[]>(() => {
    const refs: Reference[] = [];
    const pairs = run.result?.pairs ?? [];
    for (const p of pairs)
      for (const b of p.builds) {
        const label = pairs.length > 1 ? `Pair #${p.rank} build ${b.rank}` : `Optimizer build ${b.rank}`;
        refs.push({ key: `opt:${p.rank}:${b.rank}`, label, group: 'Optimizer results', input: fromBuild(b, label), build: null, runScore: b.score });
      }
    builds.forEach((b, i) => {
      if (i !== index) refs.push({ key: `mine:${i}`, label: b.name, group: 'My builds', input: null, build: evaluated?.[i]?.build ?? null });
    });
    return refs;
  }, [run.result, builds, evaluated, index]);
  const reference = references.find((r) => r.key === compareKey) ?? references[0] ?? null;
  const scoredRef = useScored(reference?.input ?? null);
  const refBuild = reference ? (reference.input ? scoredRef?.build ?? null : reference.build) : null;

  if (!request || !catalog) return null;

  const names = builds.map((b) => b.name);
  const update = (patch: Partial<BuildInput>) => setBuilds((list) => list.map((b, i) => (i === index ? { ...b, ...patch } : b)));
  const addNew = (b: BuildInput) => { setBuilds((list) => [...list, b]); setBuildIndex(builds.length); };
  const remove = () => {
    if (!current || !window.confirm(`Delete the build "${current.name}"?`)) return;
    setBuilds((list) => list.filter((_, i) => i !== index));
    setBuildIndex(Math.max(0, index - 1));
    if (compareKey?.startsWith('mine:')) setCompareKey(null);
  };

  const bestOptimizer = run.result?.pairs[0]?.builds[0] ?? null;

  if (!current) {
    return (
      <div className="panel">
        <Section title="My builds" hint="Enter a build by hand, for example the one you wear, to score it under your conditions and compare it with the optimizer's builds.">
          <div className="empty">
            <p className="muted">The weapon is the one on the Weapon tab; you pick the armor, decorations and talisman.</p>
            <div className="btn-row">
              <Button kind="primary" onClick={() => addNew(emptyBuild('My build'))}>+ New build</Button>
              {bestOptimizer && <Button onClick={() => { addNew(fromBuild(bestOptimizer, 'Optimizer build 1 (copy)')); setCompareKey(null); }}>Start from optimizer build 1</Button>}
            </div>
          </div>
        </Section>
      </div>
    );
  }

  const b = ev?.build ?? null;
  const met = ev?.targets.filter((t) => t.met).length ?? 0;

  return (
    <div className="panel">
      <Section
        title="My builds"
        hint="Builds entered by hand are scored exactly like the optimizer's: same weapon (Weapon tab), conditions, skill limits and attack profile. They are saved with the weapon; the picked one (★) represents the weapon in the inventory."
        actions={
          <div className="btn-row">
            <Button small kind={current.picked ? 'primary' : 'ghost'} onClick={() => pickBuild(current.picked ? null : index)}
              title={current.picked ? "This is the weapon's pick: the inventory compares weapons by it. Click to unpick." : "Make this the weapon's pick: the inventory compares weapons by it"}>
              {current.picked ? '★ Picked' : '☆ Pick'}
            </Button>
            <Button small kind="primary" onClick={() => addNew(emptyBuild(uniqueName('My build', names)))}>+ New</Button>
            <Button small onClick={() => addNew({ ...structuredClone(current), name: uniqueName(`${current.name} copy`, names) })}>Duplicate</Button>
            <Button small kind="ghost" onClick={remove}>Delete</Button>
          </div>
        }
      >
        <div className="buildtabs" role="tablist">
          {builds.map((x, i) => {
            const e = evaluated?.[i];
            return (
              <button key={i} type="button" role="tab" aria-selected={i === index} className={'buildtab' + (i === index ? ' active' : '')} onClick={() => setBuildIndex(i)}>
                <span>{x.picked && <span className="pickstar" title="the weapon's pick">★ </span>}{x.name || '(unnamed)'}</span>
                {e?.build ? <b>{fmt(e.build.score)}</b> : <span className="muted">…</span>}
                {e && e.errors.length > 0 && <span className="err" title={e.errors.join('\n')}>!</span>}
              </button>
            );
          })}
        </div>
        <label className="field inline">
          <span className="field-label">Name</span>
          <input className="text buildname" value={current.name} onChange={(e) => update({ name: e.target.value })} />
        </label>
      </Section>

      <div className="scorebar">
        {b ? (
          <>
            <span className="scorebar-total"><span className="muted small">Score</span> <b>{fmt(b.score)}</b></span>
            <span className="muted small">EFR {fmt(b.efr)} + EFE {fmt(b.efe)}{b.procs > 0 ? ` + procs ${fmt(b.procs)}` : ''}</span>
            <span className="statchip atk"><img src={icons.attack} alt="" />ATK <b>{fmt(b.summary.attack, 0)}</b></span>
            <span className="statchip aff"><img src={icons.affinity} alt="" />AFF <b>{b.summary.affinity}%</b></span>
            {b.summary.element !== 'none' && (
              <span className="statchip ele" style={{ ['--tint' as string]: elementCss[b.summary.element] }}>{titleCase(b.summary.element)} <b>{fmt(b.summary.element_true, 0)}</b></span>
            )}
            {ev && ev.targets.length > 0 && <span className={'tcount ' + (met === ev.targets.length ? 'ok' : 'warn')}>targets {met}/{ev.targets.length}</span>}
            {reference && refBuild && <ScoreDelta score={b.score} reference={refBuild.score} label={reference.label} />}
          </>
        ) : (
          <span className="muted">{ev ? 'Cannot score this build yet.' : 'Scoring…'}</span>
        )}
        {ev && ev.errors.length > 0 && <span className="err small right">{ev.errors.length} error{ev.errors.length > 1 ? 's' : ''}</span>}
      </div>

      <Section title="Equipment" hint="Pick each piece and the decorations in its slots. Pieces with better transcended slots (rarity 5/6) can be marked as transcended.">
        <EquipmentEditor build={current} update={update} />
        {ev?.errors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        {ev?.warnings.map((w) => <Alert key={w} kind="warning">{w}</Alert>)}
        {resolved && !resolved.weapon && <Alert kind="error">The weapon has errors. <button className="linklike" onClick={() => setTab('weapon')}>Open the Weapon tab</button></Alert>}
        {talismans.length === 0 && current.talisman === null && <p className="muted small">Your own random talismans come from the Talismans tab; craftable charms are always listed.</p>}
      </Section>

      {ev && ev.targets.length > 0 && (
        <Section title="Targets" hint="The requirements of this configuration, checked against this build (the optimizer only returns builds that meet all of them).">
          <TargetChecks targets={ev.targets} />
        </Section>
      )}

      <Section
        title="Compare"
        hint={reference?.input ? 'The optimizer build is scored again under the current conditions, so both sides use the same assumptions.' : 'Differences are this build minus the other one; green means this build is ahead.'}
        actions={references.length > 0 && (
          <Select value={reference?.key ?? null} onChange={(k) => setCompareKey(k)}
            options={references.map((r) => ({ value: r.key, label: r.label + (r.runScore !== undefined ? ` (${fmt(r.runScore)} in the run)` : r.build ? ` (${fmt(r.build.score)})` : ''), group: r.group }))} />
        )}
      >
        {!reference && <p className="muted">Run the optimizer or add a second build to have something to compare with.</p>}
        {reference && scoredRef && scoredRef.errors.length > 0 && scoredRef.errors.map((e) => <Alert key={e} kind="error">{reference.label}: {e}</Alert>)}
        {reference && reference.runScore !== undefined && refBuild && Math.abs(refBuild.score - reference.runScore) > 0.05 && (
          <Alert kind="info">The run scored {reference.label} at {fmt(reference.runScore)}; under the current conditions it scores {fmt(refBuild.score)}. Run the optimizer again after changing conditions.</Alert>
        )}
        {reference && b && refBuild && <BuildCompare a={b} aLabel={current.name || 'This build'} b={refBuild} bLabel={reference.label} />}
        {reference && (!b || !refBuild) && !(scoredRef?.errors.length) && <p className="muted">Scoring…</p>}
      </Section>

      {b && <BuildCard build={b} title={current.name || 'This build'} compact />}
    </div>
  );
}

function TargetChecks({ targets }: { targets: TargetCheck[] }) {
  return (
    <div className="chip-row">
      {targets.map((t) => {
        const bonus = t.kind === 'set' || t.kind === 'group';
        return (
          <SkillHover key={t.skill} name={t.skill} className={'chip tcheck ' + (t.met ? 'met' : 'miss')}>
            <SkillIcon name={t.skill} size={18} />
            <span>{t.met ? '✓' : '✗'} {t.label}</span>
            {!t.met && <span className="muted small">has {t.actual}{bonus ? ` piece${t.actual === 1 ? '' : 's'}` : ''}</span>}
            {t.from_core && <span className="muted small">core skill</span>}
          </SkillHover>
        );
      })}
    </div>
  );
}

// ---------------------------------------------------------------- equipment editor

function EquipmentEditor({ build, update }: { build: BuildInput; update: (patch: Partial<BuildInput>) => void }) {
  const { request, resolved, talismans, setTab } = useApp();
  const { catalog, armorByName, decorationsByName, charmsByName } = useCatalog();

  // option lists, built once per catalog / talisman list
  const armorOptions = useMemo(() => {
    const byKind = new Map<ArmorPieceKind, PickerOption<string>[]>();
    const entries = [...armorByName.values()].sort((a, b) => b.rarity - a.rarity || a.set.localeCompare(b.set) || a.name.localeCompare(b.name));
    for (const p of entries) {
      const list = byKind.get(p.kind) ?? byKind.set(p.kind, []).get(p.kind)!;
      list.push({
        key: p.name, value: p.name, group: `Rarity ${p.rarity}`,
        search: [p.name, p.set, ...p.skills.map((g) => g.skill), ...p.set_bonus, p.group_skill ?? ''].join(' ').toLowerCase(),
        render: (
          <>
            <img className="icon" src={icons.armor(p.kind, p.rarity)} alt="" width={24} height={24} />
            <span className="opt-main"><span className="opt-name">{p.name}</span><span className="muted small">{grantsText(p.skills)}</span></span>
            <span className="opt-side"><Slots slots={canTranscend(p) ? p.slots_transcended : p.slots} /><span className="muted tiny">{[...p.set_bonus, p.group_skill].filter(Boolean).join(' · ')}</span></span>
          </>
        ),
      });
    }
    return byKind;
  }, [armorByName]);

  const decoOptions = useMemo(() => {
    const cache = new Map<string, PickerOption<string>[]>();
    return (slot: SlotSpec) => {
      const key = slot.kind + slot.level;
      let list = cache.get(key);
      if (!list) {
        list = (catalog?.decorations ?? [])
          .filter((d) => fits(d, slot))
          .sort((a, b) => b.slot - a.slot || a.name.localeCompare(b.name))
          .map((d) => ({
            key: d.name, value: d.name, group: `Level ${d.slot}`,
            search: [d.name, ...d.skills.map((g) => g.skill)].join(' ').toLowerCase(),
            render: (
              <>
                <DecoIcon deco={d} size={22} />
                <span className="opt-main"><span className="opt-name">{d.name}</span><span className="muted small">{grantsText(d.skills)}</span></span>
              </>
            ),
          }));
        cache.set(key, list);
      }
      return list;
    };
  }, [catalog]);

  const talismanOptions = useMemo<PickerOption<string>[]>(() => {
    const option = (name: string, rarity: number, skills: SkillGrant[], slots: string[], group: string): PickerOption<string> => ({
      key: group + name, value: name, group,
      search: [name, ...skills.map((g) => g.skill)].join(' ').toLowerCase(),
      render: (
        <>
          <img className="icon" src={icons.talisman(rarity)} alt="" width={24} height={24} />
          <span className="opt-main"><span className="opt-name">{name}</span><span className="muted small">{grantsText(skills)}</span></span>
          {slots.length > 0 && <span className="opt-side"><Slots slots={slots} /></span>}
        </>
      ),
    });
    const charms = catalog?.charms ?? [];
    return [
      ...talismans.map((t) => option(t.name, t.rarity, Object.entries(t.skills).map(([skill, level]) => ({ skill, level })), t.slots, 'Your talismans')),
      ...charms.filter((c) => c.is_max_rank).map((c) => option(c.name, c.rarity, c.skills, [], 'Craftable charms (max rank)')),
      ...charms.filter((c) => !c.is_max_rank).map((c) => option(c.name, c.rarity, c.skills, [], 'Craftable charms (lower ranks)')),
    ];
  }, [talismans, catalog]);

  if (!catalog || !request) return null;

  const rw = resolved?.weapon ?? null;
  const weaponSlots: SlotSpec[] = (rw?.slots ?? [3, 3, 3]).map((level) => ({ level, kind: 'weapon' }));

  const armorSlots = (a: BuildArmorInput | null): SlotSpec[] => {
    const p = a ? armorByName.get(a.piece) : undefined;
    if (!p) return [];
    return (a!.transcended && canTranscend(p) ? p.slots_transcended : p.slots).map((level) => ({ level, kind: 'armor' }));
  };

  const setArmor = (kind: ArmorPieceKind, a: BuildArmorInput | null) => update({ [kind]: a } as Partial<BuildInput>);

  const pickPiece = (kind: ArmorPieceKind, name: string) => {
    const p = armorByName.get(name)!;
    const transcended = request.options.allow_transcendence && canTranscend(p);
    const next: BuildArmorInput = { piece: name, transcended, decorations: [] };
    setArmor(kind, { ...next, decorations: fitDecorations(build[kind]?.decorations ?? [], armorSlots(next), decorationsByName) });
  };

  const setTranscended = (kind: ArmorPieceKind, a: BuildArmorInput, transcended: boolean) => {
    const next = { ...a, transcended };
    setArmor(kind, { ...next, decorations: fitDecorations(a.decorations, armorSlots(next), decorationsByName) });
  };

  const t = build.talisman;
  const randomTalisman = t ? talismans.find((x) => x.name === t.name) : undefined;
  const charm = t && !randomTalisman ? charmsByName.get(t.name) : undefined;
  const talismanSlots: SlotSpec[] = randomTalisman ? randomTalisman.slots.map(parseSlot) : [];
  const talismanSkills: SkillGrant[] = randomTalisman ? Object.entries(randomTalisman.skills).map(([skill, level]) => ({ skill, level })) : charm?.skills ?? [];
  const talismanRarity = randomTalisman?.rarity ?? charm?.rarity ?? 1;
  const pickTalisman = (name: string) => {
    const r = talismans.find((x) => x.name === name);
    const slots = r ? r.slots.map(parseSlot) : [];
    update({ talisman: { name, decorations: fitDecorations(t?.decorations ?? [], slots, decorationsByName) } });
  };

  return (
    <table className="table equip editor">
      <tbody>
        <tr>
          <td className="icon-cell"><img className="icon" src={icons.weapon(rw?.type ?? request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword')} alt="" width={32} height={32} /></td>
          <td className="piece">Weapon</td>
          <td className="name">
            {rw ? (
              <>
                <b>{rw.label}</b> <span className="muted small">from the <button className="linklike" onClick={() => setTab('weapon')}>Weapon tab</button></span>
                <div className="muted small">
                  {rw.display_attack} attack, {rw.affinity}% affinity, {rw.element === 'none' ? 'no element' : `${rw.element_display} ${titleCase(rw.element)}`}{rw.sharpness ? `, ${titleCase(rw.sharpness)} sharpness` : ''}
                </div>
              </>
            ) : <span className="err">Weapon has errors</span>}
          </td>
          <td className="skills">
            <div className="pairpick">
              <span className="with-icon muted small"><SkillIcon name="set" kind="set" size={16} /> set bonus</span>
              <Select value={build.set_bonus} placeholder={`as rolled: ${rw?.set_bonus ?? 'none'}`} onChange={(set_bonus) => update({ set_bonus })}
                options={catalog.skill_pool.set_bonuses.map((s) => ({ value: s, label: s }))} />
              <span className="with-icon muted small"><SkillIcon name="group" kind="group" size={16} /> group skill</span>
              <Select value={build.group_skill} placeholder={`as rolled: ${rw?.group_skill ?? 'none'}`} onChange={(group_skill) => update({ group_skill })}
                options={catalog.skill_pool.group_skills.map((s) => ({ value: s, label: s }))} />
            </div>
          </td>
          <td className="decos"><SlotPickers slots={weaponSlots} values={build.weapon_decorations} options={decoOptions} onChange={(weapon_decorations) => update({ weapon_decorations })} /></td>
        </tr>

        {armorKinds.map((kind) => {
          const a = build[kind];
          const p = a ? armorByName.get(a.piece) : undefined;
          return (
            <tr key={kind}>
              <td className="icon-cell"><img className="icon" src={p ? icons.armor(kind, p.rarity) : icons.armorBase(kind)} alt="" width={32} height={32} /></td>
              <td className="piece">{pieceLabel[kind]}</td>
              <td className="name">
                <Picker
                  className="piecepick"
                  trigger={p ? <><b>{p.name}</b> <RarityBadge rarity={p.rarity} transcended={a!.transcended && canTranscend(p)} /></> : a ? <span className="err">{a.piece} (unknown)</span> : <span className="muted">— choose {pieceLabel[kind].toLowerCase()} —</span>}
                  options={armorOptions.get(kind) ?? []}
                  onPick={(name) => pickPiece(kind, name)}
                  onClear={a ? () => setArmor(kind, null) : undefined}
                  clearLabel="— nothing equipped —"
                  placeholder="Search pieces, sets, skills, set bonuses…"
                />
                {p && (
                  <div className="muted small piece-meta">
                    {p.set_bonus.map((x) => <SkillHover key={x} name={x} className="with-icon"><SkillIcon name={x} kind="set" size={14} />{x}</SkillHover>)}
                    {p.group_skill && <SkillHover name={p.group_skill} className="with-icon"><SkillIcon name={p.group_skill} kind="group" size={14} />{p.group_skill}</SkillHover>}
                    {canTranscend(p) && (
                      <label className="check" title="Transcended rarity 5/6 armor gets better decoration slots">
                        <input type="checkbox" checked={a!.transcended} onChange={(e) => setTranscended(kind, a!, e.target.checked)} />
                        transcended <Slots slots={p.slots} /> → <Slots slots={p.slots_transcended} />
                      </label>
                    )}
                  </div>
                )}
              </td>
              <td className="skills"><span className="chip-row">{p?.skills.map((g) => <SkillChip key={g.skill} name={g.skill} level={g.level} />)}</span></td>
              <td className="decos">
                {a && p && <SlotPickers slots={armorSlots(a)} values={a.decorations} options={decoOptions} onChange={(decorations) => setArmor(kind, { ...a, decorations })} />}
              </td>
            </tr>
          );
        })}

        <tr>
          <td className="icon-cell">{t ? <img className="icon" src={icons.talisman(talismanRarity)} alt="" width={32} height={32} /> : null}</td>
          <td className="piece">Talisman</td>
          <td className="name">
            <Picker
              className="piecepick"
              trigger={t ? (randomTalisman || charm ? <><b>{t.name}</b> <RarityBadge rarity={talismanRarity} /></> : <span className="err">{t.name} (not found)</span>) : <span className="muted">— choose talisman —</span>}
              options={talismanOptions}
              onPick={pickTalisman}
              onClear={t ? () => update({ talisman: null }) : undefined}
              clearLabel="— no talisman —"
              placeholder="Search talismans, charms, skills…"
            />
            {t && <div className="muted small">{randomTalisman ? 'your random talisman' : charm ? 'craftable charm' : 'not in your talisman list; pick it again'}</div>}
          </td>
          <td className="skills"><span className="chip-row">{talismanSkills.map((g) => <SkillChip key={g.skill} name={g.skill} level={g.level} />)}</span></td>
          <td className="decos">
            {t && <SlotPickers slots={talismanSlots} values={t.decorations} options={decoOptions} onChange={(decorations) => update({ talisman: { ...t, decorations } })} />}
          </td>
        </tr>
      </tbody>
    </table>
  );
}

/** One decoration picker per slot; values are kept one entry per slot. */
function SlotPickers({ slots, values, options, onChange }: {
  slots: SlotSpec[]; values: (string | null)[]; options: (slot: SlotSpec) => PickerOption<string>[]; onChange: (values: (string | null)[]) => void;
}) {
  const { decorationsByName } = useCatalog();
  if (slots.length === 0) return <span className="muted small">no slots</span>;
  const set = (i: number, name: string | null) => onChange(slots.map((_, j) => (j === i ? name : values[j] ?? null)));
  return (
    <div className="slotpicks">
      {slots.map((slot, i) => {
        const name = values[i] ?? null;
        const deco = name ? decorationsByName.get(name) : undefined;
        return (
          <Picker
            key={i}
            className={'slotpick' + (name ? '' : ' vacant') + (name && !fits(deco, slot) ? ' bad' : '')}
            title={deco ? `${deco.name}: ${grantsText(deco.skills)}` : `empty level ${slot.level} ${slot.kind} slot`}
            trigger={
              <>
                <span className={'slot' + (slot.kind === 'weapon' ? ' weapon' : '')}>{slot.level}</span>
                <DecoIcon deco={deco ?? null} slotLevel={slot.level} size={22} />
                <span className="slotpick-name">{deco?.name ?? (name ? `${name} (unknown)` : 'empty')}</span>
              </>
            }
            options={options(slot)}
            onPick={(n) => set(i, n)}
            onClear={name ? () => set(i, null) : undefined}
            clearLabel="— empty slot —"
            placeholder={`Level ${slot.level} ${slot.kind} slot: search jewels or skills…`}
          />
        );
      })}
    </div>
  );
}
