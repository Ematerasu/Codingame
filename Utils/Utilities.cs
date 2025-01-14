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
    public static float FastLog(float x)
    {
        if (x <= 0)
        {
            throw new ArgumentException("Logarithm is undefined for non-positive values");
        }

        float result = 0f;

        int exp = 0;
        while (x >= 2.0f)
        {
            x *= 0.5f;
            exp++;
        }
        
        float z = x - 1.0f;
        float z2 = z * z;
        result = z - (z2 / 2) + (z2 * z / 3) - (z2 * z2 / 4);

        result += exp * 0.69314718f;

        return result;
    }

    public static float FastSqrt(float x)
    {
        if (x < 0)
        {
            throw new ArgumentException("Cannot compute square root of negative number");
        }

        float guess = x * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            guess = 0.5f * (guess + x / guess);
        }

        return guess;
    }
}