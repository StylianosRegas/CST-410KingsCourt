namespace KingsCourt.Core
{
    public enum CardCategory { Execute, Reveal, Cards, NextRound, Seats }

    /// <summary>Short title and colour group for each card, used by the card UI.</summary>
    public static class CardInfo
    {
        public static CardCategory Category(ActionEffect e)
        {
            switch (e)
            {
                case ActionEffect.ExecuteUnrevealed:
                case ActionEffect.ExecuteUnrevealedHidden:
                case ActionEffect.ExecuteAdvisor:
                case ActionEffect.DieExecute:
                case ActionEffect.DieRevealOrExecute:
                case ActionEffect.MostCardsExecuted:
                    return CardCategory.Execute;
                case ActionEffect.ForceReveal:
                case ActionEffect.TrustedAdvisorForcesReveal:
                case ActionEffect.LeastCardsReveals:
                case ActionEffect.FaceUpNextRoundAndForceReveal:
                    return CardCategory.Reveal;
                case ActionEffect.Peek3Distribute:
                case ActionEffect.DieSeatDiscard2:
                case ActionEffect.RedrawHand:
                case ActionEffect.ShowHandDiscard1:
                case ActionEffect.SwapHands:
                case ActionEffect.Draw1:
                case ActionEffect.LeastCardsDraw2:
                case ActionEffect.RevealedDraw2:
                    return CardCategory.Cards;
                case ActionEffect.DieNextTrustedAdvisor:
                case ActionEffect.NextTrustedMustReveal:
                case ActionEffect.FaceUpNextRound:
                case ActionEffect.EnactTwoNextRound:
                case ActionEffect.AllAdviceToThronebearerNextRound:
                case ActionEffect.BlindNextRound:
                    return CardCategory.NextRound;
                default:
                    return CardCategory.Seats;
            }
        }

        public static string Title(ActionEffect e)
        {
            switch (e)
            {
                case ActionEffect.ExecuteUnrevealed: return "Execute";
                case ActionEffect.ExecuteUnrevealedHidden: return "Silent Execution";
                case ActionEffect.ExecuteAdvisor: return "Execute Advisor";
                case ActionEffect.DieExecute: return "Fatal Roll";
                case ActionEffect.DieRevealOrExecute: return "Judgement Roll";
                case ActionEffect.MostCardsExecuted: return "Hoarder's End";
                case ActionEffect.ForceReveal: return "Unmask";
                case ActionEffect.TrustedAdvisorForcesReveal: return "Advisor's Demand";
                case ActionEffect.LeastCardsReveals: return "Empty Handed";
                case ActionEffect.FaceUpNextRoundAndForceReveal: return "Open Court";
                case ActionEffect.Peek3Distribute: return "Royal Gifts";
                case ActionEffect.DieSeatDiscard2: return "Lucky Loss";
                case ActionEffect.RedrawHand: return "New Hand";
                case ActionEffect.ShowHandDiscard1: return "Show Your Hand";
                case ActionEffect.SwapHands: return "Trade Places";
                case ActionEffect.Draw1: return "Draw One";
                case ActionEffect.LeastCardsDraw2: return "Charity";
                case ActionEffect.RevealedDraw2: return "Reward the Bold";
                case ActionEffect.DieNextTrustedAdvisor: return "Fate's Favourite";
                case ActionEffect.NextTrustedMustReveal: return "No Secrets";
                case ActionEffect.FaceUpNextRound: return "Cards Up";
                case ActionEffect.EnactTwoNextRound: return "Double Decree";
                case ActionEffect.AllAdviceToThronebearerNextRound: return "Straight to the King";
                case ActionEffect.BlindNextRound: return "Mead";
                case ActionEffect.DieSwapWithThronebearer: return "Throne Roulette";
                case ActionEffect.ChooseSwapSeat: return "Change Seats";
                case ActionEffect.SwapSeatAndReveal: return "Swap and Expose";
                case ActionEffect.KingSwapsWithThronebearer: return "Return of the King";
                case ActionEffect.RotateLeft: return "Musical Chairs";
                default: return e.ToString();
            }
        }
    }
}
