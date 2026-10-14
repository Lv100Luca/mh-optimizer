#!/usr/bin/env python3
"""Sharpness over time for Long Sword attack sequences (prototype of the model in notes/sharpness_model.md).

The game counts sharpness in hits per color (a weapon's bar is e.g. white 20 = 20 hits at cost 1), stores it in tenths
(EquipDataSetting _SharpnessRatio 10) and takes a per-hit cost from the attack data (the 1.040 motion value sheet's
Sharpness column: 1 for most hits, 0.5 / 0.3 for multi-hit moves, 0 for Helm Breaker hits). Razor Sharp (10/25/50 %)
and Master's Touch (80 % on crits) skip a hit's cost; Protective Polish stops all loss for 30/60/90 s after sharpening;
Handicraft adds 10 hits per level to the top color, never past the weapon's maximum (400 for the Gogma weapons).

    python tools/sharpness_sim.py                      # tables + SVG charts for the Crimson loop on Headsman's Hamus
    python tools/sharpness_sim.py --sequence iai-crimson --affinity 0.9 --bonus 50 --out notes/sharpness

Everything here is relative: damage is motion value x sharpness modifier (raw; --element-share adds the element part),
so the results are factors against "always white, never sharpening", not real numbers.
"""
from __future__ import annotations

import argparse
import os
import random
from dataclasses import dataclass, replace

COLORS = ["red", "orange", "yellow", "green", "blue", "white", "purple"]
RAW = dict(red=0.50, orange=0.75, yellow=1.00, green=1.05, blue=1.20, white=1.32, purple=1.39)
ELE = dict(red=0.25, orange=0.50, yellow=0.75, green=1.00, blue=1.0625, white=1.15, purple=1.25)
GAUGE_MAX = 400          # mhdb docs: a bar never exceeds 400 hits; every Gogma weapon is 350 + Handicraft [5]
HANDICRAFT_PER_LEVEL = 10
MASTERS_TOUCH = 0.80
RAZOR_SHARP = {0: 0.0, 1: 0.10, 2: 0.25, 3: 0.50}
POLISH_SECONDS = {0: 0, 1: 30, 2: 60, 3: 90}
GRINDS = 3               # PlayerItemParam _Toishi_SharpenCount (4 on the Seikret); Speed Sharpening removes 1 / 2

# Gogma Long Sword (Headsman's Hamus), hits per color from data/gogma_weapons_base.json
HAMUS_ATTACK = dict(red=140, orange=40, yellow=40, green=80, blue=30, white=20, purple=0)
HAMUS_AFFINITY = dict(red=140, orange=40, yellow=40, green=50, blue=70, white=10, purple=0)


@dataclass(frozen=True)
class Hit:
    name: str
    mv: float
    cost: float = 1.0        # sharpness hits consumed by this hit (sheet column)
    element: float = 1.0     # element modifier of the hit
    count: int = 1
    spirit: float = 1.0      # Spirit Gauge raw multiplier the hit always uses (red-only moves x1.1)


@dataclass(frozen=True)
class Attack:
    id: str
    name: str
    seconds: float
    hits: tuple[Hit, ...]

    @property
    def hit_count(self) -> int:
        return sum(h.count for h in self.hits)

    @property
    def cost(self) -> float:
        return sum(h.cost * h.count for h in self.hits)

    @property
    def mv(self) -> float:
        return sum(h.mv * h.spirit * h.count for h in self.hits)


LS: dict[str, Attack] = {}


def _a(id: str, name: str, seconds: float, *hits: Hit) -> None:
    LS[id] = Attack(id, name, seconds, hits)


# Long Sword, 1.040 sheet (notes/motion_values.md); seconds are the rough timings of src/.../Damage/Attacks.cs
_a("ls-overhead-slash", "Overhead Slash", 0.9, Hit("Overhead Slash I", 24))
_a("ls-thrust", "Thrust", 0.8, Hit("Thrust", 18))
_a("ls-rising-slash", "Rising Slash", 0.8, Hit("Rising Slash", 18))
_a("ls-fade-slash", "Fade Slash", 1.2, Hit("Fade Slash", 22))
_a("crimson-slash-i", "Crimson Slash I", 1.1, Hit("Crimson Slash I 1", 33, 0.5, 0.5, spirit=1.1), Hit("Crimson Slash I 2", 16, 0.5, 0.5, spirit=1.1))
_a("crimson-slash-ii", "Crimson Slash II", 1.0, Hit("Crimson Slash II 1", 11, 0.5, 1.05, spirit=1.1), Hit("Crimson Slash II 2", 22, 0.5, 1.05, spirit=1.1))
_a("crimson-slash-iii", "Crimson Slash III", 1.6, Hit("Crimson Slash III 1", 30, 0.3, 1.1, spirit=1.1), Hit("Crimson Slash III 2", 18, 0.3, 1.1, spirit=1.1),
   Hit("Crimson Slash III 3", 55, 0.3, 1.1, spirit=1.1))
