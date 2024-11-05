using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

class Player
{
    static int visionRange;
    static int homeDistanceHorizontal;
    static int homeDistanceVertical;
    static (int x, int y) lazPosition = (0, 0); // Lazzie's relative position on the map
    static (int x, int y) homePosition; // Relative position of home
    static Dictionary<(int x, int y), char> map = new Dictionary<(int, int), char>(); // Known map
    static PriorityQueue<(int x, int y), double> openList = new PriorityQueue<(int x, int y), double>();
    static HashSet<(int x, int y)> openSet = new HashSet<(int x, int y)>();
    static Dictionary<(int x, int y), double> gValues = new Dictionary<(int x, int y), double>();
    static Dictionary<(int x, int y), double> rhsValues = new Dictionary<(int x, int y), double>();

    static void PrintStates()
    {
        Console.Error.WriteLine($"Laz position: {lazPosition}");
        Console.Error.WriteLine($"Home position: {homePosition}");
        // Console.Error.WriteLine("Map:");
        // Console.Error.WriteLine(string.Join(Environment.NewLine, map));
        Console.Error.WriteLine("GValues:");
        Console.Error.WriteLine(string.Join(Environment.NewLine, gValues));
        Console.Error.WriteLine("rhsValues:");
        Console.Error.WriteLine(string.Join(Environment.NewLine, rhsValues));
        Console.Error.WriteLine("openSet:");
        Console.Error.WriteLine(string.Join(";", openSet.ToArray()));
    }

    static void Main(string[] args)
    {
        string[] inputs = Console.ReadLine().Split(' ');
        visionRange = int.Parse(inputs[0]); // Diameter of Lazzie's vision
        homeDistanceHorizontal = int.Parse(inputs[1]); // Distance in W/E direction towards home (negative means W side, positive means E side)
        homeDistanceVertical = int.Parse(inputs[2]); // Distance in N/S direction towards home (negative means N side, positive means S side)
        homePosition = (homeDistanceHorizontal, homeDistanceVertical);
        
        InitializeDStarLite();
        PrintStates();
        // game loop
        while (true)
        {
            UpdateVision();
            PrintStates();
            // To debug: Console.Error.WriteLine("Debug messages...");

            //string path = PlanPath();

            // Output next direction
            Console.WriteLine(PlanPath());

        }
    }

    static void InitializeDStarLite()
    {
        gValues[lazPosition] = 0;
        rhsValues[homePosition] = 0;
        openList.Enqueue(homePosition, Heuristic(lazPosition, homePosition));
        openSet.Add(homePosition);
    }

    static void UpdateVision()
    {
        int halfVision = visionRange / 2;
        for (int i = -halfVision; i <= halfVision; i++)
        {
            string row = Console.ReadLine();
            //Console.Error.WriteLine(row);
            for (int j = -halfVision; j <= halfVision; j++)
            {
                (int x, int y) position = (lazPosition.x + j, lazPosition.y + i);
                char cell = row[j + halfVision];
                if (cell != '?') map[position] = cell;

                if (cell == '#')
                    UpdateCell(position);
            }
        }
        Console.Error.WriteLine();
    }

    static void UpdateCell((int x, int y) position)
    {
        // Mark cell as obstacle, update D* Lite state, and replan if necessary
        if (!map.ContainsKey(position))
        {
            map[position] = '#';
            rhsValues[position] = double.PositiveInfinity;
            UpdateVertex(position);
        }
    }

    static void UpdateVertex((int x, int y) u)
    {
        if (u != homePosition)
        {
            rhsValues[u] = MinSuccessor(u);
        }

        // Remove from openSet instead of openList directly
        if (openSet.Contains(u))
            openSet.Remove(u);

        // Only enqueue if there's a mismatch in g and rhs values
        if (gValues.GetValueOrDefault(u, double.PositiveInfinity) != rhsValues.GetValueOrDefault(u, double.PositiveInfinity))
        {
            openList.Enqueue(u, CalculateKey(u));
            openSet.Add(u); // Mark it in openSet
        }
    }

    static double MinSuccessor((int x, int y) u)
    {
        // Calculate minimum rhs from neighbors
        double minRHS = double.PositiveInfinity;
        foreach (var v in GetNeighbors(u))  
        {
            if (map.GetValueOrDefault(v, '.') != '#')
            {
                double cost = 1 + gValues.GetValueOrDefault(v, double.PositiveInfinity);
                minRHS = Math.Min(minRHS, cost);
            }
        }
        return minRHS;
    }

    static double Heuristic((int x, int y) a, (int x, int y) b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    static double CalculateKey((int x, int y) u) =>
        Math.Min(gValues.GetValueOrDefault(u, double.PositiveInfinity), rhsValues.GetValueOrDefault(u, double.PositiveInfinity)) +
        Heuristic(lazPosition, u);

    static string PlanPath()
    {
        (int x, int y) current = default;
        while (openList.TryDequeue(out current, out _) && !openSet.Contains(current))
        {
            // Skip elements that are no longer in openSet (they were "removed" lazily)
            continue;
        }

        // If openList is empty or no valid path found
        if (!openSet.Contains(current))
            return ""; // No valid path found

        // Process the current node
        openSet.Remove(current); // Officially remove from openSet

        if (gValues.GetValueOrDefault(current, double.PositiveInfinity) > rhsValues.GetValueOrDefault(current, double.PositiveInfinity))
        {
            // Overconsistent: update gValue to match rhsValue
            gValues[current] = rhsValues[current];
            foreach (var neighbor in GetNeighbors(current))
            {
                UpdateVertex(neighbor);
            }
        }
        else
        {
            // Underconsistent: reset gValue and update neighbors
            gValues[current] = double.PositiveInfinity;
            UpdateVertex(current);
            foreach (var neighbor in GetNeighbors(current))
            {
                UpdateVertex(neighbor);
            }
        }

        // After planning, return the path towards the home position
        return ExtractPathToHome();
    }

    static IEnumerable<(int x, int y)> GetNeighbors((int x, int y) position)
    {
        yield return (position.x + 1, position.y);
        yield return (position.x - 1, position.y);
        yield return (position.x, position.y + 1);
        yield return (position.x, position.y - 1);
    }

    static string ExtractPathToHome()
    {
        var path = new List<string>();
        var position = lazPosition;
        PrintStates();
        for(int i = 0; i < 10; i++)
        {
            // Choose the neighbor with the minimum gValue
            var nextPosition = MinNeighbor(position);
            if (nextPosition == position)
                break; // No path forward, likely due to an obstacle or incomplete path
            Console.Error.WriteLine(nextPosition);
            if (path.Count == 0)
                lazPosition = nextPosition;
            path.Add(DirectionTo(position, nextPosition));
            position = nextPosition;
        }

        return string.Join("", path); // Combine directions as a string
    }

    static (int x, int y) MinNeighbor((int x, int y) position)
    {
        (int x, int y) minNeighbor = position;
        double minGValue = double.PositiveInfinity;

        foreach (var neighbor in GetNeighbors(position))
        {
            if (gValues.GetValueOrDefault(neighbor, double.PositiveInfinity) < minGValue)
            {
                minGValue = gValues[neighbor];
                minNeighbor = neighbor;
            }
        }

        return minNeighbor;
    }

    static string DirectionTo((int x, int y) from, (int x, int y) to)
    {
        if (to.x == from.x + 1) return "E";
        if (to.x == from.x - 1) return "W";
        if (to.y == from.y + 1) return "S";
        if (to.y == from.y - 1) return "N";
        return "";
    }
}