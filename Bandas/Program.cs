#define DEBUG_MODE
//#define DEBUG_PRINT

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.Marshalling;
using System.Globalization;

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
    private char[,] _board;
    private int height, width;
    private char myPawn = Variables.MY_ID == 0 ? '0' : '1';
    private char enemyPawn = Variables.MY_ID == 1 ? '0' : '1';

    public Board(int height, int width)
    {
        _board = new char[height, width];
        this.height = height;
        this.width = width;
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

    public void MoveLeft()
    {
        for (int i = 0; i < height; i++)
        {
            bool movingLeft = false;
            int currentIdx = width - 1;
            while (currentIdx > 0)
            {
                if (_board[i, currentIdx] == enemyPawn && !movingLeft)
                    currentIdx--;
                if (_board[i, currentIdx] == myPawn && !movingLeft)
            }

        }
    }

    public void MoveRight()
    {
        
    }

    public void MoveUp()
    {
        
    }

    public void MoveDown()
    {
        
    }

    public void Transpose()
    {
        
    }

    public void RemoveEmptyRowsAndColumns()
    {
        
    }

    private bool IsRowEmpty(int row)
    {
        return true;
    }

    private bool IsColumnEmpty(int col)
    {
        return true;
    }

    private void SetColumnToHoles(int col)
    {
        
    }

    private int FindRightmostMyPawn(int row)
    {
        return 0;
    }

    private int FindLeftmostMyPawn(int row)
    {
        return 0;
    }

    public void Print()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Console.Error.Write($"{_board[i, j]} ");
            }
            Console.Error.WriteLine();
        }
        Console.Error.WriteLine();
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
#else
        int myId = int.Parse(Console.ReadLine());
        int height = int.Parse(Console.ReadLine());
        int width = int.Parse(Console.ReadLine());
#endif
        int enemy_id = 1 - myId;
        Variables.MY_ID = myId;
        Debug.Log($"My id: {myId}, enemy: {enemy_id}\n");
        Debug.Log($"{height}x{width}\n");
        Utils.globalWatch.Start();

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
            board.Print();
            board.MoveUp();
            board.Print();
            Console.WriteLine("UP"); // UP | RIGHT | DOWN | LEFT
#if !DEBUG_MODE
        }
#endif
    }
}