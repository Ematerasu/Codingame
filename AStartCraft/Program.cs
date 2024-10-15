#define DEBUG_MODE
#define DEBUG_PRINT

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks.Sources;
using System.Security.Cryptography;



#if DEBUG_MODE
using System.Diagnostics;
#endif

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/

public class Debug
{
    private static string logFilePath = "debug_log.txt";
    public static void Log(string msg)
    {
        Console.Error.Write(msg);
    }

    public static void LogToFile(string msg, bool addNewLine=true)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                if (addNewLine)
                {
                    writer.WriteLine(msg);
                }
                else
                {
                    writer.Write(msg);
                }
                
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error writing to log file: " + ex.Message);
        }
    }

    public static void ClearLogFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, false))
            {
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error clearing log file: " + ex.Message);
        }
    }
}

public enum Direction
{
    Left,
    Right,
    Up,
    Down,
}

public enum CellType
{
    Void,
    Platform,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,
}

public struct Triplet
{
    public int X;
    public int Y;
    public Direction direction;

    public override string ToString()
    {
        switch (direction)
        {
            case Direction.Left:
                return $"{X} {Y} L";
            case Direction.Up:
                return $"{X} {Y} U";
            case Direction.Down:
                return $"{X} {Y} D";
            case Direction.Right:
                return $"{X} {Y} R";
            default:
                return "????";
        }

    }

    public static CellType GetArrowDirection(Direction direction)
    {
        return direction switch
        {
            Direction.Left => CellType.ArrowLeft,
            Direction.Right => CellType.ArrowRight,
            Direction.Up => CellType.ArrowUp,
            Direction.Down => CellType.ArrowDown,
        };
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Triplet other))
            return false;

        return X == other.X && Y == other.Y && direction == other.direction;
    }

    public override int GetHashCode()
    {
        // Combine the hash codes for X, Y, and direction
        int hash = 17; // Start with a prime number
        hash = hash * 31 + X.GetHashCode();
        hash = hash * 31 + Y.GetHashCode();
        hash = hash * 31 + direction.GetHashCode();
        return hash;
    }
}

public class Agent
{
    public int Id;
    public Direction DirectionFacing;
    public bool IsAlive;
    public int score;
    public bool[,] visited;
    public (int x, int y) CurrentPos;
    private (int x, int y) StartingPos;
    private Direction originalDirection;

    public Agent(int id, Direction directionFacing, (int x, int y) currentPos)
    {
        Id = id;
        DirectionFacing = directionFacing;
        score = 0;
        visited = new bool[190, 4];
        IsAlive = true;
        CurrentPos = currentPos;
        StartingPos = currentPos;
        originalDirection = directionFacing;
    }

    public override string ToString()
    {
        string isAlive = IsAlive ? "is alive" : "is dead";
        return $"Agent {Id} facing {DirectionFacing} located at ({CurrentPos.x}, {CurrentPos.y}) and {isAlive}.";
    }

    public void KillAgent()
    {
        IsAlive = false;
    }

    public bool IsInLoop()
    {
        //Debug.Log($"{CurrentPos.x} {CurrentPos.y} {(int)DirectionFacing}\n");
        return visited[CurrentPos.x * 10 + CurrentPos.y, (int)DirectionFacing];
    }

    public void ResetAgent()
    {
        CurrentPos = StartingPos;
        DirectionFacing = originalDirection;
        IsAlive = true;
        score = 0;
        visited = new bool[190, 4];
    }

    public void SetVisited()
    {
        //Debug.Log($"{CurrentPos.x} {CurrentPos.y} {(int)DirectionFacing}\n");
        visited[CurrentPos.x * 10 + CurrentPos.y, (int)DirectionFacing] = true;
    }

    public bool MoveAgent()
    {
        if (!IsAlive)
            return false;

        SetVisited();
        switch (DirectionFacing)
        {
            case Direction.Left:
                if (CurrentPos.x == 0)
                    CurrentPos = (18, CurrentPos.y);
                else
                    CurrentPos = (CurrentPos.x - 1, CurrentPos.y);
                break;
            case Direction.Up:
                if (CurrentPos.y == 0)
                    CurrentPos = (CurrentPos.x, 9);
                else
                    CurrentPos = (CurrentPos.x, CurrentPos.y - 1);
                break;
            case Direction.Right:
                if (CurrentPos.x == 18)
                    CurrentPos = (0, CurrentPos.y);
                else
                    CurrentPos = (CurrentPos.x + 1, CurrentPos.y);
                break;
            case Direction.Down:
                if (CurrentPos.y == 9)
                    CurrentPos = (CurrentPos.x, 0);
                else
                    CurrentPos = (CurrentPos.x, CurrentPos.y + 1);
                break;
            default:
                break;
        }
        score++;
        return IsAlive;
    }
}

