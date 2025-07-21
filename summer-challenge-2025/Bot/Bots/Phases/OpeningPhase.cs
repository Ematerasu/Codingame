using System.Text;
using System.Linq;

namespace SummerChallenge2025.Bot;

public sealed class OpeningPhase : IGamePhase
{
    private readonly Dictionary<int, AgentPlan> _plans = new();
    private MapAnalyzer? _map;
    private PathPlanner? _planner;
    private HashSet<(int x, int y)>[] _coveredTiles;

    public void Enter(GameState st, int myPlayerId)
    {
        _map = new MapAnalyzer(st, myPlayerId);
        _planner = new PathPlanner(_map, st, myPlayerId);

        CalculateCoveredTiles(st);
        var myAgents = st.Agents
                        .Select((ag, id) => (ag, id))
                        .Where(t => t.ag.Alive && t.ag.playerId == myPlayerId)
                        .OrderBy(t => t.id)
                        .Select(t => t.id)
                        .ToList();

        var reserved = new Dictionary<int, HashSet<(int x, int y)>>();
        var initialOcc = new HashSet<(int x, int y)>(
            myAgents.Select(id => ((int)st.Agents[id].X, (int)st.Agents[id].Y))
        );

        foreach (var id in myAgents)
        {
            var me = st.Agents[id];
            (int tx, int ty) = GameState.AgentClasses[id] switch
            {
                AgentClass.Sniper => _planner.BestSniperSpotForRow(_planner.AssaultRow, me),
                AgentClass.Bomber => (st.W / 2, _planner.AssaultRow),
                _                 => _planner.AssaultTarget()
            };

            var fullPath = _planner.FindFullPath(
                me.X, me.Y,
                tx, ty,
                forbiddenFirstSteps: new HashSet<(int,int)>(),
                initialOccupied: initialOcc
            );
            bool collide;
            do
            {
                collide = false;
                for (int step = 1; step < fullPath.Count; step++)
                {
                    if (reserved.TryGetValue(step, out var occ) &&
                        occ.Contains(fullPath[step]))
                    {
                        // konflikt – wstaw pauzę (czekanie) przed ruchem
                        fullPath.Insert(1, fullPath[0]);
                        collide = true;
                        break;
                    }
                }
            } while (collide);
            for (int step = 1; step < fullPath.Count; step++)
            {
                if (!reserved.TryGetValue(step, out var occ))
                {
                    occ = new HashSet<(int, int)>();
                    reserved[step] = occ;
                }
                occ.Add(fullPath[step]);
            }
            if (Config.DebugEnabled)
            {
                var pathStr = string.Join(" -> ", fullPath
                    .Select(p => $"({p.x},{p.y})"));
                Console.Error.WriteLine($"[DEBUG] Agent {id} path: {pathStr}");
            }

            var q = new Queue<(byte, byte)>();
            foreach (var (x,y) in fullPath.Skip(1))
                q.Enqueue((x, y));

            _plans[id] = new AgentPlan(q, GameState.AgentClasses[id]);
        }

        if (Config.DebugEnabled)
            Console.Error.WriteLine(_map.FormatSniperSpots(15));

        if (Config.DebugEnabled)
        {
            var sb = new StringBuilder("Plans:");
            foreach (var (id, plan) in _plans)
            {
                var tgt = plan.PeekTarget();
                int dist = GameState.Mdist(st.Agents[id].X, st.Agents[id].Y, tgt.x, tgt.y);
                sb.Append($" [{id}:{plan.Class}->{tgt.x},{tgt.y}|d{dist}]");
            }
            Console.Error.WriteLine(sb.ToString());
        }
    }

