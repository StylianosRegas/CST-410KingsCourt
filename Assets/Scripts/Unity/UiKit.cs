using System;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>Small helpers so screens can be built in code (no hand-made prefabs needed).</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(.12f, .1f, .1f);
        public static readonly Color Paper = new Color(.97f, .94f, .86f);
        public static readonly Color Gold = new Color(1f, .82f, .25f);
        public static readonly Color Night = new Color(.09f, .08f, .11f, .96f);
        public static readonly Color Panel = new Color(.16f, .14f, .19f, .97f);
        public static readonly Color Button = new Color(.55f, .18f, .2f);
        public static readonly Color ButtonAlt = new Color(.25f, .27f, .36f);

        private static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Destroy that also works when not in Play mode (screenshot tool).</summary>
        public static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Box(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>Place by anchor point + size.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string text, Color bg, Action onClick, int fontSize = 30, Vector2? size = null)
        {
            var rt = Box(parent, "Button " + text, bg);
            if (size.HasValue) rt.sizeDelta = size.Value;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(.75f, .75f, .75f);
            colors.disabledColor = new Color(.5f, .5f, .5f, .6f);
            btn.colors = colors;
            btn.targetGraphic = rt.GetComponent<Image>();
            var label = Label(rt, text, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch((RectTransform)label.transform, 8, 4, 8, 4);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (size.HasValue) { le.preferredWidth = size.Value.x; le.preferredHeight = size.Value.y; }
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var rt = Rect(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static void Size(Component c, float w, float h)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
        }

        public static InputField Input(Transform parent, string placeholder, int fontSize, int maxChars)
        {
            var rt = Box(parent, "Input", new Color(1, 1, 1, .95f));
            var field = rt.gameObject.AddComponent<InputField>();
            var text = Label(rt, "", fontSize, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.supportRichText = false;
            Stretch((RectTransform)text.transform, 12, 6, 12, 6);
            var ph = Label(rt, placeholder, fontSize, new Color(.5f, .5f, .5f), TextAnchor.MiddleCenter, FontStyle.Italic);
            Stretch((RectTransform)ph.transform, 12, 6, 12, 6);
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = maxChars;
            field.targetGraphic = rt.GetComponent<Image>();
            return field;
        }

        public static Toggle MakeToggle(Transform parent, string label, bool on, Action<bool> changed)
        {
            var row = HStack(parent, "Toggle " + label, 16, TextAnchor.MiddleLeft);
            Size(row, 900, 64);
            var boxRt = Box(row.transform, "Box", new Color(.3f, .3f, .38f));
            Size(boxRt, 56, 56);
            var check = Box(boxRt, "Check", Gold);
            Stretch(check, 8, 8, 8, 8);
            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxRt.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = on;
            toggle.onValueChanged.AddListener(v => { if (changed != null) changed(v); });
            var t = Label(row.transform, label, 30, Color.white, TextAnchor.MiddleLeft);
            Size(t, 780, 56);
            return toggle;
        }
    }
}
