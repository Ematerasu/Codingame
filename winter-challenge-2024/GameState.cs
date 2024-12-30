using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace winter_challenge_2024;

public struct FieldData
{
    private byte _metadata;   // Bajt opisujący ściany, surowce, organy itd.
    private byte _harvesters; // Bajt opisujący liczbę harvesterów
    private byte _tentacles;  // Bajt opisujący liczbę tentacli
    private UInt16 _neighbours; // Bajt opisujacy sąsiednie cellki

    public int OrganId { get; set; } // Unikalny identyfikator organu
    public int RootId { get; set; }  // ID Roota, do którego należy organ
    public int ParentId { get; set; }  // ID Parenta, do którego należy organ

    public FieldData()
    {
        _metadata = 0;
        _harvesters = 0;
        _tentacles = 0;
        _neighbours = 0;
    }

    // Bajt 1: Metadata
    public bool IsEmpty => _metadata == 0;
    public bool IsWall => (_metadata & 0b00000001) != 0;
    public bool IsResource => (_metadata & 0b00000010) != 0;
    public bool IsOrgan => (_metadata & 0b00000100) != 0;
    public bool IsOwnedByMe => (_metadata & 0b00001000) != 0;
    public int Owner => (_metadata & 0b00001000) >> 3;

    public int OrganOrResourceType => (_metadata & 0b00110000) >> 4; // TT
    public int Rotation => (_metadata & 0b11000000) >> 6; // RR

    // Bajt 2: Harvesters
    public int MyHarvesters => _harvesters & 0b00001111; // MMMM Harvesting from SWNE
    public int OpponentHarvesters => (_harvesters & 0b11110000) >> 4; // OOOO Harvesting from SWNE

    // Bajt 3: Tentacles
    public int MyTentacles => _tentacles & 0b00001111; // MMMM Attacking from ENWS
    public int OpponentTentacles => (_tentacles & 0b11110000) >> 4; // OOOO Attacking from ENWS
    
    // Bajt 4: Neighbour Info
    public bool ProteinNorth => (_neighbours & 0b000000000001) != 0;
    public bool ProteinEast => (_neighbours & 0b000000000010) != 0;
    public bool ProteinSouth => (_neighbours & 0b000000000100) != 0;
    public bool ProteinWest => (_neighbours & 0b000000001000) != 0;
    public bool EnemyNorth => (_neighbours & 0b000000010000) != 0;
    public bool EnemyEast => (_neighbours & 0b000000100000) != 0;
    public bool EnemySouth => (_neighbours & 0b000001000000) != 0;
    public bool EnemyWest => (_neighbours & 0b000010000000) != 0;
    public bool MeNorth => (_neighbours & 0b000100000000) != 0;
    public bool MeEast => (_neighbours & 0b001000000000) != 0;
    public bool MeSouth => (_neighbours & 0b010000000000) != 0;
    public bool MeWest => (_neighbours & 0b100000000000) != 0;
    // Set values
    public void SetMetadata(CellType type, Direction direction, int owner)
    {
        // Ustawianie podstawowych właściwości
        _metadata = 0;

        // Ustawienie, czy pole jest ścianą
        if (type == CellType.WALL)
            _metadata |= 0b00000001;
        else if (type == CellType.PROTEIN_A || type == CellType.PROTEIN_B ||
            type == CellType.PROTEIN_C || type == CellType.PROTEIN_D)
        {
            _metadata |= 0b00000010;
        }
        else if (type == CellType.ROOT || type == CellType.BASIC ||
            type == CellType.HARVESTER || type == CellType.TENTACLE ||
            type == CellType.SPORER)
        {
            _metadata |= 0b00000100;
        }

        // Ustawienie właściciela organu
        if (owner == 1) // Gracz
            _metadata |= 0b00001000;

        // Ustawienie typu organu lub surowca (TT)
        int typeBits = type switch
        {
            CellType.ROOT => 0b00,
            CellType.BASIC => 0b00, // ROOT i BASIC dzielą typ, różnią się rotacją
            CellType.HARVESTER => 0b01,
            CellType.TENTACLE => 0b10,
            CellType.SPORER => 0b11,
            CellType.PROTEIN_A => 0b00,
            CellType.PROTEIN_B => 0b01,
            CellType.PROTEIN_C => 0b10,
            CellType.PROTEIN_D => 0b11,
            _ => 0b00 // Domyślnie
        };

        _metadata |= (byte)(typeBits << 4);

        // Ustawienie rotacji organu (RR)
        int rotationBits = direction switch
        {
            Direction.N => 0b00,
            Direction.E => 0b01,
            Direction.S => 0b10,
            Direction.W => 0b11,
            _ => 0b00 // Brak rotacji (X lub typy bez kierunku, np. ROOT)
        };

        if (type == CellType.ROOT)
            rotationBits = 0b00; // ROOT zawsze ma rotację 0
        else if (type == CellType.BASIC)
            rotationBits = 0b11; // BASIC ma zawsze rotację 3 (00 w TT, różnica w RR)

        _metadata |= (byte)(rotationBits << 6);
    }

