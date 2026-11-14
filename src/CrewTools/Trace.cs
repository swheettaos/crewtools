using System;
using System.IO;
using System.Threading;
using BepInEx;
using UnityEngine;

namespace CrewTools;

// Trace: a flight recorder for finding a crash. Every line is written to BepInEx/CrewTools.trace.log the moment it
// happens (opened, written and closed each time, so the last lines survive a game that dies without a word). It records
// the things around a crew-mate joining - a player appearing or leaving, a record being made, their first messages -
// and every crew building message and its money (how the money check decided), and which thread each ran on ("*" = not the game's main thread, where
// Unity calls are unsafe). The file starts empty each time the game starts (a reload of Crew Tools carries on in it). Switch off: [Debug] Trace = false.
internal static class Trace
{
    static string path; static int mainThread; static bool on = true;
    static readonly object gate = new();

    // fresh: the game just started (the file starts empty); otherwise Crew Tools was reloaded and the file goes on
    internal static void Start(BepInEx.Configuration.ConfigFile config, bool fresh)
    {
        mainThread = Environment.CurrentManagedThreadId;
        on = config.Bind("Debug", "Trace", true, "Write BepInEx/CrewTools.trace.log: a line for each join and first-time hook call, saved at once, to find a crash.").Value;
        if (!on) return;
        path = Path.Combine(Paths.BepInExRootPath, "CrewTools.trace.log");
        try
        {
            if (fresh) File.WriteAllText(path, $"Crew Tools trace, game started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            else File.AppendAllText(path, $"Crew Tools reloaded {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }
        catch { on = false; }
    }

    // one line: time, frame, thread, text
    internal static void Log(string text)
    {
        if (!on) return;
        int t = Environment.CurrentManagedThreadId;
        int frame = -1; try { if (t == mainThread) frame = Time.frameCount; } catch { }
        string line = $"{DateTime.Now:HH:mm:ss.fff} f{frame} t{t}{(t == mainThread ? "" : "*")} {text}\n";
        try { lock (gate) File.AppendAllText(path, line); } catch { }
    }

    // only the first few times: for hooks that run constantly
    internal static void First(ref int count, int most, string text)
    {
        if (count >= most) return;
        count++;
        Log($"{text} (#{count})");
    }
}
