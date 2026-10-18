using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KingsCourt.Core
{
    public enum ChoiceKind { TrustedAdvisor, PlayCard, KeepCard, EnactCard, DiscardCard, UseAbility, Target }

    /// <summary>Describes a decision the engine needs from a player.</summary>
    public sealed class Choice
    {
        public readonly int ActorId;
        public readonly string Prompt;
        public readonly ChoiceKind Kind;
        /// <summary>Optional extra text per option (for example who played a face-up card).</summary>
        public readonly IReadOnlyList<string> Notes;
        /// <summary>Mead's effect: even the picker can't see these cards' faces while choosing (they pick
        /// a position, not a card). Whether the card list itself carries real data is unaffected — this is
        /// purely a display instruction for the UI.</summary>
        public readonly bool Blind;

        public Choice(int actorId, string prompt, ChoiceKind kind = ChoiceKind.Target, IReadOnlyList<string> notes = null, bool blind = false)
        {
            ActorId = actorId;
            Prompt = prompt;
            Kind = kind;
            Notes = notes;
            Blind = blind;
        }
    }

    /// <summary>
    /// Anything that can make decisions for a seat: a bot, a local UI, or a network client.
    /// Each method resolves to an INDEX into the options list. Tasks may complete immediately (bots)
    /// or later (a human). Nothing here uses threads, so it also works in a WebGL build.
    /// </summary>
    public interface IPlayerAgent
    {
        Task<int> PickPlayer(Choice choice, IReadOnlyList<int> playerIds);
        Task<int> PickCard(Choice choice, IReadOnlyList<ActionCard> cards);
        Task<bool> Confirm(Choice choice);
    }

    /// <summary>Optional: agents that want to know when a new round begins.</summary>
    public interface IRoundAware
    {
        void OnRoundStart();
    }

    public sealed class RandomAgent : IPlayerAgent
    {
        private readonly Random rng;
        private readonly double confirmChance;

        public RandomAgent(int seed, double confirmChance = 0.5)
        {
            rng = new Random(seed);
            this.confirmChance = confirmChance;
        }

        public int PickIndex(int count) { return rng.Next(count); }
        public bool PickConfirm() { return rng.NextDouble() < confirmChance; }

        public Task<int> PickPlayer(Choice choice, IReadOnlyList<int> playerIds) { return Task.FromResult(rng.Next(playerIds.Count)); }
        public Task<int> PickCard(Choice choice, IReadOnlyList<ActionCard> cards) { return Task.FromResult(rng.Next(cards.Count)); }
        public Task<bool> Confirm(Choice choice) { return Task.FromResult(rng.NextDouble() < confirmChance); }
    }

    // ------------------------------------------------------------------ human seats

    public enum OptionType { Player, Card, YesNo }

    /// <summary>One question waiting on one human player. This is what the UI (or a network message) shows.</summary>
    public sealed class Decision
    {
        public int Id;
        public int ActorId;
        public ChoiceKind Kind;
        public string Prompt;
        public OptionType Type;
        public List<int> PlayerIds = new List<int>();
        public List<ActionCard> Cards = new List<ActionCard>();
        public List<string> Notes = new List<string>();
        public bool Blind;

        public int OptionCount
        {
            get { return Type == OptionType.Player ? PlayerIds.Count : Type == OptionType.Card ? Cards.Count : 2; }
        }
    }

    public struct SubmitResult
    {
        public bool Ok;
        public string Reason;
        public static SubmitResult Accept() { return new SubmitResult { Ok = true }; }
        public static SubmitResult Reject(string reason) { return new SubmitResult { Ok = false, Reason = reason }; }
    }

    /// <summary>
    /// A seat at the table. Starts as a human or a bot and can switch to a bot at any time (a bot takes over
    /// a player who disconnects, whatever their role). The host calls Submit for every answer a player sends;
    /// anything that is not the one open decision for that seat is refused.
    /// </summary>
    public sealed class SeatAgent : IPlayerAgent, IRoundAware
    {
        private static int nextDecisionId = 1;

        public readonly int PlayerId;
        public bool IsBot { get; private set; }
        public Decision Pending { get; private set; }

        public event Action<SeatAgent, Decision> DecisionOpened;
        public event Action<SeatAgent> DecisionClosed;

        private readonly RandomAgent bot;
        private TaskCompletionSource<int> waiting;
        private bool playedThisRound;

        public SeatAgent(int playerId, bool isBot, int seed)
        {
            PlayerId = playerId;
            IsBot = isBot;
            bot = new RandomAgent(seed);
        }

        public void OnRoundStart() { playedThisRound = false; }

        // ---- engine side

        public Task<int> PickPlayer(Choice choice, IReadOnlyList<int> playerIds)
        {
            if (IsBot) return Task.FromResult(bot.PickIndex(playerIds.Count));
            var d = NewDecision(choice, OptionType.Player);
            d.PlayerIds.AddRange(playerIds);
            return Open(d);
        }

        public Task<int> PickCard(Choice choice, IReadOnlyList<ActionCard> cards)
        {
            if (IsBot) return Task.FromResult(bot.PickIndex(cards.Count));
            var d = NewDecision(choice, OptionType.Card);
            d.Cards.AddRange(cards);
            return Open(d);
        }

        public async Task<bool> Confirm(Choice choice)
        {
            if (IsBot) return bot.PickConfirm();
            var d = NewDecision(choice, OptionType.YesNo);
            return await Open(d) == 0;
        }

        private Decision NewDecision(Choice choice, OptionType type)
        {
            var d = new Decision { Id = nextDecisionId++, ActorId = PlayerId, Kind = choice.Kind, Prompt = choice.Prompt, Type = type, Blind = choice.Blind };
            if (choice.Notes != null) d.Notes.AddRange(choice.Notes);
            return d;
        }

        private Task<int> Open(Decision d)
        {
            waiting = new TaskCompletionSource<int>();
            var task = waiting.Task; // captured before the event fires: a synchronous reentrant Submit()
                                      // from inside DecisionOpened (e.g. a driver answering its own prompt)
                                      // already nulls out the `waiting` field by the time this method resumes.
            Pending = d;
            if (DecisionOpened != null) DecisionOpened(this, d);
            return task;
        }

        // ---- player side

        /// <summary>The single entry point for a human answer. Returns why if it is refused.</summary>
        public SubmitResult Submit(int decisionId, int index)
        {
            var d = Pending;
            if (d == null)
            {
                if (IsBot) return SubmitResult.Reject("A bot is playing this seat.");
                return SubmitResult.Reject(playedThisRound ? "You already played your card this round." : "It isn't your turn.");
            }
            if (decisionId != d.Id) return SubmitResult.Reject("That choice is no longer open.");
            if (index < 0 || index >= d.OptionCount) return SubmitResult.Reject("That isn't a valid option.");

            if (d.Kind == ChoiceKind.PlayCard) playedThisRound = true;
            Close();
            var tcs = waiting;
            waiting = null;
            tcs.SetResult(index);   // the game may continue (and open the next decision) inside this call
            return SubmitResult.Accept();
        }

        /// <summary>Hand a bot seat to a human (used when a phone joins before the game starts).</summary>
        public void BecomeHuman() { IsBot = false; }

        /// <summary>A bot takes over this seat, answering any open question on the player's behalf.</summary>
        public void BecomeBot()
        {
            if (IsBot) return;
            IsBot = true;
            var d = Pending;
            if (d == null) return;
            Close();
            var tcs = waiting;
            waiting = null;
            tcs.SetResult(bot.PickIndex(d.OptionCount));
        }

        private void Close()
        {
            Pending = null;
            if (DecisionClosed != null) DecisionClosed(this);
        }
    }
}
