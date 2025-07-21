using System.Numerics;
using System.Runtime.CompilerServices;

namespace SummerChallenge2025.Bot;

public static class Config
{
    public const bool DebugEnabled = true;
}

public enum AgentClass
{
    Gunner,
    Sniper,
    Bomber,
    Assault,
    Berserker
}

public static class AgentUtils
{
    /*──────────────────────────  dane bazowe  ──────────────────────────*/
    public static readonly IReadOnlyDictionary<AgentClass, AgentStats> Stats;
    public static readonly IReadOnlyDictionary<AgentClass, int> Balloons;
    private static readonly Dictionary<(int cd, int range, int power, int bombs), AgentClass> _reverse;

    static AgentUtils()
    {
        Stats = new Dictionary<AgentClass, AgentStats>
        {
            [AgentClass.Gunner] = new AgentStats { ShootCooldown = 1, OptimalRange = 4, SoakingPower = 16 },
            [AgentClass.Sniper] = new AgentStats { ShootCooldown = 5, OptimalRange = 6, SoakingPower = 24 },
            [AgentClass.Bomber] = new AgentStats { ShootCooldown = 2, OptimalRange = 2, SoakingPower = 8 },
            [AgentClass.Assault] = new AgentStats { ShootCooldown = 2, OptimalRange = 4, SoakingPower = 16 },
            [AgentClass.Berserker] = new AgentStats { ShootCooldown = 5, OptimalRange = 2, SoakingPower = 32 },
        };
        Balloons = new Dictionary<AgentClass, int>
        {
            [AgentClass.Gunner] = 1,
            [AgentClass.Sniper] = 0,
            [AgentClass.Bomber] = 3,
            [AgentClass.Assault] = 2,
            [AgentClass.Berserker] = 1,
        };

        _reverse = new Dictionary<(int, int, int, int), AgentClass>();
        foreach (var kv in Stats)
        {
            var key = (kv.Value.ShootCooldown, kv.Value.OptimalRange, kv.Value.SoakingPower, Balloons[kv.Key]);
            _reverse[key] = kv.Key;
        }
    }

    public static AgentClass GuessClass(int cd, int range, int power, int bombs)
        => _reverse.TryGetValue((cd, range, power, bombs), out var cls) ? cls : AgentClass.Gunner;

}

public struct BitBoard
{
    public ulong A, B, C, D;          // 0..63 | 64..127 | 128..191 | 192..255
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int idx)
        => Unsafe.Add(ref A, idx >> 6) |= 1UL << (idx & 63);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int idx)
        => Unsafe.Add(ref A, idx >> 6) &= ~(1UL << (idx & 63));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Test(int idx)
        => (Unsafe.Add(ref A, idx >> 6) & (1UL << (idx & 63))) != 0;
}