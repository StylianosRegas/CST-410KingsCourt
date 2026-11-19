namespace KingsCourt.Core
{
    public enum Faction { Loyalist, Traitor, Chaos }

    public enum Role { King, Queen, Knight, Elder, CourtWizard, Prince, Nobleman, Jester, Beggar, Prophet, Reaper, Lovers, Medium }

    public enum Winner { None, Loyalists, Traitors, Jester, Reaper, Lovers, Draw }

    public static class RoleInfo
    {
        public static Faction FactionOf(Role role)
        {
            switch (role)
            {
                case Role.Prince:
                case Role.Nobleman:
                case Role.Prophet:
                    return Faction.Traitor;
                case Role.Jester:
                case Role.Beggar:
                case Role.Reaper:
                case Role.Lovers:
                    return Faction.Chaos;
                default:
                    return Faction.Loyalist;
            }
        }

        /// <summary>role.ToString() is what every UI label used to show directly - fine for every role
        /// except CourtWizard, which has no internal space and so rendered as one run-together word.</summary>
        public static string DisplayName(Role role)
        {
            return role == Role.CourtWizard ? "Court Wizard" : role.ToString();
        }

        public static string AbilityText(Role role, BalanceConfig b = null)
        {
            b = b ?? BalanceConfig.Current;
            switch (role)
            {
                case Role.King: return "Divine Right: always revealed, starts as thronebearer. Otherwise just another Loyalist - he can be executed like anyone else.";
                case Role.Queen: return "Power in Knowledge: when you are thronebearer, reveal an advisor's loyalty card (once).";
                case Role.Knight: return "A Knight's Duty: execute an unrevealed advisor; if they were a loyalist, you are executed too.";
                case Role.CourtWizard: return "Shadow Money Wizard Gang: an advisor must play their action card face up.";
                case Role.Elder: return "Wisdom of the Ancients: play " + b.ElderCardsRevealed + " action cards instead of " + b.ElderCardsHidden + ", once revealed (" + b.ElderCardsHidden + " card while unrevealed).";
                case Role.Jester: return "Clownin' Around: a chosen player discards a random card (every round). You win only by being voted out or killed.";
                case Role.Prince: return "Rightful Heir: swap places with the thronebearer.";
                case Role.Nobleman: return "Riches And Spoils: take 1 action card from each of " + b.NoblemanTakes + " advisors.";
                case Role.Beggar: return "Filthy Beggar: loyalist while unrevealed, traitor once revealed.";
                case Role.Prophet: return "Diviner's Revelation: become the next trusted advisor.";
                case Role.Reaper: return "Royal Execution: execute a revealed member of the Royal Family - King, Prince or Queen (once; only usable once one of them is revealed). You win when the King, Prince and Queen have all been executed.";
                case Role.Lovers: return "Bound hearts: dealt to two players together. You win the instant " + b.LoversWinAliveThreshold + " or fewer players remain alive with both of you among them. If one of you is executed, the other is executed too.";
                case Role.Medium: return "Commune With the Dead: bring back any dead player at the end of the round (once), loyalist or not. They are revealed and live for " + b.MediumReviveRounds + " more rounds, then die again, permanently. Can be used while still unrevealed, without revealing yourself.";
                default: return "";
            }
        }
    }
}
