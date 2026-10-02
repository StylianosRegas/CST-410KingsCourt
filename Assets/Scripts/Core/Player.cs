using System.Collections.Generic;

namespace KingsCourt.Core
{
    public sealed class Player
    {
        public readonly int Id;
        public string Name { get; private set; }
        public Role Role { get; private set; }
        public int Seat;
        public bool Alive = true;
        public bool Revealed;
        public bool AbilityUsed;
        public readonly List<ActionCard> Hand = new List<ActionCard>();

        public Player(int id, string name, Role role, int seat)
        {
            Id = id;
            Name = name;
            Role = role;
            Seat = seat;
            Revealed = role == Role.King;
        }

        public void Rename(string name) { if (!string.IsNullOrEmpty(name)) Name = name; }

        /// <summary>Admin cheats only - reassigns a player's role mid-game.</summary>
        public void AdminSetRole(Role role) { Role = role; }

        /// <summary>Beggar is a loyalist until revealed, then a traitor.</summary>
        public Faction EffectiveFaction
        {
            get
            {
                if (Role == Role.Beggar) return Revealed ? Faction.Traitor : Faction.Loyalist;
                return RoleInfo.FactionOf(Role);
            }
        }

        public override string ToString() { return Name + " (" + Role + ")"; }
    }
}
