// Hooks: every place Crew Tools hooks into the game, one class per game method, nothing else. Each hook only calls the
// modules that care (Guard for permissions, AntiCheat, Spending, Owners, Zones...), in a fixed order, so no two hooks
// ever sit on the same game method. Plugin.Load patches every class here; one that can't be patched is logged and
// skipped. The hooks that came with Crew Permissions (tylerun) are marked "(Crew Permissions)".
using System.Reflection;
using HarmonyLib;
using Il2CppSystem.IO;
using MoonCleaners;
using UnityEngine;
using Decimal = Il2CppSystem.Decimal;

namespace CrewTools;

internal static class Hooks
{
    // ================================================================== who sent each crew message (Crew Permissions)

    [HarmonyPatch(typeof(MoonCrewSession), "HandleWorld")]
    static class World
    {
        static void Prefix(ulong sender, out ulong __state) => __state = Guard.Enter(sender, "HandleWorld");
        static void Postfix(ulong __state) => Guard.Leave(__state);
    }

    [HarmonyPatch(typeof(MoonCrewSession), "HandleRobots")]
    static class Robots
    {
        static void Prefix(ulong sender, out ulong __state) => __state = Guard.Enter(sender, "HandleRobots");
        static void Postfix(ulong __state) => Guard.Leave(__state);
    }

    [HarmonyPatch(typeof(MoonCrewSession), "HandleWaypoints")]
    static class Waypoints
    {
        static void Prefix(ulong sender, out ulong __state) => __state = Guard.Enter(sender, "HandleWaypoints");
        static void Postfix(ulong __state) => Guard.Leave(__state);
    }

    [HarmonyPatch(typeof(MoonCrewSession), "HandleLandmarks")]
    static class Landmarks
    {
        static void Prefix(ulong sender, out ulong __state) => __state = Guard.Enter(sender, "HandleLandmarks");
        static void Postfix(ulong __state) => Guard.Leave(__state);
    }

    // Crew Tools: vehicle messages (driving, parking, recalling) always go through - the original refused the whole
    // message with Shop off, so a player couldn't drive what they own. Buying and upgrading are checked below.
    [HarmonyPatch(typeof(MoonCrewSession), "HandleOwnedVehicles")]
    static class Vehicles
    {
        static void Prefix(ulong sender, out ulong __state) => __state = Guard.Enter(sender, "HandleOwnedVehicles");
        static void Postfix(ulong __state) => Guard.Leave(__state);
    }

    // ================================================================== money

    // every change to a wallet: the money log and checks (host only), and the spending cap (a crew-mate's action)
    [HarmonyPatch(typeof(MoonWorld.TeamWallet), "set_Item")]
    static class Wallet
    {
        static void Prefix(MoonWorld.TeamWallet __instance, string player, out double __state)
        {
            __state = double.NaN;
            try { __state = Decimal.ToDouble(__instance.get_Item(player)); } catch { }
        }

        static void Postfix(MoonWorld.TeamWallet __instance, string player, double __state)
        {
            if (double.IsNaN(__state)) return;
            double after;
            try { after = Decimal.ToDouble(__instance.get_Item(player)); } catch { return; }
            if (!MoonSteamLobby.InLobby || MoonSteamLobby.IsHost)
                try { AntiCheat.MoneyChanged(__state, after); } catch (System.Exception e) { Plugin.L.LogWarning("Money audit: " + e.Message); }
            Spending.Spent(Guard.Sender, __state - after);
        }
    }

    // building money: a demolition refund needs Demolish, a building cost needs Build (Crew Permissions)
    [HarmonyPatch(typeof(MoonWorld), "CrewBuildMoney")]
    static class BuildMoney
    {
        static bool Prefix(string player, Decimal amount)
        {
            bool allowed = Decimal.ToDouble(amount) > 0 ? Guard.Allow(Node.Demolish, "demolition refunds") : Guard.Allow(Node.Build, "building costs");
            return allowed && AntiCheat.OwnWallet(Guard.Sender, player, "building money") && AntiCheat.BuildMoney(Guard.Sender, player, amount);
        }
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewTrickMoney")]
    static class Trick
    {
        static bool Prefix(string player, Decimal dollars) =>
            Guard.Pays("trick money") && AntiCheat.OwnWallet(Guard.Sender, player, "trick money") && AntiCheat.TrickMoney(Guard.Sender, dollars);
    }

    // ================================================================== building

    // a building message: refuse the halves they may not do (Guard), price it (AntiCheat), note who built what (Owners)
    [HarmonyPatch(typeof(MoonWorld), "ApplyCrewStructures")]
    static class Structures
    {
        static void Prefix(MoonWorld __instance, ref BinaryReader r)
        {
            Guard.FilterBuilding(__instance, ref r);
            Owners.Before(__instance);
        }

        static void Postfix(MoonWorld __instance)
        {
            Guard.BuildingDone(__instance);
            Owners.After(__instance);
        }
    }

    // ================================================================== vehicles

    // buying: Shop switch, protected base, spending cap (Zones); a bought vehicle counts as building for the money checks
    [HarmonyPatch(typeof(MoonWorld), "CrewVehicleBought")]
    static class VehicleBought
    {
        static bool Prefix(int vehicle)
        {
            if (!Zones.VehicleBuy()) return false;
            Guard.VehicleBought(vehicle);
            return true;
        }
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewVehicleUpgrade")]
    static class VehicleUpgrade
    {
        static bool Prefix() => Zones.VehicleBuy();
    }

