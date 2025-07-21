using System;
using System.Collections.Generic;
using System.Linq;

namespace SummerChallenge2025.Bot;

public enum EnemyPlan
{
    Unknown,
    CentralPush,
    FlankAttack,
    DoubleFlank,
    SpawnTurtle,
    FullRush,
    SplitFlankWithSniper,
}

public enum AgentTaskType
{
    PushToCover,
    Flank,
    HoldLine,
    SupportWithSplash,
}

public class AgentTask
{
    public AgentTaskType Type;
    public (int x, int y)? TargetHint;
    public int? EnemyIdFocus;
    public int Priority;

    public AgentTask(AgentTaskType type, (int, int)? hint = null, int? enemyId = null, int prio = 0)
    {
        Type = type;
        TargetHint = hint;
        EnemyIdFocus = enemyId;
        Priority = prio;
    }
}

public sealed class DevelopmentPhase : IGamePhase
{
    private readonly Dictionary<int, AgentPlan> _plans;
    private readonly int _assaultRow;
    private int _myId;
    private HashSet<(int x, int y)>[] _coveredTiles;
    private EnemyPlan _currentPlan = EnemyPlan.Unknown;

    public DevelopmentPhase(IReadOnlyDictionary<int, AgentPlan> prev, int assaultRow,
                             HashSet<(int x, int y)>[] coveredTiles,
                             List<(int x, int y, int score)> sniperSpots)
    {
        _plans = new Dictionary<int, AgentPlan>(prev);
        _assaultRow = assaultRow;
        _coveredTiles = coveredTiles;
    }

    public void Enter(GameState st, int myPlayerId)
    {
        _myId = myPlayerId;
        TryUpdatePlan(st);
    }

    private void TryUpdatePlan(GameState st)
    {
        var newPlan = EnemyPlanRecognizer.Analyze(st, _myId);
        if (_currentPlan == EnemyPlan.Unknown && newPlan != EnemyPlan.Unknown)
        {
            _currentPlan = newPlan;
            Console.Error.WriteLine($"[DevelopmentPhase] Recognized enemy plan: {_currentPlan}");
            AgentTaskAssigner.AdaptPlans(st, _plans, _currentPlan, _myId);
        }
    }

    public TurnCommand GetMove(GameState st)
    {
        TryUpdatePlan(st);
        var cmd = new TurnCommand(GameState.MaxAgents);
        var reserved = new HashSet<(byte,byte)>();
        foreach (var id in _plans.Keys.OrderBy(i => i))
        {
            ref readonly var me = ref st.Agents[id];
            if (!me.Alive) { _plans.Remove(id); continue; }

            var plan = _plans[id];
            var target = plan.NextMove(st, id);
            var step = NextStep(st, me.X, me.Y, target.X, target.Y, reserved);
            MoveAction move;
            if (step is { } s)                              // da się iść
            {
                reserved.Add(s);
                move = new MoveAction(MoveType.Step, s.x, s.y);
            }
            else                                            // nie ruszamy się
            {
                move = new MoveAction(MoveType.Step, me.X, me.Y);
            }
            cmd.SetMove(id, move);

            switch (plan.Class)
            {
                case AgentClass.Sniper:
                    Helpers.TrySniperShoot(st, id, ref cmd);
                    break;
                case AgentClass.Bomber:
                    Helpers.TryOpportunisticThrow(st, id, ref cmd);
                    break;
            }
        }
        return cmd;
    }

    public IGamePhase? GetNextPhase(GameState _) => new CombatPhase(_coveredTiles, _currentPlan, _plans);

