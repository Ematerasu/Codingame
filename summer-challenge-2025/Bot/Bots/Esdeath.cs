using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SummerChallenge2025.Bot;

public enum Phase
{
    Opening,
    MidGame,
    EndGame,
}

public enum Orders : byte
{
    StandYourGround = 0,
    Offensive = 1,
    Defensive = 2,
    Retreat = 3,
    Flank = 4,
    Hunt = 5,
}

public static class PhaseOrders
{
    public static readonly Dictionary<Phase, Orders[]> AllowedOrders = new()
    {
        [Phase.Opening] = new[] { Orders.StandYourGround, Orders.Offensive, Orders.Defensive },
        [Phase.MidGame] = new[] { Orders.StandYourGround, Orders.Offensive, Orders.Defensive },
        [Phase.EndGame] = new[] { Orders.StandYourGround, Orders.Offensive, Orders.Defensive }
    };

    public static Phase DeterminePhase(GameState st, int myId)
    {
        int total = 0, alive = 0;
        foreach (var ag in st.Agents)
        {
            if (ag.playerId != myId) continue;
            total++;
            if (ag.Alive) alive++;
        }
        int turn = st.Turn;

        if (turn < 4)
            return Phase.Opening;

        if (alive * 2 <= total)
            return Phase.EndGame;

        return Phase.MidGame;
    }

}


public sealed class Esdeath : AI
{

    // ======= Opponent model =================================================================
    private readonly AI _opponentBot;

    // ───────────────────────────────── Beam-Search helper ──────────────────────────────
    private sealed record BeamNode(GameState State, Orders[] Seq, double Score);

    // ───────────────────────────────── Cover distance cache ────────────────────────────
    private HashSet<(int x, int y)>[] _coveredTiles;
    private int[] _coverDist = Array.Empty<int>();
    private bool _first = false;
    private readonly Random _rng = new();
    private readonly Stopwatch _timer = new();
    private int _lastTurn = -1;

    private readonly struct Individual
    {
        public readonly Orders[] Sequence;
        public readonly double Score;

        public Individual(Orders[] seq, double score)
        {
            Sequence = seq;
            Score = score;
        }
    }

    public Esdeath(
        AI? opponent = null
    )
    {
        _opponentBot = opponent ?? new CoverBot();
        _coveredTiles = new HashSet<(int x, int y)>[GameState.MaxW * GameState.MaxH];
    }

    public override TurnCommand GetMove(GameState st)
    {
        if (!_first)
        {
            CalculateCoveredTiles(st);
            _first = true;
        }
        Phase phase = PhaseOrders.DeterminePhase(st, PlayerId);
        Orders[] legalOrders = PhaseOrders.AllowedOrders[phase];

        int budgetMs = st.Turn == 0 ? 950 : 47;
        _timer.Restart();
        int maxDepth = st.Turn == 0 ? 5 : 3;
        int beamWidth = 36;
        var rng = _rng;

        int depthReached = 0;
        long generated   = 0;

        var rootClone = st.FastClone();
        var beam = new List<BeamNode>
        {
            new(rootClone, Array.Empty<Orders>(), Evaluate(rootClone, PlayerId))
        };

        for (int depth = 0; depth < maxDepth; depth++)
        {
            if (_timer.ElapsedMilliseconds > budgetMs - 5)
                break;
            depthReached = depth + 1;

            var next = new List<BeamNode>(beamWidth * legalOrders.Length);

            foreach (var node in beam)
            {
                foreach (var order in legalOrders)
                {
                    if (_timer.ElapsedMilliseconds > budgetMs - 5)
                        break;      // awaryjne wyjście

                    // 1) sklonuj stan i zastosuj ruch
                    var gs = node.State.FastClone();
                    var myCmd = GenerateOrderCommand(gs, order, PlayerId);
                    var oppCmd = _opponentBot.GetMove(gs.FastClone());   // ruch wroga
                    if (PlayerId == 0)
                        gs.ApplyInPlace(myCmd, oppCmd);   // my => 0, opp => 1
                    else
                        gs.ApplyInPlace(oppCmd, myCmd);    // opp => 0, my  => 1

                    // 2) nowa sekwencja
                    var seq = new Orders[node.Seq.Length + 1];
                    node.Seq.CopyTo(seq, 0);
                    seq[^1] = order;

                    // 3) ocena stanu
                    double score = Evaluate(gs, PlayerId) - depth * 0.1; // lekka kara za głębiej

                    next.Add(new BeamNode(gs, seq, score));
                    generated++;
                }

                node.State.Dispose();
            }

            // zostaw najlepsze k kandydatów
            beam = next.OrderByDescending(n => n.Score)
                    .Take(beamWidth)
                    .ToList();
        }

        BeamNode best = beam.OrderByDescending(n => n.Score).First();
        Orders firstOrder = best.Seq.Length > 0 ? best.Seq[0] : Orders.StandYourGround;
        int      kept      = beam.Count;
        foreach (var n in beam) n.State.Dispose();

        Console.Error.WriteLine($"TURN {st.Turn} | depth={depthReached} | gen={generated} | kept={kept} | time={_timer.ElapsedMilliseconds}ms");

        return GenerateOrderCommand(st, firstOrder, PlayerId);
    }