_a("spirit-blade-i", "Spirit Blade I", 0.9, Hit("Spirit Blade I", 31))
_a("spirit-blade-ii", "Spirit Blade II", 0.9, Hit("Spirit Blade II", 30))
_a("spirit-blade-iii", "Spirit Blade III", 1.4, Hit("Spirit Blade III 1", 14, 0.3), Hit("Spirit Blade III 2", 19, 0.3), Hit("Spirit Blade III 3", 34, 0.3))
_a("spirit-roundslash-red", "Spirit Roundslash (red)", 1.5, Hit("Spirit Roundslash (Red)", 42, spirit=1.1))
_a("spinning-crimson-slash", "Spinning Crimson Slash", 1.5, Hit("Spinning Crimson Slash", 60, spirit=1.1))
_a("iai-slash", "Iai Slash", 1.2, Hit("Iai Slash 1", 18, 0.5), Hit("Iai Slash 2", 13, 0.5))
_a("iai-spirit-slash-red", "Iai Spirit Slash (red, countered)", 2.0, Hit("Iai Spirit Slash 1 (Red)", 39, 1.0, spirit=1.1), Hit("Iai Spirit Slash 2 (Red)", 86, 0.0, spirit=1.1))
_a("foresight-slash", "Foresight Slash into Whirl Slash (countered)", 2.5, Hit("Foresight Slash (Success)", 23.4, 1.0),
   Hit("Foresight Whirl Slash 1 (Success)", 28.6, 0.5), Hit("Foresight Whirl Slash 2 (Success)", 16.9, 0.5))
_a("crimson-rising-loop", "Crimson Slash I into Rising Slash", 1.9, *LS["crimson-slash-i"].hits, Hit("Rising Slash", 18))
_a("crimson-spirit-loop", "Crimson Slash I into Spirit Blade I", 2.0, *LS["crimson-slash-i"].hits, Hit("Spirit Blade I", 31))
_a("red-spirit-combo", "Spirit Blade combo into Spinning Crimson Slash", 6.0, *LS["spirit-blade-i"].hits, *LS["spirit-blade-ii"].hits,
   *LS["spirit-blade-iii"].hits, *LS["spirit-roundslash-red"].hits, *LS["spinning-crimson-slash"].hits)
_a("helm-breaker-release", "Spirit Thrust, Helm Breaker and Release Slash", 7.0, Hit("Spirit Thrust", 19),
   Hit("Spirit Helm Breaker (Red)", 23, 0.0, 0.2, count=7, spirit=1.04),  # sheet: 0 per hit (kuroyonhon: hits 2-7 are 0)
   Hit("Spirit Release Slash 1", 5, 0.3, count=2, spirit=1.04), Hit("Spirit Release Slash 2", 10, 0.0, 0.1, count=3, spirit=1.02),
   Hit("Spirit Release Slash 3", 15, 0.0, 0.1, count=3, spirit=1.02), Hit("Spirit Release Slash 4", 22, 0.0, 0.1, count=3, spirit=1.02),
   Hit("Spirit Release Slash 5", 35, 0.0, 0.1, count=3, spirit=1.02))

SEQUENCES: dict[str, list[tuple[str, int]]] = {
    "crimson": [("crimson-rising-loop", 1)],
    "crimson-spirit": [("crimson-spirit-loop", 1)],
    "iai-crimson": [("iai-spirit-slash-red", 1), ("crimson-rising-loop", 4)],   # the app's Long Sword preset
    "rotation": [("iai-spirit-slash-red", 1), ("crimson-rising-loop", 3), ("foresight-slash", 1), ("crimson-rising-loop", 3), ("helm-breaker-release", 1)],
    "spirit-combo": [("red-spirit-combo", 1)],
}


@dataclass(frozen=True)
class Skills:
    handicraft: int = 0
    razor_sharp: int = 0
    masters_touch: bool = False
    polish: int = 0
    speed_sharpening: int = 0
    affinity: float = 0.0      # 0..1 crit chance of the build (clamped), for Master's Touch

    def label(self) -> str:
        parts = []
        if self.handicraft: parts.append(f"Handicraft {self.handicraft}")
        if self.razor_sharp: parts.append(f"Razor Sharp {self.razor_sharp}")
        if self.masters_touch: parts.append(f"Master's Touch @{self.affinity:.0%}")
        if self.polish: parts.append(f"Protective Polish {self.polish}")
        if self.speed_sharpening: parts.append(f"Speed Sharpening {self.speed_sharpening}")
        return ", ".join(parts) or "no sharpness skill"

    @property
    def loss_chance(self) -> float:
        """Chance a hit costs its sharpness: Razor Sharp and Master's Touch roll independently (assumption)."""
        crit = min(max(self.affinity, 0.0), 1.0)
        return (1 - RAZOR_SHARP[self.razor_sharp]) * (1 - (crit * MASTERS_TOUCH if self.masters_touch else 0.0))


def bar_with(base: dict[str, int], handicraft: int, bonus: int, cap: int | None = GAUGE_MAX) -> dict[str, int]:
    """
    A sharpness reinforcement shifts the bar: its hits leave the red segment and join the top color (the bar keeps its
    length; MH encyclopedia wiki on Artian weapons). Handicraft then adds 10 hits per level to the top color, never past the cap.
    """
    bar = dict(base)
    top = max((c for c in COLORS if bar.get(c, 0) > 0), key=COLORS.index)
    shift = min(bonus, bar["red"])
    bar["red"] -= shift
    bar[top] += shift
    extra = HANDICRAFT_PER_LEVEL * handicraft
    if cap is not None:
        extra = max(0, min(extra, cap - sum(bar.values())))
    bar[top] += extra
    return bar


def lower_bounds(bar: dict[str, int]) -> dict[str, float]:
    acc, out = 0.0, {}
    for c in COLORS:
        out[c] = acc
        acc += bar.get(c, 0)
    return out


