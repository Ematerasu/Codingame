using System.Numerics;
using System.Runtime.CompilerServices;

namespace SummerChallenge2025.Bot;

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

    /*──────────────────────────  API  ──────────────────────────*/
    /// <summary>
    ///  Zamiana czterech liczb z inicjalizacji na <see cref="AgentClass"/>.
    ///  Jeśli nie pasuje – zwraca <see cref="AgentClass.Gunner"/>.
    /// </summary>
    public static AgentClass GuessClass(int cd, int range, int power, int bombs)
        => _reverse.TryGetValue((cd, range, power, bombs), out var cls) ? cls : AgentClass.Gunner;

    /// <summary>
    ///  Szybka ocena, czy <paramref name="target"/> jest „łatwym celem” w starciu z <paramref name="me"/>.
    /// </summary>
    public static bool IsSoftTarget(in AgentState target, in AgentState me, in AgentStats myStats, in AgentStats targetStats)
    {
        if (!target.Alive || target.playerId == me.playerId) return false;
        bool weakerDmg = targetStats.SoakingPower < myStats.SoakingPower;
        bool longerCd = targetStats.ShootCooldown > myStats.ShootCooldown;
        bool lowHP = target.Wetness > 60;
        return weakerDmg || longerCd || lowHP;
    }

    /// <summary>
    ///  Minimalna Manhattan‑distance do dowolnego przeciwnika spełniającego <paramref name="predicate"/>.
    ///  Zwraca <c>int.MaxValue</c>, jeśli brak takiego.
    /// </summary>
    public static int DistanceToClosestEnemy(GameState gs, int myId, Func<int, bool> predicate)
    {
        ref readonly var me = ref gs.Agents[myId];
        int best = int.MaxValue;
        for (int i = 0; i < GameState.MaxAgents; ++i)
        {
            if (!predicate(i)) continue;
            int d = GameState.Mdist(me.X, me.Y, gs.Agents[i].X, gs.Agents[i].Y);
            if (d < best) best = d;
        }
        return best;
    }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
        => BitOperations.PopCount(A)
        + BitOperations.PopCount(B)
        + BitOperations.PopCount(C)
        + BitOperations.PopCount(D);
}

// public static class GameStateExtensions
// {
//     public static GameStateBit ToBitState(this GameState source)
//     {
//         TileType[] paddedTiles = new TileType[GameStateBit.Cells];
//         for (int y = 0; y < source.Height; y++)
//         {
//             for (int x = 0; x < source.Width; x++)
//             {
//                 int srcIdx = x + y * source.Width;
//                 int dstIdx = GameStateBit.ToIndex(x, y);
//                 paddedTiles[dstIdx] = source.Tiles[srcIdx];
//             }
//         }

//         GameStateBit.InitStatic(paddedTiles, source.Classes);

//         var bitState = new GameStateBit((byte)source.Width, (byte)source.Height)
//         {
//             Turn = source.Turn,
//             Score0 = source.Score0,
//             Score1 = source.Score1,
//             IsGameOver = source.IsGameOver,
//             Winner = source.Winner
//         };

//         for (int id = 0; id < GameState.MaxAgents; id++)
//         {
//             bitState.Agents[id] = source.Agents[id];

//             if (source.Agents[id].Alive)
//             {
//                 int idx = GameStateBit.ToIndex(source.Agents[id].X, source.Agents[id].Y);
//                 bitState.Occup.Set(idx);
//             }
//         }

//         return bitState;
//     }
// }