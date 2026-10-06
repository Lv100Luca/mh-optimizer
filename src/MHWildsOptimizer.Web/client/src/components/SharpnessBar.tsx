import { sharpnessCss } from '../icons';
import type { SharpnessBar as Bar } from '../types';

const order = ['red', 'orange', 'yellow', 'green', 'blue', 'white', 'purple'] as const;

/** The weapon's sharpness gauge; the reinforcement bonus is shown as text because the model only uses the top color. */
export function SharpnessBar({ bar, bonus }: { bar: Bar; bonus?: number }) {
  const total = order.reduce((n, c) => n + bar[c], 0) || 1;
  return (
    <span className="sharpbar-wrap" title={order.filter((c) => bar[c] > 0).map((c) => `${c} ${bar[c]}`).join(', ')}>
      <span className="sharpbar">
        {order.map((c) => bar[c] > 0 && <span key={c} className="seg" style={{ width: `${(bar[c] / total) * 100}%`, background: sharpnessCss[c] }} />)}
      </span>
      {bonus ? <span className="muted small"> +{bonus} from reinforcements</span> : null}
    </span>
  );
}
