using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;

namespace CrewTools;

// Crew Tools' answers to HTTP requests, for tools that pass requests on to plugins (Tools.HandleHttp). Reads are GET,
// changes are POST, and each change calls what the CREW TOOLS window's buttons call, so the two always agree. Player
// ids are Steam ids, sent and taken as text (they're too big for some JSON readers as numbers).
//   GET  /crew/players                  everyone known: name, online, switches, trusted, passes, spending used and left
//   GET  /crew/now                      who's in the game now, where, and the protected base they're in (if any)
//   GET  /crew/catches[?lines=40]       unanswered cheating popups, money held for buildings, the cheats log's last lines
//   GET  /crew/settings                 protected bases and sizes, spending cap, cleanup, welcome, server name
//   POST /crew/players/modify?id=       build|demolish|sell|shop|capsules=0|1, trust=0|1, pass=<Base>&passOn=0|1,
//                                       resetSpending=1, restrict=1 (every switch off), defaults=1
//   POST /crew/players/remove?id=[&ban=1]   kick (or ban: what they built is removed and refunded)
//   POST /crew/players/unban?id=
//   POST /crew/settings/modify          zone=<Base>&on=0|1, zone=<Base>&metres=N, cap=N, name=..., cleanup=0|1, cleanupMinutes=N
//   POST /crew/cleanup                  the loose-capsule cleanup in one minute (its usual warning goes out in crew chat now)
internal static class Http
{
    // one line per endpoint, for a host's help list ("METHOD /path?params | what it does")
    internal static readonly string[] Help =
    {
        "GET /crew/players | Everyone known: id (Steam id, as text), name, online, banned, switches, trusted, passes, spent and left of the cap",
        "GET /crew/now | Who's in the game now, where, and the protected base they're in",
        "GET /crew/catches?lines=40 | Unanswered cheating popups, money held for buildings, and the cheats log's last lines",
        "GET /crew/settings | Protected bases and their sizes, the spending cap, cleanup, welcome, server name, new-player switches",
        "POST /crew/players/modify?id= | build|demolish|sell|shop|capsules=0|1, trust=0|1, pass=<Base>&passOn=0|1, resetSpending=1, restrict=1, defaults=1",
        "POST /crew/players/remove?id=&ban=1 | Kick, or ban (what they built is removed and refunded)",
        "POST /crew/players/unban?id= | Unban: they can join again",
        "POST /crew/settings/modify | zone=<Base>&on=0|1, zone=<Base>&metres=N, cap=N, name=, cleanup=0|1, cleanupMinutes=N",
        "POST /crew/cleanup | The loose-capsule cleanup in one minute (its warning goes to crew chat now)",
    };

    static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    static string Json(object o) => JsonSerializer.Serialize(o, Options);
    static string Fail(string why) => Json(new { ok = false, reason = why });

    internal static string Handle(string method, string path, NameValueCollection q)
    {
        if (path != "/crew" && !path.StartsWith("/crew/", StringComparison.Ordinal)) return null;
        bool post = method == "POST";
        switch (path)
        {
            case "/crew/players": return post ? Fail("use GET") : PlayersList();
            case "/crew/now": return post ? Fail("use GET") : Now();
            case "/crew/catches": return post ? Fail("use GET") : Catches(int.TryParse(q["lines"], out var n) ? n : 40);
            case "/crew/settings": return post ? Fail("use GET") : Settings();
            case "/crew/players/modify": return post ? Modify(q) : Fail("use POST");
            case "/crew/players/remove": return post ? Remove(q) : Fail("use POST");
            case "/crew/players/unban": return post ? Unban(q) : Fail("use POST");
            case "/crew/settings/modify": return post ? ModifySettings(q) : Fail("use POST");
            case "/crew/cleanup": return post ? CleanupNow() : Fail("use POST");
        }
        return Json(new { error = "unknown path " + path, paths = new[] { "GET /crew/players", "GET /crew/now", "GET /crew/catches", "GET /crew/settings",
            "POST /crew/players/modify", "POST /crew/players/remove", "POST /crew/players/unban", "POST /crew/settings/modify", "POST /crew/cleanup" } });
    }

