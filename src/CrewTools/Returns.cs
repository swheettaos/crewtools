// Returns: a sale refused for lack of the Sell permission gives the capsule back, dropped beside the player.
//
// From Crew Permissions by tylerun (nexusmods.com/moonsweepers/mods/71), rebuilt from the released DLL and tidied for
// Crew Tools. Guard queues the refusal; the capsule is made on the next frame (outside the crew message).
using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

internal static class Returns
{
    static readonly List<(ulong Sender, MoonCargo Cargo, double Kilograms)> queue = new();


    internal static void GiveBack(ulong sender, MoonCargo cargo, double kilograms)
    {
        if (sender != 0 && kilograms > 0) queue.Add((sender, cargo, kilograms));
    }

    internal static void Update()
    {
        if (queue.Count == 0) return;
        var now = queue.ToArray();
        queue.Clear();
        foreach (var (sender, cargo, kilograms) in now)
        {
            try { Drop(sender, cargo, kilograms); }
            catch (Exception e) { Plugin.L.LogError($"Couldn't give {Players.Name(sender)}'s capsule back: {e}"); }
        }
    }

    // a new dropped capsule 1.5 m in front of the player, 0.6 m up, sent to everyone like any dropped capsule
    static void Drop(ulong sender, MoonCargo cargo, double kilograms)
    {
        var w = Crew.World();
        if (w == null) return;
        var seller = Seller(sender);
        if (seller == null)
        {
            Plugin.L.LogWarning($"Refused sale: couldn't find {Players.Name(sender)} to give the capsule back ({kilograms:0.#} kg of {cargo}).");
            return;
        }
        var moon = w.transform;
        Vector3 at = seller.position;
        Vector3 up = (at - moon.position).normalized;                         // away from the moon's centre
        Vector3 ahead = Vector3.ProjectOnPlane(seller.forward, up);
        if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.ProjectOnPlane(seller.right, up);
        Vector3 local = moon.InverseTransformPoint(at + ahead.normalized * 1.5f + up * 0.6f);
        Vector3 localUp = local.normalized;

        int id = w.nextCanisterId++;
        var capsule = new MoonDroppedCanister
        {
            id = id, owner = "", kilograms = kilograms, capacity = kilograms, cargo = cargo,
            position = local, up = localUp, rotation = Quaternion.FromToRotation(Vector3.up, localUp),
            physicalPose = false, createdAt = Time.time,
        };
        if (!w.ApplyCrewCanister(MoonWorld.CanisterOp.Upsert, capsule, ""))
        {
            Plugin.L.LogWarning($"Refused sale: the game wouldn't place {Players.Name(sender)}'s capsule back.");
            return;
        }
        if (w.TryGetCanister(id, out var placed) && placed != null) capsule = placed;
        w.RaiseLocalCanister(MoonWorld.CanisterOp.Upsert, MoonCrew.LocalPlayerId, capsule);
        Plugin.L.LogInfo($"Refused sale: gave {Players.Name(sender)} their capsule back ({kilograms:0.#} kg of {cargo}, id {id}).");
    }

    static Transform Seller(ulong sender)
    {
        var session = MoonCrewSession.Instance;
        if (session == null || session.remotes == null) return null;
        return session.remotes.TryGetValue(sender, out var r) && r != null ? r.transform : null;
    }
}
