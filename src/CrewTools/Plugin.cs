using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace CrewTools;

// Crew Tools: host tools for crew games, one plugin, one CREW TOOLS window in the pause menu. Only the host needs it.
//
//   Plugin.cs      settings, start-up and shut-down (Tools), the BepInEx plugin, and the Driver that runs Tools each frame
//   Hooks.cs       every hook into the game, one per game method; each calls the modules below
//   Players.cs     everything kept per crew-mate (switches, trust, passes) in CrewTools.players.txt
//   GameLimits.cs  the game's own crew limits (1.0.6 on) kept the same as the Build and Shop switches
//   Crew.cs        the crew-mates in the game right now
//   Guard.cs       the permission check on every crew message (from Crew Permissions by tylerun)
//   AntiCheat.cs   money checks (tylerun) and the cheat watch (ours); what a catch gets. Alerts.cs: its popup
//   Returns.cs     a refused cargo sale's capsule given back (tylerun)
//   Zones.cs       protected bases          Spending.cs   the 30-minute spending cap
//   Owners.cs      who built what; ban reclaim and vehicles on leaving
//   Cleanup.cs     loose capsules every N minutes, and removals spread over frames (Remover)
//   Welcome.cs     the welcome in crew chat, and crew chat itself (Chat)      ServerName.cs   your server name
//   Ui.cs          the CREW TOOLS window       Widgets.cs   the UI pieces it and the popup are built from
//   Http.cs        answers to HTTP requests (/crew/...) when a host passes them on      Handover.cs   what a reload hands over
[BepInPlugin(Tools.Guid, "Crew Tools", Tools.Version)]
public sealed class Plugin : BasePlugin
{
    internal static BepInEx.Logging.ManualLogSource L;
    internal static readonly string[] Bases = { "North", "South", "East", "West" };
    internal static readonly Dictionary<string, ConfigEntry<bool>> ZoneOn = new();
    internal static readonly Dictionary<string, ConfigEntry<float>> ZoneMetres = new();
    internal static string Hooked = "";
    internal static ConfigEntry<bool> WelcomeOn; internal static ConfigEntry<string> WelcomeText;
    internal static ConfigEntry<float> Cap; internal static ConfigEntry<string> SavedName;
    internal static ConfigEntry<bool> CleanupOn; internal static ConfigEntry<float> CleanupMinutes; internal static ConfigEntry<bool> FastRemoval;
    internal static readonly Dictionary<Node, ConfigEntry<bool>> StartWith = new();     // new players' switches

    internal static Tools Running;

    // The normal BepInEx way in: Tools runs from here, driven every frame by a Driver of our own.
    public override void Load()
    {
        Tools.Config = Config; Tools.Log = Log;
        Running = new Tools(); Running.Start(null);
        AddComponent<Driver>();
    }

    // The same for the BepInEx build: a mod that passes HTTP requests on, or updates plugins, finds these on the
    // BepInPlugin class
    public string HandleHttp(string method, string path, System.Collections.Specialized.NameValueCollection query, string body) => Running?.HandleHttp(method, path, query, body);
    public string[] HttpHelp() => Running?.HttpHelp();
    public string UpdateFeed() => Tools.Feed;
}

// Everything Crew Tools does, started and stopped as a whole. BepInEx starts it through Plugin; a loader that reloads
// plugins in place while the game runs can also use it directly: it creates Tools, calls Start(state) and every frame
// Update() and LateUpdate(), and before dropping it Stop(), whose answer is the next copy's state. Stop() undoes all
// Start() did (game hooks, windows, the popup) and hands over what is in flight: spending in the last 30 minutes,
// building money held for its building, a ban reclaim's queue, cheating popups not yet answered.
public sealed class Tools
{
    internal const string Guid = "swheet.moonsweepers.crewtools", Version = "1.2.6";
    internal static ConfigFile Config;                                    // set by Plugin; otherwise opened here
    internal static BepInEx.Logging.ManualLogSource Log;
    BepInEx.Logging.ManualLogSource ownLog;
    Harmony harmony;

