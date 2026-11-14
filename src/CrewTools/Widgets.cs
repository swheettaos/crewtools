// Widgets: the one UI kit Crew Tools draws with - the CREW TOOLS window (Ui) and the cheating popup (Alerts). Unity's
// own UI (UnityEngine.UI) on canvases of our own, sized for a 1920 x 1080 screen and scaled by height, so it fits any
// screen (Steam Deck 1280 x 800, ultrawide). Positions are from the parent's centre.
//
// The look matches Quantum Conveyors' menu: rounded surfaces (panels and rows 12 units, buttons 6, round help dots),
// a soft gradient across rows, its cream panel with cream rows (the gradient sets them apart), dark text, grey buttons
// and orange accent; popups and tooltips are dark. The rounded and gradient
// sprites are drawn once, at 2x, so their corners stay smooth.
//
// Buttons don't use onClick (a listener also fired on release, so Ban / Kick / Restrict stepped twice): their action is
// kept here and run by Click() on the press.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrewTools;

internal static class Widgets
{
    internal static readonly Color Panel = new(0.937f, 0.922f, 0.882f, 1f), Ink = new(0.110f, 0.133f, 0.196f, 1f),
        Row = new(0.165f, 0.165f, 0.196f, 1f), Btn = new(0.286f, 0.286f, 0.294f, 1f), Accent = new(1f, 0.478f, 0.180f, 1f),
        Cream = new(0.949f, 0.929f, 0.894f, 1f), Dim = new(0.6f, 0.58f, 0.55f, 1f), Red = new(0.85f, 0.26f, 0.22f, 1f),
        Grey = new(0.42f, 0.42f, 0.45f, 1f), Soft = new(0.75f, 0.74f, 0.7f, 1f),
        Muted = new(0.47f, 0.45f, 0.43f, 1f);                     // secondary text on the cream (QC's dim)

    static readonly Dictionary<int, Action> actions = new();     // button -> what a click on it does

    // ---------------------------------------------------------------- the rounded look
    static Sprite rounded, button, circle, warm, cool;

    static void EnsureSprites()
    {
        if (rounded != null) return;
        rounded = RoundedSprite(256, 256, 24, "Crew Tools Rounded");
        button = RoundedSprite(256, 256, 12, "Crew Tools Button");
        circle = RoundedSprite(256, 256, 128, "Crew Tools Circle");
        warm = Gradient(new Color(Accent.r, Accent.g, Accent.b, 0.22f), new Color(Accent.r, Accent.g, Accent.b, 0f), "Crew Tools Warm Gradient");
        cool = Gradient(new Color(Ink.r, Ink.g, Ink.b, 0.24f), new Color(Ink.r, Ink.g, Ink.b, 0f), "Crew Tools Cool Gradient");
    }