public class State
{
    public CellType[][] Map;
    public HashSet<Agent> Agents;
    public List<Triplet>[,] PossibleArrowSpots;
    public HashSet<(int, int)> PlatformSpots;
    public List<(int, int)> PlatformSpotsList;
    public int possibleArrows;
    public bool[,] isArrowPossible;

    public State(CellType[][] map, HashSet<Agent> agents)
    {
        Map = map;
        Agents = agents;
        PossibleArrowSpots = new List<Triplet>[10, 19];
        possibleArrows = 0;
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 19; j++)
            {
                PossibleArrowSpots[i, j] = new List<Triplet>();
            }
        }
        PlatformSpots = new HashSet<(int, int)>();
        isArrowPossible = new bool[190,4];


        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 19; j++)
            {
                if (Map[i][j] != CellType.Platform)
                    continue;
                if (IsCorridor(j, i))
                {
                    continue;
                }
                    
                if ((i == 0 && Map[9][j] != CellType.Void) || (i > 0 && Map[i - 1][j] != CellType.Void))
                {
                    PossibleArrowSpots[i, j].Add(new Triplet { X = j, Y = i, direction = Direction.Up});
                    PlatformSpots.Add((j, i));
                    possibleArrows++;
                }
                if ((i == 9 && Map[0][j] != CellType.Void) || (i < 9 && Map[i + 1][j] != CellType.Void))
                {
                    PossibleArrowSpots[i, j].Add(new Triplet { X = j, Y = i, direction = Direction.Down });
                    PlatformSpots.Add((j, i));
                    possibleArrows++;
                }
                if ((j == 0 && Map[i][18] != CellType.Void) || (j > 0 && Map[i][j-1] != CellType.Void))
                {
                    PossibleArrowSpots[i, j].Add(new Triplet { X = j, Y = i, direction = Direction.Left });
                    PlatformSpots.Add((j, i));
                    possibleArrows++;
                }
                if ((j == 18 && Map[i][0] != CellType.Void) || (j < 18 && Map[i][j+1] != CellType.Void))
                {
                    PossibleArrowSpots[i, j].Add(new Triplet { X = j, Y = i, direction = Direction.Right });
                    PlatformSpots.Add((j, i));
                    possibleArrows++;
                }
            }
        }
        PlatformSpotsList = PlatformSpots.ToList();
        Debug.Log($"Platform spots: {PlatformSpots.Count()}, possible arrows: {possibleArrows}\n");
    }

    private void PrecomputeMap()
    {
        //Queue<(int, int)> queue = new Queue<(int, int)>();

        //for (int y = 0; y < Map.Length; y++)
        //{
        //    for (int x = 0; x < Map[y].Length; x++)
        //    {
        //        if (Map[y][x] == CellType.Void)
        //        {
        //            // Check for neighboring arrows that point to this void cell
        //            if (HasArrowPointingToVoid(x, y))
        //            {
        //                // Mark this cell as void and add to the queue
        //                queue.Enqueue((x, y));
        //            }
        //        }
        //    }
        //}
    }
    private bool IsCorridor(int x, int y)
    {
        int width = 19;
        int height = 10;

        int leftX = (x == 0) ? width - 1 : x - 1;
        int rightX = (x == width - 1) ? 0 : x + 1;
        int upY = (y == 0) ? height - 1 : y - 1;
        int downY = (y == height - 1) ? 0 : y + 1;

        bool hasVoidAboveAndBelow = (Map[upY][x] == CellType.Void && Map[downY][x] == CellType.Void);
        bool hasPlatformOnSides = (Map[y][leftX] == CellType.Platform && Map[y][rightX] == CellType.Platform);

        bool hasVoidLeftAndRight = (Map[y][leftX] == CellType.Void && Map[y][rightX] == CellType.Void);
        bool hasPlatformAboveAndBelow = (Map[upY][x] == CellType.Platform && Map[downY][x] == CellType.Platform);

        return (hasVoidAboveAndBelow && hasPlatformOnSides) || (hasVoidLeftAndRight && hasPlatformAboveAndBelow);
    }
    /// <summary>
    /// Get neigbours in order: left, right, up, down
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public (CellType, CellType, CellType, CellType) GetNeighbours(int x, int y)
    {
        int width = 19;
        int height = 10;

        int leftX = (x == 0) ? width - 1 : x - 1;
        int rightX = (x == width - 1) ? 0 : x + 1;
        int upY = (y == 0) ? height - 1 : y - 1;
        int downY = (y == height - 1) ? 0 : y + 1;

        return (Map[leftX][y], Map[rightX][y], Map[x][upY], Map[x][downY]);
    }

    private bool IsInCenter(int x, int y)
    {
        int width = 19;
        int height = 10;

        int leftX = (x == 0) ? width - 1 : x - 1;
        int rightX = (x == width - 1) ? 0 : x + 1;
        int upY = (y == 0) ? height - 1 : y - 1;
        int downY = (y == height - 1) ? 0 : y + 1;

        bool hasPlatformAboveAndBelow = (Map[upY][x] != CellType.Void && Map[downY][x] != CellType.Void);
        bool hasPlatformOnSides = (Map[y][leftX] != CellType.Void && Map[y][rightX] != CellType.Void);

        return (hasPlatformOnSides && hasPlatformAboveAndBelow);
    }

    public int Evaluate()
    {
        bool finished = false;
        int score = 0;
        foreach (Agent agent in Agents)
        {
            UpdateAgentDirection(agent);
        }
        while (!finished)
        {
            finished = true;
            foreach (Agent agent in Agents)
            {
                if (agent.IsAlive)
                {
                    finished = false;
                    agent.MoveAgent();
                    UpdateAgentDirection(agent);
                    if (Map[agent.CurrentPos.y][agent.CurrentPos.x] == CellType.Void || agent.IsInLoop())
                    {
                        agent.KillAgent();
                    }
                }
            }
        }
        foreach (Agent agent in Agents)
        {
            score += agent.score;
        }
        return score;
    }

    private void UpdateAgentDirection(Agent agent)
    {
        switch (Map[agent.CurrentPos.y][agent.CurrentPos.x])
        {
            case CellType.ArrowUp:
                agent.DirectionFacing = Direction.Up;
                break;
            case CellType.ArrowLeft:
                agent.DirectionFacing = Direction.Left;
                break;
            case CellType.ArrowDown:
                agent.DirectionFacing = Direction.Down;
                break;
            case CellType.ArrowRight:
                agent.DirectionFacing = Direction.Right;
                break;
            default:
                break;
        }
    }

    public void ResetAgents()
    {
        foreach (Agent agent in Agents)
        {
            agent.ResetAgent();
        }
    }

}

