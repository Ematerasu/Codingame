using System.Numerics;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;

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

public readonly struct Position
{
    public readonly byte X;
    public readonly byte Y;
    public Position(byte x, byte y) { X = x; Y = y; }
}

public sealed class GameStateOld
{
    // ====== Consts ======
    public const int MaxAgents = 10;
    public const int MapWidth = 20;
    public const int MapHeight = 10;

    public int Width;
    public int Height;

    public TileType[] Tiles;
    public int[] Occupancy; // -1 empty

    public AgentClass[] Classes = new AgentClass[MaxAgents];
    public AgentState[] Agents = new AgentState[MaxAgents];
    public Position[] Positions = new Position[MaxAgents];

    public int MyId;
    public int Turn;
    public int Score0, Score1;
    public bool IsGameOver;
    public int Winner;          // -1 = draw, 0 / 1 = player id

    public GameStateOld(int w = MapWidth, int h = MapHeight)
    {
        Width = w;
        Height = h;
        Tiles = new TileType[w * h];
        Occupancy = Enumerable.Repeat(-1, w * h).ToArray();
    }

    public GameStateOld Clone()
    {
        var copy = (GameStateOld)MemberwiseClone();

        copy.Occupancy = (int[])this.Occupancy.Clone();


        copy.Agents = (AgentState[])this.Agents.Clone();

        copy.Positions = (Position[])this.Positions.Clone();

        return copy;
    }

    public bool IsInBounds(byte x, byte y)
        => x < Width && y < Height;

    public ref AgentState GetAgent(int id) => ref Agents[id];

    public int AgentAt(byte x, byte y)
        => Occupancy[ToIndex(x, y)];

    public void ApplyInPlace(in TurnCommand p0Cmd, in TurnCommand p1Cmd)
    {
        if (IsGameOver) return;
        ResolveMoves(in p0Cmd, in p1Cmd);   // 1. MOVE
        ResolveHunker(in p0Cmd, in p1Cmd);  // 2. HUNKER
        ResolveCombat(in p0Cmd, in p1Cmd);  // 3. SHOOT / THROW
        CleanupAndCooldown();               // 4. wetness / cd / turn++

        if (!IsGameOver)
        {
            UpdateScores();
            CheckGameOver();
        }
    }

    private void ResolveMoves(in TurnCommand p0Cmd, in TurnCommand p1Cmd)
    {
        Span<byte> fromX = stackalloc byte[MaxAgents];
        Span<byte> fromY = stackalloc byte[MaxAgents];
        Span<byte> destX = stackalloc byte[MaxAgents];
        Span<byte> destY = stackalloc byte[MaxAgents];
        Span<bool> wants = stackalloc bool[MaxAgents];

        for (int id = 0; id < MaxAgents; ++id)
        {
            fromX[id] = Agents[id].X;
            fromY[id] = Agents[id].Y;
            destX[id] = fromX[id];
            destY[id] = fromY[id];
            wants[id] = false;
        }

        void Extract(in TurnCommand cmd, Span<byte> destX, Span<byte> destY, Span<bool> wants)
        {
            foreach (int id in cmd.EnumerateActive())
            {
                ref readonly var mv = ref cmd.Orders[id].Move;
                if (mv.Type != MoveType.Step) continue;

                ref var ag = ref Agents[id];
                if (!ag.Alive) continue;
                if (!IsInBounds(mv.X, mv.Y)) continue;

                if (mv.X == ag.X && mv.Y == ag.Y)
                    continue;

                int manhattan = Math.Abs(ag.X - mv.X) + Math.Abs(ag.Y - mv.Y);
                if (manhattan == 1)
                {
                    int idx = ToIndex(mv.X, mv.Y);
                    if (Tiles[idx] != TileType.Empty || Occupancy[idx] != -1) continue;
                }

                byte nx = ag.X, ny = ag.Y;

                int bestDist = int.MaxValue;
                foreach (var (dx, dy) in Dir4)
                {
                    byte cx = (byte)(ag.X + dx);
                    byte cy = (byte)(ag.Y + dy);
                    if (!IsInBounds(cx, cy)) continue;

                    int idx = ToIndex(cx, cy);
                    if (Tiles[idx] != TileType.Empty) continue;
                    if (Occupancy[idx] != -1) continue;

                    int d = Math.Abs(mv.X - cx) + Math.Abs(mv.Y - cy);    // Manhattan
                    if (d < bestDist) { bestDist = d; nx = cx; ny = cy; }
                }

                if (nx == ag.X && ny == ag.Y) continue;
                destX[id] = nx; destY[id] = ny; wants[id] = true;
            }
        }

        Extract(in p0Cmd, destX, destY, wants);
        Extract(in p1Cmd, destX, destY, wants);

        Span<int> firstOnTile = stackalloc int[Width * Height];
        for (int i = 0; i < firstOnTile.Length; ++i) firstOnTile[i] = -1;

        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;
            int idx = ToIndex(destX[id], destY[id]);

            if (firstOnTile[idx] == -1)
                firstOnTile[idx] = id;
            else
            {
                wants[id] = false;
                wants[firstOnTile[idx]] = false;
            }
        }

        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;
            int idx = ToIndex(destX[id], destY[id]);
            int occ = Occupancy[idx];

