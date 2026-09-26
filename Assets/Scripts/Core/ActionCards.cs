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
    /// Source of truth: "Action Card Index.docx". Duplicate cells in the index were merged into one
    /// entry with the summed count; (xN) markers are honoured. The index totals 43 cards, the GDD says 50.
    /// </summary>
    public static class ActionCatalog
    {
        public struct Entry
        {
            public ActionEffect Effect;
            public string Text;
            public int Count;
            public Entry(ActionEffect e, string t, int c) { Effect = e; Text = t; Count = c; }
        }

        public static readonly Entry[] Entries =
        {
            new Entry(ActionEffect.ExecuteUnrevealed, "Execute an unrevealed player", 1),
            new Entry(ActionEffect.ExecuteUnrevealedHidden, "Execute an unrevealed player. Do not reveal their loyalty card", 1),
            new Entry(ActionEffect.ExecuteAdvisor, "Execute an advisor", 2),
            new Entry(ActionEffect.DieExecute, "Roll the die, the player in that seat is executed", 1),
            new Entry(ActionEffect.DieRevealOrExecute, "Roll the die: if it lands on an unrevealed advisor, reveal them; if on a revealed advisor, execute them", 2),
            new Entry(ActionEffect.MostCardsExecuted, "The advisor with the most cards in hand is executed (tie: nothing happens)", 1),

            new Entry(ActionEffect.ForceReveal, "The thronebearer forces an advisor to reveal their loyalty card", 1),
            new Entry(ActionEffect.TrustedAdvisorForcesReveal, "The trusted advisor forces another advisor to reveal themselves", 1),
            new Entry(ActionEffect.LeastCardsReveals, "The advisor with the fewest cards reveals themselves (tie or already revealed: nothing happens)", 1),
            new Entry(ActionEffect.FaceUpNextRoundAndForceReveal, "All advisors play their cards face up next round; the thronebearer forces an advisor to reveal their loyalty card", 1),

            new Entry(ActionEffect.Peek3Distribute, "The thronebearer looks at the top 3 cards of the deck, then distributes them to advisors as they wish", 1),
            new Entry(ActionEffect.DieSeatDiscard2, "Roll the die, that seat discards 2 cards", 2),
            new Entry(ActionEffect.RedrawHand, "Choose an advisor: they discard their entire hand, then draw that many cards", 2),
            new Entry(ActionEffect.ShowHandDiscard1, "Choose an advisor: they show their hand to the court, then secretly discard 1 card", 2),
            new Entry(ActionEffect.SwapHands, "Choose two advisors: they swap hands with each other", 2),
            new Entry(ActionEffect.Draw1, "Choose an advisor: they draw 1 card", 2),
            new Entry(ActionEffect.LeastCardsDraw2, "The advisor with the fewest cards draws 2 cards (tie: nothing happens)", 1),
            new Entry(ActionEffect.RevealedDraw2, "All revealed advisors draw 2 cards", 2),

            new Entry(ActionEffect.DieNextTrustedAdvisor, "Roll the die, that advisor is next round's trusted advisor", 2),
            new Entry(ActionEffect.NextTrustedMustReveal, "The next trusted advisor must reveal themselves", 1),
            new Entry(ActionEffect.FaceUpNextRound, "All advisors play their cards face up next round", 1),
            new Entry(ActionEffect.EnactTwoNextRound, "Next round the thronebearer chooses 2 action cards", 2),
            new Entry(ActionEffect.AllAdviceToThronebearerNextRound, "Next round all advice cards are given to the thronebearer instead of the trusted advisor", 1),

            new Entry(ActionEffect.DieSwapWithThronebearer, "Roll the die, the thronebearer swaps places with that seat", 2),
            new Entry(ActionEffect.ChooseSwapSeat, "The thronebearer chooses an advisor to swap seats with", 4),
            new Entry(ActionEffect.SwapSeatAndReveal, "The thronebearer swaps seats with an advisor, that advisor reveals themselves", 1),
            new Entry(ActionEffect.KingSwapsWithThronebearer, "The king swaps places with the thronebearer", 2),
            new Entry(ActionEffect.RotateLeft, "All players rotate one seat to the left", 1),
        };

        public static int TotalCards
        {
            get
            {
                int n = 0;
                foreach (var e in Entries) n += e.Count;
                return n;
            }
        }

        public static List<ActionCard> BuildDeck()
        {
            var deck = new List<ActionCard>();
            int id = 0;
            foreach (var e in Entries)
                for (int i = 0; i < e.Count; i++)
                    deck.Add(new ActionCard(id++, e.Effect, e.Text));
            return deck;
        }
    }
}
