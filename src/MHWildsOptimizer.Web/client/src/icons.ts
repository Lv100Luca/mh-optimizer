// Paths into public/icons (fetched by tools/fetch_icons.py from monsterhunterwiki.org).
import type { ArmorPieceKind, Element, SkillKind } from './types';

/** wilds.mhdb.io decoration icon color -> wiki icon color file name. */
const decoColor: Record<string, string> = {
  purple: 'purple', white: 'white', emerald: 'emerald', sky: 'light-blue', pink: 'pink', yellow: 'yellow', blue: 'blue', gray: 'gray',
  red: 'red', ivory: 'tan', brown: 'brown', lemon: 'lemon', 'moss-green': 'moss', rose: 'rose', green: 'green', ultramarine: 'dark-blue',
  vermilion: 'vermilion', 'dark-purple': 'dark-purple', 'sage-green': 'light-green',
};

export const icons = {
  skill: (icon: string | null | undefined, kind?: SkillKind) => `/icons/skills/${icon ?? (kind === 'set' ? 'set' : kind === 'group' ? 'group' : 'utility')}.png`,
  decoration: (slot: number, kind: SkillKind, color: string | null | undefined) =>
    `/icons/decorations/l${Math.min(3, Math.max(1, slot))}-${kind === 'weapon' ? 'weapon' : 'armor'}-${decoColor[color ?? ''] ?? 'white'}.png`,
  armor: (kind: ArmorPieceKind, rarity: number) => `/icons/armor/${kind}-r${Math.min(8, Math.max(1, rarity))}.png`,
  armorBase: (kind: ArmorPieceKind) => `/icons/armor/${kind}-base.webp`,
  weapon: (kind: string) => `/icons/weapons/${kind}-r8.png`,
  weaponBase: (kind: string) => `/icons/weapons/${kind}-base.webp`,
  talisman: (rarity: number) => `/icons/talisman/r${Math.min(8, Math.max(0, rarity))}.png`,
  element: (element: Element) => (element === 'none' ? null : `/icons/elements/${element}.png`),
  affinity: '/icons/ui/affinity-up.png',
  attack: '/icons/ui/attack-up.png',
  defense: '/icons/ui/defense-up.png',
  sharpness: '/icons/ui/sharpness.png',
  decorationGeneric: '/icons/ui/decoration.png',
};

export const sharpnessCss: Record<string, string> = {
  red: '#d9342b', orange: '#e8862a', yellow: '#e6d22d', green: '#55c43a', blue: '#3c8df0', white: '#f2f2f2', purple: '#b56bf5',
};

export const elementCss: Record<Element, string> = {
  none: '#9aa0a6', fire: '#f26b3a', water: '#4aa8f0', thunder: '#f2d23c', ice: '#8fd8f5', dragon: '#9b6df0',
};

export const rarityCss: Record<number, string> = {
  1: '#c8c8c8', 2: '#c8c8c8', 3: '#cde0a8', 4: '#8dd36a', 5: '#5fbf7a', 6: '#4fa3e6', 7: '#b07be8', 8: '#ff8c42',
};
