using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SummerChallenge2025.Bot;

public enum TileType : byte
{
    Empty = 0,
    LowCover = 1,
    HighCover = 2
}

public struct AgentStats
{
    public int ShootCooldown;
    public int OptimalRange;
    public int SoakingPower;
}

public struct AgentState
{
    public int playerId;
    public byte X, Y;
    public int Cooldown;
    public int Wetness;
    public int SplashBombs;
    public bool Hunkering;
    public bool Alive;
}


public sealed class GameState
{
    // ───────────────  Consts  ───────────────
    public const int MaxAgents = 10;
    public const int MaxW = 20, MaxH = 10, Cells = MaxW * MaxH; // 200 fields

    // ───────────────  Unchanged map data  ───────────────
    public static readonly TileType[] Tiles = new TileType[Cells];  // filed in GameSetup
    public static AgentClass[] AgentClasses { get; private set; } = new AgentClass[MaxAgents];

    public static void InitStatic(TileType[] tilesFromInput, AgentClass[] classesFromInput)
    {
        if (tilesFromInput.Length != Cells) throw new ArgumentException($"Tiles length {tilesFromInput.Length} != 200");
        Array.Copy(tilesFromInput, Tiles, Cells);
        Array.Copy(classesFromInput, AgentClasses, MaxAgents);
    }

    // ───────────────  Dynamic state  ───────────────
    public readonly AgentState[] Agents;   // 10 × 32 B = 320 B
    public BitBoard Occup;        // 32 B – occupation (‑1 => bit=0)

    public byte W { get; private set; }   // 12…20
    public byte H { get; private set; }   //  6…10

    public int Turn;
    public int Score0, Score1;
    public bool IsGameOver;
    public int Winner = -1;

    private static readonly ArrayPool<AgentState> _agentPool = ArrayPool<AgentState>.Shared;

    public GameState(byte width, byte height)
    {
        Agents = _agentPool.Rent(MaxAgents);
        ClearAgents();
        W = width;
        H = height;
    }

