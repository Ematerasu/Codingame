namespace SummerChallenge2025.Bot;

public sealed class CombatPhase : IGamePhase
{
    private int _myId = -1;
    private HashSet<(int x, int y)>[] _coveredTiles;
    private EnemyPlan _currentPlan = EnemyPlan.Unknown;
    private readonly Dictionary<int, AgentPlan> _plans;
    private static readonly bool DBG = Config.DebugEnabled;

    public CombatPhase(HashSet<(int x, int y)>[] coveredTiles, EnemyPlan currentPlan, IReadOnlyDictionary<int, AgentPlan> prev)
    {
        _coveredTiles = coveredTiles;
        _currentPlan = currentPlan;
        _plans = new Dictionary<int, AgentPlan>(prev);
    }

    public void Enter(GameState st, int myPlayerId)
    {
        _myId = myPlayerId;
    }

    public TurnCommand GetMove(GameState st)
    {
        Stance stance = ComputeStance(st);
        Span<AgentRole> roles = stackalloc AgentRole[GameState.MaxAgents];
        AssignRoles(st, stance, roles);
        if (DBG) Console.Error.WriteLine($"[Stance] {stance}");

        Span<AgentOrder> buf = stackalloc AgentOrder[512];
        MoveChoice[][] choice = new MoveChoice[GameState.MaxAgents][];
        int[] nChoice = new int[GameState.MaxAgents];

        for (int id = 0; id < GameState.MaxAgents; id++)
        {
            ref var ag = ref st.Agents[id];
            if (!ag.Alive || ag.playerId != _myId) continue;

            int n = st.GetLegalOrders(id, buf);
            nChoice[id] = Math.Min(n, 8);
            choice[id] = new MoveChoice[nChoice[id]];

            switch (roles[id])
            {
                case AgentRole.Anchor: PlanAnchor(st, id, buf[..n], choice[id]); break;
                case AgentRole.Breaker: PlanBreaker(st, id, buf[..n], choice[id]); break;
                case AgentRole.Skirmisher: PlanSkirmisher(st, id, buf[..n], choice[id]); break;
                case AgentRole.Flanker: PlanFlanker(st, id, buf[..n], choice[id]); break;
            }
        }
        var cmd = new TurnCommand(GameState.MaxAgents);
        SolveConflicts(st, roles, choice, nChoice, ref cmd);
        if (DBG) Console.Error.WriteLine($"[Combat] ---- EndTurn {st.Turn} ----");
        return cmd;
    }
    public bool ShouldExit(GameState _) => false;
    public IGamePhase? GetNextPhase(GameState _) => null;

    // ---------------- role assignment ----------------
    private enum Stance { OFFENSIVE, SKIRMISH, DEFENSIVE, ALL_IN }
    private enum AgentRole { Anchor, Breaker, Skirmisher, Flanker }

    private Stance ComputeStance(GameState st)
    {
        int myAlive = 0, enemyAlive = 0;
        for (int i = 0; i < GameState.MaxAgents; i++)
            if (st.Agents[i].Alive)
                if (st.Agents[i].playerId == _myId) myAlive++;
                else enemyAlive++;

        int tileDiff = TerritoryDiff(st, _myId);
        if (myAlive >= enemyAlive + 1 || _currentPlan == EnemyPlan.SpawnTurtle)
            return Stance.ALL_IN;

        if (tileDiff >= 20 && myAlive >= enemyAlive)
            return Stance.OFFENSIVE;
        if (tileDiff <= -40 || myAlive < enemyAlive - 1)
            return Stance.DEFENSIVE;
        return Stance.SKIRMISH;
    }

    private void AssignRoles(GameState st, Stance s, Span<AgentRole> roles)
    {
        for (int id = 0; id < GameState.MaxAgents; id++)
        {
            var cls = GameState.AgentClasses[id];
            roles[id] = cls switch
            {
                AgentClass.Sniper => AgentRole.Anchor,
                AgentClass.Bomber when s == Stance.ALL_IN
                                                => AgentRole.Breaker,
                AgentClass.Berserker when s == Stance.ALL_IN
                                                => AgentRole.Breaker,
                AgentClass.Assault => AgentRole.Skirmisher,
                AgentClass.Gunner => AgentRole.Skirmisher,
                _ => AgentRole.Flanker
            };
        }
    }

