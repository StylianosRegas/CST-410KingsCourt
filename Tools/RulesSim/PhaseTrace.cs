using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

/// <summary>
/// Build 4's driver: plays real rounds through the live engine with placeholder (random) decisions,
/// using the same SeatAgent/Decision/Submit path a network client uses, and prints TurnMachine's phase
/// and legal-move list at every decision point. Run: dotnet run --project Tools/RulesSim phasetrace
/// </summary>
internal static class PhaseTrace
{
    internal static int Run(int players, int rounds, int seed)
    {
        var seats = new List<SeatAgent>();
        for (int i = 0; i < players; i++) seats.Add(new SeatAgent(i, isBot: false, seed: seed * 100 + i));
        var game = new Game(players, seed, seats.Cast<IPlayerAgent>().ToList());
        var rng = new Random(seed);

        int lastRoundSeen = -1;

        foreach (var seat in seats)
        {
            seat.DecisionOpened += (s, d) =>
            {
                if (game.Round != lastRoundSeen)
                {
                    lastRoundSeen = game.Round;
                    Console.WriteLine();
                    Console.WriteLine("=== Round " + game.Round + " (thronebearer: player " + (game.Thronebearer != null ? game.Thronebearer.Id : -1) + ") ===");
                }
                var phase = TurnMachine.CurrentPhase(seats);
                var moves = TurnMachine.For(s);
                Console.WriteLine("[" + phase + "] player " + s.PlayerId + " \"" + d.Prompt + "\" -> " + moves.Count + " legal move(s):");
                foreach (var m in moves) Console.WriteLine("    " + m);

                int pick = rng.Next(d.OptionCount);
                var result = s.Submit(d.Id, pick);
                if (!result.Ok) { Console.WriteLine("    REJECTED: " + result.Reason); throw new Exception("Submit was rejected for a move TurnMachine listed as legal."); }
            };
        }

        game.Logged += line => { if (line.Contains("wins") || line.Contains("Round ")) Console.WriteLine("  log: " + line); };

        while (game.Result == null && game.Round <= rounds)
        {
            int before = game.Round;
            game.PlayRound();
            if (game.Round == before && game.Result == null) break; // safety net, should not trigger
        }

        string outcome = game.Result != null ? game.Result.Winner.ToString() : "(still running, hit round cap)";
        Console.WriteLine();
        Console.WriteLine("Completed " + Math.Min(game.Round, rounds) + " round(s), result: " + outcome);
        Console.WriteLine("No errors.");
        return 0;
    }
}
