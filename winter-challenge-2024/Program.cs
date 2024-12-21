using System;
using System.Collections;
using System.Collections.Generic;



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

public struct Cell
{
    public CellType Type { get; set; } = CellType.EMPTY;
    public int Owner { get; set; } = -1;
    public int OrganId { get; set; } = -1;

    public Cell() { }
    public Cell(CellType type, int owner, int organId) 
    {
        Type = type;
        Owner = owner;
        OrganId = organId;
    }
}

public class Organ
{
    public int Id;
    public int OwnerId;
    public CellType Type;
    public (int x, int y) Position;
    public int ParentId;
    public int[] Children = new int[4]; // array containing children ids
    public int ChildrenCnt;

}
public class GameState
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Cell[,] Grid;

    // Organ storage
    private Organ[] Organs; // Fixed-size array for organs
    private int OrganCount; // Current number of organs

    // precomputed lookups
    private Organ[] BasicOrgans;
    private int BasicOrgansCnt;
    private Organ[] HarvesterOrgans;
    private int HarvesterOrgansCnt;
    private Organ[] TentacleOrgans;
    private int TentacleOrgansCnt;
    private Organ[] SporerOrgans;
    private int SporerOrgansCnt;
    private Organ[] RootOrgans;
    private int RootOrgansCnt;
    

    // Player-organ mapping
    private int[,] PlayerOrganMap; // [playerId, index] -> organId
    private int[] PlayerOrganCount; // Current count of organs for each player

    // Maximum allowed organs per player
    private const int MaxOrgansPerPlayer = 256;

    public GameState(int width, int height, int maxOrgans = 512, int maxPlayers = 2)
    {
        Width = width;
        Height = height;
        Grid = new Cell[width, height];
        Organs = new Organ[maxOrgans];
        OrganCount = 0;
        PlayerOrganMap = new int[maxPlayers, MaxOrgansPerPlayer];
        PlayerOrganCount = new int[maxPlayers];
        BasicOrgans = new Organ[maxOrgans];
        HarvesterOrgans = new Organ[maxOrgans];
        TentacleOrgans = new Organ[maxOrgans];
        SporerOrgans = new Organ[maxOrgans];
        RootOrgans = new Organ[maxOrgans];
        BasicOrgansCnt = 0;
        HarvesterOrgansCnt = 0;
        TentacleOrgansCnt = 0;
        SporerOrgansCnt = 0;
        RootOrgansCnt = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Grid[x, y] = new Cell();
            }
        }
    }

    public void AddOrgan(int ownerId, CellType type, (int x, int y) position, int parentId = -1)
    {
        if (OrganCount >= Organs.Length)
            throw new InvalidOperationException("Max number of organs reached.");

        var organ = new Organ
        {
            Id = OrganCount,
            OwnerId = ownerId,
            Type = type,
            Position = position,
            ParentId = parentId,
            Children = new int[4],
            ChildrenCnt = 0,
        };

        Organs[OrganCount] = organ;

        // Link to player
        if (PlayerOrganCount[ownerId] >= MaxOrgansPerPlayer)
            throw new InvalidOperationException("Max organs per player reached.");

        PlayerOrganMap[ownerId, PlayerOrganCount[ownerId]] = OrganCount;
        PlayerOrganCount[ownerId]++;

        // Update parent
        if (parentId != -1)
        {
            var parent = Organs[parentId];
            parent.Children[++parent.ChildrenCnt] = organ.Id; // Add this organ as a child
        }

        // Update grid
        Grid[position.x, position.y] = new Cell(type, ownerId, OrganCount);

        OrganCount++;
    }

    public void RemoveOrgan(int organId)
    {
        if (organId < 0 || organId >= OrganCount)
            return;

        var organ = Organs[organId];
        var ownerId = organ.OwnerId;

        // Remove from player mapping
        for (int i = 0; i < PlayerOrganCount[ownerId]; i++)
        {
            if (PlayerOrganMap[ownerId, i] == organId)
            {
                PlayerOrganMap[ownerId, i] = PlayerOrganMap[ownerId, PlayerOrganCount[ownerId] - 1];
                PlayerOrganCount[ownerId]--;
                break;
            }
        }

        for (int i = 0; i < organ.ChildrenCnt; i++)
        {
            RemoveOrgan(organ.Children[i]);
            organ.Children[i] = 0;
        }


        // Clear organ
        Organs[organId] = null;

        // Update grid
        Grid[organ.Position.x, organ.Position.y] = new Cell();
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
        for (int i = 0; i < OrganCount; i++)
        {
            var organ = Organs[i];
            if (organ.Type == CellType.ROOT || organ.Type == CellType.BASIC)
            {
                // Example grow logic
                var newPos = (organ.Position.x + 1, organ.Position.y); // Dummy position
                AddOrgan(organ.OwnerId, CellType.BASIC, newPos, organ.Id);
            }
        }
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
        float score = 0;

        for (int i = 0; i < OrganCount; i++)
        {
            var organ = Organs[i];
            if (organ.Type == CellType.HARVESTER)
                score += 10;
        }

        return score;
    }

    private GameState Clone()
    {
        var newState = new GameState(Width, Height, Organs.Length);

        Array.Copy(Grid, newState.Grid, Grid.Length);
        Array.Copy(Organs, newState.Organs, Organs.Length);
        Array.Copy(PlayerOrganMap, newState.PlayerOrganMap, PlayerOrganMap.Length);
        Array.Copy(PlayerOrganCount, newState.PlayerOrganCount, PlayerOrganCount.Length);

        newState.OrganCount = OrganCount;

        return newState;
    }

    private static int BitScan(int mask)
    {
        // Find the index of the first set bit (LSB)
        return (int)Math.Log2(mask & -mask);
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
}

/**
    * Grow and multiply your organisms to end up larger than your opponent.
**/
class MainClass
{
    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]); // columns in the game grid
        int height = int.Parse(inputs[1]); // rows in the game grid
        GameState gameState = new GameState(width, height);
        // game loop
        while (true)
        {
            int entityCount = int.Parse(Console.ReadLine());
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
                //gameState.UpdateCell(x, y, type, owner, organId, organParentId, organRootId);
            }
            inputs = Console.ReadLine().Split(' ');
            int myA = int.Parse(inputs[0]);
            int myB = int.Parse(inputs[1]);
            int myC = int.Parse(inputs[2]);
            int myD = int.Parse(inputs[3]); // your protein stock
            inputs = Console.ReadLine().Split(' ');
            int oppA = int.Parse(inputs[0]);
            int oppB = int.Parse(inputs[1]);
            int oppC = int.Parse(inputs[2]);
            int oppD = int.Parse(inputs[3]); // opponent's protein stock
            int requiredActionsCount = int.Parse(Console.ReadLine()); // your number of organisms, output an action for each one in any order

        }
    }
}