    // ================================================================== selling, loading, unloading

    [HarmonyPatch(typeof(MoonWorld), "CrewDeposit")]
    static class Deposit
    {
        static bool Prefix(string player, double kilograms, ref double __result) =>
            Guard.Sell(player, MoonCargo.Dust, kilograms, "selling dust", dustDeposit: true, ref __result);
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewCargoDeposit")]
    static class CargoDeposit
    {
        static bool Prefix(string player, MoonCargo cargo, double kilograms, ref double __result) =>
            Guard.Sell(player, cargo, kilograms, "selling cargo", dustDeposit: false, ref __result);
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewDustBack")]
    static class DustBack
    {
        static bool Prefix(double kilograms) => Guard.Allow(Node.Sell, "dust refunds") && AntiCheat.Kilograms(Guard.Sender, kilograms, "dust refunds");
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewPackagerLoad")]
    static class PackagerLoad
    {
        static bool Prefix(string player, double kilograms, ref double __result)
        {
            const string what = "loading a packager";
            if (Guard.Pays(what) && AntiCheat.OwnWallet(Guard.Sender, player, what) && AntiCheat.Kilograms(Guard.Sender, kilograms, what)) return true;
            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewProcessorLoad")]
    static class ProcessorLoad
    {
        static bool Prefix(double kilograms) => Guard.Pays("loading a machine") && AntiCheat.Kilograms(Guard.Sender, kilograms, "loading a machine");
    }

    [HarmonyPatch(typeof(MoonWorld), "CrewGarageUnload")]
    static class GarageUnload
    {
        static bool Prefix(double kilograms)
        {
            if (!Guard.Pays("unloading into a garage") || !AntiCheat.Kilograms(Guard.Sender, kilograms, "unloading into a garage")) return false;
            AntiCheat.CheckDeposit(kilograms, atStation: false, dust: true);
            return true;
        }
    }

    // ================================================================== shop and capsules

    // the Shop switch, their own wallet, and the price against their spending cap
    [HarmonyPatch(typeof(MoonWorld), "CrewPurchase")]
    static class Purchase
    {
        static bool Prefix(MoonWorld __instance, string player, object[] __args, ref bool __result)
        {
            if (Guard.Allow(Node.Shop, "buying") && AntiCheat.OwnWallet(Guard.Sender, player, "buying") && Spending.CanAfford(__instance, Guard.Sender, __args)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(MoonWorld), "ApplyCrewCanister")]
    static class Canister
    {
        static bool Prefix(MoonWorld.CanisterOp op, string carrier, ref bool __result)
        {
            if (Guard.Capsule(op, carrier)) return true;
            __result = false;
            return false;
        }
    }

    // ================================================================== actions that pay: always allowed, named for the money checks

    [HarmonyPatch(typeof(MoonWorld), "ApplyCrewMachineStorage")]
    static class MachineStorage { static bool Prefix() => Guard.Pays("machine storage"); }

    [HarmonyPatch(typeof(MoonWorld), "CrewSetRobotOn")]
    static class RobotOn { static bool Prefix() => Guard.Pays("switching robots"); }

    [HarmonyPatch(typeof(MoonWorld), "CrewRadioCommand")]
    static class Radio { static bool Prefix() => Guard.Pays("radio commands"); }

    [HarmonyPatch(typeof(MoonWorld), "CrewWaypointCommand")]
    static class Waypoint { static bool Prefix() => Guard.Pays("robot waypoints"); }

    [HarmonyPatch(typeof(MoonWorld), "ApplyCrewStroke")]
    static class Stroke { static bool Prefix() => Guard.Pays("cleaning"); }

    [HarmonyPatch(typeof(MoonWorld), "CrewSectionCleared")]
    static class SectionCleared { static bool Prefix() => Guard.Pays("section pay"); }

    [HarmonyPatch(typeof(MoonWorld), "CrewRaiseLandmark")]
    static class Landmark { static bool Prefix() => Guard.Pays("raising landmarks"); }

    // ================================================================== players coming and going

    // leaving: their vehicles back to the garage, refunded (Owners), before the game drops them; then the money ledger
    // is forgotten
    [HarmonyPatch(typeof(MoonWorld), "CrewmateLeft")]
    static class Left
    {
        static void Prefix(string player) => Owners.Left(player);
        static void Postfix(string player) { if (ulong.TryParse(player, out var id)) AntiCheat.Forget(id); }
    }

    // a ban: everything they built and their vehicles reclaimed, refunded
    [HarmonyPatch(typeof(MoonSteamLobby), "Ban")]
    static class Ban
    {
        static void Postfix(object[] __args) { if (__args.Length > 0 && __args[0] is ulong id) Owners.Banned(id); }
    }

    // ================================================================== the cheat watch

    // a map teleport was paid for
    [HarmonyPatch(typeof(MoonWorld), "TryPayTeleport")]
    static class TeleportPaid
    {
        static int calls;
        static void Postfix(string player, bool __result) { Trace.First(ref calls, 10, $"TryPayTeleport {player} paid={__result}"); AntiCheat.TeleportPaid(player, __result); }
    }

    // ================================================================== the window

    // while the CREW TOOLS window is open the pause menu underneath takes no keys (Space typed in a name resumed the game)
    [HarmonyPatch(typeof(MoonPauseMenu), "Update")]
    static class PauseKeys { static bool Prefix() => !Ui.IsOpen; }
}
