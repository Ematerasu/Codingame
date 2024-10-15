using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
class Debug
{
    public static void debug(string msg)
    {
        Console.Error.WriteLine(msg);
    }

    public static void Log(string msg)
    {
        Console.Error.WriteLine(msg);
    }
}

public class Building : IEquatable<Building>
{
    public int Type;
    public int Id;
    public int X, Y;
    public List<Building> Parents;
    public List<Building> Children;
    public HashSet<int> typesManaged;

    public int TubesCnt;
    public List<Building> RootsConnectedTo;
    public bool isTeleporterEntrance;
    public bool isTeleporterExit;

    public int EstimatedTraffic;
    public bool IsClustered;

    public Building(int type, int id, int x, int y)
    {
        Type = type;
        Id = id;
        X = x;
        Y = y;
        TubesCnt = 0;
        Parents = new List<Building>();
        Children = new List<Building>();
        RootsConnectedTo = new List<Building>();
        isTeleporterEntrance = false;
        isTeleporterExit = false;
        typesManaged = new HashSet<int>() {type};
        EstimatedTraffic = 0;
        IsClustered = false;
    }

    public bool Equals(Building other)
    {
        // Porównujemy na podstawie Type
        return other != null && Id == other.Id;
    }

    public override int GetHashCode()
    {
        // Używamy Type jako hash code
        return Id.GetHashCode();
    }

    public float DistanceTo(Building otherBuilding)
    {
        int deltaX = this.X - otherBuilding.X;
        int deltaY = this.Y - otherBuilding.Y;

        return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }
    public bool IsPartOfTeleport()
    {
        return isTeleporterEntrance || isTeleporterExit;
    }

    public void DebugTree(int depth = 0)
    {
        string typesManagedString = string.Join(", ", typesManaged);
        Debug.debug($"{new string(' ', depth * 2)}Building ID: {Id} (Type: {Type}, Types Managed: {typesManagedString}, Estimated traffic: {EstimatedTraffic})");

        foreach (var child in Children)
        {
            child.DebugTree(depth + 1);
        }
    }

    public void EvaluateTraffic()
    {
        EstimatedTraffic = 0;
        foreach(var building in RootsConnectedTo)
        {
            var root = building as LandingPad;
            var traffic = 0;
            foreach (var astronautType in root.AstronautTypeCount)
            {
                if (astronautType.Key == Type)
                    traffic += astronautType.Value;
            }
            EstimatedTraffic += traffic;
        }

        if (Type == 0)
        {
            var root = Player.landingPadsMap[Id];
            var traffic = 0;
            foreach (var astronautType in root.AstronautTypeCount)
            {
                traffic += astronautType.Value;
            }
            EstimatedTraffic += traffic;
        }
    }

    public override string ToString()
    {
        return $"Building {Id}, Type {Type} at ({X}, {Y}), traffic: {EstimatedTraffic}";
    }
}

public class LandingPad : Building
{
    public int NumAstronauts { get; set; }
    public Dictionary<int, int> AstronautTypeCount { get; set; }
    public List<Building> BuildingsConnected;

    public LandingPad(string[] landingPadProperties) 
        : base(0, int.Parse(landingPadProperties[1]), int.Parse(landingPadProperties[2]), int.Parse(landingPadProperties[3]))
    {
        NumAstronauts = int.Parse(landingPadProperties[4]);
        AstronautTypeCount = new Dictionary<int, int>();
        BuildingsConnected = new List<Building>();

        for (int i = 5; i < landingPadProperties.Length; i++)
        {
            var type = int.Parse(landingPadProperties[i]);
            if (AstronautTypeCount.ContainsKey(type))
            {
                AstronautTypeCount[type]++;
            }
            else
            {
                AstronautTypeCount[type] = 1;
            }
            typesManaged.Add(type);
            Player.AstronautTypesToManage.Add(type);
        }
    }
    