    private GameState(byte width, byte height, AgentState[] agents, BitBoard occ, int turn, int s0, int s1, bool over, int winner)
    {
        W = width;
        H = height;
        Agents = agents;
        Occup = occ;
        Turn = turn; Score0 = s0; Score1 = s1; IsGameOver = over; Winner = winner;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GameState FastClone()
    {
        var buf = _agentPool.Rent(MaxAgents);
        for (int i = 0; i < MaxAgents; ++i)
            buf[i] = Agents[i];
        return new GameState(W, H, buf, Occup, Turn, Score0, Score1, IsGameOver, Winner);
    }

    public void Dispose() => _agentPool.Return(Agents);

    public void ClearAgents()
    {
        for (int i = 0; i < MaxAgents; i++)
            Agents[i] = default;
        Occup = default;
    }

    // ───────────────  Helpers ───────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(int x, int y) => x + y * MaxW;   // stały stride 20

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int x, int y) => (uint)x < W && (uint)y < H;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mdist(int x1, int y1, int x2, int y2)
        => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Cdist(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    public int AgentAt(byte x, byte y)
    {
        int idx = ToIndex(x, y);
        if (!Occup.Test(idx)) return -1;
        for (int id = 0; id < MaxAgents; id++)
        {
            ref readonly var ag = ref Agents[id];
            if (!ag.Alive) continue;
            if (ag.X == x && ag.Y == y)
                return id;
        }
        return -1;
    }

    public void UpdateFromInput(IEnumerable<(int id, byte x, byte y, int cooldown, int bombs, int wetness)> data)
    {
        foreach (var tup in data)
        {
            ref var ag = ref Agents[tup.id];
            ag.X = tup.x;
            ag.Y = tup.y;
            ag.Cooldown = tup.cooldown;
            ag.SplashBombs = tup.bombs;
            ag.Wetness = tup.wetness;
            ag.Hunkering = false;
            ag.Alive = true;
            Occup.Set(ToIndex(ag.X, ag.Y));
        }
    }

    private readonly Queue<int> _bfsQueue = new Queue<int>(Cells);
    private readonly bool[] _visited = new bool[Cells];
    private readonly int[] _cameFrom = new int[Cells];

    public (byte x, byte y)? PathfindStep(int sx, int sy, int tx, int ty)
    {
        if (!PathCache.Ready) return null;

        int from = ToIndex(sx, sy);
        int to = ToIndex(tx, ty);

        if (Tiles[to] != TileType.Empty) return null;
        byte nx = PathCache.FirstStep[from, to, 0];
        byte ny = PathCache.FirstStep[from, to, 1];
        if (nx == 0 && ny == 0 && (sx != 0 || sy != 0)) return null; // nieosiągalny
        return (nx, ny);
    }
    
    public void ApplyInPlace(in TurnCommand p0Cmd, in TurnCommand p1Cmd, bool updateScore = true)
    {
        if (IsGameOver) return;
        ResolveMoves(in p0Cmd, in p1Cmd);
        ResolveHunker(in p0Cmd, in p1Cmd);
        ResolveCombat(in p0Cmd, in p1Cmd);
        Cleanup();

        if (!IsGameOver && updateScore)
        {
            UpdateScores();
            CheckGameOver();
        }
    }

    private void ResolveMoves(in TurnCommand c0, in TurnCommand c1)
    {
        Span<byte> fromX = stackalloc byte[MaxAgents];
        Span<byte> fromY = stackalloc byte[MaxAgents];
        Span<byte> destX = stackalloc byte[MaxAgents];
        Span<byte> destY = stackalloc byte[MaxAgents];
        Span<bool> wants = stackalloc bool[MaxAgents];

        // Inicjalizacja pozycji
        for (int id = 0; id < MaxAgents; ++id)
        {
            fromX[id] = Agents[id].X;
            fromY[id] = Agents[id].Y;
            destX[id] = fromX[id];
            destY[id] = fromY[id];
            wants[id] = false;
        }

        ExtractMoves(c0, destX, destY, wants);
        ExtractMoves(c1, destX, destY, wants);

        // Konflikt na tym samym polu → anuluj obie próby
        Span<int> first = stackalloc int[Cells];
        for (int i = 0; i < Cells; i++) first[i] = -1;

        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;
            int idx = ToIndex(destX[id], destY[id]);
            if (first[idx] == -1) first[idx] = id;
            else
            {
                wants[id] = false;
                wants[first[idx]] = false;
            }
        }

        // Jeśli agent chce wejść na pole innego, który się nie rusza
        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;
            for (int other = 0; other < MaxAgents; ++other)
            {
                if (id == other || !Agents[other].Alive) continue;
                if (Agents[other].X == destX[id] && Agents[other].Y == destY[id] && !wants[other])
                {
                    wants[id] = false;
                    break;
                }
            }
        }

        // Swap detection (A→B, B→A)
        bool changed;
        do
        {
            changed = false;
            for (int a = 0; a < MaxAgents; ++a)
            {
                if (!wants[a]) continue;
                for (int b = a + 1; b < MaxAgents; ++b)
                {
                    if (!wants[b]) continue;
                    bool swap = destX[a] == fromX[b] && destY[a] == fromY[b] &&
                                destX[b] == fromX[a] && destY[b] == fromY[a];
                    if (swap)
                    {
                        wants[a] = false;
                        wants[b] = false;
                        changed = true;
                    }
                }
            }
        } while (changed);

        // Zatwierdzenie ruchów
        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;

