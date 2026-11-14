// Guard: the permission checks on every crew-mate message the host's game applies (called from Hooks).
//
// From Crew Permissions by tylerun (nexusmods.com/moonsweepers/mods/71), rebuilt from the released DLL and tidied for
// Crew Tools: names, comments and shared helpers are ours; what is checked and refused is theirs, except where marked.
//
// How it works: the game hands each crew message to MoonCrewSession.Handle*(sender, ...). Enter/Leave note who sent it
// (Sender) for the length of the call; the MoonWorld.Crew* methods it then calls are where each action is allowed,
// refused, or checked for cheating (AntiCheat). The host's own actions have Sender 0 and are never checked.
using System;
using System.Collections.Generic;
using Il2CppSystem.IO;
using MoonCleaners;
using UnityEngine;
using Decimal = Il2CppSystem.Decimal;

namespace CrewTools;

internal static class Guard
{
    // the crew-mate whose message is being applied (0: the host, or not in a crew message)
    internal static ulong Sender;
    // for the trace: from a sender's first message, every message for 30 s is logged going in and coming out - one that
    // goes in and never comes out is the call that died
    static readonly HashSet<ulong> seenSenders = new();
    static readonly Stack<string> doing = new();
    static float traceUntil;

    // ------------------------------------------------------------------ who sent the message
    // Each Handle* message sets Sender for its length, then puts the previous one back (messages can nest).

    internal static ulong Enter(ulong sender, string message)
    {
        if (sender != 0 && seenSenders.Add(sender)) traceUntil = Time.unscaledTime + 30f;
        bool tracing = Time.unscaledTime < traceUntil;
        if (doing.Count > 50) doing.Clear();                  // a message that threw never pops
        doing.Push(tracing ? message : null);
        if (tracing) Trace.Log($"-> {message} from {sender}");
        AntiCheat.Why = null;
        ulong previous = Sender;
        bool crewMate = MoonSteamLobby.IsHost && sender != 0 && sender != MoonSteamLobby.HostId;
        Sender = crewMate ? sender : 0;
        return previous;
    }

    internal static void Leave(ulong previous)
    {
        if (doing.Count > 0 && doing.Pop() is string message) Trace.Log($"<- {message} done");
        Sender = previous;
        AntiCheat.Why = null;
    }

    // ------------------------------------------------------------------ allowed or refused

    static readonly Dictionary<(ulong, Node), float> lastLogged = new();   // one "Refused" line per player and node per 10 s

    // Is the sender allowed this? Logs a refusal (once per 10 s per player and node). The host is always allowed, and so
    // is anyone if the check itself fails (a broken check must not lock players out).
    internal static bool Allow(Node node, string what)
    {
        if (Sender == 0) return true;
        AntiCheat.Why = what;
        try
        {
            var p = Players.Get(Sender);
            if (Players.Allowed(p, node)) return true;
            var key = (Sender, node);
            if (!lastLogged.TryGetValue(key, out var t) || Time.unscaledTime - t > 10f)
            {
                lastLogged[key] = Time.unscaledTime;
                Plugin.L.LogInfo($"Refused {p.Name}: {what} (needs {node.ToString().ToLowerInvariant()})");
            }
            return false;
        }
        catch (Exception e)
        {
            Plugin.L.LogWarning("Permission check failed (allowed): " + e.Message);
            return true;
        }
    }

    // always allowed; names the action for the money checks (AntiCheat.Why)
    internal static bool Pays(string what)
    {
        if (Sender != 0) AntiCheat.Why = what;
        return true;
    }

    // Crew Tools: a refused action's money still arrives - the crew-mate's game already showed the cost or refund - and
    // the money checks found no matching build and banned an honest player for "fake building costs" (a refused
    // demolish's $50 refund, 2026-11-09). So for 10 s after any refusal (switch off, protected base, spending cap) their
    // building money is refused quietly, and the cheat watch doesn't count their deposits or reach. A real cheat outside
    // that window is still caught.
    static readonly Dictionary<ulong, float> refusedAt = new();
    internal static void Refused(ulong id) => refusedAt[id] = Time.unscaledTime;
    internal static bool RecentlyRefused(ulong id) => refusedAt.TryGetValue(id, out var t) && Time.unscaledTime - t <= 10f;

    // ------------------------------------------------------------------ selling (a refused sale gives the capsule back)

