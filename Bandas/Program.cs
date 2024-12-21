//#define DEBUG_MODE
//#define DEBUG_PRINT
//#define TEST_MODE

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.Marshalling;
using System.Globalization;
using System.Numerics;

public class Debug
{
    public static void Log(string msg)
    {
        Console.Error.Write(msg);
    }
}

public static class Variables
{
    public static int MY_ID { get; set; }
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
    Player0,
    Player1,
    Empty,
    Hole,
}
/**
 * Try to survive by not falling off
 **/


public class Utils
{
    public static int RESPONSE_TIME = 100;
    public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();

    public static string DirectionToString(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up:
                return "UP";
            case Direction.Left:
                return "LEFT";
            case Direction.Down:
                return "DOWN";
            case Direction.Right:
                return "RIGHT";
            default:
                return string.Empty;
        }
    }
}

struct Board
{
    public char[,] _board;
    private int height, width;
    public int ZeroPawns, OnePawns;

    public Board(int height, int width)
    {
        _board = new char[height, width];
        this.height = height;
        this.width = width;
        ZeroPawns = 0;
        OnePawns = 0;
    }

    public void ResetBoard(char[,] board)
    {
        Array.Copy(board, _board, _board.Length);
        ZeroPawns = 0;
        OnePawns = 0;
    }