def color_of(s: float, bounds: dict[str, float], bar: dict[str, int]) -> str:
    """The color of a gauge at s hits: the segment that still has hits left (s = 330 with white above it is blue)."""
    for c in reversed(COLORS):
        if bar.get(c, 0) > 0 and s > bounds[c]:
            return c
    return "red"


def expand(seq: list[tuple[str, int]]) -> list[Attack]:
    return [LS[a] for a, n in seq for _ in range(n)]


@dataclass
class Result:
    skills: Skills
    bar: dict[str, int]
    factor: float                 # damage over the hunt / damage at the top color with no sharpening
    sharpens: int
    first_drop: float | None      # seconds until the policy first called for a sharpen
    time_in: dict[str, float]     # seconds per color and "sharpening"
    curve: list[tuple[float, float]]
    cum: list[tuple[float, float]]
    hunt: float

    def share(self, c: str) -> float:
        return self.time_in.get(c, 0.0) / self.hunt

    def cum_at(self, t: float) -> float:
        pts = self.cum
        if t <= 0: return 0.0
        for (t0, d0), (t1, d1) in zip(pts, pts[1:]):
            if t0 <= t <= t1:
                return d0 if t1 == t0 else d0 + (d1 - d0) * (t - t0) / (t1 - t0)
        return pts[-1][1]


def simulate(seq: list[tuple[str, int]], skills: Skills, base_bar: dict[str, int], bonus: int = 0, hunt: float = 1800.0,
             sharpen_below: str = "white", sharpen_seconds: float = 3.5, element_share: float = 0.0, cap: int | None = GAUGE_MAX,
             rng: random.Random | None = None, stop_at_first_drop: bool = False) -> Result:
    """Expected-value simulation (rng None) or one random run of a hunt that attacks in a loop and sharpens by policy."""
    bar = bar_with(base_bar, skills.handicraft, bonus, cap)
    bounds = lower_bounds(bar)
    top = color_of(sum(bar.values()), bounds, bar)
    full = float(sum(bar.values()))
    attacks = expand(seq)
    mult = lambda c: (1 - element_share) * RAW[c] + element_share * ELE[c]
    ideal_rate = sum(a.mv for a in attacks) * mult(top) / sum(a.seconds for a in attacks)
    grinds = max(1, GRINDS - skills.speed_sharpening)
    t_sharpen = sharpen_seconds * grinds / GRINDS
    polish = POLISH_SECONDS[skills.polish]
    p_loss = skills.loss_chance
    threshold = COLORS.index(sharpen_below)

    s, t, dmg = full, 0.0, 0.0
    polish_until = float(polish)          # sharpened right before the hunt starts
    time_in: dict[str, float] = {}
    sharpens, first_drop, i = 0, None, 0
    curve, cum = [(0.0, s)], [(0.0, 0.0)]
    while t < hunt:
        a = attacks[i % len(attacks)]
        i += 1
        n, k = a.hit_count, 0
        for h in a.hits:
            for _ in range(h.count):
                th = t + a.seconds * (k + 0.5) / n
                k += 1
                c = color_of(s, bounds, bar)
                dmg += h.mv * h.spirit * mult(c)
                time_in[c] = time_in.get(c, 0.0) + a.seconds / n
                if th >= polish_until and h.cost > 0:
                    if rng is None:
                        s -= h.cost * p_loss
                    else:
                        protected = rng.random() < RAZOR_SHARP[skills.razor_sharp] or (
                            skills.masters_touch and rng.random() < min(max(skills.affinity, 0.0), 1.0) and rng.random() < MASTERS_TOUCH)
                        if not protected:
                            s -= h.cost
                    s = max(s, 0.0)
        t += a.seconds
        curve.append((t, s)); cum.append((t, dmg))
        if COLORS.index(color_of(s, bounds, bar)) < threshold:
            if first_drop is None:
                first_drop = t
                if stop_at_first_drop:
                    break
            sharpens += 1
            t += t_sharpen
            time_in["sharpening"] = time_in.get("sharpening", 0.0) + t_sharpen
            s = full
            polish_until = t + polish
            curve.append((t, s)); cum.append((t, dmg))
    return Result(skills, bar, dmg / (ideal_rate * t), sharpens, first_drop, time_in, curve, cum, t)


# ---------------------------------------------------------------- hit-based view: no sharpening, x = hits landed
@dataclass
class HitResult:
    skills: Skills
    bar: dict[str, int]
    s: list[float]            # sharpness left after n hits (index 0 = fresh)
    cum: list[float]          # cumulative damage after n hits
    colors: list[str]         # color each hit was dealt at

    def white_hits(self) -> int:
        return sum(1 for c in self.colors if c == "white")


def simulate_hits(seq: list[tuple[str, int]], skills: Skills, base_bar: dict[str, int], bonus: int = 0, hits: int = 300,
                  element_share: float = 0.0, cap: int | None = GAUGE_MAX, rng: random.Random | None = None) -> HitResult:
    """Land `hits` hits of the looped sequence on a fresh bar and never sharpen (expected value, or one random run)."""
    bar = bar_with(base_bar, skills.handicraft, bonus, cap)
    bounds = lower_bounds(bar)
    hit_list = [h for a in expand(seq) for h in a.hits for _ in range(h.count)]
    mult = lambda c: (1 - element_share) * RAW[c] + element_share * ELE[c]
    p_loss = skills.loss_chance
    s, dmg = float(sum(bar.values())), 0.0
    S, C, cols = [s], [0.0], []
    for n in range(hits):
        h = hit_list[n % len(hit_list)]
        c = color_of(s, bounds, bar)
        dmg += h.mv * h.spirit * mult(c)
        if h.cost > 0:
            if rng is None:
                s -= h.cost * p_loss
            elif not (rng.random() < RAZOR_SHARP[skills.razor_sharp] or (
                    skills.masters_touch and rng.random() < min(max(skills.affinity, 0.0), 1.0) and rng.random() < MASTERS_TOUCH)):
                s -= h.cost
            s = max(s, 0.0)
        S.append(s); C.append(dmg); cols.append(c)
    return HitResult(skills, bar, S, C, cols)


