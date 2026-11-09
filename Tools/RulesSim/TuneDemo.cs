using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingsCourt.Core;

/// <summary>
/// Build 8: proves that tuning is a value edit. Loads the balance values straight out of the Unity asset
/// (Assets/Resources/BalanceConfig.asset, the same file the Inspector edits), optionally applies Name=Value
/// overrides on top, plays the same seeded bot games at every player count, and prints the outcomes.
/// Run it twice with different values and the outcomes differ with no code edited.
///   RulesSim tune [games] [Name=Value ...]        e.g.  RulesSim tune 300 HandSize=4
///   RulesSim tune 300 --compare PrinceThroneRoundsToWin=3
/// </summary>
internal static class TuneDemo
{
    private const string AssetRelative = @"..\..\Assets\Resources\BalanceConfig.asset";

    public static int Run(string[] args)
    {
        int games = 300; bool compare = false;
        var overrides = new List<KeyValuePair<string, string>>();
        foreach (var a in args.Skip(1))
        {
            int n;
            if (a == "--compare") compare = true;
            else if (int.TryParse(a, out n) && !a.Contains("=")) games = n;
            else if (a.Contains("=")) overrides.Add(new KeyValuePair<string, string>(a.Split('=')[0], a.Split('=')[1]));
        }

        string path = FindAsset();
        if (path == null) { Console.WriteLine("Could not find Assets/Resources/BalanceConfig.asset"); return 2; }
        Console.WriteLine("Balance values read from " + path);

        var baseline = LoadFromAsset(path);
        if (compare)
        {
            var tuned = LoadFromAsset(path);
            foreach (var o in overrides) Apply(tuned, o.Key, o.Value);
            Console.WriteLine();
            Console.WriteLine("Changed (no code edited): " + string.Join(", ", overrides.Select(o => o.Key + " " + Get(baseline, o.Key) + " -> " + o.Value)));
            Console.WriteLine();
            Console.WriteLine("BEFORE");
            Summarize(baseline, games);
            Console.WriteLine();
            Console.WriteLine("AFTER");
            Summarize(tuned, games);
        }
        else
        {
            foreach (var o in overrides) Apply(baseline, o.Key, o.Value);
            Summarize(baseline, games);
        }
        return 0;
    }

    public static string FindAsset()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "Assets", "Resources", "BalanceConfig.asset");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>Reads the scalar fields under "Values:" in the asset's YAML into a BalanceConfig. The per-card
    /// DeckCounts list keeps the factory values; it is not tuned in this demo.</summary>
    public static BalanceConfig LoadFromAsset(string path)
    {
        var cfg = BalanceConfig.Factory();
        bool inValues = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            if (raw.StartsWith("  Values:")) { inValues = true; continue; }
            if (!inValues) continue;
            if (!raw.StartsWith("    ")) break;
            string line = raw.Trim();
            if (line.StartsWith("-") || line.StartsWith("DeckCounts")) break;
            int c = line.IndexOf(':');
            if (c <= 0) continue;
            Apply(cfg, line.Substring(0, c), line.Substring(c + 1).Trim());
        }
        return cfg;
    }

    private static FieldInfo Field(string name)
    {
        var f = typeof(BalanceConfig).GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) throw new ArgumentException("No balance value named " + name);
        return f;
    }

    private static string Get(BalanceConfig c, string name) { return Convert.ToString(Field(name).GetValue(c), System.Globalization.CultureInfo.InvariantCulture); }

    private static void Apply(BalanceConfig c, string name, string value)
    {
        var f = Field(name);
        if (f.FieldType == typeof(int)) f.SetValue(c, int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        else if (f.FieldType == typeof(double)) f.SetValue(c, double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void Summarize(BalanceConfig cfg, int games)
    {
        Console.WriteLine("players  avg rounds  Loyalists  Traitors  Jester  Lovers  Reaper  Draw");
        for (int players = Game.MinPlayers; players <= Game.MaxPlayers; players++)
        {
            var wins = new Dictionary<Winner, int>(); long rounds = 0;
            for (int g = 0; g < games; g++)
            {
                var agents = Enumerable.Range(0, players).Select(i => (IPlayerAgent)new RandomAgent(g * 100 + i)).ToList();
                var game = new Game(players, g, agents, 0, true, cfg);
                var result = game.Run();
                wins[result.Winner] = wins.ContainsKey(result.Winner) ? wins[result.Winner] + 1 : 1;
                rounds += game.Round;
            }
            Func<Winner, string> pct = w => (wins.ContainsKey(w) ? 100.0 * wins[w] / games : 0).ToString("0.0").PadLeft(5) + "%";
            Console.WriteLine(players.ToString().PadLeft(5) + (rounds / (double)games).ToString("0.0").PadLeft(12) + "  " + pct(Winner.Loyalists) + "    " + pct(Winner.Traitors) + "  " + pct(Winner.Jester) + " " + pct(Winner.Lovers) + " " + pct(Winner.Reaper) + " " + pct(Winner.Draw));
        }
    }
}
