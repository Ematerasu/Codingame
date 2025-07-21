using System;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Engine;

class Program
{
    static void Main(string[] args)
    {
        int seed = args.Length > 0 ? int.Parse(args[0]) : Environment.TickCount;
        Console.WriteLine($"[Engine] Running simulation with seed: {seed}");

        var runner = new Runner(
            () => new CoverBot(),
            () => new Mikasa(),
            new SimulationParams
            {
                Seed = seed,
                Games = 100,
                //Visualizer = null,
                //Visualizer = new Visualizer(),
                //Visualizer = new PngVisualizer(seed.ToString()),
            }
        );
        runner.ProfiledRun();
    }
}