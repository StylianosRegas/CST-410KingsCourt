using KingsCourt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Build 5's start-up scene: lists the five scenarios from ScenarioCatalog and lets a player choose
    /// one. Nothing scenario-specific lives here - every field shown comes off the chosen ScenarioConfig,
    /// and picking a different one produces a different setup with no code change.
    /// </summary>
    public sealed class ScenarioPicker
    {
        public readonly RectTransform Root;
        private readonly GameController c;

        public ScenarioPicker(Transform canvas, GameController controller)
        {
            c = controller;
            Root = UiKit.Box(canvas, "ScenarioPicker", new Color(0, 0, 0, .93f));
            UiKit.Stretch(Root);
            var card = UiKit.Box(Root, "Card", UiKit.Panel);
            UiKit.Place(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1400, 900));

            var title = UiKit.Label(card, "CHOOSE A SCENARIO", 44, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(900, 60));

            var viewport = UiKit.Box(card, "Viewport", new Color(0, 0, 0, .25f));
            UiKit.Stretch(viewport, 30, 90, 30, 100);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.padding = new RectOffset(20, 20, 16, 16);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;

            foreach (var scenario in ScenarioCatalog.All)
            {
                var row = UiKit.Box(content, "Scenario " + scenario.Id, new Color(1, 1, 1, .04f));
                UiKit.Size(row, 1300, 150);
                var head = UiKit.Label(row, scenario.DisplayName + "   (" + scenario.PlayerCount + "p, " + scenario.Experience + ")", 28, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Stretch((RectTransform)head.transform, 20, 104, 220, 10);
                var body = UiKit.Label(row, scenario.Description + "\nLead dial: " + scenario.LeadDial, 20, Color.white, TextAnchor.UpperLeft);
                UiKit.Stretch((RectTransform)body.transform, 20, 10, 220, 50);
                var cfg = scenario; // capture
                UiKit.MakeButton(row.transform, "PLAY", UiKit.Button, () => c.StartScenario(cfg), 26, new Vector2(170, 56));
                var btn = row.GetChild(row.childCount - 1) as RectTransform;
                UiKit.Place(btn, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-30, 0), new Vector2(170, 56));
            }

            UiKit.MakeButton(card, "CLOSE", UiKit.ButtonAlt, () => Hide(), 30, new Vector2(240, 60));
            var close = card.GetChild(card.childCount - 1) as RectTransform;
            UiKit.Place(close, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(240, 60));

            Root.gameObject.SetActive(false);
        }

        public void Show() { Root.gameObject.SetActive(true); Root.SetAsLastSibling(); }
        public void Hide() { Root.gameObject.SetActive(false); }
    }
}
