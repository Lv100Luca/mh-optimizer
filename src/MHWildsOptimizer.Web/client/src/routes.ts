// Page URLs, so the browser's back / forward, reloads and bookmarks work:
//   /<profile>/inventory
//   /<profile>/weapon/<weapon>/<tab>
//   /<profile>/new/<tab>            (the unsaved new weapon)
// The tab is always the last segment: the server's SPA fallback skips paths whose last segment looks like a file
// ("the.best"), and tab names never contain a dot.

export const tabs = ['inventory', 'weapon', 'pair', 'targets', 'limits', 'conditions', 'talismans', 'options', 'review', 'results', 'builds'] as const;

export type Tab = (typeof tabs)[number];

const isTab = (s: string | undefined): s is Tab => !!s && (tabs as readonly string[]).includes(s);

/** Where the app is: a profile, the open weapon (null = the unsaved new one) and a tab. */
export interface Route { profile: string; weapon: string | null; tab: Tab }

export function routeUrl(r: Route): string {
  const p = encodeURIComponent(r.profile);
  if (r.tab === 'inventory') return `/${p}/inventory`;
  return r.weapon === null ? `/${p}/new/${r.tab}` : `/${p}/weapon/${encodeURIComponent(r.weapon)}/${r.tab}`;
}

/** The route of a URL path; parts that are missing or unknown are undefined (weapon undefined = keep the open one). */
export function parseRoute(pathname: string): { profile?: string; weapon?: string | null; tab?: Tab } {
  let parts: string[];
  try { parts = pathname.split('/').filter(Boolean).map(decodeURIComponent); } catch { return {}; }
  const [profile, kind, a, b] = parts;
  if (!profile) return {};
  if (kind === 'inventory') return { profile, tab: 'inventory' };
  if (kind === 'new') return { profile, weapon: null, tab: isTab(a) && a !== 'inventory' ? a : 'weapon' };
  if (kind === 'weapon' && a) return { profile, weapon: a, tab: isTab(b) && b !== 'inventory' ? b : 'weapon' };
  return { profile };
}
