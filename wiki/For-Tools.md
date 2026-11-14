# For Tools

Crew Tools answers HTTP requests under `/crew/` when a mod that serves HTTP, such as [LunarLink](https://swheettaos.github.io/lunarlink/), passes requests on to plugins: its plugin
class has a public `string HandleHttp(string method, string path, NameValueCollection query, string body)` and lists
its requests in `string[] HttpHelp()`. Without such a mod nothing calls them. Reads are GET; changes are POST, and
each change does exactly what the CREW TOOLS window's button does.

<!-- include: endpoints -->

Through [LunarLink](https://swheettaos.github.io/lunarlink/) these answer on `http://127.0.0.1:8765/`, and a `POST` (a
change) needs its `allow-writes` file. `id` is a player's Steam ID, as `/crew/players` lists it (as text). Every answer
is JSON; a refused change answers `{"ok": false, "reason": "..."}`.

```bash
A=127.0.0.1:8765
```

## Each request

### `GET /crew/players`

Everyone Crew Tools knows: online or not, banned, trusted, their five switches, their base passes, and how much of the
30-minute cap they've spent.

**What it's for:** the roster behind any tool: a host dashboard, a script that finds who's close to the cap, or the
`id` you need for every change below.

```json
{"cap": 100000, "players": [{"id": "76561190000000001", "name": "Crewmate", "online": true, "trusted": false, "banned": false,
  "switches": {"build": true, "demolish": false, "sell": true, "shop": true, "capsules": true},
  "passes": ["North"], "spent": 12500, "left": 87500}]}
```

### `GET /crew/now`

Who's in the game right now, where they are, and the protected base they're standing in (if any).

**What it's for:** a live overlay or map of your crew, or a script that reacts when someone walks into a base.

```json
{"hosting": true, "players": [{"id": "76561190000000001", "name": "Crewmate", "pos": [12.4, 1993.1, -5.2], "protectedBase": "South"}]}
```

### `GET /crew/catches?lines=40`

Cheating popups waiting for your answer, building money held for a building that hasn't come yet, and the last lines
of `CrewPermissions.cheats.log`.

**What it's for:** a moderation tool: see catches without opening the game's window, and decide on them from a script
or a phone. `lines` (default 40) is how much of the log to include.

```json
{"popups": [{"id": "76561190000000002", "name": "Speedy", "what": "cheat watch: moved at 48 m/s on foot (most 30)", "count": 1, "decided": false}],
 "held": [{"id": "76561190000000001", "name": "Crewmate", "dollars": 50}],
 "log": ["... Speedy (76561190000000002): cheat watch: moved at 48 m/s on foot (most 30) ..."]}
```

### `GET /crew/settings`

The protected bases and their sizes, the spending cap, the cleanup, the welcome message, the server name, and what new
players start with.

**What it's for:** read the current rules before changing them, or show them on a dashboard.

```json
{"zones": {"North": {"on": false, "metres": 150}, "South": {"on": true, "metres": 150}, "East": {"on": false, "metres": 150}, "West": {"on": false, "metres": 150}},
 "cap": 100000, "cleanup": true, "cleanupMinutes": 30, "welcome": true, "welcomeText": "Welcome to {server}, @{name}!",
 "name": "My Crew", "newPlayers": {"build": true, "demolish": false, "sell": true, "shop": true, "capsules": true}}
```

### `POST /crew/players/modify?id=`

Change a player, exactly as the Players tab's buttons do. Any of, in one request:

| Parameter | Does |
|---|---|
| `build`, `demolish`, `sell`, `shop`, `capsules` `=0\|1` | Switch one permission off or on |
| `trust=0\|1` | Trusted players have no spending cap |
| `pass=<Base>&passOn=0\|1` | Let them work in a protected base (North, South, East or West), or take it back |
| `resetSpending=1` | Give them their full 30-minute cap again |
| `restrict=1` | Every switch off |
| `defaults=1` | Back to the new-player switches |

**What it's for:** a moderation script (restrict someone the moment they're caught), giving a friend a pass for an
event, or resetting a builder's cap after a big planned build.

```bash
curl -s -X POST "$A/crew/players/modify?id=76561190000000001&pass=South&passOn=1&trust=1"
```

### `POST /crew/players/remove?id=[&ban=1]`

Kick a player (they can rejoin), or with `ban=1` ban them: everything they built is removed and refunded to the team, a
piece at a time, and their vehicles refunded with every upgrade.

**What it's for:** acting on a catch from a tool, or a script that bans on a confirmed cheat. Only while you host, and
only a player in the game now.

### `POST /crew/players/unban?id=`

Unban: they can join again.

**What it's for:** undoing a ban from a tool, for example after a false catch you've checked in the logs.

### `POST /crew/settings/modify`

Change a rule, as the Bases, Rules and Server tabs do. Any of:

| Parameter | Does |
|---|---|
| `zone=<Base>&on=0\|1` | Protect a base, or open it |
| `zone=<Base>&metres=N` | Its radius round the station |
| `cap=N` | The spending cap per 30 minutes |
| `name=` | Your server name |
| `cleanup=0\|1`, `cleanupMinutes=N` | The loose-capsule cleanup, and how often |

**What it's for:** switching rules by time or event: open a base for a build party and protect it again after, raise
the cap for a planned build, rename the server for the evening.

```bash
curl -s -X POST "$A/crew/settings/modify?zone=North&on=1&metres=200"
```

### `POST /crew/cleanup`

Start the loose-capsule cleanup in one minute (its warning goes to crew chat now).

**What it's for:** tidying the world before you save or before an event, without waiting for the timer. The cleanup
must be on (`cleanup=1`).

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
