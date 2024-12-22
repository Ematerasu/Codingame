#define DEBUG_MODE

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace winter_challenge_2024;

public class Debug
{
    public static void Log(string msg)
    {
        Console.Error.Write(msg);
    }
}

public enum CommandEnum : byte
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
    public int ParentId { get; set; } = 0;
    public List<int> ChildrenId = new List<int>(4);
    public int OrganRootId { get; set; } = 0;
    public (int x, int y) Position { get; set; } = (0, 0);

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
            ChildrenId = this.ChildrenId,
            OrganRootId = this.OrganRootId,
            Position = this.Position
        };
    }
}

public class GameState
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Entity[,] Grid;
    public (int A, int B, int C, int D) Player0Proteins;
    public (int A, int B, int C, int D) Player1Proteins;

    public Dictionary<int, Entity> Player0Entities;
    public Dictionary<int, Entity> Player1Entities;

    public int OrganCnt;

    public GameState(int width, int height)
    {
        Width = width;
        Height = height;
        Grid = new Entity[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Grid[x, y] = new Entity() { Position = (x, y) };
            }
        }
        Player0Proteins = (0, 0, 0, 0);
        Player1Proteins = (0, 0, 0, 0);

        Player0Entities = new();
        Player1Entities = new();
    }



    public void AddEntity((int x, int y) position, CellType type, int ownerId, int organId, Direction dir, int parentId, int rootId)
    {
        var currEntity = Grid[position.x, position.y];
        if (currEntity.Id == organId && currEntity.Type == type)
            return;
        var entity = new Entity();
        entity.OwnerId = ownerId;
        entity.ParentId = parentId;
        entity.Position = position;
        entity.Type = type;
        entity.Dir = dir;
        entity.Id = organId;
        entity.OrganRootId = rootId;

        Grid[position.x, position.y] = entity;
        if (ownerId == 0)
        {
            //Debug.Log($"{position} {type} {organId} {parentId}\n");
            Player0Entities.Add(organId, entity);
            if (type != CellType.ROOT)
                Player0Entities[parentId].ChildrenId.Add(organId);
        }
        else if (ownerId == 1)
        {
            Player1Entities.Add(organId, entity);
            if (type != CellType.ROOT)
                Player1Entities[parentId].ChildrenId.Add(organId);
        }
    }

    public void RemoveOrgan(Entity entity)
    {
        var organId = entity.Id;
        if (entity.OwnerId == 0)
        {
            Player0Entities.Remove(organId);
            if (entity.Type != CellType.ROOT)
                Player0Entities[entity.ParentId].ChildrenId.Remove(organId);
            foreach (var child in entity.ChildrenId)
            {
                RemoveOrgan(Player0Entities[child]);
            }
        }
        else if (entity.OwnerId == 1)
        {
            Player1Entities.Remove(organId);
            if (entity.Type != CellType.ROOT)
                Player1Entities[entity.ParentId].ChildrenId.Remove(organId);
            foreach(var child in entity.ChildrenId)
            {
                RemoveOrgan(Player1Entities[child]);
            }
        }
        Grid[entity.Position.x, entity.Position.y] = new Entity();
    }

    public GameState Simulate(int rounds)
    {
        var newState = Clone();

        for (int i = 0; i < rounds; i++)
        {
            newState.ProcessTurn();
        }

        return newState;
    }

    private void ProcessTurn()
    {
        Grow();
        Spore();
        Harvest();
        TentacleAttack();
        CheckGameOver();
    }

    private void Grow()
    {
        // TODO
    }

    private void Spore()
    {
        // Example spore logic
    }

    private void Harvest()
    {
        // Example harvest logic
    }

    private void TentacleAttack()
    {
        // Example attack logic
    }

    private void CheckGameOver()
    {
        // Example game over logic
    }

    public float Evaluate()
    {
        // TODO
        return 0f;
    }

    public GameState Clone()
    {
        var newState = new GameState(Width, Height);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var entity = Grid[x, y];
                newState.AddEntity(entity.Position, entity.Type, entity.OwnerId, entity.Id, entity.Dir, entity.ParentId, entity.OrganRootId);
            }
        }
        newState.Player0Proteins = Player0Proteins;
        newState.Player1Proteins = Player1Proteins;
        return newState;
    }

    public void Print()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var cellType = Grid[x, y].Type;
                Console.Error.Write(Utils.CellTypeToString(cellType));
            }
            Console.Error.WriteLine();
        }
        Console.Error.WriteLine();
    }
}

public struct Action
{
    public CommandEnum Command;
    public int Id;
    public int X;
    public int Y;
    public CellType Type;
    public Direction Direction;

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
            Type = CellType.TENTACLE,
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


}
public class Utils
{
    public static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();
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
    static void Main(string[] args)
    {
        GameState gameState;

#if DEBUG_MODE
        Console.WriteLine("DEBUG_MODE is ON. Generating GameState using GameStateGenerator...");
        var random = new Random();
        gameState = GameStateGenerator.GenerateGameState(random);
        PrintDebugState(gameState);
        Console.WriteLine();
        var cloned = gameState.Clone();
        PrintDebugState(cloned);
#else
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
                    (x, y), Utils.StringToCellType(type), owner, organId, Utils.StringToDirection(organDir), organParentId, organRootId
                );
            }
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
            gameState.Player1Proteins = (oppA, oppB, oppC, oppD);
            int requiredActionsCount = int.Parse(Console.ReadLine()); // your number of organisms, output an action for each one in any order
            var cloned = gameState.Clone();
            cloned.Print();
            Console.WriteLine("WAIT");
            Debug.Log($"{Utils.globalWatch.ElapsedMilliseconds}\n");
            Utils.globalWatch.Reset();
        }
#endif
    }

#if DEBUG_MODE
    private static void PrintDebugState(GameState gameState)
    {
        Console.WriteLine($"Generated GameState: Width = {gameState.Width}, Height = {gameState.Height}");
        for (int y = 0; y < gameState.Height; y++)
        {
            for (int x = 0; x < gameState.Width; x++)
            {
                var cell = gameState.Grid[x, y];
                Console.Write(cell.Type == CellType.EMPTY ? "." : cell.Type.ToString()[0]); // Print first letter of CellType or '.' for EMPTY
            }
            Console.WriteLine();
        }

        Console.WriteLine("Player 0 Entities:");
        foreach (var entity in gameState.Player0Entities.Values)
        {
            Console.WriteLine($"Entity ID: {entity.Id}, Type: {entity.Type}, Position: {entity.Position}");
        }

        Console.WriteLine("Player 1 Entities:");
        foreach (var entity in gameState.Player1Entities.Values)
        {
            Console.WriteLine($"Entity ID: {entity.Id}, Type: {entity.Type}, Position: {entity.Position}");
        }
    }
#endif
}