    private struct MoveChoice
    {
        public AgentOrder Ord;
        public double Score;
        public double[] Breakdown;   // front, cover, dmg, prox, ...
    }

    private void PlanAnchor(GameState st, int id, ReadOnlySpan<AgentOrder> ords, Span<MoveChoice> outArr)
    {
        ref readonly var me = ref st.Agents[id];
        var cls = GameState.AgentClasses[id];
        var stats = AgentUtils.Stats[cls];

        (AgentOrder ord, double score, double[] br)[] tmp = new (AgentOrder, double, double[])[ords.Length];
        int count = 0;
        foreach (ref readonly var ord in ords)
        {
            byte nx = ord.Move.X, ny = ord.Move.Y;
            int idxTile = GameState.ToIndex(nx, ny);

            /* 1. coverScore */
            int coveredEnemies = 0;
            foreach (var (ex, ey) in _coveredTiles[idxTile])
            {
                int aid = st.AgentAt((byte)ex, (byte)ey);
                if (aid == -1) continue;
                ref readonly var enemy = ref st.Agents[aid];
                if (!enemy.Alive || enemy.playerId == _myId) continue;
                coveredEnemies++;
            }
            double coverScore = coveredEnemies * 5.0;

            /* 3. rangeScore  – sweet-spot przy optymalnym zasięgu */
            int dist = DistanceToClosestEnemy(st, _myId, nx, ny);
            double rangeScore = -Math.Abs(dist - stats.OptimalRange);

            /* 4. combatScore / penalty */
            var (combatScore, penalty) =
                EvaluateCombatForAnchor(st, id, nx, ny, ord.Combat, stats);

            double total = coverScore + rangeScore
                           + combatScore + penalty;           // penalty ujemny

            tmp[count++] = (ord, total, new[] { coverScore, rangeScore, combatScore, penalty });
        }

        int filled = 0;
        for (int k = 0; k < 8 && k < count; k++)
        {
            int bestIdx = -1; double bestScore = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (tmp[i].score > bestScore)
                { bestScore = tmp[i].score; bestIdx = i; }

            if (bestIdx == -1) break;

            outArr[filled++] = new MoveChoice
            {
                Ord       = tmp[bestIdx].ord,
                Score     = tmp[bestIdx].score,
                Breakdown = tmp[bestIdx].br
            };

            tmp[bestIdx].score = double.NegativeInfinity;
        }
    }
    private void PlanBreaker(GameState st,
                            int id,
                            ReadOnlySpan<AgentOrder> ords,
                            Span<MoveChoice> outArr)
    {
        ref readonly var me   = ref st.Agents[id];
        var cls               = GameState.AgentClasses[id];
        var stats             = AgentUtils.Stats[cls];
        bool isBomber         = cls == AgentClass.Bomber;
        bool isBerserker      = cls == AgentClass.Berserker;

        /* --- 1. wyznacz cel: centroid wrogiej drużyny ------------------ */
        double exSum = 0, eySum = 0; int enemyCnt = 0;
        for (int i = 0; i < GameState.MaxAgents; i++)
            if (st.Agents[i].Alive && st.Agents[i].playerId != _myId)
            { exSum += st.Agents[i].X; eySum += st.Agents[i].Y; enemyCnt++; }

        double goalX = enemyCnt > 0 ? exSum / enemyCnt : me.X;
        double goalY = enemyCnt > 0 ? eySum / enemyCnt : me.Y;

        /* --- 2. oceniaj wszystkie legalne ruchy ------------------------ */
        (AgentOrder ord, double score, double[] br)[] tmp = new (AgentOrder, double, double[])[ords.Length];
        int count = 0;

        foreach (ref readonly var ord in ords)
        {
            byte nx = ord.Move.X, ny = ord.Move.Y;

            /* 2a. front/aggression – skracanie dystansu do centroidu */
            double before = Math.Abs(me.X - goalX) + Math.Abs(me.Y - goalY);
            double after  = Math.Abs(nx   - goalX) + Math.Abs(ny   - goalY);
            double front  = (before - after) * 5.0;      // silna waga

            /* 2b. combat & ewentualne kary */
            var (combat, penalty) =
                EvaluateCombatForBreaker(st, id, nx, ny, ord.Combat,
                                        stats, isBomber);

            /* 2c. kara za stanie w miejscu bez akcji */
            double stuck = (nx == me.X && ny == me.Y &&
                            ord.Combat.Type == CombatType.None) ? -4.0 : 0.0;

            double total = front + combat + penalty + stuck;
            tmp[count++] = (ord, total,
                        new[] { front, combat, penalty, stuck });
        }

        /* --- 3. wybierz TOP-8 ----------------------------------------- */
        int filled = 0;
        for (int k = 0; k < 8 && k < count; k++)
        {
            int best = -1; double bestScr = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (tmp[i].score > bestScr)
                { bestScr = tmp[i].score; best = i; }

            if (best == -1) break;

            outArr[filled++] = new MoveChoice
            {
                Ord       = tmp[best].ord,
                Score     = tmp[best].score,
                Breakdown = tmp[best].br
            };
            tmp[best].score = double.NegativeInfinity;
        }
    }
    private void PlanSkirmisher(GameState st,
                                int id,
                                ReadOnlySpan<AgentOrder> ords,
                                Span<MoveChoice> outArr)
    {
        ref readonly var me    = ref st.Agents[id];
        var cls                = GameState.AgentClasses[id];
        var stats              = AgentUtils.Stats[cls];

        /* --- 1. centroid przeciwnika (do frontu) ----------------------- */
        double ex = 0, ey = 0; int enemyCnt = 0;
        foreach (var ag in st.Agents)
            if (ag.Alive && ag.playerId != _myId)
            { ex += ag.X; ey += ag.Y; enemyCnt++; }
        ex /= enemyCnt == 0 ? 1 : enemyCnt;
        ey /= enemyCnt == 0 ? 1 : enemyCnt;

        /* --- 2. evaluate wszystkie ruchy ------------------------------- */
        var tmp = new (AgentOrder ord,double score,double[] br)[ords.Length];
        int count = 0;

        foreach (ref readonly var ord in ords)
        {
            byte nx = ord.Move.X, ny = ord.Move.Y;
            int  idxTile = GameState.ToIndex(nx, ny);

            /*  frontGain  */
            double front = (Math.Abs(me.X - ex) + Math.Abs(me.Y - ey) -
                            (Math.Abs(nx - ex) + Math.Abs(ny - ey))) * 3.0;

            /*  coverScore */
            int coveredEnemies = _coveredTiles[idxTile].Count(t =>
            {
                int aid = st.AgentAt((byte)t.x,(byte)t.y);
                return aid != -1 && st.Agents[aid].Alive && st.Agents[aid].playerId != _myId;
            });
            double cover = coveredEnemies * 4.0;

            /*  range sweet-spot  */
            int dist = DistanceToClosestEnemy(st, _myId, nx, ny);
            double range = -Math.Abs(dist - stats.OptimalRange);

            /*  proximity penalty  */
            int alliesNear = 0;
            for (int a=0;a<GameState.MaxAgents;a++)
                if (a!=id && st.Agents[a].Alive && st.Agents[a].playerId==_myId &&
                    GameState.Mdist(nx,ny,st.Agents[a].X,st.Agents[a].Y)<=2)
                    alliesNear++;
            double proxPen = -alliesNear * 3.0;

            /*  combat & ff-penalty  */
            var (combat, ffPen) = EvaluateCombatForSkirmisher(
                                    st, id, nx, ny, ord.Combat, stats);

            /*  stuck  */
            double stuck = (nx==me.X && ny==me.Y &&
                            ord.Combat.Type==CombatType.None)? -3:0;

            double total = front+cover+range+proxPen+combat+ffPen+stuck;

            tmp[count++] = (ord,total,
                        new[]{front,cover,range,proxPen,combat,ffPen,stuck});
        }

        /* --- 3. TOP-8 --------------------------------------------------- */
        int filled=0;
        for(int k=0;k<8&&k<count;k++)
        {
            int best=-1; double bestScore=double.NegativeInfinity;
            for(int i=0;i<count;i++)
                if(tmp[i].score>bestScore){bestScore=tmp[i].score;best=i;}

            if(best==-1)break;
            outArr[filled++]=new MoveChoice{
                Ord=tmp[best].ord, Score=tmp[best].score, Breakdown=tmp[best].br};
            tmp[best].score=double.NegativeInfinity;
        }
    }
    private void PlanFlanker(GameState st,
                            int id,
                            ReadOnlySpan<AgentOrder> ords,
                            Span<MoveChoice> outArr)
    {
        ref readonly var me   = ref st.Agents[id];
        var stats             = AgentUtils.Stats[GameState.AgentClasses[id]];

        /* --- 1. wyznacz flank-lane ------------------------------------ */
        int topLane = 1, botLane = st.H - 2;
        bool useTop = (st.H < 8) ? (id % 2 == 0) : id % 4 < 2;   // proste rozłożenie
        int laneY   = useTop ? topLane : botLane;

        /* target X 2 kolumny w głąb wrogiej połowy */
        int goalX = (_myId == 0) ? st.W - 3 : 2;

        /* --- 2. oceń każdy ruch --------------------------------------- */
        var tmp = new (AgentOrder ord,double scr,double[] br)[ords.Length];
        int cnt = 0;

        foreach (ref readonly var ord in ords)
        {
            byte nx = ord.Move.X, ny = ord.Move.Y;
            int  idx = GameState.ToIndex(nx, ny);

            /* lane alignment */
            double laneScore = -Math.Abs(ny - laneY) * 4.0;

            /* advance forward/backward */
            int before = (_myId == 0) ? me.X : st.W - 1 - me.X;
            int after  = (_myId == 0) ? nx   : st.W - 1 - nx;
            double advance = (after - before) * 3.0;   // większe X = głębiej

            /* cover */
            int covered = _coveredTiles[idx].Count(t=>{
                int aid=st.AgentAt((byte)t.x,(byte)t.y);
                return aid!=-1&&st.Agents[aid].Alive&&st.Agents[aid].playerId!=_myId;
            });
            double cover = covered * 3.0;

            /* proximity penalty */
            int allyNear=0;
            for(int a=0;a<GameState.MaxAgents;a++)
                if(a!=id&&st.Agents[a].Alive&&st.Agents[a].playerId==_myId&&
                GameState.Mdist(nx,ny,st.Agents[a].X,st.Agents[a].Y)<=2)
                allyNear++;
            double proxPen = -allyNear * 2.0;

            /* combat + ff */
            var (combat, ffPen) = EvaluateCombatForFlanker(
                                    st,id,nx,ny,ord.Combat,stats);

            double stuck = (nx==me.X && ny==me.Y &&
                            ord.Combat.Type==CombatType.None)? -3:0;

            double total = laneScore+advance+cover+proxPen+combat+ffPen+stuck;
            tmp[cnt++] = (ord,total,new[]{
                laneScore,advance,cover,proxPen,combat,ffPen,stuck});
        }

        /* --- 3. wybierz TOP-8 ----------------------------------------- */
        int filled=0;
        for(int k=0;k<8&&k<cnt;k++)
        {
            int best=-1; double bestS=double.NegativeInfinity;
            for(int i=0;i<cnt;i++)
                if(tmp[i].scr>bestS){bestS=tmp[i].scr;best=i;}
            if(best==-1)break;
            outArr[filled++]=new MoveChoice{
                Ord=tmp[best].ord, Score=tmp[best].scr, Breakdown=tmp[best].br};
            tmp[best].scr=double.NegativeInfinity;
        }
    }