    public TurnCommand GenerateOrderCommand(GameState st, Orders order, int myId)
    {
        var cmd = new TurnCommand(GameState.MaxAgents);

        for (int id = 0; id < GameState.MaxAgents; id++)
        {
            ref readonly var ag = ref st.Agents[id];
            if (!ag.Alive || ag.playerId != myId) continue;

            AgentOrder ao = order switch
            {
                Orders.Offensive => AggressiveOrder(st, id),
                Orders.Defensive => DefensiveOrder(st, id),
                Orders.StandYourGround => StandYourGroundOrder(st, id),
                _ => default
            };

            cmd.Orders[id] = ao;
            cmd.ActiveMask |= 1UL << id;
        }

        return cmd;
    }

    private AgentOrder AggressiveOrder(GameState st, int id)
    {
        ref readonly var ag = ref st.Agents[id];
        var myClass = GameState.AgentClasses[id];
        var myStats = AgentUtils.Stats[myClass];

        // Przeciwny brzeg
        int targetX = st.W - 1;
        if (ag.X > st.W / 2) targetX = 0;

        // MOVE: jeden krok w stronę targetX
        byte bestX = ag.X, bestY = ag.Y;
        int bestDist = GameState.Mdist(ag.X, ag.Y, targetX, ag.Y);
        foreach (var (dx, dy) in Helpers.Dir4)
        {
            byte nx = (byte)(ag.X + dx);
            byte ny = (byte)(ag.Y + dy);
            if (!st.InBounds(nx, ny)) continue;
            int idx = GameState.ToIndex(nx, ny);
            if (GameState.Tiles[idx] != TileType.Empty || st.Occup.Test(idx)) continue;

            int d = GameState.Mdist(nx, ny, targetX, ny);
            if (d < bestDist) { bestDist = d; bestX = nx; bestY = ny; }
        }

        if (ag.SplashBombs > 0)
        {
            for (int y = 0; y < st.H; y++)
                for (int x = 0; x < st.W; x++)
                {
                    if (Math.Abs(x - ag.X) + Math.Abs(y - ag.Y) > 4) continue;

                    int enemies = 0, allies = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int tx = x + dx, ty = y + dy;
                            if (!st.InBounds(tx, ty)) continue;
                            int aid = st.AgentAt((byte)tx, (byte)ty);
                            if (aid == -1) continue;
                            if (st.Agents[aid].playerId == ag.playerId) allies++; else enemies++;
                        }

                    if (enemies >= 2 && allies == 0)
                    {
                        return new AgentOrder
                        {
                            Move = new MoveAction(MoveType.Step, bestX, bestY),
                            Combat = new CombatAction(CombatType.Throw, (ushort)x, (byte)y)
                        };
                    }
                }
        }

