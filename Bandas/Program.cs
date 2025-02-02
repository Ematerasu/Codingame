﻿#define DEBUG_MODE
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
using System.Runtime.InteropServices;

namespace bandas;

public static class FastMath
{
    public static float FastLog(float x)
    {
        if (x <= 0)
        {
            throw new ArgumentException("Logarithm is undefined for non-positive values");
        }

        float result = 0f;

        int exp = 0;
        while (x >= 2.0f)
        {
            x *= 0.5f;
            exp++;
        }
        
        float z = x - 1.0f;
        float z2 = z * z;
        result = z - (z2 / 2) + (z2 * z / 3) - (z2 * z2 / 4);

        result += exp * 0.69314718f;

        return result;
    }

    public static float FastSqrt(float x)
    {
        if (x < 0)
        {
            throw new ArgumentException("Cannot compute square root of negative number");
        }

        float guess = x * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            guess = 0.5f * (guess + x / guess);
        }

        return guess;
    }
}

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

public enum GameResult
{
    PLAYER0_WIN,
    PLAYER1_WIN,
    DRAW,
    NOT_FINISHED,
}

public class Utils
{
    public static int RESPONSE_TIME = 100;
    public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch selectionWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch expansionWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch simulationWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch backpropagationWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch nodeCreationWatch = new System.Diagnostics.Stopwatch();

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

    public static void PrintWatches()
    {
        Debug.Log($"Selection took {selectionWatch.ElapsedMilliseconds}ms\n");
        Debug.Log($"Expansion took {expansionWatch.ElapsedMilliseconds}ms\n");
        Debug.Log($"Simulation took {simulationWatch.ElapsedMilliseconds}ms\n");
        Debug.Log($"Backpropagation took {backpropagationWatch.ElapsedMilliseconds}ms\n");
        Debug.Log($"Node creation took {nodeCreationWatch.ElapsedMilliseconds}ms\n");
    }
}

public struct Board
{
    public char[,] _board;
    public int Height, Width;
    public int ZeroPawns, OnePawns;
    public bool IsFinished = false;
    public GameResult FinalResult = GameResult.NOT_FINISHED;
    private (int up, int down, int left, int right) _bounds;

    public Board(int height, int width)
    {
        _board = new char[height, width];
        Height = height;
        Width = width;
        ZeroPawns = 0;
        OnePawns = 0;
        _bounds = (0, height-1, 0, width-1);
    }

    public void ResetBoard(char[,] board)
    {
        Array.Copy(board, _board, _board.Length);
        ZeroPawns = 0;
        OnePawns = 0;
    }

