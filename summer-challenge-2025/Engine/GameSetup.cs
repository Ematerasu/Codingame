using System;
using System.Collections.Generic;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Engine;

public static class GameSetup
{
    private const int MinHeight = 6;
    private const int MaxHeight = 10;
    private const int GridWRatio = 2;
    private const int MinSpawnCount = 3;
    private const int MaxSpawnCount = 5;

    public static GameState GenerateRandomState(Random rng, int myPlayerId = 0)
    {
        // ── 1. Parametry mapy (zawężamy do stałych GameState) ────────────────
        int h = rng.Next(MinHeight, MaxHeight + 1);          // 6-10
        int w = h * GridWRatio;                              // 12-20

        bool ySym = rng.Next(4) < 3;    // 75 % szans na symetrię w pionie

        var gs = new GameState((byte)w, (byte)h);

        // ── 2. Losowe COVERY, lustrzane w osi X ( ± Y-sym) ───────────────────
        for (int y = 1; y < h - 1; ++y)
        {
            for (int x = 1; x < w / 2; ++x)          // lewa połowa bez brzegów
            {
                int n = rng.Next(10);                // 0..9
                TileType t = TileType.Empty;
                if (n == 0) t = TileType.HighCover;
                else if (n == 1) t = TileType.LowCover;

                int idxL = GameState.ToIndex((byte)x, (byte)y);
                int idxR = GameState.ToIndex((byte)(w - 1 - x), (byte)y);

                GameState.Tiles[idxL] = t;
                GameState.Tiles[idxR] = t;

                if (ySym)
                {
                    int idxL2 = GameState.ToIndex((byte)x, (byte)(h - 1 - y));
                    int idxR2 = GameState.ToIndex((byte)(w - 1 - x), (byte)(h - 1 - y));
                    GameState.Tiles[idxL2] = t;
                    GameState.Tiles[idxR2] = t;
                }
            }
        }

        // ── 3. Spawny po lewej krawędzi ─────────────────────────────────────
        List<byte> spawnRows = new();
        for (byte y = 0; y < h; ++y) spawnRows.Add(y);
        rng.Shuffle(spawnRows);

        int spawnCnt = rng.Next(MinSpawnCount, MaxSpawnCount + 1);
        if (spawnCnt == 5 && rng.Next(2) == 0) spawnCnt--;
        if (spawnCnt == 4 && rng.Next(2) == 0) spawnCnt--;

        var allClasses = Enum.GetValues(typeof(AgentClass)).Cast<AgentClass>().ToList();
        rng.Shuffle(allClasses);

        // ── 4. Rozstawienie agentów (symetrycznie) ──────────────────────────
        for (int i = 0; i < spawnCnt; ++i)
        {
            byte y = spawnRows[i];

            AgentClass cls = allClasses[i % allClasses.Count];
            int id0 = i;
            int id1 = i + spawnCnt;

            GameState.AgentClasses[id0] = cls;
            GameState.AgentClasses[id1] = cls;

            gs.Agents[id0] = new AgentState
            {
                X = 0,
                Y = y,
                Alive = true,
                playerId = myPlayerId,
                SplashBombs = AgentUtils.Balloons[cls],
                Cooldown = 0
            };
            gs.Occup.Set(GameState.ToIndex(0, y));

            byte y1 = y;
            byte x1 = (byte)(w - 1);

            gs.Agents[id1] = new AgentState
            {
                X = x1,
                Y = y1,
                Alive = true,
                playerId = 1 - myPlayerId,
                SplashBombs = AgentUtils.Balloons[cls],
                Cooldown = 0
            };
            gs.Occup.Set(GameState.ToIndex(x1, y1));
        }

        return gs;
    }

    public static GameState GenerateRandomBitState(Random rng, int myPlayerId = 0)
    {
        // ── 1. Parametry mapy ──────────────────────────────────────────────────
        int h = rng.Next(MinHeight, MaxHeight + 1);      // 6–10
        int w = h * GridWRatio;                          // 12–20
        bool ySym = rng.Next(4) < 3;                     // 75% szans na symetrię Y

        TileType[] tiles = new TileType[GameState.Cells];
        for (int y = 1; y < h - 1; ++y)
        {
            for (int x = 1; x < w / 2; ++x)
            {
                int n = rng.Next(10); // 0..9
                TileType t = TileType.Empty;
                if (n == 0) t = TileType.HighCover;
                else if (n == 1) t = TileType.LowCover;

                int idxL = GameState.ToIndex(x, y);
                int idxR = GameState.ToIndex(w - 1 - x, y);
                tiles[idxL] = t;
                tiles[idxR] = t;

                if (ySym)
                {
                    int idxL2 = GameState.ToIndex(x, h - 1 - y);
                    int idxR2 = GameState.ToIndex(w - 1 - x, h - 1 - y);
                    tiles[idxL2] = t;
                    tiles[idxR2] = t;
                }
            }
        }

        // ── 2. Agenci – symetryczne rozmieszczenie ─────────────────────────────
        List<byte> spawnRows = new();
        for (byte y = 0; y < h; ++y) spawnRows.Add(y);
        rng.Shuffle(spawnRows);

        int spawnCnt = rng.Next(MinSpawnCount, MaxSpawnCount + 1);
        if (spawnCnt == 5 && rng.Next(2) == 0) spawnCnt--;
        if (spawnCnt == 4 && rng.Next(2) == 0) spawnCnt--;

        var allClasses = Enum.GetValues(typeof(AgentClass)).Cast<AgentClass>().ToList();
        rng.Shuffle(allClasses);

        AgentClass[] classes = new AgentClass[GameState.MaxAgents];
        AgentState[] agents = new AgentState[GameState.MaxAgents];

        var gs = new GameState((byte)w, (byte)h);

        for (int i = 0; i < spawnCnt; ++i)
        {
            byte y = spawnRows[i];
            AgentClass cls = allClasses[i % allClasses.Count];
            int id0 = i;
            int id1 = i + spawnCnt;

            classes[id0] = cls;
            classes[id1] = cls;

            agents[id0] = new AgentState
            {
                X = 0,
                Y = y,
                Alive = true,
                playerId = myPlayerId,
                SplashBombs = AgentUtils.Balloons[cls],
                Cooldown = 0
            };
            gs.Occup.Set(GameState.ToIndex(0, y));

            byte y1 = y;
            byte x1 = (byte)(w - 1);

            agents[id1] = new AgentState
            {
                X = x1,
                Y = y1,
                Alive = true,
                playerId = 1 - myPlayerId,
                SplashBombs = AgentUtils.Balloons[cls],
                Cooldown = 0
            };
            gs.Occup.Set(GameState.ToIndex(x1, y1));
        }

        // Kopiujemy agentów do `gs.Agents`
        for (int i = 0; i < GameState.MaxAgents; i++)
            gs.Agents[i] = agents[i];

        // Zainicjalizuj statyczne dane
        GameState.InitStatic(tiles, classes);

        return gs;
    }

}

public static class RandomExtensions
{
    public static void Shuffle<T>(this Random rng, IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}