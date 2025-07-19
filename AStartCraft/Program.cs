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
using SimulatedAnnealing;



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

    public static void LogToFile(string msg, bool addNewLine = true)
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

namespace SimulatedAnnealing
{
    public class Agent
    {
        public int Id;
        public Direction DirectionFacing;
        public bool IsAlive;
        public int score;
        public bool[,] visited;
        public int X;
        public int Y;
        private int startX;
        private int startY;
        private Direction originalDirection;

        struct AgentSave
        {
            public Direction DirectionFacing;
            public int score;
            public bool[,] visited;
            public int X;
            public int Y;
        }

        private AgentSave agentSave;

        public Agent(int id, Direction directionFacing, (int x, int y) currentPos)
        {
            Id = id;
            DirectionFacing = directionFacing;
            score = 0;
            visited = new bool[190, 4];
            IsAlive = true;
            X = currentPos.x;
            Y = currentPos.y;
            startX = currentPos.x;
            startY = currentPos.y;
            originalDirection = directionFacing;
            agentSave = new AgentSave
            {
                DirectionFacing = DirectionFacing,
                score = score,
                visited = visited,
                X = X,
                Y = Y
            };
        }

        public override string ToString()
        {
            string isAlive = IsAlive ? "is alive" : "is dead";
            return $"Agent {Id} facing {DirectionFacing} located at ({X}, {Y}) and {isAlive}.";
        }

        public void SaveAgentData()
        {
            agentSave.DirectionFacing = DirectionFacing;
            agentSave.score = score;
            agentSave.visited = visited;
            agentSave.X = X;
            agentSave.Y = Y;

        }

        public void LoadSavedData()
        {
            DirectionFacing = agentSave.DirectionFacing;
            score = agentSave.score;
            visited = agentSave.visited;
            X = agentSave.X;
            Y = agentSave.Y;
        }

        public void KillAgent()
        {
            IsAlive = false;
        }

        public bool IsInLoop()
        {
            return visited[X * 10 + Y, (int)DirectionFacing];
        }

        public void ResetAgent()
        {
            X = startX;
            Y = startY;
            DirectionFacing = originalDirection;
            IsAlive = true;
            score = 0;
            visited = new bool[190, 4];
        }

        private void SetVisited()
        {
            visited[X * 10 + Y, (int)DirectionFacing] = true;
        }

        public void MoveAgent()
        {
            if (!IsAlive) return;
            score++;
            SetVisited();

            switch (DirectionFacing)
            {
                case Direction.Up:
                    Y = (Y - 1 + 10) % 10;
                    break;
                case Direction.Down:
                    Y = (Y + 1) % 10;
                    break;
                case Direction.Left:
                    X = (X - 1 + 19) % 19;
                    break;
                case Direction.Right:
                    X = (X + 1) % 19;
                    break;
            }
        }

        public void UpdateDirection(Direction direction)
        {
            DirectionFacing = direction;
        }

        public bool HaveVisited(int row, int col)
        {
            int cell = col * 10 + row;
            return visited[cell, 0] || visited[cell, 1] || visited[cell, 2] || visited[cell, 3];
        }

    }
    public class State
    {
        private static Random rng = new Random();
        private const int TOTAL_CELLS = 190;

        public ulong[] grid = new ulong[10];
        public ulong[] predefinedGrid = new ulong[10];
        public Agent[] agents;
        public CellType[][] validArrows = new CellType[TOTAL_CELLS][];
        public int[] platformsCords;
        public bool[] isCorner = new bool[TOTAL_CELLS];

        public State(Agent[] agents, int[] platformsCords)
        {
            this.agents = agents;
            this.platformsCords = platformsCords;
        }

