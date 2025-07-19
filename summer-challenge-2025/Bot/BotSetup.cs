using System;
using System.Globalization;
using System.Threading;

namespace SummerChallenge2025.Bot;

public static class BotSetup
{
    public const long NOGC_SIZE = 64 * 1024 * 1024; // 64 MB

    public static void Apply()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        bool success = GC.TryStartNoGCRegion(NOGC_SIZE);
        if (!success)
        {
            Console.Error.WriteLine("[BotSetup] GC No-GC Region failed to start.");
        }
    }
}