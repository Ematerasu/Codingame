#pragma warning disable 8602,8600
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Place the available landmarks to make pathfinding on a given map most efficient.
 **/

public class Watch
{
    public static int TIME_LIMIT = 20_000; // 20 seconds
    public static System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch GlobalStopwatch = new System.Diagnostics.Stopwatch();
}

class State
{
    private readonly char[,] map;
    public int Width { get; }
    public int Height { get; }

    public State(int width, int height, List<string> mapRows)
    {
        Width = width;
        Height = height;
        map = new char[height, width];

        // Populate the map from the input strings
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                map[row, col] = mapRows[row][col];
            }
        }
    }

    public List<(int x, int y)> FarthestLandmarkSelection(int k)
    {
        List<(int x, int y)> landmarks = new List<(int, int)>();
        var passableTiles = GetPassableTiles();
        if (passableTiles.Count == 0) throw new Exception("No passable tiles found!");

        var random = new Random();
        var randomVertex = passableTiles[random.Next(passableTiles.Count)];
        (int x, int y) farthestTile = (0, 0);
        double maxDistance = -1;
        foreach(var tile in passableTiles)
        {
            double distance = EuclideanDistance(tile, randomVertex);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthestTile = tile;
            }
        }
        landmarks.Add(farthestTile);

        for (int i = 1; i < k; i++)
        {
            farthestTile = (0, 0);
            maxDistance = -1;

            foreach (var tile in passableTiles)
            {
                double minDistance = double.MaxValue;

                // Calculate minimum distance from this tile to any existing landmark
                foreach (var landmark in landmarks)
                {
                    double distance = EuclideanDistance(tile, landmark);
                    minDistance = Math.Min(minDistance, distance);
                }

                // Keep track of the farthest tile from any landmark
                if (minDistance > maxDistance)
                {
                    maxDistance = minDistance;
                    farthestTile = tile;
                }
            }

            landmarks.Add(farthestTile);
        }

        return landmarks;
    }

    private double EuclideanDistance((int x, int y) p1, (int x, int y) p2)
    {
        return Math.Sqrt(Math.Pow(p1.x - p2.x, 2) + Math.Pow(p1.y - p2.y, 2));
    }

    private List<(int x, int y)> GetPassableTiles()
    {
        var passableTiles = new List<(int, int)>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (map[y, x] == '.')
                {
                    passableTiles.Add((x, y));
                }
            }
        }
        return passableTiles;
    }
}

class Player
{
    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        Watch.GlobalStopwatch.Start();
        int landmarksNum = int.Parse(inputs[0]); // Number of landmarks to place
        float efficiency = float.Parse(inputs[1]); // Minimal average efficiency required to pass the test
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]); // Width of the map
        int height = int.Parse(inputs[1]); // Height of the map
        var mapRows = new List<string>();
        for (int i = 0; i < height; i++)
        {
            mapRows.Add(Console.ReadLine()); // A single row of the map consisting of passable terrain ('.') and walls ('#')
        }
        State state = new State(width, height, mapRows);
        var landmarks = state.FarthestLandmarkSelection(landmarksNum);
        foreach(var landmark in landmarks)
        {
            Console.WriteLine($"{landmark.x} {landmark.y}");
        }
    }
}
#pragma warning restore 8602