    public bool ShouldExit(GameState st)
    {
        int readyShooters = 0;
        int agentsAtFront = 0;
        Dictionary<int, int> enemyToMyThreats = new();

        foreach (var (id, plan) in _plans)
        {
            ref readonly var me = ref st.Agents[id];
            if (!me.Alive || me.playerId != _myId) continue;

            // 1. Czy widzę przeciwnika i mogę strzelić?
            if (me.Cooldown == 0)
            {
                for (int eid = 0; eid < GameState.MaxAgents; eid++)
                {
                    ref readonly var enemy = ref st.Agents[eid];
                    if (!enemy.Alive || enemy.playerId == _myId) continue;

                    int dist = GameState.Mdist(me.X, me.Y, enemy.X, enemy.Y);
                    AgentClass cls = GameState.AgentClasses[id];
                    int maxRange = AgentUtils.Stats[cls].OptimalRange * 2;
                    if (dist <= maxRange)
                    {
                        readyShooters++;
                        if (!enemyToMyThreats.ContainsKey(eid))
                            enemyToMyThreats[eid] = 0;
                        enemyToMyThreats[eid]++;
                    }
                }
            }

            // 2. Czy dotarłem do frontu?
            if (Math.Abs(me.X - st.W / 2) <= 1)
                agentsAtFront++;
        }

        // warunek 1: 2+ moich gotowych do strzału
        if (readyShooters >= 2)
            return true;

        // warunek 2: 2+ moich przy froncie
        if (agentsAtFront >= 2)
            return true;

        // warunek 3: 1 przeciwnik otoczony przez 2+ moich
        foreach (var kv in enemyToMyThreats)
            if (kv.Value >= 2)
                return true;

        return false;
    }
    

    private static (byte x, byte y)? NextStep(GameState st,
                                            byte sx, byte sy,
                                            int  tx,  int  ty,
                                            HashSet<(byte,byte)> reserved)
    {
        if (sx == tx && sy == ty) return null;

        int W = st.W, H = st.H;
        bool[] vis = new bool[W * H];
        var q = new Queue<(byte x, byte y, byte fx, byte fy)>();

        Vis(sx, sy);

        foreach (var (dx,dy) in Helpers.Dir4)
            TryPush((byte)(sx+dx), (byte)(sy+dy), (byte)(sx+dx), (byte)(sy+dy));

        while (q.Count != 0)
        {
            var (x,y,fx,fy) = q.Dequeue();
            if (x == tx && y == ty) return (fx,fy);

            foreach (var (dx,dy) in Helpers.Dir4)
                TryPush((byte)(x+dx), (byte)(y+dy), fx, fy);
        }
        return null;

        void Vis(int x,int y) => vis[y*W + x] = true;
        bool IsVis(int x,int y) => vis[y*W + x];

        bool IsBlocked(int x,int y)
        {
            if ((uint)x >= W || (uint)y >= H) return true;
            TileType t = GameState.Tiles[y*W + x];                    
            if (t != TileType.Empty)             return true;         // cover = ściana
            if (st.AgentAt((byte)x,(byte)y) != -1) return true;     // ktoś stoi
            if (reserved.Contains(((byte)x,(byte)y))) return true;
            return false;
        }

        void TryPush(byte nx, byte ny, byte fx, byte fy)
        {
            if (IsBlocked(nx,ny) || IsVis(nx,ny)) return;
            Vis(nx,ny);
            q.Enqueue((nx,ny,fx,fy));
        }
    }
}

public static class EnemyPlanRecognizer
{
    public static EnemyPlan Analyze(GameState st, int myId)
    {
        int w = st.W, h = st.H;
        int top = 0, bottom = 0, center = 0;
        
        var enemies = st.Agents.Where(ag => ag.Alive && ag.playerId != myId).ToList();
        int enemyCount = enemies.Count;
        if (enemyCount == 0) return EnemyPlan.Unknown;
        double avgX = enemies.Average(ag => ag.X);
        double avgXNorm = avgX / (w - 1);
        double progress = (myId == 0) 
            ? (1.0 - avgXNorm) 
            : avgXNorm;
        foreach (var ag in enemies)
        {
            if (ag.Y < h / 4) top++;
            else if (ag.Y > 3 * h / 4) bottom++;
            else center++;
        }
        double pctCenter  = center      / (double)enemyCount;
        double pctTop     = top         / (double)enemyCount;
        double pctBottom  = bottom      / (double)enemyCount;

        if (Config.DebugEnabled)
        {
            Console.Error.WriteLine(
                $"[DEBUG] EnemyPlan.Analyze:" +
                $" enemies={enemyCount}," +
                $" avgX={avgX:F1}/{w-1} ({avgXNorm:P0})," +
                $" center={center}({pctCenter:P0})," +
                $" top={top}({pctTop:P0})," +
                $" bottom={bottom}({pctBottom:P0})"
            );
        }
        const double RUSH_THRESH   = 0.25;
        const double TURTLE_THRESH = 0.10;
        // 3) progowanie na podstawie procentów
        // Rush = >= 60% wrogów w połowie mapy (przed linią)
        if (progress >= RUSH_THRESH)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: FullRush (forward ≥ 60%)");
            return EnemyPlan.FullRush;
        }

