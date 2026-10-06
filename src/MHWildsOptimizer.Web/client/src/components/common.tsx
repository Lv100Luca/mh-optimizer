import { useEffect, useState, type ReactNode } from 'react';
import { icons } from '../icons';
import type { Deco, SkillGrant, SkillKind } from '../types';
import { useCatalog } from '../state';

export function Section({ title, hint, actions, children, className }: { title: ReactNode; hint?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={'card ' + (className ?? '')}>
      <header className="card-head">
        <div>
          <h2>{title}</h2>
          {hint && <p className="hint">{hint}</p>}
        </div>
        {actions && <div className="card-actions">{actions}</div>}
      </header>
      <div className="card-body">{children}</div>
    </section>
  );
}

export function Field({ label, hint, children, inline }: { label: ReactNode; hint?: ReactNode; children: ReactNode; inline?: boolean }) {
  return (
    <label className={'field' + (inline ? ' inline' : '')}>
      <span className="field-label">{label}</span>
      {children}
      {hint && <span className="field-hint">{hint}</span>}
    </label>
  );
}

export function Toggle({ checked, onChange, label, hint, disabled }: { checked: boolean; onChange: (v: boolean) => void; label: ReactNode; hint?: ReactNode; disabled?: boolean }) {
  return (
    <label className={'toggle' + (disabled ? ' disabled' : '') + (checked ? ' on' : '')}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span className="toggle-track"><span className="toggle-thumb" /></span>
      <span className="toggle-text">
        <span>{label}</span>
        {hint && <span className="field-hint">{hint}</span>}
      </span>
    </label>
  );
}

export function NumberInput({ value, onChange, min, max, step, width }: { value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number; width?: number }) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  return (
    <input
      className="number"
      style={width ? { width } : undefined}
      type="number"
      value={text}
      min={min}
      max={max}
      step={step}
      onChange={(e) => {
        setText(e.target.value);
        const n = Number(e.target.value);
        if (e.target.value !== '' && Number.isFinite(n)) onChange(clamp(n, min, max));
      }}
      onBlur={() => setText(String(value))}
    />
  );
}

function clamp(n: number, min?: number, max?: number) {
  if (min !== undefined && n < min) return min;
  if (max !== undefined && n > max) return max;
  return n;
}

export function Stepper({ value, min, max, onChange, suffix }: { value: number; min: number; max: number; onChange: (v: number) => void; suffix?: string }) {
  return (
    <span className="stepper">
      <button type="button" disabled={value <= min} onClick={() => onChange(value - 1)} aria-label="decrease">−</button>
      <span className="stepper-value">{value}{suffix}</span>
      <button type="button" disabled={value >= max} onClick={() => onChange(value + 1)} aria-label="increase">+</button>
    </span>
  );
}

