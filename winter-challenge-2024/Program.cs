#define DEBUG_MODE
//#define EVO_MODE
#define NEW_GAME_STATE_TEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Linq;
using System.Net;

#if DEBUG_MODE
namespace winter_challenge_2024;
#endif
public class Debug
{
    public static void Log(string msg)
    {
        Console.Error.Write(msg);
    }
}

public enum CommandEnum
{
    GROW,
    SPORE,
    WAIT,
}

public enum CellType
{
    EMPTY,
    WALL,
    ROOT,
    BASIC,
    HARVESTER,
    TENTACLE,
    SPORER,
    PROTEIN_A,
    PROTEIN_B,
    PROTEIN_C,
    PROTEIN_D,
}

public enum Direction
{
    N, E, S, W, X,
}

public class Entity
{
    public CellType Type { get; set; } = CellType.EMPTY;
    public int OwnerId { get; set; } = -1;
    public int Id { get; set; } = 0;
    public Direction Dir { get; set; } = Direction.X;
    public int ParentId { get; set; } = -1;
    public List<int> ChildrenId = new List<int>(4);
    public int OrganRootId { get; set; } = -1;
    public (int x, int y) Position { get; set; } = (0, 0);

    public bool IsUpToDate = false;

    public Entity() { }

    public Entity Clone()
    {
        return new Entity
        {
            Type = this.Type,
            OwnerId = this.OwnerId,
            Id = this.Id,
            Dir = this.Dir,
            ParentId = this.ParentId,
            ChildrenId = new List<int>(this.ChildrenId),
            OrganRootId = this.OrganRootId,
            Position = this.Position,
            IsUpToDate = this.IsUpToDate,
        };
    }

    public override string ToString()
    {
        return $"Entity [Type={Type}, OwnerId={OwnerId}, Id={Id}, Dir={Dir}, ParentId={ParentId}, " +
            $"ChildrenId=[{string.Join(", ", ChildrenId)}], OrganRootId={OrganRootId}, Position=({Position.x}, {Position.y})]\n";
    }
}


public struct Action
{
    public int X { get; init; }
    public int Y { get; init; }
    public CommandEnum Command { get; init; }
    public int Id { get; init; }
    public CellType Type { get; init; }
    public Direction Direction { get; init; }

    public string ToString(string additionalMessage = "")
    {
        if (Command == CommandEnum.WAIT)
            return "WAIT";
        if (Command == CommandEnum.SPORE)
            return $"SPORE {Id} {X} {Y} {additionalMessage}";
        if (Type == CellType.HARVESTER || Type == CellType.TENTACLE || Type == CellType.SPORER)
            return $"{Command.ToString().ToUpper()} {Id} {X} {Y} {Type.ToString().ToUpper()} {Direction} {additionalMessage}";
        // if grow basic
        return $"{Command.ToString().ToUpper()} {Id} {X} {Y} {Type.ToString().ToUpper()} {additionalMessage}";
    }

    public static Action GrowBasic(int id, int x, int y)
    {
        return new Action
        {
            Command = CommandEnum.GROW,
            Id = id,
            X = x,
            Y = y,
            Type = CellType.BASIC,
            Direction = Direction.X
        };
    }

    public static Action GrowTentacle(int id, int x, int y, Direction dir)
    {
        return new Action
        {
            Command = CommandEnum.GROW,
            Id = id,
            X = x,
            Y = y,
            Type = CellType.TENTACLE,
            Direction = dir
        };
    }
    public static Action GrowHarvester(int id, int x, int y, Direction dir)
    {
        return new Action
        {
            Command = CommandEnum.GROW,
            Id = id,
            X = x,
            Y = y,
            Type = CellType.HARVESTER,
            Direction = dir
        };
    }

    public static Action GrowSporer(int id, int x, int y, Direction dir)
    {
        return new Action
        {
            Command = CommandEnum.GROW,
            Id = id,
            X = x,
            Y = y,
            Type = CellType.SPORER,
            Direction = dir
        };
    }

    public static Action Spore(int id, int x, int y)
    {
        return new Action
        {
            Command = CommandEnum.SPORE,
            Id = id,
            X = x,
            Y = y,
            Type = CellType.ROOT,
            Direction = Direction.X
        };
    }

    public static Action Wait()
    {
        return new Action
        {
            Command = CommandEnum.WAIT
        };
    }

}
public class Utils
{
    public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch GenerateMovesWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch GrowWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch TentacleWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch HarvestWatch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch GameOverCheckWatch = new System.Diagnostics.Stopwatch();
    public const int FIRST_TURN_TIME = 1000;
    public const int MAX_TURN_TIME = 50;

