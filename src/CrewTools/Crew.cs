using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

// Crew: the game as it is now - its world, whether you host, and the crew-mates in it (one look at the scene, at most
// twice a second, shared by every module).
internal static class Crew
{
    static Dictionary<ulong, (Vector3 pos, string name)> players = new();
    static List<MoonRemotePlayer> remotes = new();
    static float lookedAt = -10f;

    // true while you host a crew game: the only time the crew modules do anything
    internal static bool Hosting => MoonSteamLobby.InLobby && MoonSteamLobby.IsHost;

    // id -> (position, name) of every crew-mate in the game now
    internal static Dictionary<ulong, (Vector3 pos, string name)> Now
    {
        get
        {
            if (Time.unscaledTime - lookedAt < 0.5f) return players;
            lookedAt = Time.unscaledTime;
            var fresh = new Dictionary<ulong, (Vector3, string)>();      // a new one, so a caller mid-loop isn't disturbed
            var found = new List<MoonRemotePlayer>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<MoonRemotePlayer>()))
            {
                try { var r = o.Cast<MoonRemotePlayer>(); if (r.SteamId != 0) { fresh[r.SteamId] = (r.transform.position, r.DisplayName); found.Add(r); } }
                catch { }
            }
            remotes = found;
            foreach (var id in fresh.Keys) if (!players.ContainsKey(id)) Trace.Log($"crew: {id} {fresh[id].Item2} appeared in the scene");
            foreach (var id in players.Keys) if (!fresh.ContainsKey(id)) Trace.Log($"crew: {id} left the scene");
            return players = fresh;
        }
    }

    // the game's world, found once (searched for at most once a second while there is none: menus, loading)
    static MoonWorld world; static float worldLookAt = -10f;
    internal static MoonWorld World()
    {
        if (world == null && Time.unscaledTime - worldLookAt >= 1f)
        {
            worldLookAt = Time.unscaledTime;
            var f = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<MoonWorld>()); if (f.Length > 0) world = f[0].Cast<MoonWorld>();
        }
        return world;
    }

    internal static bool Online(ulong id) => Now.ContainsKey(id);

    // the crew-mates' player objects from the same look (the cheat watch reads where they are and how they move)
    internal static List<MoonRemotePlayer> Remotes() { _ = Now; return remotes; }
}
