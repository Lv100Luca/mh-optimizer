# Download sizes of a published build: brotli files where the publish made them, gzip -9 estimates for the bundled solver.
# python sizes.py <wwwroot>
import gzip
import os
import sys

root = sys.argv[1]
groups = {}
for dirpath, _, files in os.walk(root):
    for f in files:
        if f.endswith((".br", ".gz", ".map")):
            continue
        path = os.path.join(dirpath, f)
        rel = os.path.relpath(path, root).replace(os.sep, "/")
        group = "bench data" if rel.startswith("bench/") else "or-tools-wasm (JSPI build)" if rel.startswith("ortools/") and "asyncify" not in rel \
            else "or-tools-wasm (asyncify fallback)" if rel.startswith("ortools/") else ".NET runtime + app" if rel.startswith("_framework/") else "other"
        raw = os.path.getsize(path)
        br = path + ".br"
        compressed = os.path.getsize(br) if os.path.exists(br) else len(gzip.compress(open(path, "rb").read(), 9))
        g = groups.setdefault(group, [0, 0, 0])
        g[0] += raw; g[1] += compressed; g[2] += 1
for name, (raw, comp, n) in sorted(groups.items()):
    print(f"{name:36} {n:4} files  {raw / 1048576:7.1f} MB raw  {comp / 1048576:6.1f} MB compressed")