public abstract class SearchAlg
{
    public State state;
    protected int possiblePlatformsCount;
    public SearchAlg(State startState)
    {
        state = startState;
        possiblePlatformsCount = startState.PlatformSpots.Count;
    }

    public abstract List<Triplet> Evaluate();

    protected void ApplyArrows(List<Triplet> arrows)
    {
        foreach (var arrow in arrows)
        {
            switch (arrow.direction)
            {
                case Direction.Left:
                    state.Map[arrow.Y][arrow.X] = CellType.ArrowLeft;
                    break;
                case Direction.Right:
                    state.Map[arrow.Y][arrow.X] = CellType.ArrowRight;
                    break;
                case Direction.Up:
                    state.Map[arrow.Y][arrow.X] = CellType.ArrowUp;
                    break;
                case Direction.Down:
                    state.Map[arrow.Y][arrow.X] = CellType.ArrowDown;
                    break;
            }
        }
    }

    protected CellType ApplyCell((int x, int y, CellType type) cellChanged)
    {
        var save = state.Map[cellChanged.x][cellChanged.y];
        state.Map[cellChanged.x][cellChanged.y] = cellChanged.type;
        return save;
    }

    protected void RevertArrows(List<Triplet> arrows)
    {
        foreach (var arrow in arrows)
        {
            state.Map[arrow.Y][arrow.X] = CellType.Platform; // Revert to platform after simulation
        }
    }

    protected void RevertCell((int x, int y, CellType type) cellChanged, CellType saved)
    {
        state.Map[cellChanged.x][cellChanged.y] = saved;
    }
}