        public State(Agent[] agents, CellType[][] initialGrid)
        {
            this.agents = agents;
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 19; col++)
                {
                    CellType cellState = initialGrid[row][col];
                    SetCellState(row, col, cellState, isPredefined: true);
                }
            }
            List<int> validPlatforms = new List<int>();
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 19; col++)
                {
                    CellType cellState = GetCellState(row, col);
                    List<CellType> validChoices = new List<CellType>();
                    if (cellState == CellType.Platform && !IsCorridor(row, col))
                    {
                        CellType upCell = GetUpCell(row, col);
                        CellType downCell = GetDownCell(row, col);
                        CellType leftCell = GetLeftCell(row, col);
                        CellType rightCell = GetRightCell(row, col);
                        if (IsCorner(upCell, downCell, leftCell, rightCell))
                        {
                            isCorner[col * 10 + row] = true;
                            if (upCell != CellType.Void && upCell != CellType.ArrowDown)
                                validChoices.Add(CellType.ArrowUp);
                            if (downCell != CellType.Void && downCell != CellType.ArrowUp)
                                validChoices.Add(CellType.ArrowDown);
                            if (leftCell != CellType.Void && leftCell != CellType.ArrowRight)
                                validChoices.Add(CellType.ArrowLeft);
                            if (rightCell != CellType.Void && rightCell != CellType.ArrowLeft)
                                validChoices.Add(CellType.ArrowRight);
                        }
                        else
                        {
                            if (upCell != CellType.Void && upCell != CellType.ArrowDown)
                                validChoices.Add(CellType.ArrowUp);
                            if (downCell != CellType.Void && downCell != CellType.ArrowUp)
                                validChoices.Add(CellType.ArrowDown);
                            if (leftCell != CellType.Void && leftCell != CellType.ArrowRight)
                                validChoices.Add(CellType.ArrowLeft);
                            if (rightCell != CellType.Void && rightCell != CellType.ArrowLeft)
                                validChoices.Add(CellType.ArrowRight);
                            validChoices.Add(CellType.Platform);
                            if (upCell != CellType.Void && downCell != CellType.Void && leftCell != CellType.Void && rightCell != CellType.Void)
                            {
                                validChoices.Add(CellType.Platform);
                                validChoices.Add(CellType.Platform);
                            }
                        }
                        validPlatforms.Add(col * 10 + row);
                    }
                    validArrows[col * 10 + row] = validChoices.ToArray();
                }
            }
            platformsCords = validPlatforms.ToArray();
        }

        public bool IsCorner(CellType upCell, CellType downCell, CellType leftCell, CellType rightCell)
        {
            return (upCell == CellType.Void && leftCell == CellType.Void) ||
                (downCell == CellType.Void && rightCell == CellType.Void) ||
                (upCell == CellType.Void && rightCell == CellType.Void) ||
                (downCell == CellType.Void && leftCell == CellType.Void);
        }

        public CellType GetLeftCell(int row, int col)
        {
            return GetCellState(row, (col + 18) % 19);
        }

        public CellType GetRightCell(int row, int col)
        {
            return GetCellState(row, (col + 1) % 19);
        }

        public CellType GetUpCell(int row, int col)
        {
            return GetCellState((row + 9) % 10, col);
        }

        public CellType GetDownCell(int row, int col)
        {
            return GetCellState((row + 1) % 10, col);
        }

        public CellType GetUpLeftCell(int row, int col)
        {
            return GetCellState((row + 11) % 10, (col + 18) % 19);
        }

        public CellType GetUpRightCell(int row, int col)
        {
            return GetCellState((row + 11) % 10, (col + 1) % 19);
        }

        public CellType GetDownLeftCell(int row, int col)
        {
            return GetCellState((row + 1) % 10, (col + 18) % 19);
        }

        public CellType GetDownRightCell(int row, int col)
        {
            return GetCellState((row + 1) % 10, (col + 1) % 19);
        }

        public bool IsCenter(int row, int col)
        {
            CellType upCell = GetUpCell(row, col);
            CellType downCell = GetDownCell(row, col);
            CellType leftCell = GetLeftCell(row, col);
            CellType rightCell = GetRightCell(row, col);
            CellType upLeftCell = GetUpLeftCell(row, col);
            CellType upRightCell = GetUpRightCell(row, col);
            CellType downLeftCell = GetDownLeftCell(row, col);
            CellType downRightCell = GetDownRightCell(row, col);

            bool isSurroundedByPlatform = (
                upCell == CellType.Platform &&
                downCell == CellType.Platform &&
                leftCell == CellType.Platform &&
                rightCell == CellType.Platform &&
                upLeftCell == CellType.Platform &&
                upRightCell == CellType.Platform &&
                downLeftCell == CellType.Platform &&
                downRightCell == CellType.Platform
            );

            return isSurroundedByPlatform;
        }
        public bool IsCorridor(int row, int col)
        {

            CellType upCell = GetUpCell(row, col);
            CellType downCell = GetDownCell(row, col);
            CellType leftCell = GetLeftCell(row, col);
            CellType rightCell = GetRightCell(row, col);

            bool isVerticalCorridor = (upCell == CellType.Void && downCell == CellType.Void) &&
                              (leftCell == CellType.Platform && rightCell == CellType.Platform);

            bool isHorizontalCorridor = (leftCell == CellType.Void && rightCell == CellType.Void) &&
                                        (upCell == CellType.Platform && downCell == CellType.Platform);

            return isVerticalCorridor || isHorizontalCorridor;
        }
        public CellType GetCellState(int row, int col)
        {
            // Extract 3 bits from the row corresponding to the column (col)
            return (CellType)((grid[row] >> (col * 3)) & 0b111); // 3 bits
        }

        public CellType GetPredefinedCellState(int row, int col)
        {
            // Extract 3 bits from the row corresponding to the column (col)
            return (CellType)((predefinedGrid[row] >> (col * 3)) & 0b111); // 3 bits
        }

        public void SetCellState(int row, int col, CellType cellType, bool isPredefined = false)
        {
            // Clear the 3 bits for this column in the row
            grid[row] &= ~(0b111UL << (col * 3)); // Clear 3 bits
            grid[row] |= ((ulong)cellType << (col * 3)); // Set new value
            if (isPredefined)
            {
                // Also set the state in predefinedGrid if it's a predefined arrow
                predefinedGrid[row] &= ~(0b111UL << (col * 3)); // Clear 3 bits
                predefinedGrid[row] |= ((ulong)cellType << (col * 3)); // Set new value
            }
        }

        public bool IsPredefinedArrow(int row, int col)
        {
            var cell = (CellType)((predefinedGrid[row] >> (col * 3)) & 0b111);
            return cell == CellType.ArrowUp || cell == CellType.ArrowDown || cell == CellType.ArrowLeft || cell == CellType.ArrowRight;
        }

        public int Evaluate((int row, int col) cellChanged)
        {
            int score = 0;
            foreach (Agent agent in agents)
            {
                agent.SaveAgentData();
                if (agent.HaveVisited(cellChanged.row, cellChanged.col))
                {
                    agent.ResetAgent();
                    CellType cellAgentStandsOn = GetCellState(agent.Y, agent.X);
                    if ((int)cellAgentStandsOn > 1)
                    {
                        agent.UpdateDirection(Utils.GetDirectionBasedOnCellType(cellAgentStandsOn));
                    }
                }
            }

            bool finished = false;
            while (!finished)
            {
                finished = true;
                foreach (Agent agent in agents)
                {
                    if (agent.IsAlive)
                    {
                        finished = false;
                        agent.MoveAgent();
                        CellType cellAgentStandsOn = GetCellState(agent.Y, agent.X);
                        if ((int)cellAgentStandsOn > 1)
                        {
                            agent.UpdateDirection(Utils.GetDirectionBasedOnCellType(cellAgentStandsOn));
                        }
                        if (cellAgentStandsOn == CellType.Void || agent.IsInLoop())
                        {
                            agent.KillAgent();
                        }
                    }
                }
            }

            foreach (Agent agent in agents)
            {
                score += agent.score;
            }
            return score;
        }

        public void LoadAgents()
        {
            foreach (Agent agent in agents)
            {
                agent.LoadSavedData();
            }
        }

        public void UpdateAgentDirection(Agent agent)
        {
            CellType cellAgentStandsOn = GetCellState(agent.Y, agent.X);
            if ((int)cellAgentStandsOn > 1)
            {
                agent.UpdateDirection(Utils.GetDirectionBasedOnCellType(cellAgentStandsOn));
            }
        }

        public void MakeRandomState()
        {
            int amountOfArrows = rng.Next(platformsCords.Length);
            for (int i = 0; i < amountOfArrows; i++)
            {
                int randomPlatform = platformsCords[rng.Next(platformsCords.Length)];
                int row = randomPlatform % 10;
                int col = randomPlatform / 10;

                int possibilites = validArrows[randomPlatform].Length;
                CellType newCellType = validArrows[randomPlatform][rng.Next(possibilites)];
                SetCellState(row, col, newCellType);

            }
        }

        public (int row, int col, CellType oldCellType) MakeRandomNeighbour()
        {
            int randomPlatform;
            do
            {
                randomPlatform = platformsCords[rng.Next(platformsCords.Length)];
            } while (!IsValidPlatform(randomPlatform));
            int row = randomPlatform % 10;
            int col = randomPlatform / 10;

            CellType currentType = GetCellState(row, col);
            int randomDirectionIndex = rng.Next(validArrows[randomPlatform].Length);
            CellType newCellType = validArrows[randomPlatform][randomDirectionIndex];
            SetCellState(row, col, newCellType);
            return (row, col, currentType);

        }

        private bool IsValidPlatform(int index)
        {
            return validArrows[index].Contains(CellType.ArrowUp) || 
                   validArrows[index].Contains(CellType.ArrowLeft) ||
                   validArrows[index].Contains(CellType.ArrowDown) ||
                   validArrows[index].Contains(CellType.ArrowRight);
        }

        public void PrintGrid()
        {
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 19; col++)
                {
                    CellType cellState = GetCellState(row, col);
                    switch (cellState)
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

        public State Clone()
        {
            State newState = new State((Agent[])this.agents.Clone(), (int[])this.platformsCords.Clone());
            Array.Copy(this.grid, newState.grid, this.grid.Length);
            Array.Copy(this.predefinedGrid, newState.predefinedGrid, this.predefinedGrid.Length);
            newState.validArrows = this.validArrows;
            return newState;
        }

        public ulong ComputeStateHash()
        {
            ulong hash = 14695981039346656037UL; // FNV offset basis for 64-bit
            foreach (ulong row in grid)
            {
                // FNV-1a hash computation (a fast non-cryptographic hash function)
                hash ^= row;
                hash *= 1099511628211UL; // FNV prime for 64-bit
            }
            return hash;
        }

        public List<(int row, int col, CellType arrow)> GetDifferences()
        {
            List<(int row, int col, CellType arrow)> changes = new List<(int row, int col, CellType arrow)>();
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 19; col++)
                {
                    CellType predefinedCellState = GetPredefinedCellState(row, col);
                    CellType changedCellState = GetCellState(row, col);

                    if (predefinedCellState != changedCellState)
                    {
                        changes.Add((row, col, changedCellState));
                    }
                }
            }

            return changes;
        }

    }

    public class Utils
    {
        public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();

        public static string CellTypeToString(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.ArrowUp:
                    return "U";
                case CellType.ArrowLeft:
                    return "L";
                case CellType.ArrowDown:
                    return "D";
                case CellType.ArrowRight:
                    return "R";
                default:
                    return string.Empty;
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
                _ => throw new NotImplementedException(),
            };
        }

        public static Direction GetDirectionBasedOnCellType(CellType direction)
        {
            return direction switch
            {
                CellType.ArrowLeft => Direction.Left,
                CellType.ArrowRight => Direction.Right,
                CellType.ArrowUp => Direction.Up,
                CellType.ArrowDown => Direction.Down,
                _ => throw new NotImplementedException(),
            };
        }
    }

    public class SimulatedAnnealing
    {
        public State state;

        private float TEMP_START = 10f;
        private float TEMP_END = 0.01f;
        private float tempRatio;
        private int N = 200;
        private long TIME_LIMIT;
        private Random rnd = new Random();
        private long duplicateCacheLimit = 1_000_000;

        private HashSet<ulong> visitedStates = new HashSet<ulong>();

        public SimulatedAnnealing(State state, long timeLimit = 970)
        {
            Utils.watch.Restart();
            this.state = state;
            TIME_LIMIT = timeLimit;
            tempRatio = (TEMP_END / TEMP_START);
        }

        public (State, int) FindBest()
        {
#if DEBUG_MODE
            int evalsDone = 0;
            int successes = 0;
            int accepts = 0;
            int duplicates = 0;
            long startTime = Utils.watch.ElapsedMilliseconds;
            System.Diagnostics.Stopwatch evalWatch = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch neighbourWatch = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch duplicatesWatch = new System.Diagnostics.Stopwatch();
#endif
            State currentState = state.Clone();
            currentState.MakeRandomState();
            int currentScore = currentState.Evaluate((0, 0));
#if DEBUG_MODE
            evalWatch.Start();
#endif
            int bestScore = state.Evaluate((0, 0));
#if DEBUG_MODE
            evalWatch.Stop();
#endif
            State bestState = state.Clone();

            visitedStates.Add(state.ComputeStateHash());

            float temperature = TEMP_START;
            while (1 == 1)
            {
                if (Utils.watch.ElapsedMilliseconds > TIME_LIMIT)
                    break;
                for (int i = 0; i < N; i++)
                {
#if DEBUG_MODE
                    neighbourWatch.Start();
#endif
                    (int row, int col, CellType previousCellType) = currentState.MakeRandomNeighbour();
#if DEBUG_MODE
                    neighbourWatch.Stop();
                    duplicatesWatch.Start();
#endif
                    ulong stateHash = currentState.ComputeStateHash();
                    if (visitedStates.Contains(stateHash))
                    {
                        //currentState.SetCellState(row, col, previousCellType);
#if DEBUG_MODE
                        duplicates++;
                        if (duplicates > duplicateCacheLimit)
                        {
                            visitedStates.Clear();
                            duplicates = 0;
                        }
#endif
                        continue;
                    }
                        
                    visitedStates.Add(stateHash);
#if DEBUG_MODE
                    duplicatesWatch.Stop();
                    evalWatch.Start();
#endif
                    int candidateScore = currentState.Evaluate((row, col));
#if DEBUG_MODE
                    evalWatch.Stop();
#endif
                    double probChance = Math.Exp((double)(candidateScore - currentScore) / (double)temperature);
#if DEBUG_MODE
                    evalsDone++;
#endif

                    if (candidateScore > currentScore || rnd.NextDouble() < probChance)
                    {
#if DEBUG_MODE
                        accepts++;
#endif
                        currentScore = candidateScore;
                        if (candidateScore > bestScore)
                        {
#if DEBUG_MODE
                            successes++;
#endif
                            bestState = currentState.Clone();
                            bestScore = candidateScore;
                        }
                    }
                    else
                    {
                        currentState.SetCellState(row, col, previousCellType);
                        currentState.LoadAgents();
                    }
                }
                long timeFrac = Utils.watch.ElapsedMilliseconds / TIME_LIMIT;
                temperature = TEMP_START * (float)Math.Pow(tempRatio, timeFrac); // TODO check if bottleneck
                currentState = bestState.Clone();
            }
#if DEBUG_MODE
            Debug.Log($"Best score: {bestScore}\n");
            Debug.Log($"Evals done: {evalsDone}, took {evalWatch.ElapsedMilliseconds}ms\n");
            Debug.Log($"Time for neigh generation: {neighbourWatch.ElapsedMilliseconds} ms\n");
            Debug.Log($"Time for duplicate check: {duplicatesWatch.ElapsedMilliseconds} ms\n");
            Debug.Log($"successes: {successes}\n");
            Debug.Log($"accepts: {accepts}\n");
            Debug.Log($"duplicates: {duplicates}\n");
            Debug.Log($"Time passed {Utils.watch.ElapsedMilliseconds - startTime}ms\n");
#endif
            return (bestState, bestScore);
        }
    }
}