    private void SolveConflicts(GameState         st,
                                Span<AgentRole>   roles,
                                MoveChoice[][]    choice,
                                int[]             nChoice,
                                ref TurnCommand   cmd)
    {
        /* 1. kolejność agentów wg priorytetu roli, potem id rosnąco ---- */
        int[] order = new int[GameState.MaxAgents];
        int   oCnt  = 0;
        for (int id = 0; id < GameState.MaxAgents; id++)
            if (st.Agents[id].Alive && st.Agents[id].playerId == _myId)
                order[oCnt++] = id;

        int RoleRank(AgentRole r) => r switch
        {
            AgentRole.Breaker    => 0,
            AgentRole.Skirmisher => 1,
            AgentRole.Anchor     => 2,
            _                    => 3            // Flanker
        };

        for (int i = 0; i < oCnt - 1; i++)
        {
            int best = i;
            for (int j = i + 1; j < oCnt; j++)
            {
                int cmp = RoleRank(roles[order[j]]).CompareTo(RoleRank(roles[order[best]]));
                if (cmp < 0 || (cmp == 0 && order[j] < order[best]))
                    best = j;
            }
            if (best != i)
            {
                int tmp = order[i];
                order[i] = order[best];
                order[best] = tmp;
            }
        }

        /* 2. zarezerwowane cele (dst) oraz mapping dst→src -------------- */
        var reserved = new HashSet<(byte x, byte y)>();
        var destOfId = new (byte x, byte y)[GameState.MaxAgents];

        /* 3. przejdź po agentach w ustalonej kolejności ----------------- */
        foreach (int id in order)
        {
            if (choice[id] == null || nChoice[id] == 0)
            {
                ref readonly var me = ref st.Agents[id];
                cmd.SetMove  (id, new MoveAction(MoveType.Step, me.X, me.Y));
                cmd.SetCombat(id, new CombatAction(CombatType.None));
                reserved.Add((me.X, me.Y));
                destOfId[id] = (me.X, me.Y);
                if (DBG) Console.Error.WriteLine($"[A{id}:{roles[id]}] NO-CHOICE – stay");
                continue;
            }
            bool issued = false;

            if (nChoice[id] > 1)
            {
                Array.Sort(choice[id], 0, nChoice[id],
                        Comparer<MoveChoice>.Create(
                            (m1, m2) => m2.Score.CompareTo(m1.Score)));
            }

            for (int k = 0; k < nChoice[id]; k++)
            {
                var mv = choice[id][k].Ord.Move;
                var dst = (mv.X, mv.Y);

                /* 3a. czy ruch powoduje kolizję z już wybranymi? */
                bool clash = reserved.Contains(dst);

                /* 3b. swap-kolizja (ja→dst, ktoś już idzie dst→moja pozycja) */
                foreach (int j in order)
                    if (destOfId[j] == (st.Agents[id].X, st.Agents[id].Y) &&
                        dst == destOfId[j])
                    { clash = true; break; }

                if (clash) continue;        // spróbuj następny kandydat

                /* 3c. ruch przyjęty -> zapis do cmd, rezerwacje */
                cmd.SetMove  (id, mv);
                cmd.SetCombat(id, choice[id][k].Ord.Combat);

                reserved.Add(dst);
                destOfId[id] = dst;
                issued = true;

                if (DBG)
                {
                    var br = choice[id][k].Breakdown;
                    Console.Error.WriteLine(
                        $"[A{id}:{roles[id]}] pick {mv.X},{mv.Y}  " +
                        $"score={choice[id][k].Score:F1}");
                }
                break;
            }

            /* 3d. fallback – zostaję w miejscu i bez akcji */
            if (!issued)
            {
                ref readonly var me = ref st.Agents[id];
                cmd.SetMove  (id, new MoveAction(MoveType.Step, me.X, me.Y));
                cmd.SetCombat(id, new CombatAction(CombatType.None));
                reserved.Add((me.X, me.Y));
                destOfId[id] = (me.X, me.Y);

                if (DBG)
                    Console.Error.WriteLine($"[A{id}:{roles[id]}] STUCK – stay");
            }
        }
    }

