// Spending: each crew-mate may spend up to Plugin.Cap dollars in any 30 minutes (shop, building - J.O.O.N.A.S. included
// - vehicles), counted from the team money as it drops while the game handles that player's action (Hooks.Wallet).
// Shop purchases are priced by the game first (that player's price, upgrade discounts included) and refused if over
// what's left (CanAfford); building is allowed while anything is left and counted after (OverCap, via Zones.Apply).
// Trusted players (Manage > Trust) have no cap.
using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;
using Decimal = Il2CppSystem.Decimal;

namespace CrewTools;

internal static class Spending
{
    const float Window = 1800f;                                              // 30 minutes
    static readonly Dictionary<ulong, List<(float t, double d)>> spent = new();
    static readonly Dictionary<ulong, float> told = new();                  // last "you've hit your cap" per player

    // Handover: the last 30 minutes' spending goes to the next copy, so a reload doesn't reset anyone's cap
    internal static Dictionary<ulong, List<(float t, double d)>> Export() => spent;
    internal static void Import(Dictionary<ulong, List<(float t, double d)>> s) { foreach (var kv in s) spent[kv.Key] = kv.Value; }

    internal static double Used(ulong id)
    {
        if (!spent.TryGetValue(id, out var l)) return 0;
        float now = Time.unscaledTime; l.RemoveAll(e => now - e.t > Window);
        double s = 0; foreach (var e in l) s += e.d; return s;
    }

    internal static double Left(ulong id) => Plugin.Cap.Value - Used(id);

    // Manage > Reset spending: their full cap again, now
    internal static void ResetBank(ulong id) { spent.Remove(id); told.Remove(id); }

    // Players.Forget
    internal static void Forget(ulong id) => ResetBank(id);

    // Hooks.Wallet: the team money dropped by this much while handling this crew-mate's action
    internal static void Spent(ulong id, double dollars)
    {
        if (id == 0 || dollars < 0.005) return;
        if (!spent.TryGetValue(id, out var l)) spent[id] = l = new();
        l.Add((Time.unscaledTime, dollars));
    }

    // Zones.Apply: Build or Shop is allowed for this player only while some of their cap is left (trusted: no cap)
    internal static bool OverCap(ulong id)
    {
        if (Players.Trusted(id) || Left(id) > 0) return false;
        Tell(id); return true;
    }

    // Hooks.Purchase: the game's own price for this player (upgrade discounts included), against what's left of their
    // cap. Kinds the game can't price here (vehicle upgrades) are allowed while anything is left and counted after.
    internal static bool CanAfford(MoonWorld w, ulong id, object[] args)
    {
        try
        {
            if (id == 0 || Players.Trusted(id) || args == null || args.Length < 3) return true;
            string player = args[0] as string, kind = args[1]?.ToString(), item = args[2] as string;
            Decimal price = default;
            bool known = kind switch
            {
                "Tool" => w.TryGetToolPrice(item, player, out price),
                "Container" => w.TryGetContainerPrice(player, item, out price),
                "TreeNode" => w.TryGetTreePrice(player, item, out price),
                _ => false,
            };
            double left = Left(id);
            if (known ? Decimal.ToDouble(price) <= left : left > 0) return true;
            Tell(id);
            return false;
        }
        catch (Exception e) { Plugin.L.LogWarning("price check: " + e.Message); return true; }
    }

    static void Tell(ulong id)
    {
        float now = Time.unscaledTime;
        if (told.TryGetValue(id, out var t) && now - t < 30f) return;
        told[id] = now;
        Chat.Say($"@{Players.Name(id)}: you've hit your spending cap (${Plugin.Cap.Value:0} per 30 min). Ask the admin to grant you more.");
    }
}