            if (occ != -1 && !wants[occ])
                wants[id] = false;
        }

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
                        wants[a] = wants[b] = false;
                        changed = true;
                    }
                }
            }
        } while (changed);

        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!wants[id]) continue;

            Occupancy[ToIndex(fromX[id], fromY[id])] = -1;
            Occupancy[ToIndex(destX[id], destY[id])] = id;

            Agents[id].X = destX[id];
            Agents[id].Y = destY[id];
            Positions[id] = new Position(destX[id], destY[id]);
        }
    }
    private void ResolveHunker(in TurnCommand p0Cmd, in TurnCommand p1Cmd)
    {
        for (int id = 0; id < MaxAgents; ++id)
            Agents[id].Hunkering = false;

        void ApplyHunker(in TurnCommand cmd)
        {
            foreach (int id in cmd.EnumerateActive())
            {
                ref readonly var cb = ref cmd.Orders[id].Combat;
                if (cb.Type != CombatType.Hunker) continue;
                if (!Agents[id].Alive) continue;

                Agents[id].Hunkering = true;
            }
        }

        ApplyHunker(in p0Cmd);
        ApplyHunker(in p1Cmd);
    }
    private void ResolveCombat(in TurnCommand p0Cmd, in TurnCommand p1Cmd)
    {
        Span<CombatAction> action = stackalloc CombatAction[MaxAgents];
        Span<bool> has = stackalloc bool[MaxAgents];

        void Collect(in TurnCommand cmd, Span<CombatAction> action, Span<bool> has)
        {
            foreach (int id in cmd.EnumerateActive())
            {
                ref readonly var cb = ref cmd.Orders[id].Combat;
                if (cb.Type == CombatType.None) continue;
                action[id] = cb;
                has[id] = true;
            }
        }
        Collect(in p0Cmd, action, has);
        Collect(in p1Cmd, action, has);

        for (int id = 0; id < MaxAgents; ++id)
        {
            if (!has[id]) continue;
            ref readonly var cb = ref action[id];
            switch (cb.Type)
            {
                case CombatType.Shoot:
                    ApplyShoot(id, cb.Arg1);          // Arg1 = enemyId
                    break;

                case CombatType.Throw:
                    ApplyThrow(id, cb.Arg1, cb.Arg2); // Arg1 = X, Arg2 = Y
                    break;

            }
        }
    }

    private void CleanupAndCooldown()
    {
        for (int id = 0; id < MaxAgents; ++id)
        {
            ref var ag = ref Agents[id];
            if (!ag.Alive) continue;

            if (ag.Wetness >= 100)
                ag.Alive = false;
            else if (ag.Cooldown > 0)
                ag.Cooldown--;
        }
        ++Turn;
    }

    private bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Cdist(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mdist(int x1, int y1, int x2, int y2)
        => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CoverModifier(TileType t) => t switch
    {
        TileType.HighCover => 0.25,
        TileType.LowCover => 0.50,
        _ => 1.00
    };

    private static readonly (sbyte dx, sbyte dy)[] Dir4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIndex(byte x, byte y) => x + y * Width;

    private void ApplyShoot(int shooterId, int targetId)
    {
        ref var shooter = ref Agents[shooterId];
        ref var target = ref Agents[targetId];
        if (shooter.playerId == target.playerId) return;
        if (!shooter.Alive || !target.Alive || shooterId == targetId) return;

        var stats = AgentUtils.Stats[Classes[shooterId]];
        if (shooter.Cooldown > 0) return;

        int dist = Mdist(shooter.X, shooter.Y, target.X, target.Y);
        if (dist > stats.OptimalRange * 2) return;

        double rangeMod = dist <= stats.OptimalRange ? 1.0 : 0.5;

        double coverMod = 1.0;

        int dx = target.X - shooter.X;
        int dy = target.Y - shooter.Y;

        bool sameCover = false;

        if (Math.Abs(dx) > 1)
        {
            int adjX = -Math.Sign(dx);
            int cx = target.X + adjX;
            int cy = target.Y;
            int idx = ToIndex((byte)cx, (byte)cy);
            if (IsInBounds((byte)cx, (byte)cy) && Cdist(cx, cy, shooter.X, shooter.Y) > 1)
            {
                var coverTile = Tiles[idx];
                var mod = CoverModifier(coverTile);
                coverMod = Math.Min(coverMod, mod);

                int sx = shooter.X - adjX;
                if (IsInBounds((byte)sx, (byte)shooter.Y))
                {
                    int shooterIdx = ToIndex((byte)sx, (byte)shooter.Y);
                    if (shooterIdx == idx)
                        sameCover = true;
                }
            }
        }

        if (Math.Abs(dy) > 1)
        {
            int adjY = -Math.Sign(dy);
            int cx = target.X;
            int cy = target.Y + adjY;
            int idx = ToIndex((byte)cx, (byte)cy);
            if (IsInBounds((byte)cx, (byte)cy) && Cdist(cx, cy, shooter.X, shooter.Y) > 1)
            {
                var coverTile = Tiles[idx];
                var mod = CoverModifier(coverTile);
                coverMod = Math.Min(coverMod, mod);

                int sy = shooter.Y - adjY;
                if (IsInBounds((byte)shooter.X, (byte)sy))
                {
                    int shooterIdx = ToIndex((byte)shooter.X, (byte)sy);
                    if (shooterIdx == idx)
                        sameCover = true;
                }
            }
        }
        if (sameCover)
            coverMod = 1.0;
        double hunkerBonus = target.Hunkering ? 0.25 : 0.0;

        int dmg = (int)Math.Round(stats.SoakingPower * rangeMod *
                                (coverMod - hunkerBonus));
        if (dmg <= 0) return;

        target.Wetness += dmg;
        shooter.Cooldown = stats.ShootCooldown + 1;
    }

    private void ApplyThrow(int throwerId, int cx, int cy)
    {
        ref var thr = ref Agents[throwerId];
        if (!thr.Alive || thr.SplashBombs <= 0) return;

        if (Mdist(thr.X, thr.Y, cx, cy) > 4) return;

        for (int id = 0; id < MaxAgents; ++id)
        {
            ref var ag = ref Agents[id];
            if (!ag.Alive) continue;

            if (Math.Abs(ag.X - cx) <= 1 && Math.Abs(ag.Y - cy) <= 1)
            {
                ag.Wetness += 30;
            }
        }
        thr.SplashBombs--;
    }

    public int GetLegalOrders(int agentId, Span<AgentOrder> dst)
    {
        ref readonly var ag = ref Agents[agentId];
        if (!ag.Alive) return 0;

        // ─── 1.  MOVE kandydaci (Stay + 4 ortogonalne puste) ────────────────
        Span<MoveAction> moves = stackalloc MoveAction[5];
        int mCnt = 0;
        moves[mCnt++] = new MoveAction(MoveType.Step, ag.X, ag.Y);  // Stay
        foreach (var (dx, dy) in Dir4)
        {
            byte nx = (byte)(ag.X + dx);
            byte ny = (byte)(ag.Y + dy);
            if (!IsInBounds(nx, ny)) continue;

            int idx = ToIndex(nx, ny);
            if (Tiles[idx] != TileType.Empty) continue;
            if (Occupancy[idx] != -1) continue;

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
            var s = AgentUtils.Stats[Classes[agentId]];
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
                    if (!IsInBounds((byte)tx, (byte)ty)) continue;

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

    private void UpdateScores()
    {
        int diff = 0;
        for (byte y = 0; y < Height; ++y)
            for (byte x = 0; x < Width; ++x)
            {
                int best0 = int.MaxValue, best1 = int.MaxValue;

                for (int id = 0; id < MaxAgents; ++id)
                {
                    ref readonly var ag = ref Agents[id];
                    if (!ag.Alive) continue;

                    int d = Math.Abs(ag.X - x) + Math.Abs(ag.Y - y);
                    if (ag.Wetness >= 50) d <<= 1;
                    if (ag.playerId == 0)
                        best0 = Math.Min(best0, d);
                    else
                        best1 = Math.Min(best1, d);
                }

                if (best0 < best1) diff++;
                else if (best1 < best0) diff--;
            }

        if (diff > 0) Score0 += diff;
        else if (diff < 0) Score1 += -diff;
    }

    private void CheckGameOver()
    {
        int lead = Score0 - Score1;
        if (lead >= 600) { IsGameOver = true; Winner = 0; return; }
        if (-lead >= 600) { IsGameOver = true; Winner = 1; return; }

        bool any0 = false, any1 = false;
        for (int i = 0; i < MaxAgents; ++i)
            if (Agents[i].Alive)
                if (Agents[i].playerId == 0) any0 = true; else any1 = true;

        if (!any0 || !any1)
        {
            IsGameOver = true;
            Winner = any0 ? 0 : any1 ? 1 : -1;
            return;
        }

        if (Turn >= 100)
        {
            IsGameOver = true;
            Winner = Score0 == Score1 ? -1 : (Score0 > Score1 ? 0 : 1);
        }
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Turn {Turn}   P0:{Score0}  P1:{Score1}");

        for (byte y = 0; y < Height; ++y)
        {
            for (byte x = 0; x < Width; ++x)
            {
                int idx = ToIndex(x, y);
                int occ = Occupancy[idx];

                char c = occ == -1
                    ? TileChar(Tiles[idx])
                    : AgentChar((byte)occ);

                sb.Append(c);
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        for (int id = 0; id < MaxAgents; ++id)
        {
            ref readonly var ag = ref Agents[id];
            if (!ag.Alive) continue;

            string side = ag.playerId == 0 ? "P0" : "P1";
            sb.AppendLine($"{id}[{side}] ({ag.X},{ag.Y}) W:{ag.Wetness} CD:{ag.Cooldown} B:{ag.SplashBombs}{(ag.Hunkering?" HUNKER":"")}");
        }

        return sb.ToString();
    }

    private static char TileChar(TileType t) => t switch
    {
        TileType.LowCover  => 'l',
        TileType.HighCover => 'h',
        _                  => '.'
    };

    private char AgentChar(byte id)
        => Agents[id].playerId == MyId
            ? (char)('A' + (id % 26)) 
            : (char)('a' + (id % 26));
}