class FullRandomSearch : SearchAlg
{
    private Random random = new Random();
    private int N = 50000;
    private HashSet<string> simulatedArrowSets = new HashSet<string>();
    public FullRandomSearch(State startState) : base(startState) { }
    public override List<Triplet> Evaluate()
    {
        N -= 80 * state.possibleArrows;
#if DEBUG_PRINT
        Debug.Log($"Doing {N} simulations\n");
#endif
        List<Triplet> best = new List<Triplet>();
        int bestScore = -1;
#if DEBUG_MODE
        int evalsDone = 0;
        long timeSpentEvaluating = 0;
        long timeSpentGeneratingArrows = 0;
        long timecheckingForDuplicates = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif

        for (int attempt = 0; attempt < N; attempt++)
        {
#if DEBUG_MODE
            stopwatch.Restart();
#endif
            List<Triplet> currentArrows = GenerateRandomArrows();
#if DEBUG_MODE
            stopwatch.Stop();
            timeSpentGeneratingArrows += stopwatch.ElapsedTicks;
#endif
#if DEBUG_MODE
            stopwatch.Restart();
#endif
            if (IsDuplicateArrowSet(currentArrows))
                continue;
#if DEBUG_MODE
            stopwatch.Stop();
            timecheckingForDuplicates += stopwatch.ElapsedTicks;
#endif
            ApplyArrows(currentArrows);
#if DEBUG_MODE
            stopwatch.Restart();
#endif
            int score = state.Evaluate();
#if DEBUG_MODE
            stopwatch.Stop();
            timeSpentEvaluating += stopwatch.ElapsedTicks;
#endif
#if DEBUG_MODE
            evalsDone++;
#endif
            if (score > bestScore)
            {
                bestScore = score;
                best = currentArrows;
            }
            state.ResetAgents();
            RevertArrows(currentArrows);
        }
#if DEBUG_PRINT
        Debug.Log($"Best score found: {bestScore}\n");
        ApplyArrows(best);
        //Player.PrintMap(state.Map);
        Debug.Log($"Evals really done: {evalsDone}\n");
        Debug.Log($"Time spent evaluating: {timeSpentEvaluating / TimeSpan.TicksPerMillisecond}ms\n");
        Debug.Log($"Time spent generating arrows: {timeSpentGeneratingArrows / TimeSpan.TicksPerMillisecond}ms\n");
        Debug.Log($"Time spent checking for duplicates: {timecheckingForDuplicates / TimeSpan.TicksPerMillisecond}ms\n");
        //Debug.Log($"Whole function took {stopwatch.ElapsedMilliseconds}ms\n");
#endif
        return best;
    }

    private List<Triplet> GenerateRandomArrows()
    {
        int arrowCount = random.Next(possiblePlatformsCount);
        HashSet<Triplet> placedArrows = new HashSet<Triplet>();
        List<Triplet> triplets = new List<Triplet>();
        HashSet<(int, int)> spotsDone = new HashSet<(int, int)>();
        int x = 0;
        int y = 0;
        for (int i = 0; i < arrowCount; i++)
        {
            do
            {
                x = random.Next(10);
                y = random.Next(19);
            } while (spotsDone.Contains((x, y)));
            
            if (state.PlatformSpots.Contains((y, x)))
            {
                var cnt = state.PossibleArrowSpots[x, y].Count();
                triplets.Add(state.PossibleArrowSpots[x, y].ElementAt(random.Next(cnt)));
                spotsDone.Add((x, y));
            }
        }
        return triplets;
    }

    private bool IsDuplicateArrowSet(List<Triplet> arrows)
    {
        string arrowKey = string.Join(";", arrows.OrderBy(arrow => arrow.GetHashCode()).Select(a => $"{a.X}-{a.Y}-{a.direction}"));
        return !simulatedArrowSets.Add(arrowKey);
    }
}


class SimulatedAnnealing : SearchAlg
{
    private Random random = new Random();
    private const double TEMP_START = 10.0;
    private const double TEMP_END = 0.001;
    private const double COOLING_RATE = 0.97;
    private int N = 1000;
    private HashSet<string> simulatedArrowSets = new HashSet<string>();
    public SimulatedAnnealing(State startState) : base(startState)
    {
    }

