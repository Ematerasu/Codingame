using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class Profiler
{
    private class Stat
    {
        public long TotalTicks;
        public int Count;

        public void Add(long ticks)
        {
            TotalTicks += ticks;
            Count++;
        }

        public double TotalMs => TotalTicks * 1000.0 / Stopwatch.Frequency;
        public double AvgMs => Count == 0 ? 0 : TotalMs / Count;
    }

    private static readonly Dictionary<string, Stat> stats = new();

    public static void Measure(string label, Action action)
    {
        var sw = Stopwatch.GetTimestamp();
        action();
        var elapsed = Stopwatch.GetTimestamp() - sw;

        if (!stats.TryGetValue(label, out var stat))
        {
            stat = new Stat();
            stats[label] = stat;
        }

        stat.Add(elapsed);
    }

    public static T Wrap<T>(string label, Func<T> func)
    {
        var sw = Stopwatch.GetTimestamp();
        T result = func();
        var elapsed = Stopwatch.GetTimestamp() - sw;

        if (!stats.TryGetValue(label, out var stat))
        {
            stat = new Stat();
            stats[label] = stat;
        }

        stat.Add(elapsed);
        return result;
    }

    public static void Report()
    {
        Console.WriteLine("\n=== PROFILER REPORT ===");
        foreach (var (label, stat) in stats)
        {
            Console.WriteLine($"{label,-25}: {stat.Count,6} calls | avg {stat.AvgMs,6:F6} ms | total {stat.TotalMs,8:F6} ms");
        }
    }

    public static void Reset()
    {
        stats.Clear();
    }
}