    private (double combat, double penalty)
    EvaluateCombatForAnchor(GameState st,
                            int id, int nx, int ny,
                            CombatAction ca,
                            AgentStats stats)
    {
        double combat = 0, pen = 0;

        switch (ca.Type)
        {
            case CombatType.Shoot:
                {
                    ref readonly var tgt = ref st.Agents[ca.Arg1];
                    if (!tgt.Alive) break;

                    int d = GameState.Mdist(nx, ny, tgt.X, tgt.Y);
                    double dmg = stats.SoakingPower * (d <= stats.OptimalRange ? 1 : 0.5);
                    int idxTarget = GameState.ToIndex(tgt.X, tgt.Y);
                    if (_coveredTiles[idxTarget].Contains((nx, ny)))
                    {
                        foreach (var (dx, dy) in Helpers.Dir4)
                        {
                            int cx = tgt.X + dx, cy = tgt.Y + dy;
                            if (st.InBounds(cx, cy)) continue;
                            if (nx == cx - dx && ny == cy - dy)
                            {
                                TileType tt = GameState.Tiles[cy * st.W + cx];
                                if (tt == TileType.LowCover) dmg *= 0.5;
                                else if (tt == TileType.HighCover) dmg *= 0.25;
                                break;
                            }
                        }
                    }
                    combat += dmg * 2.5;
                    if (tgt.Wetness + dmg >= 100) combat += 12;
                }
                break;

            case CombatType.Hunker:
                combat += 4;
                break;
        }
        return (combat, pen);
    }