    public Board Clone()
    {
        Board copy = new Board(height, width);
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                copy._board[i, j] = _board[i, j];
            }
        }
        return copy;
    }

    public void Initialize(string[] input)
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                _board[i, j] = input[i][j];
            }
        }
    }

    public void MoveLeft(char myPawn, char enemyPawn)
    {
        for (int i = 0; i < height; i++)
        {
            char save = '-';
            int currIdx = width - 1;
            bool movingLeft = false;
            while (currIdx >= 0)
            {
                if (movingLeft)
                {
                    var cell = _board[i, currIdx];
                    if (cell == '-')
                    {
                        _board[i, currIdx] = save;
                        movingLeft = false;
                    }
                    else if (cell == myPawn || cell == enemyPawn)
                    {
                        _board[i, currIdx] = save;
                        save = cell;
                    }
                    else if (cell == 'x')
                    {
                        movingLeft = false;
                        save = '-';
                    }
                }
                else // not moving left
                {
                    var cell = _board[i, currIdx];
                    if (cell == myPawn)
                    {
                        movingLeft = true;
                        save = cell;
                        _board[i, currIdx] = '-';
                    }
                    else
                    {
                        //...
                    }
                }
                currIdx--;
            }
        }
    }

    public void MoveRight(char myPawn, char enemyPawn)
    {
        for (int i = 0; i < height; i++)
        {
            char save = '-';
            int currIdx = 0;
            bool movingRight = false;
            while (currIdx < width)
            {
                if (movingRight)
                {
                    var cell = _board[i, currIdx];
                    if (cell == '-')
                    {
                        _board[i, currIdx] = save;
                        movingRight = false;
                    }
                    else if (cell == myPawn || cell == enemyPawn)
                    {
                        _board[i, currIdx] = save;
                        save = cell;
                    }
                    else if (cell == 'x')
                    {
                        movingRight = false;
                        save = '-';
                    }
                }
                else // not moving left
                {
                    var cell = _board[i, currIdx];
                    if (cell == myPawn)
                    {
                        movingRight = true;
                        save = cell;
                        _board[i, currIdx] = '-';
                    }
                    else
                    {
                        //...
                    }
                }
                currIdx++;
            }
        }
    }

    public void MoveUp(char myPawn, char enemyPawn)
    {
        for (int j = 0; j < width; j++)
        {
            char save = '-';
            int currIdx = height - 1;
            bool movingUp = false;
            while (currIdx >= 0)
            {
                if (movingUp)
                {
                    var cell = _board[currIdx, j];
                    if (cell == '-')
                    {
                        _board[currIdx, j] = save;
                        movingUp = false;
                    }
                    else if (cell == myPawn || cell == enemyPawn)
                    {
                        _board[currIdx, j] = save;
                        save = cell;
                    }
                    else if (cell == 'x')
                    {
                        movingUp = false;
                        save = '-';
                    }
                }
                else // not moving up
                {
                    var cell = _board[currIdx, j];
                    if (cell == myPawn)
                    {
                        movingUp = true;
                        save = cell;
                        _board[currIdx, j] = '-';
                    }
                    else
                    {
                        //...
                    }   
                }
                currIdx--;
            }
        }
    }

    public void MoveDown(char myPawn, char enemyPawn)
    {
        for (int j = 0; j < width; j++)
        {
            char save = '-';
            int currIdx = 0;
            bool movingDown = false;
            while (currIdx < height)
            {
                if (movingDown)
                {
                    var cell = _board[currIdx, j];
                    if (cell == '-')
                    {
                        _board[currIdx, j] = save;
                        movingDown = false;
                    }
                    else if (cell == myPawn || cell == enemyPawn)
                    {
                        _board[currIdx, j] = save;
                        save = cell;
                    }
                    else if (cell == 'x')
                    {
                        movingDown = false;
                        save = '-';
                    }
                }
                else // not moving down
                {
                    var cell = _board[currIdx, j];
                    if (cell == myPawn)
                    {
                        movingDown = true;
                        save = cell;
                        _board[currIdx, j] = '-';
                    }
                    else
                    {
                        //...
                    }
                }
                currIdx++;
            }
        }
    }

    public void RemoveEmptyColumnsAndRows()
    {
        for (int row = 0; row < height; row++)
        {
            if (IsRowEmpty(row))
            {
                for (int j = 0; j < width; j++)
                    _board[row, j] = 'x';
            }
        }

        for (int col = 0; col < width; col++)
        {
            if (IsColumnEmpty(col))
            {
                for (int j = 0; j < height; j++)
                    _board[j, col] = 'x';
            }
        }
    }

    public bool IsRowEmpty(int row)
    {
        for (int j = 0; j < width; j++)
            if (_board[row, j] == '0' || _board[row, j] == '1')
                return false;
        return true;
    }
    public bool IsColumnEmpty(int col)
    {
        for (int j = 0; j < width; j++)
            if (_board[j, col] == '0' || _board[j, col] == '1')
                return false;
        return true;
    }

    public int IsOver()
    {
        int zeroPawns = 0;
        int onePawns = 0;

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                if (_board[i, j] == '0') zeroPawns++;
                if (_board[i, j] == '1') onePawns++;
            }
        }
        ZeroPawns = zeroPawns;
        OnePawns = onePawns;
        if (zeroPawns == 0 && onePawns > 0) return 1;
        if (zeroPawns > 0 && onePawns == 0) return 2;
        if (zeroPawns == 0 && zeroPawns == 0) return 3;
        return 0;
    }
    public void Print()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Console.Write(_board[i, j] + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}

class FlatMC
{
    private Board currentState;
    private int[] N;
    private int[] W;
    private Random rnd;
    private char myPawn, enemyPawn;
    public FlatMC(int myId, int enemyId)
    {
        N = new int[4];
        W = new int[4];
        rnd = new Random();
        myPawn = myId == 0 ? '0' : '1';
        enemyPawn = myId == 1 ? '0' : '1';
    }

    public void Reset(Board state)
    {
        currentState = state;
        N = new int[4];
        W = new int[4];
    }

