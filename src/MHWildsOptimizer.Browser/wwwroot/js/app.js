// Small browser helpers the Blazor app calls synchronously (IJSInProcessRuntime) or asynchronously.
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

  copyText: async (text) => { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } },

  /** Warns before leaving the page while there are unsaved edits or a run is going. */
  setLeaveWarning: (on) => { window.onbeforeunload = on ? (e) => { e.preventDefault(); return ''; } : null; },
};
