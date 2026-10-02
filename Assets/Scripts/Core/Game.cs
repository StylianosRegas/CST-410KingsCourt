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
        public const int MaxPlayers = 10;
        /// <summary>The Lovers win instantly once alive-count drops to this many, provided both are among them.</summary>
        public const int LoversWinAliveThreshold = 5;

        public readonly List<Player> Players = new List<Player>();
        public readonly List<ActionCard> Deck;
        public readonly List<ActionCard> Discard = new List<ActionCard>();
        public readonly List<string> Log = new List<string>();
        public int Round { get; private set; }
        /// <summary>Mead's effect: nobody may see any card's face this round, in their own hand or anywhere
        /// else - not just during the moment they're actively choosing.</summary>
        public bool RoundBlind { get { return roundBlind; } }
        /// <summary>The most recently enacted card, and a sequence number that bumps every time a new one
        /// is enacted (Double Decree enacts 2 in one round) - public once enacted, not hidden info.</summary>
        public ActionCard LastEnacted { get; private set; }
        public int EnactedSeq { get; private set; }
        /// <summary>The last 3 enacted cards, oldest first - public knowledge once enacted, same as LastEnacted.
        /// Deliberately carries no player attribution, just the cards themselves.</summary>
        public readonly List<ActionCard> EnactedHistory = new List<ActionCard>();
        /// <summary>Show Your Hand's reveal (public to everyone, unlike a normal hand) - whose hand, what was
        /// in it at the moment of reveal (before the forced discard), and a sequence number so the UI can
        /// pop up a "new" reveal without re-showing a stale one every snapshot.</summary>
        public string ShownHandOwner { get; private set; }
        public List<ActionCard> ShownHandCards { get; private set; } = new List<ActionCard>();
        public int ShownHandSeq { get; private set; }
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
        private bool nextTrustedMustReveal, nextFaceUp, nextEnactTwo, nextAllToThronebearer, nextBlind;
        // Effects active during the current round.
        private bool roundFaceUp, roundEnactTwo, roundAllToThronebearer, roundBlind;
        // Double Decree: the trusted advisor keeps 3 instead of 2 that round, so the king's pick of 2 (of 3)
        // is an actual choice instead of "both cards you were already given."
        private bool nextKeepThree, roundKeepThree;
        private readonly HashSet<int> roundFaceUpIds = new HashSet<int>();
        private int princeStreak;
        private int lastThronebearerId = -1;
        /// <summary>Medium's Commune With the Dead: set during this round's ability phase, consumed at this same round's end.</summary>
        private int pendingResurrectId = -1;
        /// <summary>Players the Medium brought back, and how many more full rounds they get to live before
        /// dying again, permanently. Decremented at the end of each round they get to actually play.</summary>
        private readonly Dictionary<int, int> borrowedLifeRoundsLeft = new Dictionary<int, int>();

        public Game(int playerCount, int seed, IList<IPlayerAgent> playerAgents)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException("playerCount", "King's Court is for 5 to 10 players.");
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

            Deck = ActionCatalog.BuildDeck(playerCount);
            Shuffle(Deck);
            foreach (var p in Players) DrawInto(p, HandSize);
            Say("The court is seated: " + playerCount + " players.");
        }

        // ---------------------------------------------------------------- setup

        private static readonly Role[] LoyalistRoles = { Role.Queen, Role.Knight, Role.Elder, Role.CourtWizard, Role.Medium };
        private static readonly Role[] ChaosRoles = { Role.Jester, Role.Beggar };
        private static readonly Role[] TraitorExtraRoles = { Role.Prophet };
        private static readonly Role[] ReaperRoles = { Role.Reaper };
        private static readonly Role[] LoversRoles = { Role.Lovers };

        /// <summary>
        /// King, Prince and Nobleman always play. Some player counts also guarantee one Chaos and/or one
        /// Loyalist role. Every remaining seat is drawn without replacement from five pools (Loyalist,
        /// Chaos, Prophet, Reaper, Lovers) by the odds below - a pool whose roles have run out, or whose
        /// gating condition isn't met this draw, drops to 0%.
        ///   Jester vs Beggar: whichever of the two gets dealt first (whether via the guaranteed Chaos slot
        ///     or a weighted Chaos draw) is a genuine 50/50 coin flip, and the two are mutually exclusive -
        ///     the Chaos pool is cleared the moment either is chosen, so a game never has both and Jester's
        ///     overall appearance rate is ~50% of Chaos-slot games, not the near-guaranteed rate it was
        ///     before (both used to end up dealt together once the pool got drawn from twice, which it
        ///     usually did at higher player counts).
        ///   Prophet: no guaranteed slot at any player count, flat 15% per draw whenever still undrawn.
        ///   Reaper: only ever eligible once a Queen has already been dealt this game (King and Prince are
        ///     always dealt, so in practice this just gates on the Queen showing up) - gets exactly ONE
        ///     real shot per game, at 8%, the moment it first becomes eligible (not re-rolled every
        ///     subsequent draw, or the odds would compound across a game's several draws).
        ///   Lovers: only at 7+ players and only while at least 2 seats remain undealt - also exactly ONE
        ///     real shot per game, at 10%, for the same reason. A single draw assigns it to BOTH of the
        ///     next two seats at once, not just one.
        ///   Whatever share is left after those three is split between Loyalist/Chaos by the existing
        ///   per-player-count ratio (5p: 70/30, 6p: 50/50, 7p+: 20/80).
        /// </summary>
        private List<Role> DealRoles(int n)
        {
            const double prophetChance = 0.15;
            const double reaperChance = 0.08;
            const double loversChance = 0.10;
            bool guaranteedChaos = n >= 6;
            bool guaranteedLoyalist = n >= 7;
            double loyalistShare = n == 5 ? 0.7 : n == 6 ? 0.5 : 0.2;
            bool reaperTried = false, loversTried = false;

            var roles = new List<Role> { Role.King, Role.Prince, Role.Nobleman };
            var loyalists = new List<Role>(LoyalistRoles);
            var chaos = new List<Role>(ChaosRoles);
            var traitorExtra = new List<Role>(TraitorExtraRoles);
            var reaperPool = new List<Role>(ReaperRoles);
            var loversPool = new List<Role>(LoversRoles);

            if (guaranteedChaos) { roles.Add(TakeRandom(chaos)); chaos.Clear(); }
            if (guaranteedLoyalist) roles.Add(TakeRandom(loyalists));

            int guard = 0;
            while (roles.Count < n)
            {
                if (++guard > 500) { FillRemainder(roles, n, loyalists, chaos); break; }   // defensive only; never expected to trigger for 5-10 players

                // Prophet, Reaper and Lovers are each their own independent roll against their literal
                // stated percentage, checked BEFORE the Loyalist/Chaos split - not folded into one shared
                // weighted lottery with it. (An earlier version shared one pie with Loyalist/Chaos, which
                // meant their real odds silently inflated whenever that pie shrank, e.g. once Chaos was
                // exhausted - Lovers' "10%" was landing at ~49% in practice. Each check here means exactly
                // what its percentage says.)
                if (traitorExtra.Count > 0 && rng.NextDouble() < prophetChance) { roles.Add(TakeRandom(traitorExtra)); continue; }

                bool queenDealt = roles.Contains(Role.Queen);
                bool reaperEligible = reaperPool.Count > 0 && queenDealt && !reaperTried;
                if (reaperEligible)
                {
                    reaperTried = true;   // this draw is the one shot, win or lose
                    if (rng.NextDouble() < reaperChance) { roles.Add(TakeRandom(reaperPool)); continue; }
                }

                bool roomForLovers = n >= 7 && n - roles.Count >= 2;
                bool loversEligible = loversPool.Count > 0 && roomForLovers && !loversTried;
                if (loversEligible)
                {
                    loversTried = true;
                    if (rng.NextDouble() < loversChance)
                    {
                        TakeRandom(loversPool);
                        roles.Add(Role.Lovers);
                        roles.Add(Role.Lovers);
                        continue;
                    }
                }

                bool loyalAvail = loyalists.Count > 0, chaosAvail = chaos.Count > 0;
                if (!loyalAvail && !chaosAvail) continue;   // nothing to fall back on yet; Prophet may still land on a later pass
                double wLoyal = loyalAvail ? loyalistShare : 0;
                double wChaos = chaosAvail ? (1 - loyalistShare) : 0;
                double roll = rng.NextDouble() * (wLoyal + wChaos);
                if (roll < wLoyal) roles.Add(TakeRandom(loyalists));
                else { roles.Add(TakeRandom(chaos)); chaos.Clear(); }   // Jester/Beggar are mutually exclusive per game
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

        /// <summary>Should be unreachable given the pool sizes at 5-10 players (always enough combined
        /// capacity to finish the deal well before the loop guard trips) - a defensive backstop only, so a
        /// future change to the pools can never hang or crash the deal instead of just dealing oddly.</summary>
        private void FillRemainder(List<Role> roles, int n, List<Role> loyalists, List<Role> chaos)
        {
            while (roles.Count < n)
            {
                if (loyalists.Count > 0) roles.Add(TakeRandom(loyalists));
                else if (chaos.Count > 0) roles.Add(TakeRandom(chaos));
                else roles.Add(Role.Beggar);
            }
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
        /// still hidden wins with them). A Traitors' win goes to Prince, Nobleman, the Prophet (if dealt) and a
        /// REVEALED Beggar. A Jester win goes to the Jester alone.
        /// </summary>
        public bool DidWin(Player p)
        {
            if (Result == null) return false;
            switch (Result.Winner)
            {
                case Winner.Loyalists: return p.EffectiveFaction == Faction.Loyalist;
                case Winner.Traitors: return p.EffectiveFaction == Faction.Traitor;
                case Winner.Jester: return p.Role == Role.Jester;
                case Winner.Reaper: return p.Role == Role.Reaper;
                case Winner.Lovers: return p.Role == Role.Lovers;
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

            // The Queen's ability refreshes each time she newly takes the throne (not once per game).
            var roundStartTb = Thronebearer;
            if (roundStartTb != null && roundStartTb.Role == Role.Queen && roundStartTb.Id != lastThronebearerId)
                roundStartTb.AbilityUsed = false;
            lastThronebearerId = roundStartTb != null ? roundStartTb.Id : -1;

            // Pending effects from last round become active now.
            roundFaceUp = nextFaceUp; roundEnactTwo = nextEnactTwo; roundAllToThronebearer = nextAllToThronebearer;
            roundKeepThree = nextKeepThree;
            roundBlind = nextBlind;
            bool mustReveal = nextTrustedMustReveal;
            int forced = forcedTrustedId;
            nextFaceUp = nextEnactTwo = nextAllToThronebearer = nextTrustedMustReveal = nextKeepThree = nextBlind = false;
            forcedTrustedId = -1;
            roundFaceUpIds.Clear();
            pendingResurrectId = -1;
            foreach (var a in agents) { var aware = a as IRoundAware; if (aware != null) aware.OnRoundStart(); }

            Say("--- Round " + Round + " --- thronebearer: " + Thronebearer.Name);

            await AbilityPhaseAsync();
            if (Result != null) return;
            EnsureThrone();
            var tb = Thronebearer;

            // 1. Thronebearer names the trusted advisor. Can't repeat last round's trusted advisor, unless
            //    they're sworn in by a "die decides next trusted advisor" card effect (forced != -1).
            var candidates = Advisors();
            if (candidates.Count == 0) { End(Winner.Draw, "No advisors left."); return; }
            int lastTrustedId = TrustedAdvisorId;
            Player ta = candidates.FirstOrDefault(p => p.Id == forced);
            if (ta == null)
            {
                var eligible = candidates.Where(p => p.Id != lastTrustedId).ToList();
                if (eligible.Count == 0) eligible = candidates;   // nobody else left to choose; allow a repeat
                var ids = eligible.Select(p => p.Id).ToList();
                ta = eligible[await PickAsync(tb, "Choose your trusted advisor", ids, true, ChoiceKind.TrustedAdvisor)];
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

            // With few players left (or hands drained by other effects), fewer than 3 cards can end up in
            // play, leaving the trusted advisor no real choice (or none at all) when filtering down. Keep
            // drawing from the top of the deck until there are 3 to choose from, same as a short-handed
            // round always has (stops early if the deck itself runs dry).
            if (!roundAllToThronebearer)
            {
                int drawn = 0;
                while (played.Count < 3)
                {
                    var topCard = DrawOne();
                    if (topCard == null) break;
                    inPlay.Add(topCard);
                    played.Add(new PlayedCard { Card = topCard, OwnerId = -1, FaceUp = false });
                    drawn++;
                }
                if (drawn > 0) Say("The draw pile tops up the choices to " + played.Count + " for " + ta.Name + ".");
            }
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
                int keep = roundKeepThree ? 3 : 2;
                while (forKing.Count < keep && pool.Count > 0)
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
                // Public once enacted - shown as an actual card on everyone's screen so nobody has to
                // read the log. EnactedSeq increments per card (Double Decree enacts 2 in the same round).
                LastEnacted = c;
                EnactedSeq++;
                EnactedHistory.Add(c);
                if (EnactedHistory.Count > 3) EnactedHistory.RemoveAt(0);
                NotifyChanged();
                await Beat(900);
                await ResolveAsync(c, tb, ta);
                Discard1(c);
                NotifyChanged();
                await Beat(700);
            }
            foreach (var c in forKing) Discard1(c.Card);
            if (Result != null) { NotifyChanged(); return; }

            // 5. Everyone draws back as many cards as they played this round (not a flat refill to 3 -
            //    hand sizes can now drift up or down from card effects instead of always snapping back).
            //    The thronebearer and trusted advisor played no card face-down, so they draw none here.
            var playCounts = played.Where(c => c.OwnerId >= 0).GroupBy(c => c.OwnerId).ToDictionary(g => g.Key, g => g.Count());
            foreach (var p in AlivePlayers)
            {
                int n;
                if (playCounts.TryGetValue(p.Id, out n) && n > 0) DrawInto(p, n);
            }

            // A player the Medium already brought back finishes one of their 2 borrowed rounds here. Done
            // before this round's new resurrection below, so a player revived just now doesn't get docked
            // a round they never actually got to play.
            foreach (var id in borrowedLifeRoundsLeft.Keys.ToList())
            {
                var p = ById(id);
                if (!p.Alive) { borrowedLifeRoundsLeft.Remove(id); continue; }
                int left = borrowedLifeRoundsLeft[id] - 1;
                if (left <= 0)
                {
                    borrowedLifeRoundsLeft.Remove(id);
                    Say(p.Name + "'s borrowed time runs out.");
                    Execute(p, false);
                }
                else borrowedLifeRoundsLeft[id] = left;
            }
            if (Result != null) { NotifyChanged(); return; }

            // Medium's Commune With the Dead: the chosen ally comes back now, alive for 3 rounds before
            // dying again, permanently. Coming back always reveals them, loyalist or not, whatever their
            // prior reveal state was - communing with the dead puts them in full view of the court.
            if (pendingResurrectId >= 0)
            {
                var ally = ById(pendingResurrectId);
                if (!ally.Alive)
                {
                    ally.Alive = true;
                    ally.Revealed = true;
                    ally.Seat = FindFreeSeat(ally.Seat);
                    DrawInto(ally, HandSize);
                    borrowedLifeRoundsLeft[ally.Id] = 3;
                    Say(ally.Name + " is brought back to life for 3 rounds, revealed as " + ally.Role + ".");
                }
                pendingResurrectId = -1;
            }

            EnsureThrone();
            var now = Thronebearer;
            princeStreak = (now != null && now.Role == Role.Prince) ? princeStreak + 1 : 0;
            if (princeStreak >= 4) End(Winner.Traitors, "The Prince held the throne for 4 consecutive rounds.");
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
            int count = p.Role == Role.Elder && p.Revealed ? 2 : 1;
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
                if (!p.Revealed && p.Role != Role.Medium) continue;   // abilities need a reveal, except the Medium
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

                    case Role.Prophet:
                        if (await AskAsync(p, "Use Diviner's Revelation?"))
                        {
                            forcedTrustedId = p.Id;
                            p.AbilityUsed = true;
                            Say(p.Name + " (Prophet) will be the next trusted advisor.");
                        }
                        break;

                    case Role.Reaper:
                        // Royal Execution only targets the Royal Family (King, Prince, Queen) - whichever
                        // of them are currently revealed and alive - and is only offered at all once at
                        // least one of them is revealed, not any revealed player in general.
                        var royalTargets = new[] { Role.King, Role.Prince, Role.Queen }
                            .Select(r => Players.FirstOrDefault(x => x.Role == r))
                            .Where(x => x != null && x.Alive && x.Revealed)
                            .Select(x => x.Id).ToList();
                        if (royalTargets.Count > 0 && await AskAsync(p, "Use Royal Execution?"))
                        {
                            var victim = ById(royalTargets[await PickAsync(p, "Execute which member of the Royal Family?", royalTargets, false)]);
                            p.AbilityUsed = true;
                            Say(p.Name + " (Reaper) uses Royal Execution on " + victim.Name + ".");
                            Execute(victim, true);
                        }
                        break;

                    case Role.Medium:
                        var deadIds = Players.Where(x => !x.Alive).Select(x => x.Id).ToList();
                        if (deadIds.Count > 0 && await AskAsync(p, "Use Commune With the Dead?"))
                        {
                            var ally = ById(deadIds[await PickAsync(p, "Who do you bring back for 2 rounds?", deadIds, false)]);
                            pendingResurrectId = ally.Id;
                            p.AbilityUsed = true;
                            Say(p.Name + " (Medium) brings " + ally.Name + " back for 2 rounds.");
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
                        var p = RollSeatRerollEmpty();
                        if (p != null) Execute(p, true); else Say("Every seat is empty. Nothing happens.");
                        break;
                    }
                case ActionEffect.DieRevealOrExecute:
                    {
                        var p = RollSeatRerollEmpty();
                        if (p == null) Say("Every seat is empty. Nothing happens.");
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
                            string prompt = "Give \"" + CardInfo.Title(c.Effect) + "\" to which advisor?";
                            ById(ids[await PickAsync(tb, prompt, ids, false)]).Hand.Add(c);
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
                        Say(p.Name + " shows their hand: " + string.Join(" / ", p.Hand.Select(c => CardInfo.Title(c.Effect))));
                        // Public to everyone, like an enacted card - not just a log line, so it's actually
                        // seen rather than scrolling past unnoticed.
                        ShownHandOwner = p.Name;
                        ShownHandCards = new List<ActionCard>(p.Hand);
                        ShownHandSeq++;
                        NotifyChanged();
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
                case ActionEffect.EnactTwoNextRound: nextEnactTwo = true; nextKeepThree = true; break;
                case ActionEffect.AllAdviceToThronebearerNextRound: nextAllToThronebearer = true; break;
                case ActionEffect.BlindNextRound: nextBlind = true; break;

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

        /// <summary>For "rolling for a kill" cards (Fatal Roll, Judgement Roll): an empty seat rerolls
        /// instead of just doing nothing. Capped so an all-empty table (shouldn't happen; the game ends
        /// once fewer than 3 players are left) can't loop forever.</summary>
        private Player RollSeatRerollEmpty()
        {
            for (int i = 0; i < SeatCount * 4; i++)
            {
                var p = RollSeat();
                if (p != null) return p;
                Say("Empty seat, rolling again.");
            }
            return null;
        }

        /// <summary>A seat number that nobody currently alive holds - prefers the given one, since
        /// EnsureThrone() can leave a dead player's old seat number stale and reassigned to whoever
        /// moved into the empty throne while they were gone.</summary>
        private int FindFreeSeat(int preferred)
        {
            var occupied = new HashSet<int>(AlivePlayers.Select(p => p.Seat));
            if (!occupied.Contains(preferred)) return preferred;
            for (int s = 0; s < SeatCount; s++) if (!occupied.Contains(s)) return s;
            return preferred;
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
            p.Alive = false;
            if (revealRole) p.Revealed = true;
            Discard.AddRange(p.Hand);
            p.Hand.Clear();
            borrowedLifeRoundsLeft.Remove(p.Id);   // however they died, their borrowed time (if any) is over
            Say(p.Name + " is executed" + (revealRole ? " (" + p.Role + ")." : "."));

            if (p.Role == Role.Jester)
            {
                End(Winner.Jester, "The Jester got themselves killed.");
                return;
            }

            // Lovers: killing one half kills the other too, the same way as any other execution (reveals
            // their role, still feeds every check below) - terminates after one hop since the partner's
            // own check below finds nobody else alive to chain into.
            if (p.Role == Role.Lovers)
            {
                var partner = Players.FirstOrDefault(x => x.Role == Role.Lovers && x != p && x.Alive);
                if (partner != null) Execute(partner, true);
            }
            if (Result != null) return;

            var lovers = Players.Where(x => x.Role == Role.Lovers).ToList();
            if (lovers.Count == 2 && lovers.All(x => x.Alive) && AlivePlayers.Count() <= LoversWinAliveThreshold)
            {
                End(Winner.Lovers, "The Lovers made it to the final " + LoversWinAliveThreshold + " together.");
                return;
            }

            // Everyone currently Traitor-aligned must be executed: Prince, Nobleman, the Prophet (if
            // dealt), and a Beggar who has been revealed. A still-hidden Beggar is Loyalist-aligned and
            // isn't required — EffectiveFaction already encodes exactly that split.
            var traitors = Players.Where(x => x.EffectiveFaction == Faction.Traitor).ToList();
            if (traitors.Count > 0 && traitors.All(t => !t.Alive))
            {
                End(Winner.Loyalists, "All traitors have been executed.");
                return;
            }

            if (Players.Any(x => x.Role == Role.Reaper))
            {
                var royals = new[] { Role.King, Role.Prince, Role.Queen }.Select(r => Players.FirstOrDefault(x => x.Role == r)).ToList();
                if (royals.All(x => x != null && !x.Alive)) End(Winner.Reaper, "The King, Prince and Queen have all been executed.");
            }
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
            // Mead's blind round covers picking what to play, what the trusted advisor keeps, and what the
            // king enacts - not discards (ShowHandDiscard1 etc. already show the hand for a different reason).
            bool blind = roundBlind && (kind == ChoiceKind.PlayCard || kind == ChoiceKind.KeepCard || kind == ChoiceKind.EnactCard);
            int idx = await agents[actor.Id].PickCard(new Choice(actor.Id, prompt, kind, notes, blind), cards);
            return Math.Max(0, Math.Min(cards.Count - 1, idx));
        }

        private Task<bool> AskAsync(Player actor, string prompt)
        {
            return agents[actor.Id].Confirm(new Choice(actor.Id, prompt, ChoiceKind.UseAbility));
        }

        /// <summary>Swap the controller of a seat (for example a bot takes over a disconnected player).</summary>
        public void SetAgent(int playerId, IPlayerAgent agent) { agents[playerId] = agent; }

        // ---------------------------------------------------------------- admin cheats
        // Unlocked locally (10 discard-pile clicks in one round, see GameScreen) and only ever called by
        // whichever device is actually running this Game instance (the host, or an offline device) - a
        // joined phone has no direct access to a Game object to call these on in the first place.

        /// <summary>Reassigns a player's role mid-game. Does not re-validate the rest of the role set
        /// (duplicate/missing King etc. become the admin's problem, not the engine's).</summary>
        public void AdminSetRole(int playerId, Role role)
        {
            var p = ById(playerId);
            p.AdminSetRole(role);
            Say("[ADMIN] " + p.Name + "'s role is set to " + role + ".");
            NotifyChanged();
        }

        /// <summary>Force-reveals a player's loyalty card, bypassing the normal "an ability did it" path.</summary>
        public void AdminReveal(int playerId)
        {
            var p = ById(playerId);
            if (p.Revealed) return;
            p.Revealed = true;
            Say("[ADMIN] " + p.Name + " is revealed as " + p.Role + ".");
            NotifyChanged();
        }

        /// <summary>Force-submits an answer on behalf of whichever seat currently has a pending decision -
        /// reuses the normal SeatAgent.Submit path (so it's still logged and still index-bounds-checked),
        /// just called by the admin instead of that seat's own agent.</summary>
        public SubmitResult AdminForceSubmit(SeatAgent seat, int index)
        {
            if (seat.Pending == null) return SubmitResult.Reject("That seat has no open decision.");
            var result = seat.Submit(seat.Pending.Id, index);
            if (result.Ok) Say("[ADMIN] forced a decision for " + ById(seat.PlayerId).Name + ".");
            return result;
        }
    }
}
