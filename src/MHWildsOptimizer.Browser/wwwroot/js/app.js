// Small browser helpers the Blazor app calls synchronously (IJSInProcessRuntime) or asynchronously.
/** the hover tooltips being watched (watchHover): id -> stop */
const hoverWatches = new Map();

window.mhwo = {
  cores: () => navigator.hardwareConcurrency || 4,

  storage: {
    get: (key) => { try { return localStorage.getItem(key); } catch { return null; } },
    /** false when the browser refused (storage full or blocked) */
    set: (key, value) => { try { localStorage.setItem(key, value); return true; } catch { return false; } },
    remove: (key) => { try { localStorage.removeItem(key); } catch { /* blocked */ } },
    keys: (prefix) => {
      try { return Object.keys(localStorage).filter((k) => k.startsWith(prefix)); } catch { return []; }
    },
    /** characters stored under the prefix (keys and values) */
    size: (prefix) => {
      try {
        return Object.keys(localStorage).filter((k) => k.startsWith(prefix)).reduce((n, k) => n + k.length + (localStorage.getItem(k)?.length ?? 0), 0);
      } catch { return 0; }
    },
    /** asks the browser not to evict this site's storage under pressure */
    persist: async () => { try { return await navigator.storage?.persist?.() ?? false; } catch { return false; } },
  },

  /** Saves text as a file. */
  download: (fileName, mime, text) => {
    const url = URL.createObjectURL(new Blob([text], { type: mime }));
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },

  /** Scrolls the first element matching the selector into view (smoothly). */
  scrollIntoView: (selector) => { document.querySelector(selector)?.scrollIntoView({ behavior: 'smooth', block: 'start' }); },

  copyText: async (text) => { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } },

  /** Resolves once the browser has painted the page's latest changes (after 100 ms at most); a hidden tab paints nothing, so at once. */
  nextPaint: () => new Promise((resolve) => {
    if (document.hidden) { resolve(); return; }
    requestAnimationFrame(() => setTimeout(resolve, 0));
    setTimeout(resolve, 100);
  }),

  /**
   * Resolves after the browser has handled what else is waiting (input, rendering), without a timer: Chrome runs a hidden
   * tab's timers about once a minute, but delivers messages right away.
   */
  yieldToPage: () => new Promise((resolve) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = () => { channel.port1.close(); resolve(); };
    channel.port2.postMessage(null);
  }),

  /**
   * Tells a showing hover tooltip (HoverTip.cs) once the pointer is no longer over its anchor: on a move or a press
   * elsewhere, when a scroll moves something else under the pointer, or when the window loses the pointer.
   * x and y are where the pointer was when the tooltip was asked for, if known; until a move says otherwise, a scroll closes it.
   */
  watchHover: (id, el, dotnet, x, y) => {
    hoverWatches.get(id)?.();
    const root = el.closest('.mud-tooltip-root') ?? el;
    let at = x == null ? null : { x, y };
    const left = () => { stop(); dotnet.invokeMethodAsync('HoverLeft').catch(() => { /* already disposed */ }); };
    // the tooltip itself counts as the anchor, should MudBlazor keep it open while the pointer is on it
    const over = (target) => root.contains(target) || !!target?.closest?.('.mud-tooltip');
    const onPointer = (e) => { at = { x: e.clientX, y: e.clientY }; if (!over(e.target)) left(); };
    const onScroll = () => { if (!at || !root.contains(document.elementFromPoint(at.x, at.y))) left(); };
    // a finger lifted goes out without a related target too
    const onOut = (e) => { if (!e.relatedTarget && e.pointerType !== 'touch') left(); };
    const opts = { capture: true, passive: true };
    const stop = () => {
      hoverWatches.delete(id);
      document.removeEventListener('pointermove', onPointer, opts);
      document.removeEventListener('pointerdown', onPointer, opts);
      document.removeEventListener('scroll', onScroll, opts);
      document.removeEventListener('pointerout', onOut, opts);
      window.removeEventListener('blur', left);
    };
    document.addEventListener('pointermove', onPointer, opts);
    document.addEventListener('pointerdown', onPointer, opts);
    document.addEventListener('scroll', onScroll, opts);
    document.addEventListener('pointerout', onOut, opts);
    window.addEventListener('blur', left);
    hoverWatches.set(id, stop);
  },
  unwatchHover: (id) => { hoverWatches.get(id)?.(); },

  /** Warns before leaving the page while there are unsaved edits or a run is going. */
  setLeaveWarning: (on) => { window.onbeforeunload = on ? (e) => { e.preventDefault(); return ''; } : null; },
};