def overtakes(a: list[float], b: list[float], gain: float = 0.0) -> int | None:
    """First hit count after which a's cumulative damage stays above b's x (1 + gain) to the end; None if never."""
    first = None
    for n in range(1, min(len(a), len(b))):
        if a[n] > b[n] * (1 + gain) + 1e-9:
            first = n if first is None else first
        else:
            first = None
    return first


def critical_boost_gain(affinity: float, level: int = 3) -> float:
    boost = {1: 1.28, 2: 1.31, 3: 1.34, 4: 1.37, 5: 1.40}[level]
    a = min(max(affinity, 0.0), 1.0)
    return (1 + a * (boost - 1)) / (1 + a * 0.25) - 1


def hits_report(args, base: dict[str, int], seq: list[tuple[str, int]], cap: int | None) -> None:
    attacks = expand(seq)
    per_cycle_hits = sum(a.hit_count for a in attacks)
    per_cycle_cost = sum(a.cost for a in attacks)
    configs = [Skills(), Skills(handicraft=3), Skills(razor_sharp=3),
               Skills(masters_touch=True, affinity=0.5), Skills(masters_touch=True, affinity=0.75), Skills(masters_touch=True, affinity=1.0)]
    common = dict(base_bar=base, bonus=args.bonus, hits=args.hits, element_share=args.element_share, cap=cap)
    rs = [simulate_hits(seq, s, **common) for s in configs]
    base_r = rs[0]
    ab3 = 7 / args.true_raw
    refs = [("Attack Boost 3", ab3)] + [(f"Critical Boost 3 at {a:.0%}", critical_boost_gain(a)) for a in (0.5, 0.75, 1.0)]
    print(f"Hit-based view, no sharpening: `{args.sequence}` = {', '.join(a.name for a in attacks)} "
          f"({per_cycle_hits} hits and {per_cycle_cost:g} sharpness per cycle); bar {fmt_bar(base_r.bar)} (+{args.bonus} reinforcement)")
    print(f"- reference slot values: " + ", ".join(f"{n} {g:+.1%}" for n, g in refs) + f" (true raw {args.true_raw:g} for Attack Boost)\n")
    marks = [60, 150, 300] if args.hits >= 300 else [args.hits // 4, args.hits // 2, args.hits]
    print("| One jewel | Bar | Hits at white | " + " | ".join(f"Damage vs no jewel at {m} hits" for m in marks) + " | "
          + " | ".join(f"Beats {n} after" for n, _ in refs) + " |")
    print("|---|---|---|" + "---|" * (len(marks) + len(refs)))
    for r in rs:
        cells = [f"{r.cum[m] / base_r.cum[m] - 1:+.1%}" for m in marks]
        beats = []
        for _, g in refs:
            n = overtakes(r.cum, base_r.cum, g)
            beats.append("never" if n is None else f"{n} hits")
        print(f"| {r.skills.label()} | {fmt_bar(r.bar)} | {r.white_hits()} | " + " | ".join(cells) + " | " + " | ".join(beats) + " |")
    print()
    print("Which jewel is ahead: hit count after which the row stays ahead of the column (never = the column stays ahead)")
    names = [r.skills.label() for r in rs[1:]]
    print("| | " + " | ".join(names) + " |")
    print("|---|" + "---|" * len(names))
    for a in rs[1:]:
        cells = []
        for b in rs[1:]:
            if a is b:
                cells.append("-")
            else:
                n = overtakes(a.cum, b.cum)
                cells.append("never" if n is None else ("from the start" if n == 1 else f"{n} hits"))
        print(f"| {a.skills.label()} | " + " | ".join(cells) + " |")
    print()
    if args.out:
        bounds = lower_bounds(base_r.bar)
        roll = {0: "no sharpness roll", 30: "one sharpness roll", 50: "an EX sharpness roll", 60: "two sharpness rolls"}.get(args.bonus, f"+{args.bonus} sharpness")
        top = next(c for c in reversed(COLORS) if base_r.bar.get(c, 0) > 0)
        loop = f"{attacks[0].name} loop" if len(attacks) == 1 else f"{args.sequence} rotation"
        subtitle = (f"Headsman's Hamus with {roll} ({top} {base_r.bar[top]}) · {loop}, red gauge up, {per_cycle_hits} hits per cycle · "
                    f"no sharpening, expected values")
        names = {"no sharpness skill": "No jewel"}
        label = lambda r: names.get(r.skills.label(), r.skills.label().replace("Master's Touch @", "Master's Touch at ").replace("%", " %"))
        bands = [(bounds[c], bounds[c] + base_r.bar[c], f"{c} gauge") for c in COLORS if base_r.bar.get(c, 0) > 0]
        line_chart(os.path.join(args.out, f"hits_sharpness_{args.sequence}_bonus{args.bonus}.svg"),
                   "Sharpness left on the bar, by hits landed",
                   [(label(r), list(enumerate(r.s)), series_color(r.skills, i)) for i, r in enumerate(rs)],
                   f"hits landed  ({per_cycle_hits} per cycle)", "sharpness left (hits)", subtitle=subtitle, xmax=args.hits,
                   ymin=max(0.0, min(min(r.s) for r in rs) - 10), ymax=max(sum(r.bar.values()) for r in rs) + 4, bands=bands,
                   footnote="One jewel each: Handicraft Jwl III [3], Rzr Sharp Jwl III [3], Mastery Jewel [3]. Hits cost 0.5 / 0.5 / 1 in the Crimson loop; "
                            "Razor Sharp skips 50 %, Master's Touch 80 % of critical hits.")
        gains = [(label(r), [(n, r.cum[n] / base_r.cum[n] - 1) for n in range(1, args.hits + 1)], series_color(r.skills, i + 1)) for i, r in enumerate(rs[1:])]
        pct = lambda v: ("0 %" if abs(v) < 1e-9 else f"{v:+.0%}".replace("%", " %"))
        line_chart(os.path.join(args.out, f"hits_ratio_{args.sequence}_bonus{args.bonus}.svg"),
                   "Damage gain from one sharpness jewel, by hits landed", gains,
                   f"hits landed  ({per_cycle_hits} per cycle)", "cumulative damage vs no jewel", subtitle=subtitle, xmax=args.hits,
                   ymin=0.0, ymax=max(g for _, pts, _ in gains for _, g in pts) + 0.03,
                   thresholds=[(g, f"{n} instead ({g:+.1%})".replace("%)", " %)")) for n, g in refs],
                   y_format=pct, end_value=lambda v: f"{v:+.1%}".replace("%", " %"),
                   footnote="Reference lines: the 3-slot spent on Attack Boost 3 (+7 attack at 250 true raw) or Critical Boost 3 ((1 + 0.34 a) / (1 + 0.25 a) at affinity a) instead.")
        print(f"Charts written to {args.out}")


# ---------------------------------------------------------------- SVG charts (no dependencies)
# Chrome and ink of the dataviz reference palette (light surface); series hues are its categorical slots, and the three
# Master's Touch affinities are one ordered blue ramp (light to dark = 50 / 75 / 100 %). Text never wears a series color.
INK = dict(surface="#fcfcfb", primary="#0b0b0b", secondary="#52514e", muted="#898781", grid="#e1e0d9", axis="#c3c2b7", band="#f1f0ec")
CATEGORICAL = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"]
BLUE_RAMP = {0.5: "#86b6ef", 0.75: "#2a78d6", 1.0: "#104281"}
FONT = 'system-ui, -apple-system, &quot;Segoe UI&quot;, Roboto, sans-serif'


def series_color(skills: Skills, index: int) -> str:
    """Hue by entity: no jewel gray, Handicraft orange, Razor Sharp aqua, Master's Touch the blue ramp by affinity."""
    if skills == Skills():
        return INK["muted"]
    if skills.masters_touch and not (skills.handicraft or skills.razor_sharp or skills.polish):
        return BLUE_RAMP[min(BLUE_RAMP, key=lambda a: abs(a - skills.affinity))]
    if skills.handicraft and not (skills.razor_sharp or skills.masters_touch):
        return CATEGORICAL[1]
    if skills.razor_sharp and not (skills.handicraft or skills.masters_touch):
        return CATEGORICAL[2]
    if skills.polish and not (skills.handicraft or skills.razor_sharp or skills.masters_touch):
        return CATEGORICAL[6]
    return CATEGORICAL[index % len(CATEGORICAL)]


HALO = f'paint-order="stroke" stroke="{INK["surface"]}" stroke-width="3" stroke-linejoin="round"'


def nice_ticks(lo: float, hi: float, count: int = 5) -> list[float]:
    import math
    span = hi - lo or 1.0
    raw = span / count
    mag = 10 ** math.floor(math.log10(raw))
    step = next(s * mag for s in (1, 2, 2.5, 5, 10) if s * mag >= raw)
    first = math.ceil(lo / step - 1e-9) * step
    ticks, t = [], first
    while t <= hi + 1e-9:
        ticks.append(round(t, 10))
        t += step
    return ticks


def esc(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def text_width(s: str, size: float) -> float:
    return len(s) * size * 0.54


def line_chart(path: str, title: str, series: list[tuple[str, list[tuple[float, float]], str]], xlabel: str, ylabel: str,
               subtitle: str = "", xmax: float | None = None, ymin: float | None = None, ymax: float | None = None,
               thresholds: list[tuple[float, str]] = (), bands: list[tuple[float, float, str]] = (),
               y_format=lambda v: f"{v:g}", x_format=lambda v: f"{v:g}", end_value=None, footnote: str = "",
               width: int = 960, height: int = 540) -> None:
    """
    A clean line chart: title and subtitle, a legend row, hairline horizontal grid, 2 px lines with an end marker and a
    direct label in the right gutter (pushed apart with leader lines when they collide), dashed reference lines labelled at
    the left, optional shaded bands with a name. `series` items are (label, points, color); `end_value(y)` formats the
    value shown after the end label.
    """
    xs = [x for _, pts, _ in series for x, _ in pts]
    ys = [y for _, pts, _ in series for _, y in pts]
    x0, x1 = 0.0, xmax if xmax is not None else max(xs)
    y0 = ymin if ymin is not None else min(ys)
    y1 = ymax if ymax is not None else max(ys)
    if y1 == y0:
        y1 = y0 + 1
    # the right gutter fits the longest end label; the top grows with the legend rows
    end_text = [label + ("  " + end_value(next(y for x, y in reversed(pts) if x <= x1)) if end_value else "") for label, pts, _ in series]
    left, bottom = 64, 70
    right = max(150, int(24 + max(text_width(t, 12) for t in end_text)))
    legend_rows = 1
    if len(series) >= 2:
        lx = left
        for label, _, _ in series:
            w = 26 + text_width(label, 12) + 22
            if lx + w > width - 24:
                lx, legend_rows = left, legend_rows + 1
            lx += w
    top = 104 + 18 * (legend_rows - 1)
    pw, ph = width - left - right, height - top - bottom
    sx = lambda x: left + (x - x0) / (x1 - x0) * pw
    sy = lambda y: top + ph - (y - y0) / (y1 - y0) * ph
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" '
         f'font-family="{FONT}" font-size="12">',
         f'<rect width="{width}" height="{height}" rx="6" fill="{INK["surface"]}"/>',
         f'<text x="{left}" y="34" font-size="18" font-weight="600" fill="{INK["primary"]}">{esc(title)}</text>']
    if subtitle:
        o.append(f'<text x="{left}" y="54" font-size="12.5" fill="{INK["secondary"]}">{esc(subtitle)}</text>')
    # legend row (always, for two or more series)
    if len(series) >= 2:
        lx, ly = left, 80
        for label, _, col in series:
            w = 26 + text_width(label, 12) + 22
            if lx + w > width - 24:
                lx, ly = left, ly + 18
            o.append(f'<line x1="{lx}" y1="{ly - 4}" x2="{lx + 18}" y2="{ly - 4}" stroke="{col}" stroke-width="2.5" stroke-linecap="round"/>')
            o.append(f'<text x="{lx + 26}" y="{ly}" fill="{INK["secondary"]}">{esc(label)}</text>')
            lx += w
    # bands (named zones), drawn first; every other one shaded one step off the surface, the name centered at the left
    for i, (b0, b1, name) in enumerate(bands):
        yb0, yb1 = sy(min(max(b0, y0), y1)), sy(min(max(b1, y0), y1))
        if yb0 - yb1 > 1 and i % 2 == 0:
            o.append(f'<rect x="{left}" y="{yb1:.1f}" width="{pw}" height="{yb0 - yb1:.1f}" fill="{INK["band"]}"/>')
        if yb0 - yb1 > 16:
            o.append(f'<text x="{left + 8}" y="{(yb0 + yb1) / 2 + 4:.1f}" font-size="11" fill="{INK["muted"]}" {HALO}>{esc(name)}</text>')
    # grid, ticks, axes
    for t in nice_ticks(y0, y1):
        if y0 <= t <= y1:
            o.append(f'<line x1="{left}" y1="{sy(t):.1f}" x2="{left + pw}" y2="{sy(t):.1f}" stroke="{INK["grid"]}"/>')
            o.append(f'<text x="{left - 10}" y="{sy(t) + 4:.1f}" text-anchor="end" font-size="11.5" fill="{INK["muted"]}" '
                     f'style="font-variant-numeric: tabular-nums">{esc(y_format(t))}</text>')
    for t in nice_ticks(x0, x1, 6):
        if x0 <= t <= x1:
            o.append(f'<line x1="{sx(t):.1f}" y1="{top + ph}" x2="{sx(t):.1f}" y2="{top + ph + 5}" stroke="{INK["axis"]}"/>')
            o.append(f'<text x="{sx(t):.1f}" y="{top + ph + 20}" text-anchor="middle" font-size="11.5" fill="{INK["muted"]}" '
                     f'style="font-variant-numeric: tabular-nums">{esc(x_format(t))}</text>')
    o.append(f'<line x1="{left}" y1="{top + ph:.1f}" x2="{left + pw}" y2="{top + ph:.1f}" stroke="{INK["axis"]}"/>')
    o.append(f'<text x="{left + pw / 2:.0f}" y="{top + ph + 42}" text-anchor="middle" fill="{INK["secondary"]}">{esc(xlabel)}</text>')
    o.append(f'<text transform="translate(18,{top + ph / 2:.0f}) rotate(-90)" text-anchor="middle" fill="{INK["secondary"]}">{esc(ylabel)}</text>')
    # reference lines (thresholds): dashed hairlines, named on whichever side the curves leave free
    for y, label in thresholds:
        if y0 <= y <= y1:
            yl = sy(y)
            wl = text_width(label, 11) + 16
            crowd = lambda xa, xb: sum(1 for _, pts, _ in series for x, v in pts
                                       if x <= x1 and xa <= sx(x) <= xb and abs(sy(min(max(v, y0), y1)) - (yl - 9)) < 10)
            slots = [left + (pw - wl) * k / 11 for k in range(12)]
            xl = min(slots, key=lambda xa: (crowd(xa, xa + wl), abs(xa - left)))
            o.append(f'<line x1="{left}" y1="{yl:.1f}" x2="{left + pw}" y2="{yl:.1f}" stroke="{INK["muted"]}" stroke-dasharray="4,4"/>')
            o.append(f'<text x="{xl + 8:.1f}" y="{yl - 5:.1f}" font-size="11" fill="{INK["muted"]}" {HALO}>{esc(label)}</text>')
    # lines with end markers
    ends = []
    for label, pts, col in series:
        clipped = [(x, y) for x, y in pts if x <= x1]
        d = " ".join(f"{sx(x):.1f},{sy(min(max(y, y0), y1)):.1f}" for x, y in clipped)
        o.append(f'<polyline points="{d}" fill="none" stroke="{col}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>')
        xe, ye = clipped[-1]
        ends.append((sx(xe), sy(min(max(ye, y0), y1)), label, col, ye))
    # end labels in the gutter, pushed apart when they collide, with leader lines
    gap = 16.0
    order = sorted(range(len(ends)), key=lambda i: ends[i][1])
    pos = [ends[i][1] for i in order]
    for k in range(1, len(pos)):
        pos[k] = max(pos[k], pos[k - 1] + gap)
    overflow = pos[-1] - (top + ph) if pos else 0
    if overflow > 0:
        pos = [p - overflow for p in pos]
        for k in range(len(pos) - 2, -1, -1):
            pos[k] = min(pos[k], pos[k + 1] - gap)
    for k, i in enumerate(order):
        xe, ye, label, col, raw = ends[i]
        yl = pos[k]
        o.append(f'<circle cx="{xe:.1f}" cy="{ye:.1f}" r="4" fill="{col}" stroke="{INK["surface"]}" stroke-width="2"/>')
        if abs(yl - ye) > 3:
            o.append(f'<line x1="{xe + 6:.1f}" y1="{ye:.1f}" x2="{xe + 14:.1f}" y2="{yl:.1f}" stroke="{INK["axis"]}"/>')
        value = f'  <tspan fill="{INK["secondary"]}" style="font-variant-numeric: tabular-nums">{esc(end_value(raw))}</tspan>' if end_value else ""
        o.append(f'<text x="{xe + 18:.1f}" y="{yl + 4:.1f}" fill="{INK["primary"]}">{esc(label)}{value}</text>')
    if footnote:
        o.append(f'<text x="{left}" y="{height - 14}" font-size="11" fill="{INK["muted"]}">{esc(footnote)}</text>')
    o.append("</svg>")
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(o))