    public void SetHarvestingInfo(Direction direction, int owner)
    {
        int shift = owner == 1 ? 0 : 4;
        switch(direction)
        {
            case Direction.N:
                _harvesters |= (byte)(0b1 << shift);
                break;
            case Direction.E:
                _harvesters |= (byte)(0b1 << shift+1);
                break;
            case Direction.S:
                _harvesters |= (byte)(0b1 << shift+2);
                break;
            case Direction.W:
                _harvesters |= (byte)(0b1 << shift+3);
                break;
        }
    }
    public void ClearHarvestingInfo(Direction direction, int owner)
    {
        int shift = owner == 1 ? 0 : 4;
        switch(direction)
        {
            case Direction.N:
                _harvesters &= (byte)~(0b1 << shift);
                break;
            case Direction.E:
                _harvesters &= (byte)~(0b1 << shift+1);
                break;
            case Direction.S:
                _harvesters &= (byte)~(0b1 << shift+2);
                break;
            case Direction.W:
                _harvesters &= (byte)~(0b1 << shift+3);
                break;
        }
    }    
    public void SetTentacleInfo(Direction direction, int owner)
    {
        int shift = owner == 1 ? 0 : 4;
        switch(direction)
        {
            case Direction.N:
                _tentacles |= (byte)(0b1 << shift);
                break;
            case Direction.E:
                _tentacles |= (byte)(0b1 << shift+1);
                break;
            case Direction.S:
                _tentacles |= (byte)(0b1 << shift+2);
                break;
            case Direction.W:
                _tentacles |= (byte)(0b1 << shift+3);
                break;
        }
    }    
    public void ClearTentacleInfo(Direction direction, int owner)
    {
        int shift = owner == 1 ? 0 : 4;
        switch(direction)
        {
            case Direction.S:
                _tentacles &= (byte)~(0b1 << shift);
                break;
            case Direction.W:
                _tentacles &= (byte)~(0b1 << shift+1);
                break;
            case Direction.N:
                _tentacles &= (byte)~(0b1 << shift+2);
                break;
            case Direction.E:
                _tentacles &= (byte)~(0b1 << shift+3);
                break;
        }
    }  
    public void SetProteinInfo(Direction direction)
    {
        byte mask = 0;
        switch (direction)
        {
            case Direction.S:
                mask = 0b00000001;
                break;
            case Direction.W:
                mask = 0b00000010;
                break;
            case Direction.N:
                mask = 0b00000100;
                break;
            case Direction.E:
                mask = 0b00001000;
                break;
        }
        _neighbours |= mask;
    }

    public void SetEnemyInfo(Direction direction)
    {
        byte mask = 0;
        switch (direction)
        {
            case Direction.S:
                mask = 0b00000001;
                break;
            case Direction.W:
                mask = 0b00000010;
                break;
            case Direction.N:
                mask = 0b00000100;
                break;
            case Direction.E:
                mask = 0b00001000;
                break;
        }
        _neighbours |= (byte)(mask << 4);
    }

    public void SetMeInfo(Direction direction)
    {
        byte mask = 0;
        switch (direction)
        {
            case Direction.S:
                mask = 0b00000001;
                break;
            case Direction.W:
                mask = 0b00000010;
                break;
            case Direction.N:
                mask = 0b00000100;
                break;
            case Direction.E:
                mask = 0b00001000;
                break;
        }
        _neighbours |= (byte)(mask << 8);
    }