    public void GenerateBoard(long seed)
    {
        Random rng = new Random((int)seed);
        int pawnsPerPlayer = Width * Height / 2;
        int pawnsInCenterSquarePerPlayer = 8;

        for (int pawn = 0; pawn < pawnsInCenterSquarePerPlayer; pawn++)
        {
            int i, j;
            do
            {
                i = rng.Next(2, 6);
                j = rng.Next(2, 6);
            } while (_board[i, j] != '\0');
            _board[i, j] = '0';
            ZeroPawns++;
        }

        for (int i = 2; i < 6; i++)
        {
            for (int j = 2; j < 6; j++)
            {
                if (_board[i, j] == '\0')
                {
                    _board[i, j] = '1';
                    OnePawns++;
                }
            }
        }

        for (int pawn = 0; pawn < pawnsPerPlayer - pawnsInCenterSquarePerPlayer; pawn++)
        {
            int i, j;
            do
            {
                i = rng.Next(Height);
                j = rng.Next(Width);
            } while (_board[i, j] != '\0');
            _board[i, j] = '0'; // Gracz 0
            ZeroPawns++;
        }

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                if (_board[i, j] == '\0') // '\0' oznacza puste komórki
                {
                    _board[i, j] = '1'; // Gracz 1
                    OnePawns++;
                }
            }
        }
    }

    public Board Clone()
    {
        Board copy = new Board(Height, Width);
        copy._bounds = _bounds;
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                copy._board[i, j] = _board[i, j];
                switch (_board[i, j])
                {
                    case '0':
                        copy.ZeroPawns++;
                        break;
                    case '1':
                        copy.OnePawns++;
                        break;
                    default:
                        break;
                }
            }
        }
        copy.IsFinished = IsFinished;
        return copy;
    }

    public void Initialize(string[] input)
    {
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                _board[i, j] = input[i][j];
            }
        }
    }

    public void MoveLeft(char myPawn, char enemyPawn)
    {
        for (int i = 0; i < Height; i++)
        {
            char save = '-';
            int currIdx = Width - 1;
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
        for (int i = 0; i < Height; i++)
        {
            char save = '-';
            int currIdx = 0;
            bool movingRight = false;
            while (currIdx < Width)
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
        for (int j = 0; j < Width; j++)
        {
            char save = '-';
            int currIdx = Height - 1;
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
        for (int j = 0; j < Width; j++)
        {
            char save = '-';
            int currIdx = 0;
            bool movingDown = false;
            while (currIdx < Height)
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
        if (_bounds.up > _bounds.down || _bounds.left > _bounds.right) return;
        while (IsRowEmpty(_bounds.up))
        {
            for (int j = 0; j < Width; j++)
                _board[_bounds.up, j] = 'x';
            _bounds.up++;
            if (_bounds.up > _bounds.down) break;
        }

        while (IsRowEmpty(_bounds.down))
        {
            for (int j = 0; j < Width; j++)
                _board[_bounds.down, j] = 'x';
            _bounds.down--;
            if (_bounds.up > _bounds.down) break;
        }

        while (IsColumnEmpty(_bounds.left))
        {
            for (int j = 0; j < Height; j++)
                _board[j, _bounds.left] = 'x';
            _bounds.left++;
            if (_bounds.left > _bounds.right) break;
        }
        while (IsColumnEmpty(_bounds.right))
        {
            for (int j = 0; j < Height; j++)
                _board[j, _bounds.right] = 'x';
            _bounds.right--;
            if (_bounds.left > _bounds.right) break;
        }
    }

    public bool IsRowEmpty(int row)
    {
        for (int j = 0; j < Width; j++)
            if (_board[row, j] == '0' || _board[row, j] == '1')
                return false;
        return true;
    }
    public bool IsColumnEmpty(int col)
    {
        for (int j = 0; j < Width; j++)
            if (_board[j, col] == '0' || _board[j, col] == '1')
                return false;
        return true;
    }

    public GameResult IsOver()
    {
        int zeroPawns = 0;
        int onePawns = 0;

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                if (_board[i, j] == '0') zeroPawns++;
                if (_board[i, j] == '1') onePawns++;
            }
        }
        ZeroPawns = zeroPawns;
        OnePawns = onePawns;
        if (zeroPawns == 0 && onePawns > 0)
        {
            IsFinished = true;
            return GameResult.PLAYER1_WIN;
        } 
        if (zeroPawns > 0 && onePawns == 0) 
        {
            IsFinished = true;
            return GameResult.PLAYER0_WIN;
        }
        if (zeroPawns == 0 && onePawns == 0) 
        { 
            IsFinished = true;
            return GameResult.DRAW;
        }
        return GameResult.NOT_FINISHED;
    }
    public void Print()
    {
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                Console.Write(_board[i, j] + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    public void Play(Direction moveDirection, int playerId)
    {
        var movingPawn = playerId == 0 ? '0' : '1';
        var notMovingPawn = playerId == 1 ? '0' : '1';
        switch(moveDirection)
        {
            case Direction.Down:
                MoveDown(movingPawn, notMovingPawn);
                break;
            
            case Direction.Right:
                MoveRight(movingPawn, notMovingPawn);
                break;
            
            case Direction.Up:
                MoveUp(movingPawn, notMovingPawn);
                break;
            
            case Direction.Left:
                MoveLeft(movingPawn, notMovingPawn);
                break;
        }
        RemoveEmptyColumnsAndRows();
    }

    public bool Equals(Board other)
    {
        if (Width != other.Width || Height != other.Height)
            return false;
        bool IsEqual = true;
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                if (_board[i, j] != other._board[i, j])
                {
                    IsEqual = false;
                    break;
                }
            }
        }
        return IsEqual;
    }

}

class FlatMC
{
    private Board currentState;
    private int[] N;
    private int[] W;
    private Random rnd;
    private char myPawn, enemyPawn;
    public FlatMC(int myId)
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
        char[,] save = new char[currentState._board.GetLength(0), currentState._board.GetLength(1)];
        Array.Copy(currentState._board, save, currentState._board.Length);

        //Debug.Log($"Current global watch: {Utils.globalWatch.ElapsedMilliseconds}\n");
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
        var sims = 0;
        for (int i = 0; i < 4; i++)
        {
            //Debug.Log($"For action {(Direction)i} we have N: {N[i]}, W: {W[i]} \n");
            if (N[i] == 0) continue;
            sims += N[i];
            var tempValue = (float)W[i] / (float)N[i];
            if (tempValue > maxValue)
            {
                maxValue = tempValue;
                bestIdx = i;
            }
        }
        //Debug.Log($"Current global watch at the end: {Utils.globalWatch.ElapsedMilliseconds}\n");
        //Debug.Log($"FlatMC did {sims} simulations\n");
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
        GameResult result;
        var myTurn = false;
        while ((result = currentState.IsOver()) == GameResult.NOT_FINISHED)
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

        if (myPawn == '0' && result == GameResult.PLAYER0_WIN) return true;
        if (myPawn == '1' && result == GameResult.PLAYER1_WIN) return true;
        return false;
    }

}

