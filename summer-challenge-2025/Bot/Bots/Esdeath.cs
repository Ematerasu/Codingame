using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SummerChallenge2025.Bot;

public sealed class Esdeath : AI
{
    // ──────────────────────────────────── Tunable weights ────────────────────────────────────
    private readonly double _wTerritory;
    private readonly double _wMyWet;
    private readonly double _wOppWet;
    private readonly double _wKills;
    private readonly double _wDeaths;
    private readonly double _wCover;
    private readonly double _wDistFront;

    // ======= Opponent model =================================================================
    private readonly AI _opponentBot;

    // ──────────────────────────────────── Search params ─────────────────────────────────────
    private readonly int _depth;
    private readonly int _beamWidth;
    private readonly int _kPerAgent;

    // Re‑usable buffer to avoid allocations inside GetLegalOrders
    private readonly AgentOrder[] _ordersBuf = new AgentOrder[256];
    private readonly Node[] _beamBuf;

    private static readonly (sbyte dx, sbyte dy)[] _dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    // ───────────────────────────────── Cover distance cache ────────────────────────────
    private int _mapW;
    private int _mapH;
    private int[] _coverDist = Array.Empty<int>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Idx(int x, int y) => y * _mapW + x;

    public Esdeath(
        // evaluation weights (defaults are the hand‑picked values from the write‑up)
        double wTerritory = 10,
        double wMyWet = 2,
        double wOppWet = -3,
        double wKills = 5,
        double wDeaths = -4,
        double wCover = 1.5,
        double wDistFront = -0.5,
        // search hyper‑parameters
        int depth = 4,
        int beamWidth = 16,
        int kPerAgent = 5,
        AI? opponent = null
    )
    {
        _wTerritory = wTerritory;
        _wMyWet = wMyWet;
        _wOppWet = wOppWet;
        _wKills = wKills;
        _wDeaths = wDeaths;
        _wCover = wCover;
        _wDistFront = wDistFront;
        _depth = depth;
        _beamWidth = beamWidth;
        _kPerAgent = kPerAgent;
        _opponentBot = opponent ?? new CoverBotBit();
        _beamBuf = new Node[_beamWidth * 4]; // 4× slack
    }

    public override TurnCommand GetMove(GameState st)
    {
        int budgetMs = st.Turn == 0 ? 995 : 45;
        var stopwatch = Stopwatch.StartNew();
        if (_coverDist.Length == 0 || st.W != _mapW || st.H != _mapH)
            Profiler.Measure("Esdeath.PrecomputeCover", () => PrecomputeCoverDistances(st));
        int totalNodes = 0;
        int[] layerNodes = new int[_depth + 1];

        var best = Profiler.Wrap("Esdeath.BeamSearch", () => BeamSearch(st, _depth, _beamWidth, stopwatch, budgetMs, ref totalNodes, layerNodes));

        // debug: węzły
        Console.Error.WriteLine($"[DBG] turn {st.Turn}  explored {totalNodes} nodes  byLayer=[{string.Join(",", layerNodes)}]");

        return best;
    }

