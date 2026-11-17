using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KingsCourt.EditorTools
{
    /// <summary>
    /// Period 21 (Greybox Screens): five greybox UI panels in their own scene, navigation-only wiring
    /// (Button.onClick only ever calls GameObject.SetActive), no game logic, no colour, no imported art.
    /// Icon badges reuse the same four action-type letters as the printed player aid / rules sheet
    /// (Execute, Reveal, Swap, Cards), built from a grey square + a single TMP glyph - no imported sprites.
    /// Menu: King's Court / Build Greybox Screens Scene.
    /// </summary>
    public static class GreyboxScreensBuilder
    {
        static readonly Color Grey1 = new Color(0.80f, 0.80f, 0.80f); // panel background
        static readonly Color Grey2 = new Color(0.64f, 0.64f, 0.64f); // button / region
        static readonly Color Grey3 = new Color(0.46f, 0.46f, 0.46f); // frame / header band
        static readonly Color Grey4 = new Color(0.30f, 0.30f, 0.30f); // icon badge
        static readonly Color Ink = new Color(0.08f, 0.08f, 0.08f);

        [MenuItem("King's Court/Build Greybox Screens Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var p1 = MakePanel(canvasGO.transform, "01_MainMenu");
            var p2 = MakePanel(canvasGO.transform, "02_Setup");
            var p3 = MakePanel(canvasGO.transform, "03_MainPlay");
            var p4 = MakePanel(canvasGO.transform, "04_CardZoom");
            var p5 = MakePanel(canvasGO.transform, "05_End");

            BuildMainMenu(p1, p2);
            BuildSetup(p2, p1, p3);
            BuildMainPlay(p3, p1, p4);
            BuildCardZoom(p4, p3);
            BuildEnd(p5, p1, p2);

            // Exactly one panel active at a time.
            p1.SetActive(true);
            p2.SetActive(false);
            p3.SetActive(false);
            p4.SetActive(false);
            p5.SetActive(false);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Greybox_Screens.unity");
            Debug.Log("GREYBOX_SCENE_BUILT path=Assets/Scenes/Greybox_Screens.unity");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- screen builders

        static void BuildMainMenu(GameObject panel, GameObject setup)
        {
            MakeLabel(panel.transform, "Title", "KING'S COURT", 72, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.86f), Ink);
            MakeLabel(panel.transform, "Subtitle", "a game of loyalty, lies and an uncomfortable throne",
                28, TextAlignmentOptions.Center, new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.62f), Grey4);

            var play = MakeButton(panel.transform, "Btn_Play", "PLAY",
                new Vector2(0.38f, 0.33f), new Vector2(0.62f, 0.44f));
            Nav(play, panel, setup);

            MakeLabel(panel.transform, "Lbl_Options", "settings     credits     quit", 20, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.27f), Grey3);
        }

        static void BuildSetup(GameObject panel, GameObject mainMenu, GameObject mainPlay)
        {
            MakeLabel(panel.transform, "Header", "SETUP", 48, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.94f), Ink);
            MakeLabel(panel.transform, "Lbl_PlayerCount", "Player count: 5–10 (host sets this by hand)",
                26, TextAlignmentOptions.Left, new Vector2(0.14f, 0.6f), new Vector2(0.86f, 0.68f), Ink);
            MakeLabel(panel.transform, "Lbl_Variant", "Variant: standard roles",
                26, TextAlignmentOptions.Left, new Vector2(0.14f, 0.5f), new Vector2(0.86f, 0.58f), Ink);

            var back = MakeButton(panel.transform, "Btn_Back", "BACK",
                new Vector2(0.14f, 0.18f), new Vector2(0.34f, 0.28f));
            Nav(back, panel, mainMenu);

            var confirm = MakeButton(panel.transform, "Btn_Confirm", "CONFIRM",
                new Vector2(0.66f, 0.18f), new Vector2(0.86f, 0.28f));
            Nav(confirm, panel, mainPlay);
        }

        static void BuildMainPlay(GameObject panel, GameObject mainMenu, GameObject cardZoom)
        {
            MakeRegion(panel.transform, "Rgn_Board", new Vector2(0.06f, 0.42f), new Vector2(0.72f, 0.88f), Grey2);
            MakeLabel(panel.transform, "Lbl_BoardFrame", "THE TABLE", 22, TextAlignmentOptions.TopLeft,
                new Vector2(0.07f, 0.42f), new Vector2(0.4f, 0.88f), Grey4);

            MakeRegion(panel.transform, "Rgn_StatusBar", new Vector2(0.74f, 0.7f), new Vector2(0.94f, 0.88f), Grey2);
            MakeLabel(panel.transform, "Lbl_Turn", "Whose turn: Seat 3", 20, TextAlignmentOptions.TopLeft,
                new Vector2(0.75f, 0.7f), new Vector2(0.93f, 0.88f), Ink);
            MakeLabel(panel.transform, "Lbl_Goal", "Round 2 of ?\nGoal: execute both Traitors", 18, TextAlignmentOptions.TopLeft,
                new Vector2(0.74f, 0.52f), new Vector2(0.94f, 0.68f), Ink);

            var backBtn = MakeButton(panel.transform, "Btn_Menu", "MENU",
                new Vector2(0.74f, 0.42f), new Vector2(0.94f, 0.5f));
            Nav(backBtn, panel, mainMenu);

            MakeLabel(panel.transform, "Lbl_Hand", "YOUR HAND", 20, TextAlignmentOptions.Left,
                new Vector2(0.06f, 0.32f), new Vector2(0.3f, 0.38f), Grey4);

            // Five hand-card slots; each opens Card Zoom. Each carries an icon badge reusing the same
            // action-type letters as the printed player aid (Execute / Reveal / Swap / Cards).
            string[] icons = { "X", "R", "S", "C", "X" };
            for (int i = 0; i < 5; i++)
            {
                float x0 = 0.06f + i * 0.128f;
                float x1 = x0 + 0.11f;
                var card = MakeButton(panel.transform, "Btn_Hand" + (i + 1), "CARD " + (i + 1),
                    new Vector2(x0, 0.06f), new Vector2(x1, 0.3f));
                Nav(card, panel, cardZoom);
                MakeIconBadge(card.transform, icons[i], new Vector2(0.62f, 0.62f), new Vector2(0.92f, 0.92f));
            }
        }

        static void BuildCardZoom(GameObject panel, GameObject mainPlay)
        {
            MakeRegion(panel.transform, "Rgn_CardLarge", new Vector2(0.32f, 0.2f), new Vector2(0.68f, 0.86f), Grey2);
            MakeIconBadge(panel.transform, "R", new Vector2(0.6f, 0.76f), new Vector2(0.66f, 0.84f));
            MakeLabel(panel.transform, "Lbl_CardName", "CARD NAME", 30, TextAlignmentOptions.Center,
                new Vector2(0.33f, 0.68f), new Vector2(0.67f, 0.76f), Ink);
            MakeLabel(panel.transform, "Lbl_CardCost", "Cost: 1 Action Card", 22, TextAlignmentOptions.Center,
                new Vector2(0.33f, 0.58f), new Vector2(0.67f, 0.66f), Grey4);
            MakeLabel(panel.transform, "Lbl_CardEffect", "Effect text goes here, exactly as printed on the real card.",
                20, TextAlignmentOptions.Center, new Vector2(0.34f, 0.3f), new Vector2(0.66f, 0.56f), Ink);

            var dismiss = MakeButton(panel.transform, "Btn_Dismiss", "DISMISS",
                new Vector2(0.42f, 0.08f), new Vector2(0.58f, 0.17f));
            Nav(dismiss, panel, mainPlay);
        }

        static void BuildEnd(GameObject panel, GameObject mainMenu, GameObject setup)
        {
            MakeLabel(panel.transform, "Header", "THE COURT HAS DECIDED", 48, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.9f), Ink);
            MakeLabel(panel.transform, "Lbl_Winner", "Winner: Loyalists", 32, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.72f), Ink);
            MakeLabel(panel.transform, "Lbl_Breakdown", "Both Traitors executed on round 6.\nReason: Knight's Duty, then a forced reveal.",
                22, TextAlignmentOptions.Center, new Vector2(0.15f, 0.46f), new Vector2(0.85f, 0.6f), Grey4);

            var again = MakeButton(panel.transform, "Btn_PlayAgain", "PLAY AGAIN",
                new Vector2(0.3f, 0.2f), new Vector2(0.5f, 0.3f));
            Nav(again, panel, setup);

            var menu = MakeButton(panel.transform, "Btn_MainMenu", "MAIN MENU",
                new Vector2(0.52f, 0.2f), new Vector2(0.72f, 0.3f));
            Nav(menu, panel, mainMenu);
        }

        // ---------------------------------------------------------------- shape/widget helpers (grey, no imported art)

        static GameObject MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Grey1;
            return go;
        }

        static GameObject MakeRegion(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject MakeLabel(Transform parent, string name, string text, int fontSize,
            TextAlignmentOptions align, Vector2 aMin, Vector2 aMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.alignment = align; tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return go;
        }

        static Button MakeButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Grey2;
            var btn = go.GetComponent<Button>();
            MakeLabel(go.transform, "Label", label, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Ink);
            return btn;
        }

        /// <summary>A small grey square + one bold glyph - the same four action-type letters the printed
        /// player aid and rules sheet use (X=Execute, R=Reveal, S=Swap, C=Cards/draw-discard). No imported
        /// sprite: just a UI Image and a TMP glyph, per the greybox-only rule.</summary>
        static void MakeIconBadge(Transform parent, string glyph, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("Icon_" + glyph, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Grey4;
            MakeLabel(go.transform, "Glyph", glyph, 22, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
        }

        static void Nav(Button btn, GameObject hide, GameObject show)
        {
            UnityEventTools.AddBoolPersistentListener(btn.onClick, hide.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(btn.onClick, show.SetActive, true);
        }
    }
}
