using System;
using System.Collections.Generic;
using winter_challenge_2024;

public class GameStateGenerator
{
    private static readonly int GRID_W_RATIO = 2;
    private static readonly int MAX_SPAWN_DIST_FROM_CORNER = 4;
    private static readonly int MAX_PROTEINS = 10;
    private static readonly int MIN_PROTEINS = 3;

    public static GameState GenerateGameState(Random random)
    {
        // Ustal wymiary planszy
        int height = random.Next(8, 13);
        int width = height * GRID_W_RATIO;

        // Tworzymy GameState
        GameState gameState = new GameState(width, height)
        {
            Grid = new Entity[width, height],
            Player0Entities = new Dictionary<int, Entity>(),
            Player1Entities = new Dictionary<int, Entity>(),
            Player0Proteins = (0, 0, 0, 0),
            Player1Proteins = (0, 0, 0, 0)
        };

        // Inicjalizuj siatkę jako pustą
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                gameState.Grid[x, y] = new Entity { Position = (x, y) };
            }
        }

        // Generuj przeszkody (WALL)
        int maxObstacleCount = (int)(0.30 * width * height); // Max 50% przeszkód
        HashSet<(int x, int y)> usedCoords = new HashSet<(int x, int y)>();
        for (int i = 0; i < maxObstacleCount; i++)
        {
            var coord = GetRandomFreeCoord(random, usedCoords, width, height);
            if (coord == null) break;
            (int x, int y) = coord.Value;

            gameState.Grid[x, y].Type = CellType.WALL;
            usedCoords.Add((x, y));
        }

        // Generuj białka (PROTEIN_*)
        int maxProteinCount = (int)(0.15 * width * height); // Max 25% białek
        CellType[] proteinTypes = { CellType.PROTEIN_A, CellType.PROTEIN_B, CellType.PROTEIN_C, CellType.PROTEIN_D };
        for (int i = 0; i < maxProteinCount; i++)
        {
            var coord = GetRandomFreeCoord(random, usedCoords, width, height);
            if (coord == null) break;
            (int x, int y) = coord.Value;

            gameState.Grid[x, y].Type = proteinTypes[i % proteinTypes.Length];
            usedCoords.Add((x, y));
        }

        // Ustaw pozycje startowe graczy
        var spawn0 = GetSpawnCoord(random, width, height);
        var spawn1 = (width - 1 - spawn0.x, height - 1 - spawn0.y); // Odbicie symetryczne
        ClearCell(gameState, spawn0);
        ClearCell(gameState, spawn1);
        usedCoords.Add(spawn0);
        usedCoords.Add(spawn1);

        // Dodaj jednostki graczy (opcjonalne, jeśli potrzebujesz ich w testach)
        AddPlayerEntities(gameState, 0, 1, spawn0);
        AddPlayerEntities(gameState, 1, 2, spawn1);
        var proteins = (random.Next(MIN_PROTEINS, MAX_PROTEINS+1), random.Next(MIN_PROTEINS, MAX_PROTEINS+1), random.Next(MIN_PROTEINS, MAX_PROTEINS+1), random.Next(MIN_PROTEINS, MAX_PROTEINS+1));
        gameState.Player0Proteins = proteins;
        gameState.Player1Proteins = proteins;
        return gameState;
    }

    private static (int x, int y)? GetRandomFreeCoord(Random random, HashSet<(int x, int y)> usedCoords, int width, int height)
    {
        for (int attempts = 0; attempts < 10; attempts++)
        {
            int x = random.Next(width);
            int y = random.Next(height);

            if (!usedCoords.Contains((x, y)))
                return (x, y);
        }
        return null; // Nie znaleziono wolnego miejsca
    }

    private static (int x, int y) GetSpawnCoord(Random random, int width, int height)
    {
        int x = random.Next(0, MAX_SPAWN_DIST_FROM_CORNER + 1);
        int y = random.Next(0, MAX_SPAWN_DIST_FROM_CORNER + 1);
        return (x, y);
    }

    private static void ClearCell(GameState gameState, (int x, int y) coord)
    {
        gameState.Grid[coord.x, coord.y].Type = CellType.EMPTY;
    }

    private static void AddPlayerEntities(GameState gameState, int playerId, int id, (int x, int y) spawnCoord)
    {
        gameState.AddEntity(spawnCoord, CellType.ROOT, id, playerId, Direction.X, id, id);
    }
}
