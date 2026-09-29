namespace KingsCourt.Core
{
    public enum Faction { Loyalist, Traitor, Chaos }

    public enum Role { King, Queen, Knight, Elder, CourtWizard, Prince, Nobleman, Jester, Beggar }

    public enum Winner { None, Loyalists, Traitors, Jester, Draw }

    public static class RoleInfo
    {
        public static Faction FactionOf(Role role)
        {
            switch (role)
            {
                case Role.Prince:
                case Role.Nobleman:
                    return Faction.Traitor;
                case Role.Jester:
                case Role.Beggar:
                    return Faction.Chaos;
                default:
                    return Faction.Loyalist;
            }
        }

        public static string AbilityText(Role role)
        {
            switch (role)
            {
                case Role.King: return "Divine Right: always revealed, starts as thronebearer, cannot be executed.";
                case Role.Queen: return "Power in Knowledge: when you are thronebearer, reveal an advisor's loyalty card (once).";
                case Role.Knight: return "A Knight's Duty: execute an unrevealed advisor; if they were a loyalist, you are executed too.";
                case Role.CourtWizard: return "Shadow Money Wizard Gang: an advisor must play their action card face up.";
                case Role.Elder: return "Wisdom of the Ancients: play 2 action cards instead of 1 (always active).";
                case Role.Jester: return "Clownin' Around: a chosen player discards a random card (every round). You win only by being voted out or killed.";
                case Role.Prince: return "Rightful Heir: swap places with the thronebearer.";
                case Role.Nobleman: return "Riches And Spoils: take 1 action card from each of 2 advisors.";
                case Role.Beggar: return "Filthy Beggar: loyalist while unrevealed, traitor once revealed.";
                default: return "";
            }
        }
    }
}
