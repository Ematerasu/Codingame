#define DEBUG_MODE
//#define EVO_MODE

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

public class GameState
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Entity[,] Grid;
    public (int A, int B, int C, int D) Player0Proteins;
    public (int A, int B, int C, int D) Player1Proteins;

    public Dictionary<int, Entity> Player0Entities;
    public Dictionary<int, Entity> Player1Entities;
    public List<Entity> Harvesters;
    public List<Entity> Tentacles;
    public List<Entity> Roots;
    public int Turn {get; private set; }

    public int OrganCnt;
    public bool IsGameOver { get; private set; }

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
        OrganCnt = 0;

        Player0Entities = new();
        Player1Entities = new();
        Harvesters = new();
        Tentacles = new();
        Roots = new();
        Turn = 0;
    }

    public void AddEntity((int x, int y) position, CellType type, int entityId, int ownerId, Direction dir, int parentId, int organRootId)
    {
        if (ownerId != -1)
        {
            OrganCnt = Math.Max(OrganCnt, entityId);
        }
        var current = Grid[position.x, position.y];
        if (current.Type == type && current.Id == entityId) 
        {
            current.IsUpToDate = true;
            return;
        }

        var newEntity = new Entity
        {
            Position = position,
            Type = type,
            OwnerId = ownerId,
            Dir = dir,
            Id = entityId,
            ParentId = parentId,
            OrganRootId = organRootId,
            IsUpToDate = true,
        };

        Grid[position.x, position.y] = newEntity;
        if (ownerId != -1)
            AddEntityToPlayer(newEntity, ownerId, parentId);
    }

    public void AddEntity(Action action, int playerId)
    {
        if (!IsPositionValid((action.X, action.Y)) || !IsValidSpotToGrow(Grid[action.X, action.Y].Type))
            return;
        var playerEntities = playerId == 0 ? Player0Entities : Player1Entities;
        if (!playerEntities.ContainsKey(action.Id))
            return;
        var entity = new Entity()
        {
            Position = (action.X, action.Y),
            Type = action.Type,
            OwnerId = playerId,
            Dir = action.Direction,
            ParentId = action.Id,
            Id = ++OrganCnt, 
        };

        entity.OrganRootId = action.Command == CommandEnum.SPORE ? 
                            action.Id : 
                            GetRootId(playerId, entity.ParentId);

        Grid[action.X, action.Y] = entity;
        AddEntityToPlayer(entity, playerId, entity.ParentId);
    }

    private int GetRootId(int ownerId, int parentId)
    {
        var playerEntities = ownerId == 0 ? Player0Entities : Player1Entities;

        if (!playerEntities.TryGetValue(parentId, out var parentEntity))
        {
            //Debug.Log($"Parent ID {parentId} not found for owner {ownerId}. Current entities: " +
                        //$"{string.Join(", ", playerEntities.Keys)}\n");
            throw new KeyNotFoundException($"Parent ID {parentId} not found for owner {ownerId}.");
        }

        return parentEntity.OrganRootId;
    }

    private void AddEntityToPlayer(Entity entity, int ownerId, int parentId)
    {
        var playerEntities = ownerId == 0 ? Player0Entities : Player1Entities;

        if (entity.Type != CellType.ROOT && !playerEntities.ContainsKey(parentId))
        {
            //Debug.Log($"Parent ID {parentId} not found for new entity {entity.Id}. " +
                       //$"Player entities: {string.Join(", ", playerEntities.Keys)}\n");
        throw new Exception($"Parent ID {parentId} not found for new entity {entity.Id}.\n");
        }

        //Debug.Log($"Adding entity {entity.Id} of type {entity.Type} for owner {ownerId} with parent {parentId}.\n");
        playerEntities[entity.Id] = entity;

        if (entity.Type != CellType.ROOT && playerEntities.TryGetValue(parentId, out var parentEntity))
        {
            parentEntity.ChildrenId.Add(entity.Id);
            //Debug.Log($"Parent entity {parentId} now has children: {string.Join(", ", parentEntity.ChildrenId)}\n");
        }

        if (entity.Type == CellType.HARVESTER)
        {
            Harvesters.Add(entity);
            //Debug.Log($"Added harvester {entity.Id} at position {entity.Position}\n");
        }
        else if (entity.Type == CellType.TENTACLE)
        {
            Tentacles.Add(entity);
            //Debug.Log($"Added tentacle {entity.Id} at position {entity.Position}\n");
        }
    }

    public void RemoveOrgan(Entity entity)
    {
        var entitiesToRemove = new List<Entity>();

        void CollectEntitiesToRemove(Entity currentEntity)
        {
            entitiesToRemove.Add(currentEntity);

            foreach (var childId in currentEntity.ChildrenId)
            {
                var ownerEntities = currentEntity.OwnerId == 0 ? Player0Entities : Player1Entities;
                if (ownerEntities.TryGetValue(childId, out var childEntity))
                {
                    CollectEntitiesToRemove(childEntity);
                }
                else
                {
                    //Debug.Log($"Child ID {childId} not found in global entity map during removal.\n");
                }
            }
        }

        CollectEntitiesToRemove(entity);

        foreach (var organ in entitiesToRemove)
        {
            var ownerEntities = organ.OwnerId == 0 ? Player0Entities : Player1Entities;

            if (ownerEntities.ContainsKey(organ.Id))
            {
                //Debug.Log($"Removing entity {organ.Id} from global map.\n");
                ownerEntities.Remove(organ.Id);
            }

            if (organ.Type == CellType.HARVESTER)
            {
                Harvesters.Remove(organ);
            }
            else if (organ.Type == CellType.TENTACLE)
            {
                Tentacles.Remove(organ);
            }

            if (organ.Type != CellType.ROOT && ownerEntities.TryGetValue(organ.ParentId, out var parentEntity))
            {
                //Debug.Log($"Removing child {organ.Id} from parent {parentEntity.Id}.\n");
                parentEntity.ChildrenId.Remove(organ.Id);
            }

            Grid[organ.Position.x, organ.Position.y] = new Entity { Position = organ.Position };

            //Debug.Log($"Removed Entity {organ}\n");
        }

    }

    public void CleanUpStructures()
    {
        List<Entity> toRemove = new();
        foreach(var kvp in Player0Entities)
        {
            if (!kvp.Value.IsUpToDate) toRemove.Add(kvp.Value);
        }
        foreach(var kvp in Player1Entities)
        {
            if (!kvp.Value.IsUpToDate) toRemove.Add(kvp.Value);
        }
        foreach(var organ in toRemove)
        {
            RemoveOrgan(organ);
        }
    }
    public List<List<Action>> GetPossibleActions(int playerId)
    {        
        Utils.GenerateMovesWatch.Start();
        var playerEntities = playerId == 0 ? Player0Entities : Player1Entities;
        var playerProteins = playerId == 0 ? Player0Proteins : Player1Proteins;

        var rootEntities = playerEntities
                .Where(kvp => kvp.Value.Type == CellType.ROOT)
                .Select(kvp => kvp.Value)
                .ToList();
        var possibleActionsPerRoot  = new List<List<Action>>();

        var directions = new (int x, int y, Direction dir)[] {
            (0, 1, Direction.S), 
            (1, 0, Direction.E), 
            (-1, 0, Direction.W), 
            (0, -1, Direction.N)
        };
        foreach (var root in rootEntities)
        {
            var rootActions = new List<Action>();

            var stack = new Stack<Entity>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var entity = stack.Pop();

                foreach (var direction in directions)
                {
                    (int x, int y) targetPos = (entity.Position.x + direction.x, entity.Position.y + direction.y);
                    if (!IsPositionValid(targetPos) || !IsValidSpotToGrow(Grid[targetPos.x, targetPos.y].Type))
                        continue;
                    if (playerProteins.A >= 1)
                    {
                        rootActions.Add(Action.GrowBasic(entity.Id, targetPos.x, targetPos.y));
                    }

                    if (playerProteins.C >= 1 && playerProteins.D >= 1)
                    {
                        foreach (var harvestDirection in directions)
                        {
                            (int x, int y) ttargetPos = (targetPos.x + harvestDirection.x, targetPos.y + harvestDirection.y); 
                            if (IsPositionValid(ttargetPos) && IsProtein(Grid[ttargetPos.x, ttargetPos.y].Type))
                                rootActions.Add(Action.GrowHarvester(entity.Id, targetPos.x, targetPos.y, harvestDirection.dir));
                        }
                    }

                    if (playerProteins.B >= 1 && playerProteins.C >= 1)
                    {
                        foreach (var tentacleDirection in directions)
                        {
                            (int x, int y) ttargetPos = (targetPos.x + tentacleDirection.x, targetPos.y + tentacleDirection.y); 
                            if (IsPositionValid(ttargetPos) && (Grid[ttargetPos.x, ttargetPos.y].Type == CellType.EMPTY || Grid[ttargetPos.x, ttargetPos.y].OwnerId == 1 - playerId))
                                rootActions.Add(Action.GrowTentacle(entity.Id, targetPos.x, targetPos.y, tentacleDirection.dir));
                        }
                    }

                    if (playerProteins.B >= 1 && playerProteins.D >= 1)
                    {
                        foreach (var sporerDirection in directions)
                        {
                            (int x, int y) ttargetPos = (targetPos.x + sporerDirection.x, targetPos.y + sporerDirection.y); 
                            if (IsPositionValid(ttargetPos) && (Grid[ttargetPos.x, ttargetPos.y].Type == CellType.EMPTY || IsProtein(Grid[ttargetPos.x, ttargetPos.y].Type)))
                                rootActions.Add(Action.GrowSporer(entity.Id, targetPos.x, targetPos.y, sporerDirection.dir));
                        }
                    }
                }

                if (entity.Type == CellType.SPORER)
                {
                    var sporeActions = GetSporeActions(entity, playerProteins);
                    rootActions.AddRange(sporeActions);
                }

                foreach (var childId in entity.ChildrenId)
                {
                    if (!playerEntities.ContainsKey(childId))
                    {
                        entity.ChildrenId.Remove(childId);
                        continue;
                    }

                    stack.Push(playerEntities[childId]);
                }
            }
            if (rootActions.Count == 0) rootActions.Add(Action.Wait());
            possibleActionsPerRoot.Add(rootActions);
        }
        Utils.GenerateMovesWatch.Stop();
        return CartesianProduct(possibleActionsPerRoot);
    }

    private List<Action> GetSporeActions(Entity sporer, (int A, int B, int C, int D) availableProteins)
    {
        var actions = new List<Action>();

        if (availableProteins.A < 1 || availableProteins.B < 1 || 
            availableProteins.C < 1 || availableProteins.D < 1)
        {
            //Debug.Log($"Not enough proteins for sporer {sporer.Id}. Available: {availableProteins}\n");
            return actions;
        }

        var currentPos = GetTargetPosition(sporer.Position, sporer.Dir);
        //Debug.Log($"Sporer {sporer.Id} starting spore actions from {sporer.Position} in direction {sporer.Dir}\n");

        while (IsPositionValid(currentPos) && IsValidSpotToGrow(Grid[currentPos.x, currentPos.y].Type))
        {
            //Debug.Log($"Adding spore action for position {currentPos}\n");
            actions.Add(Action.Spore(sporer.Id, currentPos.x, currentPos.y));
            currentPos = GetTargetPosition(currentPos, sporer.Dir);
        }

        return actions;
    }

    private List<List<Action>> CartesianProduct(List<List<Action>> possibleActionsPerRoot)
    {
        IEnumerable<IEnumerable<Action>> combinations = [Enumerable.Empty<Action>()];
        foreach (var list in possibleActionsPerRoot)
        {
            combinations = from combination in combinations
                        from action in list
                        select combination.Append(action);
        }
        return combinations.Select(c => c.ToList()).ToList();
    }

    public void ProcessTurn(List<Action> player0Actions, List<Action> player1Actions)
    {
        if (IsGameOver) return;

        Utils.GrowWatch.Start();
        Grow(player0Actions, player1Actions);
        Utils.GrowWatch.Stop();
        Utils.HarvestWatch.Start();
        Harvest();
        Utils.HarvestWatch.Stop();
        Utils.TentacleWatch.Start();
        TentacleAttack();
        Utils.TentacleWatch.Stop();
        Turn++;
        Utils.GameOverCheckWatch.Start();
        IsGameOver = CheckGameOver();
        Utils.GameOverCheckWatch.Stop();
    }

    private void Grow(List<Action> growActionsPlayer0, List<Action> growActionsPlayer1)
    {
        var actionMap = new Dictionary<(int x, int y), List<(Action action, int playerId)>>();

        foreach (var action in growActionsPlayer0)
        {
            if (action.Command == CommandEnum.WAIT) continue;
            var pos = (action.X, action.Y);
            if (!actionMap.ContainsKey(pos))
                actionMap[pos] = new List<(Action, int)>();
            actionMap[pos].Add((action, 0));
        }

        foreach (var action in growActionsPlayer1)
        {
            if (action.Command == CommandEnum.WAIT) continue;
            var pos = (action.X, action.Y);
            if (!actionMap.ContainsKey(pos))
                actionMap[pos] = new List<(Action, int)>();
            actionMap[pos].Add((action, 1));
        }

        foreach (var (position, actions) in actionMap)
        {
            if (actions.Count == 1)
            {
                var (action, playerId) = actions[0];
                ProcessSingleAction(action, playerId);
            }
            else
            {
                ProcessConflict(position, actions);
            }
        }
    }

    private void Harvest()
    {
        HashSet<(int x, int y, int playerId)> harvestedAlready = new();
        foreach(var harvester in Harvesters)
        {
            var targetPos = GetTargetPosition(harvester.Position, harvester.Dir);
            if (!IsPositionValid(targetPos))
                continue;
            var cellType = Grid[targetPos.x, targetPos.y].Type;
            if (IsProtein(cellType))
            {
                if(harvestedAlready.Contains((targetPos.x, targetPos.y, harvester.OwnerId)))
                    continue;
                harvestedAlready.Add((targetPos.x, targetPos.y, harvester.OwnerId));
                if (harvester.OwnerId == 0)
                {
                    switch(cellType)
                    {
                        case CellType.PROTEIN_A:
                            Player0Proteins.A++;
                            break;
                        case CellType.PROTEIN_B:
                            Player0Proteins.B++;
                            break;
                        case CellType.PROTEIN_C:
                            Player0Proteins.C++;
                            break;
                        case CellType.PROTEIN_D:
                            Player0Proteins.D++;
                            break;
                    }
                } else if (harvester.OwnerId == 1)
                {
                    switch(cellType)
                    {
                        case CellType.PROTEIN_A:
                            Player1Proteins.A++;
                            break;
                        case CellType.PROTEIN_B:
                            Player1Proteins.B++;
                            break;
                        case CellType.PROTEIN_C:
                            Player1Proteins.C++;
                            break;
                        case CellType.PROTEIN_D:
                            Player1Proteins.D++;
                            break;
                    }
                }
            }
        }
    }

    private void TentacleAttack()
    {
        var organsToKill = new List<Entity>();
        foreach(var tentacle in Tentacles)
        {
            var targetPos = GetTargetPosition(tentacle.Position, tentacle.Dir);
            if (!IsPositionValid(targetPos))
                continue;
            var target = Grid[targetPos.x, targetPos.y];
            if (target.OwnerId != 1 - tentacle.OwnerId)
                continue;
            organsToKill.Add(target);           
        }

        foreach(var organ in organsToKill)
        {
            RemoveOrgan(organ);
        }
    }

    private bool CheckGameOver() 
    {
        bool turnLimitReached = Turn >= 100;
        bool aPlayerHasDied = Player0Entities.Count <= 0 || Player1Entities.Count <= 0;
        bool noMoreSpace = true;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Grid[x, y].Type == CellType.EMPTY || IsProtein(Grid[x, y].Type))
                {
                    noMoreSpace = false;
                    break;
                }
                    
            }
            if (!noMoreSpace) break;
        }
        bool playersCantBuy = !CanBuyAnything(0) || !CanBuyAnything(1);
        return turnLimitReached || aPlayerHasDied || noMoreSpace || playersCantBuy;
    }

    public GameState Clone()
    {
        var newState = new GameState(Width, Height);

        // Tworzenie nowej siatki (Grid) i kopiowanie encji
        newState.Grid = new Entity[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var entity = Grid[x, y];
                if (entity != null)
                {
                    newState.Grid[x, y] = entity.Clone();
                }
            }
        }

        // Kopiowanie słowników graczy
        newState.Player0Entities = new Dictionary<int, Entity>();
        foreach (var kvp in Player0Entities)
        {
            newState.Player0Entities[kvp.Key] = kvp.Value.Clone();
        }

        newState.Player1Entities = new Dictionary<int, Entity>();
        foreach (var kvp in Player1Entities)
        {
            newState.Player1Entities[kvp.Key] = kvp.Value.Clone();
        }

        // Kopiowanie list zbieraczy i macek
        newState.Harvesters = new List<Entity>();
        foreach (var harvester in Harvesters)
        {
            newState.Harvesters.Add(harvester.Clone());
        }

        newState.Tentacles = new List<Entity>();
        foreach (var tentacle in Tentacles)
        {
            newState.Tentacles.Add(tentacle.Clone());
        }

        // Kopiowanie pozostałych atrybutów gry
        newState.Player0Proteins = Player0Proteins;
        newState.Player1Proteins = Player1Proteins;
        newState.Turn = Turn;
        newState.OrganCnt = OrganCnt;
        newState.IsGameOver = IsGameOver;

        return newState;
    }

    public bool IsProtein(CellType cell)
    {
        return cell == CellType.PROTEIN_A || cell == CellType.PROTEIN_B || cell == CellType.PROTEIN_C || cell == CellType.PROTEIN_D; 
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

    private (int x, int y) GetTargetPosition((int x, int y) position, Direction direction)
    {
        return direction switch
        {
            Direction.N => (position.x, position.y - 1),
            Direction.S => (position.x, position.y + 1),
            Direction.E => (position.x + 1, position.y),
            Direction.W => (position.x - 1, position.y),
            _ => position
        };
    }

    public bool IsPositionValid((int x, int y) position)
    {
        return position.x >= 0 && position.x < Width && position.y >= 0 && position.y < Height;
    }

    private void ProcessSingleAction(Action action, int playerId)
    {
        var playerEntities = playerId == 0 ? Player0Entities : Player1Entities;
        if (!playerEntities.ContainsKey(action.Id)) return;
        CellType currentCell = Grid[action.X, action.Y].Type;
        //  Debug.Log($"Processing action: {action.ToString()}\n");
        if (currentCell == CellType.EMPTY)
        {
            // Dodaj nowy organ na pustym polu
            AddEntity(action, playerId);
            ConsumeProteins(action.Type, playerId);
        }
        else if (IsProtein(currentCell))
        {
            AddEntity(action, playerId);
            ConsumeProteins(action.Type, playerId);
            CollectProteins(currentCell, playerId);
        }
        else
        {
            ConsumeProteins(action.Type, playerId);
            //Debug.Log($"Creating wall at {action.X}, {action.Y}\n");
            RemoveOrgan(Grid[action.X, action.Y]);
            Grid[action.X, action.Y] = new Entity { Position = (action.X, action.Y), Type = CellType.WALL };
        }
    }

    private void ProcessConflict((int x, int y) position, List<(Action action, int playerId)> actions)
    {
        CellType currentCell = Grid[position.x, position.y].Type;
        bool hasProtein = IsProtein(currentCell);

        var processedPlayers = new HashSet<int>();

        if (hasProtein)
        {
            foreach (var (_, playerId) in actions)
            {
                if (!processedPlayers.Contains(playerId))
                {
                    CollectProteins(currentCell, playerId);
                    processedPlayers.Add(playerId);
                }
            }
        }
        RemoveOrgan(Grid[position.x, position.y]);
        Grid[position.x, position.y] = new Entity { Position = position, Type = CellType.WALL };
    }

    private void CollectProteins(CellType proteinType, int playerId)
    {
        var proteins = playerId == 0 ? Player0Proteins : Player1Proteins;
        switch (proteinType)
        {
            case CellType.PROTEIN_A:
                proteins.A += 3;
                break;
            case CellType.PROTEIN_B:
                proteins.B += 3;
                break;
            case CellType.PROTEIN_C:
                proteins.C += 3;
                break;
            case CellType.PROTEIN_D:
                proteins.D += 3;
                break;
            default:
                throw new InvalidOperationException($"Invalid protein type: {proteinType}");
        }
        if (playerId == 0) Player0Proteins = proteins;
        else Player1Proteins = proteins;
    }

    private void ConsumeProteins(CellType type, int playerId)
    {
        var proteins = playerId == 0 ? Player0Proteins : Player1Proteins;
        switch (type)
        {
            case CellType.BASIC:
                proteins.A -= 1; // BASIC wymaga 1 białka A
                break;
            case CellType.HARVESTER:
                proteins.C -= 1; // HARVESTER wymaga 1 białka C
                proteins.D -= 1; // i 1 białka D
                break;
            case CellType.TENTACLE:
                proteins.B -= 1; // TENTACLE wymaga 1 białka B
                proteins.C -= 1; // i 1 białka C
                break;
            case CellType.SPORER:
                proteins.B -= 1; // SPORER wymaga 1 białka B
                proteins.D -= 1; // i 1 białka D
                break;
            case CellType.ROOT:
                proteins.A -= 1;
                proteins.B -= 1; 
                proteins.C -= 1;
                proteins.D -= 1;
                break;
            default:
                throw new InvalidOperationException($"Invalid organ type: {type}");
        }
        if (playerId == 0) Player0Proteins = proteins;
        else Player1Proteins = proteins;
    }

    private bool IsValidSpotToGrow(CellType cellType)
    {
        return cellType == CellType.EMPTY || IsProtein(cellType);
    }

    private bool CanBuyAnything(int playerId)
    {
        var proteins = playerId == 0 ? Player0Proteins : Player1Proteins;

        return (proteins.A > 0) ||
                (proteins.C > 0 && proteins.D > 0) ||
                (proteins.B > 0 && proteins.C > 0) ||
                (proteins.B > 0 && proteins.D > 0) ||
                (proteins.A > 0 && proteins.B > 0 && proteins.C > 0 && proteins.D > 0);

    }

    public int GetWinner()
    {
        var player0Cnt = 0;
        var player1Cnt = 0;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Grid[x, y].OwnerId == 0)
                {
                    player0Cnt++;
                }
                else if (Grid[x, y].OwnerId == 1)
                {
                    player1Cnt++;
                }
                    
            }
        }

        if (player0Cnt > player1Cnt) return 0;
        if (player1Cnt > player0Cnt) return 1;
        return 2;
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
#if EVO_MODE
        StartEvolution();
        return;
