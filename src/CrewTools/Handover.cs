using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CrewTools;

// What Tools.Stop() hands the next copy of Crew Tools when it is reloaded in place (and Tools.Start(state) takes back):
// spending in the last 30 minutes (Spending), building money held for its building and the money ledgers (AntiCheat),
// a ban reclaim's removal queue (Remover) and cheating popups not answered yet (Alerts). Everything else is either in
// the players and owners files already, or lasts only seconds.
internal static class Handover
{
    sealed class State
    {
        public Dictionary<ulong, List<(float t, double d)>> Spent { get; set; }
        public List<AntiCheat.Held> Held { get; set; }
        public Dictionary<ulong, AntiCheat.LedgerState> Ledgers { get; set; }
        public int[] RemoveStructures { get; set; }
        public int[] RemoveCapsules { get; set; }
        public double RemovePaid { get; set; }
        public int RemoveDone { get; set; }
        public List<(ulong Sender, string Name, string What, int Count, float At, bool Banned)> Alerts { get; set; }
    }

    static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    internal static string Save()
    {
        var (held, ledgers) = AntiCheat.Export();
        var (structures, capsules, paid, done) = Remover.Export();
        return JsonSerializer.Serialize(new State { Spent = Spending.Export(), Held = held, Ledgers = ledgers, RemoveStructures = structures,
            RemoveCapsules = capsules, RemovePaid = paid, RemoveDone = done, Alerts = Alerts.Export() }, Options);
    }

    internal static void Load(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<State>(json, Options);
            if (s == null) return;
            if (s.Spent != null) Spending.Import(s.Spent);
            AntiCheat.Import(s.Held ?? new(), s.Ledgers ?? new());
            Remover.Import((s.RemoveStructures ?? Array.Empty<int>(), s.RemoveCapsules ?? Array.Empty<int>(), s.RemovePaid, s.RemoveDone));
            if (s.Alerts != null) Alerts.Import(s.Alerts);
            Plugin.L.LogInfo($"Crew Tools took over: {s.Spent?.Count ?? 0} spenders, {s.Held?.Count ?? 0} held payments, {s.RemoveStructures?.Length ?? 0} removals queued, {s.Alerts?.Count ?? 0} popups");
        }
        catch (Exception e) { Plugin.L.LogWarning("Crew Tools couldn't take over the last copy's state: " + e.Message); }
    }
}