    internal static bool Sell(string player, MoonCargo cargo, double kilograms, string what, bool dustDeposit, ref double paid)
    {
        if (AntiCheat.OwnWallet(Sender, player, what) && AntiCheat.Kilograms(Sender, kilograms, what))
        {
            if (Allow(Node.Sell, what))
            {
                AntiCheat.CheckDeposit(kilograms, atStation: true, dust: dustDeposit);   // capacity: dust deposits only
                return true;
            }
            Returns.GiveBack(Sender, cargo, kilograms);       // no Sell permission: not paid, and the capsule comes back
        }
        paid = 0;
        return false;
    }

    // ------------------------------------------------------------------ capsules
    // Without the Capsules permission a player can't pick up or put down capsules by hand. Capsules moved by a tool,
    // brush or vacuum are let through (that's cleaning, not taking).

    static readonly Dictionary<string, float> capsuleLogged = new();

    internal static bool Capsule(MoonWorld.CanisterOp op, string carrier)
    {
        if (Sender == 0) return true;
        var p = Players.Get(Sender);
        if (Players.Allowed(p, Node.Capsules)) return true;

        bool takeOrDrop = op == MoonWorld.CanisterOp.Taken || op == MoonWorld.CanisterOp.Released;
        bool byTool = carrier != null && (Has(carrier, "tool") || Has(carrier, "brush") || Has(carrier, "vacuum"));
        bool refuse = takeOrDrop && !byTool;

        string key = $"{Sender} {op} {carrier}";
        if (!capsuleLogged.TryGetValue(key, out var t) || Time.unscaledTime - t > 10f)
        {
            capsuleLogged[key] = Time.unscaledTime;
            Plugin.L.LogInfo($"Capsule {op} by {p.Name}, holder \"{carrier}\": {(refuse ? "refused (no capsules permission)" : "let through (not by hand)")}.");
        }
        if (!refuse) return true;
        Allow(Node.Capsules, "handling capsules by hand");
        return false;
    }

    static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // ------------------------------------------------------------------ building and demolishing
    // A building message is four lists: structures built, structures removed, belts built, belts removed. If the player
    // may not build or may not demolish, the message is rewritten without that half. Either way, what is built is priced
    // (and what is removed, refunded) so AntiCheat can tell real building money from made-up money.

    static double addedPrice, removedRefund;
    static int addedCount, removedCount;

    // what this player built lately, at its refund: a removal of a piece that is already gone here (deleted twice, or
    // built and deleted while messages crossed) is still priced (2026-11-10: a press bought and deleted got
    // a "$0 demolished" ban). Kept 5 minutes.
    static readonly Dictionary<int, (float At, double Refund)> recentPieces = new(), recentBelts = new();
    const float Remember = 300f;

    internal static void FilterBuilding(MoonWorld world, ref BinaryReader r)
    {
        addedPrice = removedRefund = 0;
        addedCount = removedCount = 0;
        if (Sender == 0 || r == null) return;

        var p = Players.Get(Sender);
        bool build = Players.Allowed(p, Node.Build);
        bool demolish = Players.Allowed(p, Node.Demolish);
        long start = -1;
        try { var s = r.BaseStream; if (s != null && s.CanSeek) start = s.Position; } catch { }
        if (build && demolish && start < 0) return;          // allowed, and can't read ahead to price it: apply as sent

        try
        {
            var depots = ReadList(r, MoonWorld.ReadDepot);
            var removed = ReadIds(r);
            var belts = ReadList(r, MoonWorld.ReadBelt);
            var cut = ReadIds(r);

            if (!build && (depots.Count > 0 || belts.Count > 0)) Allow(Node.Build, "building");
            if (!demolish && (removed.Count > 0 || cut.Count > 0)) Allow(Node.Demolish, "demolishing");
            Price(world, build ? depots : null, demolish ? removed : null, build ? belts : null, demolish ? cut : null);

            if (build && demolish) { r.BaseStream.Position = start; return; }   // nothing to take out: rewind

            var m = new MemoryStream(); var w = new BinaryWriter(m);
            WriteList(w, build ? depots : null, d => MoonWorld.Write(w, d));
            WriteList(w, demolish ? removed : null, id => w.Write(id));
            WriteList(w, build ? belts : null, b => MoonWorld.Write(w, b));
            WriteList(w, demolish ? cut : null, id => w.Write(id));
            w.Flush(); m.Position = 0;
            r = new BinaryReader(m);
        }
        catch (Exception e)
        {
            Plugin.L.LogError("Filtering a crew building message failed (applied as sent): " + e.Message);
            try { if (start >= 0) r.BaseStream.Position = start; } catch { }
        }
    }

    // after the message: report what it built and removed (every building message counts, even an empty one: it is
    // the proof that money came with a real build or delete)
    internal static void BuildingDone(MoonWorld world)
    {
        if (Sender == 0) return;
        AntiCheat.Built(Sender, addedPrice, removedRefund, addedCount, removedCount);
    }

