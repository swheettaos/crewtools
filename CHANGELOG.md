# Changelog

Each release's section is its notes on the GitHub releases page (`tools/release.sh` tags it; GitHub publishes it).

## 1.2.7

1.2.6 came out just before this, so both are here.

- **Crew Tools only works while you host a crew game.** Playing alone or in someone else's game it does nothing at
  all: no game hooks, no crew-chat messages, no capsule cleanup, no money log. Its hooks go in when you start hosting
  and come out when you stop; the CREW TOOLS window still opens, so you can set things up beforehand.
- **Works with Moon Sweepers 1.0.5 and the 1.0.6 preview** (from 1.2.6). 1.0.6 gives the game its own crew limits,
  Build, Drive and Spend, set per player in its crew menu (hold Tab, then right-click a crew-mate's row). Crew Tools
  keeps them the same as its switches: its Build switch sets the game's Build limit, its Shop switch the game's Spend
  limit, so the game's crew menu, each crew-mate's own game and Crew Tools always agree. Changing Build or Spend in
  the game's crew menu switches the matching Crew Tools switch too. Drive is left to the game's crew menu. On 1.0.5
  nothing changes.
- **Crew Tools still does much more than the game's limits:** separate Demolish, Sell and Capsules switches, limits for
  players who are offline, protected bases with passes, a spending cap, the money anti-cheat, ban reclaim with refunds,
  vehicles kept for players who leave, the capsule cleanup, a welcome message and your saved server name.

## 1.2.6

- **Works with Moon Sweepers 1.0.5 and the 1.0.6 preview.** 1.0.6 gives the game its own crew limits, Build, Drive
  and Spend, set per player in its crew menu (hold Tab, then right-click a crew-mate's row). Crew Tools keeps them the
  same as its switches: its Build switch sets the game's Build limit, its Shop switch the game's Spend limit, so the
  game's crew menu, each crew-mate's own game and Crew Tools always agree. Changing Build or Spend in the game's crew
  menu switches the matching Crew Tools switch too. Drive is left to the game's crew menu. On 1.0.5 nothing changes.
- **Crew Tools still does much more than the game's limits:** separate Demolish, Sell and Capsules switches, limits for
  players who are offline, protected bases with passes, a spending cap, the money anti-cheat, ban reclaim with refunds,
  vehicles kept for players who leave, the capsule cleanup, a welcome message and your saved server name.

## 1.2.5

- **Fixed: the host crashed when a crew-mate's dust sale was refused** (no Sell permission, or a protected base). The
  game already returns refused dust to the player's hopper; Crew Tools dropped a capsule of the same dust as well, and
  the player's game, still at the station, poured it in again: refused, given back, round and round until the host
  froze and crashed. Refused dust now just goes back into the hopper. A refused cargo capsule is still given back.
- **Fixed: after a cheater was caught, nobody could build until a restart**, the host included. A crew message cut short
  by the ban left Crew Tools thinking every later action was the cheater's. It now always finishes the message, and
  clears it at the next frame if anything was ever left over.
- **Fixed: crew-mates lost their vehicle upgrades.** Vehicles were taken back to the garage on every leave (a crash or
  dropped connection too), and matched by display name as well, so same-named players could lose each other's.
  Leaving players now keep their vehicles; only a ban reclaims them, matched by Steam id only.

## 1.2.4

- **The window's rows match Quantum Conveyors' menu:** cream like the window, set apart by their gradient, with dark
  text (1.2.3 drew them dark). Hints and the status line are darker so they read on the cream.

## 1.2.3

- **A cleaner window**, in the style of Quantum Conveyors' menus: rounded panels, rows, buttons and fields; a soft
  gradient on each row (warm for players in the game now); a centred title; and a "?" in the header whose tooltip
  says what each tab is for.

## 1.2.2

- **Updates itself** through a mod that updates plugins: Crew Tools answers `UpdateFeed()` with its GitHub releases, so
  such a mod can offer the newest version and install it (in place, without a restart, when it runs hot).
- The HTTP requests answer in the normal BepInEx build too (its plugin class passes them on); before, only a hot copy
  answered them.

## 1.2.1

- The HTTP requests describe themselves (a public `HttpHelp` method lists them), so a host can show them.
- First release from GitHub, with the source.

## 1.2.0

- **For tools:** Crew Tools answers HTTP requests under `/crew/` when a mod that serves HTTP passes them on to plugins
  (it calls a public `HandleHttp` method; without such a mod nothing calls it). Read the players (switches, trust,
  passes, spending), who is in the game now, the catches and held money, and the settings; change a player, kick, ban
  or unban, change a setting, or start the cleanup. Each change does exactly what the CREW TOOLS window's button does.

## 1.1.0

- Nothing changes for you or your crew. Under the hood, Crew Tools now starts and stops as one piece and hands what is
  in flight (each crew-mate's spending in the last 30 minutes, building money held for its building, a ban reclaim in
  progress, cheating popups not answered yet) to its next start. So a mod loader that reloads plugins while the game
  runs can reload Crew Tools in place, without a game restart and without resetting anyone's spending cap.

## 1.0.2

- A better money check, and the cheat watch without the movement hook.

## 1.0.1

- No ban for building money without a building; Unban; a crash trace.

## 1.0.0

- The first public release: Crew Permissions built in, a tabbed window, the cheat watch.