    private void CalculateCoveredTiles(GameState st)
    {
        _coveredTiles = new HashSet<(int x, int y)>[GameState.MaxW * GameState.MaxH];
        for (int i = 0; i < _coveredTiles.Length; i++)
            _coveredTiles[i] = new HashSet<(int x, int y)>();

        var tiles = GameState.Tiles;
        // For each potential target tile t that is adjacent to at least one cover
        for (int ty = 0; ty < st.H; ty++)
            for (int tx = 0; tx < st.W; tx++)
            {
                int tIdx = GameState.ToIndex(tx, ty);
                if (tiles[tIdx] != TileType.Empty) continue;

                // Check if t has any orthogonal neighbor cover
                bool hasCoverNeighbor = Helpers.Dir4.Any(d =>
                {
                    int cx = tx + d.x, cy = ty + d.y;
                    return st.InBounds(cx, cy) &&
                            (tiles[GameState.ToIndex(cx, cy)] == TileType.LowCover || tiles[GameState.ToIndex(cx, cy)] == TileType.HighCover);
                });
                if (!hasCoverNeighbor) continue;

                // For each possible shooter position s
                for (int sy = 0; sy < st.H; sy++)
                    for (int sx = 0; sx < st.W; sx++)
                    {
                        int sIdx = GameState.ToIndex(sx, sy);
                        if (tiles[sIdx] != TileType.Empty) continue;
                        if (GameState.Cdist(tx, ty, sx, sy) <= 1) continue;

                        double coverMod = 1.0;
                        bool sameCover = false;
                        int dx = tx - sx;
                        int dy = ty - sy;

                        // check x-direction cover
                        if (Math.Abs(dx) > 1)
                        {
                            int adjX = -Math.Sign(dx);
                            int cx = tx + adjX;
                            int cy = ty;
                            int cIdx = GameState.ToIndex(cx, cy);
                            var cType = st.InBounds(cx, cy) ? tiles[cIdx] : TileType.Empty;
                            if ((cType == TileType.LowCover || cType == TileType.HighCover) &&
                                GameState.Cdist(cx, cy, sx, sy) > 1)
                            {
                                coverMod = Math.Min(coverMod, GameState.CoverModifier(cType));
                                int sxCheck = sx - adjX;
                                if (st.InBounds(sxCheck, sy) && GameState.ToIndex(sxCheck, sy) == cIdx)
                                    sameCover = true;
                            }
                        }

                        // check y-direction cover
                        if (Math.Abs(dy) > 1)
                        {
                            int adjY = -Math.Sign(dy);
                            int cx = tx;
                            int cy = ty + adjY;
                            int cIdx = GameState.ToIndex(cx, cy);
                            var cType = st.InBounds(cx, cy) ? tiles[cIdx] : TileType.Empty;
                            if ((cType == TileType.LowCover || cType == TileType.HighCover) &&
                                GameState.Cdist(cx, cy, sx, sy) > 1)
                            {
                                coverMod = Math.Min(coverMod, GameState.CoverModifier(cType));
                                int syCheck = sy - adjY;
                                if (st.InBounds(sx, syCheck) && GameState.ToIndex(sx, syCheck) == cIdx)
                                    sameCover = true;
                            }
                        }

                        if (coverMod < 1.0 && !sameCover)
                            _coveredTiles[tIdx].Add((sx, sy));
                    }
            }
    }

