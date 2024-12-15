using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Help Lazzie come home safely.
 **/
class Player
{
    static int visionRange;
    static int homeDistanceHorizontal;
    static int homeDistanceVertical;
    static (int x, int y) lazPosition = (0, 0);
    static (int x, int y) homePosition;
    static Dictionary<(int x, int y), char> map = new Dictionary<(int, int), char>();
    static List<(int x, int y)> currentPath = new List<(int x, int y)>();
    static void Main(string[] args)
    {
        string[] inputs = Console.ReadLine().Split(' ');
        visionRange = int.Parse(inputs[0]); // Diameter of Lazzie's vision
        homeDistanceHorizontal = int.Parse(inputs[1]); // Distance in W/E direction towards home (negative means W side, positive means E side)
        homeDistanceVertical = int.Parse(inputs[2]); // Distance in N/S direction towards home (negative means N side, positive means S side)
        homePosition = (homeDistanceHorizontal, homeDistanceVertical);
        currentPath = AStarPath(lazPosition, homePosition);
        // game loop
        while (true)
        {
            UpdateVision();

            if (currentPath.Count == 0 || ObstacleOnPath())
            {
                Console.Error.WriteLine($"Obstacle found!");
                currentPath = AStarPath(lazPosition, homePosition);
            }

            // Follow the path if available
            string nextMove = "";
            if (currentPath.Count > 0)
            {
                (int x, int y) nextPosition = currentPath[0];
                nextMove = DirectionTo(lazPosition, nextPosition);
                lazPosition = nextPosition;
                currentPath.RemoveAt(0); // Move along the path
            }

            Console.WriteLine(nextMove);

        }
    }

    static bool ObstacleOnPath()
    {
        foreach (var pos in currentPath)
        {
            if (map.ContainsKey(pos) && map[pos] == '#')
                return true;
        }
        return false;
    }

    static void UpdateVision()
    {
        int halfVision = visionRange / 2;

        for (int i = -halfVision; i <= halfVision; i++)
        {
            string row = Console.ReadLine();
            Console.Error.WriteLine(row);
            for (int j = -halfVision; j <= halfVision; j++)
            {
                (int x, int y) position = (lazPosition.x + j, lazPosition.y + i);
                char cell = row[j + halfVision];
                if (cell != '?') map[position] = cell;
            }
        }
        Console.Error.WriteLine();
    }

    static string PathToDirections(List<(int x, int y)> path, int amount=10)
    {
        var directions = new StringBuilder();
        for (int i = 1; i < amount; i++)
        {
            directions.Append(DirectionTo(path[i - 1], path[i]));
        }
        return directions.ToString();
    }

    static List<(int x, int y)> AStarPath((int x, int y) start, (int x, int y) goal)
    {
        var openSet = new PriorityQueue<(int x, int y), double>();
        var cameFrom = new Dictionary<(int x, int y), (int x, int y)>();
        var gScore = new Dictionary<(int x, int y), double> { [start] = 0 };
        openSet.Enqueue(start, Heuristic(start, goal));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current))
            {
                if (map.GetValueOrDefault(neighbor, '.') == '#') continue; // Ignore obstacles

                double tentativeGScore = gScore[current] + 1;
                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, double.PositiveInfinity))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    double fScore = tentativeGScore + Heuristic(neighbor, goal);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }
        return new List<(int x, int y)>(); // Return empty if no path found
    }

    static List<(int x, int y)> ReconstructPath(Dictionary<(int x, int y), (int x, int y)> cameFrom, (int x, int y) current)
    {
        var path = new List<(int x, int y)>();
        while (cameFrom.ContainsKey(current))
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }

    static void PrintStates()
    {
        Console.Error.WriteLine($"Laz position: {lazPosition}");
        Console.Error.WriteLine($"Home position: {homePosition}");
    }

    static string DirectionTo((int x, int y) from, (int x, int y) to)
    {
        if (to.x == from.x + 1) return "E";
        if (to.x == from.x - 1) return "W";
        if (to.y == from.y + 1) return "S";
        if (to.y == from.y - 1) return "N";
        return "";
    }

    static void MoveLaz(string direction)
    {
        if (direction == "E")
            lazPosition = (lazPosition.x + 1, lazPosition.y);
        else if (direction == "N")
            lazPosition = (lazPosition.x, lazPosition.y - 1);
        else if (direction == "W")
            lazPosition = (lazPosition.x - 1, lazPosition.y);
        else if (direction == "S")
            lazPosition = (lazPosition.x, lazPosition.y + 1);
    }

    static IEnumerable<(int x, int y)> GetNeighbors((int x, int y) position)
    {
        yield return (position.x + 1, position.y);
        yield return (position.x - 1, position.y);
        yield return (position.x, position.y + 1);
        yield return (position.x, position.y - 1);
    }

    static double Heuristic((int x, int y) a, (int x, int y) b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
}