using MoonCleaners;
using Steamworks;
using UnityEngine;

namespace CrewTools;

// ServerName: your saved server name, put on every game you host (once per lobby).
internal static class ServerName
{
    static ulong appliedTo; static float nextLook;
    // every game you host gets your saved name (once per lobby, a moment after it's created)
    internal static void Update()
    {
        if (Time.unscaledTime < nextLook) return; nextLook = Time.unscaledTime + 2f;
        string name = Plugin.SavedName.Value; if (string.IsNullOrWhiteSpace(name) || !Crew.Hosting) return;
        ulong lobby = MoonSteamLobby.LobbyId; if (lobby == 0 || lobby == appliedTo) return;
        if (MoonSteamLobby.CrewName != name) Plugin.L.LogInfo("server name: " + Set(name, false));
        appliedTo = lobby;
    }
    internal static string Set(string text, bool remember = true)
    {
        if (!Crew.Hosting) return "Host a game first";
        text = (text ?? "").Trim(); if (text.Length == 0 || text.Length > 60) return "Name must be 1-60 characters";
        string old = MoonSteamLobby.CrewName ?? ""; var id = new CSteamID(MoonSteamLobby.LobbyId);
        MoonSteamLobby.CrewName = text; int keys = 0;
        int n = SteamMatchmaking.GetLobbyDataCount(id);
        for (int i = 0; i < n; i++)
        {
            if (!SteamMatchmaking.GetLobbyDataByIndex(id, i, out var k, 256, out var v, 8192)) continue;
            if (old.Length > 0 && v == old) { SteamMatchmaking.SetLobbyData(id, k, text); keys++; }
        }
        try { MoonSteamLobby.RefreshLobby(); } catch { }
        if (remember) { Plugin.SavedName.Value = text; appliedTo = MoonSteamLobby.LobbyId; }
        return keys > 0 ? $"Saved: \"{text}\"" : $"Saved \"{text}\" in game (no listing field held the old name)";
    }
}