    public TurnCommand GetMove(GameState st)
    {
        var cmd = new TurnCommand(GameState.MaxAgents);
        foreach (var kv in _plans.ToArray())
        {
            int id = kv.Key;
            if (!st.Agents[id].Alive) { _plans.Remove(id); continue; }

            var mv = kv.Value.NextMove(st, id);
            cmd.SetMove(id, mv);

            var cls = kv.Value.Class;
            switch (cls)
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

    public bool ShouldExit(GameState st) => st.W < 18 ? st.Turn >= 3 : st.Turn >= 4; 
    public IGamePhase? GetNextPhase(GameState _) => new DevelopmentPhase(_plans, _planner!.AssaultRow, _coveredTiles, _map!.SniperSpots);
}


public sealed class MapAnalyzer
{
    public readonly List<(int x, int y, int score)> SniperSpots = new();
    public readonly (int x, int y) Center;
    private readonly (double x, double y) _enemyCtr;
    private const int SHOT_RANGE = 12;

    private readonly int w, h;

    public MapAnalyzer(GameState st, int myPlayerId)
    {
        w = st.W; h = st.H; Center = (w / 2, h / 2);
        var tiles = GameState.Tiles;
        var myPos = new List<(int x, int y)>();
        var enemyPos = new List<(int x, int y)>();
        foreach (var ag in st.Agents.Where(a => a.Alive))
            (ag.playerId == myPlayerId ? myPos : enemyPos).Add((ag.X, ag.Y));
        _enemyCtr = (
            enemyPos.Average(p => (double)p.x),
            enemyPos.Average(p => (double)p.y)
        );

        foreach (var pos in EmptyTilesAdjacentToCover(tiles, w, h))
        {
            int rawScore = 0;

            foreach (var dir in Helpers.Dir4)
            {
                int cx = pos.x + dir.x, cy = pos.y + dir.y;
                if (!InBounds(cx, cy, w, h)) continue;

                var cover = tiles[GameState.ToIndex((byte)cx, (byte)cy)];
                if (cover is not (TileType.LowCover or TileType.HighCover)) continue;

                double coverWeight = cover == TileType.LowCover ? 1 : 2;
                var toEnemy = (_enemyCtr.x - pos.x, _enemyCtr.y - pos.y);
                double dot = dir.x * toEnemy.Item1 + dir.y * toEnemy.Item2;
                coverWeight *= dot > 0 ? 1.0 : 0.2;

                for (int step = 1; step <= SHOT_RANGE; step++)
                {
                    int sx = pos.x + dir.x * (step + 1);
                    int sy = pos.y + dir.y * (step + 1);
                    if (!InBounds(sx, sy, w, h)) break;

                    var tile = tiles[GameState.ToIndex(sx, sy)];
                    if (tile is TileType.LowCover or TileType.HighCover)
                        break;

                    rawScore += (int)coverWeight;
                }
            }
            if (rawScore == 0) continue;
            int centerPenalty = GameState.Mdist(pos.x, pos.y, Center.x, Center.y);
            int distToTeam = myPos.Min(p => GameState.Mdist(pos.x, pos.y, p.x, p.y));
            int distToEnemy = GameState.Mdist(pos.x, pos.y, (int)_enemyCtr.x, (int)_enemyCtr.y);


            int score = rawScore * 10
                        - centerPenalty * 2
                        - distToTeam
                        - distToEnemy;
            SniperSpots.Add((pos.x, pos.y, score));
        }
        SniperSpots.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : (a.x + a.y).CompareTo(b.x + b.y));
    }

    public string FormatSniperSpots(int top = 10)
        => string.Join(" ", SniperSpots.Take(top).Select(p => $"({p.x},{p.y}:{p.score})"));

    private static IEnumerable<(int x, int y)> EmptyTilesAdjacentToCover(TileType[] tiles, int w, int h)
    {
        foreach (var (x, y) in GridIndices(w, h))
        {
            if (tiles[GameState.ToIndex(x, y)] != TileType.Empty) continue;
            foreach (var dir in Helpers.Dir4)
            {
                int nx = x + dir.x, ny = y + dir.y;
                if (!InBounds(nx, ny, w, h)) continue;
                if (tiles[GameState.ToIndex(nx, ny)] is TileType.LowCover or TileType.HighCover)
                { yield return (x, y); break; }
            }
        }
    }

    private static IEnumerable<(int x, int y)> GridIndices(int w, int h)
    {
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) yield return (x, y);
    }
    private static bool InBounds(int x, int y, int w, int h)
        => (uint)x < w && (uint)y < h;
}

public sealed class PathPlanner
{
    private readonly MapAnalyzer map;
    private readonly GameState st;
    private readonly int _spawnSide;          // -1 = lewy brzeg, +1 = prawy brzeg
    private readonly int _assaultRow;         // Y-rząd, którym idzie drużyna
    public int AssaultRow => _assaultRow;
    public PathPlanner(MapAnalyzer m, GameState s, int myId)
    {
        map = m; st = s;

        //  ❱❱ 1. wyznacz stronę spawnu (średnie X własnych agentów)
        double myAvgX = st.Agents.Where(a => a.Alive && a.playerId == myId)
                                    .Average(a => (double)a.X);
        _spawnSide = myAvgX < st.W / 2.0 ? -1 : +1;

        //  ❱❱ 2. wybierz najlepszy wiersz (osi Y) dla Assault-Squadu
        _assaultRow = PickAssaultRow();
    }