    public void PrintAstronautTypeCount()
    {
        Debug.debug($"Astronaut Type Count for LandingPad ID: {Id}");
        
        if (AstronautTypeCount.Count == 0)
        {
            Debug.debug("No astronauts of any type.");
            return;
        }
        
        foreach (var entry in AstronautTypeCount)
        {
            Debug.debug($"Type: {entry.Key}, Count: {entry.Value}");
        }
    }
    public Tube AddBuildingToTree(Building building, int resources, List<Tube> currentTubes, List<Building> currentBuildings)
    {   
        Tube best = null;
        int bestDepth = 0;
        Building parent = null;
        Queue<(Building node, int depth)> queue = new Queue<(Building, int)>();
        queue.Enqueue((this, 0));
        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            if (node.TubesCnt < 5)
            {
                var distance = node.DistanceTo(building);
                var cost = Tube.Cost(distance);
                var candidate = new Tube(node.Id, building.Id, distance, 0);
                if (!Tube.canAddTube(candidate, currentTubes, currentBuildings))
                    continue;
                if (best == null && (cost <= resources || cost >= Teleporter.TELEPORTER_BASE_COST))
                {
                    best = candidate;
                    bestDepth = depth;
                    parent = node;
                }
                else if (bestDepth == depth)
                {
                    if (best is not null && best.Length > distance)
                    {
                        best = candidate;
                        parent = node;
                    }
                }
            }
            foreach(var child in node.Children)
            {
                queue.Enqueue((child, depth+1));
            }
        }
        if (best is not null)
        {
            BuildingsConnected.Add(building);
            if (parent is not null)
                parent.Children.Add(building);
            building.Parents.Add(parent);
            building.RootsConnectedTo.Add(this);
            building.TubesCnt++;
            parent.TubesCnt++;
            Queue<Building> q = new Queue<Building>();
            q.Enqueue(building);
            while ( q.Count > 0 )
            {
                var node = q.Dequeue();
                node.typesManaged.Add(building.Type);
                node.EvaluateTraffic();
                foreach (Building p in node.Parents)
                {
                    q.Enqueue(p);
                }
            }
        }
        else
            Debug.debug($"Couldn't connect {building.Id} to {this.Id} tree");
        return best;
    }

    public int AstronautCount()
    {
        return NumAstronauts;
    }

    public bool IsBuildingInTree(Building building)
    {
        return BuildingsConnected.Contains(building);
    }

    public decimal PadValue()
    {
        return (decimal)Math.Round((float)NumAstronauts / (float)AstronautTypeCount.Keys.Count(), 3);
    }

}

public abstract class Transporter : IEquatable<Transporter>
{
    public static float TUBE_BASE_COST = 10f;
    public static float TELEPORTER_BASE_COST = 5000f;
    public int Building1Id, Building2Id;

    public Transporter(int building1Id, int building2Id)
    {
        Building1Id = building1Id;
        Building2Id = building2Id;
    }

    public bool Equals(Transporter other)
    {
        return other != null && ((other.Building1Id == Building1Id && other.Building2Id == Building2Id) || (other.Building1Id == Building2Id && other.Building2Id == Building1Id));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Building1Id, Building2Id);
    }

    public bool IsConnecting(int building1Id, int building2Id)
    {
        if (building1Id == Building1Id && building2Id == Building2Id)
            return true;
        if (building1Id == Building2Id && building2Id == Building1Id)
            return true;
        return false;
    }

    public abstract float Score();

}

public class Tube : Transporter
{
    public int Level;
    public List<Pod> PodsInTube;
    public float Length;
    public int EstimatedTraffic;

    public static List<Tube> UnaddableTubes = new List<Tube>();

    public Tube(int building1Id, int building2Id, float length, int estimatedTraffic, int level = 1)
        : base(building1Id, building2Id)
    {
        Level = level;
        PodsInTube = new List<Pod>();
        if (length == 0)
        {
            Length = Player.buildingsMap[building1Id].DistanceTo(Player.buildingsMap[building2Id]);
        }
        else
            Length = length;
        EstimatedTraffic = estimatedTraffic;
    }

    public override string ToString()
    {
        return $"({Building1Id} -> {Building2Id}, level: {Level}, cost {Cost()}, score {Score()})";
    }

    public void Upgrade()
    {
        Level++;
    }
    
    public int CurrentCapacity()
    {
        return PodsInTube.Count * 10;
    }

    public bool NeedsMorePods()
    {
        return CurrentCapacity() < EstimatedTraffic;
    }

    public static float Cost(float length)
    {
        return length * TUBE_BASE_COST;
    }

    public float Cost()
    {
        return Length * TUBE_BASE_COST;
    }

    public float UpgradeCost()
    {
        return Cost() * (Level + 1);
    }

