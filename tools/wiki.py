#!/usr/bin/env python3
"""Builds the wiki from wiki/ into an output folder (default _wiki/): every page as it is, with each
<!-- include: ... --> marker filled in, so the wiki never holds a stale copy of something kept elsewhere:
  <!-- include: endpoints -->      the /crew/ requests, from HttpHelp in src/CrewTools/Http.cs
  <!-- include: CHANGELOG.md -->   the changelog
.github/workflows/wiki.yml runs this on every push to main and publishes the result to the GitHub wiki.
Run it yourself to look:  python3 tools/wiki.py [out]"""
import os, re, shutil, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "_wiki"))
read = lambda p: open(os.path.join(ROOT, p)).read()

def endpoints():
    rows = []
    for m in re.finditer(r'^\s*"((?:GET|POST) [^"|]+?)\s*\|\s*([^"]*)",?\s*$', read("src/CrewTools/Http.cs"), re.M):
        rows.append(f"| `{m.group(1).strip()}` | {m.group(2).strip().replace('|', chr(92) + '|')} |")
    if not rows: raise SystemExit("no endpoints found in Http.cs")
    return "| Request | What it does |\n|---|---|\n" + "\n".join(rows)

def include(m):
    what = m.group(1).strip()
    if what == "endpoints": return endpoints()
    if what == "CHANGELOG.md": return read("CHANGELOG.md").replace("(`tools/release.sh` tags it; GitHub publishes it)", "").rstrip()
    raise SystemExit(f"unknown include: {what}")

if os.path.isdir(OUT):
    for f in os.listdir(OUT):
        if f != ".git": (shutil.rmtree if os.path.isdir(os.path.join(OUT, f)) else os.remove)(os.path.join(OUT, f))
os.makedirs(OUT, exist_ok=True)
pages = 0
for f in sorted(os.listdir(os.path.join(ROOT, "wiki"))):
    if not f.endswith(".md"): continue
    open(os.path.join(OUT, f), "w").write(re.sub(r"<!-- include: (.+?) -->", include, read(os.path.join("wiki", f))))
    pages += 1
print(f"{pages} pages -> {OUT}")
