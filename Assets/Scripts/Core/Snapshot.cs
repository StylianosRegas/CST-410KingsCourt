using System.Collections.Generic;
using System.Linq;

namespace KingsCourt.Core
{
    /// <summary>What one player is allowed to know about another player.</summary>
    public sealed class PlayerInfo
    {
        public int Id;
        public string Name;
        public int Seat;
        public bool Alive;
        public bool Revealed;
        public bool IsBot;
        public bool IsYou;
        public bool IsWinner;
        /// <summary>Null when this player's role is hidden from the viewer.</summary>
        public Role? Role;
        public int HandCount;
        public bool HasRoleVisible { get { return Role.HasValue; } }
    }

    /// <summary>
    /// A view of the game filtered for one viewer. A network host would send one of these to each phone, so
    /// nobody ever receives another player's hidden role or hand.
    /// </summary>
    public sealed class GameSnapshot
    {
        public int ViewerId;                       // -1 = spectator
        public int Round;
        public int ThronebearerId = -1;
        public int TrustedAdvisorId = -1;
        public int DeckCount;
        public int DiscardCount;
        public int SeatCount;
        public List<PlayerInfo> Players = new List<PlayerInfo>();
        public List<ActionCard> YourHand = new List<ActionCard>();
        public Role? YourRole;
        public Decision YourDecision;
        public List<int> WaitingOn = new List<int>();
        public List<string> Log = new List<string>();
        public GameResult Result;
        public List<int> WinnerIds = new List<int>();

        public PlayerInfo Find(int id) { return Players.FirstOrDefault(p => p.Id == id); }

        /// <param name="seats">Human/bot seats (optional), used for bot flags and pending decisions.</param>
        /// <param name="revealAll">Spectator/debug: show every role.</param>
        public static GameSnapshot For(Game g, int viewerId, IList<SeatAgent> seats = null, bool revealAll = false, int logLines = 8)
        {
            var s = new GameSnapshot
            {
                ViewerId = viewerId,
                Round = g.Round,
                DeckCount = g.Deck.Count,
                DiscardCount = g.Discard.Count,
                SeatCount = g.SeatCount,
                TrustedAdvisorId = g.TrustedAdvisorId,
                Result = g.Result,
            };
            var tb = g.Thronebearer;
            s.ThronebearerId = tb != null ? tb.Id : -1;

            foreach (var p in g.Players)
            {
                bool mine = p.Id == viewerId;
                bool visible = revealAll || mine || p.Revealed;
                s.Players.Add(new PlayerInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Seat = p.Seat,
                    Alive = p.Alive,
                    Revealed = p.Revealed,
                    IsYou = mine,
                    IsBot = seats != null && p.Id < seats.Count && seats[p.Id].IsBot,
                    IsWinner = g.DidWin(p),
                    Role = visible ? (Role?)p.Role : null,
                    HandCount = p.Hand.Count,
                });
                if (mine) { s.YourHand = new List<ActionCard>(p.Hand); s.YourRole = p.Role; }
            }
            if (seats != null)
            {
                foreach (var seat in seats)
                {
                    if (seat.Pending == null) continue;
                    s.WaitingOn.Add(seat.PlayerId);
                    if (seat.PlayerId == viewerId) s.YourDecision = seat.Pending;
                }
            }
            s.Log = g.Log.Skip(System.Math.Max(0, g.Log.Count - logLines)).ToList();
            s.WinnerIds = g.Players.Where(p => g.DidWin(p)).Select(p => p.Id).ToList();
            return s;
        }
    }
}