    public static Direction vectorToDir((int nx, int ny) vector)
    {
        return vector switch
        {
            (0, 1) => Direction.S,
            (1, 0) => Direction.E,
            (0, -1) => Direction.N,
            (-1, 0) => Direction.W,
            _ => Direction.X
        };
    }

    public static (int nx, int ny) DirToVector(Direction dir)
    {
        return dir switch
        {
            Direction.S => (0, 1),
            Direction.E => (1, 0),
            Direction.N => (0, -1),
            Direction.W => (-1, 0),
            Direction.X => (0, 0),
            _ => (0, 0),
        };
    }
    public static string DirToString(Direction dir)
    {
        return dir switch
        {
            Direction.S => "S",
            Direction.E => "E",
            Direction.N => "N",
            Direction.W => "W",
            Direction.X => "X",
            _ => "N",
        };
    }
    public static (int nx, int ny) StringDirToVector(string dir)
    {
        return dir switch
        {
            "S" => (0, 1),
            "E" => (1, 0),
            "N" => (0, -1),
            "W" => (-1, 0),
            "X" => (0, 0),
            _ => (0, 0),
        };
    }

    public static CellType StringToCellType(string type)
    {
        return type switch
        {
            "WALL" => CellType.WALL,
            "ROOT" => CellType.ROOT,
            "BASIC" => CellType.BASIC,
            "HARVESTER" => CellType.HARVESTER,
            "TENTACLE" => CellType.TENTACLE,
            "SPORER" => CellType.SPORER,
            "A" => CellType.PROTEIN_A,
            "B" => CellType.PROTEIN_B,
            "C" => CellType.PROTEIN_C,
            "D" => CellType.PROTEIN_D,
            _ => CellType.EMPTY,
        };
    }

    public static Direction StringToDirection(string dir)
    {
        return dir switch
        {
            "N" => Direction.N,
            "E" => Direction.E,
            "W" => Direction.W,
            "S" => Direction.S,
            _ => Direction.X,
        };
    }

    public static string CellTypeToString(CellType cellType)
    {
        return cellType switch
        {
            CellType.EMPTY => ".",
            CellType.WALL => "W",
            CellType.ROOT => "R",
            CellType.BASIC => "B",
            CellType.HARVESTER => "H",
            CellType.TENTACLE => "T",
            CellType.SPORER => "S",
            CellType.PROTEIN_A => "A",
            CellType.PROTEIN_B => "B",
            CellType.PROTEIN_C => "C",
            CellType.PROTEIN_D => "D",
            _ => "."
        };
    }
}