    private (double combat, double penalty) EvaluateCombatForBreaker(
            GameState st, int id, int nx, int ny,
            CombatAction ca, AgentStats stats, bool isBomber)
    {
        double cmb = 0, pen = 0;

        switch (ca.Type)
        {
            case CombatType.Shoot:
            {
                ref readonly var tgt = ref st.Agents[ca.Arg1];
                if (!tgt.Alive) break;

                int d = GameState.Mdist(nx, ny, tgt.X, tgt.Y);
                double dmg = stats.SoakingPower * (d <= stats.OptimalRange ? 1 : 0.5);
                cmb += dmg * 1.5;                        // mniejszy mnożnik niż Sniper
                if (tgt.Wetness + dmg >= 100) cmb += 10;
            }
            break;

            case CombatType.Throw:                      // kluczowe dla Bombera
            {
                int cx = ca.Arg1, cy = ca.Arg2;
                int hitE = 0, hitF = 0;
                for (int dy=-1; dy<=1; dy++)
                for (int dx=-1; dx<=1; dx++)
                {
                    int xx = cx + dx, yy = cy + dy;
                    if ((uint)xx >= st.W || (uint)yy >= st.H) continue;
                    int aid = st.AgentAt((byte)xx,(byte)yy);
                    if (aid == -1) continue;
                    if (st.Agents[aid].playerId == _myId) hitF++; else hitE++;
                }

                cmb += hitE * (isBomber ? 14 : 10);
                if (hitF > 0) pen -= 20 * hitF / Math.Max(1, hitE); // kara proporcjonalna
            }
            break;

            case CombatType.Hunker:
                cmb += 2;                               // Breaker hunker = „zbliżam się”
            break;
        }
        return (cmb, pen);
    }