export function Segmented<T extends string | number>({ value, options, onChange, small }: {
  value: T; options: { value: T; label: ReactNode; hint?: string; disabled?: boolean }[]; onChange: (v: T) => void; small?: boolean;
}) {
  return (
    <div className={'segmented' + (small ? ' small' : '')} role="radiogroup">
      {options.map((o) => (
        <button key={String(o.value)} type="button" role="radio" aria-checked={o.value === value} title={o.hint} disabled={o.disabled}
          className={o.value === value ? 'active' : ''} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Select<T extends string>({ value, options, onChange, placeholder }: {
  value: T | null; options: { value: T; label: string; group?: string }[]; onChange: (v: T | null) => void; placeholder?: string;
}) {
  const groups = new Map<string | undefined, typeof options>();
  for (const o of options) (groups.get(o.group) ?? groups.set(o.group, []).get(o.group)!).push(o);
  return (
    <select className="select" value={value ?? ''} onChange={(e) => onChange((e.target.value || null) as T | null)}>
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {[...groups.entries()].map(([group, items]) =>
        group ? (
          <optgroup key={group} label={group}>
            {items.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          </optgroup>
        ) : (
          items.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)
        ),
      )}
    </select>
  );
}

export function Alert({ kind, children }: { kind: 'error' | 'warning' | 'info' | 'ok'; children: ReactNode }) {
  return <div className={'alert ' + kind}>{children}</div>;
}

export function Button({ children, onClick, kind, disabled, title, small, type }: {
  children: ReactNode; onClick?: () => void; kind?: 'primary' | 'danger' | 'ghost'; disabled?: boolean; title?: string; small?: boolean; type?: 'button' | 'submit';
}) {
  return (
    <button type={type ?? 'button'} className={'btn ' + (kind ?? '') + (small ? ' small' : '')} onClick={onClick} disabled={disabled} title={title}>
      {children}
    </button>
  );
}

export function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <input className="search" type="search" value={value} placeholder={placeholder ?? 'Search…'} onChange={(e) => onChange(e.target.value)} />
  );
}

// ---------------------------------------------------------------- game icons

export function SkillIcon({ name, kind, size }: { name: string; kind?: SkillKind; size?: number }) {
  const { skillsByName } = useCatalog();
  const skill = skillsByName.get(name);
  return <img className="icon" width={size ?? 20} height={size ?? 20} src={icons.skill(skill?.icon, skill?.kind ?? kind)} alt="" title={skill?.description ?? name} />;
}

/** "Weakness Exploit 5" with its icon; weapon-kind skills get the blue accent like the console output. */
export function SkillChip({ name, level, effective, max, onClick, muted }: { name: string; level?: number; effective?: number; max?: number; onClick?: () => void; muted?: boolean }) {
  const { skillsByName } = useCatalog();
  const skill = skillsByName.get(name);
  const kind = skill?.kind ?? 'armor';
  const Tag = onClick ? 'button' : 'span';
  return (
    <Tag className={`chip skill ${kind}` + (muted ? ' muted' : '')} onClick={onClick} title={skill?.description ?? undefined} type={onClick ? 'button' : undefined}>
      <SkillIcon name={name} size={18} />
      <span>{name}</span>
      {level !== undefined && <b>{level}{max !== undefined ? <span className="of">/{max}</span> : null}</b>}
      {effective !== undefined && level !== undefined && effective < level && <span className="valued">valued at {effective}</span>}
    </Tag>
  );
}

export function GrantList({ grants }: { grants: SkillGrant[] }) {
  return <span className="grants">{grants.map((g) => <SkillChip key={g.skill} name={g.skill} level={g.level} />)}</span>;
}

export function DecoIcon({ deco, slotLevel, size }: { deco: Deco | null; slotLevel?: number; size?: number }) {
  const s = size ?? 24;
  if (!deco) return <span className="deco-empty" style={{ width: s, height: s }} title={slotLevel ? `empty level ${slotLevel} slot` : 'empty slot'}>{slotLevel ?? ''}</span>;
  const title = `${deco.name}: ${deco.skills.map((g) => `${g.skill} ${g.level}`).join(', ')}`;
  return <img className="icon deco" width={s} height={s} src={icons.decoration(deco.slot, deco.kind, deco.icon_color)} alt={deco.name} title={title} />;
}

export function Slots({ slots, kind }: { slots: number[] | string[]; kind?: 'weapon' | 'armor' }) {
  if (slots.length === 0) return <span className="muted">no slots</span>;
  return (
    <span className="slots">
      {slots.map((s, i) => {
        const text = String(s);
        const level = Number(text.replace(/\D/g, '')) || 0;
        const isWeapon = kind === 'weapon' || text.startsWith('w');
        return <span key={i} className={'slot' + (isWeapon ? ' weapon' : '')} title={(isWeapon ? 'weapon' : 'armor') + ` slot level ${level}`}>{level}</span>;
      })}
    </span>
  );
}

export function RarityBadge({ rarity, transcended }: { rarity: number; transcended?: boolean }) {
  return <span className={`rarity r${rarity}`} title={`rarity ${rarity}` + (transcended ? ', transcended slots' : '')}>R{rarity}{transcended ? 'T' : ''}</span>;
}

export function fmt(n: number, digits = 1) {
  return n.toLocaleString('en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

export function titleCase(s: string) {
  return s.replace(/[-_]/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}
