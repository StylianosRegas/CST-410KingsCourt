using System;
using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;

/// <summary>
/// Build 5's verification: constructs each of the 5 scenarios from ScenarioCatalog, runs it to completion
/// with bot agents many times, and reports that it (a) never throws, (b) respects its round limit, and
/// (c) actually exercises its own end condition at least sometimes (not dead code).
/// Run: dotnet run --project Tools/RulesSim -- scenarios
/// </summary>
internal static class ScenarioTest
{
    internal static int Run(int gamesPerScenario)
    {
        int failures = 0;
        foreach (var scenario in ScenarioCatalog.All)
        {
            var winCounts = new Dictionary<Winner, int>();
            int scenarioEndFired = 0;
            int overRoundLimit = 0;

            for (int seed = 0; seed < gamesPerScenario; seed++)
            {
                var agents = Enumerable.Range(0, scenario.PlayerCount).Select(i => (IPlayerAgent)new RandomAgent(seed * 100 + i)).ToList();
                Game g;
                try
                {
                    g = scenario.Start(agents, seed);
                    if (scenario.Id == "marked-court")
                    {
                        var queen = g.Players.FirstOrDefault(p => p.Role == Role.Queen);
                        var knight = g.Players.FirstOrDefault(p => p.Role == Role.Knight);
                        if (queen != null) g.AdminReveal(queen.Id);
                        if (knight != null) g.AdminReveal(knight.Id);
                    }
                    var result = g.Run(scenario.RoundLimit > 0 ? scenario.RoundLimit : 200);
                    winCounts[result.Winner] = winCounts.ContainsKey(result.Winner) ? winCounts[result.Winner] + 1 : 1;
                    if (scenario.EndCheck != null && result.Reason == scenario.EndCheckReason) scenarioEndFired++;
                    if (scenario.RoundLimit > 0 && g.Round > scenario.RoundLimit) overRoundLimit++;
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine(scenario.Id + " EXCEPTION seed=" + seed + ": " + e);
                }
            }

            Console.WriteLine(scenario.DisplayName + " (" + scenario.PlayerCount + "p, cap " + scenario.RoundLimit + ", hand " + scenario.HandSize +
                (scenario.RefillAtEndOfTurn ? "" : ", no refill") + "): " +
                string.Join(", ", winCounts.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)) +
                (scenario.EndCheck != null ? " | scenario-end fired " + scenarioEndFired + "/" + gamesPerScenario + " times" : ""));

            if (overRoundLimit > 0) { failures++; Console.WriteLine("  FAIL: " + overRoundLimit + " game(s) exceeded the round limit"); }
            if (scenario.EndCheck != null && scenarioEndFired == 0) { failures++; Console.WriteLine("  FAIL: scenario end condition never fired across " + gamesPerScenario + " games - dead code?"); }
        }

        Console.WriteLine(failures == 0 ? "ALL SCENARIO CHECKS PASSED" : failures + " FAILURES");
        return failures == 0 ? 0 : 1;
    }
}