    public bool Intersects(Tube other)
    {
        Building building1, building2, building3, building4;
        building1 = Player.buildingsMap[Building1Id];
        building2 = Player.buildingsMap[Building2Id];
        building3 = Player.buildingsMap[other.Building1Id];
        building4 = Player.buildingsMap[other.Building2Id];
        if (this.Building1Id == other.Building1Id || this.Building1Id == other.Building2Id || this.Building2Id == other.Building1Id || this.Building2Id == other.Building2Id)
        {
            return false;
        }
        return DoIntersect(building1.X, building1.Y, building2.X, building2.Y, building3.X, building3.Y, building4.X, building4.Y);
    }

    private bool DoIntersect(int x1, int y1, int x2, int y2, 
                            int x3, int y3, int x4, int y4)
    {
        int o1 = Orientation(x1, y1, x2, y2, x3, y3);
        int o2 = Orientation(x1, y1, x2, y2, x4, y4);
        int o3 = Orientation(x3, y3, x4, y4, x1, y1);
        int o4 = Orientation(x3, y3, x4, y4, x2, y2);

        if (o1 != o2 && o3 != o4)
            return true;

        if (o1 == 0 && OnSegment(x1, y1, x3, y3, x2, y2)) return true;
        if (o2 == 0 && OnSegment(x1, y1, x4, y4, x2, y2)) return true;
        if (o3 == 0 && OnSegment(x3, y3, x1, y1, x4, y4)) return true;
        if (o4 == 0 && OnSegment(x3, y3, x2, y2, x4, y4)) return true;

        return false;
    }

    private int Orientation(int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int val = (y2 - y1) * (x3 - x2) - (x2 - x1) * (y3 - y2);

        if (val == 0) return 0;
        return (val > 0) ? 1 : 2;
    }

    private bool OnSegment(int x1, int y1, int x2, int y2, int x3, int y3)
    {
        if (x2 <= Math.Max(x1, x3) && x2 >= Math.Min(x1, x3) &&
            y2 <= Math.Max(y1, y3) && y2 >= Math.Min(y1, y3))
            return true;

        return false;
    }

    public bool IsSegmentIntersectingBuilding(Building building)
    {
        var building1 = Player.buildingsMap[Building1Id];
        var building2 = Player.buildingsMap[Building2Id];
        if (building1.Id == building.Id || building2.Id == building.Id)
            return false;
        var x1 = building1.X;
        var y1 = building1.Y;
        var x2 = building2.X;
        var y2 = building2.Y;

        var px = building.X;
        var py = building.Y;

        bool isOnLine = (y2 - y1) * (px - x1) == (py - y1) * (x2 - x1);

        if (isOnLine)
        {
            bool isWithinSegment = 
                Math.Min(x1, x2) <= px && px <= Math.Max(x1, x2) &&
                Math.Min(y1, y2) <= py && py <= Math.Max(y1, y2);

            return isWithinSegment;
        }

        return false;
    }

    public static bool canAddTube(Tube tube, List<Tube> currentTubes, List<Building> buildings)
    {
        if (Player.buildingsMap[tube.Building1Id].TubesCnt >= 5 || Player.buildingsMap[tube.Building2Id].TubesCnt >= 5)
        {
            UnaddableTubes.Add(tube);
            return false;
        }        
        if (Tube.UnaddableTubes.Contains(tube))
        {
            return false;
        }

        foreach(Tube _tube in currentTubes)
        {
            if (tube.Intersects(_tube))
            {
                //Debug.debug($"tube {tube.ToString()} intersects with tube {_tube.ToString()}");
                UnaddableTubes.Add(tube);
                return false;
            }
        }

        foreach(Building _building in buildings)
        {
            if (tube.IsSegmentIntersectingBuilding(_building))
            {
                //Debug.debug($"tube {tube.ToString()} intersects with building {_building.Id}");
                UnaddableTubes.Add(tube);
                return false;
            }
        }

        return true;
    }

    public override float Score()
    {
        //Building source = Player.buildingsMap[Building1Id];
        Building target = Player.buildingsMap[Building2Id];
        if (CurrentCapacity() == 0 && target.EstimatedTraffic > 0)
            return 2137f;
        if (CurrentCapacity() == 0)
            return 0f;
        return target.EstimatedTraffic / CurrentCapacity();
    }
}

