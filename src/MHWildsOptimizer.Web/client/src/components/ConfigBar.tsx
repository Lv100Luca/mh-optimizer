import { useState } from 'react';
import { useApp } from '../state';
import { Button } from './common';

export function ConfigBar() {
  const { configs, name, dirty, newConfig, openConfig, save, remove, runOptimizer, cancelRun, run, resolved } = useApp();
  const [saveAs, setSaveAs] = useState<string | null>(null);

  const confirmDiscard = () => !dirty || window.confirm('Discard unsaved changes?');

  const onPick = (value: string) => {
    if (value === '') { if (confirmDiscard()) newConfig(); return; }
    if (value === name) return;
    if (confirmDiscard()) void openConfig(value);
  };

  const submitSaveAs = async () => {
    const target = (saveAs ?? '').trim().replace(/\.json$/i, '');
    if (!target) return;
    if (configs.some((c) => c.name === target) && target !== name && !window.confirm(`Overwrite the existing configuration "${target}"?`)) return;
    if (await save(target)) setSaveAs(null);
  };

  const running = run.status === 'running';
  const canRun = !!resolved?.is_valid && !running;

  return (
    <div className="configbar">
      <label className="configpick">
        <span className="muted">Configuration</span>
        <select value={name ?? ''} onChange={(e) => onPick(e.target.value)}>
          <option value="">New configuration</option>
          {configs.map((c) => (
            <option key={c.name} value={c.name} title={c.summary ?? undefined}>
              {c.name}{c.has_results ? ' ✓' : ''}
            </option>
          ))}
        </select>
      </label>
      <span className={'configname' + (dirty ? ' dirty' : '')} title={dirty ? 'unsaved changes' : 'saved'}>
        {name ? `inputs/${name}.json` : 'unsaved'}{dirty ? ' *' : ''}
      </span>

      {saveAs === null ? (
        <>
          <Button onClick={() => void save()} disabled={!name || !dirty} title={name ? 'Save to inputs/' + name + '.json' : 'Use Save as… first'}>Save</Button>
          <Button onClick={() => setSaveAs(name ?? 'my-request')}>Save as…</Button>
          {name && (
            <Button kind="ghost" onClick={() => { if (window.confirm(`Delete inputs/${name}.json, its talismans and results?`)) void remove(name); }} title="Delete this configuration">
              Delete
            </Button>
          )}
        </>
      ) : (
        <form className="saveas" onSubmit={(e) => { e.preventDefault(); void submitSaveAs(); }}>
          <span className="muted">inputs/</span>
          <input autoFocus value={saveAs} onChange={(e) => setSaveAs(e.target.value)} placeholder="name" pattern="[A-Za-z0-9][A-Za-z0-9 _.\-]*" />
          <span className="muted">.json</span>
          <Button type="submit" kind="primary" small>Save</Button>
          <Button small kind="ghost" onClick={() => setSaveAs(null)}>Cancel</Button>
        </form>
      )}

      {running ? (
        <Button kind="danger" onClick={cancelRun}>Cancel run</Button>
      ) : (
        <Button kind="primary" onClick={() => void runOptimizer()} disabled={!canRun} title={canRun ? 'Search for the best builds' : 'Fix the validation errors first'}>
          ▶ Run optimizer
        </Button>
      )}
    </div>
  );
}
