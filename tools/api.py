#!/usr/bin/env python3
"""Crew Tools' /crew/ requests: the list from src/CrewTools/Http.cs (HttpHelp), with the details in docs/endpoints.toml
(what each does and is for, parameters, an example and an answer). Writes docs/api.html, the searchable API page
(tools/api_template.html with this list in it). tools/wiki.py makes the wiki's For Tools page from the same list
(markdown() below). Warns about a request with no details, and details for a request that's gone.
Run after changing a request or its details:  python3 tools/api.py"""
import json, os, re, sys, tomllib
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
read = lambda p: open(os.path.join(ROOT, p)).read()
GROUPS = ["Players", "In the game", "Settings"]

def requests():
    """Each request from HttpHelp: method, path (without its query), the one-line help, and its details."""
    details = tomllib.loads(read("docs/endpoints.toml"))
    out = []
    for m in re.finditer(r'^\s*"((GET|POST) ([^"|? ]+)[^"|]*?)\s*\|\s*([^"]*)",?\s*$', read("src/CrewTools/Http.cs"), re.M):
        line, method, path, help = m.group(1).strip(), m.group(2), m.group(3), m.group(4).strip()
        e = {"method": method, "path": path, "line": line, "help": help}
        if path in details: e["d"] = details[path]
        out.append(e)
    missing = [e["path"] for e in out if "d" not in e]
    gone = sorted(set(details) - {e["path"] for e in out})
    return out, missing, gone

def groups():
    reqs, _, _ = requests()
    return [{"group": g, "from": "Crew Tools", "endpoints": [e for e in reqs if e.get("d", {}).get("group", "Settings") == g]} for g in GROUPS]

def cell(s): return str(s).replace("|", "\\|").replace("\n", " ")

def markdown():
    """The wiki's For Tools reference: an index, then each request in full."""
    out = ["| Request | What it does |", "|---|---|"]
    for g in groups():
        out.append(f"| **{g['group']}** | |")
        out += [f"| [`{e['method']} {e['path']}`](#{re.sub(r'[^a-z0-9 -]', '', (e['method'] + ' ' + e['path']).lower()).replace(' ', '-')}) | {cell(e['help'])} |" for e in g["endpoints"]]
    for g in groups():
        out += ["", f"## {g['group']}"]
        for e in g["endpoints"]:
            d = e.get("d", {})
            out += ["", f"### `{e['method']} {e['path']}`", "", d.get("about", e["help"])]
            if d.get("use"): out += ["", f"**What it's for:** {d['use']}"]
            if e["method"] == "POST": out += ["", "Changes the game: needs the `allow-writes` file of the mod serving it."]
            if d.get("params"):
                out += ["", "**Parameters**", "", "| Name | Type | Default | Meaning |", "|---|---|---|---|"]
                out += [f"| `{cell(n)}` | {cell(t)} | {cell(df) or '-'} | {cell(mn)} |" for n, t, df, mn in d["params"]]
            if d.get("example"): out += ["", "**Example**", "", "```http", d["example"].strip(), "```"]
            if d.get("answer"): out += ["", "**Answer**", "", "```json", d["answer"].strip(), "```"]
            for k, name in (("errors", "Errors"), ("notes", "Notes")):
                if d.get(k): out += ["", f"**{name}**", ""] + [f"- {x}" for x in d[k]]
    return "\n".join(out)

def main():
    reqs, missing, gone = requests()
    for p in missing: print(f"warning: {p} has no details in docs/endpoints.toml", file=sys.stderr)
    for p in gone: print(f"warning: docs/endpoints.toml has {p}, which Http.cs doesn't list", file=sys.stderr)
    data = {"groups": [{"group": g["group"], "from": g["from"], "endpoints": [{"method": e["method"], "path": e["path"], "help": e["help"], **({"d": e["d"]} if "d" in e else {})} for e in g["endpoints"]]} for g in groups()],
            "note": "served under /crew/ by a mod that passes HTTP requests on to plugins, such as LunarLink (127.0.0.1:8765)"}
    page = read("tools/api_template.html").replace("/*DATA*/null", json.dumps(data, ensure_ascii=False).replace("</", "<\\/"))
    open(os.path.join(ROOT, "docs", "api.html"), "w").write(page)
    print(f"{len(reqs)} requests, {len(reqs) - len(missing)} with details -> docs/api.html")

if __name__ == "__main__":
    main()
