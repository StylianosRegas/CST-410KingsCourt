using System;
using System.Collections.Generic;
using System.Linq;

namespace KingsCourt.Core
{
    // Plain serializable shapes for sending a GameSnapshot to a phone. Public fields + arrays only, so
    // Unity's JsonUtility (or any JSON library) can carry them. -1 means "hidden" for roles.

    [Serializable] public class CardDto { public int id; public int effect; public string text; }

    [Serializable]
    public class PlayerDto
    {
        public int id; public string name; public int seat;
        public bool alive, revealed, isBot, isYou, isWinner;
        public int role = -1;
        public int handCount;
    }

    [Serializable]
    public class DecisionDto
    {
        public int id, actor, kind, type;
        public string prompt;
        public bool blind;
        public int[] playerIds = new int[0];
        public CardDto[] cards = new CardDto[0];
        public string[] notes = new string[0];
    }

    [Serializable]
    public class SnapshotDto
    {
        public int viewer, round, thronebearer = -1, trustedAdvisor = -1, deck, discard, seatCount;
        public PlayerDto[] players = new PlayerDto[0];
        public CardDto[] hand = new CardDto[0];
        public int yourRole = -1;
        public bool hasDecision;
        public DecisionDto decision = new DecisionDto();
        public int[] waitingOn = new int[0];
        public string[] log = new string[0];
        public bool hasResult;
        public int winner;
        public string reason;
        public int[] winnerIds = new int[0];
        public bool roundBlind;
        public bool hasLastEnacted;
        public CardDto lastEnacted = new CardDto();
        public int enactedSeq;
        public CardDto[] enactedHistory = new CardDto[0];
        public string shownHandOwner = "";
        public CardDto[] shownHandCards = new CardDto[0];
        public int shownHandSeq;
    }

    public static class Wire
    {
        public static SnapshotDto ToDto(GameSnapshot s)
        {
            var d = new SnapshotDto
            {
                viewer = s.ViewerId, round = s.Round, thronebearer = s.ThronebearerId, trustedAdvisor = s.TrustedAdvisorId,
                deck = s.DeckCount, discard = s.DiscardCount, seatCount = s.SeatCount,
                players = s.Players.Select(p => new PlayerDto
                {
                    id = p.Id, name = p.Name, seat = p.Seat, alive = p.Alive, revealed = p.Revealed, isBot = p.IsBot,
                    isYou = p.IsYou, isWinner = p.IsWinner, role = p.Role.HasValue ? (int)p.Role.Value : -1, handCount = p.HandCount,
                }).ToArray(),
                hand = s.YourHand.Select((Func<ActionCard, CardDto>)(s.RoundBlind ? BlindCard : Card)).ToArray(),
                yourRole = s.YourRole.HasValue ? (int)s.YourRole.Value : -1,
                waitingOn = s.WaitingOn.ToArray(),
                log = s.Log.ToArray(),
                hasResult = s.Result != null,
                winner = s.Result != null ? (int)s.Result.Winner : 0,
                reason = s.Result != null ? s.Result.Reason : "",
                winnerIds = s.WinnerIds.ToArray(),
                roundBlind = s.RoundBlind,
                hasLastEnacted = s.LastEnacted != null,
                lastEnacted = s.LastEnacted != null ? Card(s.LastEnacted) : new CardDto(),
                enactedSeq = s.EnactedSeq,
                enactedHistory = s.EnactedHistory.Select(Card).ToArray(),
                shownHandOwner = s.ShownHandOwner ?? "",
                shownHandCards = s.ShownHandCards.Select(Card).ToArray(),
                shownHandSeq = s.ShownHandSeq,
            };
            if (s.YourDecision != null)
            {
                var y = s.YourDecision;
                d.hasDecision = true;
                d.decision = new DecisionDto
                {
                    id = y.Id, actor = y.ActorId, kind = (int)y.Kind, type = (int)y.Type, prompt = y.Prompt, blind = y.Blind,
                    playerIds = y.PlayerIds.ToArray(),
                    cards = y.Cards.Select((Func<ActionCard, CardDto>)(y.Blind ? BlindCard : Card)).ToArray(),
                    notes = y.Notes.ToArray(),
                };
            }
            return d;
        }

        public static GameSnapshot FromDto(SnapshotDto d)
        {
            var s = new GameSnapshot
            {
                ViewerId = d.viewer, Round = d.round, ThronebearerId = d.thronebearer, TrustedAdvisorId = d.trustedAdvisor,
                DeckCount = d.deck, DiscardCount = d.discard, SeatCount = d.seatCount,
                YourHand = d.hand.Select(FromCard).ToList(),
                YourRole = d.yourRole >= 0 ? (Role?)(Role)d.yourRole : null,
                WaitingOn = d.waitingOn.ToList(),
                Log = d.log.ToList(),
                WinnerIds = d.winnerIds.ToList(),
                RoundBlind = d.roundBlind,
                LastEnacted = d.hasLastEnacted ? FromCard(d.lastEnacted) : null,
                EnactedSeq = d.enactedSeq,
                EnactedHistory = d.enactedHistory.Select(FromCard).ToList(),
                ShownHandOwner = d.shownHandOwner,
                ShownHandCards = d.shownHandCards.Select(FromCard).ToList(),
                ShownHandSeq = d.shownHandSeq,
            };
            s.Players = d.players.Select(p => new PlayerInfo
            {
                Id = p.id, Name = p.name, Seat = p.seat, Alive = p.alive, Revealed = p.revealed, IsBot = p.isBot,
                IsYou = p.isYou, IsWinner = p.isWinner, Role = p.role >= 0 ? (Role?)(Role)p.role : null, HandCount = p.handCount,
            }).ToList();
            if (d.hasResult) s.Result = new GameResult { Winner = (Winner)d.winner, Reason = d.reason };
            if (d.hasDecision)
            {
                var y = d.decision;
                s.YourDecision = new Decision
                {
                    Id = y.id, ActorId = y.actor, Kind = (ChoiceKind)y.kind, Type = (OptionType)y.type, Prompt = y.prompt, Blind = y.blind,
                    PlayerIds = y.playerIds.ToList(), Cards = y.cards.Select(FromCard).ToList(), Notes = y.notes.ToList(),
                };
            }
            return s;
        }

        private static CardDto Card(ActionCard c) { return new CardDto { id = c.Id, effect = (int)c.Effect, text = c.Text }; }
        /// <summary>Mead's blind round: send only the id (needed so a tapped position round-trips back
        /// correctly), never the effect or text — same "don't leak it over the wire" standard as hidden roles.</summary>
        private static CardDto BlindCard(ActionCard c) { return new CardDto { id = c.Id, effect = -1, text = "" }; }
        private static ActionCard FromCard(CardDto c) { return new ActionCard(c.id, (ActionEffect)c.effect, c.text); }
    }
}