public class FullMCTS
{
    const int MAX_ACTION = 4;
    const int MAX_TREE_SIZE = 1 << 16;
    const float C = 1.4f;

    private Random rng = new Random();
    readonly Direction[] ACTIONS = new Direction[] {Direction.Down, Direction.Left, Direction.Right, Direction.Up};
    public int PlayerID;
    public class Node
    {
        public int PlayerId;
        public Board State;
        public double Wins;
        public double Visits;
        public uint Parent;
        public uint[] ChildrenId;
        
        public Node(Board state, uint parent, int playerId)
        {
            ChildrenId = new uint[MAX_ACTION];
            Wins = 0;
            Visits = 0;
            State = state;
            Parent = parent;
            PlayerId = playerId;
        }

        public Node(Board state, Direction move,  uint parent, int playerId)
        {
            ChildrenId = new uint[MAX_ACTION];
            Wins = 0;
            Visits = 0;
            State = state.Clone();
            State.Play(move, 1 - playerId);
            Parent = parent;
            PlayerId = playerId;
        }

        public bool IsLeaf()
        {
            if (State.IsFinished) return true;
            for (int i = 0; i < MAX_ACTION; i++)
            {
                if (ChildrenId[i] != 0) return false;
            }
            return true;
        }
    }

    public class MCTSTree
    {
        public uint rootId = 0;
        public Node[] tree;
        public uint size;

        public MCTSTree()
        {
            rootId = 0;
            tree = new Node[MAX_TREE_SIZE];
            size = 0;
        }
    }

    public MCTSTree currentTree = new MCTSTree();

    public FullMCTS(int playerId)
    {
        PlayerID = playerId;
    }

    public void Reset(Board state)
    {
        currentTree = new MCTSTree();
        currentTree.rootId = 0;
        currentTree.tree[currentTree.rootId] = new Node(state, 0, PlayerID);
        currentTree.size++;
        for (int i = 0; i < MAX_ACTION; i++)
        {
            uint newId = currentTree.size;
            //Utils.nodeCreationWatch.Start();
            currentTree.tree[newId] = new Node(currentTree.tree[currentTree.rootId].State, ACTIONS[i], currentTree.rootId, 1 - currentTree.tree[currentTree.rootId].PlayerId);
            //Utils.nodeCreationWatch.Stop();
            currentTree.tree[currentTree.rootId].ChildrenId[i] = newId;
            currentTree.size++;
        }
    }