    static object PlayerJson(Players.Player p) => new
    {
        id = p.Id.ToString(), name = p.Name, online = Crew.Online(p.Id), trusted = p.Trusted, banned = Banned(p.Id),
        switches = PerNode(n => Players.Switch(p, n)),
        passes = p.Passes.OrderBy(x => x).ToArray(),
        spent = Math.Round(Spending.Used(p.Id), 2), left = p.Trusted ? (double?)null : Math.Round(Spending.Left(p.Id), 2),
    };

    static string PlayersList() => Json(new { cap = Plugin.Cap.Value, players = Players.All.Where(p => !p.Hidden).OrderBy(p => p.Name).Select(PlayerJson).ToArray() });

    static string Now() => Json(new
    {
        hosting = Crew.Hosting,
        players = Crew.Now.Select(kv =>
        {
            string inBase = Zones.ProtectedBaseAt(kv.Key, kv.Value.pos, out var b, out _) ? b : null;
            return new { id = kv.Key.ToString(), name = kv.Value.name, pos = new[] { kv.Value.pos.x, kv.Value.pos.y, kv.Value.pos.z }, protectedBase = inBase };
        }).ToArray(),
    });

    static string Catches(int lines)
    {
        var (held, _) = AntiCheat.Export();
        string[] log = Array.Empty<string>();
        var file = Path.Combine(Paths.BepInExRootPath, "CrewPermissions.cheats.log");
        try { if (File.Exists(file)) { var all = File.ReadAllLines(file); log = all.Skip(Math.Max(0, all.Length - Math.Clamp(lines, 0, 500))).ToArray(); } } catch { }
        return Json(new
        {
            popups = Alerts.Export().Select(a => new { id = a.Sender.ToString(), name = a.Name, what = a.What, count = a.Count, decided = a.Banned }).ToArray(),
            held = held.Select(h => new { id = h.Sender.ToString(), name = Players.Name(h.Sender), dollars = h.Dollars }).ToArray(),
            log,
        });
    }

    static string Settings() => Json(new
    {
        zones = ZoneSettings(),
        cap = Plugin.Cap.Value, cleanup = Plugin.CleanupOn.Value, cleanupMinutes = Plugin.CleanupMinutes.Value,
        welcome = Plugin.WelcomeOn.Value, welcomeText = Plugin.WelcomeText.Value, name = Plugin.SavedName.Value,
        newPlayers = PerNode(n => Plugin.StartWith[n].Value),
    });

    // dictionaries built with loops (ToDictionary with lambdas needs a compiler attribute the game's libraries hide)
    static Dictionary<string, bool> PerNode(Func<Node, bool> f)
    {
        var d = new Dictionary<string, bool>();
        foreach (var n in Players.Nodes) d[n.ToString().ToLowerInvariant()] = f(n);
        return d;
    }
    static Dictionary<string, object> ZoneSettings()
    {
        var d = new Dictionary<string, object>();
        foreach (var b in Plugin.Bases) d[b] = new { on = Plugin.ZoneOn[b].Value, metres = Plugin.ZoneMetres[b].Value };
        return d;
    }

    static bool Id(NameValueCollection q, out ulong id, out string why)
    {
        why = null;
        if (!ulong.TryParse(q["id"], out id)) { why = "id= is required (a Steam id, as /crew/players gives it)"; return false; }
        ulong want = id;
        if (!Players.All.Any(p => p.Id == want) && !Crew.Online(id)) { why = "no player " + q["id"]; return false; }
        return true;
    }