            int from = ToIndex(fromX[id], fromY[id]);
            int to = ToIndex(destX[id], destY[id]);
            Occup.Clear(from);
            Occup.Set(to);
            Agents[id].X = destX[id];
            Agents[id].Y = destY[id];
        }

        // ─────────────── LOCAL FUNC ───────────────
        void ExtractMoves(in TurnCommand cmd, Span<byte> destX, Span<byte> destY, Span<bool> wants)
        {
            foreach (int id in cmd.EnumerateActive())
            {
                ref readonly var mv = ref cmd.Orders[id].Move;
                if (mv.Type != MoveType.Step) continue;
                ref var ag = ref Agents[id];
                if (!ag.Alive) continue;

                int tx = mv.X, ty = mv.Y;
                if (!InBounds(tx, ty)) continue;
                if (tx == ag.X && ty == ag.Y) continue;

                int man = Math.Abs(tx - ag.X) + Math.Abs(ty - ag.Y);
                if (man == 1)
                {
                    int idx = ToIndex(tx, ty);
                    if (Tiles[idx] == TileType.Empty && !Occup.Test(idx))
                    {
                        destX[id] = (byte)tx;
                        destY[id] = (byte)ty;
                        wants[id] = true;
                    }
                    continue;
                }

                var step = PathfindStep(ag.X, ag.Y, tx, ty);
                if (step.HasValue)
                {
                    var (bx, by) = step.Value;
                    destX[id] = bx;
                    destY[id] = by;
                    wants[id] = true;
                }
            }
        }
    }

    private void ResolveHunker(in TurnCommand c0, in TurnCommand c1)
    {
        for (int id = 0; id < MaxAgents; id++) Agents[id].Hunkering = false;
        Apply(c0); Apply(c1);
        void Apply(in TurnCommand cmd)
        {
            foreach (int id in cmd.EnumerateActive())
                if (cmd.Orders[id].Combat.Type == CombatType.Hunker && Agents[id].Alive)
                    Agents[id].Hunkering = true;
        }
    }

    private void ResolveCombat(in TurnCommand c0, in TurnCommand c1)
    {
        Span<CombatAction> acts = stackalloc CombatAction[MaxAgents];
        Span<bool> has = stackalloc bool[MaxAgents];
        Collect(c0, acts, has); Collect(c1, acts, has);

        for (int id = 0; id < MaxAgents; id++)
            if (has[id])
            {
                ref readonly var cb = ref acts[id];
                switch (cb.Type)
                {
                    case CombatType.Shoot: ApplyShoot(id, cb.Arg1); break;
                    case CombatType.Throw: ApplyThrow(id, cb.Arg1, cb.Arg2); break;
                }
            }

        void Collect(in TurnCommand cmd, in Span<CombatAction> acts, in Span<bool> has)
        {
            foreach (int id in cmd.EnumerateActive())
            {
                ref readonly var c = ref cmd.Orders[id].Combat;
                if (c.Type != CombatType.None) { acts[id] = c; has[id] = true; }
            }
        }
    }

    private void ApplyShoot(int shooter, int target)
    {
        ref var s = ref Agents[shooter];
        ref var t = ref Agents[target];

        if (!t.Alive || !s.Alive || s.playerId == t.playerId) return;

        var st = AgentUtils.Stats[AgentClasses[shooter]];
        if (s.Cooldown > 0) return;

        int dist = Math.Abs(s.X - t.X) + Math.Abs(s.Y - t.Y);
        if (dist > st.OptimalRange * 2) return;

        double rangeMod = dist <= st.OptimalRange ? 1.0 : 0.5;
        double coverMod = 1.0;
        bool sameCover = false;

        int dx = t.X - s.X;
        int dy = t.Y - s.Y;

        if (Math.Abs(dx) > 1)
        {
            int adjX = -Math.Sign(dx);
            int cx = t.X + adjX;
            int cy = t.Y;
            if (InBounds(cx, cy) && Cdist(cx, cy, s.X, s.Y) > 1)
            {
                TileType cover = Tiles[ToIndex(cx, cy)];
                coverMod = Math.Min(coverMod, CoverModifier(cover));

                int sx = s.X - adjX;
                if (InBounds(sx, s.Y))
                {
                    if (ToIndex(sx, s.Y) == ToIndex(cx, cy))
                        sameCover = true;
                }
            }
        }

        if (Math.Abs(dy) > 1)
        {
            int adjY = -Math.Sign(dy);
            int cx = t.X;
            int cy = t.Y + adjY;
            if (InBounds(cx, cy) && Cdist(cx, cy, s.X, s.Y) > 1)
            {
                TileType cover = Tiles[ToIndex(cx, cy)];
                coverMod = Math.Min(coverMod, CoverModifier(cover));

                int sy = s.Y - adjY;
                if (InBounds(s.X, sy))
                {
                    if (ToIndex(s.X, sy) == ToIndex(cx, cy))
                        sameCover = true;
                }
            }
        }

        if (sameCover)
            coverMod = 1.0;

        double hunkerBonus = t.Hunkering ? 0.25 : 0.0;
        int dmg = (int)Math.Round(st.SoakingPower * rangeMod * (coverMod - hunkerBonus));
        if (dmg <= 0) return;

        t.Wetness += dmg;
        s.Cooldown = st.ShootCooldown + 1;
    }

    private void ApplyThrow(int thrower, int cx, int cy)
    {
        ref var th = ref Agents[thrower];
        if (!th.Alive || th.SplashBombs == 0) return;
        if (Math.Abs(th.X - cx) + Math.Abs(th.Y - cy) > 4) return;
        for (int id = 0; id < MaxAgents; id++)
            if (Agents[id].Alive && Math.Abs(Agents[id].X - cx) <= 1 && Math.Abs(Agents[id].Y - cy) <= 1)
                Agents[id].Wetness += 30;
        th.SplashBombs--;
    }

    private void Cleanup()
    {
        for (int id = 0; id < MaxAgents; ++id)
        {
            ref var ag = ref Agents[id];
            if (!ag.Alive) continue;
            if (ag.Wetness >= 100)
            {
                ag.Alive = false;
                Occup.Clear(ToIndex(ag.X, ag.Y));
            }
            else if (ag.Cooldown > 0) ag.Cooldown--;
        }
        ++Turn;
    }

    private void UpdateScores()
    {
        int minX0 = MaxW, maxX0 = -1, minY0 = MaxH, maxY0 = -1;
        int minX1 = MaxW, maxX1 = -1, minY1 = MaxH, maxY1 = -1;

        for (int id = 0; id < MaxAgents; ++id)
        {
            ref readonly var ag = ref Agents[id];
            if (!ag.Alive) continue;

            if (ag.playerId == 0)
            {
                minX0 = Math.Min(minX0, ag.X);
                maxX0 = Math.Max(maxX0, ag.X);
                minY0 = Math.Min(minY0, ag.Y);
                maxY0 = Math.Max(maxY0, ag.Y);
            }
            else
            {
                minX1 = Math.Min(minX1, ag.X);
                maxX1 = Math.Max(maxX1, ag.X);
                minY1 = Math.Min(minY1, ag.Y);
                maxY1 = Math.Max(maxY1, ag.Y);
            }
        }

        int diff = 0;

        // Sprawdź tylko pola które mogły być "garantowane", ale mimo wszystko sprawdź d0 i d1
        for (int y = 0; y < H; ++y)
        for (int x = 0; x < W; ++x)
        {
            // Optional: early skip jeśli poza bboxami obu stron
            int d0 = int.MaxValue, d1 = int.MaxValue;
            for (int id = 0; id < MaxAgents; ++id)
            {
                ref readonly var ag = ref Agents[id];
                if (!ag.Alive) continue;

                int d = Math.Abs(ag.X - x) + Math.Abs(ag.Y - y);
                if (ag.Wetness >= 50) d <<= 1;

                if (ag.playerId == 0) d0 = Math.Min(d0, d);
                else d1 = Math.Min(d1, d);
            }

            if (d0 < d1) diff++;
            else if (d1 < d0) diff--;
            // w przeciwnym razie nikt nie kontroluje
        }

        if (diff > 0) Score0 += diff;
        else if (diff < 0) Score1 -= diff;
    }

    private void CheckGameOver()
    {
        int lead = Score0 - Score1;
        if (lead >= 600) { IsGameOver = true; Winner = 0; return; }
        if (-lead >= 600) { IsGameOver = true; Winner = 1; return; }

        bool any0 = false, any1 = false;
        for (int id = 0; id < MaxAgents; ++id)
            if (Agents[id].Alive)
                if (Agents[id].playerId == 0) any0 = true; else any1 = true;
        if (!any0 || !any1)
        {
            IsGameOver = true; Winner = any0 ? 0 : any1 ? 1 : -1; return;
        }

        if (Turn >= 100)
        {
            IsGameOver = true;
            Winner = Score0 == Score1 ? -1 : (Score0 > Score1 ? 0 : 1);
        }
    }

    public int GetLegalOrders(int agentId, Span<AgentOrder> dst)
    {
        ref readonly var ag = ref Agents[agentId];
        if (!ag.Alive) return 0;

        Span<MoveAction> moves = stackalloc MoveAction[5];
        int mCnt = 0;
        moves[mCnt++] = new MoveAction(MoveType.Step, ag.X, ag.Y);  // Stay
        foreach (var (dx, dy) in Helpers.Dir4)
        {
            byte nx = (byte)(ag.X + dx);
            byte ny = (byte)(ag.Y + dy);
            if (!InBounds(nx, ny)) continue;

            int idx = ToIndex(nx, ny);
            if (Tiles[idx] != TileType.Empty) continue;
            if (Occup.Test(idx)) continue;

            moves[mCnt++] = new MoveAction(MoveType.Step, nx, ny);
        }

        // ─── 2.  COMBAT kandydaci ───────────────────────────────────────────
        //  |None|Hunker|Shoot*|Throw*|
        Span<CombatAction> comb = stackalloc CombatAction[MaxAgents + 2 + 81];
        int cCnt = 0;
        comb[cCnt++] = default;                              // None
        comb[cCnt++] = new CombatAction(CombatType.Hunker);  // Hunker

        // SHOOT (jeśli CD==0)
        if (ag.Cooldown == 0)
        {
            var s = AgentUtils.Stats[AgentClasses[agentId]];
            for (int trg = 0; trg < MaxAgents; ++trg)
            {
                if (trg == agentId || !Agents[trg].Alive) continue;
                if (Agents[trg].playerId == ag.playerId) continue;
                if (Mdist(ag.X, ag.Y, Agents[trg].X, Agents[trg].Y) <= s.OptimalRange * 2)
                    comb[cCnt++] = new CombatAction(CombatType.Shoot, (ushort)trg);
            }
        }

        // THROW (jeśli są bomby)
        if (ag.SplashBombs > 0)
        {
            for (int dx = -4; dx <= 4; ++dx)
                for (int dy = -4; dy <= 4; ++dy)
                {
                    if (dx == 0 && dy == 0) continue;
                    int man = Math.Abs(dx) + Math.Abs(dy);
                    if (man > 4) continue;

                    int tx = ag.X + dx, ty = ag.Y + dy;
                    if (!InBounds(tx, ty)) continue;

                    comb[cCnt++] = new CombatAction(CombatType.Throw, (ushort)tx, (byte)ty);
                }
        }

        // ─── 3.  Iloczyn kartezjański MOVE × COMBAT ─────────────────────────
        int outCnt = 0;
        for (int mi = 0; mi < mCnt; ++mi)
        {
            for (int ci = 0; ci < cCnt; ++ci)
            {
                if (outCnt >= dst.Length) return outCnt;   // bufor pełny
                dst[outCnt++] = new AgentOrder { Move = moves[mi], Combat = comb[ci] };
            }
        }
        return outCnt;
    }

    public static double CoverModifier(TileType tt)
        => tt switch
        {
            TileType.LowCover => 0.5,
            TileType.HighCover => 0.25,
            _ => 1.0
        };

}