    private (double combat,double penalty)
    EvaluateCombatForSkirmisher(GameState st,int id,int nx,int ny,
                                CombatAction ca,AgentStats stats)
    {
        double cmb=0,pen=0;
        switch(ca.Type)
        {
            case CombatType.Shoot:
            {
                ref readonly var tgt=ref st.Agents[ca.Arg1];
                if(!tgt.Alive)break;
                int d=GameState.Mdist(nx,ny,tgt.X,tgt.Y);
                double dmg=stats.SoakingPower*(d<=stats.OptimalRange?1:0.5);
                int idxTarget = GameState.ToIndex(tgt.X, tgt.Y);
                if (_coveredTiles[idxTarget].Contains((nx, ny)))
                {
                    foreach (var (dx, dy) in Helpers.Dir4)
                    {
                        int cx = tgt.X + dx, cy = tgt.Y + dy;
                        if (st.InBounds(cx, cy)) continue;
                        if (nx == cx - dx && ny == cy - dy)
                        {
                            TileType tt = GameState.Tiles[cy * st.W + cx];
                            if (tt == TileType.LowCover) dmg *= 0.5;
                            else if (tt == TileType.HighCover) dmg *= 0.25;
                            break;
                        }
                    }
                }
                cmb +=dmg*1.8;
                if(tgt.Wetness+dmg>=100)cmb+=10;
            }break;

            case CombatType.Throw:
            {
                int cx=ca.Arg1,cy=ca.Arg2;
                int hitE=0,hitF=0;
                for(int dy=-1;dy<=1;dy++)
                for(int dx=-1;dx<=1;dx++)
                {
                    int xx=cx+dx,yy=cy+dy;
                    if((uint)xx>=st.W||(uint)yy>=st.H)continue;
                    int aid=st.AgentAt((byte)xx,(byte)yy);
                    if(aid==-1)continue;
                    if(st.Agents[aid].playerId==_myId)hitF++;else hitE++;
                }
                cmb+=hitE*10;
                if(hitF>0)pen-=25*hitF;        // mocna kara za FF
            }break;

            case CombatType.Hunker: cmb+=2; break;
        }
        return (cmb,pen);
    }