    public List<(byte x, byte y)> FindFullPath(
        int sx, int sy,
        int tx, int ty,
        ISet<(int x, int y)> forbiddenFirstSteps,
        ISet<(int x, int y)> initialOccupied
    ) {
        var w = st.W;
        var h = st.H;
        var visited = new bool[w, h];
        var parent = new (int px, int py, int fx, int fy)?[w, h];
        var q = new Queue<(int x, int y)>();
        visited[sx, sy] = true;
        parent[sx, sy] = null;
        q.Enqueue((sx, sy));

        while (q.Count > 0) {
            var (x, y) = q.Dequeue();
            if (x == tx && y == ty) break;
            foreach (var (dx, dy) in Helpers.Dir4) {
                int nx = x + dx, ny = y + dy;
                if (!st.InBounds(nx, ny)) continue;
                if (visited[nx, ny]) continue;
                // tylko puste pola
                if (GameState.Tiles[GameState.ToIndex(nx, ny)] != TileType.Empty)
                    continue;
                // jeżeli to jest pierwszy krok (x==sx, y==sy), to sprawdź zakazy
                if (x == sx && forbiddenFirstSteps.Contains((nx, ny)))
                    continue;
                // nie daj się wpakować na czyjąś startową pozycję
                if (x == sx && initialOccupied.Contains((nx, ny)))
                    continue;

                visited[nx, ny] = true;
                // zapamiętujemy: parent[nx,ny] = (poprzedni X, poprzedni Y, pierwszyKrokX, pierwszyKrokY)
                (int fx, int fy) first = (x == sx ? (nx, ny) : (parent[x, y]!.Value.fx, parent[x, y]!.Value.fy));
                parent[nx, ny] = (x, y, first.fx, first.fy);
                q.Enqueue((nx, ny));
            }
        }

        // zbuduj ścieżkę wstecz:
        if (!visited[tx, ty]) return new List<(byte, byte)> { ((byte)sx, (byte)sy) }; 
        var path = new List<(byte, byte)>();
        int cx = tx, cy = ty;
        while (cx != sx || cy != sy) {
            path.Add(((byte)cx, (byte)cy));
            var p = parent[cx, cy]!.Value;
            cx = p.px; cy = p.py;
        }
        path.Reverse();
        // dopisz start
        path.Insert(0, ((byte)sx, (byte)sy));
        return path;
    }

    // główna metoda budująca kolejkę (1-elementową) targetu
    public Queue<(byte x, byte y)> BuildPath(int id, AgentClass cls)
    {
        var me = st.Agents[id];
        (int tx, int ty) target = cls switch
        {
            AgentClass.Sniper => BestSniperSpotForRow(_assaultRow, me),
            AgentClass.Bomber => (st.W / 2, _assaultRow),
            _ => AssaultTarget(),   // Gunner / Assault / Berserker
        };

        var q = new Queue<(byte, byte)>();
        q.Enqueue(((byte)target.tx, (byte)target.ty));
        return q;
    }

    /* ─── logika drużynowa ───────────────────────────────────────────────── */

    // Docelowe pole dla Assault-Squadu – pusty tile przy coverze,
    // w połowie planszy, po naszej stronie.
    public (int tx, int ty) AssaultTarget()
    {
        int centerX = st.W / 2 - _spawnSide;

        // szukamy pustych pól w _assaultRow w promieniu 3 od centerX,
        // priorytet: adjacent-cover > puste
        for (int dx = 0; dx <= 3; dx++)
        {
            foreach (int sign in new[] { 0, -dx, dx })
            {
                int x = centerX + sign;
                if ((uint)x >= st.W) continue;
                int idx = GameState.ToIndex(x, _assaultRow);
                if (GameState.Tiles[idx] != TileType.Empty) continue;

                // czy obok cover?
                bool nearCover = Helpers.Dir4.Any(d =>
                {
                    int nx = x + d.x, ny = _assaultRow + d.y;
                    return (uint)nx < st.W && (uint)ny < st.H &&
                           GameState.Tiles[GameState.ToIndex(nx, ny)] is TileType.LowCover or TileType.HighCover;
                });

                if (nearCover || dx == 3)      // preferuj cover, ale nie wisz na zawsze
                    return (x, _assaultRow);
            }
        }
        return (centerX, _assaultRow);        // fallback – nigdy nie powinno się zdarzyć
    }

    // Najlepsza pozycja snajpera, ALE preferujemy wiersz assaultRow
    public (int tx, int ty) BestSniperSpotForRow(int row, AgentState me)
    {
        var sameRow = map.SniperSpots.Where(p => (p.y == row) || (p.y == row + 1) || (p.y == row - 1)).ToList();
        var pool = sameRow.Count > 0 ? sameRow : map.SniperSpots;
        if (pool.Count == 0) return (st.W / 2, _assaultRow);
        return pool.OrderByDescending(p => p.score)
                    .ThenBy(p => GameState.Mdist(me.X, me.Y, p.x, p.y))
                    .Select(p => (p.x, p.y))
                    .First();
    }

