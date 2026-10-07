import { useState } from 'react';
import { NEW_WEAPON, useApp } from '../state';
import { Button } from './common';

const NEW_PROFILE = '\u0000new-profile';
const namePattern = /^[A-Za-z0-9][A-Za-z0-9 _.-]*$/;

export function ConfigBar() {
  const { profiles, profile, openProfile, createProfile, weapons, name, dirty, unsaved, newWeapon, openWeapon, save, revert, remove, runOptimizer, cancelRun, run, resolved } = useApp();
  const [saveAs, setSaveAs] = useState<string | null>(null);

  // switching keeps unsaved edits (they stay with their weapon, also across reloads), so no "discard?" questions here
  const onPickWeapon = (value: string) => {
    if (value === '') { if (name !== null) newWeapon(); return; }
    if (value !== name) void openWeapon(value);
  };

  const onPickProfile = (value: string) => {
    if (value === NEW_PROFILE) {
      const entered = window.prompt('Name of the new profile (an account with its own talismans, presets and weapons):', '')?.trim();
      if (!entered) return;
      if (!namePattern.test(entered)) { window.alert("Use letters, digits, spaces, '-', '_' or '.'."); return; }
      void createProfile(entered);
      return;
    }
    if (value !== profile) void openProfile(value);
  };

  const submitSaveAs = async () => {
    const target = (saveAs ?? '').trim().replace(/\.json$/i, '');
    if (!target) return;
    if (weapons.some((c) => c.name === target) && target !== name && !window.confirm(`Overwrite the existing weapon "${target}"?`)) return;
    if (await save(target)) setSaveAs(null);
  };

  const running = run.status === 'running';
  const canRun = !!resolved?.is_valid && !running;

  return (
    <div className="configbar">
      <label className="configpick">
        <span className="muted">Profile</span>
        <select value={profile ?? ''} onChange={(e) => onPickProfile(e.target.value)}>
          {profiles.map((p) => (
            <option key={p.name} value={p.name} title={`${p.weapons} weapons, ${p.talismans} talismans`}>{p.name}</option>
          ))}
          <option value={NEW_PROFILE}>New profile…</option>
        </select>
      </label>
      <label className="configpick">
        <span className="muted">Weapon</span>
        <select value={name ?? ''} onChange={(e) => onPickWeapon(e.target.value)}>
          <option value="">New weapon{unsaved.includes(NEW_WEAPON) ? ' *' : ''}</option>
          {weapons.map((c) => (
            <option key={c.name} value={c.name} title={c.summary ?? undefined}>
              {c.name}{c.has_results ? ' ✓' : ''}{unsaved.includes(c.name) ? ' *' : ''}
            </option>
          ))}
        </select>
      </label>
      <span className={'configname' + (dirty ? ' dirty' : '')} title={dirty ? 'unsaved changes' : 'saved'}>
        {name ?? 'unsaved'}{dirty ? ' *' : ''}
      </span>

      {saveAs === null ? (
        <>
          <Button onClick={() => void save()} disabled={!name || !dirty} title={name ? `Save ${name}` : 'Use Save as… first'}>Save</Button>
          {dirty && (
            <Button kind="ghost" onClick={() => { if (window.confirm(name ? `Drop the unsaved changes of ${name}?` : 'Start the new weapon over?')) void revert(); }}
              title="Unsaved changes are kept in this browser (also across reloads) until you save or revert them">
              Revert
            </Button>
          )}
          <Button onClick={() => setSaveAs(name ?? 'my-weapon')}>Save as…</Button>
          {name && (
            <Button kind="ghost" onClick={() => { if (window.confirm(`Delete the weapon ${name}, its builds and results?`)) void remove(name); }} title="Delete this weapon">
              Delete
            </Button>
          )}
        </>
      ) : (
        <form className="saveas" onSubmit={(e) => { e.preventDefault(); void submitSaveAs(); }}>
          <span className="muted">{profile}/</span>
          <input autoFocus value={saveAs} onChange={(e) => setSaveAs(e.target.value)} placeholder="weapon name" pattern="[A-Za-z0-9][A-Za-z0-9 _.\-]*" />
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
