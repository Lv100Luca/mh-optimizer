import { useMemo, useState } from 'react';
import { icons } from '../icons';
import { useApp, useCatalog } from '../state';
import type { TalismanInput } from '../types';
import { Alert, Button, Field, Section, Segmented, Select, SkillChip, Slots, Stepper, Toggle } from './common';

interface Draft { name: string; rarity: number; primary: { skill: string; level: number } | null; secondary: ({ skill: string; level: number } | null)[]; slots: string }

export function TalismansPanel() {
  const { request, patchRequest, talismans, setTalismans, builds, setBuilds, resolved, profile } = useApp();
  const { catalog, skillsByName } = useCatalog();
  const [draft, setDraft] = useState<{ index: number | null; value: Draft } | null>(null);

  const pool = catalog?.talisman_pool ?? null;
  const primary = useMemo(() => pool ? pool.primary : (catalog?.skills ?? []).filter((s) => s.kind === 'weapon').map((s) => ({ skill: s.name, max_level: s.max_level })), [pool, catalog]);
  const secondary = useMemo(() => pool ? pool.secondary : (catalog?.skills ?? []).filter((s) => s.kind === 'armor').map((s) => ({ skill: s.name, max_level: s.max_level })), [pool, catalog]);
  const primaryNames = useMemo(() => new Set(primary.map((p) => p.skill)), [primary]);
  const slotPatterns = useMemo(() => {
    const patterns = pool ? pool.slot_patterns.map((p) => p.join(',')) : ['', 'armor1', 'armor1,armor1', 'armor1,armor1,armor1', 'armor2', 'armor2,armor1', 'armor3', 'weapon1', 'weapon1,armor1', 'weapon1,armor1,armor1'];
    return [...new Set(patterns)].sort((a, b) => a.length - b.length || a.localeCompare(b));
  }, [pool]);
  const rarities = pool?.rarities.length ? pool.rarities : [4, 5, 6, 7];

  if (!request || !catalog) return null;

  const notes = [...(resolved?.errors ?? []).map((e) => ({ kind: 'error' as const, text: e })), ...(resolved?.warnings ?? []).map((w) => ({ kind: 'warning' as const, text: w }))]
    .filter((n) => n.text.startsWith('Talisman'));

  const toDraft = (t: TalismanInput | null): Draft => {
    const entries = Object.entries(t?.skills ?? {});
    const p = entries.find(([s]) => primaryNames.has(s));
    const rest = entries.filter(([s]) => !primaryNames.has(s));
    return {
      name: t?.name ?? `Talisman ${talismans.length + 1}`,
      rarity: t?.rarity ?? 7,
      primary: p ? { skill: p[0], level: p[1] } : null,
      secondary: [rest[0] ? { skill: rest[0][0], level: rest[0][1] } : null, rest[1] ? { skill: rest[1][0], level: rest[1][1] } : null],
      slots: (t?.slots ?? []).join(','),
    };
  };

  const fromDraft = (d: Draft): TalismanInput => {
    const skills: Record<string, number> = {};
    if (d.primary) skills[d.primary.skill] = d.primary.level;
    for (const s of d.secondary) if (s && !(s.skill in skills)) skills[s.skill] = s.level;
    return { name: d.name.trim() || 'Talisman', rarity: d.rarity, skills, slots: d.slots ? d.slots.split(',') : [] };
  };

  const commit = () => {
    if (!draft) return;
    const value = fromDraft(draft.value);
    // hand-entered builds refer to talismans by name; the saved builds of every weapon follow a rename, and so does the open weapon
    const old = draft.index === null ? null : talismans[draft.index]?.name;
    const renamed = !!old && old !== value.name;
    setTalismans((list) => (draft.index === null ? [...list, value] : list.map((t, i) => (i === draft.index ? value : t))), renamed ? { [old]: value.name } : undefined);
    if (renamed && builds.some((b) => b.talisman?.name === old))
      setBuilds((list) => list.map((b) => (b.talisman?.name === old ? { ...b, talisman: { ...b.talisman, name: value.name } } : b)));
    setDraft(null);
  };

  const maxFor = (skill: string, poolList: { skill: string; max_level: number }[]) => {
    const skillMax = skillsByName.get(skill)?.max_level ?? 7;
    const poolMax = poolList.find((p) => p.skill === skill)?.max_level;
    return { skillMax, poolMax };
  };

  const renderSkillField = (label: string, poolList: { skill: string; max_level: number }[], value: { skill: string; level: number } | null, onChange: (v: { skill: string; level: number } | null) => void, exclude: string[]) => {
    const { skillMax, poolMax } = value ? maxFor(value.skill, poolList) : { skillMax: 1, poolMax: undefined };
    return (
      <Field label={label} hint={value && poolMax !== undefined && poolMax < skillMax ? `random talismans roll ${value.skill} at most at level ${poolMax}` : undefined}>
        <div className="btn-row">
          <Select value={value?.skill ?? null} placeholder="(none)" options={poolList.filter((p) => !exclude.includes(p.skill) || p.skill === value?.skill).map((p) => ({ value: p.skill, label: p.skill }))}
            onChange={(skill) => onChange(skill ? { skill, level: Math.min(skillsByName.get(skill)?.max_level ?? 1, poolList.find((p) => p.skill === skill)?.max_level ?? 1) } : null)} />
          {value && <Stepper value={value.level} min={1} max={skillMax} onChange={(level) => onChange({ ...value, level })} />}
        </div>
      </Field>
    );
  };

  return (
    <div className="panel">
      <Section
        title={`Talismans of ${profile}`}
        hint="Talismans belong to the account: every weapon of the profile draws from this pool, and changes are saved right away. Craftable charm lines are always available at their highest rank. Add the random (melded) talismans you own; the optimizer picks the best one per build."
        actions={draft === null && <Button kind="primary" small onClick={() => setDraft({ index: null, value: toDraft(null) })}>+ Add talisman</Button>}
      >
        <Toggle
          checked={request.talismans.include_craftable}
          onChange={(include_craftable) => patchRequest((r) => ({ ...r, talismans: { ...r.talismans, include_craftable } }))}
          label={`Include the ${catalog.craftable_talisman_count} craftable charm lines`}
          hint="Every charm line at max rank (e.g. Challenger Charm III)."
        />

        {talismans.length === 0 && draft === null && <p className="muted">No random talismans yet.</p>}
        {talismans.length > 0 && (
          <table className="table talisman-table">
            <thead><tr><th></th><th>Name</th><th>Skills</th><th>Slots</th><th></th></tr></thead>
            <tbody>
              {talismans.map((t, i) => (
                <tr key={i}>
                  <td><img className="icon" src={icons.talisman(t.rarity)} alt="" width={28} height={28} title={`rarity ${t.rarity}`} /></td>
                  <td><b>{t.name}</b><div className="muted small">rarity {t.rarity}</div></td>
                  <td><div className="chip-row">{Object.entries(t.skills).map(([s, lv]) => <SkillChip key={s} name={s} level={lv} />)}</div></td>
                  <td><Slots slots={t.slots} /></td>
                  <td className="right">
                    <Button small onClick={() => setDraft({ index: i, value: toDraft(t) })}>Edit</Button>{' '}
                    <Button small kind="ghost" onClick={() => setTalismans((list) => list.filter((_, j) => j !== i))}>Remove</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {notes.map((n) => <Alert key={n.text} kind={n.kind}>{n.text}</Alert>)}
      </Section>

      {draft && (
        <Section title={draft.index === null ? 'New talisman' : `Edit ${talismans[draft.index]?.name}`} hint="Skill 1 is the weapon-kind line a random talisman rolls first; skills 2 and 3 are armor-kind lines.">
          <div className="form-grid">
            <Field label="Name"><input className="text" value={draft.value.name} onChange={(e) => setDraft({ ...draft, value: { ...draft.value, name: e.target.value } })} /></Field>
            <Field label="Rarity">
              <Segmented<number> value={draft.value.rarity} onChange={(rarity) => setDraft({ ...draft, value: { ...draft.value, rarity } })}
                options={rarities.map((r) => ({ value: r, label: <span className="with-icon"><img src={icons.talisman(r)} alt="" width={18} height={18} /> R{r}</span> }))} />
            </Field>
            {renderSkillField('Skill 1 (weapon-kind)', primary, draft.value.primary, (primary) => setDraft({ ...draft, value: { ...draft.value, primary } }), [])}
            {renderSkillField('Skill 2 (armor-kind)', secondary, draft.value.secondary[0], (v) => setDraft({ ...draft, value: { ...draft.value, secondary: [v, draft.value.secondary[1]] } }), [draft.value.secondary[1]?.skill ?? ''])}
            {renderSkillField('Skill 3 (armor-kind)', secondary, draft.value.secondary[1], (v) => setDraft({ ...draft, value: { ...draft.value, secondary: [draft.value.secondary[0], v] } }), [draft.value.secondary[0]?.skill ?? ''])}
            <Field label="Decoration slots" hint="Random talismans roll these layouts; w = weapon-kind slot, a = armor-kind slot.">
              <Select value={draft.value.slots || null} placeholder="(no slots)" options={slotPatterns.filter(Boolean).map((p) => ({ value: p, label: p.split(',').map((s) => s.replace('armor', 'a').replace('weapon', 'w')).join(' · ') }))}
                onChange={(slots) => setDraft({ ...draft, value: { ...draft.value, slots: slots ?? '' } })} />
            </Field>
          </div>
          <div className="btn-row">
            <Button kind="primary" onClick={commit} disabled={!draft.value.primary && !draft.value.secondary.some(Boolean)}>{draft.index === null ? 'Add' : 'Apply'}</Button>
            <Button kind="ghost" onClick={() => setDraft(null)}>Cancel</Button>
          </div>
        </Section>
      )}
    </div>
  );
}
