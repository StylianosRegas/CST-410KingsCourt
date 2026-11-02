using System.Collections.Generic;

namespace KingsCourt.Core
{
    /// <summary>
    /// Build 7: every mechanically-checkable rule already enforced by the engine, given a short id so a
    /// refusal can point a playtester at exactly which rule they tripped. These aren't a new rulesheet -
    /// they're the legality checks SeatAgent.Submit and Game.cs's round loop already perform (verified by
    /// Tools/RulesSim's "legality" suite), now named instead of just a free-text reason string.
    /// </summary>
    public static class Rules
    {
        public const string TurnOrder = "R-01";
        public const string OneCardPerRound = "R-02";
        public const string ValidOption = "R-03";
        public const string NoBotOverride = "R-04";
        public const string NoRepeatTrustedAdvisor = "R-05";

        // Rules taken straight from the team's rulesheet (Kings Court Rules Sheet.pdf, Oct 29, 2026).
        // The section of the sheet each one comes from is in Text below.
        public const string FilterAtLeastThree = "R-06";
        public const string EnactFromPassedCards = "R-07";
        public const string SetupHandOfThree = "R-08";
        public const string OnlyKingStartsRevealed = "R-09";
        public const string ThroneAndAdvisorPlayNoCard = "R-10";
        public const string EmptyDrawPileReshuffles = "R-11";

        public static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { FilterAtLeastThree, "Rulesheet 'Short on 3 at filter?': the Trusted Advisor always chooses from at least 3 cards; top-deck one for each one missing." },
            { EnactFromPassedCards, "Rulesheet round step 4 (Enact): the thronebearer chooses from the cards the Trusted Advisor passed, normally 2 and never more than 3, unless a card effect sends all advice straight to the thronebearer. (The first draft of this rule said 'never more than 3' with no exception; the checker found 20 rounds that broke it, all caused by that card effect, so the rule now names the exception.)" },
            { SetupHandOfThree, "Rulesheet setup step 5: deal 3 action cards to each player." },
            { OnlyKingStartsRevealed, "Rulesheet setup step 3: the King alone is revealed right away, and takes the throne. Every other role stays hidden." },
            { ThroneAndAdvisorPlayNoCard, "Rulesheet round step 2 (Play): everyone else plays a card, so the thronebearer and the Trusted Advisor play none." },
            { EmptyDrawPileReshuffles, "Rulesheet 'Empty draw pile?': shuffle the discard back in; never stop to reset. No card is ever lost." },
            { TurnOrder, "A player may only answer the one decision currently open for their own seat." },
            { OneCardPerRound, "Each advisor may submit at most one action card per round (the Elder's second card is its own decision, not a second PlayCard)." },
            { ValidOption, "A submitted answer must be one of the options actually listed for that decision." },
            { NoBotOverride, "A bot-controlled seat cannot be answered by a direct human submission." },
            { NoRepeatTrustedAdvisor, "The Trusted Advisor cannot be the same seat as last round's, unless a card effect names them." },
        };

        /// <summary>Known rule the engine does NOT check, and why: "no out-of-game information" / "no
        /// collusion outside the table" is real table etiquette from the rulesheet, but it has no signal
        /// the engine can observe - there is no system for detecting what players say to each other outside
        /// their official in-game choices. Left unimplemented on purpose, per Build 7's brief.</summary>
        public const string UncheckableReason = "No out-of-game collusion (table etiquette, not a game-state check - the engine has no way to observe side-channel conversation).";
    }
}
