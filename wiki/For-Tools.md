# For Tools

Crew Tools answers HTTP requests under `/crew/` when a mod that serves HTTP, such as [LunarLink](https://swheettaos.github.io/lunarlink/), passes requests on to plugins: its plugin
class has a public `string HandleHttp(string method, string path, NameValueCollection query, string body)` and lists
its requests in `string[] HttpHelp()`. Without such a mod nothing calls them. Reads are GET; changes are POST, and
each change does exactly what the CREW TOOLS window's button does.

The searchable version of this page is the [API page](https://swheettaos.github.io/crewtools/api.html).

Through [LunarLink](https://swheettaos.github.io/lunarlink/) these answer on `http://127.0.0.1:8765/`, and a `POST` (a
change) needs its `allow-writes` file. `id` is a player's Steam ID, as `/crew/players` lists it (as text). Every answer
is JSON; a refused change answers `{"ok": false, "reason": "..."}`.

```bash
A=127.0.0.1:8765
```

<!-- include: api -->

## Recipes

**A moderation watcher.** Every few seconds, `GET /crew/catches`; when `popups` has one that isn't `decided`, look at
`what` (the reason, as the popup shows it: a money catch like "sent trick money of $5,000", or a cheat watch catch
like "cheat watch: moved at 48 m/s on foot (most 30)") and either ban (`POST /crew/players/remove?id=&ban=1`) or restrict (`POST /crew/players/modify?id=&restrict=1`)
and look later.

```python
import json, time, urllib.request
A = "http://127.0.0.1:8765"
get = lambda p: json.load(urllib.request.urlopen(A + p))
post = lambda p: json.load(urllib.request.urlopen(urllib.request.Request(A + p, data=b"", method="POST")))
while True:
    for c in get("/crew/catches?lines=0")["popups"]:
        if not c["decided"] and c["what"].startswith("cheat watch: moved at"):     # a speed catch
            print("restricting", c["name"], post(f"/crew/players/modify?id={c['id']}&restrict=1"))
    time.sleep(5)
```

**A crew overlay.** `GET /crew/now` for who's on and where, `GET /crew/players` for each one's `left` of the cap: show
"Crewmate: $87,500 left, in South".

**A build party in a protected base.** `POST /crew/settings/modify?zone=South&on=0` to open it, or give each guest
`pass=South&passOn=1`; raise the cap with `cap=500000`; put everything back after.

**Before saving.** `POST /crew/cleanup`, wait the minute, then save.

It can also be reloaded in place by a mod loader that reloads plugins while the game runs, such as LunarLink ([[With LunarLink]]): `Tools` (`Plugin.cs`)
starts and stops as one piece and hands what is in flight (each crew-mate's spending in the last 30 minutes, building
money held for its building, a ban reclaim in progress, cheating popups not answered yet) to its next start.