    public void ReuseTree(Board state)
    {
        if (currentTree.size  > MAX_TREE_SIZE - 5)
        {
            Reset(state);
            return;
        }
        uint newRootId = 0;
        bool found = false;

        for (int i = 0; i < MAX_ACTION; i++)
        {
            uint myMoveId = currentTree.tree[currentTree.rootId].ChildrenId[i];
            if (myMoveId == 0) continue;

            for (int j = 0; j < MAX_ACTION; j++)
            {
                uint opponentMoveId = currentTree.tree[myMoveId].ChildrenId[j];
                if (opponentMoveId != 0 && currentTree.tree[opponentMoveId].State.Equals(state))
                {
                    newRootId = opponentMoveId;
                    found = true;
                    break;
                }
            }

            if (found) break;
        }

        if (!found)
        {
            //Debug.Log("No matching child node found. Resetting tree.\n");
            currentTree = new MCTSTree();
            currentTree.tree[0] = new Node(state, 0, PlayerID);
            currentTree.size = 1;
        }
        else
        {
            currentTree.rootId = newRootId;
            //Debug.Log($"Tree reused with new root: {newRootId}\n");
        }
    }
    public Direction Evaluate()
    {
        //for(int i = 0; i < 10000; i++)
        while (Utils.globalWatch.ElapsedMilliseconds < Utils.RESPONSE_TIME - 10)
        {
            //Utils.selectionWatch.Start();
            uint selectedNode = Selection();
            //Utils.selectionWatch.Stop();
            //Utils.expansionWatch.Start();
            uint newChild = Expand(selectedNode);
            //Utils.expansionWatch.Stop();
            if (newChild == 0)
            {
                throw new Exception("Cos sie odjebalo w Expand");
            }
            //Utils.simulationWatch.Start();
            GameResult simulationResult = Simulation(newChild);
            //Utils.simulationWatch.Stop();
            //Utils.backpropagationWatch.Start();
            Backpropagate(newChild, simulationResult);
            //Utils.backpropagationWatch.Stop();
        }
        //PrintTreeState();
        //Debug.Log($"FullMCTS did {currentTree.tree[currentTree.rootId].Visits} simulations\n");

        uint bestChildId = 0;
        double maxVisits = 0;
        for (int i = 0; i < MAX_ACTION; i++)
        {
            uint childId = currentTree.tree[currentTree.rootId].ChildrenId[i];
            if (childId == 0) continue;

            var visits = currentTree.tree[childId].Visits;
            if (visits > maxVisits)
            {
                maxVisits = visits;
                bestChildId = childId;
            }
        }
        for(int i=0; i < 4; i++)
        {
            uint childId = currentTree.tree[currentTree.rootId].ChildrenId[i];
            Debug.Log($"MCTS_MAIN: Child {ACTIONS[i]}: Wins: {currentTree.tree[childId].Wins}, Visits: {currentTree.tree[childId].Visits}\n");
        }
        Debug.Log($"MCTS_MAIN: Go with {ACTIONS[Array.IndexOf(currentTree.tree[currentTree.rootId].ChildrenId, bestChildId)]}\n\n");
        return ACTIONS[Array.IndexOf(currentTree.tree[currentTree.rootId].ChildrenId, bestChildId)];
    }

    private uint Selection()
    {
        uint currentId = currentTree.rootId;
        while (!currentTree.tree[currentId].IsLeaf())
        {
            uint bestChildId = currentTree.tree[currentId].ChildrenId[0];
            double bestUCB1 = -100.0;
            double currentNodeVisits = currentTree.tree[currentId].Visits;
            for (int i = 0; i < MAX_ACTION; i++)
            {
                uint childId = currentTree.tree[currentId].ChildrenId[i];
                if (childId == 0) continue;

                double childVisits = currentTree.tree[childId].Visits; 
                double childWins = currentTree.tree[childId].Wins;
                
                double ucb1 = childWins / (childVisits + 1) + C * Math.Sqrt(Math.Log(currentNodeVisits)) / (childVisits+1);
                //Debug.Log($"UCB1 value for: {childId} with {childVisits} visits, {childWins} wins and {currentNodeVisits} parent visits: {ucb1}\n");
                if (ucb1 > bestUCB1)
                {
                    bestUCB1 = ucb1;
                    bestChildId = childId;
                }
            }

            currentId = bestChildId;
        }

        return currentId;
    }

    private uint Expand(uint nodeId)
    {
        if (currentTree.size == MAX_TREE_SIZE)
        {
            return nodeId;
        }
        else if (currentTree.tree[nodeId].State.IsFinished)
        {
            //Debug.Log("Reached finished state\n");
            return nodeId;
        }
        List<uint> ids = new List<uint>(MAX_ACTION);
        for (int i = 0; i < MAX_ACTION; i++)
        {
            uint newId = currentTree.size;
            //Utils.nodeCreationWatch.Start();
            currentTree.tree[newId] = new Node(currentTree.tree[nodeId].State, ACTIONS[i], nodeId, 1 - currentTree.tree[nodeId].PlayerId);
            //Utils.nodeCreationWatch.Stop();
            currentTree.tree[nodeId].ChildrenId[i] = newId;
            currentTree.size++;
            ids.Add(newId);
            if (currentTree.size == MAX_TREE_SIZE)
                break;
        }

        return ids[rng.Next(ids.Count)];
    }