# ---------------------------------------------------------------- report
def fmt_bar(bar: dict[str, int]) -> str:
    return " ".join(f"{c[0].upper()}{bar[c]}" for c in COLORS if bar.get(c, 0) > 0)


def crossover(a: Result, b: Result, gain: float, step: float = 1.0) -> float | None:
    """First time the cumulative damage of a beats b's times (1 + gain) and stays ahead to the end of the hunt."""
    t = step
    end = min(a.hunt, b.hunt)
    hit = None
    while t <= end:
        ratio = a.cum_at(t) / max(b.cum_at(t), 1e-9)
        if ratio >= 1 + gain:
            hit = t if hit is None else hit
        else:
            hit = None
        t += step
    return hit


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sequence", default="crimson", choices=sorted(SEQUENCES), help="attack loop (default: crimson)")
    ap.add_argument("--weapon", default="attack", choices=["attack", "affinity"], help="Headsman's Hamus focus variant (bar)")
    ap.add_argument("--bonus", type=int, default=0, help="sharpness reinforcement hits on the weapon: 0, 30 (I), 50 (EX), 60, 80, 100")
    ap.add_argument("--affinity", type=float, default=0.8, help="crit chance of the build for Master's Touch (0..1)")
    ap.add_argument("--hunt", type=float, default=1800, help="hunt length in seconds for the steady-state factors")
    ap.add_argument("--sharpen-below", default="white", choices=COLORS[:6], help="sharpen as soon as the gauge drops below this color")
    ap.add_argument("--sharpen-seconds", type=float, default=3.5, help="a full 3-grind whetstone, seconds (estimate; measure it)")
    ap.add_argument("--element-share", type=float, default=0.0, help="share of the damage that is element at white (0..1)")
    ap.add_argument("--no-cap", action="store_true", help="let Handicraft and reinforcements extend past 400 hits")
    ap.add_argument("--out", default=None, help="folder for the SVG charts (default: no charts)")
    ap.add_argument("--mc", type=int, default=0, help="random runs for the spread of the first drop (0 = off)")
    ap.add_argument("--mode", default="hits", choices=["hits", "time"],
                    help="hits: no sharpening, x = hits landed, one-jewel options (default); time: hunt with a sharpening policy")
    ap.add_argument("--hits", type=int, default=300, help="hits landed in the hit-based view")
    ap.add_argument("--true-raw", type=float, default=250, help="true raw of the build, for the Attack Boost 3 reference (+7)")
    args = ap.parse_args()

    base = HAMUS_ATTACK if args.weapon == "attack" else HAMUS_AFFINITY
    seq = SEQUENCES[args.sequence]
    cap = None if args.no_cap else GAUGE_MAX
    if args.mode == "hits":
        hits_report(args, base, seq, cap)
        return
    common = dict(base_bar=base, bonus=args.bonus, hunt=args.hunt, sharpen_below=args.sharpen_below,
                  sharpen_seconds=args.sharpen_seconds, element_share=args.element_share, cap=cap)
    attacks = expand(seq)
    secs = sum(a.seconds for a in attacks)
    print(f"Sequence `{args.sequence}`: {', '.join(a.name for a in attacks)}")
    print(f"- {sum(a.hit_count for a in attacks)} hits, {sum(a.cost for a in attacks):g} sharpness and {sum(a.mv for a in attacks):g} MV per {secs:g} s "
          f"-> {sum(a.cost for a in attacks) / secs:.2f} sharpness/s unprotected")
    print(f"- weapon: Headsman's Hamus ({args.weapon} focus) {fmt_bar(base)} + {args.bonus} reinforcement; cap {cap}; "
          f"sharpen when below {args.sharpen_below} ({args.sharpen_seconds:g} s for 3 grinds); affinity {args.affinity:.0%} for Master's Touch\n")

    configs = [
        Skills(),
        Skills(handicraft=1), Skills(handicraft=2), Skills(handicraft=3), Skills(handicraft=5),
        Skills(razor_sharp=1), Skills(razor_sharp=2), Skills(razor_sharp=3),
        Skills(masters_touch=True, affinity=args.affinity),
        Skills(polish=1), Skills(polish=2), Skills(polish=3),
        Skills(speed_sharpening=2),
        Skills(razor_sharp=3, handicraft=1),
        Skills(masters_touch=True, affinity=args.affinity, handicraft=3),
        Skills(masters_touch=True, affinity=args.affinity, razor_sharp=3),
    ]
    results = [simulate(seq, s, **common) for s in configs]
    base_r = results[0]
    print("| Skills | Bar | White share | Sharpens / 10 min | First drop (s) | Damage factor | vs no skill | Beats an attack skill worth |")
    print("|---|---|---|---|---|---|---|---|")
    for r in results:
        gain = r.factor / base_r.factor - 1
        print(f"| {r.skills.label()} | {fmt_bar(r.bar)} | {r.share('white'):.0%} | {r.sharpens * 600 / r.hunt:.1f} | "
              f"{r.first_drop if r.first_drop is not None else float('nan'):.0f} | {r.factor:.4f} | {gain:+.2%} | +{gain:.1%} |")
    print()
    print("Crossover: seconds of hunt after which the sharpness skill is ahead of a +g% attack skill (none = never within the hunt)")
    gains = [0.01, 0.02, 0.03, 0.05]
    print("| Skills | " + " | ".join(f"g = {g:.0%}" for g in gains) + " |")
    print("|---|" + "---|" * len(gains))
    for r in results[1:]:
        cells = []
        for g in gains:
            x = crossover(r, base_r, g, step=0.5)
            cells.append("never" if x is None else f"{x:.0f} s")
        print(f"| {r.skills.label()} | " + " | ".join(cells) + " |")
    print()

    if args.mc:
        print(f"Random runs ({args.mc}): seconds until the gauge first drops below {args.sharpen_below} (p10 / median / p90)")
        for s in [Skills(), Skills(razor_sharp=3), Skills(masters_touch=True, affinity=args.affinity), Skills(masters_touch=True, affinity=args.affinity, razor_sharp=3)]:
            rng = random.Random(1)
            drops = sorted(simulate(seq, s, rng=rng, stop_at_first_drop=True, **common).first_drop or 0 for _ in range(args.mc))
            q = lambda p: drops[min(len(drops) - 1, int(p * len(drops)))]
            print(f"- {s.label()}: {q(0.1):.0f} / {q(0.5):.0f} / {q(0.9):.0f} s (expected-value model: {simulate(seq, s, **common).first_drop:.0f} s)")
        print()

    if args.out:
        chart = [Skills(), Skills(handicraft=5), Skills(razor_sharp=3), Skills(masters_touch=True, affinity=args.affinity), Skills(polish=3)]
        rs = [simulate(seq, s, **common) for s in chart]
        bounds = lower_bounds(rs[0].bar)
        subtitle = (f"Headsman's Hamus +{args.bonus} sharpness · {args.sequence} loop · sharpen as soon as the gauge drops below "
                    f"{args.sharpen_below} ({args.sharpen_seconds:g} s) · affinity {args.affinity:.0%} for Master's Touch · expected values")
        bands = [(bounds[c], bounds[c] + rs[0].bar[c], f"{c} gauge") for c in COLORS if rs[0].bar.get(c, 0) > 0]
        line_chart(os.path.join(args.out, f"sharpness_{args.sequence}_bonus{args.bonus}.svg"),
                   "Sharpness over time with sharpening", [(r.skills.label(), r.curve, series_color(r.skills, i)) for i, r in enumerate(rs)],
                   "seconds", "sharpness left (hits)", subtitle=subtitle, xmax=180.0,
                   ymin=max(0.0, bounds["white"] - 60), ymax=max(sum(r.bar.values()) for r in rs) + 4, bands=bands)
        samples = [t * 2.0 for t in range(0, int(min(args.hunt, 900) / 2) + 1)]
        pct = lambda v: ("0 %" if abs(v) < 1e-9 else f"{v:+.0%}".replace("%", " %"))
        gains = [(r.skills.label(), [(t, r.cum_at(t) / max(base_r.cum_at(t), 1e-9) - 1) for t in samples if t > 0], series_color(r.skills, i + 1))
                 for i, r in enumerate(rs[1:])]
        line_chart(os.path.join(args.out, f"ratio_{args.sequence}_bonus{args.bonus}.svg"),
                   "Damage gain from a sharpness skill over a hunt", gains, "seconds of hunt", "cumulative damage vs no skill",
                   subtitle=subtitle, xmax=samples[-1], ymin=-0.02, ymax=0.16,
                   thresholds=[(0.03, "an attack skill worth +3 %"), (0.06, "worth +6 %")], y_format=pct,
                   end_value=lambda v: f"{v:+.1%}".replace("%", " %"))
        print(f"Charts written to {args.out}")


if __name__ == "__main__":
    main()