    private void PrecomputeCoverDistances(GameState st)
    {
        _mapW = st.W;
        _mapH = st.H;
        _coverDist = new int[_mapW * _mapH];
        Array.Fill(_coverDist, int.MaxValue);

        Queue<(int x, int y)> q = new();

        for (int y = 0; y < _mapH; ++y)
            for (int x = 0; x < _mapW; ++x)
            {
                TileType tt = GameState.Tiles[GameState.ToIndex((byte)x, (byte)y)];
                if (tt == TileType.HighCover || tt == TileType.LowCover)
                {
                    _coverDist[Idx(x, y)] = 0;
                    q.Enqueue((x, y));
                }
            }

        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            int baseD = _coverDist[Idx(cx, cy)] + 1;
            foreach (var (dx, dy) in _dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= _mapW || ny >= _mapH) continue;
                int idx = Idx(nx, ny);
                if (baseD < _coverDist[idx])
                {
                    _coverDist[idx] = baseD;
                    q.Enqueue((nx, ny));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DistanceToNearestCover(int x, int y) => _coverDist[Idx(x, y)];

    // ──────────────────────────────────── Beam Search core ──────────────────────────────────
    private TurnCommand BeamSearch(GameState root, int depth, int beamW, Stopwatch sw, int budgetMs, ref int nodeCnt, int[] layerCnt)
    {
        int curCnt = 1;
        _beamBuf[0] = new Node(root, new TurnCommand(GameState.MaxAgents), 0);
        Node best = _beamBuf[0];
        int myId = PlayerId;
        Span<AgentOrder> buf = stackalloc AgentOrder[256];
        for (int d = 0; d < depth; ++d)
        {
            int nextCnt = 0;

            for (int bi = 0; bi < curCnt; ++bi)
            {
                ref Node node = ref _beamBuf[bi];
                if (sw.ElapsedMilliseconds >= budgetMs) return best.First;
                foreach (var myCmd in GenerateJointOrders(node.State))
                {
                    var clone = node.State.FastClone();
                    var rng = new Random();
                    var enemyCmd = _opponentBot.GetMove(clone);

                    clone.ApplyInPlace(myCmd, enemyCmd);

                    double sc = Evaluate(clone, myId);
                    var child = new Node(clone, d == 0 ? myCmd : node.First, sc);

                    nodeCnt++;
                    layerCnt[d + 1]++;

                    if (nextCnt < _beamBuf.Length)
                        _beamBuf[nextCnt++] = child;
                    else if (sc > best.Score) // rare overflow, replace worst later
                        best = child;

                    if (sc > best.Score) best = child;
                }

                if (sw.ElapsedMilliseconds >= budgetMs) return best.First;
            }

            if (nextCnt == 0) break;
            Array.Sort(_beamBuf, 0, nextCnt, NodeComparer.Desc);
            curCnt = nextCnt < _beamWidth ? nextCnt : _beamWidth;
        }

        return best.First;
    }

    // ────────────────────────────── Joint‑order generation w/ heuristics ─────────────────────────────
    private IEnumerable<TurnCommand> GenerateJointOrders(GameState st)
    {
        // Collect my alive agents once.
        int[] ids = new int[GameState.MaxAgents];
        int n = 0;
        for (int id = 0; id < GameState.MaxAgents; ++id)
            if (st.Agents[id].Alive && st.Agents[id].playerId == PlayerId)
                ids[n++] = id;

        // 2. For each agent pick top‑k actions
        AgentOrder[][] perAgent = new AgentOrder[n][];
        int[] perCnt = new int[n];

        for (int i = 0; i < n; ++i)
        {
            int agId = ids[i];
            int cnt = CustomLegalOrders(st, agId, _ordersBuf);

            // top‑k selection (array, O(N*k))
            AgentOrder[] top = new AgentOrder[_kPerAgent];
            double[] topScore = new double[_kPerAgent];
            int tCnt = 0;

            for (int j = 0; j < cnt; ++j)
            {
                double sc = LocalHeuristic(st, agId, _ordersBuf[j]);
                if (tCnt < _kPerAgent)
                {
                    topScore[tCnt] = sc;
                    top[tCnt++] = _ordersBuf[j];
                }
                else
                {
                    // replace worst if better
                    int worstIdx = 0;
                    for (int w = 1; w < _kPerAgent; ++w)
                        if (topScore[w] < topScore[worstIdx]) worstIdx = w;
                    if (sc > topScore[worstIdx])
                    {
                        topScore[worstIdx] = sc;
                        top[worstIdx] = _ordersBuf[j];
                    }
                }
            }
            perAgent[i] = top;
            perCnt[i] = tCnt;
        }
        // Cartesian product via recursive iterator – avoids big temporary lists.
        AgentOrder[] chosen = new AgentOrder[n];
        return Enumerate(0);

        IEnumerable<TurnCommand> Enumerate(int idx)
        {
            if (idx == n)
            {
                var cmd = new TurnCommand(GameState.MaxAgents);
                for (int a = 0; a < n; ++a)
                {
                    int aid = ids[a];
                    cmd.SetMove(aid, chosen[a].Move);
                    cmd.SetCombat(aid, chosen[a].Combat);
                }
                yield return cmd;
                yield break;
            }

            for (int k = 0; k < perCnt[idx]; ++k)
            {
                chosen[idx] = perAgent[idx][k];
                foreach (var c in Enumerate(idx + 1))
                    yield return c;
            }
        }
    }
    
    private int CustomLegalOrders(GameState st, int agentId, Span<AgentOrder> dst)
    {
        Span<AgentOrder> buf = stackalloc AgentOrder[256];
        int cnt = st.GetLegalOrders(agentId, buf);

        Span<AgentOrder> bestOrders = stackalloc AgentOrder[_kPerAgent];
        Span<double>     bestScore  = stackalloc double[_kPerAgent];
        int bestCnt = 0;

        AgentClass cls = GameState.AgentClasses[agentId];

        for (int i = 0; i < cnt; ++i)
        {
            ref readonly var ord = ref buf[i];

            // Sniper – ignorujemy kroki oddalające od covera
            if (cls == AgentClass.Sniper && ord.Move.Type == MoveType.Step && DistanceToNearestCover(ord.Move.X, ord.Move.Y) > 4)
                continue;

            double sc = LocalHeuristic(st, agentId, ord);

            if (bestCnt < _kPerAgent)
            {
                bestOrders[bestCnt] = ord;
                bestScore[bestCnt++] = sc;
            }
            else
            {
                // wyszukaj najgorszy
                int worst = 0;
                for (int w = 1; w < _kPerAgent; ++w)
                    if (bestScore[w] < bestScore[worst]) worst = w;
                if (sc > bestScore[worst])
                {
                    bestOrders[worst] = ord;
                    bestScore[worst] = sc;
                }
            }
        }

        // ✂️ skopiuj do dst
        for (int i = 0; i < bestCnt; ++i)
            dst[i] = bestOrders[i];
        return bestCnt;
    }

    // ─────────────────────────────────── Local (per‑agent) heuristic ──────────────────────────────────
    private double LocalHeuristic(GameState st, int agentId, in AgentOrder ord)
    {
        double score = 0;
        AgentClass cls = GameState.AgentClasses[agentId];

        // Combat type bonus
        switch (ord.Combat.Type)
        {
            case CombatType.Shoot: score += 100; break;
            case CombatType.Throw: score += 80; break;
            case CombatType.Hunker: score += 20; break;
        }

        // Finisher incentive
        if (ord.Combat.Type == CombatType.Shoot)
        {
            int trg = ord.Combat.Arg1;
            if (trg >= 0 && trg < GameState.MaxAgents && st.Agents[trg].Alive && st.Agents[trg].Wetness >= 80)
                score += 50;
        }

        // Movement evaluation
        if (ord.Move.Type == MoveType.Step)
        {
            int nx = ord.Move.X, ny = ord.Move.Y;
            score -= Math.Abs(nx - st.W / 2); // push forward
            if (NeighbourCover(st, nx, ny, out bool high)) score += high ? 20 : 10;
        }

        // Class‑specific tweaks
        if (cls == AgentClass.Bomber && ord.Combat.Type == CombatType.Throw) score += 25;
        return score;
    }

    private static bool NeighbourCover(GameState st, int x, int y, out bool high)
    {
        high = false;
        foreach (var (dx, dy) in _dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (!st.InBounds((byte)nx, (byte)ny)) continue;
            TileType tt = GameState.Tiles[GameState.ToIndex((byte)nx, (byte)ny)];
            if (tt == TileType.HighCover) { high = true; return true; }
            if (tt == TileType.LowCover) return true;
        }
        return false;
    }

    // ─────────────────────────────────── State evaluation ───────────────────────────────────
    private double Evaluate(GameState gs, int myId)
    {
        int territory = myId == 0 ? gs.Score0 - gs.Score1 : gs.Score1 - gs.Score0;
        int myWet = 0, oppWet = 0, myDead = 0, oppDead = 0;
        double coverBonus = 0, distFront = 0;

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            ref readonly var ag = ref gs.Agents[id];
            if (!ag.Alive)
            {
                if (ag.playerId == myId) myDead++; else oppDead++;
                continue;
            }

            if (ag.playerId == myId)
            {
                myWet += 100 - ag.Wetness;
                distFront += Math.Abs(ag.X - gs.W / 2);
                coverBonus += CoverScore(gs, ag);
            }
            else
            {
                oppWet += 100 - ag.Wetness;
            }
        }

        return  _wTerritory * territory +
                _wMyWet     * myWet     +
                _wOppWet    * oppWet    +
                _wKills     * oppDead   +
                _wDeaths    * myDead    +
                _wCover     * coverBonus+
                _wDistFront * distFront;
    }

    private static double CoverScore(GameState gs, in AgentState ag)
    {
        double best = 0;
        foreach (var (dx, dy) in _dirs)
        {
            int nx = ag.X + dx;
            int ny = ag.Y + dy;
            if (nx < 0 || ny < 0 || nx >= gs.W || ny >= gs.H) continue;
            TileType tt = GameState.Tiles[GameState.ToIndex((byte)nx, (byte)ny)];
            if (tt == TileType.HighCover) best = Math.Max(best, 0.75);
            else if (tt == TileType.LowCover) best = Math.Max(best, 0.50);
        }
        return best;
    }

    // ─────────────────────────────────── Helper types ─────────────────────────────────────────
    private readonly record struct Node(GameState State, TurnCommand First, double Score);

    private sealed class NodeComparer : IComparer<Node>
    {
        public static readonly NodeComparer Desc = new();
        public int Compare(Node x, Node y) => y.Score.CompareTo(x.Score);
    }

}
