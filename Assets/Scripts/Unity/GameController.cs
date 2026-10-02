using System.Linq;
using KingsCourt.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Put this on one empty GameObject in a scene and press Play. It builds everything else:
    /// Start screen -> Waiting lobby -> the top-down table with your hand at the bottom.
    /// It also owns the network side: hosting a room, joining one by code, and falling back to an offline game.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public TableView Table { get; private set; }
        public Canvas Canvas { get { return canvas; } }
        public Camera Cam { get { return cam; } }
        public Pacer Pacer { get { return pacer; } }
        public GameScreen GameScreen { get { return game; } }
        public HelpPanel HelpPanel { get { return help; } }
        public NetHost Host { get { return host; } }
        public NetClient Client { get { return client; } }
        public int LobbySeats { get { return host != null ? host.Seats : offlineSeats; } }

        [SerializeField] private CardView cardPrefab;   // optional: Assets/Prefabs/CardView.prefab
        private bool initialised;
        private Canvas canvas;
        private CanvasScaler scaler;
        private Camera cam;
        private readonly Pacer pacer = new Pacer();
        private StartScreen start;
        private LobbyScreen lobby;
        private GameScreen game;
        private HelpPanel help;
        private SettingsPanel settings;
        private IPlayerSession session;
        private int seedCounter;
        private string offlineCode = "";
        private int offlineSeats = 6;
        private NetHost host;
        private NetClient client;

        private void Start() { Init(); }

        /// <summary>Builds the cameras, canvas and screens. Called automatically; the screenshot tool calls it directly.</summary>
        public void Init()
        {
            if (initialised) return;
            initialised = true;
            if (cardPrefab != null) CardView.Prefab = cardPrefab;
            seedCounter = System.Environment.TickCount & 0xFFFF;
            SetupCamera();
            SetupCanvas();
            Table = new GameObject("Table").AddComponent<TableView>();
            Table.gameObject.SetActive(false);

            start = new StartScreen(canvas.transform, this);
            lobby = new LobbyScreen(canvas.transform, this);
            game = new GameScreen(canvas.transform, this);
            help = new HelpPanel(canvas.transform);
            settings = new SettingsPanel(canvas.transform, () => LobbyChanged());
            ShowStart();

            // A shared link such as http://192.168.1.5:8000/?join=ABCD joins straight away.
            string link = WebSocketTransport.QueryParam("join");
            if (!string.IsNullOrEmpty(link) && link.Trim().Length == 4)
            {
                start.SetJoinMessage("Joining " + link.Trim().ToUpperInvariant() + "...");
                JoinGame(link.Trim().ToUpperInvariant());
            }
        }

        private void Update() { Tick(); }

        public void Tick()
        {
            // Wide screens (phones held sideways, with browser bars) are scaled by height so nothing is cut off;
            // narrower screens are scaled by width.
            if (scaler != null && Screen.height > 0)
                scaler.matchWidthOrHeight = (float)Screen.width / Screen.height >= 16f / 9f ? 1f : 0f;
            pacer.Tick();
            if (host != null) host.Tick(Time.unscaledTime);
            if (client != null) client.Tick();
            if (game != null) game.Tick();
            if (lobby != null && lobby.Visible) lobby.Tick();
            if (start != null) start.Tick();
        }

        private void OnDestroy() { DisposeNet(); }

        // ------------------------------------------------------------------ setup

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.8f;                       // leaves the bottom third of the screen for the hand
            cam.transform.position = new Vector3(0, -1.38f, -10);
            cam.backgroundColor = new Color(.09f, .08f, .1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            if (Application.isPlaying && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        // ------------------------------------------------------------------ navigation

        public void ShowStart() { ShowStart(null); }

        public void ShowStart(string message)
        {
            if (session != null) { session.LeaveTable(); session = null; }
            DisposeNet();
            game.End();
            Table.gameObject.SetActive(false);
            lobby.Hide();
            start.Show();
            if (!string.IsNullOrEmpty(message)) start.SetJoinMessage(message);
        }

        /// <summary>HOST GAME: open a room on the server. If the server cannot be reached the lobby switches to an offline game.</summary>
        public void HostGame()
        {
            DisposeNet();
            seedCounter++;
            offlineCode = GameSession.NewCode(seedCounter);
            host = new NetHost(new WebSocketTransport(), offlineSeats);
            host.Changed += LobbyChanged;
            host.Connect(ServerUrl());
            start.Hide();
            lobby.Show();
        }

        /// <summary>JOIN GAME: join somebody else's room by its 4-letter code.</summary>
        public void JoinGame(string code)
        {
            DisposeNet();
            client = new NetClient(new WebSocketTransport(), code);
            client.Changed += OnClientChanged;
            client.Started += OnClientStarted;
            client.Connect(ServerUrl());
        }

        private void OnClientChanged()
        {
            if (client == null) return;
            switch (client.Status)
            {
                case NetClient.State.Joining:
                case NetClient.State.InLobby:
                    if (session != null) { session = null; game.End(); Table.gameObject.SetActive(false); }
                    if (!lobby.Visible) { start.Hide(); lobby.Show(); }
                    else lobby.Refresh();
                    break;
                case NetClient.State.Failed:
                case NetClient.State.Denied:
                case NetClient.State.Closed:
                    if (session == null) ShowStart(client.Error);
                    break;
            }
        }

        private void OnClientStarted()
        {
            var s = client.Session;
            s.Ended += reason => { if (session == s) ShowStart(reason); };
            BeginWith(s);
        }

        public void ChangeLobbySeats(int delta)
        {
            if (client != null) return;
            int target = Mathf.Clamp(LobbySeats + delta, Game.MinPlayers, Game.MaxPlayers);
            if (host != null) host.SetSeats(target);       // clamps to the people already seated
            offlineSeats = host != null ? host.Seats : target;
            LobbyChanged();
        }

        public void LobbyChanged() { if (lobby != null && lobby.Visible) lobby.Refresh(); }

        public void BeginGame()
        {
            if (client != null) return;                                  // joined phones wait for the host
            seedCounter++;
            if (host != null && host.Status == NetHost.State.Open)
            {
                bool allSeatsHuman = host.Members.Count + 1 >= host.Seats;
                if (!GameSettings.FillWithBots && !allSeatsHuman) return;
                BeginWith(host.StartOnline(pacer, seedCounter));
                return;
            }
            if (!GameSettings.FillWithBots) return;                      // offline needs bots to have anyone to play
            int seats = LobbySeats;
            BeginWith(new GameSession(seats, 0, seedCounter, pacer, offlineCode));
        }

        public void BeginWith(IPlayerSession newSession)
        {
            session = newSession;
            start.Hide();
            lobby.Hide();
            Table.gameObject.SetActive(true);
            Table.Build(session.Snapshot());
            game.Begin(session);
            var local = session as GameSession;
            if (local != null) local.Start();
        }

        /// <summary>Screenshot mode: draw the overlay through the camera so it can be captured without a screen.</summary>
        public void UseCameraCanvas()
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;   // draw over the table sprites
        }

        public void LeaveGame() { ShowStart(); }

        /// <summary>Game over: go back to this same room's lobby (same code) instead of the main menu, so the
        /// party doesn't have to rejoin with a new code to play again. Only the host/offline device drives this;
        /// joined phones follow automatically when the host sends BackToLobbyMsg (see NetClient.OnHost).</summary>
        public void ReturnToLobby()
        {
            if (client != null) return;
            if (session != null) { var local = session as GameSession; if (local != null) local.LeaveTable(); }
            session = null;
            game.End();
            Table.gameObject.SetActive(false);
            if (host != null && host.Status == NetHost.State.Open) host.ReturnToLobby();
            start.Hide();
            lobby.Show();
        }
        public void ShowHelp() { help.Show(); }
        public void ShowSettings() { settings.Show(); }

        // ------------------------------------------------------------------ lobby contents

        private static string ServerUrl()
        {
            string u = GameSettings.ServerUrl;
            return string.IsNullOrWhiteSpace(u) ? WebSocketTransport.DefaultUrl() : u.Trim();
        }

        private void DisposeNet()
        {
            if (host != null) { host.Changed -= LobbyChanged; host.Close(); host = null; }
            if (client != null) { client.Changed -= OnClientChanged; client.Started -= OnClientStarted; client.Leave(); client = null; }
        }

        /// <summary>What the lobby should show right now (host online, host offline, or a joined phone).</summary>
        public LobbyInfo GetLobbyInfo()
        {
            var info = new LobbyInfo();
            bool bots = GameSettings.FillWithBots;

            if (client != null)
            {
                var lobbyMsg = client.Lobby;
                info.Code = client.Code;
                info.Seats = lobbyMsg.seats > 0 ? lobbyMsg.seats : 6;
                bool inLobby = client.Status == NetClient.State.InLobby;
                info.Status = !inLobby ? "Joining..."
                    : lobbyMsg.inProgress ? "A game is already in progress. You'll join the next one."
                    : "You are in. Waiting for the host to start.";
                info.Hint = inLobby && lobbyMsg.inProgress
                    ? "Keep this screen open. You'll automatically join when the current game ends."
                    : "Keep this screen open. The game starts when the host presses Start.";
                var names = lobbyMsg.members.Select(m => m.name + (m.isHost ? "  (host)" : "")).ToList();
                info.YouRow = System.Array.FindIndex(lobbyMsg.members, m => m.id == client.MyId);
                if (info.YouRow < 0) info.YouRow = 0;
                FillRows(info, names, info.Seats, lobbyMsg.bots);
                return info;
            }

            info.CanEdit = true;
            info.ShowStart = true;
            info.YouRow = 0;
            var rows = new System.Collections.Generic.List<string> { GameSettings.PlayerName + "  (you, host)" };

            if (host != null && host.Status == NetHost.State.Open)
            {
                info.Code = host.Code;
                info.Seats = host.Seats;
                info.Status = "ONLINE: friends can join with this code";
                // Prefer the server's own LAN address over the host's page URL: the host might have
                // typed "localhost" to get here, which nobody else on the Wi-Fi can actually reach.
                string origin = !string.IsNullOrEmpty(host.LanAddress) ? "http://" + host.LanAddress : WebSocketTransport.PageOrigin();
                info.Hint = string.IsNullOrEmpty(origin)
                    ? "On their phones: open the same address, press JOIN GAME and tap the code."
                    : "Phones on the same Wi-Fi: open " + origin + " and tap JOIN GAME.\nOr send this link: " + origin + "/?join=" + host.Code;
                if (!string.IsNullOrEmpty(origin)) info.JoinLink = origin + "/?join=" + host.Code;
                rows.AddRange(host.Members.Select(m => m.Name));
                FillRows(info, rows, host.Seats, bots);
                bool full = host.Members.Count + 1 >= host.Seats;
                info.CanStart = bots || full;
                info.StartLabel = info.CanStart ? "START GAME" : "NEED " + host.Seats + " PLAYERS";
                return info;
            }

            info.Code = offlineCode;
            info.Seats = LobbySeats;
            if (host == null || host.Status == NetHost.State.Connecting)
            {
                info.Status = "Connecting to the server...";
                info.Hint = "If it cannot connect you can still play offline with bots.";
            }
            else
            {
                info.Status = "OFFLINE: the server could not be reached";
                info.Hint = "Others cannot join this game. Start the server (see Server/README.md) or check Settings, or play offline with bots.";
            }
            FillRows(info, rows, info.Seats, bots);
            info.CanStart = bots;
            info.StartLabel = bots ? "START GAME" : "NEED BOTS OR PLAYERS";
            return info;
        }

        private static void FillRows(LobbyInfo info, System.Collections.Generic.List<string> people, int seats, bool bots)
        {
            info.Rows.AddRange(people);
            for (int i = people.Count; i < seats; i++) info.Rows.Add(bots ? "Bot " + (i + 1) : "Waiting for a player...");
        }
    }
}
