import { useEffect, useRef, useState } from 'react';
import { useApp } from '../state';
import { Alert, Button, Section, SkillIcon, fmt } from './common';
import { BuildCard } from './BuildCard';

export function ResultsPanel() {
  const { run, runOptimizer, cancelRun, resolved, name } = useApp();
  const [showLog, setShowLog] = useState(false);
  const [now, setNow] = useState(Date.now());
  const logRef = useRef<HTMLPreElement>(null);

  useEffect(() => {
    if (run.status !== 'running') return;
    const t = window.setInterval(() => setNow(Date.now()), 500);
    return () => window.clearInterval(t);
  }, [run.status]);

  useEffect(() => {
    if (logRef.current) logRef.current.scrollTop = logRef.current.scrollHeight;
  }, [run.log.length]);

  const elapsed = run.startedAt ? ((run.finishedAt ?? now) - run.startedAt) / 1000 : null;
  const result = run.result;

  const download = () => {
    if (!result) return;
    const blob = new Blob([result.text], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name ?? 'request'}.results.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const logBlock = run.log.length > 0 && (
    <div className="runlog-wrap">
      <button className="linklike" onClick={() => setShowLog((s) => !s)}>{showLog ? 'Hide' : 'Show'} search log ({run.log.length} lines)</button>
      {(showLog || run.status === 'running') && <pre className="runlog" ref={logRef}>{run.log.join('\n')}</pre>}
    </div>
  );

  if (run.status === 'idle' && !result) {
    return (
      <div className="panel">
        <Section title="Results" hint="Run the optimizer to search for the best builds that reach your targets.">
          <div className="empty">
            <Button kind="primary" onClick={() => void runOptimizer()} disabled={!resolved?.is_valid}>▶ Run optimizer</Button>
            {!resolved?.is_valid && <p className="muted">Fix the validation errors in Review first.</p>}
          </div>
        </Section>
      </div>
    );
  }

  return (
    <div className="panel">
      <Section
        title={run.status === 'running' ? 'Searching…' : 'Results'}
        hint={
          run.status === 'running' ? `${elapsed?.toFixed(1)} s elapsed · talisman first, then one armor slot at a time; partial builds with the same skill state are merged.`
          : result ? `${result.pairs.length} skill pair${result.pairs.length === 1 ? '' : 's'}, ${result.pairs.reduce((n, p) => n + p.builds.length, 0)} builds · search took ${result.elapsed_seconds.toFixed(1)} s · ${new Date(result.completed_at).toLocaleString()}${run.fromDisk && name ? ` · loaded from inputs/${name}.results.json` : ''}`
          : undefined
        }
        actions={
          <div className="btn-row">
            {run.status === 'running' ? <Button kind="danger" onClick={cancelRun}>Cancel</Button> : <Button kind="primary" onClick={() => void runOptimizer()} disabled={!resolved?.is_valid}>▶ Run again</Button>}
            {result && <Button onClick={download} title="Download the plain-text report">Download .txt</Button>}
            {result && <Button kind="ghost" onClick={() => void navigator.clipboard.writeText(result.text)} title="Copy the plain-text report">Copy</Button>}
          </div>
        }
      >
        {run.status === 'running' && <div className="progress"><span className="spinner" /> {run.log[run.log.length - 1] ?? 'starting…'}</div>}
        {run.status === 'error' && <Alert kind="error">{run.error}</Alert>}
        {run.status === 'cancelled' && <Alert kind="warning">Run cancelled after {elapsed?.toFixed(1)} s.</Alert>}
        {logBlock}
      </Section>

      {result && result.skill_pair_mode === 'optimize' && result.pairs.length > 1 && (
        <Section title="Skill pair ranking" hint="Best score each rollable pair class can reach. '(any other)' means the set bonus or group skill does not matter for the score.">
          <table className="table pairs">
            <thead><tr><th>#</th><th>Set bonus</th><th>Group skill</th><th className="num">Best</th><th className="num">vs #1</th><th className="num">Builds</th><th className="num">States</th></tr></thead>
            <tbody>
              {result.pairs.map((p) => (
                <tr key={p.rank} onClick={() => document.getElementById(`pair-${p.rank}`)?.scrollIntoView({ behavior: 'smooth' })}>
                  <td>{p.rank}</td>
                  <td><span className="with-icon"><SkillIcon name={p.set_bonus} kind="set" size={18} />{p.set_bonus}</span></td>
                  <td><span className="with-icon"><SkillIcon name={p.group_skill} kind="group" size={18} />{p.group_skill}</span></td>
                  <td className="num"><b>{fmt(p.best_score)}</b></td>
                  <td className="num muted">{p.rank === 1 ? '—' : fmt(p.best_score - result.pairs[0].best_score)}</td>
                  <td className="num">{p.builds.length}</td>
                  <td className="num muted">{p.states_evaluated.toLocaleString('en-US')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Section>
      )}

      {result?.pairs.map((p) => (
        <div key={p.rank} id={`pair-${p.rank}`} className="pair-block">
          <div className="pairhead">
            <span className="rank">#{p.rank}</span>
            <span className="with-icon"><SkillIcon name={p.set_bonus} kind="set" size={20} /><b>{p.set_bonus}</b></span>
            <span className="muted">+</span>
            <span className="with-icon"><SkillIcon name={p.group_skill} kind="group" size={20} /><b>{p.group_skill}</b></span>
            <span className="muted">best {fmt(p.best_score)} · {p.states_evaluated.toLocaleString('en-US')} final states scored · {p.candidate_summary}</span>
          </div>
          {p.builds.length === 0 && <Alert kind="warning">No build satisfies the targets for this pair.</Alert>}
          {p.builds.map((b) => <BuildCard key={b.rank} build={b} best={p.rank === 1 && b.rank === 1} />)}
        </div>
      ))}
    </div>
  );
}
