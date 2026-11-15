using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using MoonCleaners;
using Steamworks;
using UnityEngine;

namespace CrewTools;

// Owners: who built what, and the ban reclaim (their pieces and vehicles).
// The game doesn't record who built a structure, so this does: every structure or belt that appears while the game
// applies a crew-mate's building message is theirs (CrewTools.owners.txt). When someone is banned, everything of
// theirs still standing is removed and the game's normal refund for each piece goes back to the team money.
internal static class Owners
{
    static readonly Dictionary<int, ulong> structures = new(), belts = new();
    static HashSet<int> beforeS, beforeB; static Dictionary<int, Vector3> beforePos;
    static string File => System.IO.Path.Combine(Paths.ConfigPath, "CrewTools.owners.txt");

    internal static int Count(ulong id) { int n = 0; foreach (var v in structures.Values) if (v == id) n++; foreach (var v in belts.Values) if (v == id) n++; return n; }

    internal static void Before(MoonWorld world)
    {
        beforeS = beforeB = null; beforePos = null; if (Guard.Sender == 0) return;
        beforeS = new HashSet<int>(); beforePos = new();
        foreach (var kv in world.depots) { beforeS.Add(kv.Key); try { beforePos[kv.Key] = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z); } catch { } }
        beforeB = new HashSet<int>(); foreach (var kv in world.conveyors) beforeB.Add(kv.Key);
    }
    internal static void After(MoonWorld world)
    {
        ulong id = Guard.Sender; if (id == 0 || beforeS == null) return;
        int n = 0;
        foreach (var kv in world.depots) if (!beforeS.Contains(kv.Key)) { structures[kv.Key] = id; n++; }       // robots too
        foreach (var kv in world.conveyors) if (!beforeB.Contains(kv.Key)) { belts[kv.Key] = id; n++; }
        // cheat watch, reach: what this message really built and removed (anything refused never got here)
        try
        {
            var added = new List<Vector3>(); var now = new HashSet<int>();
            foreach (var kv in world.depots) { now.Add(kv.Key); if (!beforeS.Contains(kv.Key)) added.Add(new Vector3(kv.Value.x, kv.Value.y, kv.Value.z)); }
            var removed = new List<Vector3>(); foreach (var kv in beforePos) if (!now.Contains(kv.Key)) removed.Add(kv.Value);
            AntiCheat.Structures(id, added, removed);
        }
        catch (Exception e) { Plugin.L.LogWarning("cheat watch reach: " + e.Message); }
        beforeS = beforeB = null; beforePos = null;
        if (n > 0) Save();
    }

    internal static void Banned(ulong id)
    {
        try
        {
            string name = Players.Name(id);
            var (pieces, dollars) = Reclaim(id); var (cars, carDollars) = ReclaimVehicles(id);
            Plugin.L.LogInfo($"banned {name}: {pieces} pieces and {cars} vehicles reclaimed, ${dollars + carDollars:0} back");
        }
        catch (Exception e) { Plugin.L.LogWarning("reclaim: " + e.Message); }
    }
    // A crew-mate who just leaves keeps their vehicles, upgrades and all, for when they come back. (Up to 1.2.4 they were
    // reclaimed on every leave, a crash or dropped connection too, and matched by display name as well, so players lost
    // upgrades, and same-named players each other's vehicles.) Only a ban reclaims them.

    // this player's vehicle: by Steam id only (the game writes it as "<id>" or "steam-<id>"), never the host's
    static bool Theirs(MoonOwnedVehicle v, ulong id) =>
        v != null && v.owner != "local" && (v.owner == id.ToString() || v.owner == "steam-" + id);

    // their vehicles off the owned list (and out of the world if they're out), refunded at the garage price - tested
    // with the host's own push sweeper (stored) and ride-on (out), 2026-11-09
    // Everything paid for a vehicle's upgrades (2026-11-09): for each upgrade kind, the price of every level it
    // reached. The game prices a level with MoonVehicleUpgrades.Price(def, vehicle, level); which index means "the
    // step to this level" is checked against the game's own next-step price (TryGetVehicleUpgradePrice).
    static MethodInfo upLevelM, upNextM, upPriceM, upAllM; static Type upKindT;
    internal static double UpgradeSpend(MoonOwnedVehicle v, out string levels)
    {
        levels = "";
        try
        {
            if (upPriceM == null)
            {
                foreach (var m in typeof(MoonWorld).GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "VehicleUpgradeLevel" && ps.Length == 2 && ps[0].ParameterType == typeof(MoonOwnedVehicle)) { upLevelM = m; upKindT = ps[1].ParameterType; }
                    if (m.Name == "TryGetVehicleUpgradePrice" && ps.Length == 3) upNextM = m;
                }
                foreach (var m in typeof(MoonVehicleUpgrades).GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (m.Name == "Price" && m.GetParameters().Length == 3) upPriceM = m;
                    if (m.Name == "get_All") upAllM = m;
                }
                if (upLevelM == null || upPriceM == null || upAllM == null) { Plugin.L.LogWarning("vehicle upgrades: game methods not found - upgrades not refunded"); return 0; }
            }
            double total = 0; var parts = new List<string>();
            foreach (var def in (System.Collections.IEnumerable)upAllM.Invoke(null, null))
            {
                object kind = null;
                foreach (var f in def.GetType().GetProperties()) if (f.PropertyType == upKindT) { kind = f.GetValue(def); break; }
                if (kind == null) continue;
                int level = (int)upLevelM.Invoke(null, new[] { (object)v, kind });
                if (level <= 0) continue;
                double P(int i) => Il2CppSystem.Decimal.ToDouble((Il2CppSystem.Decimal)upPriceM.Invoke(null, new object[] { def, v.vehicle, i }));
                // is Price(i) "the step from level i to i+1" or "the step to level i"? compare with the game's next price
                bool stepFrom = true;
                if (upNextM != null)
                {
                    var args = new object[] { v, kind, default(Il2CppSystem.Decimal) };
                    if ((bool)upNextM.Invoke(null, args)) { double next = Il2CppSystem.Decimal.ToDouble((Il2CppSystem.Decimal)args[2]); stepFrom = Math.Abs(P(level) - next) < 0.01 || Math.Abs(P(level + 1) - next) >= 0.01; }
                }
                double sum = 0; for (int i = 0; i < level; i++) sum += stepFrom ? P(i) : P(i + 1);
                total += sum; parts.Add($"{kind} {level} = ${sum:0}");
            }
            levels = parts.Count == 0 ? "none" : string.Join(", ", parts);
            return total;
        }
        catch (Exception e) { Plugin.L.LogWarning("vehicle upgrades: " + e.Message); return 0; }
    }

    // what reclaiming this player's vehicles would pay back now: (vehicles, garage prices, upgrades) - for the panel
    internal static (int count, double basePrice, double upgrades) VehicleValue(ulong id)
    {
        var w = Crew.World(); if (w == null) return (0, 0, 0);
        int n = 0; double b = 0, u = 0;
        foreach (var kv in w.ownedVehicles)
        {
            var v = kv.Value; if (!Theirs(v, id)) continue;
            n++; foreach (var o in MoonWorld.GarageOffers) if (o.vehicle == v.vehicle) { b += Il2CppSystem.Decimal.ToDouble(o.price); break; }
            u += UpgradeSpend(v, out var lv); Plugin.L.LogInfo($"{Players.Name(id)}'s vehicle {v.vehicle}: upgrades {lv}");
        }
        return (n, b, u);
    }

    // a banned player's vehicles off the owned list (and out of the world if they're out), refunded at the garage price
    // plus everything paid for their upgrades
    internal static (int count, double dollars) ReclaimVehicles(ulong id)
    {
        var w = Crew.World(); if (w == null) return (0, 0);
        var keys = new List<string>();
        foreach (var kv in w.ownedVehicles) if (Theirs(kv.Value, id)) keys.Add(kv.Key);
        int n = 0; double total = 0;
        foreach (var key in keys)
        {
            try
            {
                var v = w.ownedVehicles.get_Item(key); double price = 0;
                foreach (var o in MoonWorld.GarageOffers) if (o.vehicle == v.vehicle) { price = Il2CppSystem.Decimal.ToDouble(o.price); break; }
                double up = UpgradeSpend(v, out var levels);
                Plugin.L.LogInfo($"reclaim {v.ownerName}'s vehicle {v.vehicle}: ${price:0} + upgrades ${up:0}");
                price += up;
                if (w.garageVehicles.ContainsKey(key)) { var pv = w.garageVehicles.get_Item(key); if (pv != null) UnityEngine.Object.Destroy(pv.gameObject); w.garageVehicles.Remove(key); }
                w.ownedVehicles.Remove(key); n++; total += price;
                try { w.OwnedVehiclesRevision = w.OwnedVehiclesRevision + 1; } catch { }
                try { w.GarageChanged?.Invoke(v.garage); } catch { }
            }
            catch (Exception e) { Plugin.L.LogWarning("vehicle " + key + ": " + e.Message); }
        }
        if (total > 0) Pay(w, total);
        return (n, total);
    }

    // remove everything of theirs still standing, crediting the game's normal refund to the team money
    internal static (int pieces, double dollars) Reclaim(ulong id)
    {
        var w = Crew.World(); if (w == null) return (0, 0);
        var standing = new HashSet<int>(); foreach (var kv in w.depots) standing.Add(kv.Key);
        var beltsNow = new HashSet<int>(); foreach (var kv in w.conveyors) beltsNow.Add(kv.Key);
        double refund = 0; int n = 0; var walker = MoonRunOver.LocalWalker;
        // belts: the game's own undo, which works from anywhere and pays their refund itself (tested from 2.9 km,
        // 2026-11-09); the normal removal refuses far belts ("Move closer")
        var mine = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var kv in new List<KeyValuePair<int, ulong>>(belts))
        {
            if (kv.Value != id) continue; belts.Remove(kv.Key);
            if (beltsNow.Contains(kv.Key)) mine.Add(kv.Key);
        }
        if (mine.Count > 0 && walker != null)
        {
            try
            {
                int had = mine.Count;
                if (w.UndoConveyors(walker, mine.Cast<Il2CppSystem.Collections.Generic.IList<int>>(), out Il2CppSystem.Decimal back)) { n += had; refund += Il2CppSystem.Decimal.ToDouble(back); }
            }
            catch (Exception e) { Plugin.L.LogWarning("belts: " + e.Message); }
        }
        foreach (var kv in new List<KeyValuePair<int, ulong>>(structures))
        {
            if (kv.Value != id) continue; structures.Remove(kv.Key);
            if (!standing.Contains(kv.Key)) continue;
            double r = 0; try { r = Il2CppSystem.Decimal.ToDouble(w.StructureRefund(kv.Key)); } catch { }
            Remover.Structure(kv.Key); n++; refund += r;            // removed and refunded a piece at a time
        }
        Save(); return (n, refund);
    }
    internal static void Pay(MoonWorld w, double dollars)
    {
        try
        {
            var wallet = w.wallets; var me = SteamUser.GetSteamID().m_SteamID.ToString();
            double now = Il2CppSystem.Decimal.ToDouble(wallet.Balance);
            wallet.set_Item(me, new Il2CppSystem.Decimal(now + dollars));
        }
        catch (Exception e) { Plugin.L.LogWarning("refund not paid: " + e.Message); }
    }

    internal static void Load()                                     // Plugin.Load
    {
        try
        {
            if (!System.IO.File.Exists(File)) return;
            foreach (var line in System.IO.File.ReadAllLines(File))
            {
                var p = line.Split('\t'); if (p.Length < 3 || !int.TryParse(p[1], out var sid) || !ulong.TryParse(p[2], out var o)) continue;
                if (p[0] == "s") structures[sid] = o; else if (p[0] == "b") belts[sid] = o;
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("owners not read: " + e.Message); }
    }
    static void Save()
    {
        try
        {
            var lines = new List<string>();
            foreach (var kv in structures) lines.Add($"s\t{kv.Key}\t{kv.Value}");
            foreach (var kv in belts) lines.Add($"b\t{kv.Key}\t{kv.Value}");
            System.IO.File.WriteAllLines(File, lines);
        }
        catch (Exception e) { Plugin.L.LogWarning("owners not saved: " + e.Message); }
    }
}