    private GameResult Simulation(uint nodeId)
    {   
        GameResult result = currentTree.tree[nodeId].State.IsOver();
        if (currentTree.tree[nodeId].State.IsFinished)
        {
            //Debug.Log("Simulation not needed, state already finished\n");
            return result;
        }
        Board currentState = currentTree.tree[nodeId].State.Clone();
        int currentPlayer = currentTree.tree[nodeId].PlayerId;
        int round = 0;
        while (!currentState.IsFinished && round < 6)
        {
            Direction randomMove = ACTIONS[rng.Next(MAX_ACTION)];
            currentState.Play(randomMove, currentPlayer);
            result = currentState.IsOver();
            currentPlayer = 1 - currentPlayer;
            round++;
        }
        if (!currentState.IsFinished)
        {
            return currentState.ZeroPawns > currentState.OnePawns ? GameResult.PLAYER0_WIN : GameResult.PLAYER1_WIN;
        }
        return result;
    }

    private void Backpropagate(uint nodeId, GameResult result)
    {
        uint currentNode = nodeId;
        while (currentNode != currentTree.rootId)
        {
            currentTree.tree[currentNode].Visits++;
            int parentPlayerId = 1 - currentTree.tree[currentNode].PlayerId;

            if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 0) ||
                (result == GameResult.PLAYER1_WIN && parentPlayerId == 1))
            {
                currentTree.tree[currentNode].Wins += 1.0;
            }
            else if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 1) ||
                    (result == GameResult.PLAYER1_WIN && parentPlayerId == 0))
            {
                currentTree.tree[currentNode].Wins -= 1.0;
            }
            currentNode = currentTree.tree[currentNode].Parent;
        }

        currentTree.tree[currentTree.rootId].Visits++;

        if ((result == GameResult.PLAYER0_WIN && 1 - PlayerID == 0) ||
            (result == GameResult.PLAYER1_WIN && 1 - PlayerID == 1))
        {
            currentTree.tree[currentTree.rootId].Wins += 1.0;
        }
        else if ((result == GameResult.PLAYER0_WIN && 1 - PlayerID == 1) ||
                (result == GameResult.PLAYER1_WIN && 1 - PlayerID == 0))
        {
            currentTree.tree[currentTree.rootId].Wins -= 1.0;
        }
    }


    private void PrintTreeState()
    {
        for (uint i = currentTree.rootId; i < currentTree.size; i++)
        {
            Node node = currentTree.tree[i];
            string childrenIds = string.Join(", ", node.ChildrenId.Where(id => id != 0).Select(id => id.ToString()));

            Console.Error.WriteLine($"Node {i}:");
            Console.Error.WriteLine($"  PlayerId: {node.PlayerId}");
            Console.Error.WriteLine($"  Wins: {node.Wins}");
            Console.Error.WriteLine($"  Visits: {node.Visits}");
            Console.Error.WriteLine($"  Parent: {node.Parent}");
            Console.Error.WriteLine($"  Children: {childrenIds}");
            Console.Error.WriteLine();
        }
    }
}

public class BaseLineMCTS
{
    const float C = 1.4f;

    private Random rng = new Random();
    readonly Direction[] ACTIONS = new Direction[] {Direction.Down, Direction.Left, Direction.Right, Direction.Up};
    public int PlayerID;
    private Node? root;
    private bool _reuseTree = false;

    private class Node
    {
        public Node? Parent { get; set; }
        public Node[] Children { get; set; }
        public double Visits { get; set; }
        public double Wins { get; set; }
        public Board State { get; set; }
        public int playerId { get; set; }

        public Node(Node? parent, Board state, int playerId)
        {
            Parent = parent;
            State = state;
            Children = new Node[4];
            Visits = 0;
            Wins = 0;
            this.playerId = playerId;
        }

        public bool IsFullyExpanded()
        {
            for(int i = 0; i < 4; i++)
            {
                if (Children[i] == null) return false;
            }
            return true;
        }

