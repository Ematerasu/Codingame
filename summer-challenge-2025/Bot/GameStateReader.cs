using System.IO;

namespace SummerChallenge2025.Bot;

public static class GameStateReader
{
    private static bool       _initDone;
    private static GameState  _baseState = null!;
    private static int turn = -1;
    public static GameState ReadFromInput(TextReader input)
    {
        if (!_initDone)
            ReadInit(input);

        var gs = _baseState.FastClone();

        int agentCount = int.Parse(input.ReadLine()!);

        for (int i = 0; i < GameState.MaxAgents; ++i)
            gs.Agents[i].Alive = false;
        List<(int id, byte x, byte y, int cooldown, int bombs, int wetness)> agents = new();
        for (int i = 0; i < agentCount; ++i)
        {
            var tok = input.ReadLine()!.Split(' ');
            int id = int.Parse(tok[0]) - 1;
            byte x = byte.Parse(tok[1]);
            byte y = byte.Parse(tok[2]);
            int cooldown = int.Parse(tok[3]);
            int bombs = int.Parse(tok[4]);
            int wetness = int.Parse(tok[5]);
            agents.Add((id, x, y, cooldown, bombs, wetness));
        }
        gs.UpdateFromInput(agents);
        _ = input.ReadLine();
        ++turn;
        gs.Turn = turn;
        return gs;
    }

    //──────────────────────── init helpers ──────────────────────────────
    private static void ReadInit(TextReader input)
    {
        int agentDataCount  = int.Parse(input.ReadLine()!);

        var tmpAgents = new (int id,int player,int cd,int range,int power,int bombs)[agentDataCount];
        for (int i = 0; i < agentDataCount; ++i)
        {
            var t = input.ReadLine()!.Split(' ');
            tmpAgents[i] = (
                int.Parse(t[0]) - 1,          // 1-based → 0-based
                int.Parse(t[1]),
                int.Parse(t[2]),
                int.Parse(t[3]),
                int.Parse(t[4]),
                int.Parse(t[5]));
        }

        var dims = input.ReadLine()!.Split(' ');
        int w = int.Parse(dims[0]);
        int h = int.Parse(dims[1]);

        var gs = new GameState((byte)w, (byte)h);

        foreach (var a in tmpAgents)
        {
            GameState.AgentClasses[a.id] = AgentUtils.GuessClass(a.cd, a.range, a.power, a.bombs);
            gs.Agents[a.id] = new AgentState
            {
                playerId    = a.player,
                SplashBombs = a.bombs,
                Alive       = true
            };
        }

        for (int y = 0; y < h; ++y)
        {
            string[] row = input.ReadLine()!.Split(' ');
            for (int x = 0; x < w; ++x)
            {
                int tileType = int.Parse(row[3 * x + 2]);
                GameState.Tiles[GameState.ToIndex((byte)x,(byte)y)] = (TileType)tileType;
            }
        }

        _baseState = gs;
        _initDone  = true;
    }
}