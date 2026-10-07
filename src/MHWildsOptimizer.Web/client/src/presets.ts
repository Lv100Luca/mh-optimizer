// Weapon-type condition presets: a weapon's conditions follow its type's preset value by value. A value equal to the preset
// follows it when the preset (or the weapon type) changes; a value that differs is the weapon's own override.
// Mirrors ConditionPresets in src/MHWildsOptimizer.Core/Inputs/Profiles.cs.
import type { Conditions, OptimizationRequest } from './types';

export const weaponKind = (r: OptimizationRequest) => r.weapon.spec?.type ?? r.weapon.type ?? 'great-sword';

/** Key-order independent JSON, so { Burst: 1, Focus: 3 } equals { Focus: 3, Burst: 1 }. */
function canonical(value: unknown): string {
  if (value === undefined || value === null) return 'null';
  if (typeof value !== 'object') return JSON.stringify(value);
  if (Array.isArray(value)) return '[' + value.map(canonical).join(',') + ']';
  const entries = Object.entries(value as Record<string, unknown>).sort(([a], [b]) => a.localeCompare(b));
  return '{' + entries.map(([k, v]) => JSON.stringify(k) + ':' + canonical(v)).join(',') + '}';
}

const same = (a: unknown, b: unknown) => canonical(a) === canonical(b);

const keysOf = (...all: Conditions[]) => [...new Set(all.flatMap((c) => Object.keys(c)))];

/** The condition keys (snake_case, as in the request) where the weapon differs from the preset. */
export function overrides(current: Conditions, preset: Conditions): string[] {
  return keysOf(current, preset).filter((k) => !same(current[k], preset[k]));
}

/** Moves conditions from one preset to another, keeping the weapon's overrides. */
export function follow(current: Conditions, oldPreset: Conditions, newPreset: Conditions): Conditions {
  const result: Conditions = structuredClone(current);
  for (const k of keysOf(current, oldPreset, newPreset))
    if (same(current[k], oldPreset[k])) result[k] = structuredClone(newPreset[k]);
  return result;
}
