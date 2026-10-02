using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingsCourt.Core;
using KingsCourt.UnityLayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KingsCourt.Tests
{
    /// <summary>
    /// Host + several phones playing together, through the same host/client code the real game uses,
    /// with the relay server replaced by an in-memory copy.
    /// </summary>
    public class NetworkTests
    {
        private LoopbackHub hub;
        private NetHost host;
        private readonly List<NetClient> clients = new List<NetClient>();
        private readonly Pacer pacer = new Pacer { Instant = true };
        private float now;

        [SetUp]
        public void SetUp()
        {
            GameSettings.PaceMultiplier = 0f;
            GameSettings.FillWithBots = true;
            GameSettings.PlayerName = "Host";
            hub = new LoopbackHub();
            clients.Clear();
            now = 0;
        }

        [TearDown]
        public void TearDown() { GameSettings.PaceMultiplier = 1f; GameSettings.PlayerName = "Player 1"; }

        // ------------------------------------------------------------------ helpers

        private void Pump(int rounds = 8)
        {
            for (int i = 0; i < rounds; i++)
            {
                now += 0.1f;
                if (host != null) host.Tick(now);
                foreach (var c in clients) c.Tick();
            }
        }

        private void OpenRoom(int seats = 6)
        {
            host = new NetHost(hub.NewTransport(), seats);
            host.Connect("loopback");
            Pump();
            Assert.AreEqual(NetHost.State.Open, host.Status);
            Assert.IsNotEmpty(host.Code);
        }

        private NetClient Join(string name, string code = null)
        {
            GameSettings.PlayerName = name;
            var c = new NetClient(hub.NewTransport(), code ?? host.Code);
            clients.Add(c);
            c.Connect("loopback");
            Pump();
            return c;
        }

        private static bool Answer(IPlayerSession s, System.Random rng)
        {
            var snap = s.Snapshot();
            if (snap == null || snap.YourDecision == null) return false;
            s.Submit(snap.YourDecision.Id, rng.Next(snap.YourDecision.OptionCount));
            return true;
        }

        private void PlayToTheEnd(System.Random rng, int maxSteps = 4000)
        {
            for (int step = 0; step < maxSteps && host.Session.Game.Result == null; step++)
            {
                Answer(host.Session, rng);
                foreach (var c in clients) if (c.Session != null && c.Status == NetClient.State.InGame) Answer(c.Session, rng);
                Pump(2);
            }
            Pump();
        }

        // ------------------------------------------------------------------ tests

        [Test]
        public void ThreePhonesPlayAWholeGameTogether()
        {
            OpenRoom(6);
            var ann = Join("Ann");
            var bo = Join("Bo");
            Assert.AreEqual(2, host.Members.Count, "host should see both phones");
            Assert.AreEqual(3, ann.Lobby.members.Length, "a phone should see everyone in the lobby");
            Assert.AreEqual(NetClient.State.InLobby, ann.Status);

            var session = host.StartOnline(pacer, 5);
            Pump();
            Assert.AreEqual(NetClient.State.InGame, ann.Status);
            Assert.AreEqual(NetClient.State.InGame, bo.Status);

            // Each phone sees its own hand and role, and nobody else's hidden role
            // (except that Prince and Nobleman always recognize each other).
            bool IsTraitorTeam(KingsCourt.Core.Role r) => r == KingsCourt.Core.Role.Prince || r == KingsCourt.Core.Role.Nobleman || r == KingsCourt.Core.Role.Prophet;
            foreach (var c in new[] { ann, bo })
            {
                var snap = c.Session.Snapshot();
                Assert.AreEqual(3, snap.YourHand.Count, "each phone gets its own 3 cards");
                Assert.IsTrue(snap.YourRole.HasValue);
                bool viewerIsTraitor = IsTraitorTeam(snap.YourRole.Value);
                Assert.IsFalse(snap.Players.Any(p => !p.IsYou && !p.Revealed && p.Role.HasValue
                    && !(viewerIsTraitor && IsTraitorTeam(p.Role.Value))),
                    "a phone was sent someone else's hidden role");
                Assert.AreEqual(6, snap.Players.Count);
            }
            Assert.AreEqual("Ann", session.Game.Players[1].Name);
            Assert.AreEqual("Bo", session.Game.Players[2].Name);

            PlayToTheEnd(new System.Random(1));
            Assert.IsNotNull(session.Game.Result, "the game never finished");
            foreach (var c in new[] { ann, bo })
            {
                Assert.IsTrue(c.Session.Snapshot().Result != null, "a phone never saw the game end");
                Assert.AreEqual(session.Game.Result.Winner, c.Session.Snapshot().Result.Winner);
            }
            Assert.AreEqual(ActionCatalog.TotalCards(6), session.Game.CountCards(), "cards were lost or duplicated");
        }

        [Test]
        public void HostRefusesIllegalMovesFromPhones()
        {
            OpenRoom(6);
            var ann = Join("Ann");
            Join("Bo");
            host.StartOnline(pacer, 9);
            Pump();

            var refusals = new List<string>();
            ann.Session.Refused += r => refusals.Add(r);
            var rng = new System.Random(3);

            // Out of turn (no decision waiting for Ann): the host must refuse.
            for (int i = 0; i < 400 && refusals.Count == 0; i++)
            {
                if (ann.Session.Snapshot().YourDecision == null) { ann.Session.Submit(-1, 0); Pump(2); }
                else Answer(ann.Session, rng);
                Answer(host.Session, rng);
                foreach (var c in clients.Skip(1)) Answer(c.Session, rng);
                Pump(2);
            }
            Assert.IsNotEmpty(refusals, "playing out of turn was not refused");
            Assert.IsTrue(refusals[0].Contains("turn") || refusals[0].Contains("played") || refusals[0].Contains("open"), "unexpected refusal text: " + refusals[0]);

            // A stale/wrong decision id is refused too.
            refusals.Clear();
            for (int i = 0; i < 400 && ann.Session.Snapshot().YourDecision == null; i++)
            {
                Answer(host.Session, rng);
                foreach (var c in clients.Skip(1)) Answer(c.Session, rng);
                Pump(2);
            }
            var d = ann.Session.Snapshot().YourDecision;
            if (d != null)
            {
                ann.Session.Submit(d.Id + 999, 0);
                Pump(2);
                Assert.IsNotEmpty(refusals, "a wrong decision id was accepted");
            }
        }

        [Test]
        public void ADisconnectedPhoneIsReplacedByABotAndTheGameCarriesOn()
        {
            OpenRoom(6);
            var ann = Join("Ann");
            var bo = Join("Bo");
            var session = host.StartOnline(pacer, 11);
            Pump();
            var rng = new System.Random(2);
            for (int i = 0; i < 6; i++) { Answer(host.Session, rng); Answer(ann.Session, rng); Answer(bo.Session, rng); Pump(2); }

            int annSeat = ann.Session.PlayerId;
            ann.Leave();                       // Ann's phone drops
            Pump();
            Assert.IsTrue(session.Seats[annSeat].IsBot, "a bot should take over the seat");
            Assert.IsFalse(host.Members.Any(m => m.PlayerId == annSeat), "her freed room slot should no longer be listed as a member");

            clients.Remove(ann);
            PlayToTheEnd(rng);
            Assert.IsNotNull(session.Game.Result, "the game should finish without Ann");
            Assert.IsTrue(bo.Session.Snapshot().Result != null);
            Assert.IsTrue(bo.Session.Snapshot().Players.First(p => p.Id == annSeat).IsBot, "the others should see Ann's seat as a bot");
        }

        [Test]
        public void PhonesAreToldWhenTheHostLeaves()
        {
            OpenRoom(6);
            var ann = Join("Ann");
            host.StartOnline(pacer, 4);
            Pump();
            string ended = null;
            ann.Session.Ended += r => ended = r;
            host.Close();
            Pump();
            Assert.AreEqual(NetClient.State.Closed, ann.Status);
            Assert.IsNotNull(ended, "the phone was not told the game ended");
            Assert.AreEqual(0, hub.RoomCount);
        }

        [Test]
        public void JoiningWithABadCodeOrAFullGameIsRefused()
        {
            OpenRoom(5);
            var bad = Join("Zed", "QQQQ");
            Assert.AreEqual(NetClient.State.Failed, bad.Status);
            Assert.AreEqual("No game with that code.", bad.Error);

            var joined = new List<NetClient>();
            for (int i = 0; i < 4; i++) joined.Add(Join("P" + i));
            Assert.AreEqual(4, host.Members.Count);
            var extra = Join("Late");
            Assert.AreEqual(NetClient.State.Denied, extra.Status, "a fifth phone should not fit in a 5-seat game");
            Assert.AreEqual(4, host.Members.Count);
        }

        [Test]
        public void APhoneCanRejoinTheLobbyMidGameAndIsSeatedInTheNextOne()
        {
            OpenRoom(6);
            var ann = Join("Ann");
            var session = host.StartOnline(pacer, 7);
            Pump();
            Assert.AreEqual(NetClient.State.InGame, ann.Status);
            Assert.IsFalse(session.Seats[1].IsBot, "Ann's seat should start human");

            // She (or her connection) drops mid-game: a bot takes her seat and the game carries on.
            ann.Leave();
            Pump();
            Assert.IsTrue(session.Seats[1].IsBot, "a bot should take over her seat when she leaves");
            Assert.AreEqual(0, host.Members.Count, "leaving should free her spot, not leave a permanent ghost entry");

            // She (or anyone new) can rejoin and wait in the lobby while the game she left (or was never in) finishes,
            // instead of being denied for "that game has already started".
            var again = Join("Ann");
            Assert.AreEqual(NetClient.State.InLobby, again.Status, "a join while a game is running should wait in the lobby, not be denied");
            Assert.IsTrue(again.Lobby.inProgress, "the lobby should say a game is already running");

            PlayToTheEnd(new System.Random(5));
            Assert.IsNotNull(session.Game.Result, "the game she is waiting out never finished");
            // The waiting phone was never part of this game and should not have been asked to answer anything.
            Assert.IsNull(again.Session);

            host.ReturnToLobby();
            Pump();
            Assert.AreEqual(NetClient.State.InLobby, again.Status);
            Assert.IsFalse(again.Lobby.inProgress, "back between games, inProgress should clear");

            var next = host.StartOnline(pacer, 8);
            Pump();
            Assert.AreEqual(NetClient.State.InGame, again.Status, "the rejoined phone should be seated in the next game");
            Assert.IsFalse(next.Seats[1].IsBot);
        }

        [Test]
        public void PhonesWithTheSameNameGetUniqueNamesAndFindTheirOwnRow()
        {
            GameSettings.PlayerName = "Player 1";
            OpenRoom(6);
            var a = Join("Player 1");
            var b = Join("Player 1");
            var names = host.Members.Select(m => m.Name).ToList();
            Assert.AreEqual(2, names.Distinct().Count(), "the two phones got the same name");
            Assert.IsFalse(names.Contains("Player 1"), "a phone took the host's name");
            Assert.AreNotEqual(a.MyId, b.MyId);
            Assert.AreEqual(a.MyId, a.Lobby.members.First(m => m.id == a.MyId).id, "a phone cannot find its own row");
            Assert.AreEqual(3, a.Lobby.members.Select(m => m.name).Distinct().Count());
        }

        /// <summary>The same host/phone code as above, but over real WebSockets to the real Node relay (Server/server.js).</summary>
        [UnityTest]
        public IEnumerator RealRelayServer_HostAndTwoPhonesPlayAWholeGame()
        {
            string serverDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Server"));
            string serverJs = Path.Combine(serverDir, "server.js");
            if (!File.Exists(serverJs) || !Directory.Exists(Path.Combine(serverDir, "node_modules")))
                Assert.Ignore("Run 'npm install' in the Server folder to enable this test.");

            int port = 8200 + new System.Random().Next(700);
            var psi = new System.Diagnostics.ProcessStartInfo("node", "\"" + serverJs + "\" \"" + Path.GetTempPath() + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = serverDir,
            };
            psi.Environment["PORT"] = port.ToString();
            System.Diagnostics.Process proc = null;
            try { proc = System.Diagnostics.Process.Start(psi); }
            catch (System.Exception) { Assert.Ignore("Node.js is not installed."); }

            try
            {
                // Wait for the server to accept connections.
                float deadline = Time.realtimeSinceStartup + 15f;
                bool up = false;
                while (!up && Time.realtimeSinceStartup < deadline)
                {
                    try { using (var tcp = new System.Net.Sockets.TcpClient()) { tcp.Connect("127.0.0.1", port); up = true; } }
                    catch (System.Exception) { }
                    if (!up) yield return null;
                }
                Assert.IsTrue(up, "the relay server did not start");
                string url = "ws://127.0.0.1:" + port + "/ws";

                GameSettings.PlayerName = "Host";
                host = new NetHost(new WebSocketTransport(), 6);
                host.Connect(url);
                yield return Until(() => host.Status == NetHost.State.Open, 10f, "host never got a room code");

                var ann = NewRealClient("Ann", url);
                yield return Until(() => ann.Status == NetClient.State.InLobby && host.Members.Count == 1, 10f, "Ann never joined");
                var bo = NewRealClient("Bo", url);
                yield return Until(() => bo.Status == NetClient.State.InLobby && host.Members.Count == 2, 10f, "Bo never joined");
                yield return Until(() => ann.Lobby.members.Length == 3, 10f, "Ann never saw the lobby");

                var session = host.StartOnline(pacer, 33);
                yield return Until(() => ann.Status == NetClient.State.InGame && bo.Status == NetClient.State.InGame, 10f, "phones never got the game");

                // A windowless test run would otherwise wait for vsync between every network hop.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 1000;

                var rng = new System.Random(8);
                float start = Time.realtimeSinceStartup;
                float end = start + 150f;
                int frames = 0;
                while (session.Game.Result == null && Time.realtimeSinceStartup < end)
                {
                    Answer(host.Session, rng);
                    Answer(ann.Session, rng);
                    Answer(bo.Session, rng);
                    TickAll();
                    frames++;
                    yield return null;
                }
                Assert.IsNotNull(session.Game.Result, "the game did not finish over real sockets: host round " + session.Game.Round
                    + ", Ann sees round " + ann.Session.Snapshot().Round + ", Bo sees round " + bo.Session.Snapshot().Round
                    + ", " + frames + " frames in " + (Time.realtimeSinceStartup - start).ToString("0") + "s");
                Debug.Log("Real-socket game finished: " + session.Game.Round + " rounds, " + frames + " frames, " + (Time.realtimeSinceStartup - start).ToString("0.0") + "s");
                yield return Until(() => ann.Session.Snapshot().Result != null && bo.Session.Snapshot().Result != null, 10f, "phones never saw the end");

                // One phone drops: the room reports it and the host keeps a working session. Her seat becomes
                // a bot in the running game, but her room slot is freed immediately (not kept as a dead entry).
                ann.Leave();
                yield return Until(() => host.Members.Count == 1, 10f, "host was not told a phone left");
                host.Close();
                yield return Until(() => bo.Status == NetClient.State.Closed, 10f, "the other phone was not told the host left");
            }
            finally
            {
                if (host != null) host.Close();
                foreach (var c in clients) c.Leave();
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch (System.Exception) { }
            }
        }

        private NetClient NewRealClient(string name, string url)
        {
            GameSettings.PlayerName = name;
            var c = new NetClient(new WebSocketTransport(), host.Code);
            clients.Add(c);
            c.Connect(url);
            return c;
        }

        private void TickAll()
        {
            host.Tick(Time.unscaledTime);
            foreach (var c in clients) c.Tick();
        }

        private IEnumerator Until(System.Func<bool> condition, float seconds, string failure)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) { TickAll(); yield return null; }
            Assert.IsTrue(condition(), failure);
        }

        [Test]
        public void SnapshotSurvivesJson()
        {
            var agents = Enumerable.Range(0, 6).Select(i => (IPlayerAgent)new SeatAgent(i, i != 0, i)).ToList();
            var game = new Game(6, 3, agents);
            var task = game.RunAsync();
            var seat = (SeatAgent)agents[0];
            Assert.IsNotNull(seat.Pending, "the human should have a decision");
            var snap = GameSnapshot.For(game, 0, agents.Cast<SeatAgent>().ToList());
            string json = JsonUtility.ToJson(Wire.ToDto(snap));
            var back = Wire.FromDto(JsonUtility.FromJson<SnapshotDto>(json));
            Assert.AreEqual(snap.YourHand.Count, back.YourHand.Count);
            Assert.AreEqual(snap.YourDecision.Id, back.YourDecision.Id);
            Assert.AreEqual(snap.YourDecision.Prompt, back.YourDecision.Prompt);
            Assert.AreEqual(snap.Players.Select(p => p.Role).ToList(), back.Players.Select(p => p.Role).ToList());
            Assert.AreEqual(snap.YourRole, back.YourRole);
        }
    }
}
