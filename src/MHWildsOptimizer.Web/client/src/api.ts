import type { Catalog, ConfigFile, ConfigPayload, ConfigSummary, OptimizationResult, OptimizeEvent, Resolved } from './types';

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

export const api = {
  catalog: () => fetch('/api/catalog').then(json<Catalog>),
  configs: () => fetch('/api/configs').then(json<ConfigSummary[]>),
  loadConfig: (name: string) => fetch(`/api/configs/${encodeURIComponent(name)}`).then(json<ConfigFile>),
  saveConfig: (name: string, payload: ConfigPayload) =>
    fetch(`/api/configs/${encodeURIComponent(name)}`, { method: 'PUT', headers, body: JSON.stringify(payload) }).then(json<ConfigFile>),
  deleteConfig: (name: string) => fetch(`/api/configs/${encodeURIComponent(name)}`, { method: 'DELETE' }).then(json<void>),
  results: (name: string) => fetch(`/api/configs/${encodeURIComponent(name)}/results`).then(json<OptimizationResult>),
  resolve: (payload: ConfigPayload, signal?: AbortSignal) =>
    fetch('/api/resolve', { method: 'POST', headers, body: JSON.stringify(payload), signal }).then(json<Resolved>),

  /** Runs the optimizer; events arrive as server-sent events until "result" or "error". */
  async optimize(payload: ConfigPayload, save: string | null, onEvent: (e: OptimizeEvent) => void, signal: AbortSignal): Promise<void> {
    const url = '/api/optimize' + (save ? `?save=${encodeURIComponent(save)}` : '');
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
