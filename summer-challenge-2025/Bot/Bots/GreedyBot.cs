namespace SummerChallenge2025.Bot;

// public class GreedyBot : AI
// {
//     private readonly Random rng = new Random();
//     public override TurnCommand GetMove(GameState state)
//     {
//         var cmd = new TurnCommand(GameState.MaxAgents);
//         Span<AgentOrder> buffer = stackalloc AgentOrder[512];

//         for (int id = 0; id < GameState.MaxAgents; ++id)
//         {
//             ref readonly var ag = ref state.Agents[id];
//             if (!ag.Alive || ag.playerId != PlayerId) continue;

//             int n = state.GetLegalOrders(id, buffer);
//             if (n == 0) continue;

//             AgentOrder best = buffer[0];
//             double bestScore = double.NegativeInfinity;

//             for (int i = 0; i < n; i++)
//             {
//                 var move = buffer[i];
//                 double score = 0;

//                 // --- 1. Heurystyka kierunku X (w stronę środka mapy) ---
//                 double dirX = Math.Sign(state.W / 2.0 - ag.X) * (move.Move.X - ag.X);
//                 score += dirX * 2.0;

//                 // --- 2. Kara za bycie blisko sojusznika (rozproszenie) ---
//                 for (int j = 0; j < GameState.MaxAgents; ++j)
//                 {
//                     if (j == id || !state.Agents[j].Alive || state.Agents[j].playerId != PlayerId) continue;
//                     int dist = GameState.Mdist(move.Move.X, move.Move.Y, state.Agents[j].X, state.Agents[j].Y);
//                     if (dist == 0) score -= 1000;
//                     else score -= 5.0 / dist;
//                 }

//                 // --- 3. Walka ---
//                 switch (move.Combat.Type)
//                 {
//                     case CombatType.Shoot:
//                         {
//                             int targetId = move.Combat.Arg1;
//                             ref readonly var enemy = ref state.Agents[targetId];
//                             int dist = GameState.Mdist(ag.X, ag.Y, enemy.X, enemy.Y);
//                             bool behindCover = IsBehindCover(state, ag, enemy);

//                             score += 100 - enemy.Wetness;
//                             if (behindCover) score += 20;
//                         }
//                         break;

//                     case CombatType.Throw:
//                         {
//                             int tx = move.Combat.Arg1;
//                             int ty = move.Combat.Arg2;
//                             int enemyCount = 0, selfCount = 0;

//                             for (int eid = 0; eid < GameState.MaxAgents; ++eid)
//                             {
//                                 ref readonly var other = ref state.Agents[eid];
//                                 if (!other.Alive) continue;
//                                 if (Math.Abs(other.X - tx) <= 1 && Math.Abs(other.Y - ty) <= 1)
//                                 {
//                                     if (other.playerId == PlayerId) selfCount++;
//                                     else enemyCount++;
//                                 }
//                             }

//                             if (selfCount > 0 || enemyCount < 3)
//                                 score -= 9999;
//                             else
//                                 score += enemyCount * 25;
//                         }
//                         break;

//                     case CombatType.Hunker:
//                         score += 1;
//                         break;
//                 }

//                 if (score > bestScore)
//                 {
//                     bestScore = score;
//                     best = move;
//                 }
//             }

//             cmd.SetMove(id, best.Move);
//             cmd.SetCombat(id, best.Combat);
//         }

//         return cmd;
//     }

//     private bool IsBehindCover(GameState state, in AgentState shooter, in AgentState target)
//     {
//         int dx = target.X - shooter.X;
//         int dy = target.Y - shooter.Y;

//         if (Math.Abs(dx) > 1)
//         {
//             int cx = target.X - Math.Sign(dx);
//             int cy = target.Y;
//             if (GameState.cd(cx, cy, shooter.X, shooter.Y) > 1 &&
//                 state.IsInBounds((byte)cx, (byte)cy))
//             {
//                 var tile = state.Tiles[state.ToIndex((byte)cx, (byte)cy)];
//                 if (tile == TileType.LowCover || tile == TileType.HighCover)
//                     return true;
//             }
//         }

//         if (Math.Abs(dy) > 1)
//         {
//             int cx = target.X;
//             int cy = target.Y - Math.Sign(dy);
//             if (GameState.Cdist(cx, cy, shooter.X, shooter.Y) > 1 &&
//                 state.IsInBounds((byte)cx, (byte)cy))
//             {
//                 var tile = state.Tiles[state.ToIndex((byte)cx, (byte)cy)];
//                 if (tile == TileType.LowCover || tile == TileType.HighCover)
//                     return true;
//             }
//         }

//         return false;
//     }
// }
