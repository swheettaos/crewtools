# Settings

In `BepInEx/config/swheet.moonsweepers.crewtools.cfg`; the window changes most of these.

| Section | Settings |
|---|---|
| `[AntiCheat]` | Enabled, AutoBan, Punishment (Ban / Kick / None), BanKey (F7), IgnoreKey (F8), SlackDollars, MaxBuildMoney, MaxUnmatchedRefunds (20000), MaxTrickMoney, MaxKilograms; the cheat watch: Watch, Watch speed, Watch flying, Watch reach, Watch deposits, Watch teleports (each true/false) and their limits |
| `[Protected bases]` | North/South/East/West = on/off (default: South on), "<Base> metres" = 150 |
| `[Spending]` | Dollars per 30 minutes = 100000 |
| `[Welcome]` | On, Text (`{name}` = the player, `{server}` = your server name) |
| `[Cleanup]` | On, Every minutes = 30 |
| `[Removal]` | Fast = false |
| `[New players]` | Build, Demolish, Sell, Shop, Capsules = true/false |
| `[Server name]` | Name |
| `[Debug]` | Trace = true: writes `BepInEx/CrewTools.trace.log` (each join, each crew building message priced, and the first calls of some hooks), saved at once so the last lines survive a crash; started empty each game start |

**Files** in `BepInEx/config`: `CrewTools.players.txt` (one line a player: Steam ID | name | switches on | trusted,
hidden | base passes) and `CrewTools.owners.txt` (who built what).
**Logs** in `BepInEx`: `CrewPermissions.cheats.log`, `CrewPermissions.money.log` (names kept from the original) and,
with Trace on, `CrewTools.trace.log`.
