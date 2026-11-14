# Troubleshooting

| What you see | What it means |
|---|---|
| No CREW TOOLS in the pause menu | It didn't load: look for "Crew Tools" in `BepInEx/LogOutput.log`. It shows once you're on the Moon |
| The log says a part stopped | If one part hits an error, only that part stops (the log names it); the rest keeps working. Report it with the lines around it |
| An honest crew-mate was flagged or banned | Look at `CrewPermissions.cheats.log` and `CrewPermissions.money.log` for that time, and `CrewTools.trace.log` if Trace is on. Unban them (Manage > Unban), and report it: the money checks are meant never to catch an honest build |
| A crew-mate can't do something they should | Check their five switches, the protected bases (they may be inside one: give them a Pass), and the spending cap (Manage shows what's left) |
| Two copies running | `CrewPermissions.dll` is still in `BepInEx/plugins`: remove it, Crew Tools includes it |

Crew chat goes to everyone: the game has no private messages, so warnings are public. Report anything else with the
bug form on the [issues page](https://github.com/swheettaos/crewtools/issues/new/choose).
