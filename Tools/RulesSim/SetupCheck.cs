using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

// Build 9. Two modes:
//   setup     - cold start for every scenario and every player count: does setup run its 8 steps in order,
//               and does it end in a position the first action can start from? (the parity check, in code)
//   manifest  - the component counts the build needs, per player count and per scenario, straight from the
//               engine, so the checklist under Assets/Docs is generated from the game, not typed by hand.
internal static class SetupCheck
{
    private static readonly string[] StepNames =
    {
        "Choose the scenario", "Lay out the table", "Deal the loyalty cards", "Seat the players",
        "Build the action deck", "Shuffle the action deck", "Deal the starting hands", "Seat the King and open the first round",
    };

    public static int Setup(int seeds)
    {
        int failures = 0, games = 0;
        void Check(bool ok, string what) { if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }

        // Every scenario from the Build 5 picker, cold.
        foreach (var sc in ScenarioCatalog.All)
            for (int seed = 0; seed < seeds; seed++)
            {
                var agents = Enumerable.Range(0, sc.PlayerCount).Select(i => (IPlayerAgent)new RandomAgent(seed * 100 + i)).ToList();
                var g = sc.Start(agents, seed);
                games++;
                CheckGame(g, sc.PlayerCount, sc.HandSize > 0 ? sc.HandSize : g.Balance.HandSize, sc.Id + "#" + seed, Check);
            }
        // Every player count, with the default scenario settings.
        for (int n = Game.MinPlayers; n <= Game.MaxPlayers; n++)
            for (int seed = 0; seed < seeds; seed++)
            {
                var agents = Enumerable.Range(0, n).Select(i => (IPlayerAgent)new RandomAgent(seed * 100 + i)).ToList();
                var g = new Game(n, seed, agents);
                games++;
                CheckGame(g, n, g.Balance.HandSize, n + "p#" + seed, Check);
            }
        // Same seed, same table: the setup is deterministic (a shuffle is only random until the seed is fixed).
        for (int n = Game.MinPlayers; n <= Game.MaxPlayers; n++)
        {
            Func<Game> make = () => new Game(n, 7, Enumerable.Range(0, n).Select(i => (IPlayerAgent)new RandomAgent(i)).ToList());
            var a = make(); var b = make();
            Check(string.Join(",", a.Players.Select(p => p.Role + "@" + p.Seat)) == string.Join(",", b.Players.Select(p => p.Role + "@" + p.Seat)), n + "p same seed, different seating");
            Check(string.Join(",", a.Deck.Select(c => c.Id)) == string.Join(",", b.Deck.Select(c => c.Id)), n + "p same seed, different deck order");
        }
        // The steps themselves, once, as a player would read them.
        var demo = ScenarioCatalog.All[1].Start(Enumerable.Range(0, ScenarioCatalog.All[1].PlayerCount).Select(i => (IPlayerAgent)new RandomAgent(i)).ToList(), 1);
        Console.WriteLine("Setup steps for " + ScenarioCatalog.All[1].DisplayName + " (seed 1):");
        foreach (var s in demo.SetupSteps) Console.WriteLine("  " + s);
        Console.WriteLine("first-action problem: " + (demo.FirstActionProblem() ?? "none"));
        Console.WriteLine("setup: " + games + " cold starts, " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckGame(Game g, int players, int hand, string label, Action<bool, string> check)
    {
        check(g.SetupSteps.Count == 8, label + " ran " + g.SetupSteps.Count + " steps");
        for (int i = 0; i < g.SetupSteps.Count; i++)
            check(g.SetupSteps[i].StartsWith((i + 1) + ". "), label + " step " + (i + 1) + " out of order: " + g.SetupSteps[i]);
        var problem = g.FirstActionProblem();
        check(problem == null, label + " not ready for the first action: " + problem);
        check(g.Players.Count == players, label + " has " + g.Players.Count + " players, expected " + players);
        check(g.Players.Count(p => p.Role == Role.King) == 1, label + " has no single King");
        check(g.Players.All(p => p.Hand.Count == hand), label + " a hand is not " + hand + " cards");
        int expectedDeck = ExpectedDeckSize(g.Balance, players);
        check(g.Deck.Count + g.Players.Sum(p => p.Hand.Count) == expectedDeck, label + " deck+hands = " + (g.Deck.Count + g.Players.Sum(p => p.Hand.Count)) + ", expected " + expectedDeck);
        check(g.Players.Select(p => p.Role).Distinct().Count() == g.Players.Count || g.Players.Any(p => p.Role == Role.Lovers),
              label + " the same loyalty card was dealt twice");
    }

    public static int ExpectedDeckSize(BalanceConfig b, int players)
    {
        return b.DeckCounts.Sum(c => c.Count) * b.DeckMultiplier(players);
    }

    public static int Manifest()
    {
        var b = BalanceConfig.Factory();
        var asset = BalanceConfig.Current;
        Console.WriteLine("Action deck (copies per card, before the large-table multiplier of x" + asset.DeckMultiplierLargeTable + " from " + asset.LargeTableMinPlayers + " players):");
        int total = 0;
        foreach (var c in asset.DeckCounts) { Console.WriteLine("  " + c.Effect + " x" + c.Count); total += c.Count; }
        Console.WriteLine("  distinct action cards: " + asset.DeckCounts.Count + ", copies: " + total);
        for (int n = Game.MinPlayers; n <= Game.MaxPlayers; n++)
            Console.WriteLine("  " + n + " players: deck " + ExpectedDeckSize(asset, n) + " cards, hands " + (n * asset.HandSize) + ", draw pile " + (ExpectedDeckSize(asset, n) - n * asset.HandSize));
        Console.WriteLine("Loyalty cards (roles): " + string.Join(", ", Enum.GetValues(typeof(Role)).Cast<Role>()) + " = " + Enum.GetValues(typeof(Role)).Length);
        Console.WriteLine("Seats: throne (seat " + Game.ThroneSeat + ") + chairs up to " + (Game.MaxPlayers - 1) + " = " + Game.MaxPlayers + " seats, players " + Game.MinPlayers + "-" + Game.MaxPlayers);
        Console.WriteLine("Hand size: " + asset.HandSize);
        Console.WriteLine("Scenarios (" + ScenarioCatalog.All.Length + "):");
        foreach (var s in ScenarioCatalog.All)
            Console.WriteLine("  " + s.Id + " | " + s.DisplayName + " | " + s.PlayerCount + " players | hand " + (s.HandSize > 0 ? s.HandSize : asset.HandSize) + " | refill " + s.RefillAtEndOfTurn + " | round limit " + s.RoundLimit);
        return 0;
    }
}
