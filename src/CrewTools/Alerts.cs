// Alerts: telling the host about a catch, one at a time.
//
//   already punished (AutoBan on)  a small notice in the top-right corner - "BANNED: name" and why - gone by itself
//                                  after 8 s. Nothing to click: during play the game holds the mouse, so a button
//                                  there couldn't be clicked (2026-11-10).
//   the host decides (AutoBan off)  a card at the top: BanKey (F7) bans, IgnoreKey (F8) lets it go; its BAN / IGNORE
//                                  buttons work when the mouse is free (pause menu open).
//
// From Crew Permissions by tylerun (nexusmods.com/moonsweepers/mods/71), rebuilt from the released DLL; redesigned for
// Crew Tools and drawn with Widgets like the CREW TOOLS window. Catches of the same player within 10 s add up (x2...).
using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CrewTools.Widgets;

namespace CrewTools;

internal static class Alerts
{
    sealed class Alert
    {
        public ulong Sender;
        public string Name;
        public string What;
        public int Count = 1;
        public float At;
        public bool Banned;         // already punished (AutoBan): nothing to decide
    }

    static readonly List<Alert> waiting = new();

    const float NoticeSeconds = 8f;

    static GameObject layer, notice, card;
    static GraphicRaycaster ray;
    static Text noticeTitle, noticeLine, title, line, keys;
    static bool failed;                                         // the card couldn't be built: stop trying
    static string banName, ignoreName; static Key banKey, ignoreKey;   // the key settings, parsed when they change

    // Tools.Stop: the popup goes; what's waiting is handed over (Handover)
    internal static void Stop() { Widgets.Destroy(layer); layer = null; failed = false; }

    internal static List<(ulong Sender, string Name, string What, int Count, float At, bool Banned)> Export()
    {
        var l = new List<(ulong, string, string, int, float, bool)>();
        foreach (var a in waiting) l.Add((a.Sender, a.Name, a.What, a.Count, a.At, a.Banned));
        return l;
    }
    internal static void Import(List<(ulong Sender, string Name, string What, int Count, float At, bool Banned)> l)
    {
        foreach (var a in l) waiting.Add(new Alert { Sender = a.Sender, Name = a.Name, What = a.What, Count = a.Count, At = a.At, Banned = a.Banned });
    }

    internal static void Raise(ulong sender, string what, bool decided)
    {
        float now = Time.unscaledTime;
        foreach (var a in waiting)
            if (a.Sender == sender && now - a.At < 10f)
            {
                a.Count++; a.What = what; a.At = now; a.Banned |= decided;
                return;
            }
        waiting.Add(new Alert { Sender = sender, Name = Players.Name(sender), What = what, At = now, Banned = decided });
    }

    internal static void Update()
    {
        try
        {
            if (waiting.Count == 0)
            {
                if (layer != null) { notice.SetActive(false); card.SetActive(false); }
                return;
            }
            if (layer == null && (failed || !Build())) return;
            var a = waiting[0];
            if (a.Banned && Time.unscaledTime - a.At > NoticeSeconds) { waiting.RemoveAt(0); return; }
            Show(a);
            if (a.Banned) return;                                  // a notice: nothing to answer
            Keys();
            Click(ray);
        }
        catch (Exception e)
        {
            Plugin.L.LogError("Cheating popup: " + e);
            failed = true;
            Widgets.Destroy(layer);
            layer = null;
        }
    }

    static void Show(Alert a)
    {
        string times = a.Count > 1 ? $"  <color=#FFB4A8>x{a.Count}</color>" : "";
        int more = waiting.Count - 1;
        string waitingNote = more > 0 ? $"     ({more} more)" : "";
        notice.SetActive(a.Banned); card.SetActive(!a.Banned);
        if (a.Banned)
        {
            noticeTitle.text = $"{(AntiCheat.Kicks ? "KICKED" : "BANNED")}: <b>{a.Name}</b>{times}";
            noticeLine.text = a.What + waitingNote;
            return;
        }
        title.text = $"CHEATING: <b>{a.Name}</b>{times}";
        line.text = $"{a.What}. Refused, and all their permissions are off.";
        keys.text = $"{AntiCheat.BanKey.Value}  ban them     {AntiCheat.IgnoreKey.Value}  let it go" + waitingNote;
    }

    static void Keys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        try { if (MoonInput.Typing) return; } catch { }           // not while typing in chat
        if (Pressed(kb, AntiCheat.BanKey.Value, ref banName, ref banKey)) BanNow();
        else if (Pressed(kb, AntiCheat.IgnoreKey.Value, ref ignoreName, ref ignoreKey)) Close();
    }

    static bool Pressed(Keyboard kb, string setting, ref string name, ref Key key)
    {
        if (name != setting)
        {
            name = setting;
            if (!Enum.TryParse(name, ignoreCase: true, out key)) key = Key.None;
        }
        return key != Key.None && kb.get_Item(key).wasPressedThisFrame;
    }

    static void BanNow()
    {
        var a = waiting[0];
        waiting.RemoveAll(x => x.Sender == a.Sender);
        AntiCheat.Remove(a.Sender, "cheating, banned by the host: " + a.What, ban: true);
    }

    static void Close()                                    // IGNORE: the host lets it go
    {
        var a = waiting[0];
        waiting.RemoveAt(0);
        AntiCheat.Ignored(a.Sender, a.What);
    }

    // built on the first catch: the notice (560 x 84, top-right) and the card (760 x 150, top centre)
    static bool Build()
    {
        layer = Layer("Crew Tools Alerts", 5300, out ray);
        var bg = new Color(Ink.r, Ink.g, Ink.b, 0.92f);

        const float nw = 560, nh = 84;
        var n = Rect("Notice", layer.transform, new Vector2(nw, nh), new Vector2(960 - 20 - nw / 2, 540 - 20 - nh / 2), bg);
        notice = n.gameObject;
        Rect("Bar", n, new Vector2(8, nh), new Vector2(-nw / 2 + 4, 0), Red);
        noticeTitle = Label(n, "", new Vector2(10, 18), new Vector2(nw - 40, 30), 20, Red, TextAnchor.MiddleLeft);
        noticeLine = Label(n, "", new Vector2(10, -16), new Vector2(nw - 40, 40), 15, Cream, TextAnchor.UpperLeft);

        const float cw = 760, ch = 150, x = -cw / 2 + 24, textW = cw - 48;
        var c = Rect("Card", layer.transform, new Vector2(cw, ch), new Vector2(0, 540 - 30 - ch / 2), bg);
        card = c.gameObject;
        Rect("Bar", c, new Vector2(8, ch), new Vector2(-cw / 2 + 4, 0), Red);
        title = Label(c, "", new Vector2(x + textW / 2, 46), new Vector2(textW, 34), 24, Red, TextAnchor.MiddleLeft);
        line = Label(c, "", new Vector2(x + textW / 2, 6), new Vector2(textW, 44), 16, Cream, TextAnchor.UpperLeft);
        keys = Label(c, "", new Vector2(x + 220, -48), new Vector2(440, 30), 15, Soft, TextAnchor.MiddleLeft);
        Button(c, "BAN", new Vector2(cw / 2 - 24 - 55 - 120, -48), new Vector2(110, 32), BanNow, colour: Red);
        Button(c, "IGNORE", new Vector2(cw / 2 - 24 - 55, -48), new Vector2(110, 32), Close, colour: Grey);

        notice.SetActive(false); card.SetActive(false);
        return true;
    }
}
