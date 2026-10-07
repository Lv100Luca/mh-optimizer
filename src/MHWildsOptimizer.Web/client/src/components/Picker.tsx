import { useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

/** One choice; search is the lower-case text the query is matched against (every word must occur). */
export interface PickerOption<T> { key: string; value: T; search: string; render: ReactNode; group?: string }

const popWidth = 400;

/**
 * A button that opens a searchable list in a floating panel (armor pieces, decorations, talismans).
 * Arrow keys move through the list, Enter picks, Escape closes.
 */
export function Picker<T>({ trigger, options, onPick, onClear, clearLabel, placeholder, className, title }: {
  trigger: ReactNode;
  options: PickerOption<T>[];
  onPick: (value: T) => void;
  /** offered as the first entry when given, e.g. to empty a slot */
  onClear?: () => void;
  clearLabel?: string;
  placeholder?: string;
  className?: string;
  title?: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const [style, setStyle] = useState<CSSProperties>({});
  const button = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);

  const filtered = useMemo(() => {
    const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
    return words.length ? options.filter((o) => words.every((w) => o.search.includes(w))) : options;
  }, [options, query]);

  const close = () => { setOpen(false); setQuery(''); setActive(0); };
  const pick = (o: PickerOption<T>) => { onPick(o.value); close(); button.current?.focus(); };

  // place the panel below the button, or above it when there is more room there
  useLayoutEffect(() => {
    if (!open || !button.current) return;
    const r = button.current.getBoundingClientRect();
    const below = window.innerHeight - r.bottom;
    const left = Math.max(8, Math.min(r.left, window.innerWidth - popWidth - 8));
    setStyle(below >= 320 || below >= r.top
      ? { left, top: r.bottom + 4, maxHeight: Math.min(460, below - 12) }
      : { left, bottom: window.innerHeight - r.top + 4, maxHeight: Math.min(460, r.top - 12) });
  }, [open]);

  // close on a click outside, on scrolling the page underneath and on resize
  useEffect(() => {
    if (!open) return;
    const outside = (e: Event) => {
      const t = e.target as Node;
      if (!panel.current?.contains(t) && !button.current?.contains(t)) close();
    };
    window.addEventListener('mousedown', outside);
    window.addEventListener('scroll', outside, true);
    window.addEventListener('resize', close);
    return () => {
      window.removeEventListener('mousedown', outside);
      window.removeEventListener('scroll', outside, true);
      window.removeEventListener('resize', close);
    };
  }, [open]);

  useEffect(() => {
    panel.current?.querySelector(`[data-index="${active}"]`)?.scrollIntoView({ block: 'nearest' });
  }, [active]);

  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, filtered.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
    else if (e.key === 'Enter') { e.preventDefault(); if (filtered[active]) pick(filtered[active]); }
    else if (e.key === 'Escape') { e.preventDefault(); close(); button.current?.focus(); }
  };

  let lastGroup: string | undefined;
  return (
    <>
      <button ref={button} type="button" className={'picker-btn ' + (className ?? '') + (open ? ' open' : '')} title={title}
        onClick={() => (open ? close() : setOpen(true))} aria-haspopup="listbox" aria-expanded={open}>
        {trigger}
      </button>
      {open && createPortal(
        <div ref={panel} className="picker-pop" style={{ ...style, width: popWidth }} role="dialog">
          <input className="search" autoFocus value={query} placeholder={placeholder ?? 'Search…'}
            onChange={(e) => { setQuery(e.target.value); setActive(0); }} onKeyDown={onKey} />
          <div className="picker-list" role="listbox">
            {onClear && !query && (
              <button type="button" className="picker-opt clear" onClick={() => { onClear(); close(); }}>{clearLabel ?? '— none —'}</button>
            )}
            {filtered.length === 0 && <div className="muted picker-empty">Nothing matches “{query}”.</div>}
            {filtered.map((o, i) => {
              const header = o.group !== undefined && o.group !== lastGroup ? <div key={'g:' + o.group} className="picker-group">{o.group}</div> : null;
              lastGroup = o.group;
              return [
                header,
                <button key={o.key} type="button" role="option" aria-selected={i === active} data-index={i}
                  className={'picker-opt' + (i === active ? ' active' : '')} onMouseEnter={() => setActive(i)} onClick={() => pick(o)}>
                  {o.render}
                </button>,
              ];
            })}
          </div>
        </div>,
        document.body,
      )}
    </>
  );
}