    // a bought vehicle counts as building, for the money checks
    internal static void VehicleBought(int vehicle)
    {
        if (Sender == 0) return;
        try
        {
            var offer = MoonWorld.GarageOfferFor(vehicle);
            if (offer == null) return;
            double price = Decimal.ToDouble(offer.price);
            AntiCheat.Built(Sender, price, 0, 0, 0);
            Plugin.L.LogInfo($"{Players.Name(Sender)} bought vehicle {vehicle} at the garage (${price:#,0}).");
        }
        catch (Exception e) { Plugin.L.LogWarning("Pricing a crew vehicle: " + e.Message); }
    }

    // a count, then that many items (null items are skipped, as the game does)
    static List<T> ReadList<T>(BinaryReader r, Func<BinaryReader, T> read) where T : class
    {
        var list = new List<T>();
        for (int i = 0, n = r.ReadInt32(); i < n; i++) { var x = read(r); if (x != null) list.Add(x); }
        return list;
    }

    static List<int> ReadIds(BinaryReader r)
    {
        var list = new List<int>();
        for (int i = 0, n = r.ReadInt32(); i < n; i++) list.Add(r.ReadInt32());
        return list;
    }

    // null writes an empty list (that half taken out)
    static void WriteList<T>(BinaryWriter w, List<T> list, Action<T> write)
    {
        w.Write(list?.Count ?? 0);
        if (list != null) foreach (var x in list) write(x);
    }

    // what this message builds (new structures at their price; new belts are priced in BuildingDone) and removes (refunds)
    static void Price(MoonWorld world, List<MoonDepotSnapshot> depots, List<int> removed, List<MoonConveyorSnapshot> belts, List<int> cut)
    {
        float now = Time.unscaledTime; int fromMemory = 0, unknown = 0, newBelts = 0;
        Forget(recentPieces, now); Forget(recentBelts, now);
        try
        {
            if (depots != null)
                foreach (var d in depots)
                {
                    if (world.depots.ContainsKey(d.id)) continue;               // already there: a move, not a build
                    var def = world.BuildDefinition(d.kind);
                    if (def != null) { addedPrice += def.priceDollars; recentPieces[d.id] = (now, Refund(def.priceDollars)); }
                    addedCount++;
                }
            if (removed != null)
                foreach (int id in removed)
                {
                    if (world.depots.ContainsKey(id)) removedRefund += Decimal.ToDouble(world.StructureRefund(id));
                    else if (recentPieces.TryGetValue(id, out var r)) { removedRefund += r.Refund; fromMemory++; }
                    else { unknown++; continue; }
                    removedCount++;
                }
            // a new belt at the game's own price for its length, from the message - also when it then fails here (its
            // cost still comes: two "$50 with nothing built" bans, 2026-11-09 and -10)
            if (belts != null)
                foreach (var b in belts)
                {
                    if (world.conveyors.ContainsKey(b.id)) continue;
                    try
                    {
                        double price = Decimal.ToDouble(MoonConveyorKit.Load().Price(world.ConveyorPath(b).Length));
                        addedPrice += price; recentBelts[b.id] = (now, Refund(price)); newBelts++;
                    }
                    catch (Exception e) { Plugin.L.LogWarning("Pricing a crew belt: " + e.Message); }
                }
            if (cut != null)
                foreach (int id in cut)
                {
                    if (world.conveyors.ContainsKey(id)) removedRefund += Decimal.ToDouble(world.ConveyorRefund(id));
                    else if (recentBelts.TryGetValue(id, out var r)) { removedRefund += r.Refund; fromMemory++; }
                    else unknown++;
                }
        }
        catch (Exception e) { Plugin.L.LogWarning("Pricing a crew building message: " + e.Message); }
        Trace.Log($"building from {Sender}: {addedCount} pieces + {newBelts} belts ${addedPrice:0.##}; " +
                  $"removed {removed?.Count ?? 0} pieces + {cut?.Count ?? 0} belts, refund ${removedRefund:0.##}" +
                  (fromMemory > 0 ? $" ({fromMemory} already gone, priced from memory)" : "") + (unknown > 0 ? $" ({unknown} unknown here)" : ""));
    }

    static double Refund(double price) => Decimal.ToDouble(MoonWorld.Refund(new Decimal(price)));
    static void Forget(Dictionary<int, (float At, double Refund)> d, float now)
    {
        if (d.Count == 0) return;
        foreach (var k in new List<int>(d.Keys)) if (now - d[k].At > Remember) d.Remove(k);
    }
}