        // Centralny push = >= 60% w centrum
        if (pctCenter >= 0.5)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: CentralPush (center ≥ 60%)");
            return EnemyPlan.CentralPush;
        }

        // Podwójny flank = >= 40% góra i >= 40% dół
        if (pctTop >= 0.4 && pctBottom >= 0.4)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: DoubleFlank (top & bottom ≥ 40%)");
            return EnemyPlan.DoubleFlank;
        }

        // Pojedynczy flank = >= 40% po jednej stronie
        if (pctTop >= 0.4 || pctBottom >= 0.4)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: FlankAttack (one side ≥ 40%)");
            return EnemyPlan.FlankAttack;
        }

        // SplitFlankWithSniper = przynajmniej 1 na top, 1 na bottom i 1 w centrum
        if (top >= 1 && bottom >= 1 && center >= 1)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: SplitFlankWithSniper (1+ top, 1+ bottom, 1+ center)");
            return EnemyPlan.SplitFlankWithSniper;
        }

        if (progress <= TURTLE_THRESH)
        {
            if (Config.DebugEnabled)
                Console.Error.WriteLine("[DEBUG] EnemyPlan: SpawnTurtle (spreadX ≤ 2 && forward < 20%)");
            return EnemyPlan.SpawnTurtle;
        }

        if (Config.DebugEnabled)
            Console.Error.WriteLine("[DEBUG] EnemyPlan: Unknown");
        return EnemyPlan.Unknown;
    }
}

public static class AgentTaskAssigner
{
    public static void AdaptPlans(GameState st, Dictionary<int, AgentPlan> plans, EnemyPlan plan, int myPlayerId)
    {
        int centerX = st.W / 2;
        int centerY = st.H / 2;
        int offset = 0;
        foreach (var (id, p) in plans)
        {
            ref readonly var ag = ref st.Agents[id];
            if (!ag.Alive) continue;
            var cls = GameState.AgentClasses[id];
            offset = (offset + 1) % 4;
            (int tx, int ty) hint = (centerX, centerY + offset - 2);
            switch (plan)
            {
                case EnemyPlan.CentralPush:
                case EnemyPlan.FlankAttack:
                case EnemyPlan.DoubleFlank:
                case EnemyPlan.SplitFlankWithSniper:
                    if (cls == AgentClass.Sniper)
                        p.Task = new AgentTask(AgentTaskType.HoldLine, (centerX, centerY));
                    else
                        p.Task = new AgentTask(AgentTaskType.HoldLine, hint);
                    break;
                case EnemyPlan.SpawnTurtle:
                    p.Task = new AgentTask(AgentTaskType.PushToCover, hint);
                    break;
                case EnemyPlan.FullRush:
                    if (cls == AgentClass.Bomber)
                        p.Task = new AgentTask(AgentTaskType.SupportWithSplash, (centerX, centerY));
                    else if (cls == AgentClass.Sniper)
                        p.Task = new AgentTask(AgentTaskType.HoldLine, (centerX, centerY));
                    else
                        p.Task = new AgentTask(AgentTaskType.HoldLine, hint);
                    break;
                default:
                    p.Task = new AgentTask(AgentTaskType.PushToCover, hint);
                    break;
            }
        }
    }
}
