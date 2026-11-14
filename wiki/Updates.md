# Updates

**By hand:** download the [latest release](https://github.com/swheettaos/crewtools/releases/latest) and unzip it over the old files with the game closed.
Your players, settings and logs are kept.

**From the game:** Crew Tools says where its releases are, with a public method a mod that updates plugins can ask:

```csharp
public string UpdateFeed() => "github:swheettaos/crewtools";
```

Such a mod checks this repo's latest release when you ask it to, compares the version with yours, and installs the zip
after checking it against the SHA-256 GitHub publishes. If Crew Tools runs **hot** (from `BepInEx/hot/`, reloaded by a
mod loader), the new version reloads in place: **no restart**, and what's in flight (each crew-mate's spending in the
last 30 minutes, building money held, a ban reclaim, unanswered popups) is handed to the new copy. Installed the normal
way, in `BepInEx/plugins/`, it needs a restart, since BepInEx loads a plugin once.

**Releases** are built by GitHub from a version tag (see [[Contributing]]); each release's notes are its section of the
[[Changelog]].
