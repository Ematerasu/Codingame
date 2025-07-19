using SummerChallenge2025.Bot;
using System;
using System.Text;

namespace SummerChallenge2025.Engine;

public interface IVisualizer
{
    void UpdateOrders(TurnCommand p0, TurnCommand p1);
    void Render(GameState state);
}

public class Visualizer : IVisualizer
{
    private readonly Dictionary<int, AgentOrder> last = new();

    public void UpdateOrders(TurnCommand p0, TurnCommand p1)
    {
        last.Clear();
        Copy(p0); Copy(p1);

        void Copy(TurnCommand cmd)
        {
            foreach (int id in cmd.EnumerateActive())
                last[id] = cmd.Get(id);
        }
    }

    public void Render(GameState state)
    {
        var sb = new StringBuilder();
        int w = state.W, h = state.H;

        sb.AppendLine($"=== TURN {state.Turn} ===");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                char c = TileChar(GameState.Tiles[GameState.ToIndex((byte)x, (byte)y)]);
                int id = state.AgentAt((byte)x, (byte)y);
                if (id != -1) c = AgentChar(state, id);
                sb.Append(c);
            }
            sb.AppendLine();
        }
        sb.AppendLine();

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            ref readonly var ag = ref state.Agents[id];
            if (!ag.Alive) continue;

            char n = AgentChar(state, id);
            sb.Append($"{n}: ({ag.X},{ag.Y}) 💧{ag.Wetness} 🔫{ag.Cooldown} 💣{ag.SplashBombs}");
            if (ag.Hunkering) sb.Append(" 🛡️");

            if (last.TryGetValue(id, out var ord))
                sb.Append("  ▶ ").Append(FormatOrder(ord));
            sb.AppendLine();
        }
        Console.WriteLine(sb.ToString());
    }

    private static char TileChar(TileType t) => t switch
    {
        TileType.Empty     => '.',
        TileType.LowCover  => 'l',
        TileType.HighCover => 'h',
        _ => '?'
    };

    // stały identyfikator: gracz-0 ⇒ A,B,C…, gracz-1 ⇒ X,Y,Z…
    private static char AgentChar(GameState st, int id)
    {
        int localIdx = id; // u Ciebie id 0-4 to P0, 5-9 to P1 – wystarczy modulo
        return st.Agents[id].playerId == 0
                ? (char)('A' + localIdx)
                : (char)('X' + (localIdx % 5));
    }

    private static string FormatOrder(in AgentOrder o)
    {
        var sb = new StringBuilder();

        if (o.Move.Type == MoveType.Step &&
            !(o.Move.X == 0 && o.Move.Y == 0))
            sb.Append($"MOVE {o.Move.X} {o.Move.Y};");

        switch (o.Combat.Type)
        {
            case CombatType.Shoot:
                sb.Append($"SHOOT {o.Combat.Arg1}");
                break;
            case CombatType.Throw:
                sb.Append($"THROW {o.Combat.Arg1} {o.Combat.Arg2}");
                break;
            case CombatType.Hunker:
                sb.Append("HUNKER_DOWN");
                break;
        }
        return sb.ToString();
    }
}