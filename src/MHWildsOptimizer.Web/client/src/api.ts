import type {
  Catalog, Conditions, ConfigPayload, EvaluatedBuild, InventoryEntry, OptimizationResult, OptimizeEvent, PresetSaved, Profile, ProfileSummary, Resolved,
  TalismanInput, WeaponFile, WeaponPayload, WeaponSummary,
} from './types';

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      if (body?.message) message = body.message;
      else if (body?.title) message = body.title + (body.detail ? ': ' + body.detail : '');
      else if (body?.errors) message = Object.entries(body.errors).map(([k, v]) => `${k}: ${(v as string[]).join(', ')}`).join('; ');
    } catch {
      /* no JSON body */
    }
    throw new Error(message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

const headers = { 'Content-Type': 'application/json' };

const prof = (profile: string) => `/api/profiles/${encodeURIComponent(profile)}`;
const weapon = (profile: string, name: string) => `${prof(profile)}/weapons/${encodeURIComponent(name)}`;

export const api = {
  catalog: () => fetch('/api/catalog').then(json<Catalog>),
  profiles: () => fetch('/api/profiles').then(json<ProfileSummary[]>),
  profile: (profile: string) => fetch(prof(profile)).then(json<Profile>),
  createProfile: (profile: string) => fetch(prof(profile), { method: 'POST' }).then(json<Profile>),
  /** Saves the account's talisman pool; renames (old -> new name) are followed by the builds of every weapon. */
  saveTalismans: (profile: string, talismans: TalismanInput[], renames?: Record<string, string>) =>
    fetch(prof(profile) + '/talismans', { method: 'PUT', headers, body: JSON.stringify({ talismans, renames: renames ?? null }) }).then(json<Profile>),
  /** Saves a weapon type's condition preset; saved weapons of that type follow it except where they override it. */
  savePreset: (profile: string, weaponKind: string, conditions: Conditions) =>
    fetch(prof(profile) + '/presets/' + encodeURIComponent(weaponKind), { method: 'PUT', headers, body: JSON.stringify(conditions) }).then(json<PresetSaved>),
  inventory: (profile: string, signal?: AbortSignal) => fetch(prof(profile) + '/inventory', { signal }).then(json<InventoryEntry[]>),
  weapons: (profile: string) => fetch(prof(profile) + '/weapons').then(json<WeaponSummary[]>),
  loadWeapon: (profile: string, name: string) => fetch(weapon(profile, name)).then(json<WeaponFile>),
  saveWeapon: (profile: string, name: string, payload: WeaponPayload) =>
    fetch(weapon(profile, name), { method: 'PUT', headers, body: JSON.stringify(payload) }).then(json<WeaponFile>),
  deleteWeapon: (profile: string, name: string) => fetch(weapon(profile, name), { method: 'DELETE' }).then(json<void>),
  results: (profile: string, name: string) => fetch(weapon(profile, name) + '/results').then(json<OptimizationResult>),
  resolve: (payload: ConfigPayload, signal?: AbortSignal) =>
    fetch('/api/resolve', { method: 'POST', headers, body: JSON.stringify(payload), signal }).then(json<Resolved>),
  /** Scores payload.builds with the request's weapon and conditions. */
  evaluate: (payload: ConfigPayload, signal?: AbortSignal) =>
    fetch('/api/evaluate', { method: 'POST', headers, body: JSON.stringify(payload), signal }).then(json<EvaluatedBuild[]>),

  /** save = the weapon whose last run this becomes; events arrive as server-sent events until "result" or "error". */
  async optimize(payload: ConfigPayload, save: { profile: string; weapon: string } | null, onEvent: (e: OptimizeEvent) => void, signal: AbortSignal): Promise<void> {
    const url = '/api/optimize' + (save ? `?profile=${encodeURIComponent(save.profile)}&save=${encodeURIComponent(save.weapon)}` : '');
    const response = await fetch(url, { method: 'POST', headers, body: JSON.stringify(payload), signal });
    if (!response.ok || !response.body) throw new Error(`optimizer request failed: ${response.status} ${response.statusText}`);

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let eventType = 'message';
    let data: string[] = [];

    const dispatch = () => {
      if (data.length === 0) return;
      const text = data.join('\n');
      data = [];
      const type = eventType;
      eventType = 'message';
      let parsed: unknown = text;
      try { parsed = JSON.parse(text); } catch { /* keep the text */ }
      if (type === 'result') onEvent({ type: 'result', result: parsed as OptimizationResult });
      else if (type === 'validation') onEvent({ type: 'validation', ...(parsed as { errors: string[]; warnings: string[] }) });
      else if (type === 'error') onEvent({ type: 'error', message: (parsed as { message: string }).message ?? text });
      else onEvent({ type: 'progress', message: (parsed as { message?: string }).message ?? text });
    };

    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let newline: number;
      while ((newline = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, newline).replace(/\r$/, '');
        buffer = buffer.slice(newline + 1);
        if (line === '') dispatch();
        else if (line.startsWith('event:')) eventType = line.slice(6).trim();
        else if (line.startsWith('data:')) data.push(line.slice(5).replace(/^ /, ''));
      }
    }
    dispatch();
  },
};
