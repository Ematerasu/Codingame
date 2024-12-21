public class Debug
{
    public static void Log(string msg)
    {
        Console.Error.Write(msg);
    }
}

public class Utils
{
    public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();
    public const int FIRST_TURN_TIME = 1000;
    public const int MAX_TURN_TIME = 50;
}

public const long NOGC_SIZE = 67_108_864; // 280_000_000;
GC.TryStartNoGCRegion(NOGC_SIZE); // true


public static class FastMath
{
    // Przybliżenie logarytmu
    public static float FastLog(float x)
    {
        var vx = new FloatIntUnion { FloatValue = x };
        vx.IntValue = (int)(vx.IntValue * 8.262958288192749e-8f) - 87_989_971;
        return vx.FloatValue;
    }

    // Przybliżenie pierwiastka kwadratowego
    public static float FastSqrt(float x)
    {
        var vx = new FloatIntUnion { FloatValue = x };
        vx.IntValue = (1 << 29) + (vx.IntValue >> 1) - (1 << 22);
        return vx.FloatValue;
    }

    // Przybliżenie odwrotności pierwiastka
    public static float FastRsqrt(float x)
    {
        return 1.0f / FastSqrt(x);
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatIntUnion
    {
        [FieldOffset(0)]
        public float FloatValue;
        [FieldOffset(0)]
        public int IntValue;
    }
}