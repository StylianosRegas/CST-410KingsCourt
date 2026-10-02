using System;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Runs on every phone that is not the host. Joins a room by code, shows the lobby, and once the host
    /// starts the game, becomes a RemoteSession that just displays what the host sends and forwards taps.
    /// </summary>
    public sealed class NetClient
    {
        public enum State { Connecting, Joining, InLobby, InGame, Denied, Failed, Closed }

        public State Status { get; private set; }
        public string Error { get; private set; }
        public string Code { get; private set; }
        public LobbyMsg Lobby { get; private set; }
        /// <summary>The id the relay gave this phone. The host's lobby list uses the same ids, so we can find our own row.</summary>
        public int MyId { get; private set; } = -1;
        public RemoteSession Session { get; private set; }
        public event Action Changed;
        /// <summary>The game has started and the first view of it has arrived.</summary>
        public event Action Started;

        private readonly ITransport t;
        private int playerId = -1;

        public NetClient(ITransport transport, string code)
        {
            t = transport;
            Code = (code ?? "").Trim().ToUpperInvariant();
            Status = State.Connecting;
            Error = "";
            Lobby = new LobbyMsg();
            t.Opened += OnOpened;
            t.Received += OnRelay;
            t.Closed += OnClosed;
        }

        public void Connect(string url) { t.Connect(url); }
        public void Tick() { t.Tick(); }

        /// <summary>Change your display name while waiting in the lobby (e.g. after joining via a QR code
        /// with whatever name was already saved) - no need to leave and rejoin with a new code. The host
        /// only accepts this before the game starts (see NetHost.OnPlayerMessage "hello").</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            GameSettings.PlayerName = name.Trim();
            t.Send(Json.Relay(new RelayMsg { t = "msg", data = JsonUtility.ToJson(new HelloMsg { name = GameSettings.PlayerName }) }));
        }

        public void Leave()
        {
            if (Session != null) Session.LeaveTable(); else t.Close();
            if (Status != State.Failed && Status != State.Denied) Status = State.Closed;
        }

        private void OnOpened()
        {
            Status = State.Joining;
            t.Send(Json.Relay(new RelayMsg { t = "join", code = Code, name = GameSettings.PlayerName }));
            Raise();
        }

        private void OnRelay(string text)
        {
            var m = Json.ParseRelay(text);
            if (m == null) return;
            switch (m.t)
            {
                case "joined":
                    MyId = m.id;
                    Status = State.InLobby;
                    Raise();
                    break;
                case "error":
                    Fail(m.reason);
                    break;
                case "closed":
                    Closed("The host left the game.");
                    break;
                case "msg":
                    OnHost(m.data);
                    break;
            }
        }

        private void OnHost(string data)
        {
            switch (Json.Kind(data))
            {
                case "lobby":
                    var lobby = Json.Parse<LobbyMsg>(data);
                    if (lobby != null) { Lobby = lobby; Raise(); }
                    break;

                case "denied":
                    var denied = Json.Parse<DeniedMsg>(data);
                    Status = State.Denied;
                    Error = denied != null ? denied.reason : "The host did not let you in.";
                    t.Close();
                    Raise();
                    break;

                case "start":
                    var start = Json.Parse<StartMsg>(data);
                    if (start == null) return;
                    playerId = start.playerId;
                    Session = new RemoteSession(t, Code, playerId);
                    Session.FirstSnapshot += () => { Status = State.InGame; if (Started != null) Started(); Raise(); };
                    break;

                case "snap":
                    var snap = Json.Parse<SnapMsg>(data);
                    if (snap != null && snap.snap != null && Session != null) Session.Apply(snap.snap);
                    break;

                case "refused":
                    var refused = Json.Parse<RefusedMsg>(data);
                    if (refused != null && Session != null) Session.RaiseRefused(refused.reason);
                    break;

                case "backlobby":
                    playerId = -1;
                    Session = null;
                    Status = State.InLobby;
                    Raise();
                    break;
            }
        }

        private void OnClosed(string reason)
        {
            if (Status == State.Failed || Status == State.Denied || Status == State.Closed) return;
            Closed(Status == State.Connecting ? "Could not reach the server." : "Lost the connection to the game.");
        }

        private void Closed(string why)
        {
            if (Status == State.Closed) return;
            Status = State.Closed;
            Error = why;
            if (Session != null) Session.RaiseEnded(why);
            Raise();
        }

        private void Fail(string why)
        {
            Status = State.Failed;
            Error = why;
            t.Close();
            Raise();
        }

        private void Raise() { if (Changed != null) Changed(); }
    }

    /// <summary>The phone's view of a game the host is running. It cannot change anything by itself.</summary>
    public sealed class RemoteSession : IPlayerSession
    {
        public string JoinCode { get; private set; }
        public int PlayerId { get; private set; }
        public event Action Updated;
        public event Action<string> Refused;
        public event Action<string> Ended;
        public event Action FirstSnapshot;

        private readonly ITransport t;
        private GameSnapshot latest;
        private int lastSentDecision = -1;

        public RemoteSession(ITransport transport, string code, int playerId)
        {
            t = transport;
            JoinCode = code;
            PlayerId = playerId;
        }

        public void Apply(SnapshotDto dto)
        {
            bool first = latest == null;
            latest = Wire.FromDto(dto);
            if (first && FirstSnapshot != null) FirstSnapshot();
            if (Updated != null) Updated();
        }

        public GameSnapshot Snapshot(bool spectator = false) { return latest; }

        /// <summary>Sends the tap to the host. If the host refuses it, Refused fires with the reason.</summary>
        public SubmitResult Submit(int decisionId, int index)
        {
            if (!t.IsOpen) return SubmitResult.Reject("The connection to the host was lost.");
            // One answer per question: repeated taps while the host has not replied yet are not sent again.
            if (decisionId >= 0 && decisionId == lastSentDecision) return SubmitResult.Accept();
            lastSentDecision = decisionId;
            t.Send(Json.Relay(new RelayMsg { t = "msg", data = JsonUtility.ToJson(new SubmitMsg { decisionId = decisionId, index = index }) }));
            return SubmitResult.Accept();
        }

        public void LeaveTable() { t.Close(); }

        public void RaiseRefused(string reason)
        {
            lastSentDecision = -1;      // the host said no, so the player may try again
            if (Refused != null) Refused(reason);
        }
        public void RaiseEnded(string reason) { if (Ended != null) Ended(reason); }
    }
}
