// AntiCheat: everything that catches a cheating crew-mate, and what happens when one is caught.
//
//   Money   (from Crew Permissions by tylerun, nexusmods.com/moonsweepers/mods/71 - rebuilt from the released DLL and
//           tidied for Crew Tools): a crew-mate's game sends "building money" (a cost or a refund) separately from the
//           building. Each player has a ledger of what they really built or removed in the last two minutes (priced by
//           Guard); building money is paid only when the ledger covers it or it matches a catalog price, otherwise it
//           is held for its building (paid the moment it is covered). Crew Tools, after three false bans
//           (2026-11-09 and -10): what was built and removed is priced from the message itself (belts too) and pieces
//           already gone are priced from memory (Guard), and money that still matches nothing is judged by whether a
//           building message came with it. A real build or delete always sends one; a money cheat sends money alone.
//             cost (money taken)    not paid, flagged - never cheating: it gains a cheater nothing
//             refund, with a building message from them within MatchSeconds   not paid, flagged; cheating once these
//                                   add up past MaxUnmatchedRefunds in 10 minutes
//             refund, no building message from them within MatchSeconds either side   cheating
//           Money no build could be (MaxBuildMoney), trick money over its limit and impossible kilograms: cheating.
//   Watch   (Crew Tools): what cheat trainers (WeMod and the like) do to the host's world, from what the cheater's
//           game sends - the host can't see the program itself. WeMod's Moon Sweepers cheats, mapped to what reaches
//           the host: Super Speed / Vehicle Speed (speed), Fly Mode / Unlimited Jetpack (flying), Long Reach (reach),
//           Carry Capacity / Super Cleaning (deposits over capacity), Instant Deposit (selling from far away),
//           Teleport to Map Marker (a jump that doesn't land at a station, or isn't paid for). Money cheats are the
//           Money checks; Fast Machines and Freeze Time of Day only change the cheater's own game.
//   Caught  either kind: refused, every permission switched off, then Ban / Kick / Restrict at once, or the host is
//           asked (the Alerts popup). Logged to BepInEx/CrewPermissions.cheats.log (file name kept from the original).
//
// Never counted, so it doesn't fight the protected bases: anything a permission or a zone refused (a refused action
// never runs, so it is never looked at), money and deposits within 10 s of a refusal, the host, and the first 20 s
// after a player appears. Limits are generous on purpose - only blatant cheating trips them - and all are settings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using MoonCleaners;
using UnityEngine;
using Decimal = Il2CppSystem.Decimal;

namespace CrewTools;

internal static class AntiCheat
{
    // ================================================================== settings: [AntiCheat]
    internal static ConfigEntry<bool> Enabled, AutoBan;
    internal static ConfigEntry<string> BanKey, IgnoreKey, Punishment;
    internal static ConfigEntry<double> SlackDollars, MaxBuildMoney, MaxTrickMoney, MaxKilograms, MaxUnmatchedRefunds;
    internal static ConfigEntry<bool> Watch, Speed, Flying, Reach, Deposit, Teleport;
    internal static ConfigEntry<float> FootSpeed, VehicleSpeed, AirSeconds, ReachMetres, DepositFactor, DepositMetres, LandMetres;

