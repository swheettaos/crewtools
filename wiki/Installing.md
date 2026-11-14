# Installing

Needs **BepInEx 6 (IL2CPP)**, as the game's other mods use. Only the host installs it. Two ways:

- **On its own** (below): a normal BepInEx plugin in `BepInEx/plugins/CrewTools/`.
- **With [LunarLink](https://swheettaos.github.io/lunarlink/)**: in `BepInEx/hot/`, so it reloads and updates in place without restarts. See
  [[With LunarLink]].

1. Download `CrewTools-<version>.zip` from [Nexus Mods](https://www.nexusmods.com/moonsweepers/mods/149) or the [latest GitHub release](https://github.com/swheettaos/crewtools/releases/latest).
2. Close the game and unzip it into the game folder, so you get `Moon Sweepers/BepInEx/plugins/CrewTools/CrewTools.dll`.
3. **Remove** a separate `CrewPermissions.dll` from `BepInEx/plugins` if you have it: Crew Tools includes it.
4. Start the game. The log (`BepInEx/LogOutput.log`) says "Crew Tools x.y.z loaded: 30 game hooks in place".

## Coming from Crew Permissions

Nothing to do; the first start converts everything and loses nothing:

- **Players:** whichever of `CrewPermissions.players.txt` (switches) and an older `CrewTools.players.txt` you have is
  read into the one-line-per-player `CrewTools.players.txt`. The older Crew Tools file is kept as
  `CrewTools.players.old.txt`; `CrewPermissions.players.txt` is left as it was. Players already known keep their
  switches; only players never seen before start with the new-player defaults.
- **Settings:** Crew Permissions' `moonsweepers.crewpermissions.cfg` and an earlier build's `[Cheat watch]` section are
  read into `[AntiCheat]`. The old files are left in place; nothing reads them after that.
- **Who built what** (`CrewTools.owners.txt`) and the logs are unchanged.

## Updating

Unzip the new release over the old files with the game closed; your players and settings are kept. Or let a mod that
updates plugins do it: Crew Tools names its releases (see [[Updates]]).

## Uninstall

Delete `BepInEx/plugins/CrewTools`. Crew-mates are then unrestricted.