    private (double combat,double penalty)
    EvaluateCombatForFlanker(GameState st,int id,int nx,int ny,
                            CombatAction ca,AgentStats stats)
    {
        double cmb=0,pen=0;
        switch(ca.Type)
        {
            case CombatType.Shoot:
            {
                ref readonly var t=ref st.Agents[ca.Arg1];
                if(!t.Alive)break;
                int d=GameState.Mdist(nx,ny,t.X,t.Y);
                double dmg=stats.SoakingPower*(d<=stats.OptimalRange?1:0.5);
                cmb+=dmg*1.6;
                if(t.Wetness+dmg>=100)cmb+=8;
            }break;

            case CombatType.Throw:
            {
                int cx=ca.Arg1,cy=ca.Arg2,hitE=0,hitF=0;
                for(int dy=-1;dy<=1;dy++)
                for(int dx=-1;dx<=1;dx++)
                {
                    int xx=cx+dx,yy=cy+dy;
                    if((uint)xx>=st.W||(uint)yy>=st.H)continue;
                    int aid=st.AgentAt((byte)xx,(byte)yy);
                    if(aid==-1)continue;
                    if(st.Agents[aid].playerId==_myId)hitF++;else hitE++;
                }
                cmb+=hitE*8;
                if(hitF>0)pen-=25*hitF;
            }break;

            case CombatType.Hunker: cmb+=1; break;
        }
        return (cmb,pen);
    }

    private static int TerritoryDiff(GameState st, int myId)
    {
        int diff = 0;                                  // moje − przeciwnika
        for (int y = 0; y < st.H; y++)
            for (int x = 0; x < st.W; x++)
            {
                if (GameState.Tiles[GameState.ToIndex(x, y)] != TileType.Empty) continue;

                int bestMe = int.MaxValue, bestOpp = int.MaxValue;
                for (int id = 0; id < GameState.MaxAgents; id++)
                {
                    ref readonly var ag = ref st.Agents[id];
                    if (!ag.Alive) continue;

                    int d = Math.Abs(ag.X - x) + Math.Abs(ag.Y - y);
                    if (ag.Wetness >= 50) d <<= 1;          // podwajamy wg zasad

                    if (ag.playerId == myId) bestMe = Math.Min(bestMe, d);
                    else bestOpp = Math.Min(bestOpp, d);
                }
                if (bestMe < bestOpp) diff++;
                else if (bestOpp < bestMe) diff--;
            }
        return diff;                                   // >0 przewaga, <0 strata
    }
    
    private static int DistanceToClosestEnemy(GameState st,
                                            int myPlayerId,
                                            int x, int y)
    {
        int best = int.MaxValue;
        for (int i = 0; i < GameState.MaxAgents; i++)
        {
            ref readonly var e = ref st.Agents[i];
            if (!e.Alive || e.playerId == myPlayerId) continue;
            int d = GameState.Mdist(x, y, e.X, e.Y);
            if (d < best) best = d;
        }
        return best;
    }
}
