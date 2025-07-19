namespace SummerChallenge2025.Bot;

public class CoverBot : AI
{
    /*──── instancja ────*/
    private double[,] _coverScore = null!;
    private int _w,_h,_midX;
    private readonly Random _rng = new();
    private readonly int[] _stay = new int[GameState.MaxAgents];
    private readonly int[] _lastX = new int[GameState.MaxAgents];
    private readonly int[] _lastY = new int[GameState.MaxAgents];
    private readonly AgentClass[] _myClass = new AgentClass[GameState.MaxAgents];
    private readonly int[] _sniperTargetCol = new int[GameState.MaxAgents];
    private bool _initDone;

    public override TurnCommand GetMove(GameState state)
    {
        if (!_initDone)
        {
            RecogniseClasses(state);
            PrecomputeCover(state);
            PrecomputeSniperTargets(state);
            _initDone = true;
        }

        Span<AgentOrder> buffer = stackalloc AgentOrder[512];
        var cmd = new TurnCommand(GameState.MaxAgents);
        bool enemyVisibleGlobal = AnyEnemyVisible(state);

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            ref readonly var me = ref state.Agents[id];
            if (!me.Alive || me.playerId != PlayerId) continue;

            int legal = state.GetLegalOrders(id, buffer);
            if (legal == 0) continue;

            var cls = _myClass[id];
            var myStats = AgentUtils.Stats[cls];

            AgentOrder best = buffer[0];
            double bestScore = double.NegativeInfinity;

            for (int i = 0; i < legal; i++)
            {
                ref readonly var ord = ref buffer[i];
                int tx = ord.Move.X, ty = ord.Move.Y;
                double sc = 0;

                /*──── 1. Positional Heuristics ────*/
                // forward baseline
                int advance = PlayerId == 0 ? tx : (_w-1-tx);
                double fwBase = state.Turn < 6 ? 4 : 1;
                sc += advance * fwBase;

                // sniper preferred column
                if (cls == AgentClass.Sniper)
                    sc -= Math.Abs(tx - _sniperTargetCol[id]) * 3;

                // cover preference
                double coverW = cls == AgentClass.Sniper ? 2.0 : cls == AgentClass.Berserker ? 0.5 : 1.0;
                sc += _coverScore[tx,ty] * coverW;

                // spread penalty
                for (int j=0;j<GameState.MaxAgents;++j)
                {
                    if (j==id) continue;
                    ref readonly var ally = ref state.Agents[j];
                    if (!ally.Alive || ally.playerId!=PlayerId) continue;
                    int d = Mdist(tx,ty,ally.X,ally.Y);
                    if (d==0) sc-=1000; else if (d<3) sc-=2.5/d; else sc-=0.8/d;
                }

                // SEEK WEAK ENEMY for aggressive classes
                if (cls!=AgentClass.Sniper)
                {
                    var (bestDist,scoreBonus)=EvaluateSeekTarget(state,tx,ty,myStats,cls);
                    sc += scoreBonus;
                }

                /*──── 2. Combat ────*/
                bool seesEnemy=false;
                switch(ord.Combat.Type)
                {
                    case CombatType.Shoot:
                    {
                        int tid = ord.Combat.Arg1; ref readonly var en = ref state.Agents[tid];
                        if(!en.Alive) break;
                        seesEnemy=true;
                        int dist = Mdist(tx,ty,en.X,en.Y);
                        double dmg = myStats.SoakingPower*(dist<=myStats.OptimalRange?1:0.5);
                        if(IsBehindCover(state,tx,ty,en)) dmg*=0.5;
                        if(en.Hunkering) dmg*=0.75;
                        double scale = cls==AgentClass.Sniper?1.4:1.1;
                        sc += dmg*scale + (en.Wetness+dmg>=100?40:0);
                        if(cls==AgentClass.Berserker && dist<=2) sc+=15;
                    }break;
                    case CombatType.Throw:
                    {
                        int cx=ord.Combat.Arg1, cy=ord.Combat.Arg2;
                        int em=0, fr=0;
                        for(int k=0;k<GameState.MaxAgents;++k)
                        {
                            ref readonly var a = ref state.Agents[k];
                            if(!a.Alive) continue;
                            if(Math.Abs(a.X-cx)>1||Math.Abs(a.Y-cy)>1) continue;
                            if(a.playerId==PlayerId) fr++; else em++;
                        }
                        bool ok=false; double baseVal=0;
                        if(cls==AgentClass.Bomber)
                        {
                            if(fr==0 && em>0){ ok=true; baseVal=30*em; }
                        }
                        else
                        {
                            if(fr==0 && em>0){ ok=true; baseVal=25*em; }
                        }
                        if(ok){ sc+=baseVal; seesEnemy=true; } else sc-=9000;
                    }break;
                    case CombatType.Hunker:
                    {
                        bool danger = EnemyCanShoot(state,tx,ty);
                        sc += danger? (cls==AgentClass.Sniper?4:2):-15;
                    }break;
                }

                // boredom / stagnation
                if(!seesEnemy && !enemyVisibleGlobal) sc-=3;
                if(tx==_lastX[id] && ty==_lastY[id]) sc-=4*(_stay[id]+1);

                sc+=_rng.NextDouble()*1e-4;
                if(sc>bestScore){ bestScore=sc; best=ord; }
            }

            cmd.SetMove(id,best.Move);
            cmd.SetCombat(id,best.Combat);

            if(best.Move.X==_lastX[id] && best.Move.Y==_lastY[id]) _stay[id]++; else _stay[id]=0;
            _lastX[id]=best.Move.X; _lastY[id]=best.Move.Y;
        }
        return cmd;
    }

    /*──── helper: pick weak enemy heuristic ────*/
    private (int dist,double bonus) EvaluateSeekTarget(GameState st,int x,int y,AgentStats myStat,AgentClass cls)
    {
        int bestDist=999; double bonus=0;
        for(int i=0;i<GameState.MaxAgents;++i)
        {
            ref readonly var en = ref st.Agents[i]; if(!en.Alive || en.playerId==PlayerId) continue;
            int dist=Mdist(x,y,en.X,en.Y);
            bool weak = en.Wetness>60 || AgentUtils.Stats[GameState.AgentClasses[i]].ShootCooldown>myStat.ShootCooldown || AgentUtils.Stats[GameState.AgentClasses[i]].SoakingPower<myStat.SoakingPower;
            double w = weak?0.8:0.4; // weight per tile
            double val = -dist*w;
            if(val>bonus){ bonus=val; bestDist=dist; }
        }
        return (bestDist,bonus);
    }

    /*──── initial calculations ────*/
    private void RecogniseClasses(GameState st)
    {
        for(int id=0; id<GameState.MaxAgents; ++id)
        {
            ref readonly var ag = ref st.Agents[id]; if(!ag.Alive) continue;
            var s = AgentUtils.Stats[GameState.AgentClasses[id]]; int bombs=ag.SplashBombs;
            _myClass[id]=AgentUtils.GuessClass(s.ShootCooldown, s.OptimalRange, s.SoakingPower, bombs);
            //Console.Error.WriteLine($"Agent {id} -> {_myClass[id]}");
        }
    }

    private void PrecomputeCover(GameState st)
    {
        _w=st.W; _h=st.H; _midX=(_w-1)/2;
        _coverScore=new double[_w,_h];
        for(int y=0;y<_h;y++)
            for(int x=0;x<_w;x++)
            {
                if(GameState.Tiles[GameState.ToIndex((byte)x,(byte)y)]!=TileType.Empty){ _coverScore[x,y]=-9999; continue; }
                double s=0; foreach(var (dx,dy) in dirs4){ int nx=x+dx, ny=y+dy; if(!st.InBounds((byte)nx,(byte)ny)) continue; var t=GameState.Tiles[GameState.ToIndex((byte)nx,(byte)ny)]; if(t==TileType.HighCover) s+=4; else if(t==TileType.LowCover) s+=2; }
                _coverScore[x,y]=s;
            }
    }
    private void PrecomputeSniperTargets(GameState st)
    {
        for(int id=0; id<GameState.MaxAgents; ++id)
        {
            if(_myClass[id]!=AgentClass.Sniper) continue;
            int bestCol=_midX; double bestVal=-1e9;
            for(int x=0;x<_w;x++)
            {
                double val=_coverScore[x,st.Agents[id].Y]; // need cover orth to cell
                if(val<2) continue; // need at least low cover adj
                // count open cells in range
                int cnt=0;
                for(int cy=0;cy<_h;cy++)
                    if(Mdist(x,cy,x,st.Agents[id].Y)<=AgentUtils.Stats[AgentClass.Sniper].OptimalRange) cnt++;
                if(cnt+val>bestVal){ bestVal=cnt+val; bestCol=x; }
            }
            _sniperTargetCol[id]=bestCol;
        }
    }

    /*──── utilities ────*/
    private bool AnyEnemyVisible(GameState st)
    {
        for(int i=0;i<GameState.MaxAgents;++i)
        {
            ref readonly var ag = ref st.Agents[i]; if(!ag.Alive||ag.playerId!=PlayerId) continue;
            var stt=AgentUtils.Stats[GameState.AgentClasses[i]];
            for(int j=0;j<GameState.MaxAgents;++j)
            {
                ref readonly var en = ref st.Agents[j]; if(!en.Alive||en.playerId==PlayerId) continue;
                if(Mdist(ag.X,ag.Y,en.X,en.Y)<=stt.OptimalRange*2) return true;
            }
        }
        return false;
    }
    private bool EnemyCanShoot(GameState st,int x,int y)
    {
        for(int i=0;i<GameState.MaxAgents;++i)
        {
            ref readonly var en = ref st.Agents[i]; if(!en.Alive||en.playerId==PlayerId||en.Cooldown>0) continue;
            if(Mdist(x,y,en.X,en.Y)<=AgentUtils.Stats[GameState.AgentClasses[i]].OptimalRange*2) return true;
        }
        return false;
    }
    private bool IsBehindCover(GameState st,int sx,int sy,in AgentState t)
    {
        int dx=t.X-sx, dy=t.Y-sy;
        if(Math.Abs(dx)>1){ int cx=t.X-Math.Sign(dx), cy=t.Y; if(st.InBounds((byte)cx,(byte)cy) && GameState.Tiles[GameState.ToIndex((byte)cx,(byte)cy)]!=TileType.Empty) return true; }
        if(Math.Abs(dy)>1){ int cx=t.X, cy=t.Y-Math.Sign(dy); if(st.InBounds((byte)cx,(byte)cy) && GameState.Tiles[GameState.ToIndex((byte)cx,(byte)cy)]!=TileType.Empty) return true; }
        return false;
    }
    private static int Mdist(int x1,int y1,int x2,int y2)=>Math.Abs(x1-x2)+Math.Abs(y1-y2);
    private static readonly (int,int)[] dirs4={ (1,0),(-1,0),(0,1),(0,-1) };
}