    // The first time, the money settings are taken from the old Crew Permissions config (moonsweepers.crewpermissions.cfg)
    // if it's there, so nothing set before is lost.
    internal static void Bind(ConfigFile c)
    {
        var old = new Dictionary<string, string>();
        try
        {
            string f = Path.Combine(Paths.ConfigPath, "moonsweepers.crewpermissions.cfg");
            if (File.Exists(f))
                foreach (var line in File.ReadAllLines(f))
                {
                    int eq = line.IndexOf('='); if (line.StartsWith("#") || eq < 0) continue;
                    old[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
        }
        catch (Exception e) { Plugin.L.LogWarning("old anti-cheat settings not read: " + e.Message); }
        // the cheat watch's settings were under [Cheat watch] in earlier Crew Tools builds: kept, under their new names
        try
        {
            string f = Path.Combine(Paths.ConfigPath, "swheet.moonsweepers.crewtools.cfg"); string section = "";
            if (File.Exists(f))
                foreach (var line in File.ReadAllLines(f))
                {
                    if (line.StartsWith("[")) { section = line.Trim(); continue; }
                    int eq = line.IndexOf('='); if (section != "[Cheat watch]" || line.StartsWith("#") || eq < 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    old["watch:" + (k is "On" ? "Watch" : k is "Speed" or "Flying" or "Reach" or "Deposits" or "Teleports" ? "Watch " + k.ToLowerInvariant() : k)] = v;
                }
        }
        catch (Exception e) { Plugin.L.LogWarning("old cheat watch settings not read: " + e.Message); }
        bool W(string k, bool d) => old.TryGetValue("watch:" + k, out var v) && bool.TryParse(v, out var b) ? b : d;
        float F(string k, float d) => old.TryGetValue("watch:" + k, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : d;
        bool B(string k, bool d) => old.TryGetValue(k, out var v) && bool.TryParse(v, out var b) ? b : d;
        double D(string k, double d) => old.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : d;
        string S(string k, string d) => old.TryGetValue(k, out var v) && v.Length > 0 ? v : d;

        const string A = "AntiCheat";
        Enabled = c.Bind(A, "Enabled", B("Enabled", true), "All of the anti-cheat: the money checks and the cheat watch. Each catch is refused, takes their permissions away and is punished (Punishment, AutoBan).");
        AutoBan = c.Bind(A, "AutoBan", B("AutoBan", false), "Off: a catch pops up for the host, who bans (BanKey) or lets it go (IgnoreKey); their permissions are off either way. On: the punishment happens at once.");
        BanKey = c.Bind(A, "BanKey", S("BanKey", "F7"), "In the popup (AutoBan off): punish the player.");
        IgnoreKey = c.Bind(A, "IgnoreKey", S("IgnoreKey", "F8"), "In the popup: let it go (AutoBan off), or close it.");
        Punishment = c.Bind(A, "Punishment", S("Punishment", "Ban"), "What a cheater gets: Ban, Kick or None (refused, permissions taken away, they stay).");
        SlackDollars = c.Bind(A, "SlackDollars", D("SlackDollars", 1.0), "Money: building money allowed beyond what the host priced a crew-mate's building at (rounding), in dollars.");
        MaxBuildMoney = c.Bind(A, "MaxBuildMoney", D("MaxBuildMoney", 5000000.0), "Money: building money in one message above this is a cheat at once, in dollars.");
        MaxUnmatchedRefunds = c.Bind(A, "MaxUnmatchedRefunds", 20000.0, "Money: refunds that came with a building message but match nothing removed are not paid and only flagged, until they add up past this many dollars in 10 minutes from one player: then it is cheating. A refund with no building message at all is cheating at once.");
        MaxTrickMoney = c.Bind(A, "MaxTrickMoney", D("MaxTrickMoney", 25000.0), "Money: trick money in one message above this is a cheat, in dollars.");
        MaxKilograms = c.Bind(A, "MaxKilograms", D("MaxKilograms", 100000.0), "Money: kilograms in one sale, load or unload above this (or below zero) is a cheat.");
        Watch = c.Bind(A, "Watch", W("Watch", true), "Cheat watch: catch what cheat trainers (WeMod and the like) do to your world.");
        Speed = c.Bind(A, "Watch speed", W("Watch speed", true), "Moving faster than the game allows.");
        FootSpeed = c.Bind(A, "Max speed on foot m/s", F("Max speed on foot m/s", 30f), "Faster than this for 2+ seconds on foot (not in a vehicle).");
        VehicleSpeed = c.Bind(A, "Max speed in a vehicle m/s", F("Max speed in a vehicle m/s", 60f), "Faster than this for 2+ seconds in a vehicle.");
        Flying = c.Bind(A, "Watch flying", W("Watch flying", true), "Off the ground, not in a vehicle and not climbing, for too long.");
        AirSeconds = c.Bind(A, "Max seconds in the air", F("Max seconds in the air", 45f), "A jetpack burst or a fall from the tallest tower lasts well under this.");
        Reach = c.Bind(A, "Watch reach", W("Watch reach", true), "Building or removing too far from where they stand.");
        ReachMetres = c.Bind(A, "Max reach m", F("Max reach m", 60f), "The game's build reach is about 30 m.");
        Deposit = c.Bind(A, "Watch deposits", W("Watch deposits", true), "Selling or unloading more dust than they can carry, or selling from far away.");
        DepositFactor = c.Bind(A, "Max deposit x capacity", F("Max deposit x capacity", 3f), "A deposit above this many times their tool's or vehicle's dust capacity.");
        DepositMetres = c.Bind(A, "Max deposit distance m", F("Max deposit distance m", 150f), "Selling dust farther than this from every station.");
        Teleport = c.Bind(A, "Watch teleports", W("Watch teleports", true), "A jump of 150 m+ that doesn't land at a station, or isn't paid for: the game's map only teleports you to a station, for money.");
        LandMetres = c.Bind(A, "Max teleport landing distance from a station m", F("Max teleport landing distance from a station m", 200f), "A map teleport lands within this of a station.");
    }

    static bool Off(ulong sender) => !Enabled.Value || sender == 0;
    static bool WatchOff => !Enabled.Value || !Watch.Value;
    static bool Sane(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

    // what the money arriving now is for (set by Guard per action) and who caused it (held money paid later)
    internal static string Why;
    internal static ulong Cause;

    // every frame (Driver): held building money, and once a second unpaid teleports
    internal static void Update()
    {
        PayHeld();
        if (Time.unscaledTime >= nextLook) { nextLook = Time.unscaledTime + 0.5f; Look(); }
        if (Time.unscaledTime >= nextTick) { nextTick = Time.unscaledTime + 1f; UnpaidJumps(); }
    }
    static float nextTick, nextLook;

    // the cheat watch's eyes: each crew-mate's position and state, read from their player object twice a second (it
    // hooked the game's movement code until 1.0.2; the game froze or died as crew-mates joined with that hook in)
    static void Look()
    {
        if (WatchOff || !MoonSteamLobby.IsHost) return;
        var here = Crew.Now;
        // gone from the game: their last position is forgotten, so a rejoin (which puts them back where they were when
        // they left) starts a new track with its 20 s grace instead of reading as a jump from the old spot
        foreach (var id in new List<ulong>(tracks.Keys)) if (!here.ContainsKey(id)) { tracks.Remove(id); jumpedAt.Remove(id); }
        foreach (var r in Crew.Remotes())
            try { if (r != null) Moved(r, r.transform.position, r.Grounded, r.Mounted, r.Climbing); }
            catch { }
    }

    // ================================================================== money
    const float Window = 120f;          // seconds a building stays in the ledger
    const float MatchSeconds = 10f;     // building money and its building message arrive within this of each other

    // what one player built (Built: cost/refund the building was worth) and was paid (Paid), in the last Window seconds;
    // BuildSlots/RemoveSlots: one entry per structure built/removed, used up by catalog matches
    sealed class Ledger
    {
        public readonly List<(float At, double Spend, double Refund)> Built = new();
        public readonly List<(float At, double Spend, double Refund)> Paid = new();
        public readonly List<float> BuildSlots = new();
        public readonly List<float> RemoveSlots = new();
        public readonly List<float> Messages = new();                          // when each building message came
        public readonly List<(float At, double Dollars)> Unmatched = new();    // refunds not paid (with a message)

        public bool MessageNear(float t) => Messages.Exists(m => Math.Abs(m - t) <= MatchSeconds);

        public double Budget(bool spend) => Sum(Built, spend);
        public double Used(bool spend) => Sum(Paid, spend);

        static double Sum(List<(float At, double Spend, double Refund)> list, bool spend)
        {
            float now = Time.unscaledTime;
            list.RemoveAll(e => now - e.At > Window);
            double total = 0;
            foreach (var e in list) total += spend ? e.Spend : e.Refund;
            return total;
        }
    }

    // building money held until its building arrives
    sealed class Pending
    {
        public ulong Sender;
        public string Player;
        public Decimal Amount;
        public double Dollars;      // negative: a cost; positive: a refund
        public float At;
    }

    static readonly Dictionary<ulong, Ledger> ledgers = new();
    static readonly List<Pending> pending = new();
    static readonly HashSet<string> oddPlayers = new();                        // wallet names already logged once
    static List<double> catalog;                                               // every structure price, read once a minute
    static float catalogAt = -999f;

    // Handover: building money held for its building, and what each player built and was paid lately (the ledgers it's
    // checked against), go to the next copy. The cheat watch's movement tracks (seconds long) start again.
    internal sealed class Held { public ulong Sender { get; set; } public string Player { get; set; } public double Dollars { get; set; } public float At { get; set; } }
    internal sealed class LedgerState
    {
        public List<(float At, double Spend, double Refund)> Built { get; set; } = new();
        public List<(float At, double Spend, double Refund)> Paid { get; set; } = new();
        public List<float> BuildSlots { get; set; } = new();
        public List<float> RemoveSlots { get; set; } = new();
        public List<float> Messages { get; set; } = new();
        public List<(float At, double Dollars)> Unmatched { get; set; } = new();
    }
    internal static (List<Held> held, Dictionary<ulong, LedgerState> ledgers) Export()
    {
        var held = new List<Held>();
        foreach (var p in pending) held.Add(new Held { Sender = p.Sender, Player = p.Player, Dollars = p.Dollars, At = p.At });
        var led = new Dictionary<ulong, LedgerState>();
        foreach (var kv in ledgers)
            led[kv.Key] = new LedgerState { Built = new(kv.Value.Built), Paid = new(kv.Value.Paid), BuildSlots = new(kv.Value.BuildSlots),
                RemoveSlots = new(kv.Value.RemoveSlots), Messages = new(kv.Value.Messages), Unmatched = new(kv.Value.Unmatched) };
        return (held, led);
    }
    internal static void Import(List<Held> held, Dictionary<ulong, LedgerState> led)
    {
        foreach (var h in held) pending.Add(new Pending { Sender = h.Sender, Player = h.Player, Amount = new Decimal(h.Dollars), Dollars = h.Dollars, At = h.At });
        foreach (var kv in led)
        {
            var l = Of(kv.Key);
            l.Built.AddRange(kv.Value.Built); l.Paid.AddRange(kv.Value.Paid); l.BuildSlots.AddRange(kv.Value.BuildSlots);
            l.RemoveSlots.AddRange(kv.Value.RemoveSlots); l.Messages.AddRange(kv.Value.Messages); l.Unmatched.AddRange(kv.Value.Unmatched);
        }
    }

    static Ledger Of(ulong sender)
    {
        if (!ledgers.TryGetValue(sender, out var l)) ledgers[sender] = l = new Ledger();
        return l;
    }

    // Guard: this player's message built (spend) or removed (refund) this much, in so many structures
    internal static void Built(ulong sender, double spend, double refund, int added, int removed)
    {
        if (sender == 0) return;
        var l = Of(sender); float now = Time.unscaledTime;
        l.Messages.RemoveAll(t => now - t > Window); l.Messages.Add(now);
        if (spend > 0 || refund > 0) l.Built.Add((now, spend, refund));
        for (int i = 0; i < added; i++) l.BuildSlots.Add(now);
        for (int i = 0; i < removed; i++) l.RemoveSlots.Add(now);
    }

    static void Pay(ulong sender, double dollars) =>
        Of(sender).Paid.Add((Time.unscaledTime, dollars < 0 ? -dollars : 0, dollars > 0 ? dollars : 0));

    // is this much building money within what they built (or removed), less what they were already paid?
    static bool Covered(ulong sender, double dollars)
    {
        var l = Of(sender); bool spend = dollars < 0;
        return l.Used(spend) + Math.Abs(dollars) <= l.Budget(spend) + SlackDollars.Value;
    }

    // Guard: a crew-mate's game sends building money. Paid now (true), held (false, paid later by PayHeld), or a cheat.
    internal static bool BuildMoney(ulong sender, string player, Decimal amount)
    {
        if (Off(sender)) return true;
        // Crew Tools: an action refused in the last 10 s (permission off, protected base, spending cap) still sends its
        // money from the crew-mate's game, which already showed the cost or refund. Refuse it quietly: not paid, and not
        // called cheating (a refused demolish's $50 refund got an honest player banned, 2026-11-09).
        if (Guard.RecentlyRefused(sender))
        {
            Plugin.L.LogInfo($"{Players.Name(sender)}: building money right after a refused action - not paid, not counted as cheating");
            return false;
        }
        double dollars = Decimal.ToDouble(amount);
        if (!Sane(dollars) || Math.Abs(dollars) > MaxBuildMoney.Value)
        {
            Caught(sender, $"sent building money of {Money(dollars)}, more than any build could cost");
            return false;
        }
        if (Covered(sender, dollars) || CatalogMatch(sender, dollars)) { Pay(sender, dollars); Trace.Log($"money from {sender}: {Money(dollars)} paid"); return true; }
        Trace.Log($"money from {sender}: {Money(dollars)} held for its building");
        pending.Add(new Pending { Sender = sender, Player = player, Amount = amount, Dollars = dollars, At = Time.unscaledTime });
        return false;
    }

    // building money that matched nothing they built or removed: not paid; cheating or a flag (see the top)
    static void Unmatched(Pending p)
    {
        var l = Of(p.Sender);
        double worth = l.Budget(p.Dollars < 0);
        bool message = l.MessageNear(p.At);
        Trace.Log($"money from {p.Sender}: {Money(p.Dollars)} matched nothing (building message near it: {(message ? "yes" : "no")})");
        if (p.Dollars < 0)
        {
            Note(p.Sender, $"build cost of {Money(-p.Dollars)} with nothing built here (built {Money(worth)} worth) - not paid");
            return;
        }
        string what = $"a refund of {Money(p.Dollars)} with nothing removed here (removed {Money(worth)} worth)";
        if (!message) { Caught(p.Sender, $"sent {what} and no building or demolition at all"); return; }
        float now = Time.unscaledTime;
        l.Unmatched.RemoveAll(u => now - u.At > 600f); l.Unmatched.Add((now, p.Dollars));
        double total = 0; foreach (var u in l.Unmatched) total += u.Dollars;
        if (total > MaxUnmatchedRefunds.Value) { Caught(p.Sender, $"sent refunds of {Money(total)} in 10 minutes that matched nothing removed (latest: {what})"); return; }
        Note(p.Sender, $"{what} - not paid ({Money(total)} unmatched in 10 minutes)");
    }

    // pay held money as soon as its building has come; after MatchSeconds without one it isn't paid (Unmatched)
    static void PayHeld()
    {
        if (pending.Count == 0) return;
        float now = Time.unscaledTime;
        for (int i = 0; i < pending.Count; i++)
        {
            var p = pending[i];
            bool real = Covered(p.Sender, p.Dollars);
            if (!real && now - p.At < MatchSeconds) continue;               // still waiting for its building
            if (!real) real = CatalogMatch(p.Sender, p.Dollars);
            pending.RemoveAt(i--);
            if (!real) { Unmatched(p); continue; }
            Pay(p.Sender, p.Dollars);
            try
            {
                Why = $"building money from {Players.Name(p.Sender)} (held until its building came)";
                Cause = p.Sender;
                Crew.World()?.CrewBuildMoney(p.Player, p.Amount);
            }
            catch (Exception e) { Plugin.L.LogWarning("Applying held building money: " + e.Message); }
            finally { Why = null; Cause = 0; }
        }
    }

    // every structure price in the game (kinds 1-2048), re-read once a minute
    static List<double> Catalog()
    {
        if (catalog != null && Time.unscaledTime - catalogAt < 60f) return catalog;
        catalogAt = Time.unscaledTime;
        var prices = new SortedSet<double>();
        try
        {
            var w = Crew.World();
            if (w != null)
                for (int kind = 1; kind <= 2048; kind++)
                {
                    MoonDepotBuildDefinition def;
                    try { def = w.BuildDefinition(kind); } catch { continue; }
                    if (def != null && def.priceDollars > 0) prices.Add(def.priceDollars);
                }
        }
        catch (Exception e) { Plugin.L.LogWarning("Reading the build catalog: " + e.Message); }
        if (catalog == null) Plugin.L.LogInfo($"Build catalog: {prices.Count} prices.");
        return catalog = new List<double>(prices);
    }

    // Building money that isn't covered but is exactly one structure's price (or refund), while they have a recent
    // build (or removal) not yet matched: a building that arrived as something else. Uses up that build.
    static bool CatalogMatch(ulong sender, double dollars)
    {
        if (sender == 0) return false;
        var l = Of(sender); float now = Time.unscaledTime;
        bool spend = dollars < 0;
        var slots = spend ? l.BuildSlots : l.RemoveSlots;
        slots.RemoveAll(t => now - t > Window);
        if (slots.Count == 0) return false;
        double amount = Math.Abs(dollars);
        foreach (double price in Catalog())
        {
            double expected = spend ? price : Decimal.ToDouble(MoonWorld.Refund(new Decimal(price)));
            if (Math.Abs(expected - amount) > SlackDollars.Value) continue;
            slots.RemoveAt(0);
            l.Built.Add((now, spend ? amount : 0, spend ? 0 : amount));
            Plugin.L.LogInfo($"Building money from {Players.Name(sender)} of {Money(dollars)}: matches a catalog {(spend ? "price" : "refund")} (a building that arrived as something else).");
            return true;
        }
        return false;
    }

    // Guard: kilograms sold, loaded or unloaded: 0 to MaxKilograms
    internal static bool Kilograms(ulong sender, double kilograms, string what)
    {
        if (Off(sender)) return true;
        if (Sane(kilograms) && kilograms >= 0 && kilograms <= MaxKilograms.Value) return true;
        Caught(sender, $"sent {what} of {kilograms:0.#} kg");
        return false;
    }

    // Guard: money for a vehicle trick: 0 to MaxTrickMoney
    internal static bool TrickMoney(ulong sender, Decimal amount)
    {
        if (Off(sender)) return true;
        double dollars = Decimal.ToDouble(amount);
        if (!double.IsNaN(dollars) && dollars >= 0 && dollars <= MaxTrickMoney.Value) return true;
        Caught(sender, "sent trick money of " + Money(dollars));
        return false;
    }

    // Guard: the wallet a message names. Always allowed (the format isn't fully known); another crew-mate's wallet is
    // noted once, and anything else is logged once, to learn from.
    internal static bool OwnWallet(ulong sender, string player, string what)
    {
        if (Off(sender) || string.IsNullOrEmpty(player) || player == sender.ToString()) return true;
        if (ulong.TryParse(player, out var other) && IsCrew(other))
        {
            if (oddPlayers.Add(sender + ">" + player)) Note(sender, $"sent {what} naming {Players.Name(other)}'s wallet (allowed; logged to learn the format)");
            return true;
        }
        if (oddPlayers.Add(player)) Plugin.L.LogInfo($"Crew message from {Players.Name(sender)} names player \"{player}\" (allowed).");
        return true;
    }

    static bool IsCrew(ulong id)
    {
        try { return id == MoonSteamLobby.HostId || MoonSteamLobby.InSteamLobby(id); }
        catch { return false; }
    }

    // Guard: every wallet change, written to the money log with what it was for; money a crew-mate caused with no action
    // named for it is noted (not punished)
    internal static void MoneyChanged(double before, double after)
    {
        double change = after - before;
        if (Math.Abs(change) < 0.005) return;
        ulong who = Guard.Sender != 0 ? Guard.Sender : Cause;
        string name = who != 0 ? Players.Name(who) : "host";
        string why = Why ?? (who != 0 ? "NOTHING THAT PAYS" : "host's own game");
        string signed = (change > 0 ? "+" : "-") + Money(Math.Abs(change));
        Append("CrewPermissions.money.log", $"{signed,-15} -> {Money(after),-16} {name}: {why}");
        if (who != 0 && Why == null && Enabled.Value) Note(who, $"the money changed by {signed} with no action named for it (check CrewPermissions.money.log)");
    }

    // Guard: they left
    internal static void Forget(ulong sender)
    {
        ledgers.Remove(sender);
        pending.RemoveAll(p => p.Sender == sender);
        tracks.Remove(sender); jumpedAt.Remove(sender);
    }

    // ================================================================== watch: movement, from each crew-mate update
    sealed class Track { public Vector3 pos; public float t, seenAt, fastSince = -1, airSince = -1; public MoonRemotePlayer who; }
    static readonly Dictionary<ulong, Track> tracks = new();

    static void Moved(MoonRemotePlayer who, Vector3 position, bool grounded, bool mounted, bool climbing)
    {
        if (WatchOff || who == null) return;
        ulong id; try { id = who.SteamId; } catch { return; }
        if (id == 0 || !MoonSteamLobby.IsHost) return;
        float now = Time.unscaledTime;
        if (!tracks.TryGetValue(id, out var k) || now - k.t > 10f) { Trace.Log($"watch: tracking {id}"); tracks[id] = new Track { pos = position, t = now, seenAt = now, who = who }; return; }
        k.who = who;
        float dt = now - k.t; if (dt < 0.05f) return;
        float dist = Vector3.Distance(position, k.pos);
        var up = position.normalized; var d = position - k.pos; float side = (d - up * Vector3.Dot(d, up)).magnitude;   // sideways only
        k.pos = position; k.t = now;
        if (now - k.seenAt < 20f) return;                                   // just arrived: spawn and map teleports
        if (dist > 150f) { k.fastSince = -1; k.airSince = -1; Jumped(id, position, dist, now); return; }

        if (Speed.Value)
        {
            float v = side / dt, max = mounted ? VehicleSpeed.Value : FootSpeed.Value;
            if (v <= max) k.fastSince = -1;
            else if (k.fastSince < 0) k.fastSince = now;
            else if (now - k.fastSince >= 2f) { WatchCaught(id, $"moved at {v:0} m/s {(mounted ? "in a vehicle" : "on foot")} (most {max:0})"); k.fastSince = -1; }
        }
        if (Flying.Value)
        {
            bool passenger = false; try { passenger = who.PassengerView != null; } catch { }
            bool air = !grounded && !mounted && !climbing && !passenger;
            if (!air) k.airSince = -1;
            else if (k.airSince < 0) k.airSince = now;
            else if (now - k.airSince >= AirSeconds.Value) { WatchCaught(id, $"off the ground for {now - k.airSince:0} s (most {AirSeconds.Value:0})"); k.airSince = -1; }
        }
    }

    // where the cheat watch last saw them (5 s at most ago)
    static bool PositionOf(ulong id, out Vector3 p)
    {
        p = default;
        if (!tracks.TryGetValue(id, out var k) || Time.unscaledTime - k.t > 5f) return false;
        p = k.pos; return true;
    }

    static float NearestStation(Vector3 at)
    {
        var w = Crew.World(); float near = float.MaxValue;
        if (w != null) foreach (var s in w.stations) near = Math.Min(near, Vector3.Distance(at, s.transform.position));
        return near;
    }

    // ================================================================== watch: teleports
    // The map only teleports you to a station, and costs money the host takes (MoonWorld.TryPayTeleport). A jump of
    // 150 m+ that lands away from every station is a cheat at once; one that lands at a station must be paid for within
    // 15 s either side. The payment rule is only enforced once a paid crew teleport has been seen this session - proof
    // the payment reaches this game - so honest players can't be caught if it ever doesn't.
    static readonly Dictionary<ulong, float> paidAt = new(), jumpedAt = new(), jumpDist = new();
    static bool paymentsSeen;

    static void Jumped(ulong id, Vector3 to, float dist, float now)
    {
        if (!Teleport.Value) return;
        float near = NearestStation(to);
        if (near == float.MaxValue) return;                                  // no world
        if (near > LandMetres.Value) WatchCaught(id, $"jumped {dist:0} m to a spot {near:0} m from the nearest station (the map only teleports to stations)");
        else if (!PaidNear(id, now)) { jumpedAt[id] = now; jumpDist[id] = dist; }       // at a station: was it paid? (UnpaidJumps)
    }

    internal static void TeleportPaid(string player, bool paid)
    {
        if (!paid || string.IsNullOrEmpty(player)) return;
        var digits = new System.Text.StringBuilder(); foreach (char ch in player) if (char.IsDigit(ch)) digits.Append(ch);
        if (!ulong.TryParse(digits.ToString(), out var id) || id < 70000000000000000UL) id = Guard.Sender;    // "steam-7656..." or the sender
        if (id == 0) return;
        paidAt[id] = Time.unscaledTime; paymentsSeen = true; jumpedAt.Remove(id);
    }

    static bool PaidNear(ulong id, float t) => paidAt.TryGetValue(id, out var p) && Math.Abs(p - t) <= 15f;

    static void UnpaidJumps()
    {
        if (WatchOff || !Teleport.Value || !paymentsSeen || jumpedAt.Count == 0) return;
        float now = Time.unscaledTime;
        foreach (var id in new List<ulong>(jumpedAt.Keys))
        {
            float t = jumpedAt[id];
            if (PaidNear(id, t)) { jumpedAt.Remove(id); continue; }
            if (now - t > 15f) { jumpedAt.Remove(id); WatchCaught(id, $"jumped {jumpDist.GetValueOrDefault(id):0} m without paying for a teleport"); }
        }
    }

    // ================================================================== watch: reach and deposits
    // Owners: what a crew building message really built and removed (anything refused never got there)
    internal static void Structures(ulong id, List<Vector3> added, List<Vector3> removed)
    {
        if (WatchOff || !Reach.Value || id == 0 || !PositionOf(id, out var me) || Guard.RecentlyRefused(id)) return;
        float far = 0;
        foreach (var p in added) far = Math.Max(far, Vector3.Distance(p, me));
        foreach (var p in removed) far = Math.Max(far, Vector3.Distance(p, me));
        if (far > ReachMetres.Value) WatchCaught(id, $"built or removed {far:0} m from where they stood (most {ReachMetres.Value:0})");
    }

    // Guard.Sell and Hooks.GarageUnload, only once the action was allowed (a refused one is never looked at). Capacity is
    // checked for dust (sales and garage unloads); distance for any sale.
    internal static void CheckDeposit(double kg, bool atStation, bool dust)
    {
        if (WatchOff || !Deposit.Value) return;
        ulong id = Guard.Sender; if (id == 0 || Guard.RecentlyRefused(id)) return;
        if (!tracks.TryGetValue(id, out var k) || k.who == null) return;
        double cap = Capacity(k.who);
        if (dust && cap > 0 && kg > cap * DepositFactor.Value) { WatchCaught(id, $"{(atStation ? "sold" : "unloaded")} {kg:0} kg, carrying at most {cap:0} kg"); return; }
        if (!atStation) return;
        float near = NearestStation(k.pos);
        if (near != float.MaxValue && near > DepositMetres.Value) WatchCaught(id, $"sold dust {near:0} m from the nearest station (most {DepositMetres.Value:0})");
    }

    // their current dust capacity: the vehicle they're in, else the tool in their hands; 0 = unknown (not checked)
    static double Capacity(MoonRemotePlayer p)
    {
        try
        {
            var w = Crew.World();
            if (p.Mounted && p.Vehicle != null && w != null && !string.IsNullOrEmpty(p.Vehicle.Storage)) { double c = w.VehicleDustCapacity(p.Vehicle.Storage); if (c > 0) return c; }
            if (p.Tool != null && p.Tool.containerKg > 0) return p.Tool.containerKg;
        }
        catch { }
        return 0;
    }

    // a watch catch: one per player per 30 s (one cheat trips a check many times a second)
    static readonly Dictionary<ulong, float> watchCaughtAt = new();
    static void WatchCaught(ulong id, string what)
    {
        if (!Enabled.Value) return;
        float now = Time.unscaledTime;
        if (watchCaughtAt.TryGetValue(id, out var t) && now - t < 30f) return;
        watchCaughtAt[id] = now;
        try { Caught(id, "cheat watch: " + what); } catch (Exception e) { Plugin.L.LogWarning("cheat watch: " + e.Message); }
    }

    // ================================================================== caught

    // Refused, every permission off, then punished at once (AutoBan) or the host is asked (Alerts popup).
    internal static void Caught(ulong sender, string what)
    {
        if (sender == 0) return;
        string name = Players.Name(sender);
        Plugin.L.LogWarning($"Cheating: {name} ({sender}) {what}. Refused.");
        Write($"CHEATING  {name} ({sender}) {what}. Refused.");
        if (Players.RevokeAll(sender))
        {
            Plugin.L.LogWarning($"All permissions removed from {name} ({sender}).");
            Write($"PERMISSIONS REMOVED  {name} ({sender}): everything switched off after cheating.");
        }
        if (StillRemoved(sender)) return;                           // already banned or kicked
        bool decided = AutoBan.Value && Punish(sender, what);
        Alerts.Raise(sender, what, decided);
    }

    static bool StillRemoved(ulong sender)
    {
        try { return MoonSteamLobby.IsRemoved(sender); }
        catch { return false; }
    }

    internal static bool Kicks => (Punishment.Value ?? "").Trim().Equals("Kick", StringComparison.OrdinalIgnoreCase);

    // the Punishment setting: None (nothing more: the permissions are already off), Kick, or anything else bans
    static bool Punish(ulong sender, string what)
    {
        if ((Punishment.Value ?? "").Trim().Equals("None", StringComparison.OrdinalIgnoreCase)) return false;
        Remove(sender, "cheating: " + what, ban: !Kicks);
        return true;
    }

    // Alerts: the host chose Ignore
    internal static void Ignored(ulong sender, string what)
    {
        Plugin.L.LogInfo($"Host ignored: {Players.Name(sender)} {what}.");
        Write($"ignored by the host  {Players.Name(sender)} ({sender}) {what}.");
    }

    internal static void Remove(ulong sender, string why, bool ban)
    {
        string name = Players.Name(sender);
        try
        {
            if (ban) MoonSteamLobby.Ban(sender); else MoonSteamLobby.Kick(sender);
            string done = ban ? "BANNED" : "KICKED";
            Plugin.L.LogWarning($"{name} ({sender}) {done.ToLowerInvariant()}: {why}.");
            Write($"{done}  {name} ({sender}): {why}.");
        }
        catch (Exception e) { Plugin.L.LogError($"Couldn't remove {name}: {e.Message}"); }
    }

    // something odd, only noted (yellow): not punished
    static void Note(ulong sender, string what)
    {
        string name = sender != 0 ? Players.Name(sender) : "the host";
        Plugin.L.LogWarning($"Flag, {name} ({sender}): {what}.");
        Write($"flag  {name} ({sender}): {what}.");
    }

    // ================================================================== logs
    static void Write(string line) => Append("CrewPermissions.cheats.log", line);

    static void Append(string file, string line)
    {
        try { File.AppendAllText(Path.Combine(Paths.BepInExRootPath, file), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}{Environment.NewLine}"); }
        catch { }
    }

    static string Money(double dollars) => "$" + dollars.ToString("#,0.##", CultureInfo.InvariantCulture);
}
