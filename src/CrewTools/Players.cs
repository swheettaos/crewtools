// Players: everything kept about each crew-mate, in one record and one file (BepInEx/config/CrewTools.players.txt).
//
//   switches   Build, Demolish, Sell, Shop, Capsules - each on or off (the permission switches, from Crew Permissions
//              by tylerun, nexusmods.com/moonsweepers/mods/71; Guard checks them on every crew message)
//   trusted    no spending cap (Spending)
//   passes     protected bases they may work in anyway (Zones)
//   hidden     removed from the window's History list by the host; shown again when they come back
//
// A player seen for the first time starts with the new-player switches (Rules tab). Anyone already known keeps exactly
// the switches they have; the window marks each switch that differs from the default (*).
//
// Before 1.0.0 this was two files: the switches in CrewPermissions.players.txt (from the original mod) and names, trust,
// passes in CrewTools.players.txt (earlier Crew Tools builds). The first load reads both into the new file; the old
// CrewTools.players.txt is kept as CrewTools.players.old.txt and CrewPermissions.players.txt is left as it was.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using MoonCleaners;
using UnityEngine;

namespace CrewTools;

internal static class Players
{
    internal sealed class Player
    {
        public ulong Id;
        public string Name = "";
        public readonly HashSet<Node> On = new();
        public readonly HashSet<string> Passes = new();
        public bool Trusted, Hidden;
    }

    internal static readonly Node[] Nodes = (Node[])Enum.GetValues(typeof(Node));

    static readonly Dictionary<ulong, Player> players = new();

    internal static IEnumerable<Player> All => players.Values;

    // a player's record; the first time they're seen, made with the new-player switches
    internal static Player Get(ulong id)
    {
        if (players.TryGetValue(id, out var p)) return p;
        players[id] = p = new Player { Id = id, Name = LobbyName(id) ?? id.ToString() };
        SetDefaults(p);
        Trace.Log($"new player record {id}");
        Plugin.L.LogInfo($"{p.Name}: new player, default switches ({Describe(p)})");
        return p;
    }

    // the new-player switch for this node (Rules tab)
    internal static bool Default(Node node) => Plugin.StartWith[node].Value;

    // Manage > Reset to defaults (and new players): their switches set to the new-player ones
    internal static void SetDefaults(Player p)
    {
        p.On.Clear();
        foreach (var n in Nodes) if (Default(n)) p.On.Add(n);
        Save();
    }

    static string Describe(Player p) => string.Join(", ", Nodes.Select(n => n.ToString().ToLowerInvariant() + (p.On.Contains(n) ? " on" : " off")));

    // their name: as last seen in the game, else from the Steam lobby, else their Steam ID
    internal static string Name(ulong id)
    {
        if (players.TryGetValue(id, out var p) && !string.IsNullOrEmpty(p.Name) && p.Name != id.ToString()) return p.Name;
        return LobbyName(id) ?? id.ToString();
    }

    static string LobbyName(ulong id)
    {
        try { var n = MoonSteamLobby.PlayerName(id); return string.IsNullOrEmpty(n) ? null : n; }
        catch { return null; }
    }

    // ------------------------------------------------------------------ permission switches

    internal static bool Switch(Player p, Node node) => p.On.Contains(node);

    // the switch, then Crew Tools' rules for the action being handled: protected bases and the spending cap (Zones.Apply)
    internal static bool Allowed(Player p, Node node)
    {
        bool allowed = Switch(p, node);
        Zones.Apply(p, node, ref allowed);
        return allowed;
    }

    internal static void SetSwitch(ulong id, Node node, bool on)
    {
        var p = Get(id);
        if (on) p.On.Add(node); else p.On.Remove(node);
        Save();
    }

    // every switch off (a cheating catch, or Restrict). True if anything was on.
    internal static bool RevokeAll(ulong id)
    {
        var p = Get(id);
        bool hadAny = p.On.Count > 0;
        p.On.Clear();
        Save();
        return hadAny;
    }

    // ------------------------------------------------------------------ trust and passes

    internal static bool Trusted(ulong id) => players.TryGetValue(id, out var p) && p.Trusted;
    internal static void SetTrusted(ulong id, bool on) { Get(id).Trusted = on; Save(); }

    internal static bool HasPass(ulong id, string bse) => players.TryGetValue(id, out var p) && p.Passes.Contains(bse);
    internal static void SetPass(ulong id, string bse, bool on)
    {
        var p = Get(id);
        if (on) p.Passes.Add(bse); else p.Passes.Remove(bse);
        Save();
    }

    // passes for bases that are protected right now
    internal static int PassCount(ulong id) =>
        players.TryGetValue(id, out var p) ? p.Passes.Count(b => Plugin.ZoneOn.TryGetValue(b, out var on) && on.Value) : 0;

    // Manage > Remove from list: off the History list, trust and passes forgotten. The record and its switches stay, so
    // a player switched off for cheating doesn't come back as a new player with the default switches.
    internal static void Forget(ulong id)
    {
        if (!players.TryGetValue(id, out var p)) return;
        p.Hidden = true; p.Trusted = false; p.Passes.Clear();
        Spending.Forget(id);
        Save();
    }

