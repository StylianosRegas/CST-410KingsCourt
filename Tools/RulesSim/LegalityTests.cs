using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

// Plays games with human seats driven by a script that keeps trying illegal moves. Run: RulesSim legality
internal static class LegalityTests
{
    public static int Run(int games)
    {
        int failures = 0, refusals = 0, accepted = 0, botTakeovers = 0;
        var reasons = new Dictionary<string, int>();
        Action<SubmitResult, bool, string> expect = (r, shouldAccept, what) =>
        {
            if (r.Ok != shouldAccept) { failures++; Console.WriteLine("FAIL: " + what + " -> ok=" + r.Ok + " " + r.Reason); }
            if (r.Ok) accepted++;
            else { refusals++; reasons[r.Reason] = reasons.ContainsKey(r.Reason) ? reasons[r.Reason] + 1 : 1; }
        };

        for (int g = 0; g < games; g++)
        {
            int players = 5 + g % 4;
            var rng = new Random(g);
            var seats = new List<SeatAgent>();
            // Seats 0 and 1 are humans, the rest bots.
            for (int i = 0; i < players; i++) seats.Add(new SeatAgent(i, i >= 2, g * 50 + i));
            var game = new Game(players, g, seats.Cast<IPlayerAgent>().ToList());
            var task = game.RunAsync();
            bool tookOver = false;
            int steps = 0;

            while (!task.IsCompleted)
            {
                if (++steps > 5000) { failures++; Console.WriteLine("FAIL: game did not finish, seed " + g); break; }
                var open = seats.Where(s => !s.IsBot && s.Pending != null).ToList();
                if (open.Count == 0) { failures++; Console.WriteLine("FAIL: deadlock, nobody has a decision, seed " + g); break; }
                var seat = open[0];
                var d = seat.Pending;

                // A human who is not the one being asked tries to answer: must be refused.
                var other = seats.FirstOrDefault(s => !s.IsBot && s != seat && s.Pending == null);
                if (other != null) expect(other.Submit(d.Id, 0), false, "answering when it is not your turn");

                // Bad answers to the real question.
                expect(seat.Submit(d.Id + 1000, 0), false, "stale decision id");
                expect(seat.Submit(d.Id, -1), false, "negative option");
                expect(seat.Submit(d.Id, d.OptionCount), false, "option out of range");

                // Occasionally a human drops and a bot takes the seat, whatever role they have.
                if (!tookOver && rng.Next(40) == 0) { seat.BecomeBot(); tookOver = true; botTakeovers++; continue; }

                var kind = d.Kind;
                expect(seat.Submit(d.Id, rng.Next(d.OptionCount)), true, "a legal answer");

                // Playing a card twice: right after a legal play the same seat may not play again.
                if (kind == ChoiceKind.PlayCard && seat.Pending == null && !seat.IsBot)
                {
                    var again = seat.Submit(d.Id, 0);
                    expect(again, false, "playing a second card");
                    if (!again.Ok && !again.Reason.Contains("already played") && !again.Reason.Contains("no longer open") && !again.Reason.Contains("isn't your turn"))
                    { failures++; Console.WriteLine("FAIL: unexpected refusal text: " + again.Reason); }
                }
            }
            if (task.IsCompleted && game.Result == null) { failures++; Console.WriteLine("FAIL: finished without a result"); }
            if (game.CountCards() != ActionCatalog.TotalCards) { failures++; Console.WriteLine("FAIL: card leak, seed " + g); }
            if (game.Players.First(p => p.Role == Role.King).Alive == false) { failures++; Console.WriteLine("FAIL: King died, seed " + g); }
        }

        Console.WriteLine("Legality: " + games + " games with 2 scripted humans. Accepted " + accepted + ", refused " + refusals + ", bot takeovers " + botTakeovers);
        foreach (var kv in reasons.OrderByDescending(k => k.Value)) Console.WriteLine("  refused x" + kv.Value + ": " + kv.Key);
        Console.WriteLine(failures == 0 ? "LEGALITY CHECKS PASSED" : failures + " LEGALITY FAILURES");
        return failures == 0 ? 0 : 1;
    }
}