    static string Modify(NameValueCollection q)
    {
        if (!Id(q, out var id, out var why)) return Fail(why);
        var done = new List<string>();
        foreach (var n in Players.Nodes)
        {
            var v = q[n.ToString().ToLowerInvariant()];
            if (v != null) { Players.SetSwitch(id, n, v == "1"); done.Add($"{n} {(v == "1" ? "on" : "off")}"); }
        }
        if (q["trust"] != null) { Players.SetTrusted(id, q["trust"] == "1"); done.Add(q["trust"] == "1" ? "trusted" : "not trusted"); }
        if (q["pass"] != null)
        {
            var b = Plugin.Bases.FirstOrDefault(x => x.Equals(q["pass"], StringComparison.OrdinalIgnoreCase));
            if (b == null) return Fail("pass= is one of " + string.Join(", ", Plugin.Bases));
            Players.SetPass(id, b, q["passOn"] != "0"); done.Add($"{b} pass {(q["passOn"] != "0" ? "given" : "taken")}");
        }
        if (q["resetSpending"] == "1") { Spending.ResetBank(id); done.Add("spending reset"); }
        if (q["restrict"] == "1") { Players.RevokeAll(id); done.Add("every switch off"); }
        if (q["defaults"] == "1") { Players.SetDefaults(Players.Get(id)); Players.Save(); done.Add("new-player defaults"); }
        if (done.Count == 0) return Fail("nothing to change: give build|demolish|sell|shop|capsules=0|1, trust, pass, resetSpending, restrict or defaults");
        Plugin.L.LogInfo($"host (HTTP): {Players.Name(id)}: {string.Join(", ", done)}");
        return Json(new { ok = true, done, player = PlayerJson(Players.Get(id)) });
    }

    static string Remove(NameValueCollection q)
    {
        if (!Id(q, out var id, out var why)) return Fail(why);
        if (!Crew.Hosting) return Fail("only while you host a game");
        if (!Crew.Online(id)) return Fail(Players.Name(id) + " isn't in the game right now");
        bool ban = q["ban"] == "1";
        AntiCheat.Remove(id, ban ? "banned by the host" : "kicked by the host", ban);
        return Json(new { ok = true, done = ban ? "banned (what they built is being removed and refunded)" : "kicked (they can rejoin)" });
    }

    static string Unban(NameValueCollection q)
    {
        if (!ulong.TryParse(q["id"], out var id)) return Fail("id= is required");
        if (!Banned(id)) return Fail(Players.Name(id) + " isn't banned");
        try { MoonCleaners.MoonSteamLobby.Unban(id); } catch (Exception e) { return Fail("couldn't unban: " + e.Message); }
        Plugin.L.LogInfo($"host (HTTP) unbanned {Players.Name(id)} ({id})");
        return Json(new { ok = true, done = "unbanned: they can join again" });
    }

    static string ModifySettings(NameValueCollection q)
    {
        var done = new List<string>();
        if (q["zone"] != null)
        {
            var b = Plugin.Bases.FirstOrDefault(x => x.Equals(q["zone"], StringComparison.OrdinalIgnoreCase));
            if (b == null) return Fail("zone= is one of " + string.Join(", ", Plugin.Bases));
            if (q["on"] != null) { Plugin.ZoneOn[b].Value = q["on"] == "1"; done.Add($"{b} protected {(q["on"] == "1" ? "on" : "off")}"); }
            if (float.TryParse(q["metres"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0)
            { Plugin.ZoneMetres[b].Value = m; done.Add($"{b} zone {m:0} m"); }
        }
        if (float.TryParse(q["cap"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cap) && cap >= 0)
        { Plugin.Cap.Value = cap; done.Add($"cap ${cap:0} per 30 min"); }
        if (q["name"] != null) { ServerName.Set(q["name"]); done.Add("server name set"); }
        if (q["cleanup"] != null) { Plugin.CleanupOn.Value = q["cleanup"] == "1"; done.Add("cleanup " + (q["cleanup"] == "1" ? "on" : "off")); }
        if (float.TryParse(q["cleanupMinutes"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cm) && cm >= 1)
        { Plugin.CleanupMinutes.Value = cm; done.Add($"cleanup every {cm:0} min"); }
        if (done.Count == 0) return Fail("nothing to change: give zone=&on= or zone=&metres=, cap=, name=, cleanup=, cleanupMinutes=");
        Plugin.L.LogInfo("host (HTTP) settings: " + string.Join(", ", done));
        return Json(new { ok = true, done });
    }

    static string CleanupNow()
    {
        if (!Crew.Hosting) return Fail("only while you host a game");
        if (!Plugin.CleanupOn.Value) return Fail("the cleanup is off (settings: cleanup=1)");
        Cleanup.Soon();
        return Json(new { ok = true, done = "cleanup in 1 minute; the warning is in crew chat now" });
    }

    static bool Banned(ulong id) { try { return MoonCleaners.MoonCrewBans.IsBanned(id); } catch { return false; } }
}
