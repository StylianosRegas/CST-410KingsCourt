using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// The in-game overlay on top of the table: your role, the event log, your hand (Uno-style, at the bottom),
    /// the decision panel that appears when it is your turn, and the game-over panel.
    /// </summary>
    public sealed class GameScreen
    {
        public readonly RectTransform Root;

        private readonly GameController c;
        private GameSession session;
        private GameSnapshot snap;
        private bool dirty;

        private Text roleText, logText, statusText;
        private RectTransform statusRt;
        private Image statusBg;

        private readonly RectTransform handRoot;
        private readonly List<CardView> hand = new List<CardView>();
        private int selectedHand = -1;

        private readonly RectTransform panel, panelBody;
        private readonly Text promptText, subText;
        private readonly Button confirm;
        private readonly List<CardView> panelCards = new List<CardView>();
        private int selectedPanel = -1;
        private int shownDecisionId = -1;
        private Decision shownDecision;

        private readonly RectTransform toastRt;
        private readonly Text toastText;
        private float toastUntil;

        private readonly RectTransform overlay;
        private readonly Text overTitle, overBody;

        public GameScreen(Transform canvas, GameController controller)
        {
            c = controller;
            Root = UiKit.Rect(canvas, "GameScreen");
            UiKit.Stretch(Root);

            // Role panel (top-left).
            var rolePanel = UiKit.Box(Root, "RolePanel", new Color(.1f, .09f, .13f, .82f));
            UiKit.Place(rolePanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(480, 150));
            roleText = UiKit.Label(rolePanel, "", 24, Color.white, TextAnchor.UpperLeft);
            UiKit.Stretch((RectTransform)roleText.transform, 16, 10, 16, 10);

            // Top-right buttons.
            var bar = UiKit.HStack(Root, "TopButtons", 12, TextAnchor.MiddleRight);
            UiKit.Place((RectTransform)bar.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(520, 60));
            UiKit.MakeButton(bar.transform, "HELP", UiKit.ButtonAlt, () => c.ShowHelp(), 26, new Vector2(150, 56));
            UiKit.MakeButton(bar.transform, "SETTINGS", UiKit.ButtonAlt, () => c.ShowSettings(), 26, new Vector2(190, 56));
            UiKit.MakeButton(bar.transform, "LEAVE", UiKit.Button, () => c.LeaveGame(), 26, new Vector2(150, 56));

            // Status pill (top centre, under the header text).
            statusRt = UiKit.Box(Root, "Status", new Color(.1f, .09f, .13f, .85f));
            UiKit.Place(statusRt, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-20, -8), new Vector2(800, 48));
            statusBg = statusRt.GetComponent<Image>();
            statusText = UiKit.Label(statusRt, "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch((RectTransform)statusText.transform, 8, 2, 8, 2);

            // Event log (bottom-left).
            var logPanel = UiKit.Box(Root, "Log", new Color(.05f, .05f, .08f, .6f));
            UiKit.Place(logPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(400, 250));
            logText = UiKit.Label(logPanel, "", 19, new Color(.88f, .88f, .9f), TextAnchor.LowerLeft);
            UiKit.Stretch((RectTransform)logText.transform, 10, 8, 10, 8);

            // Hand (bottom centre).
            handRoot = UiKit.Rect(Root, "Hand");
            UiKit.Place(handRoot, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(1000, CardView.Height + 90));

            // Decision panel (centre of the table).
            panel = UiKit.Box(Root, "DecisionPanel", new Color(.09f, .08f, .12f, .96f));
            UiKit.Place(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 141), new Vector2(1000, 340));
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.Gold;
            outline.effectDistance = new Vector2(3, -3);
            promptText = UiKit.Label(panel, "", 34, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)promptText.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(940, 50));
            subText = UiKit.Label(panel, "", 22, new Color(.85f, .85f, .9f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)subText.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(940, 36));
            panelBody = UiKit.Rect(panel, "Body");
            UiKit.Stretch(panelBody, 20, 80, 20, 100);
            confirm = UiKit.MakeButton(panel, "CONFIRM", UiKit.Button, () => OnConfirm(), 30, new Vector2(400, 64));
            UiKit.Place((RectTransform)confirm.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(400, 64));

            // Toast (refused moves).
            toastRt = UiKit.Box(Root, "Toast", new Color(.7f, .12f, .14f, .96f));
            UiKit.Place(toastRt, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, CardView.Height + 110), new Vector2(760, 56));
            toastText = UiKit.Label(toastRt, "", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch((RectTransform)toastText.transform, 10, 4, 10, 4);
            toastRt.gameObject.SetActive(false);

            // Game over.
            overlay = UiKit.Box(Root, "GameOver", new Color(.06f, .05f, .09f, .97f));
            UiKit.Place(overlay, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 141), new Vector2(980, 380));
            var oo = overlay.gameObject.AddComponent<Outline>();
            oo.effectColor = UiKit.Gold;
            oo.effectDistance = new Vector2(3, -3);
            overTitle = UiKit.Label(overlay, "", 56, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)overTitle.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(920, 80));
            overBody = UiKit.Label(overlay, "", 26, Color.white, TextAnchor.UpperCenter);
            UiKit.Place((RectTransform)overBody.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -104), new Vector2(920, 170));
            var menu = UiKit.MakeButton(overlay, "BACK TO MENU", UiKit.Button, () => c.LeaveGame(), 34, new Vector2(400, 76));
            UiKit.Place((RectTransform)menu.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(400, 76));
            overlay.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            Root.gameObject.SetActive(false);
        }

        public void Begin(GameSession s)
        {
            session = s;
            s.Updated += () => dirty = true;
            selectedHand = selectedPanel = -1;
            shownDecisionId = -1;
            Root.gameObject.SetActive(true);
            dirty = true;
        }

        public void End() { Root.gameObject.SetActive(false); session = null; }

        public void Tick()
        {
            if (session == null) return;
            if (dirty) { dirty = false; Rebuild(); }
            if (toastRt.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastRt.gameObject.SetActive(false);
        }

        /// <summary>For screenshots: mark a hand card / panel card as selected.</summary>
        public void DebugSelect(int handIndex, int panelIndex)
        {
            selectedHand = handIndex;
            selectedPanel = panelIndex;
            Rebuild();
        }

        public void DebugToast(string message) { Toast(message); }

        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            toastText.text = message;
            toastUntil = Time.unscaledTime + 2.6f;
            toastRt.gameObject.SetActive(true);
            toastRt.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ rebuild from the snapshot

        private void Rebuild()
        {
            snap = session.Snapshot();
            var d = snap.YourDecision;
            c.Table.SetCandidates(d != null && d.Type == OptionType.Player ? d.PlayerIds : null);
            c.Table.Refresh(snap);

            UpdateRole();
            logText.text = string.Join("\n", snap.Log);
            UpdateStatus(d);
            UpdateHand(d);
            UpdatePanel(d);
            UpdateOverlay();
        }

        private void UpdateRole()
        {
            var you = snap.Find(snap.ViewerId);
            if (you == null || !snap.YourRole.HasValue) { roleText.text = ""; return; }
            var role = snap.YourRole.Value;
            var team = RoleInfo.FactionOf(role);
            string state = you.Alive ? (you.Revealed ? "revealed to everyone" : "secret") : "executed";
            roleText.text = "<b>YOU ARE THE " + role.ToString().ToUpperInvariant() + "</b>  (" + team + ", " + state + ")\n" + RoleInfo.AbilityText(role);
            roleText.supportRichText = true;
            roleText.color = Color.Lerp(Color.white, TableView.TeamColor(role), .35f);
        }

        private void UpdateStatus(Decision d)
        {
            var tb = snap.Find(snap.ThronebearerId);
            string info = "Round " + snap.Round + "  |  " + snap.Players.Count(p => p.Alive) + " alive  |  Thronebearer: " + (tb != null ? tb.Name : "-");
            if (snap.Result != null) { statusText.text = "GAME OVER  |  " + info; statusBg.color = new Color(.1f, .09f, .13f, .85f); return; }
            if (d != null)
            {
                statusText.text = info + "  |  YOUR TURN";
                statusBg.color = new Color(.65f, .48f, .05f, .95f);
            }
            else
            {
                var names = snap.WaitingOn.Select(id => snap.Find(id)).Where(p => p != null).Select(p => p.Name).ToArray();
                statusText.text = info + "  |  " + (names.Length == 0 ? "the court deliberates" : "waiting for " + string.Join(", ", names));
                statusBg.color = new Color(.1f, .09f, .13f, .85f);
            }
        }

        // ------------------------------------------------------------------ hand

        private bool IsHandDecision(Decision d)
        {
            return d != null && d.Type == OptionType.Card && (d.Kind == ChoiceKind.PlayCard || d.Kind == ChoiceKind.DiscardCard);
        }

        private void UpdateHand(Decision d)
        {
            if (d != null && d.Id != shownDecisionId && IsHandDecision(d)) selectedHand = -1;
            bool handMode = IsHandDecision(d);
            int n = snap.YourHand.Count;
            while (hand.Count < n)
            {
                var cv = CardView.Create(handRoot, "HandCard");
                cv.Clicked += OnHandClick;
                var rt = (RectTransform)cv.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
                rt.pivot = new Vector2(.5f, 0f);
                hand.Add(cv);
            }
            while (hand.Count > n) { UiKit.Kill(hand[hand.Count - 1].gameObject); hand.RemoveAt(hand.Count - 1); }
            if (selectedHand >= n) selectedHand = -1;

            float spacing = n <= 1 ? 0 : Mathf.Min(CardView.Width + 16f, (900f - CardView.Width) / (n - 1));
            for (int i = 0; i < n; i++)
            {
                var cv = hand[i];
                cv.Index = i;
                cv.Set(snap.YourHand[i]);
                cv.SetState(handMode || d == null, handMode && i == selectedHand);
                ((RectTransform)cv.transform).anchoredPosition = new Vector2((i - (n - 1) / 2f) * spacing, 6);
            }
        }

        private void OnHandClick(CardView cv)
        {
            var d = snap.YourDecision;
            if (d == null)
            {
                // Not your turn: ask the host, which explains exactly why it is refused.
                var r = session.Submit(-1, cv.Index);
                if (!r.Ok) Toast(r.Reason);
                return;
            }
            if (!IsHandDecision(d)) { Toast("Finish the current choice first."); return; }
            selectedHand = selectedHand == cv.Index ? -1 : cv.Index;
            dirty = true;
        }

        // ------------------------------------------------------------------ decision panel

        private void UpdatePanel(Decision d)
        {
            if (d == null || snap.Result != null)
            {
                panel.gameObject.SetActive(false);
                shownDecisionId = -1;
                shownDecision = null;
                return;
            }
            panel.gameObject.SetActive(true);
            if (d.Id != shownDecisionId) BuildPanel(d);
            shownDecision = d;

            // Selection state for the current mode.
            if (IsHandDecision(d))
                confirm.interactable = selectedHand >= 0;
            else if (d.Type == OptionType.Card)
            {
                for (int i = 0; i < panelCards.Count; i++) panelCards[i].SetState(true, i == selectedPanel);
                confirm.interactable = selectedPanel >= 0;
            }
        }

        private void BuildPanel(Decision d)
        {
            shownDecisionId = d.Id;
            selectedPanel = -1;
            for (int i = panelBody.childCount - 1; i >= 0; i--) UiKit.Kill(panelBody.GetChild(i).gameObject);
            panelCards.Clear();
            promptText.text = d.Prompt.ToUpperInvariant();
            confirm.gameObject.SetActive(false);

            if (IsHandDecision(d))
            {
                Resize(760, 230);
                subText.text = d.Kind == ChoiceKind.DiscardCard ? "Click a card in your hand to select it, then confirm." : "Click a card in your hand to select it, then press play.";
                confirm.gameObject.SetActive(true);
                UiKit.SetButtonText(confirm, d.Kind == ChoiceKind.DiscardCard ? "DISCARD THIS CARD" : "PLAY THIS CARD");
                confirm.interactable = false;
            }
            else if (d.Type == OptionType.Card)
            {
                Resize(Mathf.Clamp(120 + d.Cards.Count * 160, 720, 1300), 400);
                bool keep = d.Kind == ChoiceKind.KeepCard;
                subText.text = keep ? "You are the Trusted Advisor. Pick a card to pass to the King (two go through, one at a time)."
                                    : "Pick the card that will happen.";
                confirm.gameObject.SetActive(true);
                UiKit.SetButtonText(confirm, keep ? "PASS TO THE KING" : "ENACT THIS CARD");
                confirm.interactable = false;
                const float scale = 0.85f;
                float w = CardView.Width * scale + 16;
                for (int i = 0; i < d.Cards.Count; i++)
                {
                    var cv = CardView.Create(panelBody, "PanelCard");
                    cv.Index = i;
                    cv.Set(d.Cards[i]);
                    cv.Clicked += OnPanelCardClick;
                    var rt = (RectTransform)cv.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                    rt.pivot = new Vector2(.5f, .5f);
                    rt.localScale = new Vector3(scale, scale, 1);
                    rt.anchoredPosition = new Vector2((i - (d.Cards.Count - 1) / 2f) * w, 0);
                    panelCards.Add(cv);
                    string note = i < d.Notes.Count ? d.Notes[i] : "";
                    if (!string.IsNullOrEmpty(note))
                    {
                        var t = UiKit.Label(panelBody, note, 16, new Color(1f, .9f, .5f), TextAnchor.UpperCenter);
                        UiKit.Place((RectTransform)t.transform, new Vector2(.5f, .5f), new Vector2(.5f, 1f), new Vector2((i - (d.Cards.Count - 1) / 2f) * w, -CardView.Height * scale / 2f - 4), new Vector2(w, 40));
                    }
                }
            }
            else if (d.Type == OptionType.Player)
            {
                Resize(1000, d.PlayerIds.Count > 4 ? 380 : 300);
                subText.text = d.Kind == ChoiceKind.TrustedAdvisor
                    ? "Every other player plays a card for them. They pass two to you."
                    : "Choose a player. They are highlighted on the table.";
                var grid = UiKit.Rect(panelBody, "Grid");
                UiKit.Stretch(grid);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(280, 76);
                g.spacing = new Vector2(16, 14);
                g.childAlignment = TextAnchor.MiddleCenter;
                foreach (var id in d.PlayerIds)
                {
                    var p = snap.Find(id);
                    int pid = id;
                    string label = p.Name + (p.IsBot && !p.Name.StartsWith("Bot") ? " [bot]" : "") + (p.Role.HasValue && p.Revealed ? "\n" + p.Role.Value : "");
                    var b = UiKit.MakeButton(grid, label, p.Revealed && p.Role.HasValue ? TableView.TeamColor(p.Role.Value) * 0.7f : UiKit.ButtonAlt, () => Send(d, IndexOfPlayer(d, pid)), 24, new Vector2(280, 76));
                }
            }
            else // yes / no
            {
                Resize(820, 300);
                string ability = snap.YourRole.HasValue ? RoleInfo.AbilityText(snap.YourRole.Value) : "";
                subText.text = ability;
                var row = UiKit.HStack(panelBody, "YesNo", 30);
                UiKit.Stretch((RectTransform)row.transform);
                UiKit.MakeButton(row.transform, "YES, USE IT", UiKit.Button, () => Send(d, 0), 32, new Vector2(340, 90));
                UiKit.MakeButton(row.transform, "NOT NOW", UiKit.ButtonAlt, () => Send(d, 1), 32, new Vector2(340, 90));
            }
        }

        private static int IndexOfPlayer(Decision d, int playerId) { return d.PlayerIds.IndexOf(playerId); }

        private void Resize(float w, float h) { panel.sizeDelta = new Vector2(w, h); }

        private void OnPanelCardClick(CardView cv)
        {
            selectedPanel = selectedPanel == cv.Index ? -1 : cv.Index;
            dirty = true;
        }

        private void OnConfirm()
        {
            var d = snap != null ? snap.YourDecision : null;
            if (d == null) { Toast("It isn't your turn."); return; }
            int index = IsHandDecision(d) ? selectedHand : selectedPanel;
            if (index < 0) { Toast("Select a card first."); return; }
            Send(d, index);
        }

        private void Send(Decision d, int index)
        {
            var r = session.Submit(d.Id, index);
            if (!r.Ok) Toast(r.Reason);
            selectedHand = selectedPanel = -1;
            dirty = true;
        }

        // ------------------------------------------------------------------ game over

        private void UpdateOverlay()
        {
            if (snap.Result == null) { overlay.gameObject.SetActive(false); return; }
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            string who = snap.Result.Winner == Winner.Draw ? "NOBODY WINS" : snap.Result.Winner.ToString().ToUpperInvariant() + " WIN";
            if (snap.Result.Winner == Winner.Jester) who = "THE JESTER WINS";
            overTitle.text = who;
            var winners = snap.WinnerIds.Select(id => snap.Find(id)).Where(p => p != null).Select(p => p.Name + " (" + p.Role + ")").ToArray();
            var you = snap.Find(snap.ViewerId);
            string result = you != null && you.IsWinner ? "You won!" : "You lost.";
            overBody.text = snap.Result.Reason + "\n\nWinners: " + (winners.Length == 0 ? "none" : string.Join(", ", winners)) + "\n" + result;
        }
    }
}
