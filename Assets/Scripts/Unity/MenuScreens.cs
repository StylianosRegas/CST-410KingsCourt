using System;
using System.Collections.Generic;
using KingsCourt.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>Start screen: Host, Join, How to Play, Settings.</summary>
    public sealed class StartScreen
    {
        public readonly RectTransform Root;
        private readonly CodePad pad;
        private readonly GameController c;

        public StartScreen(Transform canvas, GameController controller)
        {
            c = controller;
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
            UiKit.MakeButton(stack.transform, "HOST GAME", UiKit.Button, () => c.HostGame(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "JOIN GAME", UiKit.ButtonAlt, () => pad.Show(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "SCENARIOS", UiKit.ButtonAlt, () => c.ShowScenarioPicker(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "HOW TO PLAY", UiKit.ButtonAlt, () => c.ShowHelp(), 40, new Vector2(520, 90));
            UiKit.MakeButton(stack.transform, "SETTINGS", UiKit.ButtonAlt, () => c.ShowSettings(), 40, new Vector2(520, 90));

            // Join code entry: a pad of letters you tap (works with a finger, no keyboard needed).
            pad = new CodePad(canvas, c);
        }

        /// <summary>Shows a message (for example "No game with that code.") on the join pad and opens it.</summary>
        public void SetJoinMessage(string text)
        {
            pad.SetMessage(text);
            pad.Show();
        }

        public void Show() { Root.gameObject.SetActive(true); Root.SetAsFirstSibling(); }
        public void Hide() { Root.gameObject.SetActive(false); pad.Hide(); }

        /// <summary>Called every frame so the join pad can also take a physical keyboard, not just taps.</summary>
        public void Tick() { pad.Tick(); }
    }

    /// <summary>Tap-in join code: four boxes and a grid of letters. Codes never contain I or O, so there is nothing to mix up.</summary>
    public sealed class CodePad
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        public readonly RectTransform Root;
        private readonly GameController c;
        private readonly Text[] slots = new Text[4];
        private readonly Text message;
        private string code = "";

        public CodePad(Transform canvas, GameController controller)
        {
            c = controller;
            Root = UiKit.Box(canvas, "CodePad", new Color(0, 0, 0, .93f));
            UiKit.Stretch(Root);
            var card = UiKit.Box(Root, "Card", UiKit.Panel);
            UiKit.Place(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1300, 900));

            var title = UiKit.Label(card, "ENTER THE JOIN CODE", 50, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(1000, 70));

            var row = UiKit.HStack(card, "Slots", 20);
            UiKit.Place((RectTransform)row.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -110), new Vector2(700, 150));
            for (int i = 0; i < 4; i++)
            {
                var box = UiKit.Box(row.transform, "Slot" + i, new Color(1, 1, 1, .95f));
                UiKit.Size(box, 150, 150);
                slots[i] = UiKit.Label(box, "", 110, UiKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch((RectTransform)slots[i].transform);
            }

            message = UiKit.Label(card, "The host's lobby screen shows the code.", 28, new Color(.85f, .85f, .9f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)message.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -275), new Vector2(1100, 44));

            var grid = UiKit.Rect(card, "Letters");
            UiKit.Place(grid, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -335), new Vector2(1190, 340));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(135, 100);
            g.spacing = new Vector2(14, 14);
            g.childAlignment = TextAnchor.UpperCenter;
            foreach (char ch in Letters)
            {
                char letter = ch;
                UiKit.MakeButton(grid, letter.ToString(), UiKit.ButtonAlt, () => Add(letter), 48, new Vector2(135, 100));
            }

            var buttons = UiKit.HStack(card, "Buttons", 30);
            UiKit.Place((RectTransform)buttons.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(1100, 100));
            UiKit.MakeButton(buttons.transform, "DELETE", UiKit.ButtonAlt, () => Delete(), 36, new Vector2(300, 90));
            UiKit.MakeButton(buttons.transform, "JOIN", UiKit.Button, () => Join(), 44, new Vector2(400, 90));
            UiKit.MakeButton(buttons.transform, "CANCEL", UiKit.ButtonAlt, () => Hide(), 36, new Vector2(300, 90));
            Root.gameObject.SetActive(false);
        }

        public void Show() { Root.gameObject.SetActive(true); Root.SetAsLastSibling(); Refresh(); }
        public void Hide() { Root.gameObject.SetActive(false); }
        public void SetMessage(string text) { message.text = text; }

        /// <summary>A PC has a real keyboard: let it type the code too, not just tap the on-screen grid.</summary>
        public void Tick()
        {
            if (!Root.gameObject.activeSelf) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            foreach (var key in kb.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                char letter = KeyToLetter(key.keyCode);
                if (letter != '\0' && Letters.IndexOf(letter) >= 0) Add(letter);
            }
            if (kb.backspaceKey.wasPressedThisFrame) Delete();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Join();
        }

        private static char KeyToLetter(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return (char)('A' + (key - Key.A));
            return '\0';
        }

        private void Add(char ch) { if (code.Length < 4) { code += ch; Refresh(); } }
        private void Delete() { if (code.Length > 0) { code = code.Substring(0, code.Length - 1); Refresh(); } }

        private void Join()
        {
            if (code.Length != 4) { message.text = "A code has 4 letters."; return; }
            message.text = "Connecting...";
            c.JoinGame(code);
        }

        private void Refresh()
        {
            for (int i = 0; i < 4; i++) slots[i].text = i < code.Length ? code[i].ToString() : "";
        }
    }

    /// <summary>Everything the lobby screen shows. The controller fills it in for a host, an offline game or a joined phone.</summary>
    public sealed class LobbyInfo
    {
        public string Code = "----";
        public string Status = "";
        public string Hint = "";
        /// <summary>Non-empty when hosting online: the full shareable join link, for a "copy link" button.</summary>
        public string JoinLink = "";
        public List<string> Rows = new List<string>();
        public int YouRow;
        public int Seats = 6;
        public bool CanEdit;        // host only: seats and bots
        public bool ShowStart;      // host only
        public bool CanStart;
        public string StartLabel = "START GAME";
    }

    /// <summary>The waiting lobby: join code, who is seated, bots toggle, Start.</summary>
    public sealed class LobbyScreen
    {
        public readonly RectTransform Root;
        private readonly GameController c;
        private readonly Text codeText, seatsText, hint, status;
        private readonly RectTransform seatList;
        private readonly Button startButton, minus, plus, copyLinkButton;
        private readonly Toggle botToggle;
        private readonly Image qrImage;
        private readonly RectTransform qrRect;
        private readonly InputField nameInput;
        private readonly RectTransform nameRow;
        private string joinLink = "";
        private float copiedUntil;

        public LobbyScreen(Transform canvas, GameController controller)
        {
            c = controller;
            Root = UiKit.Box(canvas, "LobbyScreen", new Color(.07f, .06f, .09f, 1f));
            UiKit.Stretch(Root);

            var title = UiKit.Label(Root, "WAITING LOBBY", 72, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)title.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(1200, 90));

            var codeLabel = UiKit.Label(Root, "JOIN CODE", 28, new Color(.75f, .75f, .8f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)codeLabel.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -170), new Vector2(400, 40));
            codeText = UiKit.Label(Root, "----", 120, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)codeText.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -210), new Vector2(500, 140));
            status = UiKit.Label(Root, "", 26, new Color(.6f, .9f, .6f), TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Place((RectTransform)status.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -360), new Vector2(600, 80));
            hint = UiKit.Label(Root, "", 22, new Color(.85f, .75f, .5f), TextAnchor.UpperCenter);
            UiKit.Place((RectTransform)hint.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -450), new Vector2(600, 170));

            copyLinkButton = UiKit.MakeButton(Root, "COPY JOIN LINK", UiKit.ButtonAlt, () => CopyLink(), 26, new Vector2(320, 60));
            UiKit.Place((RectTransform)copyLinkButton.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -635), new Vector2(320, 60));

            var qrBox = UiKit.Box(Root, "QrCode", Color.white);
            qrRect = qrBox;
            UiKit.Place(qrRect, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -770), new Vector2(220, 220));
            qrImage = qrBox.GetComponent<Image>();
            qrImage.preserveAspect = true;

            // A joined phone (not the host) gets this instead of the copy-link/QR block above, so someone
            // who joined via a QR/link scan with a stale saved name can fix it without leaving and
            // rejoining with a new code.
            nameRow = UiKit.Rect(Root, "NameRow");
            UiKit.Place(nameRow, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -650), new Vector2(600, 110));
            var nameLabel = UiKit.Label(nameRow, "YOUR NAME", 24, new Color(.75f, .75f, .8f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)nameLabel.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(600, 36));
            nameInput = UiKit.Input(nameRow, "Your name", 30, 24);
            UiKit.Place((RectTransform)nameInput.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -44), new Vector2(400, 64));
            nameInput.onEndEdit.AddListener(v => { if (c.Client != null) c.Client.Rename(v); });

            var panel = UiKit.Box(Root, "Seats", UiKit.Panel);
            UiKit.Place(panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(330, -170), new Vector2(900, 620));
            seatsText = UiKit.Label(panel, "", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place((RectTransform)seatsText.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -14), new Vector2(560, 56));
            minus = UiKit.MakeButton(panel, "-", UiKit.ButtonAlt, () => c.ChangeLobbySeats(-1), 40, new Vector2(64, 56));
            UiKit.Place((RectTransform)minus.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -14), new Vector2(64, 56));
            plus = UiKit.MakeButton(panel, "+", UiKit.ButtonAlt, () => c.ChangeLobbySeats(1), 40, new Vector2(64, 56));
            UiKit.Place((RectTransform)plus.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -14), new Vector2(64, 56));

            var v = UiKit.VStack(panel, "List", 6, TextAnchor.UpperLeft);
            UiKit.Place((RectTransform)v.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -84), new Vector2(840, 530));
            seatList = (RectTransform)v.transform;

            botToggle = UiKit.MakeToggle(Root, "Fill empty seats with bots", GameSettings.FillWithBots, on => { GameSettings.FillWithBots = on; c.LobbyChanged(); });
            UiKit.Place((RectTransform)botToggle.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-500, -705), new Vector2(400, 50));

            startButton = UiKit.MakeButton(Root, "START GAME", UiKit.Button, () => c.BeginGame(), 44, new Vector2(520, 100));
            UiKit.Place((RectTransform)startButton.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(330, 120), new Vector2(520, 100));
            var back = UiKit.MakeButton(Root, "BACK", UiKit.ButtonAlt, () => c.ShowStart(), 34, new Vector2(240, 70));
            UiKit.Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(240, 70));
            var help = UiKit.MakeButton(Root, "HELP", UiKit.ButtonAlt, () => c.ShowHelp(), 30, new Vector2(180, 70));
            UiKit.Place((RectTransform)help.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(180, 70));
        }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsFirstSibling();
            Refresh();
        }

        public void Hide() { Root.gameObject.SetActive(false); }
        public bool Visible { get { return Root.gameObject.activeSelf; } }

        /// <summary>Called every frame while visible, only to revert the button label after "COPIED!".</summary>
        public void Tick()
        {
            if (copiedUntil > 0f && Time.unscaledTime >= copiedUntil)
            {
                copiedUntil = 0f;
                UiKit.SetButtonText(copyLinkButton, "COPY JOIN LINK");
            }
        }

        private void CopyLink()
        {
            if (string.IsNullOrEmpty(joinLink)) return;
            Clipboard.Copy(joinLink);
            UiKit.SetButtonText(copyLinkButton, "COPIED!");
            copiedUntil = Time.unscaledTime + 1.5f;
        }

        public void Refresh()
        {
            var info = c.GetLobbyInfo();
            botToggle.SetIsOnWithoutNotify(GameSettings.FillWithBots);
            botToggle.gameObject.SetActive(info.CanEdit);
            minus.gameObject.SetActive(info.CanEdit);
            plus.gameObject.SetActive(info.CanEdit);
            startButton.gameObject.SetActive(info.ShowStart);
            bool hasLink = !string.IsNullOrEmpty(info.JoinLink);
            copyLinkButton.gameObject.SetActive(hasLink);
            qrRect.gameObject.SetActive(hasLink);
            if (hasLink && joinLink != info.JoinLink) qrImage.sprite = SpriteFactory.QrCode(info.JoinLink);
            joinLink = info.JoinLink;
            copiedUntil = 0f;
            UiKit.SetButtonText(copyLinkButton, "COPY JOIN LINK");

            bool isClient = c.Client != null;
            nameRow.gameObject.SetActive(isClient);
            if (isClient && !nameInput.isFocused) nameInput.text = GameSettings.PlayerName;

            codeText.text = info.Code;
            status.text = info.Status;
            hint.text = info.Hint;
            seatsText.text = "Seats: " + info.Seats + (info.CanEdit ? "  (5 to 10)" : "");
            for (int i = seatList.childCount - 1; i >= 0; i--) UiKit.Kill(seatList.GetChild(i).gameObject);
            for (int i = 0; i < info.Rows.Count; i++)
            {
                bool you = i == info.YouRow;
                var row = UiKit.Box(seatList, "Seat " + i, new Color(1, 1, 1, .06f));
                UiKit.Size(row, 840, 58);
                var t = UiKit.Label(row, (i + 1) + ".   " + info.Rows[i], 30, you ? UiKit.Gold : new Color(.75f, .82f, 1f), TextAnchor.MiddleLeft);
                UiKit.Stretch((RectTransform)t.transform, 20, 0, 10, 0);
            }
            startButton.interactable = info.CanStart;
            UiKit.SetButtonText(startButton, info.StartLabel);
        }
    }

    /// <summary>Settings: name, bots toggle, chat toggle (off until online chat exists), game speed, server address.</summary>
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
            UiKit.MakeToggle(stack.transform, "Chat (available once online chat is added)", GameSettings.ChatEnabled, on => { GameSettings.ChatEnabled = on; Changed(); });

            var speedRow = UiKit.HStack(stack.transform, "SpeedRow", 20, TextAnchor.MiddleLeft);
            UiKit.Size(speedRow, 940, 70);
            var sl = UiKit.Label(speedRow.transform, "Game speed", 30, Color.white, TextAnchor.MiddleLeft);
            UiKit.Size(sl, 250, 60);
            speedButton = UiKit.MakeButton(speedRow.transform, "", UiKit.ButtonAlt, () => { GameSettings.Speed = (GameSettings.Speed + 1) % 3; UpdateSpeedText(); Changed(); }, 30, new Vector2(300, 60));
            var note = UiKit.Label(speedRow.transform, "how long bots pause between steps", 22, new Color(.7f, .7f, .75f), TextAnchor.MiddleLeft);
            UiKit.Size(note, 380, 60);
            UpdateSpeedText();

            var serverRow = UiKit.HStack(stack.transform, "ServerRow", 20, TextAnchor.MiddleLeft);
            UiKit.Size(serverRow, 940, 70);
            var srvLabel = UiKit.Label(serverRow.transform, "Server", 30, Color.white, TextAnchor.MiddleLeft);
            UiKit.Size(srvLabel, 250, 60);
            var serverInput = UiKit.Input(serverRow.transform, "automatic (leave blank)", 26, 80);
            UiKit.Size(serverInput, 640, 60);
            serverInput.text = GameSettings.ServerUrl;
            serverInput.onEndEdit.AddListener(v => GameSettings.ServerUrl = (v ?? "").Trim());

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
