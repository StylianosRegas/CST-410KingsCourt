using System;
using System.Collections.Generic;
using KingsCourt.UnityLayer;

namespace KingsCourt.Tests
{
    /// <summary>An in-memory copy of Server/server.js, so multi-phone games can be tested without sockets.</summary>
    public sealed class LoopbackHub
    {
        private sealed class Room
        {
            public LoopbackTransport Host;
            public readonly Dictionary<int, LoopbackTransport> Peers = new Dictionary<int, LoopbackTransport>();
            public int Next = 1;
        }

        private readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        private readonly Dictionary<LoopbackTransport, string> roomOf = new Dictionary<LoopbackTransport, string>();
        private readonly Dictionary<LoopbackTransport, int> idOf = new Dictionary<LoopbackTransport, int>();
        private int codeCounter;
        public int MaxPeers = 11;

        public LoopbackTransport NewTransport() { return new LoopbackTransport(this); }
        public int RoomCount { get { return rooms.Count; } }

        internal void FromClient(LoopbackTransport from, string text)
        {
            var m = Json.ParseRelay(text);
            if (m == null) return;
            switch (m.t)
            {
                case "host":
                    string code = "AAA" + (char)('A' + codeCounter++);
                    rooms[code] = new Room { Host = from };
                    roomOf[from] = code;
                    from.Deliver(new RelayMsg { t = "hosted", code = code });
                    break;

                case "join":
                    Room room;
                    if (!rooms.TryGetValue((m.code ?? "").ToUpperInvariant(), out room)) { from.Deliver(new RelayMsg { t = "error", reason = "No game with that code." }); return; }
                    if (room.Peers.Count >= MaxPeers) { from.Deliver(new RelayMsg { t = "error", reason = "That game is full." }); return; }
                    int id = room.Next++;
                    room.Peers[id] = from;
                    roomOf[from] = (m.code ?? "").ToUpperInvariant();
                    idOf[from] = id;
                    from.Deliver(new RelayMsg { t = "joined", id = id });
                    room.Host.Deliver(new RelayMsg { t = "peer_joined", id = id, name = m.name });
                    break;

                case "msg":   // phone -> host
                    Room r1 = RoomOf(from);
                    if (r1 != null && idOf.ContainsKey(from)) r1.Host.Deliver(new RelayMsg { t = "msg", from = idOf[from], data = m.data });
                    break;

                case "to":    // host -> one phone
                    Room r2 = RoomOf(from);
                    LoopbackTransport peer;
                    if (r2 != null && r2.Host == from && r2.Peers.TryGetValue(m.id, out peer)) peer.Deliver(new RelayMsg { t = "msg", data = m.data });
                    break;

                case "all":
                    Room r3 = RoomOf(from);
                    if (r3 != null && r3.Host == from) foreach (var p in new List<LoopbackTransport>(r3.Peers.Values)) p.Deliver(new RelayMsg { t = "msg", data = m.data });
                    break;
            }
        }

        internal void Dropped(LoopbackTransport t)
        {
            Room room = RoomOf(t);
            if (room == null) return;
            string code = roomOf[t];
            if (room.Host == t)
            {
                foreach (var p in new List<LoopbackTransport>(room.Peers.Values)) { p.Deliver(new RelayMsg { t = "closed" }); p.ForceClosed("closed"); }
                rooms.Remove(code);
            }
            else if (idOf.ContainsKey(t))
            {
                room.Peers.Remove(idOf[t]);
                room.Host.Deliver(new RelayMsg { t = "peer_left", id = idOf[t] });
            }
        }

        private Room RoomOf(LoopbackTransport t)
        {
            string code;
            Room room;
            return roomOf.TryGetValue(t, out code) && rooms.TryGetValue(code, out room) ? room : null;
        }
    }

    public sealed class LoopbackTransport : ITransport
    {
        private readonly LoopbackHub hub;
        private readonly Queue<Action> inbox = new Queue<Action>();
        private bool closed;

        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action<string> Received;
        public event Action<string> Closed;

        public LoopbackTransport(LoopbackHub hub) { this.hub = hub; }

        public void Connect(string url) { inbox.Enqueue(() => { IsOpen = true; if (Opened != null) Opened(); }); }

        public void Send(string text) { if (IsOpen && !closed) hub.FromClient(this, text); }

        public void Close()
        {
            if (closed) return;
            closed = true;
            IsOpen = false;
            hub.Dropped(this);
        }

        internal void Deliver(RelayMsg m)
        {
            string text = Json.Relay(m);
            inbox.Enqueue(() => { if (Received != null) Received(text); });
        }

        internal void ForceClosed(string reason)
        {
            closed = true;
            IsOpen = false;
            inbox.Enqueue(() => { if (Closed != null) Closed(reason); });
        }

        public void Tick()
        {
            int n = inbox.Count;
            for (int i = 0; i < n; i++) inbox.Dequeue()();
        }
    }
}