    public override List<Triplet> Evaluate()
    {
        List<Triplet> bestArrows = new List<Triplet>();
        int bestScore = -1;

        double temperature = TEMP_START;
#if DEBUG_MODE
        int evalsDone = 0;
        long timeSpentEvaluating = 0;
        long timeSpentGeneratingArrows = 0;
        long timecheckingForDuplicates = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        int accepts = 0;
        int successes = 0;
        int duplicates = 0;
#endif
        List<Triplet> currentArrows = GenerateRandomArrows();
        ApplyArrows(currentArrows);
#if DEBUG_PRINT
        //Player.PrintMapToFile(state.Map);
        //Debug.LogToFile("Starting algorithm");
#endif
        while (temperature > TEMP_END)
        {
#if DEBUG_PRINT
            //Debug.Log($"Temperate: {temperature}\n");
#endif
            for (int i = 0; i < N; i++)
            {
#if DEBUG_MODE
                stopwatch.Restart();
#endif
                (List<Triplet> candidateArrows, (int, int, CellType) cellChanged) = GenerateNeighbour(currentArrows);
#if DEBUG_MODE
                stopwatch.Stop();
                //Debug.LogToFile("Current arrows: " + string.Join(";", currentArrows.Select(triplet => triplet.ToString())));
                //Debug.LogToFile("Candidate arrows: " + string.Join(";", candidateArrows.Select(triplet => triplet.ToString())));
                timeSpentGeneratingArrows += stopwatch.ElapsedTicks;
#endif
#if DEBUG_MODE
                //stopwatch.Restart();
#endif
                if (IsDuplicateArrowSet(candidateArrows))
                {
                    duplicates++;
                    continue;
                }
#if DEBUG_MODE
                //stopwatch.Stop();
                //timecheckingForDuplicates += stopwatch.ElapsedTicks;
#endif
                CellType save = ApplyCell(cellChanged);
#if DEBUG_PRINT
                //Player.PrintMapToFile(state.Map);
                //Debug.LogToFile($"Applied {cellChanged}");
#endif
#if DEBUG_MODE
                stopwatch.Restart();
#endif
                int candidateScore = state.Evaluate();
#if DEBUG_MODE
                stopwatch.Stop();
                timeSpentEvaluating += stopwatch.ElapsedTicks;
#endif
#if DEBUG_MODE
                evalsDone++;
#endif
                if (candidateScore > bestScore)
                {
                    //Debug.LogToFile($"Found better score. New: {candidateScore}, old: {bestScore}.");
                    bestScore = candidateScore;
                    currentArrows = candidateArrows;
                    //Debug.LogToFile("Current arrows: " + string.Join(";", currentArrows.Select(triplet => triplet.ToString())));
                    successes++;
                }
                else
                {
                    double acceptanceProbability = Math.Exp((candidateScore - bestScore) / temperature);
                    if (acceptanceProbability+0.25 < random.NextDouble())
                    {
                        //Debug.LogToFile($"Accepted new score {candidateScore}.");
                        bestScore = candidateScore;
                        currentArrows = candidateArrows;
                        //Debug.LogToFile("Current arrows: " + string.Join(";", currentArrows.Select(triplet => triplet.ToString())));
                        accepts++;
                    }
                    else
                    {
                        //Debug.LogToFile($"Score {candidateScore} was too low.");
                        RevertCell(cellChanged, save);
                    }
                }
                state.ResetAgents();
            }
            bestArrows = currentArrows;
            //Debug.LogToFile("Best arrows: " + string.Join(";", bestArrows.Select(triplet => triplet.ToString())));
            temperature *= COOLING_RATE;
        }
#if DEBUG_PRINT
        Debug.Log($"Best score found: {bestScore}\n");
        //ApplyArrows(bestArrows);
        //Player.PrintMap(state.Map);
        Debug.Log($"Evals really done: {evalsDone}\n");
        Debug.Log($"Duplicates: {duplicates}\n");
        Debug.Log($"Successes: {successes}\n");
        Debug.Log($"Accepts of worse solution: {accepts}\n");
        Debug.Log($"Time spent evaluating: {timeSpentEvaluating / TimeSpan.TicksPerMillisecond}ms\n");
        Debug.Log($"Time spent generating arrows: {timeSpentGeneratingArrows / TimeSpan.TicksPerMillisecond}ms\n");
        Debug.Log($"Time spent checking for duplicates: {timecheckingForDuplicates / TimeSpan.TicksPerMillisecond}ms\n");
        //Debug.Log($"Whole function took {stopwatch.ElapsedMilliseconds}ms\n");
#endif
        return bestArrows;
    }

