using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

// Cleanup: loose capsules every N minutes (Cleanup), and every removal spread over frames (Remover).
// Capsules and bricks lying on the ground or stuck in the air go; anything on a belt, carried or flying stays.
// Crew chat warns a minute before.
internal static class Cleanup
{
    static float next = -1f; static bool warned;
    internal static void Update()
    {
        if (!Plugin.CleanupOn.Value || Crew.World() == null) return;
        float now = Time.unscaledTime, every = Plugin.CleanupMinutes.Value * 60f;
        if (next > now + every) { next = now + every; warned = false; }                  // made shorter in the panel
        if (next < 0) { next = now + every; warned = false; return; }
        if (!warned && now >= next - 60f) { warned = true; if (MoonSteamLobby.InLobby) Chat.Say("Cleanup in 1 minute: loose capsules on the ground will be removed."); }
        if (now < next) return;
        next = now + every; warned = false;
        int n = Run();
        Plugin.L.LogInfo($"cleanup: {n} loose capsules removed");
        if (MoonSteamLobby.InLobby && n > 0) Chat.Say($"Cleanup: removing {n} loose capsules.");
    }
    // HTTP: the next cleanup in one minute (the usual warning in crew chat goes out now)
    internal static void Soon() { next = Time.unscaledTime + 60f; warned = false; }

    internal static int Run()
    {
        var w = Crew.World(); if (w == null) return 0;
        int n = 0;
        foreach (var kv in w.canisters)
        {
            int id = kv.Key;
            try { if (w.IsCanisterRiding(id) || w.IsCanisterCarried(id) || w.IsCanisterFlying(id)) continue; } catch { continue; }
            Remover.Capsule(id); n++;                               // removed a few a frame, not all at once
        }
        return n;
    }
}


// Remover: removals spread out so the game never hitches. Measured 2026-11-09: the game's RemoveStructureNow costs ~15 ms when the last removal was 0.25 s+ ago, but 100-130 ms
// when it comes straight after another (it finishes the previous one's background work on the spot) - removing a
// banned player's 100 pieces in one go would freeze the hosted game for ~12 s. So: one structure every 0.25 s, its
// refund paid as it goes; loose capsules 10 a frame (~2 ms each).
internal static class Remover
{
    static readonly Queue<int> structures = new(), capsules = new();
    static float nextAt; static double paid; static int done;
    internal static void Structure(int id) => structures.Enqueue(id);
    // Handover: a ban reclaim in progress carries on in the next copy
    internal static (int[] structures, int[] capsules, double paid, int done) Export() => (structures.ToArray(), capsules.ToArray(), paid, done);
    internal static void Import((int[] structures, int[] capsules, double paid, int done) s)
    {
        foreach (var i in s.structures) structures.Enqueue(i);
        foreach (var i in s.capsules) capsules.Enqueue(i);
        paid = s.paid; done = s.done;
    }
    internal static void Capsule(int id) => capsules.Enqueue(id);
    internal static void Update()
    {
        if (structures.Count == 0 && capsules.Count == 0) return;
        var w = Crew.World(); if (w == null) { structures.Clear(); capsules.Clear(); return; }
        for (int i = 0; i < (Plugin.FastRemoval.Value ? 20 : 10) && capsules.Count > 0; i++)        // ~2 ms each (measured)
        {
            int id = capsules.Dequeue();
            try
            {
                if (!w.canisters.ContainsKey(id) || w.IsCanisterRiding(id) || w.IsCanisterCarried(id) || w.IsCanisterFlying(id)) continue;
                w.RemoveCanister(id);
            }
            catch { }
        }
        if (structures.Count == 0 || Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + (Plugin.FastRemoval.Value ? 0.1f : 0.25f);
        int s = structures.Dequeue();
        try
        {
            if (w.depots.ContainsKey(s))
            {
                double r = 0; try { r = Il2CppSystem.Decimal.ToDouble(w.StructureRefund(s)); } catch { }
                w.RemoveStructureNow(s); done++;
                if (r > 0) { Owners.Pay(w, r); paid += r; }
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("remove " + s + ": " + e.Message); }
        if (structures.Count == 0) { Plugin.L.LogInfo($"reclaim finished: {done} structures removed, ${paid:0} refunded"); done = 0; paid = 0; }
    }
}