    // ------------------------------------------------------------------ once a second (Driver)
    // Every crew-mate in the game has a record (a new one gets the default switches, in Get) with their name as the game
    // shows it now; one removed from the History list is listed again.
    static float nextLook;
    internal static void Update()
    {
        if (Time.unscaledTime < nextLook) return; nextLook = Time.unscaledTime + 1f;
        if (!Crew.Hosting) return;
        bool changed = false;
        foreach (var (id, seen) in Crew.Now)
        {
            var p = Get(id);
            if (!string.IsNullOrEmpty(seen.name) && p.Name != seen.name) { p.Name = seen.name; changed = true; }
            if (p.Hidden) { p.Hidden = false; changed = true; }
        }
        if (changed) Save();
    }

    // ------------------------------------------------------------------ the file
    // One line a player:  Steam ID | name | +build +sell ... | trusted hidden | South North

    const string Header = "# Crew Tools players: Steam ID | name | switches on | trusted, hidden | base passes";
    static string FilePath => Path.Combine(Paths.ConfigPath, "CrewTools.players.txt");

    internal static void Load()
    {
        players.Clear();
        try
        {
            if (File.Exists(FilePath) && (File.ReadLines(FilePath).FirstOrDefault() ?? "").StartsWith("# Crew Tools players:")) LoadFile();
            else { Migrate(); Save(); }
        }
        catch (Exception e) { Plugin.L.LogError("players not read: " + e.Message); }
        Plugin.L.LogInfo($"{players.Count} player(s) known.");
    }

    static void LoadFile()
    {
        foreach (string raw in File.ReadAllLines(FilePath))
        {
            if (raw.StartsWith("#") || raw.Trim().Length == 0) continue;
            string[] f = raw.Split('|');
            if (f.Length < 5 || !ulong.TryParse(f[0].Trim(), out var id)) continue;
            var p = new Player { Id = id, Name = f[1].Trim() };
            foreach (var s in Words(f[2])) if (s.StartsWith("+") && Enum.TryParse<Node>(s.Substring(1), true, out var n)) p.On.Add(n);
            var flags = Words(f[3]);
            p.Trusted = flags.Contains("trusted"); p.Hidden = flags.Contains("hidden");
            foreach (var b in Words(f[4])) p.Passes.Add(b);
            players[id] = p;
        }
    }

    static string[] Words(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // before 1.0.0: CrewPermissions.players.txt ("id | name | role | +node ...") and CrewTools.players.txt (tab lines:
    // name / trusted / pass). Either, both or neither may be there (Crew Permissions alone, earlier Crew Tools builds,
    // or a fresh install).
    static void Migrate()
    {
        string perms = Path.Combine(Paths.ConfigPath, "CrewPermissions.players.txt");
        bool hadPerms = File.Exists(perms), hadOurs = File.Exists(FilePath);
        if (hadPerms)
            foreach (string raw in File.ReadAllLines(perms))
            {
                if (raw.StartsWith("#")) continue;
                string[] f = raw.Split('|');
                if (f.Length < 3 || !ulong.TryParse(f[0].Trim(), out var id)) continue;
                var p = Record(id); p.Name = f[1].Trim();              // known: their switches as they were
                if (f.Length > 3)
                    foreach (var s in Words(f[3])) if (s.StartsWith("+") && Enum.TryParse<Node>(s.Substring(1), true, out var n)) p.On.Add(n);
            }
        if (hadOurs)
        {
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                var f = raw.Split('\t'); if (f.Length < 2 || !ulong.TryParse(f[1], out var id)) continue;
                var p = Record(id);
                if (f[0] == "trusted") p.Trusted = true;
                else if (f[0] == "pass" && f.Length >= 3) p.Passes.Add(f[2]);
                else if (f[0] == "name" && f.Length >= 3) p.Name = f[2];
            }
            File.Copy(FilePath, Path.Combine(Paths.ConfigPath, "CrewTools.players.old.txt"), true);
        }
        if (hadPerms || hadOurs)
            Plugin.L.LogInfo($"players: {players.Count} moved into CrewTools.players.txt from " +
                             string.Join(" and ", new[] { hadPerms ? "CrewPermissions.players.txt" : null, hadOurs ? "the old CrewTools.players.txt (kept as CrewTools.players.old.txt)" : null }.Where(x => x != null)));
        else Plugin.L.LogInfo("players: new CrewTools.players.txt (no earlier player files)");
    }

    static Player Record(ulong id)
    {
        if (!players.TryGetValue(id, out var p)) players[id] = p = new Player { Id = id };
        return p;
    }

    internal static void Save()
    {
        try
        {
            var lines = new List<string> { Header };
            foreach (var p in players.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                string on = string.Join(" ", Nodes.Where(p.On.Contains).Select(n => "+" + n.ToString().ToLowerInvariant()));
                var flags = new List<string>();
                if (p.Trusted) flags.Add("trusted"); if (p.Hidden) flags.Add("hidden");
                lines.Add($"{p.Id} | {Clean(p.Name)} | {on} | {string.Join(" ", flags)} | {string.Join(" ", p.Passes)}");
            }
            File.WriteAllLines(FilePath, lines);
        }
        catch (Exception e) { Plugin.L.LogWarning("players not saved: " + e.Message); }
    }

    static string Clean(string name) => (name ?? "").Replace("|", "/").Replace("\n", " ").Trim();
}