        public Node GetBestChild()
        {
            double ucb, bestUCB = -100.0;
            int bestIdx = 0;
            for(int i = 0; i < 4; i++)
            {
                var child = Children[i];
                ucb = child.Visits > 0 ? child.Wins/child.Visits + C * Math.Sqrt(Math.Log(Visits) / child.Visits) : double.MaxValue;
                if (ucb > bestUCB)
                {
                    bestIdx = i;
                    bestUCB = ucb;
                }
            }
            return Children[bestIdx];
        }
    }
    public BaseLineMCTS(int playerId, bool reuseTree=false)
    {
        PlayerID = playerId;
        _reuseTree = reuseTree;
    }


    public Direction Evaluate(Board initialState)
    {
        if (root == null || !_reuseTree)
            root = new Node(null, initialState, PlayerID);
        else
        {
            Node? matchingChild = null;

            foreach(var child in root.Children)
            {
                foreach (var grandchild in child.Children)
                {
                    if (grandchild != null && grandchild.State.Equals(initialState))
                    {
                        matchingChild = grandchild;
                        break;
                    }
                }
                if (matchingChild != null) break;
            }
            

            if (matchingChild != null)
            {
                root = matchingChild;
                root.Parent = null;
                Debug.Log("Reused Tree!\n");
            }
            else
            {
                root = new Node(null, initialState, PlayerID);
            }
        }
        //for(int i = 0; i < 100 ; i++)
        while (Utils.globalWatch.ElapsedMilliseconds < Utils.RESPONSE_TIME - 5)
        {
            Node selectedNode = Selection(root);
            Node expandedNode = Expansion(selectedNode);
            GameResult result = Simulation(expandedNode);
            Backpropagation(expandedNode, result);
        }

        int bestChildId = 0;
        double maxVisits = 0;
        for (int i = 0; i < 4; i++)
        {
            var visits = root.Children[i].Visits;
            if (visits > maxVisits)
            {
                maxVisits = visits;
                bestChildId = i;
            }
        }
        for(int i=0; i < 4; i++)
        {
            Debug.Log($"MCTS2: Child {ACTIONS[i]}: Wins: {root.Children[i].Wins}, Visits: {root.Children[i].Visits}\n");
        }
        Debug.Log($"MCTS2: Go with {ACTIONS[bestChildId]}\n\n");
        return ACTIONS[bestChildId];
    }

    private Node Selection(Node node)
    {
        while (node.IsFullyExpanded())
        {
            node = node.GetBestChild();
        }
        return node;
    }

    private Node Expansion(Node node)
    {
        for (int i = 0; i < 4; i++)
        {
            if (node.Children[i] == null)
            {
                var newState = node.State.Clone();
                newState.Play(ACTIONS[i], node.playerId);
                var newNode = new Node(node, newState, 1 - node.playerId);
                node.Children[i] = newNode;

                return newNode;
            }
        }

        throw new InvalidOperationException("All children are already expanded.");
    }

    private GameResult Simulation(Node node)
    {
        var state = node.State.Clone();
        var result = state.IsOver();
        var currPlayer = node.playerId;
        while(result == GameResult.NOT_FINISHED)
        {
            var randomMove = ACTIONS[rng.Next(4)];
            state.Play(randomMove, currPlayer);
            currPlayer = 1 - currPlayer;
            result = state.IsOver();
        }
        return result;
    }

    private void Backpropagation(Node node, GameResult result)
    {
        while (node != null)
        {
            node.Visits++;
            int parentPlayerId = 1 - node.playerId;

            if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 0) ||
                (result == GameResult.PLAYER1_WIN && parentPlayerId == 1))
            {
                node.Wins += 1.0;
            }
            else if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 1) ||
                    (result == GameResult.PLAYER1_WIN && parentPlayerId == 0))
            {
                node.Wins -= 1.0;
            }
            node = node.Parent;
        }
    }
}

public class FullMCTS2
{
    const float C = 1.4f;

    private Random rng = new Random();
    readonly Direction[] ACTIONS = new Direction[] {Direction.Down, Direction.Left, Direction.Right, Direction.Up};
    public int PlayerID;
    private Node? root;
    private bool _reuseTree = false;

    private class Node
    {
        public Node? Parent { get; set; }
        public Node[] Children { get; set; }
        public double Visits { get; set; }
        public double Wins { get; set; }
        public Board State { get; set; }
        public int playerId { get; set; }
        public bool IsWinning {get; set;}
        public bool IsLosing {get; set;}

