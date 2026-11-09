using System;
using System.Collections.Generic;

namespace KingsCourt.Core
{
    /// <summary>One entry of the deck list: how many copies of a card effect are in the action deck.</summary>
    [Serializable]
    public sealed class CardCount
    {
        public ActionEffect Effect;
        public int Count;
        public CardCount() { }
        public CardCount(ActionEffect effect, int count) { Effect = effect; Count = count; }
    }

    /// <summary>
    /// Build 8: every balance number in the game, in one place. The playtest changelog says what changed and
    /// why; this is where the change is made, so a retune is a value edit instead of a code change.
    /// In Unity this class is the payload of the BalanceAsset ScriptableObject (Assets/Resources/BalanceConfig.asset),
    /// which is what the game actually reads. <see cref="Factory"/> is the same set of values for builds
    /// that run without Unity (the RulesSim tool) and is what the asset was first generated from.
    /// Structural facts (the throne is seat 0, a game has 5 to 10 players) are not balance and stay in Game.
    /// Values that belong to one scenario (player count, round cap, refills) live on that scenario's ScenarioConfig.
    /// </summary>
    [Serializable]
    public sealed class BalanceConfig
    {
        // ---------------------------------------------------------------- hands
        /// <summary>Starting/refill hand size for a standard game. A scenario may override it (Poisoned Crown: 4).</summary>
        public int HandSize = 3;

        // ---------------------------------------------------------------- role odds (the deal)
        public double ProphetChance = 0.15;
        public double ReaperChance = 0.08;
        public double LoversChance = 0.10;
        /// <summary>From this many players on, one Chaos role (Jester or Beggar) is guaranteed.</summary>
        public int GuaranteedChaosMinPlayers = 6;
        /// <summary>From this many players on, one extra Loyalist role is guaranteed.</summary>
        public int GuaranteedLoyalistMinPlayers = 7;
        /// <summary>The Lovers can only be dealt from this many players on.</summary>
        public int LoversMinPlayers = 7;
        /// <summary>Share of the open seats that go to Loyalist rather than Chaos roles, by player count.</summary>
        public double LoyalistShare5Players = 0.7;
        public double LoyalistShare6Players = 0.5;
        public double LoyalistShare7PlusPlayers = 0.2;

        // ---------------------------------------------------------------- the round
        /// <summary>The trusted advisor chooses from at least this many cards; the draw pile tops short rounds up.</summary>
        public int FilterChoices = 3;
        /// <summary>Cards the trusted advisor passes to the king in a normal round.</summary>
        public int CardsKept = 2;
        /// <summary>Cards the trusted advisor keeps when Double Decree is active.</summary>
        public int CardsKeptDoubleDecree = 3;
        /// <summary>Cards the thronebearer enacts in a normal round.</summary>
        public int CardsEnacted = 1;
        /// <summary>Cards the thronebearer enacts when Double Decree is active.</summary>
        public int CardsEnactedDoubleDecree = 2;
        /// <summary>How many of the most recently enacted cards stay on display.</summary>
        public int EnactedHistoryShown = 3;
        /// <summary>The court cannot sit with fewer living players than this.</summary>
        public int MinPlayersAlive = 3;
        /// <summary>Safety limit on game length.</summary>
        public int MaxRounds = 200;

        // ---------------------------------------------------------------- victory targets
        /// <summary>Consecutive rounds the Prince must hold the throne for the traitors to win.</summary>
        public int PrinceThroneRoundsToWin = 4;
        /// <summary>The Lovers win once this many or fewer players are alive, both of them among them.</summary>
        public int LoversWinAliveThreshold = 5;

        // ---------------------------------------------------------------- role abilities
        /// <summary>Action cards an Elder plays per round once revealed.</summary>
        public int ElderCardsRevealed = 2;
        /// <summary>Action cards an Elder plays per round while unrevealed.</summary>
        public int ElderCardsHidden = 1;
        /// <summary>Advisors the Nobleman takes a card from.</summary>
        public int NoblemanTakes = 2;
        /// <summary>Rounds a player brought back by the Medium lives before dying again.</summary>
        public int MediumReviveRounds = 3;

        // ---------------------------------------------------------------- action card effects
        public int PeekCards = 3;                 // Peek3Distribute: cards the thronebearer looks at
        public int DieDiscardCards = 2;           // DieSeatDiscard2: cards the rolled seat discards
        public int DrawOneCards = 1;              // Draw1
        public int LeastCardsDrawCards = 2;       // LeastCardsDraw2
        public int RevealedDrawCards = 2;         // RevealedDraw2