        // COMBAT: strzelaj jeśli w zasięgu
        for (int eid = 0; eid < GameState.MaxAgents; eid++)
        {
            ref readonly var enemy = ref st.Agents[eid];
            if (!enemy.Alive || enemy.playerId == ag.playerId) continue;
            int dist = GameState.Mdist(ag.X, ag.Y, enemy.X, enemy.Y);
            if (dist <= myStats.OptimalRange * 2 && ag.Cooldown == 0)
                return new AgentOrder
                {
                    Move = new MoveAction(MoveType.Step, bestX, bestY),
                    Combat = new CombatAction(CombatType.Shoot, (ushort)eid)
                };
        }

        // inaczej: zwykły ruch i hunker
        return new AgentOrder
        {
            Move = new MoveAction(MoveType.Step, bestX, bestY),
            Combat = new CombatAction(CombatType.Hunker)
        };
    }

    private AgentOrder DefensiveOrder(GameState st, int id)
    {
        ref readonly var ag = ref st.Agents[id];
        double bestScore = double.NegativeInfinity;
        (byte x, byte y) bestMove = (ag.X, ag.Y);

        foreach (var (dx, dy) in Helpers.Dir4)
        {
            byte nx = (byte)(ag.X + dx), ny = (byte)(ag.Y + dy);
            if (!st.InBounds(nx, ny)) continue;
            int idx = GameState.ToIndex(nx, ny);
            if (GameState.Tiles[idx] != TileType.Empty || st.Occup.Test(idx)) continue;

            int coverBonus = 0;
            foreach (var (ox, oy) in Helpers.Dir4)
            {
                int cx = nx + ox, cy = ny + oy;
                if (!st.InBounds(cx, cy)) continue;
                var tile = GameState.Tiles[GameState.ToIndex(cx, cy)];
                if (tile == TileType.LowCover) coverBonus += 2;
                if (tile == TileType.HighCover) coverBonus += 4;
            }

            int allyDist = 0;
            foreach (var other in st.Agents)
            {
                if (!other.Alive || other.playerId != ag.playerId || other.X == ag.X && other.Y == ag.Y) continue;
                allyDist += 5 - GameState.Mdist(nx, ny, other.X, other.Y);  // preferuje bliżej
            }

            double score = coverBonus + allyDist;
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = (nx, ny);
            }
        }

        return new AgentOrder
        {
            Move = new MoveAction(MoveType.Step, bestMove.x, bestMove.y),
            Combat = new CombatAction(CombatType.Hunker)
        };
    }

    private AgentOrder StandYourGroundOrder(GameState st, int id)
    {
        ref readonly var ag = ref st.Agents[id];

        foreach (var (dx, dy) in Helpers.Dir4)
        {
            int nx = ag.X + dx;
            int ny = ag.Y + dy;
            if (!st.InBounds(nx, ny)) continue;
            int idx = GameState.ToIndex(nx, ny);
            if (GameState.Tiles[idx] is TileType.LowCover or TileType.HighCover)
            {
                int cx = ag.X + dx, cy = ag.Y + dy;
                int cidx = GameState.ToIndex(cx, cy);
                if (!st.Occup.Test(cidx))
                {
                    return new AgentOrder
                    {
                        Move = new MoveAction(MoveType.Step, (byte)cx, (byte)cy),
                        Combat = new CombatAction(CombatType.Hunker)
                    };
                }
            }
        }

        // W przeciwnym razie: shoot jeśli widzi
        var stats = AgentUtils.Stats[GameState.AgentClasses[id]];
        if (ag.Cooldown == 0)
        {
            for (int i = 0; i < GameState.MaxAgents; ++i)
            {
                ref readonly var trg = ref st.Agents[i];
                if (!trg.Alive || trg.playerId == ag.playerId) continue;
                if (GameState.Mdist(ag.X, ag.Y, trg.X, trg.Y) <= stats.OptimalRange * 2)
                    return new AgentOrder
                    {
                        Move = new MoveAction(MoveType.Step, ag.X, ag.Y),
                        Combat = new CombatAction(CombatType.Shoot, (ushort)i)
                    };
            }
        }

        // Albo rzuć bombę jeśli warto
        if (ag.SplashBombs > 0)
        {
            for (int y = 0; y < st.H; y++)
                for (int x = 0; x < st.W; x++)
                {
                    if (Math.Abs(x - ag.X) + Math.Abs(y - ag.Y) > 4) continue;
                    int enemies = 0, allies = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int tx = x + dx, ty = y + dy;
                            if (!st.InBounds(tx, ty)) continue;
                            int aid = st.AgentAt((byte)tx, (byte)ty);
                            if (aid == -1) continue;
                            if (st.Agents[aid].playerId == ag.playerId) allies++; else enemies++;
                        }
                    if (enemies >= 2 && allies == 0)
                        return new AgentOrder
                        {
                            Move = new MoveAction(MoveType.Step, ag.X, ag.Y),
                            Combat = new CombatAction(CombatType.Throw, (ushort)x, (byte)y)
                        };
                }
        }

        return new AgentOrder
        {
            Move = new MoveAction(MoveType.Step, ag.X, ag.Y),
            Combat = new CombatAction(CombatType.Hunker)
        };
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
                        if (GameState.Cdist(tx, ty, sx, sy) <= 1 || GameState.Mdist(tx, ty, sx, sy) > 12) continue;

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

    private static double Evaluate(GameState gs, int myId)
    {
        // ── akumulatory HP / centroidy ────────────────────────────────
        int   myHP = 0, enHP = 0, myAlive = 0, enAlive = 0;
        double myCx = 0, myCy = 0, enCx = 0, enCy = 0;

        foreach (var ag in gs.Agents)
        {
            if (!ag.Alive) continue;
            int hp = 100 - ag.Wetness;

            if (ag.playerId == myId)
            {
                myHP += hp;
                myCx += ag.X; myCy += ag.Y;
                myAlive++;
            }
            else
            {
                enHP += hp;
                enCx += ag.X; enCy += ag.Y;
                enAlive++;
            }
        }

        // centroidy (jeśli ktoś żyje)
        if (myAlive > 0) { myCx /= myAlive; myCy /= myAlive; }
        if (enAlive > 0) { enCx /= enAlive; enCy /= enAlive; }

        // ── odległość centroidów od środka mapy ───────────────────────
        double midX = (gs.W - 1) / 2.0;
        double midY = (gs.H - 1) / 2.0;

        double myCentDist = Math.Abs(myCx - midX) + Math.Abs(myCy - midY);
        double enCentDist = Math.Abs(enCx - midX) + Math.Abs(enCy - midY);

        // ── rozproszenie (średni dystans do własnego centroidu) ───────
        double myDisp = 0, enDisp = 0;
        foreach (var ag in gs.Agents)
        {
            if (!ag.Alive) continue;
            if (ag.playerId == myId)
                myDisp += GameState.Mdist(ag.X, ag.Y, (int)Math.Round(myCx), (int)Math.Round(myCy));
            else
                enDisp += GameState.Mdist(ag.X, ag.Y, (int)Math.Round(enCx), (int)Math.Round(enCy));
        }
        if (myAlive > 0) myDisp /= myAlive;
        if (enAlive > 0) enDisp /= enAlive;

        // ── łączna wartość stanu ───────────────────────────────────────
        double v = 0;
        v += (myHP     - enHP)     * 1.0;   // przewaga HP
        v += (myAlive  - enAlive)  * 50.0;  // zabicie wroga boli
        v += (enCentDist - myCentDist) * 10.0; // bliżej środka = lepiej
        v += (myDisp   - enDisp)   * 5.0;   // większe rozproszenie = trudniej nas zbombić

        return v;
    }

}
