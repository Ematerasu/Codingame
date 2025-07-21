namespace SummerChallenge2025.Bot;

public sealed class CombatPhase : IGamePhase
{
    private int _myId = -1;
    private AI _opponentAI = new CoverBot();
    private HashSet<(int x, int y)>[] _coveredTiles;
    private EnemyPlan _currentPlan = EnemyPlan.Unknown;
    private readonly Dictionary<int, AgentPlan> _plans;

    public CombatPhase(HashSet<(int x, int y)>[] coveredTiles, EnemyPlan currentPlan, IReadOnlyDictionary<int, AgentPlan> prev)
    {
        _coveredTiles = coveredTiles;
        _currentPlan = currentPlan;
        _plans = new Dictionary<int, AgentPlan>(prev);
    }

    public void Enter(GameState st, int myPlayerId)
    {
        _myId = myPlayerId;
        _opponentAI.Initialize(1 - myPlayerId);
    }

    public TurnCommand GetMove(GameState st)
    {
        Span<AgentOrder> buf = stackalloc AgentOrder[512];
        var cmd       = new TurnCommand(GameState.MaxAgents);
        var reserved  = new HashSet<(byte,byte)>();          // pola już zajęte

        /* ── stance globalny ─────────────────────────────────────────── */
        int myAlive=0, enAlive=0, myWet=0, enWet=0;
        for (int i=0;i<GameState.MaxAgents;i++)
            if (st.Agents[i].Alive)
                if (st.Agents[i].playerId==_myId){ myAlive++; myWet+=st.Agents[i].Wetness; }
                else                              { enAlive++; enWet+=st.Agents[i].Wetness; }
        bool aggressive = myAlive>enAlive || myWet<enWet;

        /* ── wybór focusEnemy ────────────────────────────────────────── */
        int focusEnemy=-1; double bestM=double.NegativeInfinity;
        for (int eid=0;eid<GameState.MaxAgents;eid++)
        {
            ref readonly var e = ref st.Agents[eid];
            if (!e.Alive || e.playerId==_myId) continue;

            int myShot=0,enShot=0;
            for (int aid=0;aid<GameState.MaxAgents;aid++)
            {
                ref readonly var a = ref st.Agents[aid];
                if (!a.Alive) continue;
                int maxR=AgentUtils.Stats[GameState.AgentClasses[aid]].OptimalRange*2;
                if (GameState.Mdist(a.X,a.Y,e.X,e.Y)>maxR) continue;
                if (a.playerId==_myId) myShot++; else enShot++;
            }
            double margin=myShot-enShot - e.Wetness*0.01;
            if (margin>bestM){ bestM=margin; focusEnemy=eid; }
        }

        /* ── pętla agentów ───────────────────────────────────────────── */
        for (int id=0; id<GameState.MaxAgents; id++)
        {
            ref var me = ref st.Agents[id];
            if (!me.Alive || me.playerId!=_myId) continue;

            int legal = st.GetLegalOrders(id, buf);
            if (legal==0) continue;

            Span<double> scores = stackalloc double[legal];
            EvaluateOrders(st, id, ref me, buf[..legal],
                        aggressive, focusEnemy, reserved, scores);

            /* — wybór najlepszego nie-kolidującego ruchu — */
            int pick=-1;
            while (true)
            {
                double best=double.NegativeInfinity;
                int bestIdx=-1;
                for (int i=0;i<legal;i++)
                    if (scores[i]>best){ best=scores[i]; bestIdx=i; }
                if (bestIdx==-1) break;                 // nic nie zostało
                var ord=buf[bestIdx];
                var dst=(ord.Move.X,ord.Move.Y);
                if (reserved.Contains(dst) && dst!=(me.X,me.Y))
                { scores[bestIdx]=double.NegativeInfinity; continue; } // kolizja
                pick=bestIdx; reserved.Add(dst); break;
            }

            if (pick==-1)                               // fallback: stay
            {
                cmd.SetMove(id,new MoveAction(MoveType.Step,me.X,me.Y));
                cmd.SetCombat(id,new CombatAction(CombatType.None));
            }
            else
            {
                var sel=buf[pick];
                cmd.SetMove(id,  sel.Move);
                cmd.SetCombat(id,sel.Combat);
            }
        }
        return cmd;
    }
    public bool ShouldExit(GameState _) => false;
    public IGamePhase? GetNextPhase(GameState _) => null;

