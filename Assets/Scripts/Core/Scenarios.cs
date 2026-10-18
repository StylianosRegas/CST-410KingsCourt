using System;
using System.Linq;

namespace KingsCourt.Core
{
    /// <summary>
    /// Build 5: a scenario is a configuration, not a scene. One entry per scenario from the Index Cards
    /// Padlet's Scenarios section (team write-up, Oct 2026). Everything that varies between the five lives
    /// here; nothing scenario-specific is hardcoded anywhere else - Game reads HandSize/RefillAtEndOfTurn/
    /// ScenarioEndCheck off whatever ScenarioConfig built it.
    /// </summary>
    public sealed class ScenarioConfig
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int PlayerCount;
        public int RoundLimit;          // 0 = no cap beyond the engine's own safety limit
        public int HandSize = Game.DefaultHandSize;
        public bool RefillAtEndOfTurn = true;
        public string Experience;       // the one-word tone from the write-up (Cozy, Sprawling, Frantic, ...)
        public string LeadDial;         // the one knob that makes this scenario what it is

        /// <summary>Null = standard win conditions only (both traitors executed / Prince holds throne 3
        /// rounds / Jester executed). Non-null adds one more trigger on top of those, checked every round.</summary>
        public Func<Game, Winner?> EndCheck;
        public string EndCheckReason;

        public Game Start(System.Collections.Generic.IList<IPlayerAgent> agents, int seed)
        {
            var g = new Game(PlayerCount, seed, agents, HandSize, RefillAtEndOfTurn);
            if (EndCheck != null) { g.ScenarioEndCheck = EndCheck; g.ScenarioEndReason = EndCheckReason; }
            return g;
        }
    }

    public static class ScenarioCatalog
    {
        public static readonly ScenarioConfig[] All =
        {
            new ScenarioConfig
            {
                Id = "quiet-court",
                DisplayName = "The Quiet Court",
                Description = "5 players, 5 spaces. King, Queen, Knight, Prince, Nobleman - no chaos roles. " +
                    "Loyalists win when both traitors are executed; traitors win if one holds the throne for " +
                    "3 consecutive turns.",
                PlayerCount = 5,
                RoundLimit = 10,
                Experience = "Cozy",
                LeadDial = "Loyalists versus traitors only",
                EndCheck = null, // standard conditions already cover this scenario exactly
            },
            new ScenarioConfig
            {
                Id = "crowded-table",
                DisplayName = "The Crowded Table",
                Description = "8 players, every seat filled, the full role set dealt face down. Standard win " +
                    "conditions, including the Jester winning if executed.",
                PlayerCount = 8,
                RoundLimit = 0,
                Experience = "Sprawling",
                LeadDial = "Every seat filled",
                EndCheck = null,
            },
            new ScenarioConfig
            {
                Id = "falling-hourglass",
                DisplayName = "The Falling Hourglass",
                Description = "6 players. King, Queen, Court Wizard, Prince, Nobleman, Jester. A 24-card draw " +
                    "pile with no reshuffle - if it empties before both traitors are executed, the traitors win.",
                PlayerCount = 6,
                RoundLimit = 0,
                Experience = "Frantic",
                LeadDial = "Twenty-four-card draw pile, no reshuffle",
                EndCheck = g => g.Deck.Count == 0 ? Winner.Traitors : (Winner?)null,
                EndCheckReason = "The draw pile ran out before the traitors were caught.",
            },
            new ScenarioConfig
            {
                Id = "marked-court",
                DisplayName = "The Marked Court",
                Description = "7 players. King, Queen, Knight, Elder, Prince, Nobleman, Beggar. The Queen and " +
                    "Knight start already revealed. Standard win conditions, 12-round cap.",
                PlayerCount = 7,
                RoundLimit = 12,
                Experience = "Cerebral",
                LeadDial = "Two roles start revealed",
                EndCheck = null,
                // Pre-reveal is applied by the scenario runner right after Game construction (AdminReveal),
                // not by Game itself - revealing on turn zero isn't a round-check condition.
            },
            new ScenarioConfig
            {
                Id = "poisoned-crown",
                DisplayName = "The Poisoned Crown",
                Description = "6 players. King, Knight, Elder, Prince, Nobleman, Jester. 4 cards each, no " +
                    "refills at all - if every living player's hand is empty before both traitors are " +
                    "executed, the traitors win.",
                PlayerCount = 6,
                RoundLimit = 4,
                HandSize = 4,
                RefillAtEndOfTurn = false,
                Experience = "Cruel",
                LeadDial = "Four cards, no refills",
                EndCheck = g => g.AlivePlayers.All(p => p.Hand.Count == 0) ? Winner.Traitors : (Winner?)null,
                EndCheckReason = "Every hand ran dry before the traitors were caught.",
            },
        };

        public static ScenarioConfig Find(string id) { return All.FirstOrDefault(s => s.Id == id); }
    }
}