        // ---------------------------------------------------------------- the die
        /// <summary>A kill roll that lands on an empty seat rerolls, at most this many times per seat at the table.</summary>
        public int DieRerollsPerSeat = 4;

        // ---------------------------------------------------------------- deck
        /// <summary>From this many players on, every card count is multiplied by <see cref="DeckMultiplierLargeTable"/>.</summary>
        public int LargeTableMinPlayers = 8;
        public int DeckMultiplierLargeTable = 2;
        /// <summary>Copies of each action card in the deck (before the large-table multiplier).</summary>
        public List<CardCount> DeckCounts = new List<CardCount>();

        public int DeckMultiplier(int playerCount) { return playerCount >= LargeTableMinPlayers ? DeckMultiplierLargeTable : 1; }

        public int CopiesOf(ActionEffect effect)
        {
            foreach (var c in DeckCounts) if (c.Effect == effect) return c.Count;
            return 0;
        }

        public double LoyalistShare(int playerCount)
        {
            return playerCount <= 5 ? LoyalistShare5Players : playerCount == 6 ? LoyalistShare6Players : LoyalistShare7PlusPlayers;
        }

        // ---------------------------------------------------------------- current / factory

        private static BalanceConfig current;

        /// <summary>The config new games use. Unity sets this from the BalanceAsset at startup; everything
        /// else (RulesSim, tests) gets <see cref="Factory"/>.</summary>
        public static BalanceConfig Current
        {
            get { return current ?? (current = Factory()); }
            set { current = value; }
        }

        public BalanceConfig Clone()
        {
            var c = (BalanceConfig)MemberwiseClone();
            c.DeckCounts = new List<CardCount>();
            foreach (var d in DeckCounts) c.DeckCounts.Add(new CardCount(d.Effect, d.Count));
            return c;
        }

        /// <summary>The values the team tuned the game to through Build 7. Generated into the asset once; after
        /// that the asset is the source of truth and this only backs the no-Unity tools.</summary>
        public static BalanceConfig Factory()
        {
            var b = new BalanceConfig();
            b.DeckCounts.AddRange(new[]
            {
                new CardCount(ActionEffect.ExecuteUnrevealed, 1),
                new CardCount(ActionEffect.ExecuteUnrevealedHidden, 1),
                new CardCount(ActionEffect.ExecuteAdvisor, 2),
                new CardCount(ActionEffect.DieExecute, 1),
                new CardCount(ActionEffect.DieRevealOrExecute, 2),
                new CardCount(ActionEffect.MostCardsExecuted, 1),

                new CardCount(ActionEffect.ForceReveal, 1),
                new CardCount(ActionEffect.TrustedAdvisorForcesReveal, 1),
                new CardCount(ActionEffect.LeastCardsReveals, 1),
                new CardCount(ActionEffect.FaceUpNextRoundAndForceReveal, 1),

                new CardCount(ActionEffect.Peek3Distribute, 1),
                new CardCount(ActionEffect.DieSeatDiscard2, 2),
                new CardCount(ActionEffect.RedrawHand, 2),
                new CardCount(ActionEffect.ShowHandDiscard1, 2),
                new CardCount(ActionEffect.SwapHands, 2),
                new CardCount(ActionEffect.Draw1, 2),
                new CardCount(ActionEffect.LeastCardsDraw2, 1),
                new CardCount(ActionEffect.RevealedDraw2, 2),

                new CardCount(ActionEffect.DieNextTrustedAdvisor, 2),
                new CardCount(ActionEffect.NextTrustedMustReveal, 1),
                new CardCount(ActionEffect.FaceUpNextRound, 1),
                new CardCount(ActionEffect.EnactTwoNextRound, 2),
                new CardCount(ActionEffect.AllAdviceToThronebearerNextRound, 1),
                new CardCount(ActionEffect.BlindNextRound, 2),

                new CardCount(ActionEffect.DieSwapWithThronebearer, 2),
                new CardCount(ActionEffect.ChooseSwapSeat, 4),
                new CardCount(ActionEffect.SwapSeatAndReveal, 1),
                new CardCount(ActionEffect.KingSwapsWithThronebearer, 2),
                new CardCount(ActionEffect.RotateLeft, 1),
            });
            return b;
        }
    }
}
