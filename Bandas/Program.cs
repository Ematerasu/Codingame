//#define DEBUG_MODE
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
    private int[] Rows;
    private const int MASK = 0b11;
    private int height, width;

    public Board(int height, int width)
    {
        Rows = new int[8];
        this.height = height;
        this.width = width;
    }

    public void Initialize(string[] input)
    {
        for (int i = 0; i < height; i++)
        {
            Rows[i] = 0;
            //Debug.Log(input[i]+"\n");
            for (int j = 0; j < width; j++)
            {
                int value = input[i][j] switch
                {
                    '-' => 0b00,   // Puste pole
                    '0' => 0b01,   // Gracz 0
                    '1' => 0b10,   // Gracz 1
                    'x' => 0b11,   // Dziura
                    _ => 0b10
                };
                //Debug.Log($"Got {input[i][j]}, returned: {value}\n");
                Rows[i] |= value << (j * 2);
            }
            //Debug.Log($"{Convert.ToString(Rows[i], 2)}\n");
        }
    }

    public void MoveLeft()
    {
        for (int i = 0; i < height; i++)
        {
            int row = Rows[i];
            int rightmostMyPawn = FindRightmostMyPawn(row);
            if (rightmostMyPawn == -1)
                continue;
            int mask = (1 << ((rightmostMyPawn + 1) * 2)) - 1;
            int extractedBits = row & mask;
            extractedBits >>= 2;
            row &= ~mask;
            row |= extractedBits;
            Rows[i] = row;
        }
    }

    public void MoveRight()
    {
        for (int i = 0; i < height; i++)
        {
            int row = Rows[i];
            int leftmostMyPawn = FindLeftmostMyPawn(row);
            if (leftmostMyPawn == -1)
                continue;
            int mask = ~((1 << (leftmostMyPawn * 2)) - 1);
            int extractedBits = row & mask;
            extractedBits <<= 2;
            row &= ~mask;
            row |= extractedBits;
            row &= (1 << (width * 2)) - 1;
            Rows[i] = row;
        }
    }

    public void MoveUp()
    {
        var my_id = Variables.MY_ID == 0 ? 0b01 : 0b10;
        var enemy_id = Variables.MY_ID == 1 ? 0b01 : 0b10;
        var EMPTY = 0b00;
        for (int col = 0; col < width; col++)
        {
            int[] column = new int[height];

            // Ekstrakcja kolumny
            for (int row = 0; row < height; row++)
            {
                column[row] = (Rows[row] >> (col * 2)) & MASK;
            }

            // Przesuwanie kolumny w górę
            int writePos = 0;
            for (int readPos = 0; readPos < height; readPos++)
            {
                if (column[readPos] == my_id || column[readPos] == enemy_id)
                {
                    column[writePos++] = column[readPos];
                }
            }

            // Wypełnienie pustych pól na dole kolumny
            for (int i = writePos; i < height; i++)
            {
                column[i] = EMPTY;
            }

            // Wpisanie zmodyfikowanej kolumny z powrotem
            for (int row = 0; row < height; row++)
            {
                Rows[row] &= ~(MASK << (col * 2));         // Zerowanie starej kolumny
                Rows[row] |= column[row] << (col * 2);     // Wstawienie nowej wartości
            }
        }
    }

    public void MoveDown()
    {
        for (int col = 0; col < width; col++)
        {
            int writePos = 7;
            for (int row = 7; row >= 0; row--)
            {
                int cell = (Rows[row] >> (col * 2)) & MASK;
                if (cell == 0b00 || cell == 0b01)
                {
                    Rows[writePos] |= cell << (col * 2);
                    if (writePos != row) Rows[row] &= ~(MASK << (col * 2));
                    writePos--;
                }
            }
        }
    }

    public void RemoveEmptyRowsAndColumns()
    {
        for (int i = 0; i < height; i++)
        {
            if (IsRowEmpty(i)) Rows[i] = ~0; // Ustaw cały rząd na dziury (0b11)
        }
        for (int col = 0; col < width; col++)
        {
            if (IsColumnEmpty(col)) SetColumnToHoles(col);
        }
    }

    private bool IsRowEmpty(int row)
    {
        for (int j = 0; j < width; j++)
        {
            int cell = (Rows[row] >> (j * 2)) & MASK;
            if (cell != 0b10) return false; // Niepusty
        }
        return true;
    }

    private bool IsColumnEmpty(int col)
    {
        for (int row = 0; row < width; row++)
        {
            int cell = (Rows[row] >> (col * 2)) & MASK;
            if (cell != 0b10) return false;
        }
        return true;
    }

    private void SetColumnToHoles(int col)
    {
        for (int row = 0; row < width; row++)
        {
            Rows[row] |= (0b11 << (col * 2));
        }
    }

    private int FindRightmostMyPawn(int row)
    {
        var my_id = Variables.MY_ID == 0 ? 0b01 : 0b10;
        for (int pos = 7; pos >= 0; pos--)
        {
            int cell = (row >> (pos * 2)) & MASK;
            if (cell == my_id)
                return pos;
        }
        return -1;
    }

    private int FindLeftmostMyPawn(int row)
    {
        var my_id = Variables.MY_ID == 0 ? 0b01 : 0b10;
        for (int pos = 0; pos < width; pos++)
        {
            int cell = (row >> (pos * 2)) & MASK;
            if (cell == my_id)
                return pos;
        }
        return -1;
    }

    public void Print()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int cell = (Rows[i] >> (j*2)) & MASK;
                Console.Error.Write(cell switch
                {
                    0b00 => "- ",
                    0b01 => "0 ",
                    0b10 => "1 ",
                    0b11 => "x ",
                    _ => "? "
                });
            }
            Console.Error.WriteLine();
        }
        Console.Error.WriteLine();
    }
}
class Player
{
    public const long NOGC_SIZE = 67_108_864;
    static void Main(string[] args)
    {
        int myId = int.Parse(Console.ReadLine());
        int height = int.Parse(Console.ReadLine());
        int width = int.Parse(Console.ReadLine());
        int enemy_id = 1 - myId;
        Variables.MY_ID = myId;
        Debug.Log($"My id: {myId}, enemy: {enemy_id}\n");
        Debug.Log($"{height}x{width}\n");
        Utils.globalWatch.Start();

        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        GC.TryStartNoGCRegion(NOGC_SIZE);

        Board board = new Board(height, width);
        while (true)
        {
            List<string> lines = new List<string>();
            
            for (int i = 0; i < height; i++)
            {
                lines.Add(Console.ReadLine().Replace(" ", string.Empty));
            }

            board.Initialize(lines.ToArray());
            board.Print();
            board.MoveRight();
            board.Print();
            Console.WriteLine("RIGHT"); // UP | RIGHT | DOWN | LEFT

        }
    }
}