public class Teleporter : Transporter
{
    public Teleporter(int building1Id, int building2Id)
        : base(building1Id, building2Id)
    {
    }

    public static float Cost() { return TELEPORTER_BASE_COST; }

    public override string ToString()
    {
        return $"({Building1Id} -> {Building2Id})";
    }

    public override float Score()
    {
        Building target = Player.buildingsMap[Building2Id];
        float estimatedTransfer = (float)target.EstimatedTraffic;
        return 1000000;
    }
}

public class Pod
{
    public static int PODE_BASE_COST = 1000;
    public static int PodsCnt = 0;
    public int PodId;
    public List<int> Path;

    public Pod(List<int> path)
    {
        PodId = ++PodsCnt;
        Path = new List<int>(path);
        if (PodsCnt >= 500)
        {
            PodsCnt = 0;
        }
    }

    public Pod(string[] podsProperties)
    {
        PodId = int.Parse(podsProperties[0]);
        Path = new List<int>();
        for (int i = 2; i < podsProperties.Length; i++)
        {
            Path.Add(int.Parse(podsProperties[i]));
        }
    }
}
public class CityState
{
    public int resources;
    public List<Tube> tubes;
    public List<Teleporter> teleporters;
    public List<Pod> pods;

    public List<LandingPad> trees;

    public Dictionary<int, List<Building>> typeToBuildingMap;
    public List<Tube> newTubes;
    public List<Pod> newPods;
    public List<Tube> tubeUpgrades;
    public List<Teleporter> newTeleports;

    public CityState(int initialResources)
    {
        resources = initialResources;
        tubes = new List<Tube>();
        teleporters = new List<Teleporter>();
        pods = new List<Pod>();
        typeToBuildingMap = new Dictionary<int, List<Building>>();
        newTubes = new List<Tube>();
        newPods = new List<Pod>();
        tubeUpgrades = new List<Tube>();
        newTeleports = new List<Teleporter>();
    }

    public void Preprocess(List<Building> buildings, List<LandingPad> landingPads)
    {
        foreach(Building building in buildings)
        {
            if (typeToBuildingMap.ContainsKey(building.Type))
            {
                typeToBuildingMap[building.Type].Add(building);
            }
            else
            {
                typeToBuildingMap[building.Type] = new List<Building> { building };
            }
        }

        var orderedPads = landingPads.OrderByDescending(pad => pad.PadValue()).ToList();
        foreach(LandingPad landingPad in orderedPads)
        {
            foreach(int type in landingPad.AstronautTypeCount.Keys)
            {
                var nearBuildingsOfSameType = typeToBuildingMap[type].OrderBy(b => b.DistanceTo(landingPad)).ThenBy(b => b.RootsConnectedTo.Count()).Take(3);
                foreach(Building b in nearBuildingsOfSameType)
                {
                    var tube = new Tube(landingPad.Id, b.Id, b.DistanceTo(landingPad), landingPad.AstronautTypeCount[b.Type]);
                    if(AddTube(tube, buildings))
                    {
                        FinishTube(landingPad, b, tube);
                        break;
                    }
                }
            }
        }

        AddNewTeleporters(buildings, landingPads);
    }

    public List<string> Evaluate(List<Building> buildings, List<LandingPad> landingPads, List<Building> newBuildings, List<LandingPad> newLandingPads)
    {
        List<string> actions = new List<string>();
        Debug.debug($"Resources to spend: {resources}");

        foreach(Building building in newBuildings)
        {
            if (typeToBuildingMap.ContainsKey(building.Type))
            {
                typeToBuildingMap[building.Type].Add(building);
            }
            else
            {
                typeToBuildingMap[building.Type] = new List<Building> { building };
            }
        }

        if(Player.round < 10)
        {
            CreateNewTubes(buildings, landingPads);
            foreach(Building building in buildings)
            {
                building.EvaluateTraffic();
            }
            foreach(Tube tube in newTubes)
            {
                actions.Add($"TUBE {tube.Building1Id} {tube.Building2Id}");
            }

            AddNewTeleporters(buildings, landingPads);
            foreach(Teleporter tp in newTeleports)
            {
                actions.Add($"TELEPORT {tp.Building1Id} {tp.Building2Id}");
            }
        }
        else
        {
            AddNewTeleporters(buildings, landingPads);
            foreach(Teleporter tp in newTeleports)
            {
                actions.Add($"TELEPORT {tp.Building1Id} {tp.Building2Id}");
            }

            if (newTeleports.Count() == 0)
            {
                CreateNewTubes(buildings, landingPads);
                foreach(Building building in buildings)
                {
                    building.EvaluateTraffic();
                }
            }
        }

        // AddNewPodes();
        // foreach(Tube tube in tubeUpgrades)
        // {
        //     actions.Add($"UPGRADE {tube.Building1Id} {tube.Building2Id}");
        // }

        foreach(Pod pod in newPods)
        {
            actions.Add($"POD {pod.PodId} {string.Join(" ", pod.Path)}");
        }

        DebugState(buildings);
        // foreach(LandingPad pad in landingPads)
        // {
        //     pad.DebugTree();
        // }
        newTubes.Clear();
        tubeUpgrades.Clear();
        newTeleports.Clear();
        newPods.Clear();
        return actions;
    }

