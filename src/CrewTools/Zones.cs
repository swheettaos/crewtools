using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrewTools;

// Zones: protected bases. Per base (North/South/East/West) on/off and a radius from its station. Crew-mates inside one
// they have no pass for can look but not build, demolish, sell, shop or handle capsules (Apply, from Players.Allowed),
// nor buy or upgrade a vehicle (VehicleBuy). They are told once per base, the first time they walk in after joining.
internal static class Zones
{
    // Players.Allowed asks "may this player do this?" while it handles that player's action; this adds the protected
    // bases and the spending cap to the answer their switch gave (allowed). Every refusal is noted (Guard.Refused).
    internal static void Apply(Players.Player p, Node node, ref bool allowed)
    {
        if (p == null) return;
        ulong id = p.Id;
        if (id == 0 || Guard.Sender != id) return;                    // only for the action being handled (not the panel's display)
        bool zone = InZone(id);
        // spending (shop, building) only while they're under their 30-minute cap (trusted: no cap)
        if (allowed && !zone && (node == Node.Shop || node == Node.Build) && Spending.OverCap(id)) { allowed = false; Guard.Refused(id); return; }
        if (!allowed && !zone) { Guard.Refused(id); return; }                  // their switch is off
        if (!zone) return;
        // inside a protected base: refused quietly - they were told once when they walked in (Update below). A message
        // per refused action spammed crew chat with "shopping/selling is restricted" (2026-11-09)
        allowed = false; Guard.Refused(id);
    }

    // Hooks: a crew-mate buying or upgrading a vehicle (driving, parking and recalling are never refused): refused when
    // their Shop switch is off, inside a protected base without a pass, or over their spending cap
    internal static bool VehicleBuy()
    {
        ulong id = Guard.Sender; if (id == 0) return true;
        bool zone = InZone(id), over = Spending.OverCap(id);
        bool noShop = !Players.Switch(Players.Get(id), Node.Shop);   // the switch alone: zone and cap are checked here
        if (!zone && !noShop && !over) return true;
        Guard.Refused(id);                                         // the payment their game sends is refused too
        Plugin.L.LogInfo($"{Players.Name(id)}: vehicle purchase refused ({(noShop ? "shop switched off" : zone ? "protected base" : "spending cap")})");
        return false;
    }

    // "@name: this area (South base) is restricted." - once per base, the first time they walk in after joining;
    // never for a player with a pass for that base. Forgotten when they've been gone 20 s (a rejoin is told again).
    static readonly HashSet<(ulong id, string bse)> told = new(); static readonly Dictionary<ulong, float> lastSeen = new(); static float nextEntry;
    internal static void Update()
    {
        if (Time.unscaledTime < nextEntry) return; nextEntry = Time.unscaledTime + 1f;
        if (!Crew.Hosting || Crew.World() == null) return;
        float now = Time.unscaledTime;
        foreach (var (id, pl) in Crew.Now)
        {
            lastSeen[id] = now;
            if (!ProtectedBaseAt(id, pl.pos, out var b, out _) || !told.Add((id, b))) continue;
            string name = string.IsNullOrEmpty(pl.name) ? "crewmate" : pl.name;
            string msg = $"@{name}: this area ({b} base) is restricted.";
            try { Chat.Say(msg); } catch (Exception e) { Plugin.L.LogWarning("crew chat: " + e.Message); }
            Plugin.L.LogInfo(msg);
        }
        foreach (var id in new List<ulong>(lastSeen.Keys))
            if (now - lastSeen[id] > 20f) { lastSeen.Remove(id); told.RemoveWhere(k => k.id == id); }
    }

    internal static string BaseOf(string stationName)
    {
        foreach (var b in new[] { "South", "East", "West", "North" }) if (stationName.Contains(b)) return b;
        return "North";                                              // the main station is just "TheCollector"
    }

    // is this crew-mate inside a protected base they have no pass for?
    internal static bool InZone(ulong id) => Crew.Now.TryGetValue(id, out var pl) && ProtectedBaseAt(id, pl.pos, out _, out _);

    // the protected base (switched on, no pass for this player) whose zone this position is in, if any
    internal static bool ProtectedBaseAt(ulong id, Vector3 pos, out string bse, out float metres)
    {
        bse = null; metres = 0;
        var w = Crew.World(); if (w == null) return false;
        foreach (var s in w.stations)
        {
            string b = BaseOf(s.gameObject.name);
            if (!Plugin.ZoneOn.TryGetValue(b, out var on) || !on.Value) continue;
            if (Players.HasPass(id, b)) continue;                       // the host gave them a pass for this base
            float m = Plugin.ZoneMetres[b].Value;
            if (Vector3.Distance(pos, s.transform.position) <= m) { bse = b; metres = m; return true; }
        }
        return false;
    }
}