    static Sprite RoundedSprite(int w, int h, int radius, string name)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, RoundedAlpha(x + 0.5f, y + 0.5f, w, h, radius));
        t.SetPixels(px); t.Apply(false, true);
        return Sprite.Create(t, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static Sprite Gradient(Color left, Color right, string name)
    {
        const int w = 256, h = 128, radius = 24;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var c = Color.Lerp(left, right, x / (float)(w - 1)); c.a *= RoundedAlpha(x + 0.5f, y + 0.5f, w, h, radius);
            px[y * w + x] = c;
        }
        t.SetPixels(px); t.Apply(false, true);
        return Sprite.Create(t, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static float RoundedAlpha(float x, float y, float w, float h, float radius)
    {
        float cx = Mathf.Clamp(x, radius, w - radius), cy = Mathf.Clamp(y, radius, h - radius);
        return Mathf.Clamp01(radius + 0.75f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));
    }

    // a rounded surface on an image; small ones (thin bars) stay square
    static void Round(Image img, Vector2 size, Sprite sprite)
    {
        if (img == null || size.x < 24 || size.y < 24) return;
        img.sprite = sprite; img.type = Image.Type.Sliced;
    }

    // the soft gradient across a row, as Quantum Conveyors' rows have: warm (orange) for something live, cool otherwise
    internal static void Shade(RectTransform row, bool warmTone)
    {
        EnsureSprites();
        var g = Rect("Shade", row, row.sizeDelta, Vector2.zero, null);
        var img = g.gameObject.AddComponent<Image>(); img.sprite = warmTone ? warm : cool; img.type = Image.Type.Sliced; img.raycastTarget = false;
        g.SetAsFirstSibling();
    }

    // repaint a row's gradient (live or not)
    internal static void Shade(Transform row, bool warmTone)
    {
        var g = row.Find("Shade"); var img = g != null ? g.GetComponent<Image>() : null;
        if (img != null) img.sprite = warmTone ? warm : cool;
    }

    // a round "?" that shows a dark tooltip while the mouse is over it (Tooltip, each frame)
    internal static RectTransform Help(Transform parent, Vector2 pos, string text, Vector2 tipSize, Vector2 tipPos)
    {
        EnsureSprites();
        var dot = Rect("Help", parent, new Vector2(30, 30), pos, Btn);
        var img = dot.GetComponent<Image>(); img.sprite = circle; img.type = Image.Type.Sliced;
        var q = Label(dot, "?", Vector2.zero, new Vector2(26, 26), 18, Cream, TextAnchor.MiddleCenter); q.fontStyle = FontStyle.Bold;
        var tip = Rect("Tip", parent, tipSize, tipPos, Row);
        var tx = Label(tip, text, Vector2.zero, tipSize - new Vector2(28, 24), 16, Cream, TextAnchor.UpperLeft);
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tip.gameObject.SetActive(false);
        tips.Add((dot, tip));
        return dot;
    }
    static readonly List<(RectTransform dot, RectTransform tip)> tips = new();

    // each frame: a help dot's tooltip shows while the mouse is over the dot
    internal static void Tooltips()
    {
        var mouse = Mouse.current; if (mouse == null) return;
        var at = mouse.position.ReadValue();
        for (int i = tips.Count - 1; i >= 0; i--)
        {
            var (dot, tip) = tips[i];
            if (dot == null || tip == null) { tips.RemoveAt(i); continue; }
            bool over = dot.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(dot, at, null);
            if (tip.gameObject.activeSelf != over) { tip.gameObject.SetActive(over); if (over) tip.SetAsLastSibling(); }
        }
    }

    // a canvas of its own, drawn above the game's UI (higher order = on top), with a raycaster for Click
    internal static GameObject Layer(string name, int order, out GraphicRaycaster ray)
    {
        var go = new GameObject(name); UnityEngine.Object.DontDestroyOnLoad(go);
        var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        ray = go.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            ownEventSystem = new GameObject("Crew Tools EventSystem"); ownEventSystem.AddComponent<EventSystem>(); UnityEngine.Object.DontDestroyOnLoad(ownEventSystem);
        }
        return go;
    }

    static GameObject ownEventSystem;                     // made here when the game had none; Tools.Stop removes it
    internal static void Stop()
    {
        if (ownEventSystem != null) { UnityEngine.Object.Destroy(ownEventSystem); ownEventSystem = null; }
        actions.Clear(); tips.Clear();
    }

    // remove a layer or part of one, and forget its buttons
    internal static void Destroy(GameObject go)
    {
        if (go == null) return;
        foreach (var b in go.GetComponentsInChildren<UnityEngine.UI.Button>(true)) actions.Remove(b.gameObject.GetInstanceID());
        UnityEngine.Object.Destroy(go);
    }

    internal static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos, Color? bg)
    {
        var go = new GameObject(name); var r = go.AddComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = size; r.anchoredPosition = pos;
        if (bg.HasValue) { EnsureSprites(); var img = go.AddComponent<Image>(); img.color = bg.Value; Round(img, size, rounded); }
        return r;
    }

    internal static Text Label(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align)
    {
        var t = Rect("Text", parent, size, pos, null).gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); t.text = text; t.fontSize = fontSize; t.color = color; t.alignment = align;
        t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    internal static InputField Field(Transform parent, Vector2 pos, Vector2 size, int maxChars = 60)
    {
        var r = Rect("Field", parent, size, pos, Btn); Round(r.GetComponent<Image>(), size, button); var f = r.gameObject.AddComponent<InputField>();
        f.targetGraphic = r.GetComponent<Image>();
        f.textComponent = Label(r, string.Empty, Vector2.zero, size - new Vector2(16, 4), 22, Cream, TextAnchor.MiddleLeft);
        f.textComponent.rectTransform.anchoredPosition = new Vector2(8, 0); f.characterLimit = maxChars;
        return f;
    }

    // accent: orange with dark text; colour: any other background; "×" is a bare close cross
    internal static UnityEngine.UI.Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action action, bool accent = false, Color? colour = null)
    {
        bool close = label == "×";
        var r = Rect("Button", parent, size, pos, colour ?? (accent ? Accent : close ? new Color(0, 0, 0, 0) : Btn));
        Round(r.GetComponent<Image>(), size, button);
        var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = r.GetComponent<Image>();
        actions[r.gameObject.GetInstanceID()] = action;
        var t = Label(r, label, Vector2.zero, size - new Vector2(8, 4), close ? 34 : 19, accent ? Ink : close ? Dim : Cream, TextAnchor.MiddleCenter);
        if (accent) t.fontStyle = FontStyle.Bold;
        return b;
    }

    // a switch or choice button: orange when lit, grey when not
    internal static void Paint(UnityEngine.UI.Button b, bool lit)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>(); var tx = b.GetComponentInChildren<Text>();
        if (img != null) img.color = lit ? Accent : Btn;
        if (tx != null) tx.color = lit ? Ink : Cream;
    }

    // A left-click press on this layer: runs the button under the mouse, or focuses a text field. With modal set (a
    // pop-up), only clicks inside it count, and a click on its background is swallowed.
    internal static void Click(GraphicRaycaster ray, Transform modal = null)
    {
        var mouse = Mouse.current; var es = EventSystem.current;
        if (ray == null || mouse == null || es == null || !mouse.leftButton.wasPressedThisFrame) return;
        var ev = new PointerEventData(es) { position = mouse.position.ReadValue() };
        var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>(); ray.Raycast(ev, hits);
        var en = hits.GetEnumerator();
        while (en.MoveNext())
        {
            var go = en.Current.gameObject; if (go == null) continue;
            if (modal != null && !go.transform.IsChildOf(modal)) continue;
            var b = go.GetComponentInParent<UnityEngine.UI.Button>();
            if (b != null) { if (actions.TryGetValue(b.gameObject.GetInstanceID(), out var act)) act(); break; }
            if (modal != null) break;
            var f = go.GetComponentInParent<InputField>(); if (f != null) { es.SetSelectedGameObject(f.gameObject); f.ActivateInputField(); break; }
        }
    }
}