        public Node(Node? parent, Board state, int playerId)
        {
            Parent = parent;
            State = state;
            Children = new Node[4];
            Visits = 0;
            Wins = 0;
            this.playerId = playerId;
            IsWinning = false;
            IsLosing = false;
        }

        public bool IsFullyExpanded()
        {
            for(int i = 0; i < 4; i++)
            {
                if (Children[i] == null) return false;
            }
            return true;
        }

        public Node GetBestChild()
        {
            double ucb, bestUCB = -100.0;
            int bestIdx = 0;
            for(int i = 0; i < 4; i++)
            {
                var child = Children[i];
                ucb = child.Visits > 0 ? child.Wins/child.Visits + C * Math.Sqrt(Math.Log(Visits) / child.Visits) : double.MaxValue;
                if (ucb > bestUCB)
                {
                    bestIdx = i;
                    bestUCB = ucb;
                }
            }
            return Children[bestIdx];
        }
    }
    public FullMCTS2(int playerId, bool reuseTree=false)
    {
        PlayerID = playerId;
        _reuseTree = reuseTree;
    }


    public Direction Evaluate(Board initialState)
    {
        if (root == null || !_reuseTree)
            root = new Node(null, initialState, PlayerID);
        else
        {
            Node? matchingChild = null;

            foreach(var child in root.Children)
            {
                foreach (var grandchild in child.Children)
                {
                    if (grandchild != null && grandchild.State.Equals(initialState))
                    {
                        matchingChild = grandchild;
                        break;
                    }
                }
                if (matchingChild != null) break;
            }
            

            if (matchingChild != null)
            {
                root = matchingChild;
                root.Parent = null;
                Debug.Log("Reused Tree!\n");
            }
            else
            {
                root = new Node(null, initialState, PlayerID);
            }
        }
        //for(int i = 0; i < 100 ; i++)
        while (Utils.globalWatch.ElapsedMilliseconds < Utils.RESPONSE_TIME - 3)
        {
            Node selectedNode = Selection(root);
            Node expandedNode = Expansion(selectedNode);
            GameResult result = Simulation(expandedNode);
            Backpropagation(expandedNode, result);
        }

        int bestChildId = 0;
        double maxVisits = 0;
        for (int i = 0; i < 4; i++)
        {
            var visits = root.Children[i].Visits;
            if (visits > maxVisits)
            {
                maxVisits = visits;
                bestChildId = i;
            }
        }
        for(int i=0; i < 4; i++)
        {
            Debug.Log($"MCTS2: Child {ACTIONS[i]}: Wins: {root.Children[i].Wins}, Visits: {root.Children[i].Visits}\n");
        }
        Debug.Log($"MCTS2: Go with {ACTIONS[bestChildId]}\n\n");
        return ACTIONS[bestChildId];
    }

    private Node Selection(Node node)
    {
        while (node.IsFullyExpanded())
        {
            node = node.GetBestChild();
        }
        return node;
    }

    private Node Expansion(Node node)
    {
        if (node.IsWinning || node.IsLosing) return node;

        for (int i = 0; i < 4; i++)
        {
            if (node.Children[i] == null)
            {
                var newState = node.State.Clone();
                newState.Play(ACTIONS[i], node.playerId);
                var newNode = new Node(node, newState, 1 - node.playerId);
                node.Children[i] = newNode;

                return newNode;
            }
        }

        throw new InvalidOperationException("All children are already expanded.");
    }

    private GameResult Simulation(Node node)
    {
        if (node.IsWinning) return PlayerID == 0 ? GameResult.PLAYER0_WIN : GameResult.PLAYER1_WIN;
        if (node.IsLosing) return PlayerID == 1 ? GameResult.PLAYER0_WIN : GameResult.PLAYER1_WIN;

        var state = node.State.Clone();
        var result = state.IsOver();
        if (result == GameResult.PLAYER0_WIN)
        {
            if (PlayerID == 0) node.IsWinning = true;
            else node.IsLosing = true;

            return result;
        }
        else if (result == GameResult.PLAYER1_WIN)
        {
            if (PlayerID == 1) node.IsWinning = true;
            else node.IsLosing = true;

            return result;
        }
        var currPlayer = node.playerId;
        while(result == GameResult.NOT_FINISHED)
        {
            var randomMove = ACTIONS[rng.Next(4)];
            state.Play(randomMove, currPlayer);
            currPlayer = 1 - currPlayer;
            result = state.IsOver();
        }
        return result;
    }

