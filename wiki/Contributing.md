# Contributing

## Reporting a problem

Use the [issue forms](https://github.com/swheettaos/crewtools/issues/new/choose): bug, feature or question. For a ban or a flag that looks wrong, include
the matching lines of `CrewPermissions.cheats.log` and `CrewPermissions.money.log` (remove Steam IDs you'd rather not
share).

## Building

Needs the .NET 8 SDK and the game with BepInEx 6 (IL2CPP); the project references the game's own DLLs.

```bash
./build.sh              # build: src/CrewTools/bin/Release/net6.0/CrewTools.dll
./build.sh --install    # game closed: into BepInEx/plugins/CrewTools/
./build.sh --hot        # game running, with a mod loader that reloads plugins from BepInEx/hot/
```

`MOON_GAME_DIR` sets the game's folder (default: Steam's on Linux).

## Sending a change

Fork, branch, pull request against `main`; the owner reviews and merges. GitHub builds every push to `main` against a
private copy of the game's DLLs; on a fork, and on a pull request from one, that build is skipped (with a note), so
build and test locally against your own game.

## Changing a /crew/ request

Its one-line description is in `HttpHelp` (`src/CrewTools/Http.cs`); its details (what it's for, parameters, an
example and an answer) are in `docs/endpoints.toml`. Then run `python3 tools/api.py`: it rewrites `docs/api.html` (the
searchable API page) and warns about a request without details. The wiki's For Tools page is made from the same files.

## Releasing

Put the version in `src/CrewTools/Plugin.cs` (`Version = "x.y.z"`), add its section to `CHANGELOG.md`, commit and
push, then `tools/release.sh x.y.z`: it checks those, tags `vx.y.z` and pushes the tag; GitHub builds the zip and
publishes the release with that changelog section. `tools/release.sh x.y.z --dry-run` builds the zip into `dist/`
instead.

After a game or BepInEx update, `tools/refs.sh` refreshes the private copy of the game's DLLs GitHub builds against.

## License

[GPL-3.0](https://github.com/swheettaos/crewtools/blob/main/LICENSE). Contributions are accepted under the same license. The parts from Crew Permissions are
tylerun's, used with permission ([[Credits]]).