    // heurystyka wyboru assaultRow: najwięcej coverów w linii poziomej,
    // ale tylko w „naszej połowie” planszy (x ≤ W/2  lub  ≥ W/2)
    private int PickAssaultRow()
    {
        int w = st.W, h = st.H, half = w / 2;
        int bestRow = st.H / 2, bestScore = -1;

        for (int y = 0; y < h; y++)
        {
            int score = 0;
            for (int x = 0; x < w; x++)
            {
                if (_spawnSide == -1 && x > half) continue;
                if (_spawnSide == +1 && x < half) continue;

                var tile = GameState.Tiles[GameState.ToIndex(x, y)];
                if (tile is TileType.LowCover or TileType.HighCover) score++;
            }
            if (score > bestScore)
            { bestScore = score; bestRow = y; }
        }
        return bestRow;
    }
}

public sealed class AgentPlan
{
    private readonly Queue<(byte x, byte y)> _path;
    public AgentClass Class { get; }
    public AgentTask? Task { get; set; }

    public bool HasPendingTarget => _path.Count > 0;
    public (byte x, byte y)? PeekTargetOrNull()
        => _path.Count > 0 ? _path.Peek() : null;

    public AgentPlan(Queue<(byte, byte)> path, AgentClass cls, AgentTask? task = null)
    { _path = path; Class = cls; Task = task; }

    public MoveAction NextMove(GameState st, int id)
    {
        var me = st.Agents[id];
        if (Task != null)
        {
            switch (Task.Type)
            {
                case AgentTaskType.Flank:
                    if (Task.TargetHint is (int tx, int ty))
                    {
                        Console.Error.WriteLine($"[DEBUG] Agent {id} flank task: {Task.TargetHint}");
                        var flankY = ty + (me.Y < ty ? 1 : -1);
                        OverrideTarget(tx, flankY);
                    }
                    break;
                case AgentTaskType.PushToCover:
                    if (Task.TargetHint is (int tx3, int ty3))
                    {
                        Console.Error.WriteLine($"[DEBUG] Agent {id} push task: {Task.TargetHint}");
                        OverrideTarget(tx3, ty3);
                    }
                    else
                    {
                        Console.Error.WriteLine($"[DEBUG] Agent {id} push task: center");
                        OverrideTarget(st.W / 2, st.H / 2);
                    }
                    break;
                case AgentTaskType.HoldLine:
                    if (Task.TargetHint is (int tx4, int ty4))
                    {
                        Console.Error.WriteLine($"[DEBUG] Agent {id} hold line: {Task.TargetHint}");
                        if (GameState.Mdist(me.X, me.Y, tx4, ty4) > 1)
                            OverrideTarget(tx4, ty4);
                    }
                    break;
                case AgentTaskType.SupportWithSplash:
                    Console.Error.WriteLine($"[DEBUG] Agent {id} support with splash: {Task.TargetHint}");
                    if (Task.TargetHint is (int tx5, int ty5))
                        OverrideTarget(tx5, ty5);
                    else
                        OverrideTarget(st.W / 2, st.H / 2);
                    break;
            }
        }
        if (_path.Count == 0) return new MoveAction(MoveType.Step, me.X, me.Y);
        var (x, y) = _path.Peek();

        if (me.X == x && me.Y == y) { _path.Dequeue(); return new MoveAction(MoveType.Step, me.X, me.Y); }
        if (st.AgentAt(x, y) != -1) return new MoveAction(MoveType.Step, me.X, me.Y);
        return new MoveAction(MoveType.Step, x, y);
    }
    public (byte x, byte y) PeekTarget()
        => _path.Count > 0 ? _path.Peek() : ((byte)0, (byte)0);

    public void OverrideTarget(int x, int y)
    {
        _path.Clear();
        _path.Enqueue(((byte)x, (byte)y));
    }

}


