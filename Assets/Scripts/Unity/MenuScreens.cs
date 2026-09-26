using System;
using KingsCourt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>Start screen: Host, Join, How to Play, Settings.</summary>
    public sealed class StartScreen
    {
        public readonly RectTransform Root;
        private readonly Text joinMessage;
        private readonly InputField codeInput;
        private readonly RectTransform joinBox;

        public StartScreen(Transform canvas, GameController c)
        {
            Root = UiKit.Box(canvas, "StartScreen", new Color(.07f, .06f, .09f, 1f));
            UiKit.Stretch(Root);

            var crown = UiKit.Rect(Root, "Crown");
            UiKit.Place(crown, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(190, 190));
            var crownImg = crown.gameObject.AddComponent<Image>();
            crownImg.sprite = SpriteFactory.Icon(Role.King);
            crownImg.preserveAspect = true;

            var title = UiKit.Label(Root, "KING'S COURT", 110, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -270), new Vector2(1400, 130));
            var sub = UiKit.Label(Root, "A game of loyalty, lies and a very uncomfortable throne", 34, new Color(.85f, .82f, .75f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Place((RectTransform)sub.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -395), new Vector2(1400, 50));

            var stack = UiKit.VStack(Root, "Buttons", 22);
            UiKit.Place((RectTransform)stack.transform, new Vector2(.5f, .5f), new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(520, 520));
            UiKit.MakeButton(stack.transform, "HOST GAME", UiKit.Button, () => c.ShowLobby(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "JOIN GAME", UiKit.ButtonAlt, () => joinBox.gameObject.SetActive(!joinBox.gameObject.activeSelf), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "HOW TO PLAY", UiKit.ButtonAlt, () => c.ShowHelp(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "SETTINGS", UiKit.ButtonAlt, () => c.ShowSettings(), 40, new Vector2(520, 90));

            // Join code entry (opens under the buttons).
            joinBox = UiKit.Box(Root, "JoinBox", UiKit.Panel);
            UiKit.Place(joinBox, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(980, 170));
            codeInput = UiKit.Input(joinBox, "ENTER 4-LETTER CODE", 46, 4);
            UiKit.Place((RectTransform)codeInput.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(30, 20), new Vector2(430, 80));
            var join = UiKit.MakeButton(joinBox, "JOIN", UiKit.Button, () => OnJoin(), 38, new Vector2(200, 80));
            UiKit.Place((RectTransform)join.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(480, 20), new Vector2(200, 80));
            joinMessage = UiKit.Label(joinBox, "Codes are shown on the host's lobby screen.", 24, new Color(.8f, .8f, .85f), TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)joinMessage.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 8), new Vector2(920, 56));
            joinBox.gameObject.SetActive(false);
        }

        private void OnJoin()
        {
            string code = (codeInput.text ?? "").Trim().ToUpperInvariant();
            if (code.Length != 4) { joinMessage.text = "A code has 4 letters."; return; }
            // Online play is the next milestone (Unity Relay). Nothing is sent anywhere yet.
            joinMessage.text = "Online joining is not connected yet. For now, use HOST GAME to play with bots.";
        }

        public void Show() { Root.gameObject.SetActive(true); Root.SetAsFirstSibling(); }
        public void Hide() { Root.gameObject.SetActive(false); }
    }

    /// <summary>The waiting lobby: join code, who is seated, bots toggle, Start.</summary>
    public sealed class LobbyScreen
    {
        public readonly RectTransform Root;
        private readonly GameController c;
        private readonly Text codeText, seatsText, hint;
        private readonly RectTransform seatList;
        private readonly Button startButton;
        private Toggle botToggle;
        private string code = "";

        public LobbyScreen(Transform canvas, GameController c)
        {
            this.c = c;
            Root = UiKit.Box(canvas, "LobbyScreen", new Color(.07f, .06f, .09f, 1f));
            UiKit.Stretch(Root);

            var title = UiKit.Label(Root, "WAITING LOBBY", 72, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(1200, 90));

            var codeLabel = UiKit.Label(Root, "JOIN CODE", 28, new Color(.75f, .75f, .8f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)codeLabel.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -170), new Vector2(400, 40));
            codeText = UiKit.Label(Root, "----", 120, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)codeText.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -210), new Vector2(500, 140));
            hint = UiKit.Label(Root, "", 24, new Color(.85f, .75f, .5f), TextAnchor.UpperCenter);
            UiKit.Place((RectTransform)hint.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -370), new Vector2(560, 170));

            // Seats.
            var panel = UiKit.Box(Root, "Seats", UiKit.Panel);
            UiKit.Place(panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(330, -170), new Vector2(900, 620));
            seatsText = UiKit.Label(panel, "", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place((RectTransform)seatsText.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -14), new Vector2(560, 56));
            var minus = UiKit.MakeButton(panel, "-", UiKit.ButtonAlt, () => c.ChangeLobbySeats(-1), 40, new Vector2(64, 56));
            UiKit.Place((RectTransform)minus.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -14), new Vector2(64, 56));
            var plus = UiKit.MakeButton(panel, "+", UiKit.ButtonAlt, () => c.ChangeLobbySeats(1), 40, new Vector2(64, 56));
            UiKit.Place((RectTransform)plus.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -14), new Vector2(64, 56));

            var v = UiKit.VStack(panel, "List", 6, TextAnchor.UpperLeft);
            UiKit.Place((RectTransform)v.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -84), new Vector2(840, 530));
            seatList = (RectTransform)v.transform;

            // Bottom controls.
            botToggle = UiKit.MakeToggle(Root, "Fill empty seats with bots", GameSettings.FillWithBots, on => { GameSettings.FillWithBots = on; Refresh(); });
            UiKit.Place((RectTransform)botToggle.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-430, 210), new Vector2(760, 64));

            startButton = UiKit.MakeButton(Root, "START GAME", UiKit.Button, () => c.BeginGame(), 44, new Vector2(520, 100));
            UiKit.Place((RectTransform)startButton.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(330, 120), new Vector2(520, 100));
            var back = UiKit.MakeButton(Root, "BACK", UiKit.ButtonAlt, () => c.ShowStart(), 34, new Vector2(240, 70));
            UiKit.Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(240, 70));
            var help = UiKit.MakeButton(Root, "HELP", UiKit.ButtonAlt, () => c.ShowHelp(), 30, new Vector2(180, 70));
            UiKit.Place((RectTransform)help.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(180, 70));
        }

        public void Show(string joinCode)
        {
            code = joinCode;
            Root.gameObject.SetActive(true);
            Root.SetAsFirstSibling();
            botToggle.SetIsOnWithoutNotify(GameSettings.FillWithBots);
            Refresh();
        }

        public void Hide() { Root.gameObject.SetActive(false); }

        public void Refresh()
        {
            int seats = c.LobbySeats;
            bool bots = GameSettings.FillWithBots;
            codeText.text = code;
            seatsText.text = "Seats: " + seats + "  (5 to 8)";
            hint.text = "Friends will type this code on the start screen.\nOnline joining from phones is coming next; for now you can play with bots.";
            for (int i = seatList.childCount - 1; i >= 0; i--) UiKit.Kill(seatList.GetChild(i).gameObject);
            for (int i = 0; i < seats; i++)
            {
                string label = i == 0 ? GameSettings.PlayerName + "  (you, host)" : bots ? "Bot " + (i + 1) : "Waiting for a player...";
                Color col = i == 0 ? UiKit.Gold : bots ? new Color(.7f, .8f, 1f) : new Color(.6f, .6f, .65f);
                var row = UiKit.Box(seatList, "Seat " + i, new Color(1, 1, 1, .06f));
                UiKit.Size(row, 840, 58);
                var t = UiKit.Label(row, (i + 1) + ".   " + label, 30, col, TextAnchor.MiddleLeft);
                UiKit.Stretch((RectTransform)t.transform, 20, 0, 10, 0);
            }
            bool canStart = bots;   // with no bots there are no other players connected yet
            startButton.interactable = canStart;
            UiKit.SetButtonText(startButton, canStart ? "START GAME" : "NEED " + seats + " PLAYERS");
        }
    }

    /// <summary>Settings: bots toggle, chat toggle (off until online chat exists), game speed, name.</summary>
    public sealed class SettingsPanel
    {
        public readonly RectTransform Root;
        private readonly Button speedButton;
        private readonly InputField nameInput;
        private readonly Action onChanged;

        public SettingsPanel(Transform canvas, Action onChanged)
        {
            this.onChanged = onChanged;
            Root = UiKit.Box(canvas, "SettingsPanel", new Color(0, 0, 0, .93f));
            UiKit.Stretch(Root);
            var card = UiKit.Box(Root, "Card", UiKit.Panel);
            UiKit.Place(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100, 760));

            var title = UiKit.Label(card, "SETTINGS", 52, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(800, 70));

            var stack = UiKit.VStack(card, "Stack", 22, TextAnchor.UpperLeft);
            UiKit.Place((RectTransform)stack.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -120), new Vector2(940, 520));

            var nameRow = UiKit.HStack(stack.transform, "NameRow", 20, TextAnchor.MiddleLeft);
            UiKit.Size(nameRow, 940, 70);
            var nameLabel = UiKit.Label(nameRow.transform, "Your name", 30, Color.white, TextAnchor.MiddleLeft);
            UiKit.Size(nameLabel, 250, 60);
            nameInput = UiKit.Input(nameRow.transform, "Your name", 32, 12);
            UiKit.Size(nameInput, 420, 60);
            nameInput.text = GameSettings.PlayerName;
            nameInput.onEndEdit.AddListener(s => { if (!string.IsNullOrWhiteSpace(s)) GameSettings.PlayerName = s.Trim(); });

            UiKit.MakeToggle(stack.transform, "Fill empty seats with bots", GameSettings.FillWithBots, on => { GameSettings.FillWithBots = on; Changed(); });
            var chat = UiKit.MakeToggle(stack.transform, "Chat (available once online play is added)", GameSettings.ChatEnabled, on => { GameSettings.ChatEnabled = on; Changed(); });

            var speedRow = UiKit.HStack(stack.transform, "SpeedRow", 20, TextAnchor.MiddleLeft);
            UiKit.Size(speedRow, 940, 70);
            var sl = UiKit.Label(speedRow.transform, "Game speed", 30, Color.white, TextAnchor.MiddleLeft);
            UiKit.Size(sl, 250, 60);
            speedButton = UiKit.MakeButton(speedRow.transform, "", UiKit.ButtonAlt, () => { GameSettings.Speed = (GameSettings.Speed + 1) % 3; UpdateSpeedText(); Changed(); }, 30, new Vector2(300, 60));
            var note = UiKit.Label(speedRow.transform, "how long bots pause between steps", 22, new Color(.7f, .7f, .75f), TextAnchor.MiddleLeft);
            UiKit.Size(note, 380, 60);
            UpdateSpeedText();

            var close = UiKit.MakeButton(card, "DONE", UiKit.Button, () => Hide(), 38, new Vector2(300, 80));
            UiKit.Place((RectTransform)close.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(300, 80));
            Root.gameObject.SetActive(false);
        }

        private void UpdateSpeedText()
        {
            string[] names = { "Relaxed", "Normal", "Fast" };
            UiKit.SetButtonText(speedButton, names[GameSettings.Speed]);
        }

        private void Changed() { if (onChanged != null) onChanged(); }
        public void Show() { Root.gameObject.SetActive(true); Root.SetAsLastSibling(); nameInput.text = GameSettings.PlayerName; }
        public void Hide() { Root.gameObject.SetActive(false); Changed(); }
    }
}
