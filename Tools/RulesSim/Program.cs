using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

// Plays many bot-only games and checks engine invariants. Run: dotnet run --project Tools/RulesSim
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "legality") return LegalityTests.Run(args.Length > 1 ? int.Parse(args[1]) : 300);
        if (args.Length > 0 && args[0] == "report") return Report.Run(args.Length > 1 ? int.Parse(args[1]) : 1000);
        int games = args.Length > 0 ? int.Parse(args[0]) : 2000;
        int failures = 0;
        Console.WriteLine("Action deck size: " + ActionCatalog.TotalCards);

        for (int players = Game.MinPlayers; players <= Game.MaxPlayers; players++)
        {
            var wins = new Dictionary<Winner, int>();
            long rounds = 0;
            double chaosSum = 0, loyalSum = 0; int badDeal = 0;
            for (int g = 0; g < games; g++)
            {
                var agents = Enumerable.Range(0, players).Select(i => (IPlayerAgent)new RandomAgent(g * 100 + i)).ToList();
                var game = new Game(players, g, agents);
                var roleSet = game.Players.Select(p => p.Role).ToList();
                if (roleSet.Count != roleSet.Distinct().Count() || roleSet.Count != players || !roleSet.Contains(Role.King) || !roleSet.Contains(Role.Prince) || !roleSet.Contains(Role.Nobleman)) badDeal++;
                chaosSum += roleSet.Count(r => RoleInfo.FactionOf(r) == Faction.Chaos);
                loyalSum += roleSet.Count(r => RoleInfo.FactionOf(r) == Faction.Loyalist && r != Role.King);
                try
                {
                    var result = game.Run();
                    if (game.CountCards() != ActionCatalog.TotalCards)
                    { failures++; Console.WriteLine("CARD LEAK players=" + players + " seed=" + g + " count=" + game.CountCards()); }
                    if (game.Players.Count(p => p.Alive) < 1 || game.Players.First(p => p.Role == Role.King).Alive == false)
                    { failures++; Console.WriteLine("KING DIED seed=" + g); }
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
