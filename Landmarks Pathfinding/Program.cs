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
        private List<(int x, int y)> passableTiles;
        private HashSet<(int x, int y)> passableTilesSet;
        public Dictionary<(int x, int y), List<(int x, int y)>> landmarksPlacedInComponent;

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
            passableTiles = GetPassableTiles();
            passableTilesSet = passableTiles.ToHashSet();
            landmarksPlacedInComponent = new Dictionary<(int x, int y), List<(int x, int y)>>();
        }

        private List<List<(int x, int y)>> FindConnectedComponents()
        {
            var components = new List<List<(int x, int y)>>();
            var visited = new bool[map.GetLength(0), map.GetLength(1)];

            foreach (var tile in passableTiles)
            {
                if (!visited[tile.y, tile.x])
                {
                    var component = new List<(int x, int y)>();
                    var queue = new Queue<(int x, int y)>();
                    queue.Enqueue(tile);
                    visited[tile.y, tile.x] = true;

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        component.Add(current);

                        foreach (var neighbor in GetNeighbors(current))
                        {
                            if (!visited[neighbor.y, neighbor.x] && passableTilesSet.Contains(neighbor))
                            {
                                visited[neighbor.y, neighbor.x] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                    components.Add(component);
                }
            }

            return components;
        }

        public List<(int x, int y)> FarthestLandmarkSelection(int k)
        {
            List<(int x, int y)> landmarks = new List<(int, int)>();

            var components = FindConnectedComponents();
            
            double totalTiles = (double)passableTiles.Count;
            int remainingLandmarks = k;
            int remainingComponents = components.Count;
            var landmarksDistribution = new List<int>();
            foreach (var component in components)
            {
                int componentSize = component.Count;
                if (componentSize < 0.05 * totalTiles)
                {
                    landmarksDistribution.Add(0);
                    remainingComponents--;
                    continue;
                }

                int landmarksInComponent = (int)Math.Round((double)k * componentSize / totalTiles);
                landmarksDistribution.Add(landmarksInComponent);
            }

            int totalAssignedLandmarks = landmarksDistribution.Sum();
            if (totalAssignedLandmarks > k)
            {
                for (int i = 0; i < landmarksDistribution.Count; i++)
                {
                    if (totalAssignedLandmarks <= k) break;
                    if (landmarksDistribution[i] > 1)
                    {
                        landmarksDistribution[i]--;
                        totalAssignedLandmarks--;
                    }
                }
            }
            remainingLandmarks -= totalAssignedLandmarks;

            if (remainingLandmarks > 0)
            {
                int largestComponentIndex = -1;
                int largestComponentSize = 0;
                for (int i = 0; i < components.Count; i++)
                {
                    int componentSize = components[i].Count;
                    if (componentSize > largestComponentSize)
                    {
                        largestComponentSize = componentSize;
                        largestComponentIndex = i;
                    }
                }

                if (largestComponentIndex != -1)
                {
                    landmarksDistribution[largestComponentIndex] += remainingLandmarks;
                }
            }
            
            for (int i = 0; i < components.Count; i++)
            {
                var component = components[i];
                int landmarksInComponent = landmarksDistribution[i];
                if (landmarksInComponent > 0)
                {
                    Console.Error.WriteLine($"Give {landmarksInComponent} to component {component[0]} of size {component.Count}");
                    var componentLandmarks = SelectLandmarksInComponent(component, landmarksInComponent);
                    landmarks.AddRange(componentLandmarks);
                }
            }

            return landmarks;
        }

        private List<(int x, int y)> SelectLandmarksInComponent(List<(int x, int y)> component, int numLandmarks)
        {
            var random = new Random();
            var initialTile = component[random.Next(component.Count)];
            
            List<(int x, int y)> landmarks = new List<(int x, int y)>();
            var memoizedDistances = new Dictionary<(int x, int y), Dictionary<(int x, int y), double>>();
            if (!landmarksPlacedInComponent.ContainsKey(component[0]))
            {
                var distancesFromFirst = Dijkstra(initialTile);
                var farthestTile = distancesFromFirst.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
                landmarks.Add(farthestTile);
                memoizedDistances[initialTile] = distancesFromFirst;
                memoizedDistances[farthestTile] = Dijkstra(farthestTile);
                numLandmarks--;
            }
            else
            {
                Console.Error.WriteLine($"I was already in {component[0]} and placed here {landmarksPlacedInComponent[component[0]].Count} landmarks");
                landmarks = landmarksPlacedInComponent[component[0]];
                var c = string.Join(";", landmarks.Select(x => x.ToString()).ToArray());
                Console.Error.WriteLine($"Landmarks placed: {c}");
            }

            for (int i = 0; i < numLandmarks; i++)
            {
                var maxMinDistance = double.MinValue;
                (int x, int y) nextLandmark = (0, 0);

                foreach (var tile in component)
                {
                    if (landmarks.Contains(tile)) continue;

                    var minDistance = double.MaxValue;
                    foreach (var landmark in landmarks)
                    {
                        if (!memoizedDistances.ContainsKey(landmark))
                        {
                            memoizedDistances[landmark] = Dijkstra(landmark);
                        }

                        var distancesFromLandmark = memoizedDistances[landmark];
                        if (distancesFromLandmark.ContainsKey(tile))
                        {
                            minDistance = Math.Min(minDistance, distancesFromLandmark[tile]);
                        }
                    }

                    if (minDistance > maxMinDistance)
                    {
                        maxMinDistance = minDistance;
                        nextLandmark = tile;
                    }
                }

                landmarks.Add(nextLandmark);

                if (!memoizedDistances.ContainsKey(nextLandmark))
                {
                    memoizedDistances[nextLandmark] = Dijkstra(nextLandmark);
                }
            }
            landmarksPlacedInComponent[component[0]] = landmarks;
            return landmarks;
        }

        private Dictionary<(int x, int y), double> Dijkstra((int x, int y) start)
        {
            var distances = new Dictionary<(int x, int y), double>();
            var priorityQueue = new PriorityQueue<(int x, int y), double>();

            distances[start] = 0;
            priorityQueue.Enqueue(start, 0);

            var directions = new (int dx, int dy, double cost)[] {
                (0, 1, 1), (1, 0, 1), (0, -1, 1), (-1, 0, 1),
                (-1, -1, Math.Sqrt(2)), (-1, 1, Math.Sqrt(2)), (1, -1, Math.Sqrt(2)), (1, 1, Math.Sqrt(2))
            };

            while (priorityQueue.Count > 0)
            {
                var current = priorityQueue.Dequeue();
                double currentDist = distances[current];

                foreach (var (dx, dy, cost) in directions)
                {
                    (int x, int y) neighbor = (current.x + dx, current.y + dy);
                    if (!passableTilesSet.Contains(neighbor)) continue;

                    double newDist = currentDist + cost;
                    if (!distances.ContainsKey(neighbor) || newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        priorityQueue.Enqueue(neighbor, newDist);
                    }
                }
            }

            return distances;
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

        private List<(int x, int y)> GetNeighbors((int x, int y) tile)
        {
            var directions = new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (-1, -1), (-1, 1), (1, -1), (1, 1) };
            var neighbors = new List<(int x, int y)>();

            foreach (var (dx, dy) in directions)
            {
                (int x, int y) neighbor = (tile.x + dx, tile.y + dy);
                if (IsWithinBounds(neighbor) && map[neighbor.y, neighbor.x] == '.')
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        private bool IsWithinBounds((int x, int y) tile)
        {
            return tile.x >= 0 && tile.x < map.GetLength(1) && tile.y >= 0 && tile.y < map.GetLength(0);
        }

        private void PrintDictionary(Dictionary<(int x, int y), double> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                Console.Error.WriteLine($"Key: ({kvp.Key.x}, {kvp.Key.y}), Value: {kvp.Value}");
            }
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
            Console.Error.WriteLine(landmarksNum);
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