public static class Helpers
{
    public static readonly (int x, int y)[] Dir4 =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public static bool TrySniperShoot(GameState st, int id, ref TurnCommand cmd)
    {
        var me = st.Agents[id];
        if (me.Cooldown > 0) return false;
        int best = -1, bestWet = -1;
        for (int i = 0; i < GameState.MaxAgents; ++i)
        {
            ref readonly var trg = ref st.Agents[i];
            if (i == id || !trg.Alive || trg.playerId == me.playerId) continue;
            int d = GameState.Mdist(me.X, me.Y, trg.X, trg.Y);
            if (d > 12) continue;
            if (best == -1 || trg.Wetness > bestWet) { best = i; bestWet = trg.Wetness; }
        }
        if (best != -1)
        {
            cmd.SetCombat(id, new CombatAction(CombatType.Shoot, (ushort)best));
            return true;
        }
        return false;
    }

    public static void TryOpportunisticThrow(GameState st, int id, ref TurnCommand cmd)
    {
        var me = st.Agents[id];
        if (me.SplashBombs == 0) return;
        int bestX = -1, bestY = -1, bestCount = 0;
        for (int y = 0; y < st.H; y++)
            for (int x = 0; x < st.W; x++)
            {
                if (Math.Abs(x - me.X) + Math.Abs(y - me.Y) > 4) continue;
                int cnt = 0, friendly = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((uint)nx >= st.W || (uint)ny >= st.H) continue;
                        int aid = st.AgentAt((byte)nx, (byte)ny);
                        if (aid == -1) continue;
                        if (st.Agents[aid].playerId == me.playerId) friendly++;
                        else cnt++;
                    }
                if (friendly == 0 && cnt > bestCount)
                {
                    bestCount = cnt; bestX = x; bestY = y;
                }
            }
        if (bestCount >= 2)
            cmd.SetCombat(id, new CombatAction(CombatType.Throw, (ushort)bestX, (byte)bestY));
    }

    public static bool TryShootAt(GameState st, int id, int targetId, ref TurnCommand cmd)
    {
        ref readonly var me = ref st.Agents[id];
        if (me.Cooldown > 0) return false;

        ref readonly var trg = ref st.Agents[targetId];
        if (!trg.Alive) return false;

        int rangeMax = AgentUtils.Stats[GameState.AgentClasses[id]].OptimalRange * 2;
        if (GameState.Mdist(me.X, me.Y, trg.X, trg.Y) > rangeMax) return false;

        /* ► oszacuj faktyczny dmg po coverze */
        double cover = GetCoverModifier(id, targetId, st);     // 1.0 / 0.5 / 0.25
        int soak = AgentUtils.Stats[GameState.AgentClasses[id]].SoakingPower;
        double dmg = soak * cover;

        const int MinWetness = 12;                              // heurystyczny próg
        if (dmg < MinWetness) return false;                          // strata czasu

        cmd.SetCombat(id, new CombatAction(CombatType.Shoot, (ushort)targetId));
        return true;
    }

    public static double GetCoverModifier(int shooterId, int targetId, GameState st)
    {
        ref readonly var sh = ref st.Agents[shooterId];
        ref readonly var tg = ref st.Agents[targetId];

        int dx = tg.X - sh.X;
        int dy = tg.Y - sh.Y;
        double best = 1.0;

        foreach (var (ox, oy) in new[] { (Math.Sign(dx), 0), (0, Math.Sign(dy)) })
        {
            if (ox == 0 && oy == 0) continue;

            int cx = tg.X - ox;                       // kratka covera między nami?
            int cy = tg.Y - oy;
            if (GameState.Cdist(cx, cy, sh.X, sh.Y) <= 1) continue;   // za blisko

            if (!st.InBounds(cx, cy)) continue;

            var tile = GameState.Tiles[GameState.ToIndex(cx, cy)];
            double mod = tile switch
            {
                TileType.LowCover => 0.5,
                TileType.HighCover => 0.25,
                _ => 1.0
            };
            best = Math.Min(best, mod);
        }
        return best;
    }
    
    public static bool HasCoverNearby(GameState st, int x, int y)
    {
        foreach (var (dx, dy) in Dir4)
        {
            int nx = x + dx, ny = y + dy;
            if (!st.InBounds(nx, ny)) continue;
            var tile = GameState.Tiles[GameState.ToIndex(nx, ny)];
            if (tile == TileType.LowCover || tile == TileType.HighCover)
                return true;
        }
        return false;
    }
}