    public Direction Evaluate()
    {
        Utils.globalWatch.Restart();
        char[,] save = new char[currentState._board.GetLength(0), currentState._board.GetLength(1)];
        Array.Copy(currentState._board, save, currentState._board.Length);

        Debug.Log($"Current global watch: {Utils.globalWatch.ElapsedMilliseconds}\n");
        while (Utils.globalWatch.ElapsedMilliseconds < Utils.RESPONSE_TIME - 10)
        {
            var action = (Direction)rnd.Next(4);
            var result = Simulate(action);
            N[(int)action] += 1;
            if (result)
            {
                W[(int)action] += 1;
            }
            currentState.ResetBoard(save);
        }
        var maxValue = 0f;
        var bestIdx = 0;
        for (int i = 0; i < 4; i++)
        {
            Debug.Log($"For action {(Direction)i} we have N: {N[i]}, W: {W[i]} \n");
            if (N[i] == 0) continue;
            
            var tempValue = (float)W[i] / (float)N[i];
            if (tempValue > maxValue)
            {
                maxValue = tempValue;
                bestIdx = i;
            }
        }
        Debug.Log($"Current global watch at the end: {Utils.globalWatch.ElapsedMilliseconds}\n");
        return (Direction)bestIdx;
    }

    public bool Simulate(Direction action)
    {
        //currentState.Print();
        switch (action)
        {
            case Direction.Left:
                currentState.MoveLeft(myPawn, enemyPawn);
                break;
            case Direction.Up:
                currentState.MoveUp(myPawn, enemyPawn);
                break;
            case Direction.Down:
                currentState.MoveDown(myPawn, enemyPawn);
                break;
            case Direction.Right:
                currentState.MoveRight(myPawn, enemyPawn);
                break;
        }
        currentState.RemoveEmptyColumnsAndRows();
        //currentState.Print();
        //Debug.Log($"{action}\n\n");
        int result;
        var myTurn = false;
        while ((result = currentState.IsOver()) == 0)
        {
            var randomAction = (Direction)rnd.Next(4);
            var movingPawn = myTurn ? myPawn : enemyPawn;
            var notMovingPawn = !myTurn ? myPawn : enemyPawn;
            //Debug.Log($"movingPawn: {movingPawn}, notMovingPawn: {notMovingPawn}\n");
            switch (randomAction)
            {
                case Direction.Left:
                    currentState.MoveLeft(movingPawn, notMovingPawn);
                    break;
                case Direction.Up:
                    currentState.MoveUp(movingPawn, notMovingPawn);
                    break;
                case Direction.Down:
                    currentState.MoveDown(movingPawn, notMovingPawn);
                    break;
                case Direction.Right:
                    currentState.MoveRight(movingPawn, notMovingPawn);
                    break;
            }
            currentState.RemoveEmptyColumnsAndRows();
            //currentState.Print();
            //Debug.Log($"{randomAction}\n\n");
            myTurn = !myTurn;
        }

        if (myPawn == '0' && result == 2) return true;
        if (myPawn == '1' && result == 1) return true;
        return false;
    }

}

class FullMCTS
{
    const uint MAX_ACTION = 4;
    const uint MAX_TREE_SIZE = 1 << 10;
    const uint MAX_GAME_LENGTH = 1 << 7;
    struct Node
    {
        uint wins;
        uint visits;
        uint[] childrenId;
        public Node()
        {
            childrenId = new uint[MAX_ACTION];
            wins = 0;
            visits = 0;
        }
    }

    struct MCTSTree
    {
        uint rootId = 0;
        Node[] tree;
        uint size;

        uint[] selectedStates;
        uint selectedStatesNum;

        public MCTSTree()
        {
            rootId = 0;
            tree = new Node[MAX_TREE_SIZE];
            size = 0;
            selectedStates = new uint[MAX_GAME_LENGTH];
            selectedStatesNum = 0;
        }
    }


}

