using System;
using System.Collections.Generic;
using System.Linq;

namespace KingsCourt.Core
{
    /// <summary>
    /// Build 9: the table's setup procedure as code. A player setting up the physical game follows numbered
    /// steps from a closed box to the first action; the Game constructor runs the same steps in the same
    /// order, one method per step, each named after the step it implements (Assets/Docs/SETUP_PROCEDURE.md
    /// lists them next to the physical step they mirror).
    ///
    /// Nothing here changes how a game plays: the steps are the old constructor body split up, and they
    /// consume the random number generator in exactly the same order (RulesSim "fingerprint" proves it).
    /// </summary>
    public sealed partial class Game
    {
        /// <summary>The steps that ran, in order, each as "N. what happened". A finished setup lists steps 1-8.</summary>
        public readonly List<string> SetupSteps = new List<string>();

        /// <summary>Step 1 - Choose the scenario. The scenario (player count, hand size, refill rule) decides what
        /// goes on the table; this checks it is a game the box can hold and settles the balance numbers.</summary>
        private BalanceConfig Step1_ChooseTheScenario(int playerCount, IList<IPlayerAgent> playerAgents, ref int handSize, BalanceConfig balance)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException("playerCount", "King's Court is for 5 to 10 players.");
            if (playerAgents == null || playerAgents.Count != playerCount)
                throw new ArgumentException("One agent per player is required.");
            var b = balance ?? BalanceConfig.Current;
            if (handSize <= 0) handSize = b.HandSize;   // 0 = the standard hand size from the config
            if (handSize < 1) throw new ArgumentOutOfRangeException("handSize", "A hand size below 1 leaves nobody anything to play.");
            SetupSteps.Add("1. Choose the scenario: " + playerCount + " players, hands of " + handSize + ".");
            return b;
        }

        /// <summary>Step 2 - Lay out the table: the throne at the head, one chair per other player. Returns the
        /// chair numbers (the throne is seat 0), in table order.</summary>
        private List<int> Step2_LayOutTheTable(int playerCount)
        {
            var chairs = Enumerable.Range(1, playerCount - 1).ToList();
            SetupSteps.Add("2. Lay out the table: the throne and " + chairs.Count + " chairs.");
            return chairs;
        }

        /// <summary>Step 3 - Deal the loyalty cards: one role card face down for every seat, by the odds table.</summary>
        private List<Role> Step3_DealTheLoyaltyCards(int playerCount)
        {
            var roles = DealRoles(playerCount);
            SetupSteps.Add("3. Deal the loyalty cards: " + roles.Count + " role cards, face down.");
            return roles;
        }

        /// <summary>Step 4 - Seat the players: the King takes the throne, everyone else draws a chair at random.</summary>
        private void Step4_SeatThePlayers(List<Role> roles, List<int> chairs)
        {
            var seats = chairs.OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < roles.Count; i++)
            {
                int seat = roles[i] == Role.King ? ThroneSeat : seats[0];
                if (roles[i] != Role.King) seats.RemoveAt(0);
                Players.Add(new Player(i, "Player " + (i + 1), roles[i], seat));
            }
            SetupSteps.Add("4. Seat the players: the King on the throne, " + (Players.Count - 1) + " others in chairs.");
        }

        /// <summary>Step 5 - Build the action deck: every action card, doubled for a large table.</summary>
        private List<ActionCard> Step5_BuildTheActionDeck(int playerCount)
        {
            var deck = ActionCatalog.BuildDeck(playerCount, Balance);
            SetupSteps.Add("5. Build the action deck: " + deck.Count + " cards.");
            return deck;
        }

        /// <summary>Step 6 - Shuffle the action deck.</summary>
        private void Step6_ShuffleTheActionDeck()
        {
            Shuffle(Deck);
            SetupSteps.Add("6. Shuffle the action deck.");
        }

        /// <summary>Step 7 - Deal the starting hands: every player draws up to the hand size, from the top.</summary>
        private void Step7_DealTheStartingHands()
        {
            foreach (var p in Players) DrawInto(p, HandSize);
            SetupSteps.Add("7. Deal the starting hands: " + HandSize + " cards each, " + Deck.Count + " left in the draw pile.");
        }

        /// <summary>Step 8 - Seat the King and open the first round. The table's first player is whoever sits on the
        /// throne (the King always does), so the first thronebearer is the King and the first action is theirs:
        /// naming a trusted advisor. There is nothing to decide here, which is why it is a check and not a draw.</summary>
        private void Step8_SeatTheKingAndOpenTheFirstRound()
        {
            var first = Thronebearer;
            if (first == null || first.Role != Role.King)
                throw new InvalidOperationException("Setup failed: the King is not on the throne.");
            SetupSteps.Add("8. Seat the King and open the first round: " + first.Name + " holds the throne and goes first.");
        }

        /// <summary>The parity check, in code: is this a position a player could start the first action from?
        /// Returns null when it is, otherwise the first thing that is wrong.</summary>
        public string FirstActionProblem()
        {
            if (SetupSteps.Count != 8) return "setup ran " + SetupSteps.Count + " steps, not 8";
            var tb = Thronebearer;
            if (tb == null || tb.Role != Role.King) return "the King is not the first thronebearer";
            if (Players.Select(p => p.Seat).Distinct().Count() != Players.Count) return "two players share a seat";
            if (Players.Any(p => p.Hand.Count != HandSize)) return "a hand is not " + HandSize + " cards";
            var all = Players.SelectMany(p => p.Hand).Concat(Deck).ToList();
            if (all.Select(c => c.Id).Distinct().Count() != all.Count) return "a card is in two places";
            if (Round != 0 || Result != null) return "the game is not at its start";
            return null;
        }
    }
}