#endif
        GameState gameState;

#if DEBUG_MODE
        //Console.WriteLine("DEBUG_MODE is ON. Generating GameState using GameStateGenerator...");
        var random = new Random();
        var bot1 = new RandomBot(0);
        var bot2 = new HeuristicBot(1);
        var results = new int[3] { 0, 0, 0};
        Utils.globalWatch.Restart();
        for(int i = 0; i < 100; i++)
        {
            Console.WriteLine($"Game: {i+1}\n");
            gameState = GameStateGenerator.GenerateGameState(random);
            //Console.WriteLine($"Generated GameState: Width = {gameState.Width}, Height = {gameState.Height}");
            //PrintDebugState(gameState);
            //Console.WriteLine();
            while(!gameState.IsGameOver)
            {
                Utils.watch.Restart();
                var actions = bot1.Evaluate(gameState);
                Utils.watch.Restart();
                var actions1 = bot2.Evaluate(gameState);
                //Console.WriteLine($"Action for player 0: {actions[0].ToString()}");
                //Console.WriteLine($"Action for player 1: {actions1[0].ToString()}");
                gameState.ProcessTurn(actions, actions1);
                //Helpers.PrintDebugState(gameState);
                Utils.watch.Reset();
            }
            results[gameState.GetWinner()]++;
        }
        Console.WriteLine($"Elapsed: {Utils.globalWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"Generate moves time: {Utils.GenerateMovesWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"Grow time: {Utils.GrowWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"Harvester time: {Utils.HarvestWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"Tentacle time: {Utils.TentacleWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"GameOverCheck time: {Utils.GameOverCheckWatch.ElapsedMilliseconds}ms\n");
        Console.WriteLine($"Results: {results[0]} {results[1]} {results[2]} \n");
        

#else
        GC.TryStartNoGCRegion(NOGC_SIZE); // true
        var bot1 = new HeuristicBot(1);
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