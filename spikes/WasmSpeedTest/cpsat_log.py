# Summarizes CP-SAT search logs (log_search_progress:true): subsolver lineup, who found solutions / bounds, and when.
# python cpsat_log.py <log>...
import re
import sys


def section(lines, title):
    out, on = [], False
    for line in lines:
        if line.startswith(title):
            on = True
        elif on and not line.strip():
            break
        if on:
            out.append(line.rstrip())
    return out


for path in sys.argv[1:]:
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    print(f"===== {path}")
    for i, line in enumerate(lines):
        if line.startswith("Starting search at"):
            print("\n".join(l.rstrip() for l in lines[i:i + 4]))
    sols = [l for l in lines if re.match(r"#\d+\s", l)]
    bounds = [l for l in lines if l.startswith("#Bound")]
    done = [l for l in lines if l.startswith("#Done") or l.startswith("#Model") and "done" in l]
    if sols:
        print("last solution:", sols[-1][:110])
    if bounds:
        print("last bound:   ", bounds[-1][:110])
    print("\n".join(section(lines, "Objective bounds")))
    print("\n".join(section(lines, "Solutions found per subsolver")[:12]))
    print(lines[-1] if lines else "")
