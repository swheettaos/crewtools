# Crew Tools

**Host tools for Moon Sweepers crew games:** what each crew-mate may do, where and how much, cheat catching, and the
host chores. Only the host needs it; crew-mates install nothing. A BepInEx 6 (IL2CPP) plugin.

**Runs on its own** as a normal BepInEx plugin. **With [LunarLink](https://swheettaos.github.io/lunarlink/) it's hot-loadable and updates itself in place,
with no restart:** see [On its own, or better with LunarLink](#on-its-own-or-better-with-lunarlink).

[Nexus Mods](https://www.nexusmods.com/moonsweepers/mods/149) · [Download the latest release](https://github.com/swheettaos/crewtools/releases/latest) ·
[Docs site](https://swheettaos.github.io/crewtools/) · [Wiki](https://github.com/swheettaos/crewtools/wiki) · [API](https://swheettaos.github.io/crewtools/api.html) ·
[Changelog](CHANGELOG.md) · [Report a problem](https://github.com/swheettaos/crewtools/issues/new/choose)

## On its own, or better with LunarLink

**Crew Tools runs on its own.** It's a normal BepInEx plugin: install it in `BepInEx/plugins/` and everything works.
Nothing else is needed.

**With [LunarLink](https://swheettaos.github.io/lunarlink/) it's hot-loadable and updates itself, in place, while the game runs.** LunarLink is a free
BepInEx plugin ([GitHub](https://github.com/swheettaos/lunarlink)) that loads plugins from `BepInEx/hot/` and reloads them without a restart. Crew Tools is
built for it:

| | On its own | With LunarLink |
|---|---|---|
| **Updating** | Download the new zip, close the game, unzip, start again | F9 → UPDATES → **Update**: downloaded from this repo's releases, checked against GitHub's SHA-256 and reloaded in place, **no restart** |
| **Your crew during an update** | Everyone leaves while you restart | Nobody notices: spending in the last 30 minutes, building money held, a ban reclaim in progress and unanswered cheating popups carry over to the new version |
| **Turning it off** | Remove the DLL and restart | F9 → HOT PLUGINS → **Disable**: unloaded from memory at once; **Enable** brings it back |
| **Scripts and tools** | - | HTTP requests under `/crew/` (players, catches, settings, kick, ban, cleanup), listed in LunarLink's `/help` |
| **An AI assistant** | - | Through LunarLink's [MCP server](https://github.com/swheettaos/lunarlink/wiki/MCP), Crew Tools' requests become tools: ask Claude "who's online, and has anyone been caught?" |
| **Seeing its state** | `BepInEx/LogOutput.log` | F9 shows it running, its build time, and any error |

**To run it with LunarLink:** install [LunarLink](https://github.com/swheettaos/lunarlink/releases/latest), then put `CrewTools.dll` in **`BepInEx/hot/`**
instead of `BepInEx/plugins/CrewTools/` (not in both: LunarLink refuses to load a second copy), start the game and turn
it on in F9 → HOT PLUGINS. From then on, F9 → UPDATES keeps it up to date: press **Check for updates**, or tick
**Check for updates at game start**, and the tab shows an orange count when a new Crew Tools is out.

More about LunarLink: [its site](https://swheettaos.github.io/lunarlink/) · [wiki](https://github.com/swheettaos/lunarlink/wiki) · [how updates work](https://github.com/swheettaos/lunarlink/wiki/Updates) ·
[hot plugins](https://github.com/swheettaos/lunarlink/wiki/Hot-Plugins).

## Credits: built on Crew Permissions by tylerun

The permission switches and the money anti-cheat come from
[Crew Permissions by tylerun](https://www.nexusmods.com/moonsweepers/mods/71), merged in with the author's permission
("Anyone can take and edit my mods. Please provide credit to the original on your page"). Thank you, tylerun!

From Crew Permissions (rebuilt from its released DLL, renamed and commented for Crew Tools; what is checked and refused
is tylerun's design):

| File | What |
|---|---|
| `Guard.cs` | The permission check on every crew message (build, demolish, sell, shop, capsules) and the pricing of what a crew-mate builds or removes |
| `Hooks.cs` | The hooks marked "(Crew Permissions)": which game methods are checked |
| `AntiCheat.cs` | Its money section: building money must match what was really built or removed; impossible amounts, trick money and kilograms are caught; and what a catch gets (all permissions off, then ban, kick or ask the host) |
| `Alerts.cs` | Telling the host about a catch (redesigned for Crew Tools) |
| `Returns.cs` | A refused cargo sale gives the capsule back, beside the player |
| `Players.cs` | The permission switches (`Switch`, `Allowed`); the rest of the player record is Crew Tools' |
| `Node.cs` | The five switches |

Its roles (Visitor, Crew, Trusted) are gone: every player was loaded as a Visitor, so they never applied.

Changed in Crew Tools (marked "Crew Tools:" in the code):

- Vehicle messages always go through (driving, parking, recalling); only buying or upgrading needs Shop.
- Building money right after a refused action is refused quietly instead of called cheating (an honest player was
  banned for a refused demolish's $50 refund).
- The permission check also applies protected bases and the spending cap.
- Its settings moved into the Crew Tools config (`[AntiCheat]`); its old window is replaced by the CREW TOOLS tabs.
- Its player file is merged with Crew Tools' (`CrewTools.players.txt`).

Crew Tools' own: `Plugin.cs`, `Crew.cs`, `Players.cs` (trust, passes, defaults), `Zones.cs`, `Spending.cs`,
`GameLimits.cs`, `Owners.cs`, `Cleanup.cs`, `Welcome.cs`, `ServerName.cs`, `Ui.cs`, `Widgets.cs`, `Http.cs`, `Handover.cs`, and the
cheat watch section of `AntiCheat.cs`.

## Install

Needs **BepInEx 6 (IL2CPP)**, as the game's other mods use.

1. Download `CrewTools-<version>.zip` from [Nexus Mods](https://www.nexusmods.com/moonsweepers/mods/149) or the [latest GitHub release](https://github.com/swheettaos/crewtools/releases/latest).
2. Close the game and unzip it into the game folder, so you get `Moon Sweepers/BepInEx/plugins/CrewTools/CrewTools.dll`.
3. **Remove** the separate `CrewPermissions.dll` from `BepInEx/plugins` if you have it: Crew Tools includes it.
4. Start the game. The log says "Crew Tools 1.2.4 loaded: 30 game hooks in place".

**Coming from Crew Permissions** (or an earlier Crew Tools test build): nothing to do; the first start converts
everything and loses nothing.

- **Players:** whichever of `CrewPermissions.players.txt` (switches) and the old `CrewTools.players.txt` (names, trust,
  passes, defaults) you have, one, both or neither, is read into the new one-line-per-player `CrewTools.players.txt`.
  The old `CrewTools.players.txt` is kept as `CrewTools.players.old.txt`; `CrewPermissions.players.txt` is left as it
  was (no longer read). Players already in either file keep the switches they had; only players never seen before
  start with the new-player defaults.
- **Settings:** Crew Permissions' `moonsweepers.crewpermissions.cfg` and an earlier build's `[Cheat watch]` section are
  read into `[AntiCheat]`. The old files and lines are left in place; nothing reads them after that.
- **Who built what** (`CrewTools.owners.txt`) and the logs are unchanged.

The log says "players: N moved into CrewTools.players.txt from ..." (naming the files it found), or on a fresh
install "players: new CrewTools.players.txt (no earlier player files)".

## What it does (host only)

| | |
|---|---|
| **Permissions** | Per player: Build, Demolish, Sell, Shop, Capsules, each on or off. A refused sale isn't paid: dust goes back into the player's hopper, a cargo capsule is given back. Capsules moved by a tool, brush or vacuum are always allowed. |
| **Game crew limits** | Moon Sweepers 1.0.6 adds its own per-player limits (Build, Drive, Spend: hold Tab, right mouse button). Crew Tools keeps them the same as its switches: Build to the game's Build, Shop to its Spend, both ways. Drive is the game's own. On 1.0.5 there are none, and Crew Tools works as before. |
| **Protected bases** | Restrict only: nobody is banned for it. Crew-mates within N metres of a base's station can look but can't build, demolish, sell, shop or handle capsules. They can use their vehicles there (drive, park, recall), but not buy or upgrade one. The first time they walk into a protected base after joining they're told once in crew chat: "@name: this area (South base) is restricted." Refused actions after that are refused quietly, with no chat spam. You (the host) are never restricted. Map teleports still work. Give a crew-mate a pass for chosen bases with Passes in their player row. |
| **Vehicles** | Crew-mates can always drive, park and recall vehicles they own, also with Shop off, inside a protected base, or over the spending cap. Only buying or upgrading a vehicle needs Shop (and no zone, and room under the cap). |
| **Spending cap** | Each crew-mate may spend at most $X in any 30 minutes (shop, buildings, robots, vehicles), priced before the purchase goes through. Trusted players have no cap. |
| **New players** | A player seen for the first time starts with the new-player switches you choose on the Rules tab (default: build, sell, shop, capsules on; demolish off). Everyone already known keeps their own switches. On the Players tab a switch that differs from the default is marked * (e.g. "off*"); Manage > Reset to defaults sets a player back to them. |
| **Money anti-cheat** | A crew-mate's game sends the money for a build or a delete separately from the build itself. Crew Tools prices what each building message builds and removes (belts at the game's own price for their length, and pieces already gone from what it remembers of the last 5 minutes) and pays building money only when it matches. Money that matches nothing is never paid. A cost (money taken) is flagged in the cheats log, never a ban. A refund with a build or delete from them within 10 s is flagged, and a ban once these pass $20,000 in 10 minutes. A refund with no build or delete at all is cheating. Also cheating: money no build could cost, trick money over the limit, impossible kilograms. A catch: refused, every switch off, then Ban / Kick / Restrict, at once or ask you. |
| **Welcome** | A crew-chat greeting when someone joins, then one line per protected base. |
| **Server name** | Your server name, saved and put on every game you host. |
| **Cleanup** | Every N minutes, loose capsules and bricks lying around are removed, with a crew-chat warning a minute before. Spread over frames so the game doesn't hitch. |
| **Ban reclaim** | Ban someone and everything they built (buildings, robots, belts) and their vehicles is removed with the game's normal refund paid back to the team, a piece at a time, no freeze. |
| **Leaving players** | Keep their vehicles, upgrades and all, for when they come back (a crash or lost connection too). Vehicles are only reclaimed on a ban, matched by Steam id: refunded at the garage price plus every upgrade level bought, at the game's prices. |

## Cheat watch

Catches what cheat trainers (WeMod and the like) do to *your* world: the host can't see the program on someone else's
PC, only what their game sends. A catch is treated like a money cheat: switches off, and your Ban / Kick / Restrict and
At once / Ask me settings apply.

| Check | Catches |
|---|---|
| Speed | Faster than 30 m/s sideways on foot, or 60 m/s in a vehicle, for 2+ seconds |
| Flying | Off the ground (not in a vehicle, not a passenger, not climbing) for 45+ seconds |
| Reach | Building or removing more than 60 m from where they stand (the game's reach is ~30 m) |
| Deposits | Selling or unloading more than 3x the dust their tool or vehicle holds, or selling dust more than 150 m from every station |
| Teleports | A jump of 150 m+ that lands more than 200 m from every station (the game's map only teleports you to a station), and a jump with no teleport payment within 15 s (map teleports cost money and the host takes it). The payment check starts once one paid crew teleport has been seen in the session, so it can't catch honest players if payments ever don't reach the host |

Fast Machines and Freeze Time of Day only change the cheater's own game.

It never counts: anything a permission or a protected base refused, money or deposits within 10 s of a refusal, the
host, or the first 20 s after a player appears. A jump that lands at a station is a map teleport (allowed). The
Anti-cheat on/off switch covers it too. Every check and limit is a setting (`[AntiCheat]`). The limits are generous so
only blatant cheating trips them; they haven't been tried against a real cheater yet.

## In game

Pause menu → **CREW TOOLS** (while on the Moon). One window, five tabs:

| Tab | |
|---|---|
| **Players** | "In game" / "History" (everyone who has played here). One row per player: their five switches Build / Demolish / Sell / Shop / Capsules (on = orange; click to switch), Passes (which protected bases they may work in) and Manage (reset spending, trust, reset to defaults, restrict, kick, ban, or Unban for a banned player, also from History; remove from the list). Nine rows; scroll with the mouse wheel or Up / Down. |
| **Bases** | Each base: PROTECTED / open, and its distance (- / +). |
| **Rules** | Spending limit per 30 minutes; cleanup on/off and how often; ban reclaim and cleanup speed (Slow / Fast); what new players start with. |
| **Anti-cheat** | On/off (money checks + cheat watch); a cheater gets Ban / Kick / Restrict; At once / Ask me; the trick-money limit; cheat watch on/off. |
| **Server** | The server name (Save); the welcome message on/off and its text (Save). |

Every switch shows its state: orange = on, grey = off. In a choice, the lit button is the one in use.

### When someone is caught

- **At once** (AutoBan on): a small notice in the top-right corner, "BANNED: name" and why, gone by itself after 8 s.
- **Ask me:** a card at the top: F7 bans, F8 lets it go (the keys work during play; its buttons work with the pause
  menu open).

## Settings

In `BepInEx/config/swheet.moonsweepers.crewtools.cfg`; the window changes most of these.

| Section | Settings |
|---|---|
| `[AntiCheat]` | Enabled, AutoBan, Punishment (Ban / Kick / None), BanKey (F7), IgnoreKey (F8), SlackDollars, MaxBuildMoney, MaxUnmatchedRefunds (20000), MaxTrickMoney, MaxKilograms; the cheat watch: Watch, Watch speed, Watch flying, Watch reach, Watch deposits, Watch teleports (each true/false) and their limits |
| `[Protected bases]` | North/South/East/West = on/off (default: South on), "<Base> metres" = 150 |
| `[Spending]` | Dollars per 30 minutes = 100000 |
| `[Welcome]` | On, Text (`{name}` = the player, `{server}` = your server name). Crew chat takes 120 characters |
| `[Cleanup]` | On, Every minutes = 30 |
| `[Removal]` | Fast = false |
| `[New players]` | Build, Demolish, Sell, Shop, Capsules = true/false |
| `[Server name]` | Name |
| `[Debug]` | Trace = true: writes `BepInEx/CrewTools.trace.log`, a line for each join, each crew building message (what it built and removed, priced) and its money, and the first calls of some hooks, saved at once so the last lines survive a crash. Started empty each game start. Set false to stop it |

Files in `BepInEx/config`: `CrewTools.players.txt` (one line a player: Steam ID | name | switches on | trusted, hidden |
base passes), `CrewTools.owners.txt` (who built what). Logs in `BepInEx`: `CrewPermissions.cheats.log`,
`CrewPermissions.money.log` (names kept from the original).

## For tools

Crew Tools answers HTTP requests under `/crew/` when a mod that serves HTTP, such as [LunarLink](https://github.com/swheettaos/lunarlink), passes them on to plugins: its plugin class
has a public `string HandleHttp(string method, string path, NameValueCollection query, string body)` and lists the
requests in `string[] HttpHelp()`. Without such a mod nothing calls them. Read the players (switches, trust, passes,
spending), who is in the game now, the catches and held money, and the settings; change a player, kick, ban or unban,
change a setting, or start the cleanup. Each change does exactly what the CREW TOOLS window's button does. Every request with
what it's for, its parameters and an example answer, plus recipes (a moderation watcher, a crew overlay), is on the
wiki's [For Tools](https://github.com/swheettaos/crewtools/wiki/For-Tools) page.

**Updates:** Crew Tools answers `string UpdateFeed()` with `github:swheettaos/crewtools`, so
[LunarLink](https://github.com/swheettaos/lunarlink) (or any mod that updates plugins) can check its latest release here and install it (the zip, checked
against the SHA-256 GitHub publishes), in place when it runs hot.

It can also be reloaded in place by a mod loader that reloads plugins while the game runs, such as [LunarLink](https://github.com/swheettaos/lunarlink): `Tools` (`Plugin.cs`) starts
and stops as one piece and hands what is in flight (each crew-mate's spending in the last 30 minutes, building money
held for its building, a ban reclaim in progress, cheating popups not answered yet) to its next start.

## Notes

- Who built what is only known for pieces built while Crew Tools is installed.
- Crew chat goes to everyone; the game has no private messages.
- If one part of Crew Tools hits an error, only that part stops (the log says which); the rest keeps working.

## Uninstall

Delete `BepInEx/plugins/CrewTools`. Crew-mates are then unrestricted.

## Building

Needs the .NET 8 SDK and the game with BepInEx 6 (IL2CPP); the project references the game's own DLLs.

```bash
./build.sh              # build
./build.sh --install    # game closed: into BepInEx/plugins/CrewTools/
```

`MOON_GAME_DIR` sets the game's folder (default: Steam's on Linux). GitHub builds every push to `main` against a
private copy of the game's DLLs; on a fork that build is skipped, so build locally against your own game.

The [wiki](https://github.com/swheettaos/crewtools/wiki) is built from `wiki/` by `tools/wiki.py` on every push, and the
[docs site](https://swheettaos.github.io/crewtools/) is `docs/`.

**Releasing:** put the version in `src/CrewTools/Plugin.cs` (`Version = "x.y.z"`), add its section to `CHANGELOG.md`,
commit and push, then `tools/release.sh x.y.z`: it checks those, tags `vx.y.z` and pushes the tag, and GitHub builds
the zip and publishes the release with that changelog section.

## License

Crew Tools is licensed under the [GPL-3.0](LICENSE). The parts from Crew Permissions are tylerun's work, used with
their permission and credited above.