class Player
{
    public const long NOGC_SIZE = 67_108_864;
    string _test_file = "test.txt";
    static void Main(string[] args)
    {
#if DEBUG_MODE
        int myId = 0;
        int height = 8;
        int width = 8;
#elif TEST_MODE
        RunTests();
        return;
#else
        int myId = int.Parse(Console.ReadLine());
        int height = int.Parse(Console.ReadLine());
        int width = int.Parse(Console.ReadLine());
#endif
        int enemy_id = 1 - myId;
        Variables.MY_ID = myId;
        Debug.Log($"My id: {myId}, enemy: {enemy_id}\n");
        Debug.Log($"{height}x{width}\n");
        var flatmc = new FlatMC(myId, enemy_id);

        GC.TryStartNoGCRegion(NOGC_SIZE);

        Board board = new Board(height, width);
#if !DEBUG_MODE
        while (true)
        {
#endif
            List<string> lines = new List<string>();

#if DEBUG_MODE
            string[] fileLines = File.ReadAllLines("test.txt");
            int lineIndex = 0;

            lineIndex = 3;

            for (int i = 0; i < height; i++)
            {
                lines.Add(fileLines[lineIndex++].Replace(" ", string.Empty));
            }
#else
            // Normalnie wczytujemy dane z konsoli
            for (int i = 0; i < height; i++)
            {
                lines.Add(Console.ReadLine().Replace(" ", string.Empty));
            }
#endif

            board.Initialize(lines.ToArray());
        //for (int k = 0; k < 8; k++)
        //{
        //    board.Print();
        //    Debug.Log($"{board.IsOver()}\n");
        //    Debug.Log($"{board.ZeroPawns}, {board.OnePawns}\n");
        //    board.MoveDown('1', '0');
        //    board.RemoveEmptyColumnsAndRows();
        //    board.Print();
        //    Debug.Log($"{board.IsOver()}\n");
        //    Debug.Log($"{board.ZeroPawns}, {board.OnePawns}\n\n");

        //}

        //board.Print();
        flatmc.Reset(board);
        Console.WriteLine(Utils.DirectionToString(flatmc.Evaluate())); // UP | RIGHT | DOWN | LEFT
#if !DEBUG_MODE
        }
#endif
    }

#if TEST_MODE
    static void RunTests()
    {
        const int numTests = 10;

        for (int testIndex = 1; testIndex <= numTests; testIndex++)
        {
            string fileName = $"test_{testIndex}.txt";
            Console.WriteLine($"Running test: {fileName}");

            if (!File.Exists(fileName))
            {
                Console.WriteLine($"File not found: {fileName}");
                continue;
            }

            string[] lines = File.ReadAllLines(fileName);

            // Wczytaj ID gracza, rozmiar planszy i pierwszy stan
            int myId = int.Parse(lines[0]);
            int height = int.Parse(lines[1]);
            int width = int.Parse(lines[2]);
            Variables.MY_ID = myId;

            string[] initialBoard = new string[height];
            for (int i = 0; i < height; i++)
            {
                initialBoard[i] = lines[3 + i].Replace(" ", string.Empty);
            }

            // Wczytaj oczekiwaną planszę docelową
            int moveLine = 3 + height + 1; // linia z metodą
            string moveMethod = lines[moveLine].Trim();

            string[] expectedBoard = new string[height];
            for (int i = 0; i < height; i++)
            {
                expectedBoard[i] = lines[moveLine + 1 + i].Replace(" ", string.Empty);
            }

            // Inicjalizacja planszy i wykonanie ruchu
            Board board = new Board(height, width);
            board.Initialize(initialBoard);

            ExecuteMove(board, moveMethod);

            // Porównanie plansz
            bool testPassed = CompareBoards(board, expectedBoard);
            Console.WriteLine(testPassed ? "Test PASSED" : "Test FAILED");
        }
    }

    static void ExecuteMove(Board board, string moveMethod)
    {
        switch (moveMethod)
        {
            case "MoveLeft":
                board.MoveLeft('0', '1');
                break;
            case "MoveRight":
                board.MoveRight('0', '1');
                break;
            case "MoveUp":
                board.MoveUp('0', '1');
                break;
            case "MoveDown":
                board.MoveDown('0', '1');
                break;
            default:
                Console.WriteLine($"Unknown method: {moveMethod}");
                break;
        }
    }

    static bool CompareBoards(Board board, string[] expected)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (board._board[i, j] != expected[i][j])
                {
                    return false;
                }
            }
        }
        return true;
    }
#endif
}