import { useMemo, useState } from 'react';
import { icons } from '../icons';
import { useApp, useCatalog } from '../state';
import { Button, Field, NumberInput, RarityBadge, SearchBox, Section, Segmented, SkillChip, SkillIcon, Toggle } from './common';

const beamPresets = [
  { value: 20000, label: 'Fast', hint: '20k states per slot' },
  { value: 100000, label: 'Default', hint: '100k states per slot' },
  { value: 300000, label: 'Thorough', hint: '300k states per slot' },
  { value: 1000000, label: 'Exhaustive', hint: '1M states per slot (slow)' },
];

export function OptionsPanel() {
  const { request, patchRequest, resolved } = useApp();
  const { catalog, weaponTypes } = useCatalog();
  const [query, setQuery] = useState('');
  const o = request?.options;
  const sets = useMemo(() => {
    const q = query.trim().toLowerCase();
    return (catalog?.armor_sets ?? [])
      .filter((s) => s.rarity >= (o?.min_rarity ?? 5))
      .filter((s) => !q || s.name.toLowerCase().includes(q) || s.set_bonus.some((b) => b.toLowerCase().includes(q)) || (s.group_skill ?? '').toLowerCase().includes(q));
  }, [catalog, query, o?.min_rarity]);
  if (!request || !catalog || !o) return null;

  const patch = (p: Partial<typeof o>) => patchRequest((r) => ({ ...r, options: { ...r.options, ...p } }));
  const kind = request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword';
  const core = weaponTypes.get(kind)?.core_skills ?? [];
  const excluded = new Set(o.exclude_sets);
  const toggleSet = (name: string) => patch({ exclude_sets: excluded.has(name) ? o.exclude_sets.filter((s) => s !== name) : [...o.exclude_sets, name] });
  const isPreset = beamPresets.some((p) => p.value === o.max_states_per_depth);
  const setWarnings = resolved?.warnings.filter((w) => w.startsWith('options.exclude_sets')) ?? [];

  return (
    <div className="panel two-col">
      <div className="col">
        <Section title="Search options">
          <div className="form-grid">
            <Toggle checked={o.allow_transcendence} onChange={(allow_transcendence) => patch({ allow_transcendence })}
              label="Allow transcended slots" hint="Armor Transcendence (HR 100+): rarity 5 armor gains +1 level on all three slot positions, rarity 6 on the first two." />
            <Toggle checked={o.require_weapon_core_skills} onChange={(require_weapon_core_skills) => patch({ require_weapon_core_skills })}
              label="Require the weapon's core skills"
              hint={core.length > 0 ? <span className="chip-row">{core.map((c) => <SkillChip key={c.skill} name={c.skill} level={c.level} />)}</span> : 'This weapon type has no core skills defined.'} />
            <Field label="Builds to report per skill pair" inline>
              <NumberInput value={o.top_n} min={1} max={50} width={90} onChange={(top_n) => patch({ top_n })} />
            </Field>
            <Field label="Minimum armor rarity" hint="Rarity 5 and 6 pieces are only worth it with transcendence; raise this to speed the search up.">
              <Segmented<number> value={o.min_rarity} onChange={(min_rarity) => patch({ min_rarity })}
                options={[5, 6, 7, 8].map((r) => ({ value: r, label: <span className="with-icon"><img src={icons.armor('chest', r)} alt="" width={18} height={18} /> R{r}+</span> }))} />
            </Field>
            <Field label="Search beam" hint="Partial builds kept per armor slot. Larger is closer to exhaustive but slower; the default finishes in seconds for a fixed pair.">
              <div className="btn-row">
                <Segmented<number> value={isPreset ? o.max_states_per_depth : -1} onChange={(v) => v > 0 && patch({ max_states_per_depth: v })}
                  options={[...beamPresets, ...(isPreset ? [] : [{ value: -1, label: 'Custom', hint: 'custom value' }])]} />
                <NumberInput value={o.max_states_per_depth} min={1000} max={10000000} step={1000} width={120} onChange={(max_states_per_depth) => patch({ max_states_per_depth })} />
              </div>
            </Field>
          </div>
        </Section>
      </div>

      <div className="col">
        <Section
          title={`Excluded armor sets (${o.exclude_sets.length})`}
          hint="Leave out sets you do not own, e.g. event or collaboration gear. Only sets at or above the minimum rarity are listed."
          actions={o.exclude_sets.length > 0 && <Button small kind="ghost" onClick={() => patch({ exclude_sets: [] })}>Clear</Button>}
        >
          {setWarnings.map((w) => <p key={w} className="warn small">{w}</p>)}
          <SearchBox value={query} onChange={setQuery} placeholder="Search sets, set bonuses, group skills…" />
          <div className="setlist">
            {sets.map((s) => (
              <label key={s.name} className={'setrow' + (excluded.has(s.name) ? ' excluded' : '')}>
                <input type="checkbox" checked={excluded.has(s.name)} onChange={() => toggleSet(s.name)} />
                <img className="icon" src={icons.armor('head', s.rarity)} alt="" width={22} height={22} />
                <span className="setname">{s.name}</span>
                <RarityBadge rarity={s.rarity} />
                <span className="muted small setinfo">
                  {s.set_bonus.map((b) => <span key={b} className="with-icon"><SkillIcon name={b} kind="set" size={14} />{b}</span>)}
                  {s.group_skill && <span className="with-icon"><SkillIcon name={s.group_skill} kind="group" size={14} />{s.group_skill}</span>}
                  <span>{s.pieces.length} piece{s.pieces.length === 1 ? '' : 's'}</span>
                </span>
              </label>
            ))}
          </div>
        </Section>
      </div>
    </div>
  );
}