public static class PathCache
{
    public static readonly byte[,,] FirstStep = new byte[GameState.Cells, GameState.Cells, 2];
    public static bool Ready;

    public static void Precompute(GameState st)
    {
        for (int sy = 0; sy < st.H; sy++)
            for (int sx = 0; sx < st.W; sx++)
            {
                int from = GameState.ToIndex(sx, sy);
                if (GameState.Tiles[from] != TileType.Empty) continue;

                Span<bool> visited = stackalloc bool[GameState.Cells];
                Span<int> parent = stackalloc int[GameState.Cells];

                visited.Clear();
                for (int i = 0; i < GameState.Cells; i++) parent[i] = -1;

                Span<int> queue = stackalloc int[GameState.Cells];
                int qHead = 0, qTail = 0;

                visited[from] = true;
                queue[qTail++] = from;

                while (qHead < qTail)
                {
                    int cur = queue[qHead++];
                    int cx = cur % GameState.MaxW, cy = cur / GameState.MaxW;

                    foreach (var (dx, dy) in Helpers.Dir4)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (!st.InBounds(nx, ny)) continue;
                        int ni = GameState.ToIndex(nx, ny);
                        if (visited[ni] || GameState.Tiles[ni] != TileType.Empty) continue;

                        visited[ni] = true;
                        parent[ni] = cur;
                        queue[qTail++] = ni;
                    }
                }

                for (int ty = 0; ty < st.H; ty++)
                    for (int tx = 0; tx < st.W; tx++)
                    {
                        int to = GameState.ToIndex(tx, ty);
                        if (!visited[to]) continue;

                        // Odtwórz pierwszy krok z parent[]
                        int step = to;
                        while (parent[step] != from && parent[step] != -1)
                            step = parent[step];

                        if (step == from) continue;

                        PathCache.FirstStep[from, to, 0] = (byte)(step % GameState.MaxW);
                        PathCache.FirstStep[from, to, 1] = (byte)(step / GameState.MaxW);
                    }
            }

        Ready = true;
    }
}

