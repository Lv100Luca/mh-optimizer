// Helpers for hand-entered builds (BuildInput) and how they relate to the optimizer's builds.
import type { ArmorPieceKind, Build, BuildArmorInput, BuildInput, Decoration, SkillKind } from './types';

export const armorKinds: ArmorPieceKind[] = ['head', 'chest', 'arms', 'waist', 'legs'];

export const pieceLabel: Record<ArmorPieceKind, string> = { head: 'Head', chest: 'Chest', arms: 'Arms', waist: 'Waist', legs: 'Legs' };

export function emptyBuild(name: string): BuildInput {
  return { name, set_bonus: null, group_skill: null, weapon_decorations: [], head: null, chest: null, arms: null, waist: null, legs: null, talisman: null };
}

/** The hand-entered form of an optimizer build, so it can be edited and compared. */
export function fromBuild(b: Build, name: string): BuildInput {
  const names = (decos: ({ name: string } | null)[]) => decos.map((d) => d?.name ?? null);
  const armor = (kind: ArmorPieceKind): BuildArmorInput | null => {
    const a = b.armor.find((x) => x.kind === kind);
    return a ? { piece: a.name, transcended: a.transcended, decorations: names(a.decorations) } : null;
  };
  return {
    name,
    set_bonus: b.weapon.set_bonus,
    group_skill: b.weapon.group_skill,
    weapon_decorations: names(b.weapon.decorations),
    head: armor('head'), chest: armor('chest'), arms: armor('arms'), waist: armor('waist'), legs: armor('legs'),
    talisman: b.talisman ? { name: b.talisman.name, decorations: names(b.talisman.decorations) } : null,
  };
}

/** A slot as the editor sees it: level plus the decoration kind it takes. */
export interface SlotSpec { level: number; kind: SkillKind }

/** "weapon1" / "armor2" (talisman slot notation) -> slot spec. */
export function parseSlot(text: string): SlotSpec {
  return { level: Number(text.replace(/\D/g, '')) || 1, kind: text.startsWith('w') ? 'weapon' : 'armor' };
}

export function fits(deco: Decoration | undefined, slot: SlotSpec) {
  return !!deco && deco.kind === slot.kind && deco.slot <= slot.level;
}

/** Keeps each decoration that still fits its slot (by position) after the slots changed; one entry per slot. */
export function fitDecorations(names: (string | null)[], slots: SlotSpec[], decorations: Map<string, Decoration>): (string | null)[] {
  return slots.map((slot, i) => {
    const name = names[i] ?? null;
    return name && fits(decorations.get(name), slot) ? name : null;
  });
}

/** A name for a new build that is not taken yet: "My build", "My build 2", ... */
export function uniqueName(base: string, taken: string[]) {
  if (!taken.includes(base)) return base;
  for (let i = 2; ; i++) if (!taken.includes(`${base} ${i}`)) return `${base} ${i}`;
}
