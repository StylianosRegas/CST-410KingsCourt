using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>The Help panel: how a round works, every role, every card. Content comes straight from the rules code.</summary>
    public sealed class HelpPanel
    {
        public readonly RectTransform Root;
        private readonly RectTransform content;
        private readonly Text tabHint;

        public HelpPanel(Transform canvas)
        {
            Root = UiKit.Box(canvas, "HelpPanel", new Color(0, 0, 0, .93f));
            UiKit.Stretch(Root);
            var card = UiKit.Box(Root, "Card", UiKit.Panel);
            UiKit.Place(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1400, 900));

            var title = UiKit.Label(card, "HOW TO PLAY", 44, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(900, 60));

            var tabs = UiKit.HStack(card, "Tabs", 14);
            UiKit.Place((RectTransform)tabs.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -92), new Vector2(900, 60));
            UiKit.MakeButton(tabs.transform, "The Round", UiKit.ButtonAlt, () => ShowRound(), 26, new Vector2(260, 56));
            UiKit.MakeButton(tabs.transform, "Roles", UiKit.ButtonAlt, () => ShowRoles(), 26, new Vector2(260, 56));
            UiKit.MakeButton(tabs.transform, "Cards", UiKit.ButtonAlt, () => ShowCards(), 26, new Vector2(260, 56));

            // Scrollable content area.
            var viewport = UiKit.Box(card, "Viewport", new Color(0, 0, 0, .25f));
            UiKit.Stretch(viewport, 30, 90, 30, 170);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            // A fresh RectTransform defaults to sizeDelta (100, 100). Left unset, that made this stretched
            // content 100px wider than its own viewport mask (50px overflowing past each edge, every line,
            // not just wrapped ones) — the actual cause of the clipped "How to Play" text.
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10; v.padding = new RectOffset(20, 20, 16, 16);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;

            tabHint = UiKit.Label(card, "Scroll with the mouse wheel or by dragging.", 20, new Color(.7f, .7f, .75f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)tabHint.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-250, 40), new Vector2(700, 34));

            UiKit.MakeButton(card, "CLOSE", UiKit.Button, () => Hide(), 30, new Vector2(240, 60));
            var close = card.GetChild(card.childCount - 1) as RectTransform;
            UiKit.Place(close, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 24), new Vector2(240, 60));

            ShowRound();
            Root.gameObject.SetActive(false);
        }

        public void Show() { Root.gameObject.SetActive(true); Root.SetAsLastSibling(); }
        public void Hide() { Root.gameObject.SetActive(false); }

        private void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--) UiKit.Kill(content.GetChild(i).gameObject);
            content.anchoredPosition = Vector2.zero;
        }

        private void Paragraph(string head, string body)
        {
            if (head != null)
            {
                var h = UiKit.Label(content, head, 30, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Size(h, 1200, 42);
            }
            UiKit.Label(content, body, 25, Color.white, TextAnchor.UpperLeft);
        }

        public void ShowRound()
        {
            Clear();
            Paragraph("The goal",
                "Loyalists (blue) protect the King. They win when both Traitors, the Prince and the Nobleman, have been executed.\n" +
                "Traitors (red) win if the Prince sits on the throne for 3 rounds in a row.\n" +
                "Chaos roles (purple) play for themselves. The Jester wins only by being executed. The Beggar counts as a Loyalist until he is revealed, then he joins the Traitors.");
            Paragraph("Every round",
                "1.  The thronebearer (whoever sits on the throne) names a Trusted Advisor.\n" +
                "2.  Every other advisor plays one action card face down, all at the same time. The Trusted Advisor plays no card.\n" +
                "3.  The Trusted Advisor looks at every card played and passes two of them to the King. The rest are discarded.\n" +
                "4.  The thronebearer picks one of those two and it happens. Hands then refill to 3 cards.");
            Paragraph("Secret roles",
                "Your role is hidden until an action card reveals it. Revealed players get their role piece on the table. " +
                "A role's ability can only be used once it is revealed (the Elder's and the Martyr's can be used while still hidden). The King is always revealed from the start, but is otherwise just another Loyalist - he can be executed like anyone else.");
            Paragraph("Dice and the deck",
                "\"Roll the die\" cards roll one face per seat. The seat that comes up is affected; an empty seat does nothing. " +
                "When the deck runs out, the discard pile is shuffled back in.");
            Paragraph("The game keeps you honest",
                "You can only act when it is your turn. You cannot play a second card in a round (only the Elder plays two), " +
                "and you cannot answer a question that was asked of someone else. If you try, the game tells you why and nothing happens. " +
                "If someone leaves, a bot takes their seat.");
        }

        public void ShowRoles()
        {
            Clear();
            foreach (var role in new[] { Role.King, Role.Queen, Role.Knight, Role.Elder, Role.CourtWizard, Role.Medium, Role.Prince, Role.Nobleman, Role.Prophet, Role.Jester, Role.Beggar, Role.Reaper, Role.Lovers })
            {
                var row = UiKit.HStack(content, "Role " + role, 20, TextAnchor.MiddleLeft);
                UiKit.Size(row, 1200, 110);
                var disc = UiKit.Box(row.transform, "Disc", Color.Lerp(TableView.TeamColor(role), Color.black, .35f));
                UiKit.Size(disc, 96, 96);
                var iconRt = UiKit.Rect(disc, "Icon");
                UiKit.Stretch(iconRt, 8, 8, 8, 8);
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = SpriteFactory.Icon(role);
                img.preserveAspect = true;
                var team = RoleInfo.FactionOf(role);
                var text = UiKit.Label(row.transform, role + "  (" + team + ")\n" + RoleInfo.AbilityText(role), 24, Color.white, TextAnchor.MiddleLeft);
                UiKit.Size(text, 900, 100);
            }
        }

        public void ShowCards()
        {
            Clear();
            Paragraph(null, "The action deck has " + ActionCatalog.TotalCards(5) + " cards (the deck doubles in size at 8 or more players). Number in brackets = copies in the deck.");
            foreach (var e in ActionCatalog.Entries)
            {
                var cat = CardInfo.Category(e.Effect);
                var row = UiKit.HStack(content, "Card " + e.Effect, 16, TextAnchor.MiddleLeft);
                UiKit.Size(row, 1200, 76);
                var chip = UiKit.Box(row.transform, "Chip", CardView.CategoryColor(cat));
                UiKit.Size(chip, 270, 64);
                var t = UiKit.Label(chip, CardInfo.Title(e.Effect) + "  x" + e.Count, 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch((RectTransform)t.transform, 6, 2, 6, 2);
                var d = UiKit.Label(row.transform, e.Text, 22, Color.white, TextAnchor.MiddleLeft);
                UiKit.Size(d, 880, 70);
            }
        }
    }
}
