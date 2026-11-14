using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using MoonCleaners;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using static CrewTools.Widgets;

namespace CrewTools;

// Ui: the CREW TOOLS window. A CREW TOOLS button on the pause menu's first page opens one window with five tabs
// (Players, Bases, Rules, Anti-cheat, Server), drawn with Widgets. While it is open the pause menu underneath takes no
// keys (Hooks.PauseKeys) and the mouse is free.
internal static class Ui
{
    static MoonPauseMenu pause;
    static GameObject buttonLayer, panel; static GraphicRaycaster btnRay, panelRay; static InputField nameField, welcomeField; static Text status;

    internal static bool IsOpen => panel != null;                          // Hooks.PauseKeys: the pause menu takes no keys while open

    static float pauseLookAt = -10f;

    internal static void Update()
    {
        if (pause == null && Time.unscaledTime - pauseLookAt >= 1f)
        {
            pauseLookAt = Time.unscaledTime;
            var f = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<MoonPauseMenu>()); if (f.Length > 0) pause = f[0].Cast<MoonPauseMenu>();
        }
        bool show = pause != null && pause.IsOpen && (int)pause.CurrentPage == 0 && Crew.World() != null;
        if (!show && panel != null) ClosePanel();
        bool showButton = show;
        if (showButton && buttonLayer == null && panel == null) MakeButton();
        if (!showButton && buttonLayer != null) { Widgets.Destroy(buttonLayer); buttonLayer = null; }
        if (panel != null)
        {
            var kb = Keyboard.current; if (kb != null && kb.escapeKey.wasPressedThisFrame) { ClosePanel(); return; }
            var ms = Mouse.current;                                                  // the mouse wheel scrolls the players list
            if (ms != null && popup == null && tabNow == "Players") { float sy = ms.scroll.ReadValue().y; if (sy > 0.01f) ScrollPlayers(-1); else if (sy < -0.01f) ScrollPlayers(1); }
            Click(panelRay, popup);
            Tooltips();
        }
        else if (buttonLayer != null) Click(btnRay);
    }
    internal static void LateUpdate() { if (panel != null || buttonLayer != null) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }

    static void MakeButton()
    {
        buttonLayer = Layer("Crew Tools Button", 31000, out btnRay);
        var b = Button(buttonLayer.transform, "CREW TOOLS", new Vector2(-150, -150), new Vector2(240, 54), OpenPanel, true);
        var r = b.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(1, 1); r.anchoredPosition = new Vector2(-150, -150);
    }

    // the players list scrolls (2026-11-09: no way past the first four names): visibleRows rows show playerOrder
    // from playerTop
    static readonly List<(RectTransform row, Text label, Text basesText, List<(Node node, Image bg, Text text)> perms)> playerSlots = new();
    static readonly (Node node, string tag)[] PermNodes = {
        (Node.Build, "Bld"), (Node.Demolish, "Dem"), (Node.Sell, "Sell"), (Node.Shop, "Shop"), (Node.Capsules, "Cap") };
    static void TogglePerm(ulong id, Node node) => Players.SetSwitch(id, node, !Players.Switch(Players.Get(id), node));
    static List<ulong> playerOrder = new(); static int playerTop;
    static void ScrollPlayers(int d) { playerTop = Math.Clamp(playerTop + d, 0, Math.Max(0, playerOrder.Count - visibleRows)); Refresh(); }
    static RectTransform popup; static readonly List<(string bse, Text text, Image bg)> popupRows = new(); static ulong popupFor;

    // ---------------------------------------------------------------- the window (tabs, user 2026-11-10)
    // One 980 x 900 window, five tabs. Every switch shows its state (ON orange / OFF grey), every choice is a row of
    // buttons with the chosen one lit, every number is  -  value  +. Widgets register a refresher, so Refresh() is one pass.
    static readonly List<Action> refreshers = new();
    static readonly List<(string name, RectTransform content, Image bg, Text text)> tabs = new();
    static string tabNow = "Players"; static bool showOnline = true; static Text tabOnline, tabHistory, rangeText;
    static int visibleRows = 9; static HashSet<ulong> onlineIds = new();
    const float W = 920f;                                                 // row width; window is 980

    static RectTransform RowBox(Transform p, float y, float h = 56f) { var r = Rect("Row", p, new Vector2(W, h), new Vector2(0, y), Panel); Shade(r, false); return r; }
    static Text RowTitle(RectTransform row, string title, float w = 460f) =>
        Label(row, title, new Vector2(-W / 2 + 18 + w / 2, 0), new Vector2(w, 52), 19, Ink, TextAnchor.MiddleLeft);
    // an ON / OFF switch ending at xRight
    static void Toggle(RectTransform row, float xRight, Func<bool> get, Action flip, string on = "ON", string off = "OFF", float w = 110f)
    {
        UnityEngine.UI.Button b = null;
        b = Button(row, "", new Vector2(xRight - w / 2, 0), new Vector2(w, 40), () => { flip(); Refresh(); });
        refreshers.Add(() => { bool v = get(); Paint(b, v); var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = v ? on : off; });
    }
    // a choice: the options side by side, ending at xRight, the chosen one lit
    static void Choice(RectTransform row, float xRight, string[] options, Func<int> get, Action<int> set, float w = 110f)
    {
        var bs = new List<UnityEngine.UI.Button>();
        for (int i = 0; i < options.Length; i++)
        {
            int k = i; float x = xRight - (options.Length - i) * (w + 6) + w / 2 + 6;
            bs.Add(Button(row, options[i], new Vector2(x, 0), new Vector2(w, 40), () => { set(k); Refresh(); }));
        }
        refreshers.Add(() => { int v = get(); for (int i = 0; i < bs.Count; i++) Paint(bs[i], i == v); });
    }
    // -  value  +  ending at xRight
    static void Stepper(RectTransform row, float xRight, Func<string> value, Action minus, Action plus)
    {
        Button(row, "+", new Vector2(xRight - 25, 0), new Vector2(50, 40), () => { plus(); Refresh(); });
        var v = Label(row, "", new Vector2(xRight - 50 - 80, 0), new Vector2(150, 40), 19, Ink, TextAnchor.MiddleCenter);
        Button(row, "-", new Vector2(xRight - 50 - 160 - 25 + 10, 0), new Vector2(50, 40), () => { minus(); Refresh(); });
        refreshers.Add(() => v.text = value());
    }
    static void Hint(Transform p, string text, float y) => Label(p, text, new Vector2(0, y), new Vector2(W, 34), 16, Muted, TextAnchor.MiddleLeft);

    static void ShowTab(string name)
    {
        tabNow = name; if (name != "Players") ClosePasses();
        foreach (var (n, c, bg, tx) in tabs) { bool on = n == name; c.gameObject.SetActive(on); if (bg != null) bg.color = on ? Accent : Btn; if (tx != null) tx.color = on ? Ink : Cream; }
    }

    static void OpenPanel()
    {
        if (buttonLayer != null) { Widgets.Destroy(buttonLayer); buttonLayer = null; }
        refreshers.Clear(); tabs.Clear(); playerSlots.Clear();
        panel = Layer("Crew Tools", 31001, out panelRay);
        var win = Rect("Window", panel.transform, new Vector2(980, 900), Vector2.zero, Panel);
        Label(win, "Crew Tools", new Vector2(0, 405), new Vector2(400, 56), 32, Ink, TextAnchor.MiddleCenter);
        Button(win, "×", new Vector2(450, 410), new Vector2(50, 50), ClosePanel);
        Help(win, new Vector2(125, 405),
            "Crew Tools runs your crew game: what each crew-mate may do, where and how much.\n\n" +
            "Players: each one's five switches (orange = ON), base passes, and Manage (spending, trust, kick, ban).\n" +
            "Bases: protect a base so crew-mates near it can look but not touch.\n" +
            "Rules: the spending cap, the cleanup, and what new players start with.\n" +
            "Anti-cheat: what happens to a cheater, and the cheat watch.\n" +
            "Server: your server name and the welcome message.",
            new Vector2(640, 250), new Vector2(0, 230));
        string[] names = { "Players", "Bases", "Rules", "Anti-cheat", "Server" };
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            var tb = Button(win, n, new Vector2(-W / 2 + 90 + i * 184, 340), new Vector2(176, 50), () => { ShowTab(n); Refresh(); });
            var content = Rect(n, win, new Vector2(W, 700), new Vector2(0, -40), null);
            tabs.Add((n, content, tb.GetComponent<Image>(), tb.GetComponentInChildren<Text>()));
        }
        BuildPlayers(win, tabs[0].content); BuildBases(tabs[1].content); BuildRules(tabs[2].content); BuildCheats(tabs[3].content); BuildServer(tabs[4].content);
        status = Label(win, Plugin.Hooked, new Vector2(0, -425), new Vector2(W, 30), 15, Muted, TextAnchor.MiddleCenter);
        ShowTab(tabNow); Refresh();
    }

    // ---------------- Players: Online / History, column headings, nine rows that scroll
    static void RebuildOrder()
    {
        onlineIds = new HashSet<ulong>(Crew.Now.Keys);
        var list = new List<ulong>();
        if (showOnline) list.AddRange(onlineIds); else list.AddRange(History());
        string Nm(ulong id) => Players.Name(id).ToLowerInvariant();
        list.Sort((x, y) => string.CompareOrdinal(Nm(x), Nm(y)));
        playerOrder = list; playerTop = Math.Clamp(playerTop, 0, Math.Max(0, list.Count - visibleRows));
    }
    // everyone who has played here and isn't in the game now (minus those removed from the list)
    static IEnumerable<ulong> History() => Players.All.Where(p => !p.Hidden && !onlineIds.Contains(p.Id)).Select(p => p.Id);

    static void BuildPlayers(RectTransform win, RectTransform c)
    {
        var bar = Rect("Bar", c, new Vector2(W, 50), new Vector2(0, 320), null);
        var on = Button(bar, "", new Vector2(-W / 2 + 95, 0), new Vector2(180, 44), () => { showOnline = true; playerTop = 0; RebuildOrder(); Refresh(); });
        var hi = Button(bar, "", new Vector2(-W / 2 + 285, 0), new Vector2(180, 44), () => { showOnline = false; playerTop = 0; RebuildOrder(); Refresh(); });
        tabOnline = on.GetComponentInChildren<Text>(); tabHistory = hi.GetComponentInChildren<Text>();
        rangeText = Label(bar, "", new Vector2(W / 2 - 250, 0), new Vector2(170, 44), 16, Ink, TextAnchor.MiddleRight);
        Button(bar, "Up", new Vector2(W / 2 - 135, 0), new Vector2(60, 40), () => ScrollPlayers(-1));
        Button(bar, "Down", new Vector2(W / 2 - 40, 0), new Vector2(90, 40), () => ScrollPlayers(1));
        refreshers.Add(() => { Paint(on, showOnline); Paint(hi, !showOnline); tabOnline.text = $"In game ({onlineIds.Count})"; int hist = History().Count(); tabHistory.text = $"History ({hist})"; });
        // column headings, lined up with the row's buttons below
        var head = Rect("Head", c, new Vector2(W, 30), new Vector2(0, 278), null);
        Label(head, "Player", new Vector2(-W / 2 + 18 + 120, 0), new Vector2(240, 28), 15, Ink, TextAnchor.MiddleLeft);
        string[] cols = { "Build", "Demolish", "Sell", "Shop", "Capsules" };
        for (int q = 0; q < cols.Length; q++) Label(head, cols[q], new Vector2(-95 + q * 82, 0), new Vector2(80, 28), 15, Ink, TextAnchor.MiddleCenter);
        Label(head, "Passes", new Vector2(330, 0), new Vector2(90, 28), 15, Ink, TextAnchor.MiddleCenter);
        for (int k = 0; k < visibleRows; k++)
        {
            int slot = k; float y = 236 - k * 54;
            var row = Rect("Player", c, new Vector2(W, 48), new Vector2(0, y), Panel); Shade(row, false);
            var t = Label(row, "", new Vector2(-W / 2 + 18 + 150, 0), new Vector2(300, 46), 15, Ink, TextAnchor.MiddleLeft);
            var perms = new List<(Node, Image, Text)>();
            for (int q = 0; q < PermNodes.Length; q++)
            {
                var (node, _) = PermNodes[q];
                var pb = Button(row, "", new Vector2(-95 + q * 82, 0), new Vector2(76, 36), () => { int i = playerTop + slot; if (i < playerOrder.Count) { TogglePerm(playerOrder[i], node); Refresh(); } });
                perms.Add((node, pb.GetComponent<Image>(), pb.GetComponentInChildren<Text>()));
            }
            var bb = Button(row, "", new Vector2(330, 0), new Vector2(90, 36), () => { int i = playerTop + slot; if (i < playerOrder.Count) OpenPasses(win, playerOrder[i]); });
            Button(row, "Manage", new Vector2(415, 0), new Vector2(76, 36), () => { int i = playerTop + slot; if (i < playerOrder.Count) OpenManage(win, playerOrder[i]); }, true);
            playerSlots.Add((row, t, bb.GetComponentInChildren<Text>(), perms));
        }
        Hint(c, "* differs from what new players start with (Rules tab). Manage > Reset to defaults sets them back.", 236 - visibleRows * 54 - 8);
        var none = Label(c, "", new Vector2(0, 236), new Vector2(W, 48), 18, Muted, TextAnchor.MiddleCenter);
        refreshers.Add(() => none.text = playerOrder.Count == 0 ? (showOnline ? "Nobody else is in the game." : "No one else has played here yet.") : "");
        RebuildOrder();
    }

    // ---------------- Bases
    static void BuildBases(RectTransform c)
    {
        Hint(c, "Protected bases: crew-mates within the distance can look, but can't build, demolish, sell, shop or handle capsules.", 320);
        Hint(c, "Vehicles still work there. Give a player a pass for a base with Passes on the Players tab.", 290);
        for (int i = 0; i < Plugin.Bases.Length; i++)
        {
            string b = Plugin.Bases[i]; var row = RowBox(c, 230 - i * 66, 58);
            var title = RowTitle(row, b + " base", 260);
            Stepper(row, W / 2 - 190, () => $"{Plugin.ZoneMetres[b].Value:0} m",
                () => Plugin.ZoneMetres[b].Value = Mathf.Max(50, Plugin.ZoneMetres[b].Value - 50), () => Plugin.ZoneMetres[b].Value = Mathf.Min(2000, Plugin.ZoneMetres[b].Value + 50));
            Toggle(row, W / 2 - 14, () => Plugin.ZoneOn[b].Value, () => Plugin.ZoneOn[b].Value = !Plugin.ZoneOn[b].Value, "PROTECTED", "open", 160);
        }
    }

    // ---------------- Rules
    static void BuildRules(RectTransform c)
    {
        var r1 = RowBox(c, 300); RowTitle(r1, "Spending limit per player, any 30 minutes");
        Stepper(r1, W / 2 - 14, () => $"${Plugin.Cap.Value:N0}", () => Plugin.Cap.Value = Mathf.Max(10000, Plugin.Cap.Value - 10000), () => Plugin.Cap.Value = Mathf.Min(10000000, Plugin.Cap.Value + 10000));
        Hint(c, "Trusted players have no limit (Manage on the Players tab).", 258);
        var r2 = RowBox(c, 200); RowTitle(r2, "Clean up loose capsules and bricks every");
        Stepper(r2, W / 2 - 140, () => $"{Plugin.CleanupMinutes.Value:0} min", () => Plugin.CleanupMinutes.Value = Mathf.Max(5, Plugin.CleanupMinutes.Value - 5), () => Plugin.CleanupMinutes.Value = Mathf.Min(240, Plugin.CleanupMinutes.Value + 5));
        Toggle(r2, W / 2 - 14, () => Plugin.CleanupOn.Value, () => Plugin.CleanupOn.Value = !Plugin.CleanupOn.Value);
        var r3 = RowBox(c, 130); RowTitle(r3, "Ban reclaim and cleanup speed");
        Choice(r3, W / 2 - 14, new[] { "Slow", "Fast" }, () => Plugin.FastRemoval.Value ? 1 : 0, k => Plugin.FastRemoval.Value = k == 1);
        Hint(c, "Slow: no visible hitch. Fast: about twice as fast, small hitches.", 88);
        var r4 = RowBox(c, 30); RowTitle(r4, "New players start with", 260);
        var nodes = Players.Nodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            var e = Plugin.StartWith[nodes[i]]; string n = nodes[i].ToString();
            Toggle(r4, W / 2 - 14 - (nodes.Length - 1 - i) * 116, () => e.Value, () => e.Value = !e.Value, n, n, 110);
        }
        Hint(c, "Given to a player the first time they're seen. Players tab: * marks a switch that differs from these.", -12);
    }

    // ---------------- Anti-cheat
    static void BuildCheats(RectTransform c)
    {
        var r1 = RowBox(c, 300); RowTitle(r1, "Anti-cheat (money checks + cheat watch)");
        Toggle(r1, W / 2 - 14, () => AntiCheat.Enabled.Value, () => AntiCheat.Enabled.Value = !AntiCheat.Enabled.Value);
        var r2 = RowBox(c, 234); RowTitle(r2, "A cheater gets");
        // "Restrict" is the setting None: refused and every permission off (that happens on any catch), nothing more
        string[] punish = { "Ban", "Kick", "None" };
        Choice(r2, W / 2 - 14, new[] { "Ban", "Kick", "Restrict" },
            () => Math.Max(0, Array.FindIndex(punish, p => p.Equals((AntiCheat.Punishment.Value ?? "").Trim(), StringComparison.OrdinalIgnoreCase))),
            k => AntiCheat.Punishment.Value = punish[k]);
        var r3 = RowBox(c, 168); RowTitle(r3, "When");
        Choice(r3, W / 2 - 14, new[] { "At once", "Ask me" }, () => AntiCheat.AutoBan.Value ? 0 : 1, k => AntiCheat.AutoBan.Value = k == 0, 140);
        Hint(c, "Ask me: a popup lets you ban (F7) or let it go (F8). Restrict: their permissions go off, they stay.", 126);
        var r4 = RowBox(c, 66); RowTitle(r4, "One trick paying more than this is cheating");
        var trick = AntiCheat.MaxTrickMoney;
        Stepper(r4, W / 2 - 14, () => $"${trick.Value:N0}", () => trick.Value = Math.Max(5000, trick.Value - 5000), () => trick.Value = Math.Min(10000000, trick.Value + 5000));
        var r5 = RowBox(c, -10); RowTitle(r5, "Cheat watch (trainers like WeMod)");
        Toggle(r5, W / 2 - 14, () => AntiCheat.Watch.Value, () => AntiCheat.Watch.Value = !AntiCheat.Watch.Value);
        Hint(c, "Catches: super speed, flying, long reach, oversized or far-off dust deposits, teleports that skip the map.", -56);
        Hint(c, "Never counts what was refused (permissions, protected bases), the host, or the first 20 s after joining.", -86);
        Hint(c, "Limits are in the config file under [AntiCheat].", -116);
    }

    // ---------------- Server
    static void BuildServer(RectTransform c)
    {
        Label(c, "Server name (shown in the server list)", new Vector2(0, 320), new Vector2(W, 30), 19, Ink, TextAnchor.MiddleLeft);
        nameField = Field(c, new Vector2(-90, 274), new Vector2(W - 200, 48));
        nameField.text = string.IsNullOrWhiteSpace(Plugin.SavedName.Value) ? (MoonSteamLobby.CrewName ?? "") : Plugin.SavedName.Value;
        Button(c, "Save", new Vector2(W / 2 - 85, 274), new Vector2(170, 48), () => status.text = ServerName.Set(nameField.text), true);
        var r1 = RowBox(c, 190); RowTitle(r1, "Welcome message when someone joins");
        Toggle(r1, W / 2 - 14, () => Plugin.WelcomeOn.Value, () => Plugin.WelcomeOn.Value = !Plugin.WelcomeOn.Value);
        // the welcome's first line, editable here (crew chat takes 120 characters; {name} and {server} are filled in)
        Label(c, "Welcome text   ({name} = the player, {server} = your server name)", new Vector2(0, 136), new Vector2(W, 30), 17, Ink, TextAnchor.MiddleLeft);
        welcomeField = Field(c, new Vector2(-90, 92), new Vector2(W - 200, 48), 120);
        welcomeField.text = Plugin.WelcomeText.Value;
        Button(c, "Save", new Vector2(W / 2 - 85, 92), new Vector2(170, 48), SaveWelcome, true);
        Hint(c, "Then one line per protected base is added by itself.", 50);
        Hint(c, "Crew Tools includes Crew Permissions by tylerun (nexusmods.com/moonsweepers/mods/71), with thanks.", -300);
    }

    static void SaveWelcome()
    {
        string t = (welcomeField.text ?? "").Trim();
        if (t.Length == 0) { status.text = "Welcome text can't be empty"; return; }
        Plugin.WelcomeText.Value = t;
        status.text = $"Welcome saved: \"{t}\"";
    }

    static void Refresh()
    {
        foreach (var r in refreshers) { try { r(); } catch { } }
        foreach (var (what, text) in manageRows)
        {
            if (text == null) continue;
            bool trusted = Players.Trusted(popupFor);
            text.text = what switch
            {
                "reset" => trusted ? "Reset spending (trusted: no limit anyway)" : $"Reset spending (spent ${Spending.Used(popupFor):0} of ${Plugin.Cap.Value:0})",
                "trust" => trusted ? "Untrust (spending limit again)" : "Trust (no spending limit)",
                "defaults" => "Reset switches to the new-player defaults",
                "restrict" => "Restrict: all permissions off",
                "kick" => armed == "kick" ? "Sure? Click again to KICK" : "Kick (they can rejoin)",
                "ban" => Banned(popupFor) ? "Unban (they can join again)" : armed == "ban" ? "Sure? Click again to BAN" : "Ban (reclaims what they built)",
                "forget" => armed == "forget" ? "Sure? Click again to REMOVE from the list" : "Remove from this list (when they're not in the game)",
                _ => what
            };
            text.color = (what == "kick" || what == "ban" || what == "forget") && armed == what ? new Color(1f, 0.55f, 0.45f) : Cream;
        }
        foreach (var (bse, text, bg) in popupRows)
        {
            bool on = Players.HasPass(popupFor, bse);
            if (text != null) text.text = $"{bse} base: {(on ? "ALLOWED" : "restricted")}";
            if (bg != null) bg.color = on ? Accent : Btn;
        }
        for (int k = 0; k < playerSlots.Count; k++)
        {
            var (row, label, basesText, perms) = playerSlots[k]; int idx = playerTop + k; bool has = idx < playerOrder.Count;
            if (row != null) row.gameObject.SetActive(has);
            if (!has) continue;
            ulong id = playerOrder[idx];
            if (row != null) Shade(row.transform, onlineIds.Contains(id));          // warm: in the game now
            if (basesText != null) { int np = Players.PassCount(id); basesText.text = np == 0 ? "none" : $"{np} base{(np == 1 ? "" : "s")}"; }
            string name = Players.Name(id);
            label.text = name + "\n" + (Players.Trusted(id) ? "trusted: no limit" : $"spent ${Spending.Used(id):N0} of ${Plugin.Cap.Value:N0}");
            try
            {
                var p = Players.Get(id);
                foreach (var (node, bg, tx) in perms)
                {
                    bool on = Players.Switch(p, node);
                    if (bg != null) bg.color = on ? Accent : Btn;
                    string mark = on != Players.Default(node) ? "*" : "";       // differs from the new-player default
                    if (tx != null) { tx.text = (on ? "ON" : "off") + mark; tx.color = on ? Ink : Dim; tx.fontSize = 16; }
                }
            }
            catch { }
        }
        if (rangeText != null)
        {
            int cnt = playerOrder.Count;
            rangeText.text = cnt > visibleRows ? $"{playerTop + 1}-{Math.Min(cnt, playerTop + visibleRows)} of {cnt}" : "";
        }
    }

    // pick which protected bases one player may build in - only the bases protected now are listed
    static void OpenPasses(RectTransform win, ulong id)
    {
        ClosePasses(); popupFor = id;
        var bases = new List<string>(); foreach (var b in Plugin.Bases) if (Plugin.ZoneOn[b].Value) bases.Add(b);
        string name = Players.Name(id);
        float h = 150 + Math.Max(1, bases.Count) * 58;
        popup = Rect("Passes", win, new Vector2(560, h), new Vector2(0, 0), new Color(0.12f, 0.12f, 0.15f, 0.98f));
        Label(popup, $"{name}: protected bases", new Vector2(0, h / 2 - 40), new Vector2(520, 40), 22, Cream, TextAnchor.MiddleCenter);
        if (bases.Count == 0) Label(popup, "No bases are protected right now.", new Vector2(0, h / 2 - 100), new Vector2(520, 40), 18, Dim, TextAnchor.MiddleCenter);
        for (int i = 0; i < bases.Count; i++)
        {
            string b = bases[i];
            var tb = Button(popup, "", new Vector2(0, h / 2 - 100 - i * 58), new Vector2(480, 48), () => { Players.SetPass(popupFor, b, !Players.HasPass(popupFor, b)); Refresh(); });
            popupRows.Add((b, tb.GetComponentInChildren<Text>(), tb.GetComponent<Image>()));
        }
        Button(popup, "Done", new Vector2(0, -h / 2 + 40), new Vector2(160, 46), () => { ClosePasses(); Refresh(); }, true);
        Refresh();
    }
    // one player's controls: spending (reset, trust) and discipline (restrict, kick, ban - kick and ban ask twice; a
    // banned player's Ban row is Unban, one click, also from History while they're away)
    static readonly List<(string what, Text text)> manageRows = new(); static string armed; static Text manageNote;
    static void OpenManage(RectTransform win, ulong id)
    {
        ClosePasses(); popupFor = id; armed = null;
        string name = Players.Name(id);
        const float h = 652;
        popup = Rect("Manage", win, new Vector2(560, h), new Vector2(0, 0), new Color(0.12f, 0.12f, 0.15f, 0.98f));
        Label(popup, name, new Vector2(0, h / 2 - 40), new Vector2(520, 40), 24, Cream, TextAnchor.MiddleCenter);
        string[] kinds = { "reset", "trust", "defaults", "restrict", "kick", "ban", "forget" };
        for (int i = 0; i < kinds.Length; i++)
        {
            string k = kinds[i];
            var tb = Button(popup, "", new Vector2(0, h / 2 - 100 - i * 62), new Vector2(480, 52), () => Act(k));
            manageRows.Add((k, tb.GetComponentInChildren<Text>()));
        }
        manageNote = Label(popup, "", new Vector2(0, -h / 2 + 100), new Vector2(520, 50), 17, Dim, TextAnchor.MiddleCenter);
        // the vehicle refund preview, in the note line under the buttons (a line under the title didn't show - user)
        var (vc, vb, vu) = Owners.VehicleValue(id, name);
        vehicleLine = vc == 0 ? "No vehicles." : $"{vc} vehicle{(vc == 1 ? "" : "s")}: leaving refunds ${vb:0}; a ban refunds ${vb + vu:0} (+ upgrades ${vu:0})";
        manageNote.text = vehicleLine; manageNote.color = Cream;
        Button(popup, "Done", new Vector2(0, -h / 2 + 40), new Vector2(160, 46), () => { ClosePasses(); Refresh(); }, true);
        Refresh();
    }
    static void Act(string k)
    {
        ulong id = popupFor; string name = Players.Name(id);
        bool host = Crew.Hosting;
        if (k == "reset") { armed = null; Spending.ResetBank(id); Note($"{name}: full spending cap again."); }
        else if (k == "trust") { armed = null; Players.SetTrusted(id, !Players.Trusted(id)); Note(Players.Trusted(id) ? $"{name}: no spending limit." : $"{name}: spending limit applies again."); }
        else if (k == "defaults") { armed = null; Players.SetDefaults(Players.Get(id)); Note($"{name}: switches set to the new-player defaults."); }
        else if (k == "restrict") { armed = null; Players.RevokeAll(id); Note($"{name}: every permission off. Switch them back on in their row."); }
        else if (k == "forget")
        {
            if (Crew.Online(id)) { armed = null; Note($"{name} is in the game now: remove them after they leave."); }
            else if (armed != "forget") { armed = "forget"; Note($"Click again to remove {name} from this list (their trust and passes are forgotten)."); }
            else
            {
                armed = null; Players.Forget(id); playerOrder.Remove(id);
                playerTop = Math.Clamp(playerTop, 0, Math.Max(0, playerOrder.Count - visibleRows));
                ClosePasses(); Refresh(); return;
            }
        }
        else if (k == "ban" && Banned(id))
        {
            armed = null;
            try { MoonSteamLobby.Unban(id); Plugin.L.LogInfo($"host unbanned {name} ({id})"); Note($"{name} is unbanned: they can join again (their switches stay as they are)."); }
            catch (Exception e) { Note($"Couldn't unban: {e.Message}"); }
        }
        else if (k == "kick" || k == "ban")
        {
            if (!host) { armed = null; Note("Only while you host a game."); }
            else if (!Crew.Online(id)) { armed = null; Note($"{name} isn't in the game right now."); }
            else if (armed != k) { armed = k; Note($"Click again to {k} {name}."); }
            else
            {
                armed = null;
                try
                {
                    if (k == "kick") MoonSteamLobby.Kick(id); else MoonSteamLobby.Ban(id);
                    Plugin.L.LogInfo($"host {(k == "kick" ? "kicked" : "banned")} {name} ({id})");
                    Note(k == "kick" ? $"{name} was kicked (they can rejoin)." : $"{name} was banned; what they built is being removed and refunded.");
                }
                catch (Exception e) { Note($"Couldn't {k}: {e.Message}"); }
            }
        }
        Refresh();
    }
    // banned from your games (the game's own ban list)
    static bool Banned(ulong id) { try { return MoonCrewBans.IsBanned(id); } catch { return false; } }

    static string vehicleLine = "";
    static void Note(string t) { if (manageNote != null) manageNote.text = t + (string.IsNullOrEmpty(vehicleLine) ? "" : "\n" + vehicleLine); }

    static void ClosePasses() { manageRows.Clear(); manageNote = null; armed = null; popupRows.Clear(); if (popup != null) Widgets.Destroy(popup.gameObject); popup = null; }

    static void ClosePanel() { ClosePasses(); Widgets.Destroy(panel); panel = null; panelRay = null; nameField = null; welcomeField = null; }

    // Tools.Stop: the window and the button go
    internal static void Stop()
    {
        if (panel != null) ClosePanel();
        if (buttonLayer != null) { Widgets.Destroy(buttonLayer); buttonLayer = null; btnRay = null; }
        pause = null;
    }
}
