using System;
using System.Collections.Generic;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Engine;

public class Runner
{
    private readonly Func<AI> botFactory0, botFactory1;
    private readonly SimulationParams prm;
    private Random rng;
    private readonly IVisualizer? vis;

    public Runner(Func<AI> b0, Func<AI> b1, SimulationParams p)
    {
        botFactory0 = b0;
        botFactory1 = b1;
        prm = p;
        rng = new Random(prm.Seed);
        vis = prm.Visualizer;
    }

    public void Run()
    {
        int games = prm.Games;
        int wins0 = 0, wins1 = 0, draws = 0;

        for (int g = 0; g < games; g++)
        {
            GameState state = GameSetup.GenerateRandomState(rng, myPlayerId: 0);
            AI bot0 = botFactory0();
            AI bot1 = botFactory1();
            bot0.Initialize(0);
            bot1.Initialize(1);

            vis?.Render(state);

            for (int turn = 0; turn < prm.MaxTurns; ++turn)
            {
                var cmd0 = bot0.GetMove(state.FastClone());
                var cmd1 = bot1.GetMove(state.FastClone());

                vis?.UpdateOrders(cmd0, cmd1);
                state.ApplyInPlace(cmd0, cmd1);
                vis?.Render(state);

                if (state.IsGameOver)
                    break;
            }

            if (state.Winner == 0) wins0++;
            else if (state.Winner == 1) wins1++;
            else draws++;
        }
        rng = new Random(prm.Seed + 1);
        int total = wins0 + wins1 + draws;
        Console.WriteLine("=== Final Statistics ===");
        Console.WriteLine($"Games Played: {total}");
        Console.WriteLine($"Bot0 Wins:    {wins0}");
        Console.WriteLine($"Bot1 Wins:    {wins1}");
        Console.WriteLine($"Draws:        {draws}");
        Console.WriteLine($"Winrate P0:   {wins0 * 100.0 / total:0.00}%");
        Console.WriteLine($"Winrate P1:   {wins1 * 100.0 / total:0.00}%");
    }
    
    public void ProfiledRun()
    {
        Profiler.Reset();
        Profiler.Measure("FullSimulation", () =>
        {
            int games = prm.Games;
            int wins0 = 0, wins1 = 0, draws = 0;

            for (int g = 0; g < games; g++)
            {
                Profiler.Measure("OneGame", () =>
                {
                    GameState state = GameSetup.GenerateRandomState(rng, myPlayerId: 0);
                    AI bot0 = botFactory0();
                    AI bot1 = botFactory1();
                    bot0.Initialize(0);
                    bot1.Initialize(1);

                    if (vis != null)
                        Profiler.Measure("vis.Render", () => vis.Render(state));

                    for (int turn = 0; turn < prm.MaxTurns; ++turn)
                    {
                        var cloned0 = Profiler.Wrap("CloneForP0", () => state.FastClone());
                        var cloned1 = Profiler.Wrap("CloneForP1", () => state.FastClone());

                        var cmd0 = Profiler.Wrap("Bot0.GetMove", () => bot0.GetMove(cloned0));
                        var cmd1 = Profiler.Wrap("Bot1.GetMove", () => bot1.GetMove(cloned1));

                        if (vis != null)
                            Profiler.Measure("vis.UpdateOrders", () => vis.UpdateOrders(cmd0, cmd1));

                        Profiler.Measure("ApplyMove", () => state.ApplyInPlace(cmd0, cmd1));

                        if (vis != null)
                            Profiler.Measure("vis.Render", () => vis.Render(state));

                        if (state.IsGameOver)
                            break;
                    }

                    if (state.Winner == 0) wins0++;
                    else if (state.Winner == 1) wins1++;
                    else draws++;
                });
            }

            rng = new Random(prm.Seed + 1);
            int total = wins0 + wins1 + draws;
            Console.WriteLine("=== Final Statistics ===");
            Console.WriteLine($"Games Played: {total}");
            Console.WriteLine($"Bot0 Wins:    {wins0}");
            Console.WriteLine($"Bot1 Wins:    {wins1}");
            Console.WriteLine($"Draws:        {draws}");
            Console.WriteLine($"Winrate P0:   {wins0 * 100.0 / total:0.00}%");
            Console.WriteLine($"Winrate P1:   {wins1 * 100.0 / total:0.00}%");
        });

        Profiler.Report();
    }
}
