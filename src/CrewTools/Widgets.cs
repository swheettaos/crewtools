// Widgets: the one UI kit Crew Tools draws with - the CREW TOOLS window (Ui) and the cheating popup (Alerts). Unity's
// own UI (UnityEngine.UI) on canvases of our own, sized for a 1920 x 1080 screen and scaled by height, so it fits any
// screen (Steam Deck 1280 x 800, ultrawide). Positions are from the parent's centre.
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
        Grey = new(0.42f, 0.42f, 0.45f, 1f), Soft = new(0.75f, 0.74f, 0.7f, 1f);

    static readonly Dictionary<int, Action> actions = new();     // button -> what a click on it does

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
        actions.Clear();
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
        if (bg.HasValue) go.AddComponent<Image>().color = bg.Value;
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
        var r = Rect("Field", parent, size, pos, Btn); var f = r.gameObject.AddComponent<InputField>();
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
        var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = r.GetComponent<Image>();
        actions[r.gameObject.GetInstanceID()] = action;
        Label(r, label, Vector2.zero, size - new Vector2(8, 4), close ? 34 : 20, accent || close ? Ink : Cream, TextAnchor.MiddleCenter);
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
