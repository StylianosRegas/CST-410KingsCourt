using System.Collections.Generic;

namespace KingsCourt.Core
{
    public enum ActionEffect
    {
        ExecuteUnrevealed,
        ExecuteUnrevealedHidden,
        ExecuteAdvisor,
        DieExecute,
        DieRevealOrExecute,
        MostCardsExecuted,

        ForceReveal,
        TrustedAdvisorForcesReveal,
        LeastCardsReveals,
        FaceUpNextRoundAndForceReveal,

        Peek3Distribute,
        DieSeatDiscard2,
        RedrawHand,
        ShowHandDiscard1,
        SwapHands,
        Draw1,
        LeastCardsDraw2,
        RevealedDraw2,

        DieNextTrustedAdvisor,
        NextTrustedMustReveal,
        FaceUpNextRound,
        EnactTwoNextRound,
        AllAdviceToThronebearerNextRound,
        BlindNextRound,

        DieSwapWithThronebearer,
        ChooseSwapSeat,
        SwapSeatAndReveal,
        KingSwapsWithThronebearer,
        RotateLeft
    }

    public sealed class ActionCard
    {
        public readonly int Id;
        public readonly ActionEffect Effect;
        public readonly string Text;

        public ActionCard(int id, ActionEffect effect, string text)
        {
            Id = id;
            Effect = effect;
            Text = text;
        }

        public override string ToString() { return Text; }
    }

    /// <summary>
    /// Source of truth: "Action Card Index.docx" plus cards added after playtesting. Build 8: this list says
    /// what each card IS (effect and text); how many copies of it are in the deck lives in BalanceConfig.DeckCounts.
    /// The original index totalled 43 cards (the GDD said 50); "Mead" (x2) was added 2026-09-29 after
    /// playtesting feedback, bringing the deck to 45.
    /// </summary>
    public static class ActionCatalog
    {
        public struct Entry
        {
            public ActionEffect Effect;
            public string Text;
            public Entry(ActionEffect e, string t) { Effect = e; Text = t; }
        }

        public static readonly Entry[] Entries =
        {
            new Entry(ActionEffect.ExecuteUnrevealed, "Execute an unrevealed player"),
            new Entry(ActionEffect.ExecuteUnrevealedHidden, "Execute an unrevealed player. Do not reveal their loyalty card"),
            new Entry(ActionEffect.ExecuteAdvisor, "Execute an advisor"),
            new Entry(ActionEffect.DieExecute, "Roll the die, the player in that seat is executed"),
            new Entry(ActionEffect.DieRevealOrExecute, "Roll the die: if it lands on an unrevealed advisor, reveal them; if on a revealed advisor, execute them"),
            new Entry(ActionEffect.MostCardsExecuted, "The advisor with the most cards in hand is executed (tie: nothing happens)"),

            new Entry(ActionEffect.ForceReveal, "The thronebearer forces an advisor to reveal their loyalty card"),
            new Entry(ActionEffect.TrustedAdvisorForcesReveal, "The trusted advisor forces another advisor to reveal themselves"),
            new Entry(ActionEffect.LeastCardsReveals, "The advisor with the fewest cards reveals themselves (tie or already revealed: nothing happens)"),
            new Entry(ActionEffect.FaceUpNextRoundAndForceReveal, "All advisors play their cards face up next round; the thronebearer forces an advisor to reveal their loyalty card"),

            new Entry(ActionEffect.Peek3Distribute, "The thronebearer looks at the top 3 cards of the deck, then distributes them to advisors as they wish"),
            new Entry(ActionEffect.DieSeatDiscard2, "Roll the die, that seat discards 2 cards"),
            new Entry(ActionEffect.RedrawHand, "Choose an advisor: they discard their entire hand, then draw that many cards"),
            new Entry(ActionEffect.ShowHandDiscard1, "Choose an advisor: they show their hand to the court, then secretly discard 1 card"),
            new Entry(ActionEffect.SwapHands, "Choose two advisors: they swap hands with each other"),
            new Entry(ActionEffect.Draw1, "Choose an advisor: they draw 1 card"),
            new Entry(ActionEffect.LeastCardsDraw2, "The advisor with the fewest cards draws 2 cards (tie: nothing happens)"),
            new Entry(ActionEffect.RevealedDraw2, "All revealed advisors draw 2 cards"),

            new Entry(ActionEffect.DieNextTrustedAdvisor, "Roll the die, that advisor is next round's trusted advisor"),
            new Entry(ActionEffect.NextTrustedMustReveal, "The next trusted advisor must reveal themselves"),
            new Entry(ActionEffect.FaceUpNextRound, "All advisors play their cards face up next round"),
            new Entry(ActionEffect.EnactTwoNextRound, "Next round the trusted advisor keeps 3 cards and the thronebearer enacts 2 of them"),
            new Entry(ActionEffect.AllAdviceToThronebearerNextRound, "Next round all advice cards are given to the thronebearer instead of the trusted advisor"),
            new Entry(ActionEffect.BlindNextRound, "Next round, nobody may look at any card's face - not your hand, not what's kept, not what's enacted"),

            new Entry(ActionEffect.DieSwapWithThronebearer, "Roll the die, the thronebearer swaps places with that seat"),
            new Entry(ActionEffect.ChooseSwapSeat, "The thronebearer chooses an advisor to swap seats with"),
            new Entry(ActionEffect.SwapSeatAndReveal, "The thronebearer swaps seats with an advisor, that advisor reveals themselves"),
            new Entry(ActionEffect.KingSwapsWithThronebearer, "The king swaps places with the thronebearer"),
            new Entry(ActionEffect.RotateLeft, "All players rotate one seat to the left"),
        };

        /// <summary>At large tables the deck is multiplied (BalanceConfig.LargeTableMinPlayers / DeckMultiplierLargeTable) so the table doesn't run dry round after round.</summary>
        public static int Multiplier(int playerCount, BalanceConfig balance = null) { return (balance ?? BalanceConfig.Current).DeckMultiplier(playerCount); }

        /// <summary>Copies of one card in the deck, before the large-table multiplier.</summary>
        public static int CountOf(ActionEffect effect, BalanceConfig balance = null) { return (balance ?? BalanceConfig.Current).CopiesOf(effect); }

        public static int TotalCards(int playerCount, BalanceConfig balance = null)
        {
            var b = balance ?? BalanceConfig.Current;
            int n = 0;
            foreach (var e in Entries) n += b.CopiesOf(e.Effect);
            return n * b.DeckMultiplier(playerCount);
        }

        public static List<ActionCard> BuildDeck(int playerCount, BalanceConfig balance = null)
        {
            var b = balance ?? BalanceConfig.Current;
            int mult = b.DeckMultiplier(playerCount);
            var deck = new List<ActionCard>();
            int id = 0;
            foreach (var e in Entries)
                for (int i = 0; i < b.CopiesOf(e.Effect) * mult; i++)
                    deck.Add(new ActionCard(id++, e.Effect, e.Text));
            return deck;
        }
    }
}