    public void ClearProteinInfo(Direction direction)
    {
        byte mask = 0;
        switch (direction)
        {
            case Direction.S:
                mask = 0b00000001;
                break;
            case Direction.W:
                mask = 0b00000010;
                break;
            case Direction.N:
                mask = 0b00000100;
                break;
            case Direction.E:
                mask = 0b00001000;
                break;
        }
        _neighbours &= (byte)~mask;
    }


    public void ClearEnemyInfo(Direction direction)
    {
        byte mask = 0;
        switch (direction)
        {
            case Direction.S:
                mask = 0b00000001;
                break;
            case Direction.W:
                mask = 0b00000010;
                break;
            case Direction.N:
                mask = 0b00000100;
                break;
            case Direction.E:
                mask = 0b00001000;
                break;
        }
        _neighbours &= (byte)~(mask << 4);
    }


    public void Reset()
    {
        _metadata = 0;
        OrganId = 0;
        RootId = 0;
        ParentId = 0;
    }

    public override string ToString()
    {
        if (IsWall) return "W";
        if (IsResource)
        {
            switch(OrganOrResourceType)
            {
                case 0:
                    return "A";
                case 1:
                    return "B";
                case 2:
                    return "C";
                case 3:
                    return "D";
            }
        }
        if (IsOrgan)
        {
            switch(OrganOrResourceType)
            {
                case 0:
                    return Rotation == 0 ? "r" : "b";
                case 1:
                    return "h";
                case 2:
                    return "t";
                case 3:
                    return "s";
            }
        }
        return ".";
    }
}

public class NewGameState
{
    public int Width { get; }
    public int Height { get; }
    public FieldData[,] Board { get; }
    public int[] PlayerProteins { get; set; }
    public int[] OpponentProteins { get; set; }

    public Dictionary<int, (int x, int y)> IdToPosition = new();
    private Dictionary<int, List<int>> childrenMap = new(); // Map Parent -> Children
    
    private int[] roots;

    public int Turn { get; private set; }
    public int OrganCnt;
    public bool IsGameOver { get; private set; }

    public NewGameState(int width, int height)
    {
        Width = width;
        Height = height;
        Board = new FieldData[width, height];
        PlayerProteins = [0, 0, 0, 0];
        OpponentProteins = [0, 0, 0, 0];
        roots = [0, 0];
        Turn = 1;
        OrganCnt = 0;
    }