/**
    * Grow and multiply your organisms to end up larger than your opponent.
**/
class MainClass
{
    public const long NOGC_SIZE = 67_108_864; // 280_000_000;
    static void Main(string[] args)
    {
#if NEW_GAME_STATE_TEST
    var random = new Random();
    var bot1 = new BeamSearchBot(1, 3, 3);
    var bot2 = new RandomBot(0);
    Utils.globalWatch.Restart();
    NewGameState newGameState = GameStateGenerator.GenerateGameState(random);
    var i = 0;
    while(!newGameState.IsGameOver)
    {
        var actions1 = bot1.Evaluate(newGameState);
        var actions2 = bot2.Evaluate(newGameState);
        newGameState.ProcessTurn(actions1, actions2);
        string bot1ActionsStr = string.Join("\\n", actions1.Select(act => act.ToString()));
        string bot2ActionsStr = string.Join("\\n", actions2.Select(act => act.ToString()));
        Helpers.VisualizeDebugState(newGameState, $"TEST{i}", $"imgs", $"Bot1:\\n{bot1ActionsStr}\\nBot2:\\n{bot2ActionsStr}\\n");
        i++;
    }
    Console.WriteLine($"Elapsed: {Utils.globalWatch.ElapsedMilliseconds}ms\n");
    Console.WriteLine($"Generate moves time: {Utils.GenerateMovesWatch.ElapsedMilliseconds}ms\n");
    Console.WriteLine($"Grow time: {Utils.GrowWatch.ElapsedMilliseconds}ms\n");
    Console.WriteLine($"Harvester time: {Utils.HarvestWatch.ElapsedMilliseconds}ms\n");
    Console.WriteLine($"Tentacle time: {Utils.TentacleWatch.ElapsedMilliseconds}ms\n");
    Console.WriteLine($"GameOverCheck time: {Utils.GameOverCheckWatch.ElapsedMilliseconds}ms\n");
    return;
#endif
#if EVO_MODE
        StartEvolution();
        return;
#endif
        NewGameState gameState;

#if DEBUG_MODE
        // //Console.WriteLine("DEBUG_MODE is ON. Generating GameState using GameStateGenerator...");
        // var random = new Random();
        // var bot1 = new BeamSearchBot(0, 9, 8);
        // var bot2 = new HeuristicBot(1);
        // var results = new int[3] { 0, 0, 0};
        // Utils.globalWatch.Restart();
        // for(int i = 0; i < 3; i++)
        // {
        //     Console.WriteLine($"Game: {i+1}\n");
        //     gameState = GameStateGenerator.GenerateGameState(random);
        //     //Console.WriteLine($"Generated GameState: Width = {gameState.Width}, Height = {gameState.Height}");
        //     //PrintDebugState(gameState);
        //     //Console.WriteLine();
        //     while(!gameState.IsGameOver)
        //     {
        //         Utils.watch.Restart();
        //         var actions = bot1.Evaluate(gameState);
        //         Utils.watch.Restart();
        //         var actions1 = bot2.Evaluate(gameState);
        //         string title = $"Game#{i+1} - TURN {gameState.Turn:D3}";
        //         string strActions = string.Join("\\n", actions.Select(act => act.ToString()));
        //         //Helpers.VisualizeDebugState(gameState, title, $"imgs/Game{i+1}", $"Action by BeamSearch\\n: {strActions}");
        //         // Console.WriteLine($"Action for player 0: {actions[0].ToString()}");
        //         // Console.WriteLine($"Action for player 1: {actions1[0].ToString()}");
        //         gameState.ProcessTurn(actions, actions1);
        //         //Helpers.PrintDebugState(gameState);
        //         Utils.watch.Reset();
        //     }
        //     results[gameState.GetWinner()]++;
        // }
        // Console.WriteLine($"Elapsed: {Utils.globalWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"Generate moves time: {Utils.GenerateMovesWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"Grow time: {Utils.GrowWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"Harvester time: {Utils.HarvestWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"Tentacle time: {Utils.TentacleWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"GameOverCheck time: {Utils.GameOverCheckWatch.ElapsedMilliseconds}ms\n");
        // Console.WriteLine($"Results: {results[0]} {results[1]} {results[2]} \n");
        

#else
        GC.TryStartNoGCRegion(NOGC_SIZE); // true
        var bot1 = new BeamSearchBot(0, 9, 8);
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]); // columns in the game grid
        int height = int.Parse(inputs[1]); // rows in the game grid
        gameState = new GameState(width, height);

        // game loop
        while (true)
        {
            int entityCount = int.Parse(Console.ReadLine());
            Utils.globalWatch.Start();
            for (int i = 0; i < entityCount; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                int x = int.Parse(inputs[0]);
                int y = int.Parse(inputs[1]); // grid coordinate
                string type = inputs[2]; // WALL, ROOT, BASIC, TENTACLE, HARVESTER, SPORER, A, B, C, D
                int owner = int.Parse(inputs[3]); // 1 if your organ, 0 if enemy organ, -1 if neither
                int organId = int.Parse(inputs[4]); // id of this entity if it's an organ, 0 otherwise
                string organDir = inputs[5]; // N,E,S,W or X if not an organ
                int organParentId = int.Parse(inputs[6]);
                int organRootId = int.Parse(inputs[7]);
                gameState.AddEntity(
                    (x, y),
                    Utils.StringToCellType(type),
                    organId,
                    owner,
                    Utils.StringToDirection(organDir),
                    organParentId,
                    organRootId
                );
            }
            gameState.CleanUpStructures();
            inputs = Console.ReadLine().Split(' ');
            int myA = int.Parse(inputs[0]);
            int myB = int.Parse(inputs[1]);
            int myC = int.Parse(inputs[2]);
            int myD = int.Parse(inputs[3]); // your protein stock
            gameState.Player1Proteins = (myA, myB, myC, myD);
            inputs = Console.ReadLine().Split(' ');
            int oppA = int.Parse(inputs[0]);
            int oppB = int.Parse(inputs[1]);
            int oppC = int.Parse(inputs[2]);
            int oppD = int.Parse(inputs[3]); // opponent's protein stock
            gameState.Player0Proteins = (oppA, oppB, oppC, oppD);
            int requiredActionsCount = int.Parse(Console.ReadLine()); // your number of organisms, output an action for each one in any order
            gameState.Player0PossibleMoves = gameState.GetPossibleActions(0);
            gameState.Player1PossibleMoves = gameState.GetPossibleActions(1);
            var actions = bot1.Evaluate(gameState);
            foreach(var action in actions)
            {
                Console.WriteLine(action.ToString());
            }
            
            //Debug.Log($"{Utils.globalWatch.ElapsedMilliseconds}\n");
            Utils.globalWatch.Reset();
        }
#endif
    }

#if EVO_MODE
    static void StartEvolution()
    {
        Random rng = new Random();
        int populationSize = 5;
        int generations = 100;
        Evolution.TrainBots(generations, populationSize, rng);
    }
#endif
}