    public void CreateNewTubes(List<Building> buildings, List<LandingPad> landingPads)
    {
        Random rnd = new Random();
        var orderedPads = landingPads.Where(pad => pad.TubesCnt < 5)
                                        .OrderBy(pad => pad.TubesCnt)
                                        .ThenBy(b => rnd.Next());
                                        //.Take(10);
        foreach(LandingPad landingPad in orderedPads)
        {
            var validBuildings = buildings.Where(b => landingPad.AstronautTypeCount.ContainsKey(b.Type))
                                            .OrderBy(b => landingPad.AstronautTypeCount[b.Type])
                                            .ThenBy(b => rnd.Next())
                                            .Take(5);
            foreach(Building building in validBuildings)
            {
                var flowCalc = building.Type != 0 ? landingPad.AstronautTypeCount[building.Type] : (building as LandingPad).NumAstronauts;
                var tube = new Tube(landingPad.Id, building.Id, building.DistanceTo(landingPad), flowCalc);
                if(AddTube(tube, buildings))
                {
                    FinishTube(building, landingPad, tube);
                    break;
                }
            }

            var padTeleports = landingPads.Where(pad => pad.isTeleporterEntrance);
            foreach(LandingPad pad in padTeleports)
            {
                var tube = new Tube(landingPad.Id, pad.Id, pad.DistanceTo(landingPad), 10);
                if(AddTube(tube, buildings))
                {
                    FinishTube(pad, landingPad, tube);
                    break;
                }
            }
        }
        // var loneBuildings = buildings.Where(b => Player.AstronautTypesToManage.Contains(b.Type) && b.Type != 0)
        //                                 .OrderBy(b => b.TubesCnt)       
        //                                 .ThenBy(b => rnd.Next())
        //                                 .Take(10);
        // foreach(Building building in loneBuildings)
        // {
        //     var validBuildings = buildings.Where(b => b.Id != building.Id && b.typesManaged.Contains(building.Type)).OrderBy(b => rnd.Next()).Take(4);
        //     var tubesInfo = string.Join(", ", validBuildings.Select(tube => tube.ToString()));
        //     Debug.debug($"Building {building.ToString()}: valid buildings [{tubesInfo}]");
        //     foreach(var b in validBuildings)
        //     {
        //         var flowCalc = b.Parents.Sum(p => p.EstimatedTraffic);
        //         var tube = new Tube(building.Id, b.Id, building.DistanceTo(b), flowCalc);
        //         if(AddTube(tube, buildings))
        //         {
        //             FinishTube(building, b, tube);
        //             break;
        //         }
        //     }
        // }
    }

    private void FinishTube(Building building, Building building2, Tube tube)
    {
        if (building2.Type == 0)
            building.RootsConnectedTo.Add(building2);
        if (building.Type == 0)
            building2.RootsConnectedTo.Add(building);
        if (building.Type != 0 && building2.Type != 0)
        {
            building.RootsConnectedTo.AddRange(building2.RootsConnectedTo);
            building2.RootsConnectedTo.AddRange(building.RootsConnectedTo);
        }
        building.Parents.Add(building2);
        building2.Children.Add(building);
        building.TubesCnt++;
        building2.TubesCnt++;
        var merge = new HashSet<int>(building.typesManaged.Union(building2.typesManaged));
        building.typesManaged = merge;
        building2.typesManaged = merge;
        if (resources < Pod.PODE_BASE_COST)
            return;
        List<int> path = new List<int>() {tube.Building1Id, tube.Building2Id, tube.Building1Id};
        var pod = new Pod(path);
        AddPod(pod);
        tube.PodsInTube.Add(pod);
        resources -= Pod.PODE_BASE_COST;
    }