    private void Backpropagation(Node node, GameResult result)
    {
        while (node != null)
        {
            node.Visits++;
            int parentPlayerId = 1 - node.playerId;

            if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 0) ||
                (result == GameResult.PLAYER1_WIN && parentPlayerId == 1))
            {
                node.Wins += 1.0;
            }
            else if ((result == GameResult.PLAYER0_WIN && parentPlayerId == 1) ||
                    (result == GameResult.PLAYER1_WIN && parentPlayerId == 0))
            {
                node.Wins -= 1.0;
            }
            node = node.Parent;
        }
    }
}



class Player
{
    public const long NOGC_SIZE = 67_108_864;
    static void Main(string[] args)
    {
#if TEST_MODE
        RunTests();
        return;
#endif

#if DEBUG_MODE
        int myId = 0;
        int height = 8;
        int width = 8;
#else
        int myId = int.Parse(Console.ReadLine());
        int height = int.Parse(Console.ReadLine());
        int width = int.Parse(Console.ReadLine());
        GC.TryStartNoGCRegion(NOGC_SIZE);
#endif
        int enemy_id = 1 - myId;
        Variables.MY_ID = myId;
        Debug.Log($"My id: {myId}, enemy: {enemy_id}\n");
        Debug.Log($"{height}x{width}\n");
        var mcts = new FullMCTS2(myId, true);
        Board board = new Board(height, width);
        int round = 1;
#if !DEBUG_MODE
        while (true)
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < height; i++)
            {
                lines.Add(Console.ReadLine().Replace(" ", string.Empty));
            }

            board.Initialize(lines.ToArray());
            Utils.globalWatch.Restart();
            var move = mcts.Evaluate(board.Clone());
            Console.WriteLine(Utils.DirectionToString(move));
        }
    }
}
#else
        var adversary = new BaseLineMCTS(enemy_id, true);
        int player0win = 0;
        int player1win = 0;
        for(int game = 0; game < 50; game++)
        {
            round = 1;
            var seed = Guid.NewGuid().GetHashCode();
            Debug.Log($"Seed: {seed}\n");
            var i = 0;
            board = new Board(height, width);
            board.GenerateBoard(seed);
            board.Print();
            //GameVisualizer.ClearFolder("imgs");
            //GameVisualizer.VisualizeBoard(board, boardFile: $"Board{i}");
            var random = new Random(seed);
            i++;
            while (board.IsOver() == GameResult.NOT_FINISHED)
            {
                Utils.globalWatch.Restart();
                var move = mcts.Evaluate(board.Clone());
                // GameVisualizer.VisualizeMCTSTree(mcts.currentTree);
                // Console.ReadLine();
                board.Play(move, myId);
                //GameVisualizer.VisualizeBoard(board, $"Board{i}-Player0-{move}");
                if (board.IsOver() != GameResult.NOT_FINISHED) break;
                Utils.globalWatch.Restart();
                move = adversary.Evaluate(board.Clone());
                Debug.Log($"Played {move}\n");
                board.Play(move, enemy_id);
                //GameVisualizer.VisualizeBoard(board, $"Board{i}-Player1-{move}");
                i++;
                //board.Print();
                round++;
                if (round > 200) break;
            }
            i++;
            //board.Print();
            //GameVisualizer.VisualizeBoard(board, boardFile: $"Board{i}");
            var result = board.IsOver();
            if (result == GameResult.PLAYER0_WIN) player0win++;
            if (result == GameResult.PLAYER1_WIN) player1win++;
        }
        Debug.Log($"Player0 wins: {player0win}\n");
        Debug.Log($"Player1 wins: {player1win}\n");
        Utils.PrintWatches();
        Debug.Log($"My id: {myId}, enemy: {enemy_id}\n");
    }
}
#if TEST_MODE
    static void RunTests()
    {
        const int numTests = 2;

        for (int testIndex = 1; testIndex <= numTests; testIndex++)
        {
            string fileName = $"test{testIndex}.txt";
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

            int moveLine = 3 + height;
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
#endif