    private bool IsDuplicateArrowSet(List<Triplet> arrows)
    {
        string arrowKey = string.Join(";", arrows.OrderBy(arrow => arrow.GetHashCode()).Select(a => $"{a.X}-{a.Y}-{a.direction}"));
        if (simulatedArrowSets.Contains(arrowKey))
        {
            return true;
        }
        simulatedArrowSets.Add(arrowKey);
        return false;
    }

    private (List<Triplet>, (int, int, CellType)) GenerateNeighbour(List<Triplet> currentArrows)
    {
        var rndIdx = random.Next(possiblePlatformsCount);
        (int y, int x) randomSpot = state.PlatformSpotsList[rndIdx];
        Triplet triplet;
        if (state.Map[randomSpot.x][randomSpot.y] == CellType.Platform) // Here we add random arrow
        {
            var rndArrow = (Direction)random.Next(4);
            triplet = new Triplet { X=randomSpot.y, Y=randomSpot.x, direction=rndArrow };
            var copy = new List<Triplet>(currentArrows)
            {
                triplet
            };
            return (copy, (randomSpot.x, randomSpot.y, Triplet.GetArrowDirection(rndArrow)));
        }
        // We are on arrow, delete it
        var cp = new List<Triplet>(currentArrows);
        cp.RemoveAll(triplet =>  triplet.X == randomSpot.y && triplet.Y == randomSpot.x);
        return (cp, (randomSpot.x, randomSpot.y, CellType.Platform));
    }

    private List<Triplet> GenerateRandomArrows()
    {
        int arrowCount = random.Next(possiblePlatformsCount);
        HashSet<Triplet> placedArrows = new HashSet<Triplet>();
        List<Triplet> triplets = new List<Triplet>();
        HashSet<(int, int)> spotsDone = new HashSet<(int, int)>();
        int x = 0;
        int y = 0;
        for (int i = 0; i < arrowCount; i++)
        {
            do
            {
                x = random.Next(10);
                y = random.Next(19);
            } while (spotsDone.Contains((x, y)));

            if (state.PlatformSpots.Contains((y, x)))
            {
                var cnt = state.PossibleArrowSpots[x, y].Count();
                triplets.Add(state.PossibleArrowSpots[x, y].ElementAt(random.Next(cnt)));
                spotsDone.Add((x, y));
            }
        }
        return triplets;
    }
}   
class Player
{
    static string testFileName = "16-hypersonic-deluxe.txt";
    static string[] testFileNames = new string[]
    {
        //"01-simple.txt",
        //"02-dual-simple.txt",
        //"03-buggy-robot.txt",
        //"04-dual-buggy-robots.txt",
        //"05-3x3-platform.txt",
        //"06-roundabout.txt",
        //"07-dont-fall.txt",
        //"08-portal.txt",
        //"09-codingame.txt",
        "10-multiple-3x3-platforms.txt",
        //"11-9x9-quantic-platform.txt",
        //"12-one-long-road.txt",
        //"13-hard-choice.txt",
        //"14-the-best-way.txt",
        //"15-hypersonic.txt",
        //"16-hypersonic-deluxe.txt",
        //"17-saturn-rings.txt",
        //"18-gl-hf.txt",
        //"19-cross.txt",
        //"20-to-the-right.txt",
        //"21-wings-of-liberty.txt",
        //"22-round-the-clock.txt",
        //"23-cells.txt",
        //"24-shield.txt",
        //"25-starcraft.txt",
        //"26-xel.txt",
        //"27-4-gates.txt",
        //"28-confusion.txt",
        //"29-bunker.txt",
        //"30-split.txt",
    };

    static void Main(string[] args)
    {
#if DEBUG_MODE
        Debug.ClearLogFile();
        foreach (string fileName in testFileNames)
        {
            Console.Error.WriteLine(fileName);
            testFileName = fileName;
            Play(args);
        }
#else
        Play(args);
#endif
    }
    static void Play(string[] args)
    {
        string[] inputLines;
#if DEBUG_MODE
        Stopwatch watch = Stopwatch.StartNew();
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "tests", testFileName);

