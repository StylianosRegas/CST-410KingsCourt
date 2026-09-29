using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KingsCourt.Core
{
    public sealed class GameResult
    {
        public Winner Winner;
        public string Reason;
    }

    /// <summary>
    /// Host-authoritative King's Court rules. No Unity dependencies so it can be unit-tested,
    /// simulated with bots, and later driven by a network layer.
    /// Assumptions where the GDD is silent are listed in Assets/Docs/RULES_ASSUMPTIONS.md.
    /// </summary>
    public sealed class Game
    {
        public const int ThroneSeat = 0;
        public const int HandSize = 3;
        public const int MinPlayers = 5;
        public const int MaxPlayers = 8;

        public readonly List<Player> Players = new List<Player>();
        public readonly List<ActionCard> Deck;
        public readonly List<ActionCard> Discard = new List<ActionCard>();
        public readonly List<string> Log = new List<string>();
        public int Round { get; private set; }
        /// <summary>Trusted advisor of the current/most recent round, or -1.</summary>
        public int TrustedAdvisorId { get; private set; } = -1;
        public GameResult Result { get; private set; }
        public int SeatCount { get { return Players.Count; } }
        public event Action<string> Logged;

        private readonly Random rng;
        private readonly IPlayerAgent[] agents;

        /// <summary>Pacing hook: the UI sets this to wait a moment between steps so people can follow. Bots-only sims leave it.</summary>
        public Func<int, Task> Beat = ms => Task.CompletedTask;

        /// <summary>Raised whenever the visible game state changes.</summary>
        public event Action Changed;
        private void NotifyChanged() { if (Changed != null) Changed(); }
        private readonly List<ActionCard> inPlay = new List<ActionCard>();

        // Effects that carry into the next round.
        private int forcedTrustedId = -1;
        private bool nextTrustedMustReveal, nextFaceUp, nextEnactTwo, nextAllToThronebearer;
        // Effects active during the current round.
        private bool roundFaceUp, roundEnactTwo, roundAllToThronebearer;
        private readonly HashSet<int> roundFaceUpIds = new HashSet<int>();
        private int princeStreak;

        public Game(int playerCount, int seed, IList<IPlayerAgent> playerAgents)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException("playerCount", "King's Court is for 5 to 8 players.");
            if (playerAgents == null || playerAgents.Count != playerCount)
                throw new ArgumentException("One agent per player is required.");

            rng = new Random(seed);
            agents = playerAgents.ToArray();

            var roles = DealRoles(playerCount);
            var seats = Enumerable.Range(1, playerCount - 1).OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < playerCount; i++)
            {
                int seat = roles[i] == Role.King ? ThroneSeat : seats[0];
                if (roles[i] != Role.King) seats.RemoveAt(0);
                Players.Add(new Player(i, "Player " + (i + 1), roles[i], seat));
            }

            Deck = ActionCatalog.BuildDeck();
            Shuffle(Deck);
            foreach (var p in Players) DrawInto(p, HandSize);
            Say("The court is seated: " + playerCount + " players.");
        }

        // ---------------------------------------------------------------- setup

        private static readonly Role[] LoyalistRoles = { Role.Queen, Role.Knight, Role.Elder, Role.CourtWizard };
        private static readonly Role[] ChaosRoles = { Role.Jester, Role.Beggar };

        /// <summary>
        /// King, Prince and Nobleman always play. Some player counts also guarantee one Chaos and/or one
        /// Loyalist role. Every remaining seat is drawn without replacement: pick Loyalist or Chaos by the odds
        /// below, then a random role of that kind. A kind whose roles have run out drops to 0%.
        ///   5 players: 2 draws, 70% Loyalist / 30% Chaos
        ///   6 players: +1 Chaos, 2 draws, 50 / 50
        ///   7 players: +1 Chaos +1 Loyalist, 2 draws, 20% Loyalist / 80% Chaos
        ///   8 players: +1 Chaos +1 Loyalist, 3 draws, 20% / 80%
        /// </summary>
        private List<Role> DealRoles(int n)
        {
            bool guaranteedChaos = n >= 6;
            bool guaranteedLoyalist = n >= 7;
            double loyalistChance = n == 5 ? 0.7 : n == 6 ? 0.5 : 0.2;

            var roles = new List<Role> { Role.King, Role.Prince, Role.Nobleman };
            var loyalists = new List<Role>(LoyalistRoles);
            var chaos = new List<Role>(ChaosRoles);

            if (guaranteedChaos) roles.Add(TakeRandom(chaos));
            if (guaranteedLoyalist) roles.Add(TakeRandom(loyalists));

            while (roles.Count < n)
            {
                double wLoyal = loyalists.Count > 0 ? loyalistChance : 0;
                double wChaos = chaos.Count > 0 ? 1 - loyalistChance : 0;
                if (wLoyal + wChaos <= 0) break;
                var pool = rng.NextDouble() * (wLoyal + wChaos) < wLoyal ? loyalists : chaos;
                roles.Add(TakeRandom(pool));
            }
            Shuffle(roles);
            return roles;
        }

        private Role TakeRandom(List<Role> pool)
        {
            int i = rng.Next(pool.Count);
            var r = pool[i];
            pool.RemoveAt(i);
            return r;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        // ---------------------------------------------------------------- queries

        public Player Thronebearer { get { return Players.FirstOrDefault(p => p.Alive && p.Seat == ThroneSeat); } }
        public IEnumerable<Player> AlivePlayers { get { return Players.Where(p => p.Alive).OrderBy(p => p.Seat); } }

        private List<Player> Advisors()
        {
            var tb = Thronebearer;
            return AlivePlayers.Where(p => p != tb).ToList();
        }

        private Player PlayerAtSeat(int seat) { return Players.FirstOrDefault(p => p.Alive && p.Seat == seat); }

        /// <summary>Cards that exist anywhere: deck + discard + hands + cards mid-round.</summary>
        public int CountCards()
        {
            return Deck.Count + Discard.Count + inPlay.Count + Players.Sum(p => p.Hand.Count);
        }

        private void Say(string msg)
        {
            Log.Add(msg);
            if (Logged != null) Logged(msg);
        }

        // ---------------------------------------------------------------- main loop

        public GameResult Run(int maxRounds = 200)
        {
            // Only for all-bot games: every task completes synchronously, so this never blocks.
            return RunAsync(maxRounds).GetAwaiter().GetResult();
        }

        public async Task<GameResult> RunAsync(int maxRounds = 200)
        {
            while (Result == null)
            {
                if (Round >= maxRounds) { End(Winner.Draw, "Round limit reached."); break; }
                await PlayRoundAsync();
            }
            return Result;
        }

        /// <summary>Synchronous round for all-bot games.</summary>
        public void PlayRound() { PlayRoundAsync().GetAwaiter().GetResult(); }

        /// <summary>
        /// Did this player win? Loyalists' win goes to everyone currently counted as a loyalist (a Beggar who is
        /// still hidden wins with them). A Traitors' win goes to the Prince, Nobleman and a REVEALED Beggar.
        /// A Jester win goes to the Jester alone.
        /// </summary>
        public bool DidWin(Player p)
        {
            if (Result == null) return false;
            switch (Result.Winner)
            {
                case Winner.Loyalists: return p.EffectiveFaction == Faction.Loyalist;
                case Winner.Traitors: return p.EffectiveFaction == Faction.Traitor;
                case Winner.Jester: return p.Role == Role.Jester;
                default: return false;
            }
        }

        private void End(Winner winner, string reason)
        {
            if (Result != null) return;
            Result = new GameResult { Winner = winner, Reason = reason };
            Say("GAME OVER: " + winner + " win. " + reason);
        }

        public async Task PlayRoundAsync()
        {
            if (Result != null) return;
            Round++;
            EnsureThrone();
            if (AlivePlayers.Count() < 3) { End(Winner.Draw, "Too few players left to hold a court."); return; }

            // Pending effects from last round become active now.
            roundFaceUp = nextFaceUp; roundEnactTwo = nextEnactTwo; roundAllToThronebearer = nextAllToThronebearer;
            bool mustReveal = nextTrustedMustReveal;
            int forced = forcedTrustedId;
            nextFaceUp = nextEnactTwo = nextAllToThronebearer = nextTrustedMustReveal = false;
            forcedTrustedId = -1;
            roundFaceUpIds.Clear();
            foreach (var a in agents) { var aware = a as IRoundAware; if (aware != null) aware.OnRoundStart(); }

            Say("--- Round " + Round + " --- thronebearer: " + Thronebearer.Name);

            await AbilityPhaseAsync();
            if (Result != null) return;
            EnsureThrone();
            var tb = Thronebearer;

            // 1. Thronebearer names the trusted advisor.
            var candidates = Advisors();
            if (candidates.Count == 0) { End(Winner.Draw, "No advisors left."); return; }
            Player ta = candidates.FirstOrDefault(p => p.Id == forced);
            if (ta == null)
            {
                var ids = candidates.Select(p => p.Id).ToList();
                ta = candidates[await PickAsync(tb, "Choose your trusted advisor", ids, true, ChoiceKind.TrustedAdvisor)];
            }
            TrustedAdvisorId = ta.Id;
            Say(ta.Name + " is the trusted advisor.");
            if (mustReveal) Reveal(ta);

            NotifyChanged();
            await Beat(700);

            // 2. Every other advisor (not the thronebearer, not the trusted advisor) plays face down, all at once.
            //    The Elder plays two. The trusted advisor plays nothing.
            var played = new List<PlayedCard>();
            var players = AlivePlayers.Where(x => x != tb && x != ta).ToList();
            await Task.WhenAll(players.Select(p => PlayCardsAsync(p, played)));
            Shuffle(played);   // so seat order does not give away who played what
            NotifyChanged();
            await Beat(700);

            // 3. Trusted advisor filters to two (unless everything goes straight to the thronebearer).
            List<PlayedCard> forKing;
            if (roundAllToThronebearer)
            {
                forKing = played;
                Say("All advice goes straight to the thronebearer.");
            }
            else
            {
                forKing = new List<PlayedCard>();
                var pool = new List<PlayedCard>(played);
                while (forKing.Count < 2 && pool.Count > 0)
                {
                    int i = await PickCardForAsync(ta, "Pass this card to the king", pool.Select(x => x.Card).ToList(), ChoiceKind.KeepCard, PlayedNotes(pool));
                    forKing.Add(pool[i]);
                    pool.RemoveAt(i);
                }
                foreach (var c in pool) Discard1(c.Card);
                if (played.Count > 0) Say(ta.Name + " passes " + forKing.Count + " card(s) to the king.");
            }
            NotifyChanged();
            await Beat(600);

            // 4. Thronebearer picks and enacts.
            int enact = roundEnactTwo ? 2 : 1;
            for (int n = 0; n < enact && forKing.Count > 0 && Result == null; n++)
            {
                int i = await PickCardForAsync(tb, "Choose the card to enact", forKing.Select(x => x.Card).ToList(), ChoiceKind.EnactCard, PlayedNotes(forKing));
                var c = forKing[i].Card;
                forKing.RemoveAt(i);
                Say(tb.Name + " enacts: " + c.Text);
                NotifyChanged();
                await Beat(900);
                await ResolveAsync(c, tb, ta);
                Discard1(c);
                NotifyChanged();
                await Beat(700);
            }
            foreach (var c in forKing) Discard1(c.Card);
            if (Result != null) { NotifyChanged(); return; }

            // 5. Refill hands, check the Prince clock.
            foreach (var p in AlivePlayers)
                if (p.Hand.Count < HandSize) DrawInto(p, HandSize - p.Hand.Count);

            EnsureThrone();
            var now = Thronebearer;
            princeStreak = (now != null && now.Role == Role.Prince) ? princeStreak + 1 : 0;
            if (princeStreak >= 2) End(Winner.Traitors, "The Prince held the throne for 2 consecutive rounds.");
            NotifyChanged();
            await Beat(500);
        }

        private sealed class PlayedCard
        {
            public ActionCard Card;
            public int OwnerId;
            public bool FaceUp;
        }

        private IReadOnlyList<string> PlayedNotes(List<PlayedCard> cards)
        {
            return cards.Select(c => c.FaceUp ? "Played face up by " + ById(c.OwnerId).Name : "").ToList();
        }

        private async Task PlayCardsAsync(Player p, List<PlayedCard> played)
        {
            int count = p.Role == Role.Elder ? 2 : 1;
            for (int i = 0; i < count && p.Hand.Count > 0; i++)
            {
                string prompt = count == 2 ? "Play an action card (" + (i + 1) + " of 2)" : "Play an action card";
                var card = p.Hand[await PickCardForAsync(p, prompt, p.Hand.ToList(), ChoiceKind.PlayCard)];
                p.Hand.Remove(card);
                inPlay.Add(card);
                bool faceUp = roundFaceUp || roundFaceUpIds.Contains(p.Id);
                played.Add(new PlayedCard { Card = card, OwnerId = p.Id, FaceUp = faceUp });
                Say(p.Name + " plays a card " + (faceUp ? "face up: " + card.Text : "face down."));
                NotifyChanged();
            }
        }

        // ---------------------------------------------------------------- abilities

        private async Task AbilityPhaseAsync()
        {
            foreach (var p in Players.OrderBy(x => x.Seat).ToList())
            {
                if (!p.Alive || Result != null) continue;
                if (!p.Revealed) continue;                       // abilities need a reveal
                if (p.Role != Role.Jester && p.AbilityUsed) continue;
                var tb = Thronebearer;

                switch (p.Role)
                {
                    case Role.Queen:
                        if (tb != p) break;
                        var qTargets = Advisors().Where(a => !a.Revealed).Select(a => a.Id).ToList();
                        if (qTargets.Count > 0 && await AskAsync(p, "Use Power in Knowledge?"))
                        {
                            Reveal(ById(qTargets[await PickAsync(p, "Whose loyalty card do you reveal?", qTargets, false)]));
                            p.AbilityUsed = true;
                        }
                        break;

                    case Role.Knight:
                        var kTargets = AlivePlayers.Where(a => a != p && !a.Revealed).Select(a => a.Id).ToList();
                        if (kTargets.Count > 0 && await AskAsync(p, "Use A Knight's Duty?"))
                        {
                            var victim = ById(kTargets[await PickAsync(p, "Who do you execute?", kTargets, false)]);
                            p.AbilityUsed = true;
                            bool loyalist = victim.EffectiveFaction == Faction.Loyalist;
                            Execute(victim, true);
                            if (loyalist && Result == null) { Say("The Knight executed a loyalist and is executed too."); Execute(p, true); }
                        }
                        break;

                    case Role.CourtWizard:
                        var wTargets = Advisors().Where(a => a != p).Select(a => a.Id).ToList();
                        if (wTargets.Count > 0 && await AskAsync(p, "Use Shadow Money Wizard Gang?"))
                        {
                            int id = wTargets[await PickAsync(p, "Who must play face up?", wTargets, false)];
                            roundFaceUpIds.Add(id);
                            p.AbilityUsed = true;
                            Say(p.Name + " forces " + ById(id).Name + " to play face up.");
                        }
                        break;

                    case Role.Jester:
                        var jTargets = AlivePlayers.Where(a => a != p && a.Hand.Count > 0).Select(a => a.Id).ToList();
                        if (jTargets.Count > 0 && await AskAsync(p, "Use Clownin' Around?"))
                        {
                            var v = ById(jTargets[await PickAsync(p, "Who discards a random card?", jTargets, false)]);
                            var card = v.Hand[rng.Next(v.Hand.Count)];
                            v.Hand.Remove(card);
                            Discard.Add(card);
                            Say(p.Name + " (Jester) makes " + v.Name + " discard a random card.");
                        }
                        break;

                    case Role.Prince:
                        if (tb != null && tb != p && await AskAsync(p, "Use Rightful Heir?"))
                        {
                            SwapSeats(p, tb);
                            p.AbilityUsed = true;
                            Say(p.Name + " (Prince) swaps places with the thronebearer.");
                        }
                        break;

                    case Role.Nobleman:
                        var nTargets = AlivePlayers.Where(a => a != p && a.Hand.Count > 0).Select(a => a.Id).ToList();
                        if (nTargets.Count >= 2 && await AskAsync(p, "Use Riches And Spoils?"))
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                var v = ById(nTargets[await PickAsync(p, "Take a card from whom?", nTargets, false)]);
                                nTargets.Remove(v.Id);
                                var card = v.Hand[rng.Next(v.Hand.Count)];
                                v.Hand.Remove(card);
                                p.Hand.Add(card);
                                Say(p.Name + " (Nobleman) takes a card from " + v.Name + ".");
                            }
                            p.AbilityUsed = true;
                        }
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- card effects

        private async Task ResolveAsync(ActionCard card, Player tb, Player ta)
        {
            var advisors = Advisors();
            switch (card.Effect)
            {
                case ActionEffect.ExecuteUnrevealed:
                    await ExecutePickAsync(tb, advisors.Where(a => !a.Revealed), "Execute which unrevealed player?", true);
                    break;
                case ActionEffect.ExecuteUnrevealedHidden:
                    await ExecutePickAsync(tb, advisors.Where(a => !a.Revealed), "Execute which unrevealed player (role stays hidden)?", false);
                    break;
                case ActionEffect.ExecuteAdvisor:
                    await ExecutePickAsync(tb, advisors, "Execute which advisor?", true);
                    break;
                case ActionEffect.DieExecute:
                    {
                        var p = RollSeat();
                        if (p != null) Execute(p, true); else Say("The die landed on an empty seat.");
                        break;
                    }
                case ActionEffect.DieRevealOrExecute:
                    {
                        var p = RollSeat();
                        if (p == null) Say("The die landed on an empty seat.");
                        else if (!p.Revealed) Reveal(p);
                        else Execute(p, true);
                        break;
                    }
                case ActionEffect.MostCardsExecuted:
                    {
                        var p = UniqueByCount(advisors, true);
                        if (p != null) Execute(p, true); else Say("Tie for most cards. Nothing happens.");
                        break;
                    }

                case ActionEffect.ForceReveal:
                    await PickAndRevealAsync(tb, advisors.Where(a => !a.Revealed), "Force which advisor to reveal?");
                    break;
                case ActionEffect.TrustedAdvisorForcesReveal:
                    await PickAndRevealAsync(ta, advisors.Where(a => a != ta && !a.Revealed), "Force which advisor to reveal?");
                    break;
                case ActionEffect.LeastCardsReveals:
                    {
                        var p = UniqueByCount(advisors, false);
                        if (p != null && !p.Revealed) Reveal(p); else Say("Nothing happens.");
                        break;
                    }
                case ActionEffect.FaceUpNextRoundAndForceReveal:
                    nextFaceUp = true;
                    await PickAndRevealAsync(tb, advisors.Where(a => !a.Revealed), "Force which advisor to reveal?");
                    break;

                case ActionEffect.Peek3Distribute:
                    {
                        var top = DrawCards(3);
                        var ids = advisors.Select(a => a.Id).ToList();
                        foreach (var c in top)
                        {
                            if (ids.Count == 0) { Discard.Add(c); continue; }
                            ById(ids[await PickAsync(tb, "Give this card to which advisor?", ids, false)]).Hand.Add(c);
                        }
                        Say(tb.Name + " distributes " + top.Count + " cards.");
                        break;
                    }
                case ActionEffect.DieSeatDiscard2:
                    {
                        var p = RollSeat();
                        if (p == null) { Say("The die landed on an empty seat."); break; }
                        for (int i = 0; i < 2 && p.Hand.Count > 0; i++)
                        {
                            var c = p.Hand[await PickCardForAsync(p, "Discard a card", p.Hand.ToList(), ChoiceKind.DiscardCard)];
                            p.Hand.Remove(c);
                            Discard.Add(c);
                        }
                        Say(p.Name + " discards 2 cards.");
                        break;
                    }
                case ActionEffect.RedrawHand:
                    {
                        var p = await PickOneAsync(tb, advisors, "Which advisor redraws their hand?");
                        if (p == null) break;
                        int n = p.Hand.Count;
                        Discard.AddRange(p.Hand);
                        p.Hand.Clear();
                        DrawInto(p, n);
                        Say(p.Name + " discards their hand and redraws " + n + ".");
                        break;
                    }
                case ActionEffect.ShowHandDiscard1:
                    {
                        var p = await PickOneAsync(tb, advisors, "Which advisor shows their hand?");
                        if (p == null) break;
                        Say(p.Name + " shows their hand: " + string.Join(" / ", p.Hand.Select(c => c.Effect.ToString())));
                        if (p.Hand.Count > 0)
                        {
                            var c = p.Hand[await PickCardForAsync(p, "Secretly discard a card", p.Hand.ToList(), ChoiceKind.DiscardCard)];
                            p.Hand.Remove(c);
                            Discard.Add(c);
                        }
                        break;
                    }
                case ActionEffect.SwapHands:
                    {
                        var a = await PickOneAsync(tb, advisors, "Swap hands: first advisor");
                        if (a == null) break;
                        var b = await PickOneAsync(tb, advisors.Where(x => x != a), "Swap hands: second advisor");
                        if (b == null) break;
                        var tmp = new List<ActionCard>(a.Hand);
                        a.Hand.Clear(); a.Hand.AddRange(b.Hand);
                        b.Hand.Clear(); b.Hand.AddRange(tmp);
                        Say(a.Name + " and " + b.Name + " swap hands.");
                        break;
                    }
                case ActionEffect.Draw1:
                    {
                        var p = await PickOneAsync(tb, advisors, "Which advisor draws a card?");
                        if (p != null) DrawInto(p, 1);
                        break;
                    }
                case ActionEffect.LeastCardsDraw2:
                    {
                        var p = UniqueByCount(advisors, false);
                        if (p != null) DrawInto(p, 2); else Say("Tie for fewest cards. Nothing happens.");
                        break;
                    }
                case ActionEffect.RevealedDraw2:
                    foreach (var p in advisors.Where(a => a.Revealed)) DrawInto(p, 2);
                    break;

                case ActionEffect.DieNextTrustedAdvisor:
                    {
                        var p = RollSeat();
                        if (p != null && p != tb) { forcedTrustedId = p.Id; Say(p.Name + " will be next round's trusted advisor."); }
                        else Say("Nothing happens.");
                        break;
                    }
                case ActionEffect.NextTrustedMustReveal: nextTrustedMustReveal = true; break;
                case ActionEffect.FaceUpNextRound: nextFaceUp = true; break;
                case ActionEffect.EnactTwoNextRound: nextEnactTwo = true; break;
                case ActionEffect.AllAdviceToThronebearerNextRound: nextAllToThronebearer = true; break;

                case ActionEffect.DieSwapWithThronebearer:
                    {
                        var p = RollSeat();
                        if (p != null && p != tb) SwapSeats(tb, p); else Say("Nothing happens.");
                        break;
                    }
                case ActionEffect.ChooseSwapSeat:
                    {
                        var p = await PickOneAsync(tb, advisors, "Swap seats with which advisor?");
                        if (p != null) SwapSeats(tb, p);
                        break;
                    }
                case ActionEffect.SwapSeatAndReveal:
                    {
                        var p = await PickOneAsync(tb, advisors, "Swap seats with which advisor?");
                        if (p == null) break;
                        SwapSeats(tb, p);
                        Reveal(p);
                        break;
                    }
                case ActionEffect.KingSwapsWithThronebearer:
                    {
                        var king = Players.First(x => x.Role == Role.King);
                        if (king.Alive && king != tb) SwapSeats(king, tb); else Say("Nothing happens.");
                        break;
                    }
                case ActionEffect.RotateLeft:
                    {
                        var ring = AlivePlayers.ToList();
                        var seats = ring.Select(p => p.Seat).ToList();
                        for (int i = 0; i < ring.Count; i++) ring[i].Seat = seats[(i + 1) % ring.Count];
                        Say("Everyone rotates one seat.");
                        break;
                    }
            }
        }

        // ---------------------------------------------------------------- effect helpers

        private async Task ExecutePickAsync(Player actor, IEnumerable<Player> options, string prompt, bool revealRole)
        {
            var p = await PickOneAsync(actor, options, prompt);
            if (p != null) Execute(p, revealRole);
        }

        private async Task PickAndRevealAsync(Player actor, IEnumerable<Player> options, string prompt)
        {
            var p = await PickOneAsync(actor, options, prompt);
            if (p != null) Reveal(p);
        }

        private async Task<Player> PickOneAsync(Player actor, IEnumerable<Player> options, string prompt)
        {
            var list = options.ToList();
            if (list.Count == 0) { Say("No valid target."); return null; }
            var ids = list.Select(p => p.Id).ToList();
            return list[await PickAsync(actor, prompt, ids, true)];
        }

        private Player UniqueByCount(List<Player> advisors, bool most)
        {
            if (advisors.Count == 0) return null;
            int target = most ? advisors.Max(a => a.Hand.Count) : advisors.Min(a => a.Hand.Count);
            var hits = advisors.Where(a => a.Hand.Count == target).ToList();
            return hits.Count == 1 ? hits[0] : null;
        }

        /// <summary>The die has one face per seat. Returns the player there, or null for an empty seat.</summary>
        private Player RollSeat()
        {
            int seat = rng.Next(SeatCount);
            Say("The die rolls seat " + seat + ".");
            return PlayerAtSeat(seat);
        }

        private void SwapSeats(Player a, Player b)
        {
            int tmp = a.Seat; a.Seat = b.Seat; b.Seat = tmp;
            Say(a.Name + " and " + b.Name + " swap seats.");
        }

        private void Reveal(Player p)
        {
            if (p.Revealed) return;
            p.Revealed = true;
            Say(p.Name + " is revealed as " + p.Role + ".");
        }

        private void Execute(Player p, bool revealRole)
        {
            if (!p.Alive) return;
            if (p.Role == Role.King) { Say("The King cannot be executed."); return; }
            p.Alive = false;
            if (revealRole) p.Revealed = true;
            Discard.AddRange(p.Hand);
            p.Hand.Clear();
            Say(p.Name + " is executed" + (revealRole ? " (" + p.Role + ")." : "."));

            if (p.Role == Role.Jester)
            {
                End(Winner.Jester, "The Jester got themselves killed.");
                return;
            }
            var traitors = Players.Where(x => RoleInfo.FactionOf(x.Role) == Faction.Traitor).ToList();
            if (traitors.All(t => !t.Alive)) End(Winner.Loyalists, "Both traitors have been executed.");
        }

        /// <summary>If the throne's seat was emptied, the lowest-numbered surviving seat moves up.</summary>
        private void EnsureThrone()
        {
            if (Thronebearer != null) return;
            var next = AlivePlayers.FirstOrDefault();
            if (next != null) next.Seat = ThroneSeat;
        }

        private Player ById(int id) { return Players[id]; }

        // ---------------------------------------------------------------- cards and deck

        private void Discard1(ActionCard c)
        {
            inPlay.Remove(c);
            Discard.Add(c);
        }

        /// <summary>Unplayed/discarded cards are reshuffled into the deck when it runs out.</summary>
        private ActionCard DrawOne()
        {
            if (Deck.Count == 0 && Discard.Count > 0)
            {
                Deck.AddRange(Discard);
                Discard.Clear();
                Shuffle(Deck);
                Say("The deck is empty: the discard pile is reshuffled.");
            }
            if (Deck.Count == 0) return null;
            var c = Deck[Deck.Count - 1];
            Deck.RemoveAt(Deck.Count - 1);
            return c;
        }

        private List<ActionCard> DrawCards(int n)
        {
            var list = new List<ActionCard>();
            for (int i = 0; i < n; i++)
            {
                var c = DrawOne();
                if (c == null) break;
                list.Add(c);
            }
            return list;
        }

        private void DrawInto(Player p, int n) { p.Hand.AddRange(DrawCards(n)); }

        // ---------------------------------------------------------------- agent plumbing

        private async Task<int> PickAsync(Player actor, string prompt, IReadOnlyList<int> ids, bool required, ChoiceKind kind = ChoiceKind.Target)
        {
            int idx = await agents[actor.Id].PickPlayer(new Choice(actor.Id, prompt, kind), ids);
            return Math.Max(0, Math.Min(ids.Count - 1, idx));
        }

        private async Task<int> PickCardForAsync(Player actor, string prompt, IReadOnlyList<ActionCard> cards, ChoiceKind kind = ChoiceKind.DiscardCard, IReadOnlyList<string> notes = null)
        {
            int idx = await agents[actor.Id].PickCard(new Choice(actor.Id, prompt, kind, notes), cards);
            return Math.Max(0, Math.Min(cards.Count - 1, idx));
        }

        private Task<bool> AskAsync(Player actor, string prompt)
        {
            return agents[actor.Id].Confirm(new Choice(actor.Id, prompt, ChoiceKind.UseAbility));
        }

        /// <summary>Swap the controller of a seat (for example a bot takes over a disconnected player).</summary>
        public void SetAgent(int playerId, IPlayerAgent agent) { agents[playerId] = agent; }
    }
}