    public bool AddTube(Tube tube, List<Building> buildings)
    {
        if (Tube.canAddTube(tube, tubes, buildings) && !tubes.Any(t => t.Equals(tube)) && !newTubes.Any(t => t.Equals(tube)) && resources >= (int)tube.Cost())
        {
            tubes.Add(tube);
            newTubes.Add(tube);
            resources -= (int)tube.Cost();
            return true;
        }
        else
        {
            //Debug.debug($"Can't add tube ({tube.Building1Id} {tube.Building2Id})");
            return false;
        }
    }

    public void AddNewTeleporters(List<Building> buildings, List<LandingPad> landingPads)
    {
        Random rnd = new Random();
        var validBuildings = buildings.Where(b => Player.AstronautTypesToManage.Contains(b.Type)).OrderBy(b => b.TubesCnt).Take(10);
        foreach(Building building in validBuildings)
        {
            if (resources < (int)Teleporter.TELEPORTER_BASE_COST)
                break;
            Debug.debug(building.ToString());
            var validPad = landingPads.Where(pad => !pad.IsPartOfTeleport()).Where(pad => pad.AstronautTypeCount.Keys.Contains(building.Type)).OrderBy(pad => pad.AstronautTypeCount[building.Type]).FirstOrDefault();
            if (validPad is null)
                continue;
            Debug.debug(validPad.ToString());
            var tp = new Teleporter(validPad.Id, building.Id);
            AddTeleporter(tp);
            resources -= (int)Teleporter.TELEPORTER_BASE_COST;
        }
    }

    public void AddNewPodes()
    {
        var validTubes = tubes.Where(t => t.PodsInTube.Count() == 0);
        foreach (Tube tube in validTubes)
        {
            // if (tube.PodsInTube.Count() == tube.Level)
            // {
            //     if (resources < (int)tube.UpgradeCost())
            //         continue;
            //     tubeUpgrades.Add(tube);
            //     resources -= (int)tube.UpgradeCost();
            // }
            if (resources < Pod.PODE_BASE_COST)
                return;
            List<int> path = new List<int>() {tube.Building1Id, tube.Building2Id, tube.Building1Id};
            var pod = new Pod(path);
            AddPod(pod);
            tube.PodsInTube.Add(pod);
            resources -= Pod.PODE_BASE_COST;
        }
    }
    public List<string> TryAddTeleport(List<Building> buildings, List<LandingPad> landingPads)
    {
        List<string> actions = new List<string>();
        return actions;
    }
    public void AddTeleporter(Teleporter teleporter)
    {
        teleporters.Add(teleporter);
        newTeleports.Add(teleporter);
        Building b1 = Player.buildingsMap[teleporter.Building1Id];
        Building b2 = Player.buildingsMap[teleporter.Building2Id];
        b1.isTeleporterEntrance = true;
        b2.isTeleporterExit = true;
        b1.Children.Add(b2);
        b2.Parents.Add(b1);

        if (b1.Type == 0 && b2.Type != 0)
        {
            b2.RootsConnectedTo.Add(b1);
            b2.EvaluateTraffic();
        }

    }

    public void AddPod(Pod pod)
    {
        pods.Add(pod);
        newPods.Add(pod);
    }

    public void DebugState(List<Building> buildings)
    {
        Debug.debug($"Resources: {resources}");

        string buildingsInfo = string.Join(", ", buildings.Select(building => building.ToString()));
        Debug.debug($"Buildings: [{buildingsInfo}]");

        string tubesInfo = string.Join(", ", tubes.Select(tube => tube.ToString()));
        Debug.debug($"Tubes: [{tubesInfo}]");

        string teleportersInfo = string.Join(", ", teleporters.Select(teleporter => teleporter.ToString()));
        Debug.debug($"Teleporters: [{teleportersInfo}]");

        string podsInfo = string.Join(", ", pods.Select(pod => $"Pod Id: {pod.PodId}"));
        Debug.debug($"Pods: [{podsInfo}]");

        Debug.debug("Building map:");
        foreach(KeyValuePair<int, List<Building>> entry in typeToBuildingMap)
        {
            string buildingMap = string.Join(", ", entry.Value.Select(b => b.Id));
            Debug.debug($"\t{entry.Key} -> [{buildingMap}]");
        }
    }
}


