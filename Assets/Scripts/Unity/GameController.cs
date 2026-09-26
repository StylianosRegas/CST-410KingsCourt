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
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public TableView Table { get; private set; }
        public Canvas Canvas { get { return canvas; } }
        public Camera Cam { get { return cam; } }
        public Pacer Pacer { get { return pacer; } }
        public GameScreen GameScreen { get { return game; } }
        public HelpPanel HelpPanel { get { return help; } }
        public int LobbySeats { get; private set; }

        private Canvas canvas;
        private CanvasScaler scaler;
        private Camera cam;
        private readonly Pacer pacer = new Pacer();
        private StartScreen start;
        private LobbyScreen lobby;
        private GameScreen game;
        private HelpPanel help;
        private SettingsPanel settings;
        private GameSession session;
        private int seedCounter;
        private string lobbyCode = "";

        [SerializeField] private CardView cardPrefab;   // optional: Assets/Prefabs/CardView.prefab
        private bool initialised;

        private void Start() { Init(); }

        /// <summary>Builds the cameras, canvas and screens. Called automatically; the screenshot tool calls it directly.</summary>
        public void Init()
        {
            if (initialised) return;
            initialised = true;
            if (cardPrefab != null) CardView.Prefab = cardPrefab;
            LobbySeats = 6;
            seedCounter = System.Environment.TickCount & 0xFFFF;
            SetupCamera();
            SetupCanvas();
            Table = new GameObject("Table").AddComponent<TableView>();
            Table.gameObject.SetActive(false);

            start = new StartScreen(canvas.transform, this);
            lobby = new LobbyScreen(canvas.transform, this);
            game = new GameScreen(canvas.transform, this);
            help = new HelpPanel(canvas.transform);
            settings = new SettingsPanel(canvas.transform, () => lobby.Refresh());
            ShowStart();
        }

        private void Update() { Tick(); }

        public void Tick()
        {
            // Wide screens (phones held sideways, with browser bars) are scaled by height so nothing is cut off;
            // narrower screens are scaled by width.
            if (scaler != null && Screen.height > 0)
                scaler.matchWidthOrHeight = (float)Screen.width / Screen.height >= 16f / 9f ? 1f : 0f;
            pacer.Tick();
            if (game != null) game.Tick();
        }

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

        public void ShowStart()
        {
            if (session != null) { session.LeaveTable(); session = null; }
            game.End();
            Table.gameObject.SetActive(false);
            lobby.Hide();
            start.Show();
        }

        public void ShowLobby()
        {
            seedCounter++;
            lobbyCode = GameSession.NewCode(seedCounter);
            start.Hide();
            lobby.Show(lobbyCode);
        }

        public void ChangeLobbySeats(int delta)
        {
            LobbySeats = Mathf.Clamp(LobbySeats + delta, Game.MinPlayers, Game.MaxPlayers);
            lobby.Refresh();
        }

        public void BeginGame()
        {
            if (!GameSettings.FillWithBots) return;   // no other players can connect yet
            seedCounter++;
            BeginWith(new GameSession(LobbySeats, 0, seedCounter, pacer, lobbyCode));
        }

        public void BeginWith(GameSession newSession)
        {
            session = newSession;
            start.Hide();
            lobby.Hide();
            Table.gameObject.SetActive(true);
            Table.Build(session.Snapshot());
            game.Begin(session);
            session.Start();
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
        public void ShowHelp() { help.Show(); }
        public void ShowSettings() { settings.Show(); }
    }
}
