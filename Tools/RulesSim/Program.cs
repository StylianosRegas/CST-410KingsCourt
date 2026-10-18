using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

// Plays many bot-only games and checks engine invariants. Run: dotnet run --project Tools/RulesSim
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "scenarios") return ScenarioTest.Run(args.Length > 1 ? int.Parse(args[1]) : 500);
        if (args.Length > 0 && args[0] == "phasetrace")
        {
            int tracePlayers = args.Length > 1 ? int.Parse(args[1]) : 5;
            int traceRounds = args.Length > 2 ? int.Parse(args[2]) : 2;
            int traceSeed = args.Length > 3 ? int.Parse(args[3]) : 1;
            return PhaseTrace.Run(tracePlayers, traceRounds, traceSeed);
        }
        if (args.Length > 0 && args[0] == "legality") return LegalityTests.Run(args.Length > 1 ? int.Parse(args[1]) : 300);
        if (args.Length > 0 && args[0] == "report") return Report.Run(args.Length > 1 ? int.Parse(args[1]) : 1000);
        if (args.Length > 0 && args[0] == "deal")
        {
            int n = args.Length > 1 ? int.Parse(args[1]) : 10;
            int dealGames = args.Length > 2 ? int.Parse(args[2]) : 20000;
            var counts = new Dictionary<Role, int>();
            for (int g = 0; g < dealGames; g++)
            {
                var agents = Enumerable.Range(0, n).Select(i => (IPlayerAgent)new RandomAgent(g * 100 + i, 0)).ToList();
                var game = new Game(n, g, agents);
                foreach (var p in game.Players) counts[p.Role] = counts.ContainsKey(p.Role) ? counts[p.Role] + 1 : 1;
            }
            Console.WriteLine(n + " players, " + dealGames + " deals:");
            foreach (var kv in counts.OrderByDescending(k => k.Value))
                Console.WriteLine("  " + kv.Key.ToString().PadRight(12) + (100.0 * kv.Value / dealGames).ToString("0.0").PadLeft(6) + "%  (avg " + (kv.Value / (double)dealGames).ToString("0.00") + " per game)");
            return 0;
        }
        int games = args.Length > 0 ? int.Parse(args[0]) : 2000;
        int failures = 0;

        for (int players = Game.MinPlayers; players <= Game.MaxPlayers; players++)
        {
            Console.WriteLine("Action deck size at " + players + " players: " + ActionCatalog.TotalCards(players));
            var wins = new Dictionary<Winner, int>();
            long rounds = 0;
            double chaosSum = 0, loyalSum = 0; int badDeal = 0;
            for (int g = 0; g < games; g++)
            {
                var agents = Enumerable.Range(0, players).Select(i => (IPlayerAgent)new RandomAgent(g * 100 + i)).ToList();
                var game = new Game(players, g, agents);
                var roleSet = game.Players.Select(p => p.Role).ToList();
                // Every role is unique except Lovers, which is dealt to exactly two seats at once by design.
                bool distinctOk = roleSet.GroupBy(r => r).All(gr => gr.Key == Role.Lovers ? gr.Count() == 2 : gr.Count() == 1);
                if (!distinctOk || roleSet.Count != players || !roleSet.Contains(Role.King) || !roleSet.Contains(Role.Prince) || !roleSet.Contains(Role.Nobleman)) badDeal++;
                chaosSum += roleSet.Count(r => RoleInfo.FactionOf(r) == Faction.Chaos);
                loyalSum += roleSet.Count(r => RoleInfo.FactionOf(r) == Faction.Loyalist && r != Role.King);
                try
                {
                    var result = game.Run();
                    if (game.CountCards() != ActionCatalog.TotalCards(players))
                    { failures++; Console.WriteLine("CARD LEAK players=" + players + " seed=" + g + " count=" + game.CountCards()); }
                    if (game.Players.Count(p => p.Alive) < 1)
                    { failures++; Console.WriteLine("EVERYONE DEAD seed=" + g); }
                    wins[result.Winner] = wins.ContainsKey(result.Winner) ? wins[result.Winner] + 1 : 1;
                    rounds += game.Round;
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine("EXCEPTION players=" + players + " seed=" + g + ": " + e);
                }
            }
            if (badDeal > 0) { failures++; Console.WriteLine("BAD ROLE DEAL x" + badDeal); }
            Console.WriteLine(players + " players: avg chaos roles " + (chaosSum / games).ToString("0.00") + ", avg extra loyalists " + (loyalSum / games).ToString("0.00"));
            Console.WriteLine(players + " players: avg rounds " + (rounds / (double)games).ToString("0.0") + " | " +
                string.Join(", ", wins.OrderBy(k => k.Key).Select(k => k.Key + " " + (100.0 * k.Value / games).ToString("0.0") + "%")));
        }
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " FAILURES");
        return failures == 0 ? 0 : 1;
    }
}