    /*======================================================================*/
    /*                      ---  Heurystyki zamknięte  ---                  */
    /*======================================================================*/
    private void EvaluateOrders(GameState st, int id, ref AgentState me,
                                ReadOnlySpan<AgentOrder> orders,
                                bool aggressive, int focusEnemy,
                                HashSet<(byte,byte)> reserved,
                                Span<double> scores)
    {
        var cls   = GameState.AgentClasses[id];
        var stats = AgentUtils.Stats[cls];

        bool isBomber = cls==AgentClass.Bomber;
        bool isSniper = cls==AgentClass.Sniper;
        bool isMelee  = cls==AgentClass.Berserker;

        for (int idx=0; idx<orders.Length; idx++)
        {
            ref readonly var ord = ref orders[idx];
            byte nx=ord.Move.X, ny=ord.Move.Y;
            var  dst=(nx,ny);
            double sc=0;

            /* 0. kara za wejście w już zarezerwowane pole (ale pozwól stać) */
            if (reserved.Contains(dst) && dst!=(me.X,me.Y))
                { scores[idx]=double.NegativeInfinity; continue; }

            /* 1. frontGain  – przesuwanie linii frontu (kontrola terenu) */
            int gain = (_myId==0) ? nx-me.X : me.X-nx;   // +1 gdy posuwam się do przodu
            sc += gain * 2.0;                            // waga frontu

            /* 2. allyProximity  – rozproszenie */
            int nearAllies=0;
            for (int aid=0;aid<GameState.MaxAgents;aid++)
            {
                if (aid==id) continue;
                ref readonly var a=ref st.Agents[aid];
                if (!a.Alive||a.playerId!=_myId) continue;
                int d=GameState.Mdist(nx,ny,a.X,a.Y);
                if (d<=2) nearAllies++;
            }
            sc -= nearAllies * 4.0;                      // kara za tłok

            /* 3. stance jak wcześniej (cover/agresja) */
            if (aggressive && focusEnemy!=-1)
            {
                ref readonly var trg = ref st.Agents[focusEnemy];
                sc += (GameState.Mdist(me.X,me.Y,trg.X,trg.Y) -
                    GameState.Mdist(nx,ny,trg.X,trg.Y))*3;
            }
            else
            {
                int idxTile = GameState.ToIndex(nx, ny);
                int coveredEnemies = 0;

                // zlicz wrogów, którzy stoją w polach dających nam redukcję dmg
                foreach (var (ex, ey) in _coveredTiles[idxTile])
                {
                    int aid = st.AgentAt((byte)ex, (byte)ey);
                    if (aid == -1) continue;
                    ref readonly var enemy = ref st.Agents[aid];
                    if (!enemy.Alive || enemy.playerId == _myId) continue;
                    coveredEnemies++;
                }

                /*  Waga ochrony:  +6 za każdego wroga, przed którym chroni cover.
                    Jeśli nikt nie patrzy przez cover, bonus = 0. */
                sc += coveredEnemies * 6;
            }

            /* 4. heurystyki klas  (identyczne jak wcześniej) */
            if (isBomber)
            {
                if (me.SplashBombs>0 && AnyEnemyInRange(st,nx,ny,4)) sc+=15;
                sc -= DistanceToClosestEnemy(st,_myId,nx,ny)*0.8;
            }
            else if (isSniper)
            {
                if (focusEnemy!=-1)
                {
                    int d=GameState.Mdist(nx,ny,
                        st.Agents[focusEnemy].X,st.Agents[focusEnemy].Y);
                    sc -= Math.Abs(d - stats.OptimalRange)*2;
                }
                int near=DistanceToClosestEnemy(st,_myId,nx,ny);
                if (near<3) sc-=8;
            }
            else if (isMelee)
            {
                sc -= DistanceToClosestEnemy(st,_myId,nx,ny)*1.2;
            }
            else
            {
                int d=DistanceToClosestEnemy(st,_myId,nx,ny);
                sc -= Math.Abs(d - stats.OptimalRange);
            }

            /* 5. akcja bojowa  (bez zmian) */
            sc += ScoreCombatAction(st, id, nx, ny, ord.Combat,
                                    stats, isSniper, isBomber, focusEnemy);

            /* 6. anti-stuck */
            if (nx==me.X && ny==me.Y && ord.Combat.Type==CombatType.None)
                sc -= 3;

            scores[idx]=sc;
        }
    }
    
    private double ScoreCombatAction(GameState st,int id,int nx,int ny,
                                    CombatAction ca,AgentStats stats,
                                    bool isSniper,bool isBomber,int focusEnemy)
    {
        double sc=0;
        switch (ca.Type)
        {
            case CombatType.Shoot:
            {
                ref readonly var t=ref st.Agents[ca.Arg1];
                if (!t.Alive) break;
                int d=GameState.Mdist(nx,ny,t.X,t.Y);
                double dmg=stats.SoakingPower*(d<=stats.OptimalRange?1:0.5);
                sc+=dmg*(isSniper?2.5:2.0);
                if (t.Wetness+dmg>=100) sc+=12;
                if (ca.Arg1==focusEnemy) sc+=6;
            }
            break;

            case CombatType.Throw:
            {
                int cx=ca.Arg1, cy=ca.Arg2;
                int hitE=0,hitF=0;
                for (int dy=-1;dy<=1;dy++)
                for (int dx=-1;dx<=1;dx++)
                {
                    int xx=cx+dx,yy=cy+dy;
                    if ((uint)xx>=st.W||(uint)yy>=st.H) continue;
                    int aid=st.AgentAt((byte)xx,(byte)yy);
                    if (aid==-1) continue;
                    if (st.Agents[aid].playerId==_myId) hitF++; else hitE++;
                }
                sc+=hitE*(isBomber?14:10)-hitF*30;
            }
            break;

            case CombatType.Hunker:
                if (AnyEnemyCanShoot(st,nx,ny)) sc+=4; else sc-=6;
            break;
        }
        return sc;
    }

    private static int DistanceToClosestEnemy(GameState st, int myPlayerId, int x, int y)
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

    private bool AnyEnemyInRange(GameState st,int x,int y,int range)
    {
        for (int i=0;i<GameState.MaxAgents;i++)
        {
            ref readonly var e = ref st.Agents[i];
            if (!e.Alive || e.playerId==_myId) continue;
            if (GameState.Mdist(x,y,e.X,e.Y)<=range) return true;
        }
        return false;
    }

    private bool AnyEnemyCanShoot(GameState st,int x,int y)
    {
        for (int i=0;i<GameState.MaxAgents;i++)
        {
            ref readonly var e = ref st.Agents[i];
            if (!e.Alive || e.playerId==_myId || e.Cooldown>0) continue;
            int r = AgentUtils.Stats[GameState.AgentClasses[i]].OptimalRange*2;
            if (GameState.Mdist(x,y,e.X,e.Y)<=r) return true;
        }
        return false;
    }
}