        if (File.Exists(filePath))
        {
            // Odczytujemy dane wejściowe z pliku
            inputLines = File.ReadAllLines(filePath);
        }
        else
        {
            Console.WriteLine($"Test file {testFileName} not found!");
            return;
        }
#else
        List<string> data = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(Console.ReadLine());
        }
        var str = Console.ReadLine();
        var cnt = int.Parse(str);
        data.Add(str);

        for (int i = 0; i < cnt; i++)
        {
            data.Add(Console.ReadLine());
        }

        inputLines = data.ToArray();
#endif
        CellType[][] map = new CellType[10][];
        for (int i = 0; i < 10; i++)
        {
            map[i] = ParseMapLine(inputLines[i]);
        }
#if DEBUG_PRINT
        //PrintMap(map);
#endif
        int robotCount = int.Parse(inputLines[10]);
        HashSet<Agent> agents = new HashSet<Agent>();
        for (int i = 0; i < robotCount; i++)
        {
            string[] inputs = inputLines[11 + i].Split(' ');
            int x = int.Parse(inputs[0]);
            int y = int.Parse(inputs[1]);
            string direction = inputs[2];
            switch (direction)
            {
                case "U":
                    agents.Add(new Agent(i, Direction.Up, (x, y)));
                    break;
                case "R":
                    agents.Add(new Agent(i, Direction.Right, (x, y)));
                    break;
                case "D":
                    agents.Add(new Agent(i, Direction.Down, (x, y)));
                    break;
                case "L":
                    agents.Add(new Agent(i, Direction.Left, (x, y)));
                    break;
                default:
                    Debug.Log($"Error parsing direction of agent nr {i + 1}\n");
                    break;
            }
#if DEBUG_PRINT
            //Debug.Log(string.Join("\n", agents.Select(agent => agent.ToString()).ToArray()));
#endif
        }
        State state = new State(map, agents);
        //FullRandomSearch search = new FullRandomSearch(state);
        SimulatedAnnealing search = new SimulatedAnnealing(state);
        // Write an action using Console.WriteLine()
        // To debug: Console.Error.WriteLine("Debug messages...");

        Console.WriteLine(string.Join(" ", search.Evaluate().Select(triplet => triplet.ToString())));
#if DEBUG_MODE
        Debug.Log("\n");
        PrintMap(state.Map);
        Debug.Log($"Finished all in {watch.ElapsedMilliseconds}ms\n\n");
#endif
    }
    static CellType[] ParseMapLine(string line)
    {
        List<CellType> mapLine = new List<CellType>();
        foreach (char c in line)
        {
            switch (c)
            {
                case '#':
                    mapLine.Add(CellType.Void);
                    break;
                case '.':
                    mapLine.Add(CellType.Platform);
                    break;
                case 'U':
                    mapLine.Add(CellType.ArrowUp);
                    break;
                case 'R':
                    mapLine.Add(CellType.ArrowRight);
                    break;
                case 'D':
                    mapLine.Add(CellType.ArrowDown);
                    break;
                case 'L':
                    mapLine.Add(CellType.ArrowLeft);
                    break;
                default:
                    Debug.Log($"Error parsing line {line} at character {c}");
                    break;
            }
        }
        return mapLine.ToArray();
    }

    public static void PrintMap(CellType[][] map)
    {
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 19; j++)
            {
                switch (map[i][j])
                {
                    case CellType.Void:
                        Debug.Log("V");
                        break;
                    case CellType.Platform:
                        Debug.Log("P");
                        break;
                    case CellType.ArrowUp:
                        Debug.Log("U");
                        break;
                    case CellType.ArrowLeft:
                        Debug.Log("L");
                        break;
                    case CellType.ArrowDown:
                        Debug.Log("D");
                        break;
                    case CellType.ArrowRight:
                        Debug.Log("R");
                        break;
                }
            }
            Debug.Log("\n");
        }
    }

    public static void PrintMapToFile(CellType[][] map)
    {
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 19; j++)
            {
                switch (map[i][j])
                {
                    case CellType.Void:
                        Debug.LogToFile("V", false);
                        break;
                    case CellType.Platform:
                        Debug.LogToFile("P", false);
                        break;
                    case CellType.ArrowUp:
                        Debug.LogToFile("U", false);
                        break;
                    case CellType.ArrowLeft:
                        Debug.LogToFile("L", false);
                        break;
                    case CellType.ArrowDown:
                        Debug.LogToFile("D", false);
                        break;
                    case CellType.ArrowRight:
                        Debug.LogToFile("R", false);
                        break;
                }
            }
            Debug.LogToFile("\n", false);
        }
    }
}