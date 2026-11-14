# Changelog

Each release's section is its notes on the GitHub releases page (`tools/release.sh` tags it; GitHub publishes it).

## Unreleased

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
