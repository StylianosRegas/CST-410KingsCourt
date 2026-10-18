using System.Collections.Generic;

namespace KingsCourt.Core
{
    /// <summary>
    /// The 10 verbs from the Oct 6 Verb Harvest doc (Verb_Harvest___Kings_Court.docx, Section 2 / Table 3),
    /// named exactly as the harvest named them. "Trade hands" survived the cut-test as a CUT-candidate
    /// (Section 4) but was kept in the deck for Week 12 rebalancing, so it stays in the enum.
    /// </summary>
    public enum Verb
    {
        PlayActionCard,
        NameTrustedAdvisor,
        FilterToTwo,
        EnactCard,
        Execute,
        Reveal,
        SwapSeats,
        DrawCards,
        DiscardCards,
        TradeHands,
    }

    /// <summary>
    /// The phase card from the Verb Harvest doc, Section 3. One entry per printed phase; AbilityEffect
    /// covers the sub-decisions a resolving card can ask for (target a player, confirm an ability) that
    /// happen inside Enact rather than as a phase of their own on the printed strip.
    /// </summary>
    public enum TurnPhase
    {
        NameAdvisor,
        SubmitCards,
        Filter,
        Enact,
        AbilityEffect,
        Refill,
    }

    /// <summary>One legal action for one player: what the engine's live Decision already allows them to
    /// pick, re-labelled with the harvested verb. Built from a SeatAgent's Pending Decision, so an illegal
    /// move is never in this list in the first place - it mirrors the same option set Submit() enforces.</summary>
    public readonly struct LegalMove
    {
        public readonly Verb Verb;
        public readonly int ActorId;
        public readonly int DecisionId;
        public readonly int OptionIndex;
        public readonly string Label;

        public LegalMove(Verb verb, int actorId, int decisionId, int optionIndex, string label)
        {
            Verb = verb;
            ActorId = actorId;
            DecisionId = decisionId;
            OptionIndex = optionIndex;
            Label = label;
        }

        public override string ToString() { return Verb + " #" + OptionIndex + " (" + Label + ")"; }
    }

    /// <summary>
    /// Reads the live engine's seat state and reports it as the Verb Harvest's phase and legal-move shape,
    /// without duplicating the round loop in Game.PlayRoundAsync. The seat whose Decision is open IS the
    /// state - this function doesn't infer anything Game.cs doesn't already know.
    /// </summary>
    public static class TurnMachine
    {
        /// <summary>Which seat (if any) currently has an open Decision, and what phase that Decision maps to.
        /// Returns null when nobody is waiting on anyone - i.e. between rounds, during Refill.</summary>
        public static TurnPhase? CurrentPhase(IList<SeatAgent> seats)
        {
            foreach (var seat in seats)
            {
                var d = seat.Pending;
                if (d == null) continue;
                return PhaseFor(d.Kind);
            }
            return null;
        }

        private static TurnPhase PhaseFor(ChoiceKind kind)
        {
            switch (kind)
            {
                case ChoiceKind.TrustedAdvisor: return TurnPhase.NameAdvisor;
                case ChoiceKind.PlayCard: return TurnPhase.SubmitCards;
                case ChoiceKind.KeepCard: return TurnPhase.Filter;
                case ChoiceKind.EnactCard: return TurnPhase.Enact;
                default: return TurnPhase.AbilityEffect; // DiscardCard / UseAbility / Target: card-effect sub-decisions
            }
        }

        /// <summary>The legal moves open to one seat right now. Empty when that seat has no pending Decision -
        /// an absent move, never a present-then-rejected one, matching the Verb Harvest's "no 'and'" rule.</summary>
        public static List<LegalMove> For(SeatAgent seat)
        {
            var moves = new List<LegalMove>();
            var d = seat.Pending;
            if (d == null) return moves;
            var verb = VerbFor(d);
            for (int i = 0; i < d.OptionCount; i++)
                moves.Add(new LegalMove(verb, seat.PlayerId, d.Id, i, OptionLabel(d, i)));
            return moves;
        }

        private static Verb VerbFor(Decision d)
        {
            switch (d.Kind)
            {
                case ChoiceKind.TrustedAdvisor: return Verb.NameTrustedAdvisor;
                case ChoiceKind.PlayCard: return Verb.PlayActionCard;
                case ChoiceKind.KeepCard: return Verb.FilterToTwo;
                case ChoiceKind.EnactCard: return Verb.EnactCard;
                case ChoiceKind.DiscardCard: return Verb.DiscardCards;
                default: return VerbFromPrompt(d.Prompt); // UseAbility / Target: card text decides the verb
            }
        }

        /// <summary>UseAbility and Target decisions cover several verbs (Execute, Reveal, SwapSeats,
        /// DrawCards, TradeHands) through one ChoiceKind each, because Game.cs's PickAsync/Confirm helpers
        /// don't carry a verb - only a human-readable prompt. Known gap: this is a best-effort keyword read
        /// of that prompt, not a structural guarantee like the other five verbs above.</summary>
        private static Verb VerbFromPrompt(string prompt)
        {
            var p = prompt.ToLowerInvariant();
            if (p.Contains("execute")) return Verb.Execute;
            if (p.Contains("reveal")) return Verb.Reveal;
            if (p.Contains("swap")) return Verb.SwapSeats;
            if (p.Contains("draw")) return Verb.DrawCards;
            if (p.Contains("trade") || p.Contains("swap hand")) return Verb.TradeHands;
            if (p.Contains("discard")) return Verb.DiscardCards;
            return Verb.Execute; // conservative fallback - most untagged Target picks in Game.cs are execute-style
        }

        private static string OptionLabel(Decision d, int index)
        {
            switch (d.Type)
            {
                case OptionType.Player: return "player " + d.PlayerIds[index];
                case OptionType.Card: return d.Blind ? "card (hidden)" : d.Cards[index].Text;
                default: return index == 0 ? "yes" : "no";
            }
        }
    }
}
