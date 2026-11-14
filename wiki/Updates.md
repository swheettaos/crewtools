# Updates

**By hand:** download the [latest release](https://github.com/swheettaos/crewtools/releases/latest) and unzip it over the old files with the game closed.
Your players, settings and logs are kept.

**From the game, with [LunarLink](https://swheettaos.github.io/lunarlink/):** F9 → UPDATES → **Check for updates**, then **Update** on the CrewTools
row. Crew Tools names its releases with a public method LunarLink asks:

```csharp
public string UpdateFeed() => "github:swheettaos/crewtools";
```

LunarLink checks this repo's latest release when you ask, compares the version with yours, and installs the zip after
checking it against the SHA-256 GitHub publishes. Running hot (from `BepInEx/hot/`), the new version reloads in place:
**no restart**, and what's in flight (each crew-mate's spending in the last 30 minutes, building money held, a ban
reclaim, unanswered popups) is handed to the new copy. Installed the normal way, in `BepInEx/plugins/`, it needs a
restart, since BepInEx loads a plugin once. See [[With LunarLink]].

**Releases** are built by GitHub from a version tag (see [[Contributing]]); each release's notes are its section of the
[[Changelog]].