    public void Start(string state)
    {
        if (Log == null) { ownLog = BepInEx.Logging.Logger.CreateLogSource("Crew Tools"); Plugin.L = ownLog; } else Plugin.L = Log;
        var cfg = Config ?? new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, Guid + ".cfg"), true);
        Trace.Start(cfg, fresh: state == null);
        foreach (var b in Plugin.Bases)
        {
            Plugin.ZoneOn[b] = cfg.Bind("Protected bases", b, b == "South", $"Crew-mates can't build, demolish, sell, shop or handle capsules near {b} base.");
            Plugin.ZoneMetres[b] = cfg.Bind("Protected bases", b + " metres", 150f, $"Size of {b} base's protected zone, from its station.");
        }
        Plugin.WelcomeOn = cfg.Bind("Welcome", "On", true, "Greet each crew-mate in crew chat when they join, then tell them which bases are protected.");
        Plugin.WelcomeText = cfg.Bind("Welcome", "Text", "Welcome to the crew, @{name}!", "First line. {name} = the player (optional: {server} = your server name).");
        Plugin.Cap = cfg.Bind("Spending", "Dollars per 30 minutes", 100000f, "Most a crew-mate may spend in any 30 minutes (shop, building - robots included - vehicles). Trusted players have no limit.");
        Plugin.CleanupOn = cfg.Bind("Cleanup", "On", true, "Remove loose capsules and bricks lying on the ground or stuck in the air, with a warning in crew chat a minute before.");
        Plugin.CleanupMinutes = cfg.Bind("Cleanup", "Every minutes", 30f, "How often.");
        foreach (var n in Players.Nodes)
            Plugin.StartWith[n] = cfg.Bind("New players", n.ToString(), n != Node.Demolish, $"New crew-mates start with {n} allowed (set when they are first seen; change it per player on the Players tab after).");
        Plugin.FastRemoval = cfg.Bind("Removal", "Fast", false, "How fast a ban reclaim and the cleanup remove things. Off (slow): one building every 0.25 s and 10 capsules a frame - no visible hitch. On (fast): one every 0.1 s and 20 a frame - about twice as fast, small hitches (~35 ms).");
        Plugin.SavedName = cfg.Bind("Server name", "Name", "", "Your server name; put on every game you host (empty = the game's own \"<Steam name>'s crew\"). Set from CREW TOOLS.");
        AntiCheat.Bind(cfg);
        Players.Load();
        Owners.Load();
        if (state != null) Handover.Load(state);
        harmony = new Harmony(Guid);
        Plugin.Hooked = Idle;
        Plugin.L.LogInfo($"Crew Tools {Version} {(state == null ? "loaded" : "reloaded")}; it works only while you host a crew game");
    }

    // Crew Tools only works while you host a crew game (user, 2026-11-15): playing alone or in someone else's game it
    // has no game hooks in, says nothing in chat and cleans nothing up. The hooks go in when hosting starts and come out
    // when it stops.
    const string Idle = "Not hosting: Crew Tools is idle";
    bool hooked;

    void Hook()
    {
        // every game hook (Hooks.cs); one that can't be applied is logged and skipped, the rest still work
        int n = 0, failed = 0;
        foreach (var type in typeof(Hooks).GetNestedTypes(AccessTools.all))
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
            try { harmony.CreateClassProcessor(type).Patch(); n++; Trace.Log("hooked " + type.Name); }
            catch (Exception e) { failed++; Plugin.L.LogError($"Couldn't hook {type.Name}; that part of Crew Tools is off: {e.Message}"); }
        }
        GameLimits.Start(harmony);
        Cleanup.Restart();
        hooked = true;
        Plugin.Hooked = $"{n} game hooks in place" + (failed > 0 ? $", {failed} failed (see the log)" : "");
        Plugin.L.LogInfo("Hosting: Crew Tools on, " + Plugin.Hooked);
    }

    void Unhook()
    {
        harmony.UnpatchSelf(); hooked = false; Guard.Sender = 0;
        Plugin.Hooked = Idle;
        Plugin.L.LogInfo("Not hosting: Crew Tools idle (game hooks out)");
    }

    public string Stop()
    {
        harmony?.UnpatchSelf(); harmony = null; hooked = false;
        Ui.Stop(); Alerts.Stop(); Widgets.Stop();
        try { Players.Save(); } catch (Exception e) { Plugin.L.LogWarning("saving players: " + e.Message); }
        var state = Handover.Save();
        Plugin.L.LogInfo("Crew Tools stopped");
        if (ownLog != null) { BepInEx.Logging.Logger.Sources.Remove(ownLog); ownLog = null; }
        return state;
    }

    // Runs every module once a frame (each decides for itself how often it really works). Each has its own error catch:
    // a module that throws is switched off until Crew Tools starts again, and the rest carry on. The anti-cheat is never
    // switched off (held building money would never be paid); its errors are logged at most every 10 s.
    readonly (string name, Action run)[] modules =
    {
        ("cheating popup", Alerts.Update), ("capsules given back", Returns.Update), ("welcome", Welcome.Update),
        ("players", Players.Update), ("cleanup", Cleanup.Update), ("removals", Remover.Update), ("protected bases", Zones.Update),
        ("server name", ServerName.Update), ("game limits", GameLimits.Update),
    };
    bool[] off;
    float cheatErrorAt = -10f, uiErrorAt = -10f;

    public void Update()
    {
        try { Ui.Update(); } catch (Exception e) { if (Time.unscaledTime - uiErrorAt > 10f) { uiErrorAt = Time.unscaledTime; Plugin.L.LogWarning("window: " + e.Message); } }
        bool hosting = Crew.Hosting;
        if (hosting != hooked) { if (hosting) Hook(); else Unhook(); }
        if (!hosting) return;
        Guard.Frame();
        try { AntiCheat.Update(); }
        catch (Exception e)
        {
            if (Time.unscaledTime - cheatErrorAt > 10f) { cheatErrorAt = Time.unscaledTime; Plugin.L.LogWarning("anti-cheat: " + e.Message); }
        }
        off ??= new bool[modules.Length];
        for (int i = 0; i < modules.Length; i++)
        {
            if (off[i]) continue;
            try { modules[i].run(); }
            catch (Exception e) { off[i] = true; Plugin.L.LogError($"Crew Tools {modules[i].name} stopped (until Crew Tools starts again): {e}"); }
        }
    }

    public void LateUpdate() { try { Ui.LateUpdate(); } catch { } }

    // HTTP requests a host passes on to plugins (Http.cs): /crew/... answered, anything else is not ours (null)
    public string HandleHttp(string method, string path, System.Collections.Specialized.NameValueCollection query, string body) => Http.Handle(method, path, query);
    public string[] HttpHelp() => Http.Help;

    // Where Crew Tools' releases are, for a mod that updates plugins (it asks UpdateFeed()): the latest GitHub release's
    // zip, checked against the SHA-256 GitHub publishes.
    internal const string Feed = "github:swheettaos/crewtools";
    public string UpdateFeed() => Feed;
}

// The BepInEx build's frame driver: calls Tools every frame. (A loader that drives Tools itself doesn't use it.)
public sealed class Driver : MonoBehaviour
{
    public Driver(IntPtr ptr) : base(ptr) { }
    void Update() => Plugin.Running?.Update();
    void LateUpdate() => Plugin.Running?.LateUpdate();
}
