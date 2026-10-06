import { useMemo } from 'react';
import { useApp } from '../state';
import { Alert, Section, SkillChip, SkillIcon } from './common';
import { SkillPicker } from './SkillPicker';

export function TargetsPanel() {
  const { request, patchRequest, resolved, setTab } = useApp();
  const relevant = useMemo(
    () => new Set([...(resolved?.relevance?.skills ?? []), ...(resolved?.relevance?.set_bonuses ?? []), ...(resolved?.relevance?.group_skills ?? [])]),
    [resolved],
  );
  if (!request) return null;

  const core = resolved?.targets.filter((t) => t.from_core) ?? [];
  const targetErrors = resolved?.errors.filter((e) => e.startsWith('Target')) ?? [];

  return (
    <div className="panel">
      <Section
        title="Target skills"
        hint="Minimum skill levels every reported build must reach. Set bonuses and group skills can be required too, by tier: a set bonus at I needs 2 pieces, at II 4 pieces, a group skill 3 pieces (the Gogma weapon counts when it rolled the bonus). Everything else is filled by the optimizer for damage. Skills the optimizer already values under your conditions are marked."
      >
        {targetErrors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        <SkillPicker mode="target" values={request.target_skills} highlight={relevant} onChange={(target_skills) => patchRequest((r) => ({ ...r, target_skills }))} />
      </Section>

      {core.length > 0 && (
        <Section title="Added automatically" hint="Weapon core skills come from the Options tab (require weapon core skills).">
          <div className="chip-row">
            {core.map((t) => <SkillChip key={t.skill} name={t.skill} level={t.level} />)}
            <button className="linklike" onClick={() => setTab('options')}>change in Options</button>
          </div>
        </Section>
      )}

      {resolved?.relevance && (
        <Section title="What the optimizer values" hint="Under the current conditions these skills, set bonuses and group skills change the score; others only matter if you make them a target. Required set bonuses and group skills are tracked even when they add nothing to the score.">
          <div className="chip-row">
            {resolved.relevance.skills.map((s) => <SkillChip key={s} name={s} muted={!(s in request.target_skills)} />)}
          </div>
          {(resolved.relevance.set_bonuses.length > 0 || resolved.relevance.group_skills.length > 0) && (
            <div className="chip-row">
              {resolved.relevance.set_bonuses.map((s) => <span key={s} className={'chip skill set' + (s in request.target_skills ? '' : ' muted')}><SkillIcon name={s} kind="set" size={18} />{s}</span>)}
              {resolved.relevance.group_skills.map((s) => <span key={s} className={'chip skill group' + (s in request.target_skills ? '' : ' muted')}><SkillIcon name={s} kind="group" size={18} />{s}</span>)}
            </div>
          )}
        </Section>
      )}
    </div>
  );
}
