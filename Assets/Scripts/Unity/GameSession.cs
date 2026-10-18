using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>Player-facing options, saved between runs. Storage may be blocked, so every access is guarded.</summary>
    public static class GameSettings
    {
        public static bool FillWithBots { get { return GetInt("fillBots", 1) == 1; } set { SetInt("fillBots", value ? 1 : 0); } }
        public static bool ChatEnabled { get { return GetInt("chat", 0) == 1; } set { SetInt("chat", value ? 1 : 0); } }
        /// <summary>0 = relaxed, 1 = normal, 2 = fast (only changes how long the game pauses between steps).</summary>
        public static int Speed { get { return Mathf.Clamp(GetInt("speed", 1), 0, 2); } set { SetInt("speed", value); } }
        /// <summary>Blank = the server the page was loaded from (phones) or localhost (editor).</summary>
        public static string ServerUrl { get { return GetString("server", ""); } set { SetString("server", value ?? ""); } }
        public static string PlayerName { get { return GetString("name", "Player 1"); } set { SetString("name", value); } }

        /// <summary>Tests set this to 0 to skip every pause.</summary>
        public static float PaceMultiplier = 1f;
        public static float PaceFactor { get { return (Speed == 0 ? 1.6f : Speed == 2 ? 0.35f : 1f) * PaceMultiplier; } }

        private static int GetInt(string k, int d) { try { return PlayerPrefs.GetInt("kc_" + k, d); } catch (Exception) { return d; } }
        private static void SetInt(string k, int v) { try { PlayerPrefs.SetInt("kc_" + k, v); } catch (Exception) { } }
        private static string GetString(string k, string d) { try { return PlayerPrefs.GetString("kc_" + k, d); } catch (Exception) { return d; } }
        private static void SetString(string k, string v) { try { PlayerPrefs.SetString("kc_" + k, v); } catch (Exception) { } }
    }

    /// <summary>Lets the rules engine pause between steps without threads (works in WebGL too).</summary>
    public sealed class Pacer
    {
        private readonly List<KeyValuePair<float, TaskCompletionSource<bool>>> timers = new List<KeyValuePair<float, TaskCompletionSource<bool>>>();

        /// <summary>Skip all pauses (used when rendering screenshots).</summary>
        public bool Instant;

        public Task Wait(int ms)
        {
            if (Instant) return Task.CompletedTask;
            float seconds = ms / 1000f * GameSettings.PaceFactor;
            if (seconds <= 0.01f) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            timers.Add(new KeyValuePair<float, TaskCompletionSource<bool>>(Time.unscaledTime + seconds, tcs));
            return tcs.Task;
        }

        public void Tick()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < timers[i].Key) continue;
                var tcs = timers[i].Value;
                timers.RemoveAt(i);
                tcs.SetResult(true);
            }
        }

        public void CancelAll()
        {
            var all = timers.Select(t => t.Value).ToList();
            timers.Clear();
            foreach (var t in all) t.SetResult(true);
        }
    }

    /// <summary>
    /// One running game as seen from this device. Today the host and the only human player are the same
    /// device (everyone else is a bot). The network layer will put remote players in the other seats: it only
    /// has to send each player their GameSnapshot and call SeatAgent.Submit with what they answer.
    /// </summary>
    public sealed class GameSession : IPlayerSession
    {
        public readonly Game Game;
        public readonly List<SeatAgent> Seats;
        public readonly int LocalPlayerId;
        public string JoinCode { get; private set; }
        public event Action Updated;
        // Never raised for a local game: Submit() answers directly. They exist so the UI treats every game the same way.
        public event Action<string> Refused { add { } remove { } }
        public event Action<string> Ended { add { } remove { } }

        private readonly Pacer pacer;
        private bool running;

        /// <summary>Build 5: when a scenario is given, it decides the player count, hand size, refill
        /// behaviour and any extra end condition - the `players` argument is ignored in favour of
        /// scenario.PlayerCount, same as the brief asks ("nothing scenario-specific is hardcoded in a
        /// scene or a script").</summary>
        public GameSession(int players, int localPlayerId, int seed, Pacer pacer, string joinCode = null, ScenarioConfig scenario = null)
        {
            this.pacer = pacer;
            LocalPlayerId = localPlayerId;
            JoinCode = joinCode ?? NewCode(seed);
            int count = scenario != null ? scenario.PlayerCount : players;
            Seats = new List<SeatAgent>();
            for (int i = 0; i < count; i++) Seats.Add(new SeatAgent(i, i != localPlayerId, seed * 31 + i));
            Game = scenario != null ? scenario.Start(Seats.Cast<IPlayerAgent>().ToList(), seed) : new Game(count, seed, Seats.Cast<IPlayerAgent>().ToList());
            Game.Players[localPlayerId].Rename(GameSettings.PlayerName);
            for (int i = 0; i < count; i++) if (i != localPlayerId) Game.Players[i].Rename("Bot " + (i + 1));
            // Scenario-specific starting state that isn't a round-check condition (Marked Court: Queen and
            // Knight start already revealed) - applied once, right after the deal, not as a per-round rule.
            if (scenario != null && scenario.Id == "marked-court")
            {
                var queen = Game.Players.FirstOrDefault(p => p.Role == Role.Queen);
                var knight = Game.Players.FirstOrDefault(p => p.Role == Role.Knight);
                if (queen != null) Game.AdminReveal(queen.Id);
                if (knight != null) Game.AdminReveal(knight.Id);
            }
            Game.Beat = pacer.Wait;
            Game.Changed += Raise;
            Game.Logged += _ => Raise();
            foreach (var s in Seats) { s.DecisionOpened += (a, d) => Raise(); s.DecisionClosed += a => Raise(); }
        }

        public static string NewCode(int seed)
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            var r = new System.Random(seed * 7919 + 13);
            return new string(Enumerable.Range(0, 4).Select(_ => letters[r.Next(letters.Length)]).ToArray());
        }

        private void Raise() { if (Updated != null) Updated(); }

        public async void Start()
        {
            if (running) return;
            running = true;
            try { await Game.RunAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            Raise();
        }

        public GameSnapshot Snapshot(bool spectator = false)
        {
            // When the game is over every role is shown.
            return GameSnapshot.For(Game, spectator ? -1 : LocalPlayerId, Seats, spectator || Game.Result != null, 7);
        }

        /// <summary>The host checks every answer. Returns why it was refused (out of turn, second card, stale...).</summary>
        public SubmitResult Submit(int decisionId, int index)
        {
            return Seats[LocalPlayerId].Submit(decisionId, index);
        }

        /// <summary>The local player leaves: a bot takes the seat so the game (for anyone else) carries on.</summary>
        /// <summary>A phone joined before the start: this seat is now a human, and the player picks the name.</summary>
        public void AssignHuman(int playerId, string name)
        {
            Seats[playerId].BecomeHuman();
            Game.Players[playerId].Rename(name);
        }

        public void LeaveTable()
        {
            Seats[LocalPlayerId].BecomeBot();
            pacer.CancelAll();
        }
    }
}