class Player
{
    public List<Building> buildings;
    public List<LandingPad> landingPads;
    public static Dictionary<int, Building> buildingsMap = new Dictionary<int, Building>();
    public static Dictionary<int, LandingPad> landingPadsMap = new Dictionary<int, LandingPad>();
    public static HashSet<int> AstronautTypesToManage = new HashSet<int>();

    public static int round = 0;

    public void Play()
    {
        CityState currentState = new CityState(0);;
        buildings = new List<Building>();
        landingPads = new List<LandingPad>();
        List<LandingPad> newLandingPads = new List<LandingPad>();
        List<Building> newBuildings = new List<Building>();
        
        while (true)
        {
            round++;
            #region STATE INITIALIZATION
            int resources = int.Parse(Console.ReadLine());
            currentState.resources = resources;
            currentState.tubes.Clear();
            currentState.teleporters.Clear();
            currentState.pods.Clear();
            int numTravelRoutes = int.Parse(Console.ReadLine());
            for (int i = 0; i < numTravelRoutes; i++)
            {
                string[] inputs = Console.ReadLine().Split(' ');
                int buildingId1 = int.Parse(inputs[0]);
                int buildingId2 = int.Parse(inputs[1]);
                int capacity = int.Parse(inputs[2]);
                if (capacity == 0)
                {
                    if (!currentState.teleporters.Any(t => t.IsConnecting(buildingId1, buildingId2)))
                        currentState.teleporters.Add(
                            new Teleporter(buildingId1, buildingId2)
                        );
                }
                else
                {
                    if (!currentState.tubes.Any(t => t.IsConnecting(buildingId1, buildingId2)))
                        currentState.tubes.Add(
                            new Tube(buildingId1, buildingId2, capacity, 0)
                        );
                }
            }
            int numPods = int.Parse(Console.ReadLine());
            for (int i = 0; i < numPods; i++)
            {
                string[] podProperties = Console.ReadLine().Split(' ');
                if (!currentState.pods.Any(p => p.PodId == int.Parse(podProperties[0])))
                    currentState.pods.Add(new Pod(podProperties));
            }
            int numNewBuildings = int.Parse(Console.ReadLine());
            for (int i = 0; i < numNewBuildings; i++)
            {
                string[] buildingProperties = Console.ReadLine().Split(' ');
                if (int.Parse(buildingProperties[0]) == 0)
                {
                    LandingPad pad = new LandingPad(buildingProperties);
                    landingPadsMap.Add(pad.Id, pad);
                    buildingsMap.Add(pad.Id, pad);
                    buildings.Add(pad);
                    landingPads.Add(pad);
                    newLandingPads.Add(pad);
                }
                else // simple building
                {
                    Building building = new Building(
                            int.Parse(buildingProperties[0]),
                            int.Parse(buildingProperties[1]),
                            int.Parse(buildingProperties[2]),
                            int.Parse(buildingProperties[3])
                        );
                    buildingsMap.Add(building.Id, building);
                    buildings.Add(building);
                    newBuildings.Add(building);
                }
                
            }
            #endregion
            

            // Write an action using Console.WriteLine()
            // To debug: Console.Error.WriteLine("Debug messages...");
            GenerateMoves(currentState, newLandingPads, newBuildings);
            
            newLandingPads.Clear();
            newBuildings.Clear();
        }
    }

    static void Main(string[] args)
    {
        Player instance = new Player();
        instance.Play();
    }

    public void GenerateMoves(CityState state, List<LandingPad> newPads, List<Building> newBuildings)
    {
        Debug.debug($"Round {round}");
        if (round == 1)
        {
            state.Preprocess(newBuildings, newPads);
        }
        
        List<string> actions = state.Evaluate(buildings, landingPads, newBuildings, newPads);

        if (actions.Count > 0)
            Console.WriteLine(string.Join(";", actions));
        else
            Console.WriteLine("WAIT");
    }
}