    public NewGameState Clone()
    {
        var clonedState = new NewGameState(Width, Height);

        // Clone Board
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                clonedState.Board[x, y] = Board[x, y];
            }
        }

        clonedState.PlayerProteins = (int[])PlayerProteins.Clone();
        clonedState.OpponentProteins = (int[])OpponentProteins.Clone();
        clonedState.roots = (int[])roots.Clone();

        clonedState.IdToPosition = new Dictionary<int, (int x, int y)>(IdToPosition);
        clonedState.childrenMap = new Dictionary<int, List<int>>(childrenMap.Count);
        foreach (var kvp in childrenMap)
        {
            clonedState.childrenMap[kvp.Key] = new List<int>(kvp.Value);
        }

        clonedState.Turn = Turn;
        clonedState.OrganCnt = OrganCnt;

        return clonedState;
    }

    public void AddEntity(int x, int y, int organId, int parentId, int rootId, CellType type, Direction direction, int owner)
    {
        OrganCnt = Math.Max(OrganCnt, organId);
        Board[x, y].Reset();
        Board[x, y].OrganId = organId;
        Board[x, y].RootId = rootId;
        Board[x, y].ParentId = parentId;

        Board[x, y].SetMetadata(type, direction, owner);
        if (type == CellType.HARVESTER)
        {
            var vec = Utils.DirToVector(direction);
            if (IsValidPos(x+vec.nx, y+vec.ny))
                Board[x+vec.nx, y+vec.ny].SetHarvestingInfo(direction, owner);
        }
        else if (type == CellType.TENTACLE)
        {
            var vec = Utils.DirToVector(direction);
            if (IsValidPos(x+vec.nx, y+vec.ny))
                Board[x+vec.nx, y+vec.ny].SetTentacleInfo(direction, owner);
        }

        if (organId != 0)
        {
            IdToPosition[organId] = (x, y);
            childrenMap[organId] = new List<int>(4);
            if (type != CellType.ROOT)
            {
                childrenMap[parentId].Add(organId);
            }
            else
                roots[owner]++;
        }

        AddNeighbourInfo(x, y, type, owner);
    }

    public void RemoveOrgan(int x, int y)
    {
        var field = Board[x, y];
        if (field.IsOrgan && field.OrganOrResourceType == 0b01) // Harvester
        {
            switch(field.Rotation)
            {
                case 0b00:
                    if (y-1 >= 0)
                        Board[x, y-1].ClearHarvestingInfo(Direction.N, field.Owner);
                    break;
                case 0b01:
                    if (x+1 < Width)
                        Board[x+1, y].ClearHarvestingInfo(Direction.E, field.Owner);
                    break;
                case 0b10:
                    if (y+1 < Height)
                        Board[x, y+1].ClearHarvestingInfo(Direction.S, field.Owner);
                    break;
                case 0b11:
                    if (x-1 >= 0)
                        Board[x-1, y].ClearHarvestingInfo(Direction.W, field.Owner);
                    break;
            }
        }
        else if (field.IsOrgan && field.OrganOrResourceType == 0b10) // Tentacle
        {
            switch(field.Rotation)
            {
                case 0b00:
                    if (y-1 >= 0)
                        Board[x, y-1].ClearTentacleInfo(Direction.N, field.Owner);
                    break;
                case 0b01:
                    if (x+1 < Width)
                        Board[x+1, y].ClearTentacleInfo(Direction.E, field.Owner);
                    break;
                case 0b10:
                    if (y+1 < Height)
                        Board[x, y+1].ClearTentacleInfo(Direction.S, field.Owner);
                    break;
                case 0b11:
                    if (x-1 >= 0)
                        Board[x-1, y].ClearTentacleInfo(Direction.W, field.Owner);
                    break;
            }
        }
        var organId = field.OrganId;
        var childrenIds = childrenMap[organId].ToArray();
        foreach(var childId in childrenIds)
        {
            var position = IdToPosition[childId];
            RemoveOrgan(position.x, position.y);
        }
        if (field.OrganOrResourceType == 0b00 && field.Rotation == 0b00)
            roots[field.Owner]++;
        var parentId = field.ParentId;
        IdToPosition.Remove(organId);
        childrenMap[parentId].Remove(organId);
        Board[x, y].Reset();
        ClearNeighbourInfo(x, y, false);
    }

    public void ProcessTurn(Action[] playerCommands, Action[] opponentCommands)
    {
        if (IsGameOver) return;
        Utils.GrowWatch.Start();
        ProcessGrowth(playerCommands, opponentCommands);
        Utils.GrowWatch.Stop();
        Utils.HarvestWatch.Start();
        ProcessResources();
        Utils.HarvestWatch.Stop();
        Utils.TentacleWatch.Start();
        ProcessAttacks();
        Utils.TentacleWatch.Stop();
        Utils.GameOverCheckWatch.Start();
        IsGameOver = CheckGameEnd();
        Utils.GameOverCheckWatch.Stop();
        Turn++;
    }

    public List<Action[]> GenerateActions(int playerId)
    {
        Utils.GenerateMovesWatch.Start();
        List<int> rootsMap = new();
        List<Action>[] allRootsActions = Enumerable.Range(0, roots[playerId])
                                            .Select(_ => new List<Action>() {Action.Wait()})
                                            .ToArray();
        var directions = new (int x, int y, Direction from)[] {
            (0, 1, Direction.N), 
            (1, 0, Direction.W), 
            (-1, 0, Direction.E), 
            (0, -1, Direction.S)
        };

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var field = Board[x, y];
                if (!field.IsOrgan || field.Owner != playerId) continue;
                if (!rootsMap.Contains(field.RootId)) rootsMap.Add(field.RootId);
                var rootIdx = rootsMap.IndexOf(field.RootId);
                foreach(var direction in directions)
                {
                    if (!IsValidPos(x + direction.x, y + direction.y)) continue;

                    var neighbor = Board[x + direction.x, y + direction.y];
                    if (neighbor.IsOrgan && neighbor.Owner == playerId) continue;
                    if (neighbor.IsWall) continue;
                    allRootsActions[rootIdx].Add(Action.GrowBasic(field.OrganId, x + direction.x, y + direction.y));
                    switch(direction.from)
                    {
                        case Direction.N:
                            if (neighbor.ProteinEast) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if (neighbor.ProteinSouth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if (neighbor.ProteinWest) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if ((neighbor.EnemyEast && playerId == 1) || (neighbor.MeEast && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if ((neighbor.EnemySouth && playerId == 1) || (neighbor.MeSouth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if ((neighbor.EnemyWest && playerId == 1) || (neighbor.MeWest && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, 1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (-1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            }
                            break;
                        case Direction.W:
                            if (neighbor.ProteinEast) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if (neighbor.ProteinSouth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if (neighbor.ProteinNorth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if ((neighbor.EnemyEast && playerId == 1) || (neighbor.MeEast && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if ((neighbor.EnemySouth && playerId == 1) || (neighbor.MeSouth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if ((neighbor.EnemyNorth && playerId == 1) || (neighbor.MeNorth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, 1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, -1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            }
                            break;
                        case Direction.S:
                            if (neighbor.ProteinEast) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if (neighbor.ProteinWest) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if (neighbor.ProteinNorth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if ((neighbor.EnemyEast && playerId == 1) || (neighbor.MeEast && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            if ((neighbor.EnemyWest && playerId == 1) || (neighbor.MeWest && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if ((neighbor.EnemyNorth && playerId == 1) || (neighbor.MeNorth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.E));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, -1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (-1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            }
                            break;
                        case Direction.E:
                            if (neighbor.ProteinSouth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if (neighbor.ProteinWest) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if (neighbor.ProteinNorth) allRootsActions[rootIdx].Add(Action.GrowHarvester(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if ((neighbor.EnemySouth && playerId == 1) || (neighbor.MeSouth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            if ((neighbor.EnemyWest && playerId == 1) || (neighbor.MeWest && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            if ((neighbor.EnemyNorth && playerId == 1) || (neighbor.MeNorth && playerId == 0)) allRootsActions[rootIdx].Add(Action.GrowTentacle(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, -1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.N));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (0, 1), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.S));
                            }
                            if (HasAtLeastFreeFieldsInDirection(x + direction.x, y + direction.y, (-1, 0), 3))
                            {
                                allRootsActions[rootIdx].Add(Action.GrowSporer(field.OrganId, x + direction.x, y + direction.y, Direction.W));
                            }
                            break;
                    }
                }

                if (field.IsOrgan && field.OrganOrResourceType == 0b11)
                {
                    (int dx, int dy) vector = field.Rotation switch {
                        0b00 => (0, -1),
                        0b01 => (1, 0),
                        0b10 => (0, 1),
                        0b11 => (-1, 0),
                        _ => (0, 0),
                    };

                    (int x, int y) nextPos = (x+vector.dx, y+vector.dy);
                    while(IsValidPos(nextPos.x, nextPos.y))
                    {
                        var nextField = Board[nextPos.x, nextPos.y];
                        if (nextField.IsWall || nextField.IsOrgan) break;
                        
                        if (HasAtLeastFreeOrResourceFields(nextPos.x, nextPos.y, 3))
                        {
                            allRootsActions[rootIdx].Add(Action.Spore(field.OrganId, nextPos.x, nextPos.y));
                        }
                        nextPos = (nextPos.x+vector.dx, nextPos.y+vector.dy);
                    }
                }
            }
        }
        var moves = CartesianProduct(allRootsActions);
        var filteredMoves = FilterMovesByProteinCost(moves, playerId == 1 ? PlayerProteins : OpponentProteins);
        Utils.GenerateMovesWatch.Stop();
        return filteredMoves;
    }

    private bool HasAtLeastFreeFieldsInDirection(int x, int y, (int x, int y) direction, int count)
    {
        int freeCount = 0;
        for (int i = 0; i < count; i++)
        {
            x += direction.x;
            y += direction.y;
            if (!IsValidPos(x, y) || Board[x, y].IsWall || Board[x, y].IsOrgan) break;
            freeCount++;
        }
        return freeCount >= count;
    }

    bool HasAtLeastFreeOrResourceFields(int x, int y, int count)
    {
        var directions = new (int x, int y, Direction from)[] {
            (0, 1, Direction.N), 
            (1, 0, Direction.W), 
            (-1, 0, Direction.E), 
            (0, -1, Direction.S)
        };

        int matchingFields = 0;
        foreach (var dir in directions)
        {
            int nx = x + dir.x, ny = y + dir.y;
            if (IsValidPos(nx, ny) && (!Board[nx, ny].IsOrgan || Board[nx, ny].IsResource))
            {
                matchingFields++;
                if (matchingFields >= count) return true;
            }
        }
        return false;
    }

    private List<Action[]> CartesianProduct(List<Action>[] possibleActionsPerRoot)
    {
        // Startujemy z jedną pustą tablicą, która reprezentuje brak akcji na początku
        IEnumerable<Action[]> combinations = new[] { Array.Empty<Action>() };

        foreach (var actionsForRoot in possibleActionsPerRoot)
        {
            // Tworzymy nowe kombinacje, rozszerzając istniejące tablice o każdą możliwą akcję
            combinations = from combination in combinations
                        from action in actionsForRoot
                        select combination.Append(action).ToArray();
        }

        // Zwracamy jako lista
        return combinations.ToList();
    }

    private List<Action[]> FilterMovesByProteinCost(List<Action[]> moves, int[] playerProteins)
    {
        var organCosts = new Dictionary<CellType, int[]>
        {
            { CellType.BASIC, new[] { 1, 0, 0, 0 } },
            { CellType.HARVESTER, new[] { 0, 0, 1, 1 } },
            { CellType.TENTACLE, new[] { 0, 1, 1, 0 } },
            { CellType.SPORER, new[] { 0, 1, 0, 1 } },
            { CellType.ROOT, new[] { 1, 1, 1, 1 } }
        };
        return moves.Where(move =>
        {
            int[] totalCosts = new int[playerProteins.Length];
            foreach (var action in move)
            {
                if (organCosts.TryGetValue(action.Type, out var costs))
                {
                    for (int i = 0; i < costs.Length; i++)
                    {
                        totalCosts[i] += costs[i];
                    }
                }
            }

            for (int i = 0; i < playerProteins.Length; i++)
            {
                if (playerProteins[i] - totalCosts[i] < 0)
                {
                    return false;
                }
            }

            return true;
        }).ToList();
    }

    private void ProcessGrowth(Action[] playerCommands, Action[] opponentCommands) 
    {
        int[] playerProteinsChange = new int[4] {0, 0, 0, 0};
        int[] enemyProteinsChange = new int[4] {0, 0, 0, 0};
        Dictionary<(int x, int y), int> processedFields = new();

        foreach(var action in playerCommands)
        {
            if (action.Command == CommandEnum.WAIT) continue;
            var targetCell = Board[action.X, action.Y];
            if (action.Command == CommandEnum.SPORE)
            {
                playerProteinsChange[0] -= 1;
                playerProteinsChange[1] -= 1;
                playerProteinsChange[2] -= 1;
                playerProteinsChange[3] -= 1;
                if (targetCell.IsEmpty)
                {
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, OrganCnt, CellType.ROOT, action.Direction, 1);
                }
                else if(targetCell.IsResource)
                {
                    playerProteinsChange[targetCell.OrganOrResourceType] += 3;
                    ClearNeighbourInfo(action.X, action.Y, true);
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, OrganCnt, CellType.ROOT, action.Direction, 1);
                    processedFields.Add((action.X, action.Y), targetCell.OrganOrResourceType);
                }
                else if (targetCell.IsOrgan)
                {
                    RemoveOrgan(action.X, action.Y);
                    AddEntity(action.X, action.Y, 0, 0, 0, CellType.WALL, Direction.X, -1);
                }
                
            }
            else
            {
                switch (action.Type)
                {
                    case CellType.BASIC:
                        playerProteinsChange[0] -= 1;
                        break;
                    case CellType.TENTACLE:
                        playerProteinsChange[1] -= 1;
                        playerProteinsChange[2] -= 1;
                        break;
                    case CellType.HARVESTER:
                        playerProteinsChange[2] -= 1;
                        playerProteinsChange[3] -= 1;
                        break;
                    case CellType.SPORER:
                        playerProteinsChange[1] -= 1;
                        playerProteinsChange[3] -= 1;
                        break;
                }
                if (targetCell.IsEmpty)
                {
                    var parentPos = IdToPosition[action.Id];
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, Board[parentPos.x, parentPos.y].RootId, action.Type, action.Direction, 1);
                }
                else if(targetCell.IsResource)
                {
                    ClearNeighbourInfo(action.X, action.Y, true);
                    playerProteinsChange[targetCell.OrganOrResourceType] += 3;
                    var parentPos = IdToPosition[action.Id];
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, Board[parentPos.x, parentPos.y].RootId, action.Type, action.Direction, 1);
                    processedFields.Add((action.X, action.Y), targetCell.OrganOrResourceType);
                }
                else if (targetCell.IsOrgan)
                {
                    RemoveOrgan(action.X, action.Y);
                    AddEntity(action.X, action.Y, 0, 0, 0, CellType.WALL, Direction.X, -1);
                }
            }
        }

        foreach(var action in opponentCommands)
        {
            if (action.Command == CommandEnum.WAIT) continue;
            var targetCell = Board[action.X, action.Y];
            if (action.Command == CommandEnum.SPORE)
            {
                enemyProteinsChange[0] -= 1;
                enemyProteinsChange[1] -= 1;
                enemyProteinsChange[2] -= 1;
                enemyProteinsChange[3] -= 1;
                if (targetCell.IsEmpty)
                {
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, OrganCnt, CellType.ROOT, action.Direction, 0);
                }
                else if(targetCell.IsResource)
                {
                    ClearNeighbourInfo(action.X, action.Y, true);
                    enemyProteinsChange[targetCell.OrganOrResourceType] += 3;
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, OrganCnt, CellType.ROOT, action.Direction, 0);
                }
                else if (targetCell.IsOrgan)
                {
                    if (processedFields.ContainsKey((action.X, action.Y)))
                    {
                        enemyProteinsChange[processedFields[(action.X, action.Y)]] += 3;
                    }
                    RemoveOrgan(action.X, action.Y);
                    AddEntity(action.X, action.Y, 0, 0, 0, CellType.WALL, Direction.X, -1);
                }
                
            }
            else
            {
                switch (action.Type)
                {
                    case CellType.BASIC:
                        enemyProteinsChange[0] -= 1;
                        break;
                    case CellType.TENTACLE:
                        enemyProteinsChange[1] -= 1;
                        enemyProteinsChange[2] -= 1;
                        break;
                    case CellType.HARVESTER:
                        enemyProteinsChange[2] -= 1;
                        enemyProteinsChange[3] -= 1;
                        break;
                    case CellType.SPORER:
                        enemyProteinsChange[1] -= 1;
                        enemyProteinsChange[3] -= 1;
                        break;
                }
                if (targetCell.IsEmpty)
                {
                    var parentPos = IdToPosition[action.Id];
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, Board[parentPos.x, parentPos.y].RootId, action.Type, action.Direction, 0);
                }
                else if(targetCell.IsResource)
                {
                    ClearNeighbourInfo(action.X, action.Y, true);
                    enemyProteinsChange[targetCell.OrganOrResourceType] += 3;
                    var parentPos = IdToPosition[action.Id];
                    AddEntity(action.X, action.Y, ++OrganCnt, action.Id, Board[parentPos.x, parentPos.y].RootId, action.Type, action.Direction, 0);
                }
                else if (targetCell.IsOrgan)
                {
                    if (processedFields.ContainsKey((action.X, action.Y)))
                    {
                        enemyProteinsChange[processedFields[(action.X, action.Y)]] += 3;
                    }
                    RemoveOrgan(action.X, action.Y);
                    AddEntity(action.X, action.Y, 0, 0, 0, CellType.WALL, Direction.X, -1);
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            PlayerProteins[i] += playerProteinsChange[i];
            OpponentProteins[i] += enemyProteinsChange[i];
        }
    }

    private void ProcessResources()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var field = Board[x, y];
                if (!field.IsResource) continue;
                if (field.MyHarvesters > 0)
                    PlayerProteins[field.OrganOrResourceType] += 1;
                if (field.OpponentHarvesters > 0)
                    OpponentProteins[field.OrganOrResourceType] += 1;
            }
        }
    }

    private void ProcessAttacks()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var field = Board[x, y];
                if (field.MyTentacles == 0 && field.OpponentTentacles == 0) continue;
                if (!field.IsOrgan) continue;
                if ((field.Owner == 1 && field.OpponentTentacles > 0) || (field.Owner == 0 && field.MyTentacles > 0))
                    RemoveOrgan(x, y);
            }
        }
    }

    private bool CheckGameEnd()
    {
        bool turnLimitReached = Turn >= 100;
        bool aPlayerHasDied = roots[0] == 0 || roots[1] == 0;
        bool noMoreSpace = true;
        int myOrgans = 0;
        int opponentOrgans = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Board[x, y].IsEmpty || Board[x, y].IsResource)
                {
                    noMoreSpace = false;
                }
                if (Board[x, y].IsOrgan)
                {
                    if (Board[x, y].IsOwnedByMe)
                    {
                        myOrgans++;
                    }
                    else
                    {
                        opponentOrgans++;
                    }
                }
            }
        }
        bool playersCantBuy = (!CanBuyAnything(0) && myOrgans > opponentOrgans) || (!CanBuyAnything(1) && opponentOrgans > myOrgans);
        return turnLimitReached || aPlayerHasDied || noMoreSpace || playersCantBuy;
    }

    private void AddNeighbourInfo(int x, int y, CellType type, int owner)
    {
        var directions = new (int dx, int dy, Direction dir)[]
        {
            (0, -1, Direction.N), // North
            (1, 0, Direction.E),  // East
            (0, 1, Direction.S),  // South
            (-1, 0, Direction.W)  // West
        };

        foreach(var dirInfo in directions)
        {
            if (x+dirInfo.dx >=0 && x+dirInfo.dx < Width && y+dirInfo.dy >= 0 && y+dirInfo.dy < Height)
            {
                if (type == CellType.PROTEIN_A || type == CellType.PROTEIN_B || type == CellType.PROTEIN_C || type == CellType.PROTEIN_D)
                    Board[x+dirInfo.dx, y+dirInfo.dy].SetProteinInfo(dirInfo.dir);
                else if (type != CellType.EMPTY && type != CellType.WALL && owner == 0)
                    Board[x+dirInfo.dx, y+dirInfo.dy].SetEnemyInfo(dirInfo.dir);
                else if (type != CellType.EMPTY && type != CellType.WALL && owner == 1)
                    Board[x+dirInfo.dx, y+dirInfo.dy].SetMeInfo(dirInfo.dir);
            }
            
        }
    }
    private void ClearNeighbourInfo(int x, int y, bool isProtein)
    {
        var directions = new (int dx, int dy, Direction dir)[]
        {
            (0, -1, Direction.N), // North
            (1, 0, Direction.E),  // East
            (0, 1, Direction.S),  // South
            (-1, 0, Direction.W)  // West
        };

        foreach(var dirInfo in directions)
        {
            if (x+dirInfo.dx >=0 && x+dirInfo.dx < Width && y+dirInfo.dy >= 0 && y+dirInfo.dy < Height)
            {
                if (isProtein)
                    Board[x+dirInfo.dx, y+dirInfo.dy].ClearProteinInfo(dirInfo.dir);
                else
                    Board[x+dirInfo.dx, y+dirInfo.dy].ClearEnemyInfo(dirInfo.dir);
            }
            
        }
    }
    public void Print()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Console.Error.Write(Board[x, y].ToString());
            }
            Console.Error.WriteLine();
        }
        Console.Error.WriteLine();
    }

    private bool IsValidPos(int x, int y)
    {
        return x >=0 && x < Width && y >= 0 && y < Height;
    }

    private bool CanBuyAnything(int playerId)
    {
        var proteins = playerId == 0 ? OpponentProteins : PlayerProteins;

        return (proteins[0] > 0) ||
                (proteins[2] > 0 && proteins[3] > 0) ||
                (proteins[1] > 0 && proteins[2] > 0) ||
                (proteins[1] > 0 && proteins[3] > 0) ||
                (proteins[0]> 0 && proteins[1] > 0 && proteins[2] > 0 && proteins[3] > 0);

    }

    public int GetWinner()
    {
        int myOrgans = 0;
        int opponentOrgans = 0;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Board[x, y].IsOrgan)
                {
                    if (Board[x, y].IsOwnedByMe)
                    {
                        myOrgans++;
                    }
                    else
                    {
                        opponentOrgans++;
                    }
                }
            }
        }

        if (myOrgans > opponentOrgans)
            return 1;
        else if (opponentOrgans > myOrgans)
            return 0;
        else
            return 2;
    }
}