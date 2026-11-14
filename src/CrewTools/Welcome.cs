using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

// Welcome: greet each crew-mate once per session in crew chat, then name the protected bases. Chat: crew chat itself.
internal static class Welcome
{
    static readonly HashSet<ulong> greeted = new(); static readonly Dictionary<ulong, float> seenAt = new();
    static float nextLook;
    // one short line per protected base (crew chat takes at most 120 characters a message)
    internal static List<string> ZoneNote()
    {
        var lines = new List<string>();
        foreach (var b in Plugin.Bases)
            if (Plugin.ZoneOn[b].Value) lines.Add($"{b} base is restricted: look, but you can't touch (within {Plugin.ZoneMetres[b].Value:0} m).");
        if (lines.Count > 0) lines.Add("Teleporting from the map still works.");
        return lines;
    }
    internal static void Update()
    {
        if (!Plugin.WelcomeOn.Value || Time.unscaledTime < nextLook) return;
        nextLook = Time.unscaledTime + 1f;
        if (!Crew.Hosting) return;
        foreach (var (id, pl) in Crew.Now)
        {
            string name = pl.name;
            if (greeted.Contains(id)) continue;
            if (!seenAt.TryGetValue(id, out var t)) { seenAt[id] = Time.unscaledTime; continue; }
            if (Time.unscaledTime - t < 5f) continue;                       // a few seconds to finish loading in
            greeted.Add(id);
            try
            {
                Chat.Say(Plugin.WelcomeText.Value.Replace("{name}", string.IsNullOrEmpty(name) ? "crewmate" : name).Replace("{server}", MoonSteamLobby.CrewName ?? "our crew"));
                foreach (var line in ZoneNote()) Chat.Say(line);
            }
            catch (Exception e) { Plugin.L.LogWarning("welcome: " + e.Message); }
        }
    }
}

// crew chat, never past the game's limit (MoonTalk.MaxLength, 120): cut at a word, with "..."
internal static class Chat
{
    internal static void Say(string text)
    {
        int max = 120; try { max = MoonTalk.MaxLength; } catch { }
        text = (text ?? "").Trim();
        if (text.Length > max) { int cut = text.LastIndexOf(' ', max - 3); text = text.Substring(0, cut > 20 ? cut : max - 3).TrimEnd() + "..."; }
        if (text.Length > 0) MoonTalk.Say(text);
    }
}