class Player
{
#if DEBUG_MODE
    public static int combinedScore = 0;
#endif
    public const long NOGC_SIZE = 67_108_864; // 280_000_000;
#if DEBUG_MODE
    static string testFileName = "16-hypersonic-deluxe.txt";
    static string[] testFileNames = new string[]
    {
        "01-simple.txt", // best 23
        "02-dual-simple.txt", // best 46
        "03-buggy-robot.txt", // best 22
        "04-dual-buggy-robots.txt", // best 44
        "05-3x3-platform.txt", // best 11
        "06-roundabout.txt", // best 80
        "07-dont-fall.txt", // best 55
        "08-portal.txt", // best 28
        "09-codingame.txt", // best 80
        "10-multiple-3x3-platforms.txt", // best 56
        "11-9x9-quantic-platform.txt", // best 81
        "12-one-long-road.txt", // best 59
        "13-hard-choice.txt", // best 192
        "14-the-best-way.txt", // best 86
        "15-hypersonic.txt", // best 248
        "16-hypersonic-deluxe.txt", // best 462
        "17-saturn-rings.txt", // best 392
        "18-gl-hf.txt", // best 198
        "19-cross.txt", // best 365
        "20-to-the-right.txt", // best 680
        "21-wings-of-liberty.txt", // best 495
        "22-round-the-clock.txt", // best 453
        "23-cells.txt", // best 174
        "24-shield.txt", // best 222
        "25-starcraft.txt", // best 564
        "26-xel.txt", // best 698
        "27-4-gates.txt", // best 275
        "28-confusion.txt", // best 172
        "29-bunker.txt", // best 504
        "30-split.txt", // best 540
    };
#endif
    static void Main(string[] args)
    {
#if DEBUG_MODE
        Debug.ClearLogFile();
        foreach (string fileName in testFileNames)
        {
            Console.Error.WriteLine(fileName);
            testFileName = fileName;
            Play(args);
            Utils.globalWatch.Reset();
        }
        Debug.Log($"Combined score: {combinedScore}\n");
#else
        Play(args);
#endif
    }
    static void Play(string[] args)
    {
        string[] inputLines;
#if DEBUG_MODE
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
        Utils.globalWatch.Start();
        CellType[][] map = new CellType[10][];
        for (int i = 0; i < 10; i++)
        {
            map[i] = ParseMapLine(inputLines[i]);
        }

        int robotCount = int.Parse(inputLines[10]);
        Agent[] agents = new Agent[robotCount];
        for (int i = 0; i < robotCount; i++)
        {
            string[] inputs = inputLines[11 + i].Split(' ');
            int x = int.Parse(inputs[0]);
            int y = int.Parse(inputs[1]);
            string direction = inputs[2];
            switch (direction)
            {
                case "U":
                    agents[i] = new Agent(i, Direction.Up, (x, y));
                    break;
                case "R":
                    agents[i] = new Agent(i, Direction.Right, (x, y));
                    break;
                case "D":
                    agents[i] = new Agent(i, Direction.Down, (x, y));
                    break;
                case "L":
                    agents[i] = new Agent(i, Direction.Left, (x, y));
                    break;
                default:
                    Debug.Log($"Error parsing direction of agent nr {i + 1}\n");
                    break;
            }
        }

        State state = new State(agents, map);
        SimulatedAnnealing.SimulatedAnnealing search = new SimulatedAnnealing.SimulatedAnnealing(state, 500);

#if !DEBUG_MODE
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        GC.TryStartNoGCRegion(NOGC_SIZE); // true
#endif
        (State bestState, int score) = search.FindBest();
        search = new SimulatedAnnealing.SimulatedAnnealing(bestState, 350);
        (bestState, score) = search.FindBest();
        search = new SimulatedAnnealing.SimulatedAnnealing(bestState, 130);
        (State newBestState, int newScore) = search.FindBest();
        if (score > newScore)
        {
#if DEBUG_MODE
            combinedScore += score;
#endif
            Console.WriteLine(string.Join(" ", bestState.GetDifferences().Select(change => $"{change.col} {change.row} {Utils.CellTypeToString(change.arrow)}")));
        }
        else
        {
#if DEBUG_MODE
            combinedScore += newScore;
#endif
            Console.WriteLine(string.Join(" ", newBestState.GetDifferences().Select(change => $"{change.col} {change.row} {Utils.CellTypeToString(change.arrow)}")));
        }

#if DEBUG_MODE
        //bestState.PrintGrid();
        Debug.Log("\n");
        Debug.Log($"Finished all in {Utils.globalWatch.ElapsedMilliseconds}ms\n\n");
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
}