using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

/// <summary>
/// Build 7: scripts specific illegal moves and checks the refusal names the exact rule id that was
/// violated, so a playtester (or this test) can look it up on Assets/Scripts/Core/Rules.cs.
/// Run: dotnet run --project Tools/RulesSim -- rules
/// </summary>
internal static class RulesTest
{
    internal static int Run()
    {
        int failures = 0;
        const int players = 6;
        var seats = new List<SeatAgent>();
        for (int i = 0; i < players; i++) seats.Add(new SeatAgent(i, isBot: false, seed: i));
        var game = new Game(players, seed: 1, seats.Cast<IPlayerAgent>().ToList());

        Decision firstOpen = null;
        SeatAgent firstOpenSeat = null;
        foreach (var seat in seats)
        {
            seat.DecisionOpened += (s, d) => { if (firstOpen == null) { firstOpen = d; firstOpenSeat = s; } };
        }
        var playTask = game.PlayRoundAsync();

        // R-01 TurnOrder: submit to a seat whose decision isn't open.
        var idleSeat = seats.First(s => s != firstOpenSeat);
        var r1 = idleSeat.Submit(1, 0);
        Check(ref failures, "R-01 (wrong seat)", r1, Rules.TurnOrder);

        // R-03 ValidOption: an out-of-range index on the seat that IS open.
        var r3 = firstOpenSeat.Submit(firstOpen.Id, 9999);
        Check(ref failures, "R-03 (bad index)", r3, Rules.ValidOption);

        // R-01 again: a stale decision id on the open seat.
        var r1b = firstOpenSeat.Submit(firstOpen.Id + 500, 0);
        Check(ref failures, "R-01 (stale decision id)", r1b, Rules.TurnOrder);

        // Answer for real, then try to answer again this round -> R-02 once we can reach a PlayCard seat.
        firstOpenSeat.Submit(firstOpen.Id, 0);

        Console.WriteLine(failures == 0 ? "ALL RULE-ID CHECKS PASSED" : failures + " FAILURES");
        Console.WriteLine();
        failures += RulesheetChecks(120);
        Console.WriteLine();
        Console.WriteLine("Rules not mechanically checked: " + Rules.UncheckableReason);
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Build 7: rules R-06 to R-11 come straight from the team's rulesheet. They are checked as invariants
    /// across many full games played by scripted seats, because the engine builds the legal options and a
    /// player can't "attempt" these the way they can attempt an out-of-turn answer.
    /// </summary>
    private static int RulesheetChecks(int games)
    {
        int failures = 0;
        int keepChecks = 0, enactChecks = 0, playChecks = 0, reshuffles = 0, setupChecks = 0, filterSkipped = 0;
        Action<string, string> fail = (id, what) => { failures++; Console.WriteLine("FAIL [" + id + "] " + what); };

        for (int g = 0; g < games; g++)
        {
            int players = 5 + g % 6;
            var seats = new List<SeatAgent>();
            for (int i = 0; i < players; i++) seats.Add(new SeatAgent(i, false, g * 50 + i));
            var game = new Game(players, g, seats.Cast<IPlayerAgent>().ToList());

            // R-08 and R-09 are setup rules: check them before a single card is played.
            setupChecks++;
            foreach (var p in game.Players)
                if (p.Hand.Count != Game.DefaultHandSize) fail(Rules.SetupHandOfThree, "seed " + g + ": " + p.Name + " has " + p.Hand.Count + " cards");
            var revealed = game.Players.Where(p => p.Revealed).ToList();
            if (revealed.Count != 1 || revealed[0].Role != Role.King || revealed[0].Seat != Game.ThroneSeat)
                fail(Rules.OnlyKingStartsRevealed, "seed " + g + ": revealed at start = " + string.Join(", ", revealed.Select(p => p.ToString())));

            var rng = new Random(g);
            int keepRound = -1;
            var task = game.RunAsync();
            int steps = 0;
            while (!task.IsCompleted)
            {
                if (++steps > 8000) { fail(Rules.EmptyDrawPileReshuffles, "game did not finish, seed " + g); break; }
                var seat = seats.FirstOrDefault(s => s.Pending != null);
                if (seat == null) { fail(Rules.EmptyDrawPileReshuffles, "deadlock, seed " + g); break; }
                var d = seat.Pending;

                if (d.Kind == ChoiceKind.PlayCard)
                {
                    playChecks++;
                    var tb = game.Thronebearer;
                    if ((tb != null && seat.PlayerId == tb.Id) || seat.PlayerId == game.TrustedAdvisorId)
                        fail(Rules.ThroneAndAdvisorPlayNoCard, "seed " + g + " round " + game.Round + ": seat " + seat.PlayerId + " was asked to play a card");
                }
                else if (d.Kind == ChoiceKind.KeepCard && keepRound != game.Round)
                {
                    keepRound = game.Round;
                    keepChecks++;
                    // Fewer than 3 is only allowed when the draw and discard piles are both empty.
                    if (d.OptionCount < 3 && game.Deck.Count + game.Discard.Count > 0)
                        fail(Rules.FilterAtLeastThree, "seed " + g + " round " + game.Round + ": advisor offered " + d.OptionCount + " cards");
                }
                else if (d.Kind == ChoiceKind.EnactCard)
                {
                    enactChecks++;
                    // A card effect ("All advice goes straight to the thronebearer") can skip the filter on
                    // purpose, so the thronebearer legitimately sees every card that round. Anything else
                    // above 3 is a breach.
                    bool skippedFilter = game.Log.Skip(Math.Max(0, game.Log.Count - 4)).Any(l => l.Contains("All advice goes straight"));
                    if (skippedFilter) filterSkipped++;
                    else if (d.OptionCount > 3)
                        fail(Rules.EnactFromPassedCards, "seed " + g + " round " + game.Round + ": thronebearer offered " + d.OptionCount + " cards");
                }
                seat.Submit(d.Id, rng.Next(d.OptionCount));
            }

            if (game.Log.Any(l => l.Contains("reshuffled"))) reshuffles++;
            if (game.CountCards() != ActionCatalog.TotalCards(players))
                fail(Rules.EmptyDrawPileReshuffles, "seed " + g + ": card count changed (" + game.CountCards() + ")");
            if (game.Result == null) fail(Rules.EmptyDrawPileReshuffles, "seed " + g + ": finished without a result");
        }

        Console.WriteLine("Rulesheet rules R-06 to R-11 across " + games + " full games (5 to 10 players):");
        Console.WriteLine("  R-08 / R-09 setup checks .......... " + setupChecks);
        Console.WriteLine("  R-10 play decisions checked ....... " + playChecks);
        Console.WriteLine("  R-06 filter decisions checked ..... " + keepChecks);
        Console.WriteLine("  R-07 enact decisions checked ...... " + enactChecks + " (" + filterSkipped + " skipped the filter by a card effect, which is allowed)");
        Console.WriteLine("  R-11 games that reshuffled the discard: " + reshuffles + ", no card ever lost");
        Console.WriteLine(failures == 0 ? "ALL RULESHEET CHECKS PASSED" : failures + " RULESHEET FAILURES");
        return failures;
    }

    private static void Check(ref int failures, string what, SubmitResult r, string expectedRuleId)
    {
        string tag = "[" + expectedRuleId + "]";
        bool ok = !r.Ok && r.Reason != null && r.Reason.StartsWith(tag);
        Console.WriteLine((ok ? "ok  " : "FAIL") + " " + what + " -> " + r.Reason);
        if (!ok) failures++;
    }
}
