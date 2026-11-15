// GameLimits: the game's own crew limits (Moon Sweepers 1.0.6 and later), kept the same as Crew Tools' switches.
//
// From 1.0.6 the game has its own per-player limits: Build, Drive and Spend, set by the host in the crew menu (hold Tab,
// right mouse button), saved in its own file and published to the lobby so each crew-mate's game refuses those actions
// itself. Crew Tools stays in charge: its Build switch sets the game's Build limit, its Shop switch the game's Spend
// limit, and the game's Drive limit is left as the host set it (Crew Tools has no driving switch). When the host
// changes Build or Spend in the game's crew menu, the matching Crew Tools switch follows.
//
// Crew Tools is built against the 1.0.5 game, which has none of this, so everything here is found by name when Crew
// Tools starts. On 1.0.5 nothing is found and this does nothing.
using System;
using System.Reflection;
using HarmonyLib;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

internal static class GameLimits
{
    // the game's Limit flags (MoonCrewLimits.Limit)
    const int Build = 1, Spend = 4;

    static Type limitType;
    static MethodInfo hostOff, set, lobbySet;
    static bool dirty, applying;
    static float nextLook;

    internal static bool Present => set != null;

    // Finds the game's crew limits and follows changes made in its crew menu. Called once when Crew Tools starts.
    internal static void Start(Harmony harmony)
    {
        limitType = null; hostOff = set = lobbySet = null;
        var game = typeof(MoonWorld).Assembly;
        var limits = game.GetType("MoonCleaners.MoonCrewLimits");
        limitType = game.GetType("MoonCleaners.MoonCrewLimits+Limit");
        if (limits == null || limitType == null)
        {
            Plugin.L.LogInfo("Game crew limits: not in this game version (1.0.5 or earlier); Crew Tools' switches only.");
            return;
        }
        const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
        hostOff = limits.GetMethod("HostOff", Static, null, new[] { typeof(ulong) }, null);
        var found = limits.GetMethod("Set", Static, null, new[] { typeof(ulong), typeof(string), limitType }, null);
        lobbySet = typeof(MoonSteamLobby).GetMethod("SetLimits", Static, null, new[] { typeof(ulong), limitType }, null);
        if (hostOff == null || found == null || lobbySet == null)
        {
            Plugin.L.LogWarning("Game crew limits: found, but not as expected; Crew Tools won't touch them.");
            return;
        }
        harmony.Patch(found, postfix: new HarmonyMethod(typeof(GameLimits), nameof(SetByGame)));
        set = found;
        dirty = true;
        Plugin.L.LogInfo("Game crew limits: found; kept the same as Crew Tools' Build and Shop switches.");
    }

    // Players.Save: a switch may have changed, so look again at the next update
    internal static void Changed() => dirty = true;

    // Once a second (and only while hosting, like all of Crew Tools): game limits set from Crew Tools' switches - every
    // known player's after a switch changed, and the crew-mates in the game always. The game gives a player it hasn't
    // seen in a public crew its own default when they join (MoonCrewLimits.Welcome: no building or spending); Crew Tools'
    // switches win over that, so a new player gets the new-player switches set on the Rules tab, and a player Crew Tools
    // already knows keeps their own.
    internal static void Update()
    {
        if (!Present || Time.unscaledTime < nextLook) return;
        nextLook = Time.unscaledTime + 1f;
        if (dirty) { dirty = false; foreach (var p in Players.All) Match(p); }
        else foreach (var (id, _) in Crew.Now) Match(Players.Get(id));
    }

    static void Match(Players.Player p)
    {
        int now = Off(p.Id);
        if (now < 0) return;
        int want = (now & ~(Build | Spend)) | (Players.Switch(p, Node.Build) ? 0 : Build) | (Players.Switch(p, Node.Shop) ? 0 : Spend);
        if (want != now) Apply(p, want);
    }

    // the player's game limits as the host saved them; -1 if they couldn't be read
    static int Off(ulong id)
    {
        try { return Convert.ToInt32(hostOff.Invoke(null, new object[] { id })); }
        catch (Exception e) { Plugin.L.LogWarning($"Game crew limits: couldn't read {id}'s: {e.InnerException?.Message ?? e.Message}"); return -1; }
    }

    static void Apply(Players.Player p, int off)
    {
        var limit = Enum.ToObject(limitType, (byte)off);
        applying = true;
        try
        {
            lobbySet.Invoke(null, new object[] { p.Id, limit });                                   // saved and sent to the crew
            Trace.Log($"game limits for {p.Name}: {off}");
        }
        catch (Exception e) { Plugin.L.LogWarning($"Game crew limits: couldn't set {p.Name}'s: {e.InnerException?.Message ?? e.Message}"); }
        finally { applying = false; }
    }

    // After the game saves a player's limits: if the host changed them in the crew menu (not Crew Tools, in Apply), the
    // Build and Shop switches follow.
    static void SetByGame(object[] __args)
    {
        if (applying) return;
        try
        {
            ulong id = (ulong)__args[0];
            int bits = Convert.ToInt32(__args[2]);
            var p = Players.Get(id);
            bool build = (bits & Build) == 0, shop = (bits & Spend) == 0;
            if (Players.Switch(p, Node.Build) == build && Players.Switch(p, Node.Shop) == shop) return;
            Players.SetSwitch(id, Node.Build, build);
            Players.SetSwitch(id, Node.Shop, shop);
            Plugin.L.LogInfo($"{p.Name}: Build {(build ? "on" : "off")}, Shop {(shop ? "on" : "off")} (set in the game's crew menu)");
        }
        catch (Exception e) { Plugin.L.LogWarning("Game crew limits: couldn't follow a change: " + e.Message); }
    }
}
