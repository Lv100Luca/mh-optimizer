import { useApp, type Tab } from './state';
import { icons } from './icons';
import { ConfigBar } from './components/ConfigBar';
import { WeaponPanel } from './components/WeaponPanel';
import { SkillPairPanel } from './components/SkillPairPanel';
import { TargetsPanel } from './components/TargetsPanel';
import { LimitsPanel } from './components/LimitsPanel';
import { ConditionsPanel } from './components/ConditionsPanel';
import { TalismansPanel } from './components/TalismansPanel';
import { OptionsPanel } from './components/OptionsPanel';
import { ReviewPanel } from './components/ReviewPanel';
import { ResultsPanel } from './components/ResultsPanel';

interface NavItem { id: Tab; label: string; icon: string; badge?: string | number; tone?: 'ok' | 'warn' | 'err' }

export function App() {
  const app = useApp();
  const { catalog, request, resolved, run, tab, setTab, loadError, notice } = app;

  if (loadError) {
    return (
      <div className="boot boot-error">
        <h1>MH Wilds Optimizer</h1>
        <p>The API could not be reached: {loadError}</p>
        <p>Start the server with <code>dotnet run --project src/MHWildsOptimizer.Web</code> and reload.</p>
      </div>
    );
  }
  if (!catalog || !request) return <div className="boot">Loading game data…</div>;

  const conditionsOn = catalog.conditions.filter((c) => request.conditions[c.key] === true).length;
  const limits = Object.keys(request.conditions.skill_limits ?? {}).length;
  const errors = resolved?.errors.length ?? 0;
  const warnings = resolved?.warnings.length ?? 0;
  const weaponKind = request.weapon.spec?.type ?? request.weapon.type ?? 'great-sword';

  const nav: NavItem[] = [
    { id: 'weapon', label: 'Weapon', icon: icons.weapon(weaponKind) },
    { id: 'pair', label: 'Skill pair', icon: icons.skill('set'), badge: request.skill_pair.mode === 'fixed' ? 'fixed' : 'optimize' },
    { id: 'targets', label: 'Target skills', icon: icons.skill('offense'), badge: Object.keys(request.target_skills).length || undefined },
    { id: 'limits', label: 'Skill limits', icon: icons.skill('utility'), badge: limits || undefined },
    { id: 'conditions', label: 'Conditions', icon: icons.skill('attack'), badge: `${conditionsOn} on` },
    { id: 'talismans', label: 'Talismans', icon: icons.talisman(7), badge: app.talismans.length || undefined },
    { id: 'options', label: 'Options', icon: icons.defense },
    { id: 'review', label: 'Review', icon: icons.skill('item'), badge: errors ? `${errors} err` : warnings ? `${warnings} warn` : 'ok', tone: errors ? 'err' : warnings ? 'warn' : 'ok' },
    { id: 'results', label: 'Results', icon: icons.affinity, badge: run.status === 'running' ? '…' : run.result ? run.result.pairs.reduce((n, p) => n + p.builds.length, 0) : undefined },
  ];

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <img src={icons.weapon('great-sword')} alt="" />
          <div>
            <div className="brand-title">MH Wilds Optimizer</div>
            <div className="brand-sub">Gogma set maker · Ver {catalog.game_version}</div>
          </div>
        </div>
        <ConfigBar />
      </header>

      <nav className="sidenav" aria-label="Sections">
        {nav.map((item) => (
          <button key={item.id} className={'navitem' + (tab === item.id ? ' active' : '')} onClick={() => setTab(item.id)}>
            <img src={item.icon} alt="" />
            <span className="navlabel">{item.label}</span>
            {item.badge !== undefined && <span className={'navbadge' + (item.tone ? ' ' + item.tone : '')}>{item.badge}</span>}
          </button>
        ))}
      </nav>

      <main className="content">
        {tab === 'weapon' && <WeaponPanel />}
        {tab === 'pair' && <SkillPairPanel />}
        {tab === 'targets' && <TargetsPanel />}
        {tab === 'limits' && <LimitsPanel />}
        {tab === 'conditions' && <ConditionsPanel />}
        {tab === 'talismans' && <TalismansPanel />}
        {tab === 'options' && <OptionsPanel />}
        {tab === 'review' && <ReviewPanel />}
        {tab === 'results' && <ResultsPanel />}
      </main>

      <footer className="statusbar">
        {resolved === null ? (
          <span className="muted">Validating…</span>
        ) : errors ? (
          <button className="linklike err" onClick={() => setTab('review')}>{errors} error{errors > 1 ? 's' : ''} · {resolved.errors[0]}</button>
        ) : warnings ? (
          <button className="linklike warn" onClick={() => setTab('review')}>{warnings} warning{warnings > 1 ? 's' : ''} · {resolved.warnings[0]}</button>
        ) : (
          <span className="ok">Request is valid{resolved.baseline ? ` · weapon-only baseline ${resolved.baseline.total.toFixed(1)}` : ''}</span>
        )}
        <span className="muted right">
          {catalog.skills.length} skills · {catalog.decorations.length} decorations · {catalog.armor_sets.length} armor sets · data wilds.mhdb.io, icons monsterhunterwiki.org
        </span>
      </footer>

      {notice && <div className={'toast ' + notice.kind}>{notice.text}</div>}
    </div>
  );
}
