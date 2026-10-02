using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

// Role win rates and estimated real-time length. Run: RulesSim report [gamesPerPlayerCount]
internal static class Report
{
    // Seconds at the table, per round. Fast = experienced group, Normal = talkative group, Slow = new players.
    private static readonly double[] Fast = { 45, 5, 10, 15, 10, 10 };   // discuss, pick TA, play cards, TA filters, king picks, resolve+refill
    private static readonly double[] Normal = { 90, 10, 20, 30, 20, 20 };
    private static readonly double[] Slow = { 150, 15, 30, 45, 30, 30 };
    private const double SetupSeconds = 180; // deal loyalty cards, explain, seat

    public static int Run(int games)
    {
        var roleWins = new Dictionary<Role, int>();
        var roleSeen = new Dictionary<Role, int>();
        var winners = new Dictionary<Winner, int>();
        var rounds = new List<int>();
        var perCount = new Dictionary<int, List<int>>();

        for (int players = Game.MinPlayers; players <= Game.MaxPlayers; players++)
        {
            perCount[players] = new List<int>();
            for (int g = 0; g < games; g++)
            {
                var agents = Enumerable.Range(0, players).Select(i => (IPlayerAgent)new RandomAgent(g * 100 + i + players * 1000000)).ToList();
                var game = new Game(players, g + players * 100000, agents);
                var result = game.Run();
                winners[result.Winner] = winners.ContainsKey(result.Winner) ? winners[result.Winner] + 1 : 1;
                rounds.Add(game.Round);
                perCount[players].Add(game.Round);
                foreach (var p in game.Players)
                {
                    roleSeen[p.Role] = roleSeen.ContainsKey(p.Role) ? roleSeen[p.Role] + 1 : 1;
                    if (game.DidWin(p)) roleWins[p.Role] = roleWins.ContainsKey(p.Role) ? roleWins[p.Role] + 1 : 1;
                }
            }
        }

        int brackets = Game.MaxPlayers - Game.MinPlayers + 1;
        int total = games * brackets;
        Console.WriteLine("Simulated " + total + " games (" + games + " each at " + Game.MinPlayers + " to " + Game.MaxPlayers + " players, random-choice bots)");
        Console.WriteLine();
        Console.WriteLine("TEAM RESULTS");
        foreach (var w in winners.OrderByDescending(k => k.Value))
            Console.WriteLine("  " + w.Key.ToString().PadRight(10) + w.Value.ToString().PadLeft(5) + "  " + Pct(w.Value, total));
        Console.WriteLine();
        Console.WriteLine("ROLE   (wins / games the role was in play; a role wins when its team wins)");
        foreach (var r in Enum.GetValues(typeof(Role)).Cast<Role>())
        {
            int seen = roleSeen.ContainsKey(r) ? roleSeen[r] : 0, wins = roleWins.ContainsKey(r) ? roleWins[r] : 0;
            Console.WriteLine("  " + r.ToString().PadRight(12) + wins.ToString().PadLeft(5) + " / " + seen.ToString().PadLeft(5) + "   " + Pct(wins, seen) + "   in play " + Pct(seen, total));
        }

        rounds.Sort();
        double avg = rounds.Average();
        Console.WriteLine();
        Console.WriteLine("ROUNDS PER GAME: mean " + avg.ToString("0.0") + ", median " + rounds[rounds.Count / 2] + ", 90th pct " + rounds[(int)(rounds.Count * 0.9)] + ", max " + rounds.Last());
        foreach (var kv in perCount) Console.WriteLine("  " + kv.Key + " players: mean " + kv.Value.Average().ToString("0.0") + " rounds");
        Console.WriteLine();
        Console.WriteLine("ESTIMATED PLAY TIME (setup " + SetupSeconds / 60 + " min + rounds x per-round time)");
        Print("Fast  ", Fast, rounds, avg);
        Print("Normal", Normal, rounds, avg);
        Print("Slow  ", Slow, rounds, avg);
        return 0;
    }

    private static void Print(string label, double[] perRound, List<int> rounds, double avg)
    {
        double round = perRound.Sum();
        Func<double, string> min = s => (s / 60).ToString("0").PadLeft(3) + " min";
        Console.WriteLine("  " + label + " (" + round + "s/round)  average " + min(SetupSeconds + avg * round) +
            "   median " + min(SetupSeconds + rounds[rounds.Count / 2] * round) + "   90th pct " + min(SetupSeconds + rounds[(int)(rounds.Count * 0.9)] * round));
    }

    private static string Pct(int a, int b) { return (b == 0 ? 0 : 100.0 * a / b).ToString("0.0").PadLeft(5) + "%"; }
}
