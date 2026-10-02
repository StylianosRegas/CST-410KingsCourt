using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Runs on the host's phone. Opens a room on the relay server, lists who joins, then runs the game:
    /// it is the only place the rules run, so it decides whether every move from every phone is legal.
    /// </summary>
    public sealed class NetHost
    {
        public enum State { Connecting, Open, Failed, Closed }

        public sealed class Member
        {
            public int Id;                 // relay id
            public string Name;
            public int PlayerId = -1;      // seat in the game once it starts
            public bool Online = true;
        }

        public State Status { get; private set; }
        public string Code { get; private set; }
        public string Error { get; private set; }
        /// <summary>The server's own guess at its LAN-reachable "host:port", e.g. "192.168.1.5:8000" —
        /// more reliable than the host's own page URL, which could be "localhost" and useless to anyone
        /// else. Empty if the server couldn't tell (very old server, or no LAN interface found).</summary>
        public string LanAddress { get; private set; } = "";
        public int Seats { get; private set; }
        public GameSession Session { get; private set; }
        public IReadOnlyList<Member> Members { get { return members; } }
        public event Action Changed;

        private readonly ITransport t;
        private readonly List<Member> members = new List<Member>();
        private bool dirty;
        private float lastSend;
        private int guestCounter;

        public NetHost(ITransport transport, int seats = 6)
        {
            t = transport;
            HostName = GameSettings.PlayerName;
            Seats = Mathf.Clamp(seats, Game.MinPlayers, Game.MaxPlayers);
            Status = State.Connecting;
            Code = "";
            Error = "";
            t.Opened += () => t.Send(Json.Relay(new RelayMsg { t = "host" }));
            t.Received += OnRelay;
            t.Closed += OnClosed;
        }

        public void Connect(string url) { t.Connect(url); }

        /// <summary>A free seat is a free seat whether or not a game is currently running: a game in progress
        /// no longer turns joins away, it just waits them in the lobby for the next round (see peer_joined).</summary>
        public bool CanTakeMorePlayers { get { return members.Count + 1 < Seats; } }

        public void SetSeats(int seats)
        {
            if (Session != null) return;
            Seats = Mathf.Clamp(seats, Game.MinPlayers, Game.MaxPlayers);
            if (Seats < members.Count + 1) Seats = members.Count + 1;   // cannot go below the people already seated
            BroadcastLobby();
            Raise();
        }

        public void Close()
        {
            if (Status != State.Failed) Status = State.Closed;
            t.Close();
        }

        /// <summary>Called every frame by the controller.</summary>
        public void Tick(float now)
        {
            t.Tick();
            if (Session != null && dirty && now - lastSend > 0.08f)
            {
                dirty = false;
                lastSend = now;
                SendSnapshots();
            }
        }

        // ------------------------------------------------------------ lobby

        /// <summary>The host's name, fixed when the room was opened.</summary>
        public string HostName { get; private set; }

        private LobbyMsg BuildLobby()
        {
            var list = new List<MemberDto> { new MemberDto { id = 0, name = HostName, isHost = true, playerId = 0 } };
            list.AddRange(members.Select(m => new MemberDto { id = m.Id, name = m.Name, playerId = m.PlayerId, online = m.Online }));
            return new LobbyMsg { members = list.ToArray(), seats = Seats, bots = GameSettings.FillWithBots, code = Code, inProgress = Session != null };
        }

        public void BroadcastLobby()
        {
            if (Status != State.Open) return;
            t.Send(Json.Relay(new RelayMsg { t = "all", data = JsonUtility.ToJson(BuildLobby()) }));
        }

        /// <summary>Starts the real game with everyone who has joined; bots take the remaining seats.</summary>
        public GameSession StartOnline(Pacer pacer, int seed)
        {
            Session = new GameSession(Seats, 0, seed, pacer, Code);
            Session.Game.Players[0].Rename(HostName);
            int pid = 1;
            foreach (var m in members)
            {
                m.PlayerId = pid;
                Session.AssignHuman(pid, m.Name);
                pid++;
            }
            foreach (var m in members)
                SendTo(m.Id, new StartMsg { playerId = m.PlayerId, code = Code });
            Session.Updated += () => dirty = true;
            dirty = true;
            Session.Start();
            return Session;
        }

        /// <summary>The game just ended: drop the finished session and send everyone back to this same room's lobby.</summary>
        public void ReturnToLobby()
        {
            if (Session == null) return;
            Session = null;
            foreach (var m in members) m.PlayerId = -1;
            foreach (var m in members) if (m.Online) SendTo(m.Id, new BackToLobbyMsg());
            BroadcastLobby();
            Raise();
        }

        // ------------------------------------------------------------ relay traffic

        private void OnRelay(string text)
        {
            var m = Json.ParseRelay(text);
            if (m == null) return;
            switch (m.t)
            {
                case "hosted":
                    Code = m.code;
                    LanAddress = m.lan ?? "";
                    Status = State.Open;
                    Raise();
                    break;

                case "error":
                    Error = m.reason;
                    Status = State.Failed;
                    Raise();
                    break;

                case "peer_joined":
                    // A game already in progress no longer turns a join away: they wait in the lobby
                    // (PlayerId stays -1, so they get no snapshots) and join the next game once it starts.
                    if (!CanTakeMorePlayers)
                    {
                        SendTo(m.id, new DeniedMsg { reason = "That game is full." });
                        return;
                    }
                    members.Add(new Member { Id = m.id, Name = UniqueName(string.IsNullOrWhiteSpace(m.name) ? "Guest " + (++guestCounter) : m.name, null) });
                    BroadcastLobby();
                    Raise();
                    break;

                case "peer_left":
                    OnPeerLeft(m.id);
                    break;

                case "msg":
                    OnPlayerMessage(m.from, m.data);
                    break;
            }
        }

        /// <summary>Two phones can both be called "Player 1" (the default), so make names unique: "Player 1 (2)".</summary>
        private string UniqueName(string wanted, Member except)
        {
            string baseName = (wanted ?? "").Trim();
            if (baseName.Length > 24) baseName = baseName.Substring(0, 24);
            if (baseName.Length == 0) baseName = "Guest";
            string name = baseName;
            int n = 2;
            while (name == HostName || members.Any(x => x != except && x.Name == name)) name = baseName + " (" + n++ + ")";
            return name;
        }

        private void OnPlayerMessage(int from, string data)
        {
            var member = members.FirstOrDefault(x => x.Id == from);
            if (member == null) return;
            switch (Json.Kind(data))
            {
                case "hello":
                    var hello = Json.Parse<HelloMsg>(data);
                    if (hello != null && !string.IsNullOrWhiteSpace(hello.name) && Session == null)
                    {
                        member.Name = UniqueName(hello.name, member);
                        BroadcastLobby();
                        Raise();
                    }
                    break;

                case "submit":
                    var submit = Json.Parse<SubmitMsg>(data);
                    if (submit == null) return;
                    if (Session == null || member.PlayerId < 0) { SendTo(from, new RefusedMsg { reason = "The game has not started." }); return; }
                    // The relay tells us which phone sent this, so a phone can only ever act as its own player.
                    var result = Session.Seats[member.PlayerId].Submit(submit.decisionId, submit.index);
                    if (!result.Ok) SendTo(from, new RefusedMsg { reason = result.Reason });
                    dirty = true;
                    break;
            }
        }

        private void OnPeerLeft(int id)
        {
            var member = members.FirstOrDefault(x => x.Id == id);
            if (member == null) return;
            if (Session != null && member.PlayerId >= 0)
            {
                // A bot takes over the seat, whatever the player's role. The game carries on.
                Session.Seats[member.PlayerId].BecomeBot();
                dirty = true;
            }
            // Either way, drop their spot in the room: a mid-game seat is now a bot independent of this
            // member list, so there's nothing left to keep here, and it frees the slot so this same person
            // (or anyone else) can rejoin instead of finding the room permanently "full".
            members.Remove(member);
            BroadcastLobby();
            Raise();
        }

        private void OnClosed(string reason)
        {
            if (Status == State.Connecting) { Status = State.Failed; Error = reason; }
            else if (Status != State.Failed) { Status = State.Closed; Error = reason; }
            Raise();
        }

        // ------------------------------------------------------------ sending

        private void SendSnapshots()
        {
            foreach (var m in members)
            {
                if (!m.Online || m.PlayerId < 0) continue;
                var snap = GameSnapshot.For(Session.Game, m.PlayerId, Session.Seats, Session.Game.Result != null, 10);
                SendTo(m.Id, new SnapMsg { snap = Wire.ToDto(snap) });
            }
        }

        private void SendTo(int id, object msg)
        {
            t.Send(Json.Relay(new RelayMsg { t = "to", id = id, data = JsonUtility.ToJson(msg) }));
        }

        private void Raise() { if (Changed != null) Changed(); }
    }
}
