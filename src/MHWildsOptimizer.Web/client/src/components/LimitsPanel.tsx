import { useApp } from '../state';
import { Alert, Button, Section } from './common';
import { SkillPicker } from './SkillPicker';

export function LimitsPanel() {
  const { request, patchRequest, resolved } = useApp();
  if (!request) return null;
  const limits = request.conditions.skill_limits ?? {};
  const limitErrors = resolved?.errors.filter((e) => e.startsWith('Skill limit') || e.includes('above its limit')) ?? [];
  const setLimits = (skill_limits: Record<string, number>) => patchRequest((r) => ({ ...r, conditions: { ...r.conditions, skill_limits } }));
  const isGs = (request.weapon.spec?.type ?? request.weapon.type) === 'great-sword';

  return (
    <div className="panel">
      <Section
        title="Skill limits"
        hint="Cap how much the optimizer values a skill. 0 removes it from the optimization (it is never slotted or chased); n values it only up to level n, so higher levels are shown but score nothing."
        actions={
          !('Burst' in limits) && (
            <Button small onClick={() => setLimits({ ...limits, Burst: isGs ? 1 : 0 })} title="Great Sword rarely reaches the five-hit Burst boost">
              + Burst ≤ {isGs ? 1 : 0} (slow weapons)
            </Button>
          )
        }
      >
        {limitErrors.map((e) => <Alert key={e} kind="error">{e}</Alert>)}
        <SkillPicker mode="limit" values={limits} onChange={setLimits} />
      </Section>
    </div>
  );
}
