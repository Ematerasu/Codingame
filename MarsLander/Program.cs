﻿using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

public enum RotationEnum
{
    LEFT,
    RIGHT,
    STAY,
}

public enum PowerEnum
{
    MORE,
    LESS,
    STAY,
}

public record struct Move(RotationEnum rotation, PowerEnum power);

public class GameState
{
    private const double Gravity = 3.711;
    private const int MaxRotationChange = 15;
    private const int MaxThrustChange = 1;

    public double X { get; private set; }
    public double Y { get; private set; }
    public double HorizontalSpeed { get; private set; }
    public double VerticalSpeed { get; private set; }
    public int Fuel { get; private set; }
    public int Rotation { get; private set; }
    public int ThrustPower { get; private set; }
    
    public static Move[] ValidMoves = [
        new Move(RotationEnum.RIGHT, PowerEnum.MORE),
        new Move(RotationEnum.RIGHT, PowerEnum.LESS),
        new Move(RotationEnum.RIGHT, PowerEnum.STAY),
        new Move(RotationEnum.LEFT, PowerEnum.MORE),
        new Move(RotationEnum.LEFT, PowerEnum.LESS),
        new Move(RotationEnum.LEFT, PowerEnum.STAY),
        new Move(RotationEnum.STAY, PowerEnum.MORE),
        new Move(RotationEnum.STAY, PowerEnum.LESS),
        new Move(RotationEnum.STAY, PowerEnum.STAY),
    ];
    public bool Crashed { get; private set; }
    public bool Success { get; private set; }

    public static readonly Dictionary<int, (double Sin, double Cos)> RotationMap = new()
    {
        { -90, (-1.0000000000, 0.0000000000) },
        { -75, (-0.9659258263, 0.2588190451) },
        { -60, (-0.8660254038, 0.5000000000) },
        { -45, (-0.7071067812, 0.7071067812) },
        { -30, (-0.5000000000, 0.8660254038) },
        { -15, (-0.2588190451, 0.9659258263) },
        { 0, (0.0000000000, 1.0000000000) },
        { 15, (0.2588190451, 0.9659258263) },
        { 30, (0.5000000000, 0.8660254038) },
        { 45, (0.7071067812, 0.7071067812) },
        { 60, (0.8660254038, 0.5000000000) },
        { 75, (0.9659258263, 0.2588190451) },
        { 90, (1.0000000000, 0.0000000000) },
    };

    public GameState(
        double x, double y, double horizontalSpeed, double verticalSpeed,
        int fuel, int rotation, int thrustPower,
        bool crashed = false, bool success = false)
    {
        X = x;
        Y = y;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        Fuel = fuel;
        Rotation = rotation;
        ThrustPower = thrustPower;
        Crashed = crashed;
        Success = success;
    }

    public GameState ForwardModel(Move move)
    {
        double currentX = X;
        double currentY = Y;
        int currentRotation = Rotation;
        int currentThrustPower = ThrustPower;
        int newRotation, newThrustPower;
        if (move.rotation == RotationEnum.LEFT)
            {
                newRotation = currentRotation + MaxRotationChange;
                newRotation = newRotation > 90 ? 90 : newRotation;
            }
            else if (move.rotation == RotationEnum.RIGHT)
            {
                newRotation = currentRotation - MaxRotationChange;
                newRotation = newRotation < -90 ? -90 : newRotation;
            }
            else
                newRotation = currentRotation;

            if (move.power == PowerEnum.MORE)
            {
                newThrustPower = currentThrustPower + MaxThrustChange;
                newThrustPower = newThrustPower > 4 ? 4 : newThrustPower;
            }
            else if (move.power == PowerEnum.LESS)
            {
                newThrustPower = currentThrustPower - MaxThrustChange;
                newThrustPower = newThrustPower < 0 ? 0 : newThrustPower;
            }
            else
                newThrustPower = currentThrustPower;
        newThrustPower = Math.Min(Fuel, newThrustPower);
        double sin, cos;
        (sin, cos) = newRotation switch
        {
            -90 => (-1.0000000000, 0.0000000000),
            -75 => (-0.9659258263, 0.2588190451),
            -60 => (-0.8660254038, 0.5000000000),
            -45 => (-0.7071067812, 0.7071067812),
            -30 => (-0.5000000000, 0.8660254038),
            -15 => (-0.2588190451, 0.9659258263),
            0 => (0.0000000000, 1.0000000000),
            15 => (0.2588190451, 0.9659258263),
            30 => (0.5000000000, 0.8660254038),
            45 => (0.7071067812, 0.7071067812),
            60 => (0.8660254038, 0.5000000000),
            75 => (0.9659258263, 0.2588190451),
            90 => (1.0000000000, 0.0000000000),
        };
        double thrustX = sin * newThrustPower;
        double thrustY = cos * newThrustPower;

        double accelerationX = -thrustX;
        double accelerationY = thrustY - Gravity;

        double newHorizontalSpeed = HorizontalSpeed + accelerationX; 
        double newVerticalSpeed = VerticalSpeed + accelerationY;

        double newX = currentX + HorizontalSpeed + 0.5 * accelerationX;
        double newY = currentY + VerticalSpeed + 0.5 * accelerationY;

        int newFuel = Fuel - newThrustPower < 0 ? 0 : Fuel - newThrustPower;

        bool isCrashed = false;
        bool isSuccessful = false;
        bool valid = newRotation == 0 && Math.Abs(newVerticalSpeed) <= 40 && Math.Abs(newHorizontalSpeed) <= 20;
        double curX = currentX > newX ? currentX : newX; //on the right
        double cur2X = currentX < newX ? currentX : newX; // on the left
        foreach(var segment in Globals.SurfaceSegments)
        {
            if (segment.x1 > curX) break;
            if (segment.x2 < cur2X) continue;
            if (newY > segment.y1 && newY > segment.y2) continue;
            if (DoLinesIntersect(currentX, currentY, newX, newY, segment.x1, segment.y1, segment.x2, segment.y2))
            {
                if (segment.y1 == segment.y2)
                {
                    isSuccessful = valid; isCrashed = !valid;
                }
                else
                {
                    isCrashed = true;
                }
            }

        }
        if (newX < 0 || newX > 7000 || newY < 0) isCrashed = true;
        return new GameState(newX, newY, newHorizontalSpeed, newVerticalSpeed, newFuel, newRotation, newThrustPower, isCrashed, isSuccessful);
    }

    public GameState ForwardModel(Move[] moves)
    {
        double currentX = X;
        double currentY = Y;
        double currentHorizontalSpeed = HorizontalSpeed;
        double currentVerticalSpeed = VerticalSpeed;
        int currentFuel = Fuel;
        int currentRotation = Rotation;
        int currentThrustPower = ThrustPower;

        bool isCrashed = false;
        bool isSuccessful = false;
        double ua, ub, denom, newX, newY, newHorizontalSpeed, newVerticalSpeed, accelerationX, accelerationY;
        double thrustX, thrustY;
        int newRotation, newThrustPower, newFuel;
        double sin, cos;
        foreach (var move in moves)
        {
            if (isCrashed || isSuccessful) break;

            if (move.rotation == RotationEnum.LEFT)
            {
                newRotation = currentRotation + MaxRotationChange;
                newRotation = newRotation > 90 ? 90 : newRotation;
            }
            else if (move.rotation == RotationEnum.RIGHT)
            {
                newRotation = currentRotation - MaxRotationChange;
                newRotation = newRotation < -90 ? -90 : newRotation;
            }
            else
                newRotation = currentRotation;

            if (move.power == PowerEnum.MORE)
            {
                newThrustPower = currentThrustPower + MaxThrustChange;
                newThrustPower = newThrustPower > 4 ? 4 : newThrustPower;
            }
            else if (move.power == PowerEnum.LESS)
            {
                newThrustPower = currentThrustPower - MaxThrustChange;
                newThrustPower = newThrustPower < 0 ? 0 : newThrustPower;
            }
            else
                newThrustPower = currentThrustPower;

            (sin, cos) = newRotation switch
            {
                -90 => (-1.0000000000, 0.0000000000),
                -75 => (-0.9659258263, 0.2588190451),
                -60 => (-0.8660254038, 0.5000000000),
                -45 => (-0.7071067812, 0.7071067812),
                -30 => (-0.5000000000, 0.8660254038),
                -15 => (-0.2588190451, 0.9659258263),
                0 => (0.0000000000, 1.0000000000),
                15 => (0.2588190451, 0.9659258263),
                30 => (0.5000000000, 0.8660254038),
                45 => (0.7071067812, 0.7071067812),
                60 => (0.8660254038, 0.5000000000),
                75 => (0.9659258263, 0.2588190451),
                90 => (1.0000000000, 0.0000000000),
            };
            thrustX = sin * newThrustPower;
            thrustY = cos * newThrustPower;

            accelerationX = -thrustX;
            accelerationY = thrustY - Gravity;

            newHorizontalSpeed = currentHorizontalSpeed + accelerationX; 
            newVerticalSpeed = currentVerticalSpeed + accelerationY;

            newX = currentX + currentHorizontalSpeed + 0.5 * accelerationX;
            newY = currentY + currentVerticalSpeed + 0.5 * accelerationY;

            newFuel = currentFuel - newThrustPower < 0 ? 0 : currentFuel - newThrustPower;

            bool valid = newRotation == 0 && Math.Abs(newVerticalSpeed) <= 40 && Math.Abs(newHorizontalSpeed) <= 20;
            double curX = currentX > newX ? currentX : newX; //on the right
            double cur2X = currentX < newX ? currentX : newX; // on the left
            foreach(var segment in Globals.SurfaceSegments)
            {
                if (segment.x1 > curX) break;
                if (segment.x2 < cur2X) continue;
                if (newY > segment.y1 && newY > segment.y2) continue;
                if (DoLinesIntersect(currentX, currentY, newX, newY, segment.x1, segment.y1, segment.x2, segment.y2))
                {
                    if (segment.y1 == segment.y2)
                    {
                        isSuccessful = valid; isCrashed = !valid;
                    }
                    else
                    {
                        isCrashed = true;
                    }
                }

            }

            currentX = newX;
            currentY = newY;
            currentHorizontalSpeed = newHorizontalSpeed;
            currentVerticalSpeed = newVerticalSpeed;
            currentFuel = newFuel;
            currentRotation = newRotation;
            currentThrustPower = newThrustPower;
            if (currentX < 0 || currentX > 7000 || currentY < 0) isCrashed = true;
        }

        return new GameState(currentX, currentY, currentHorizontalSpeed, currentVerticalSpeed, currentFuel, currentRotation, currentThrustPower, isCrashed, isSuccessful);
    }

    public static bool DoLinesIntersect(double x1, double y1, double x2, double y2, 
                                    double x3, double y3, double x4, double y4)
    {
        double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        
        if (Math.Abs(denominator) < 1e-10)
            return false;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denominator;
        double u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / denominator;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, HS: {HorizontalSpeed}, VS: {VerticalSpeed}, Fuel: {Fuel}, Rotation: {Rotation}, Thrust: {ThrustPower}\nCrashed: {Crashed}, Success: {Success}";
    }
}

public class RHEA
{
    private readonly int _populationSize;
    private readonly int _horizonLength;
    private readonly int _mutationRate;
    private readonly int _maxGenerations;
    private readonly Random _random;
    private Move[][]? _previousPopulation;
    private int _eliteSize;

    public RHEA(int populationSize, int horizonLength, int mutationRate, int maxGenerations)
    {
        _populationSize = populationSize;
        _horizonLength = horizonLength;
        _mutationRate = mutationRate;
        _maxGenerations = maxGenerations;
        _random = new Random();
        _eliteSize = _populationSize / 2;
    }

    public Move FindBestMove(GameState initialState)
    {
        var population = _previousPopulation == null
            ? InitializePopulation()
            : ReusePopulation();
        int generation = 0;
        for (; generation < _maxGenerations && Globals.globalWatch.ElapsedMilliseconds < 99; generation++)
        {
            var fitnessScores = EvaluateFitness(population, initialState);
            population = CreateNextGeneration(population, fitnessScores);
        }
        Console.Error.WriteLine($"Generations: {generation}");
        _previousPopulation = population;
        var bestSequence = population[0];
        return bestSequence[0];
    }

    private Move[][] ReusePopulation()
    {
        var population = new Move[_populationSize][];
        for (int i = 0; i < _populationSize; i++)
        {
            var individual = new Move[_horizonLength];
            Array.Copy(_previousPopulation![i], 1, individual, 0, _horizonLength - 1);
            individual[_horizonLength - 1] = GameState.ValidMoves[_random.Next(9)];
            population[i] = individual;
        }

        return population;
    }

    private Move[][] InitializePopulation()
    {
        var population = new Move[_populationSize][];

        for (int i = 0; i < _populationSize; i++)
        {
            var individual = new Move[_horizonLength];
            for(int j = 0; j < _horizonLength; j++)
            {
                individual[j] = GameState.ValidMoves[_random.Next(9)];
            }

            population[i] = individual;
        }

        return population;
    }

    private Move[] GenerateInitialIndividual(GameState initialState)
    {
        var individual = new Move[_horizonLength];
        var currentState = initialState;
        //(int x, int y) middleOfLandingSpot = ((Globals.LandingSegment.x2 + Globals.LandingSegment.x1) / 2, Globals.LandingSegment.y1);
        for (int step = 0; step < _horizonLength; step++)
        {
            var availableMoves = GameState.ValidMoves;
            Move bestMove = availableMoves[0];
            double bestFitness = double.MinValue;

            foreach (var move in availableMoves)
            {
                var simulatedState = currentState.ForwardModel(move);

                double fitness = ComputeFitness(simulatedState);
                //(simulatedState.X - middleOfLandingSpot.x) * (simulatedState.X - middleOfLandingSpot.x) + (simulatedState.Y - middleOfLandingSpot.y) * (simulatedState.Y - middleOfLandingSpot.y);

                if (fitness > bestFitness)
                {
                    bestFitness = fitness;
                    bestMove = move;
                }
                
            }
            individual[step] = bestMove;
            currentState = currentState.ForwardModel(bestMove);
            if (currentState.Crashed || currentState.Success)
            {
                break;
            }
        }
        
        return individual;
    }

    private double[] EvaluateFitness(Move[][] population, GameState initialState)
    {
        var fitnessScores = new double[_populationSize];

        for (int i = 0; i < _populationSize; i++)
        {
            var individual = population[i];
            var state = initialState.ForwardModel(individual);
            fitnessScores[i] = ComputeFitness(state);
        }
        return fitnessScores;
    }

    private double ComputeFitness(GameState state)
    {
        double initialHorizontalVelocity = state.HorizontalSpeed;
        double initialVerticalVelocity = state.VerticalSpeed;
        double flightTime = (2 * Math.Abs(initialVerticalVelocity)) / 3.771;
        double predictedLandingX = state.X + (initialHorizontalVelocity * flightTime);

        int middleOfLandingSpotX = (Globals.LandingSegment.x1 + Globals.LandingSegment.x2) / 2;
        double distanceX = state.X - middleOfLandingSpotX;
        double distanceY = state.Y - Globals.LandingSegment.y1;
        double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);
        
        double speedPenalty = Math.Sqrt(state.HorizontalSpeed*state.HorizontalSpeed + state.VerticalSpeed*state.VerticalSpeed);
        if (state.Success)
        return 100 + state.Fuel;

        if (state.Crashed && (state.X >= Globals.LandingSegment.x1 && state.X <= Globals.LandingSegment.x2))
            return 100 - Math.Abs(state.HorizontalSpeed) / 250 - Math.Abs(state.VerticalSpeed) / 250;

        return 100 - distance - speedPenalty / 175;
    }


    private Move[][] CreateNextGeneration(Move[][] population, double[] fitnessScores)
    {
        var sortedPopulation = population
            .Zip(fitnessScores, (individual, fitness) => (individual, fitness))
            .OrderByDescending(pair => pair.fitness)
            .Select(pair => pair.individual)
            .ToArray();
        
        var elite = sortedPopulation.Take(_eliteSize).ToArray();
        var offspring = new Move[_populationSize - _eliteSize][];

        for(int i = 0; i < _populationSize - _eliteSize; i++)
        {
            var parent1 = elite[_random.Next(_eliteSize)];
            var parent2 = elite[_random.Next(_eliteSize)];
            var child = Crossover(parent1, parent2);
            MutateIndividual(child);
            offspring[i] = child;
        }

        return elite.Concat(offspring).ToArray();
    }

    private Move[] Crossover(
        Move[] parent1,
        Move[] parent2)
    {
        var crossoverPoint = _random.Next(1, _horizonLength - 1);
        Move[] child = new Move[_horizonLength];
        for(int i = 0; i < crossoverPoint; i++)
        {
            child[i] = parent1[i];
        }
        for(int i = crossoverPoint; i < _horizonLength; i++)
        {
            child[i] = parent2[i];
        }
        return child;
    }

    private Move[] MutateIndividual(Move[] individual)
    {
        Move[] availableMoves = GameState.ValidMoves;

        for (int i = 0; i < _horizonLength; i++)
        {
            if (_mutationRate >= _random.Next(100))
                individual[i] = availableMoves[_random.Next(9)];
        }
        return individual;
    }
}


public static class Globals
{
    public static (int x1, int y1, int x2, int y2) LandingSegment = (0, 0, 0, 0);
    public static (int x1, int y1, int x2, int y2)[] SurfaceSegments = [(0, 0, 0, 0)];
    public static (int x, int y) middleOfLandingSpot = (0, 0);
    public static System.Diagnostics.Stopwatch globalWatch = new System.Diagnostics.Stopwatch();

    public static Dictionary<int, List<(int x1, int x2, int y)>> SurfaceIntervals = new Dictionary<int, List<(int x1, int x2, int y)>>();
    public static int IntervalSize = 100;

    public static void PreprocessSurfaceSegments()
    {
        SurfaceIntervals.Clear();

        foreach (var segment in SurfaceSegments)
        {
            int minX = segment.x1;
            int maxX = segment.x2;
            double slope = (double)(segment.y2 - segment.y1) / (segment.x2 - segment.x1);
            double intercept = segment.y1 - slope * segment.x1;

            for (int x = minX; x < maxX; x += IntervalSize)
            {
                int intervalStart = x;
                int intervalEnd = Math.Min(x + IntervalSize, maxX);
                int y1 = (int)(slope * (intervalEnd-intervalStart) + intercept);

                if (!SurfaceIntervals.ContainsKey(x / IntervalSize))
                {
                    SurfaceIntervals[x / IntervalSize] = new List<(int x1, int x2, int y)>();
                }

                SurfaceIntervals[x / IntervalSize].Add((intervalStart, intervalEnd, y1));
            }
        }
    }

    public static bool IsTouchingSurface(int x, int y)
    {
        int key = x / IntervalSize;
        if (!SurfaceIntervals.ContainsKey(key))
            return false;

        foreach (var interval in SurfaceIntervals[key])
        {
            if (x >= interval.x1 && x <= interval.x2 && y <= interval.y)
                return true;
        }

        return false;
    }
}

class Player
{
    public const long NOGC_SIZE = 67_108_864;
    private const bool DEBUG_MODE = true;
    static void Main(string[] args)
    {
        if (!DEBUG_MODE)
            GC.TryStartNoGCRegion(NOGC_SIZE);
        string[] inputs;
        RHEA rhea = new RHEA(50, 100, 10, 10000);
        TextReader inputReader = DEBUG_MODE ? new StreamReader("input.txt") : Console.In;
#pragma warning disable CS8604 // Możliwy argument odwołania o wartości null.
        int N = int.Parse(inputReader.ReadLine()); // the number of points used to draw the surface of Mars.
#pragma warning restore CS8604 // Możliwy argument odwołania o wartości null.
        var surfaceSegments = new (int x1, int y1, int x2, int y2)[N];
        int prevX = 0, prevY = 0;
        Console.Error.WriteLine(N);
        for (int i = 0; i < N; i++)
        {
#pragma warning disable CS8602 // Wyłuskanie odwołania, które może mieć wartość null.
            inputs = inputReader.ReadLine().Split(' ');
#pragma warning restore CS8602 // Wyłuskanie odwołania, które może mieć wartość null.
            int landX = int.Parse(inputs[0]); // X coordinate of a surface point. (0 to 6999)
            int landY = int.Parse(inputs[1]); // Y coordinate of a surface point. By linking all the points together in a sequential fashion, you form the surface of Mars.
            if (i > 0)
            {
                surfaceSegments[i] = (prevX, prevY, landX, landY);
                Console.Error.WriteLine($"{prevX} {prevY} {landX} {landY}");
            }

            prevX = landX;
            prevY = landY;
        }

        foreach (var segment in surfaceSegments)
        {
            int length = Math.Abs(segment.x2 - segment.x1);
            bool isFlat = segment.y1 == segment.y2;

            if (isFlat && length >= 1000)
            {
                Globals.LandingSegment = segment;
            }
        }
        Globals.SurfaceSegments = surfaceSegments;
        Globals.middleOfLandingSpot = ((Globals.LandingSegment.x2 + Globals.LandingSegment.x1)/2, Globals.LandingSegment.y1);
        // Read it once
#pragma warning disable CS8602 // Wyłuskanie odwołania, które może mieć wartość null.
        inputs = inputReader.ReadLine().Split(' ');
#pragma warning restore CS8602 // Wyłuskanie odwołania, które może mieć wartość null.
        double X = int.Parse(inputs[0]);
        double Y = int.Parse(inputs[1]);
        double hSpeed = int.Parse(inputs[2]); // the horizontal speed (in m/s), can be negative.
        double vSpeed = int.Parse(inputs[3]); // the vertical speed (in m/s), can be negative.
        int fuel = int.Parse(inputs[4]); // the quantity of remaining fuel in liters.
        int rotation = int.Parse(inputs[5]); // the rotation angle in degrees (-90 to 90).
        int thrustPower = int.Parse(inputs[6]); // the thrust power (0 to 4).\
        var gameState = new GameState(
            X, Y, hSpeed, vSpeed, fuel, rotation, thrustPower
        );
        Console.Error.WriteLine(gameState);
        Globals.globalWatch.Restart();
        Move move = rhea.FindBestMove(gameState);
        rotation = move.rotation switch {
            RotationEnum.LEFT => rotation == 90 ? 90 : rotation + 15,
            RotationEnum.RIGHT => rotation == -90 ? -90 : rotation - 15,
            RotationEnum.STAY => rotation,
            _ => rotation,
        };
        thrustPower = move.power switch {
            PowerEnum.MORE => thrustPower == 4 ? 4 : thrustPower + 1,
            PowerEnum.LESS => thrustPower == 0 ? 0 : thrustPower - 1,
            PowerEnum.STAY => thrustPower,
            _ => thrustPower,
        };
        Console.WriteLine($"{rotation} {thrustPower}");
        while (true)
        {
            var _ = inputReader.ReadLine();
            gameState = gameState.ForwardModel(move);
            Globals.globalWatch.Restart();
            move = rhea.FindBestMove(gameState);
            Console.Error.WriteLine(gameState);
            rotation = move.rotation switch {
                RotationEnum.LEFT => rotation == 90 ? 90 : rotation + 15,
                RotationEnum.RIGHT => rotation == -90 ? -90 : rotation - 15,
                RotationEnum.STAY => rotation,
                _ => rotation,
            };
            thrustPower = move.power switch {
                PowerEnum.MORE => thrustPower == 4 ? 4 : thrustPower + 1,
                PowerEnum.LESS => thrustPower == 0 ? 0 : thrustPower - 1,
                PowerEnum.STAY => thrustPower,
                _ => thrustPower,
            };
            Console.WriteLine($"{rotation} {thrustPower}");
            if (DEBUG_MODE)
            {
                if (gameState.Crashed || gameState.Success) break;
            }
            if (gameState.Y < 0 || gameState.X < 0 || gameState.X > 7000) 
                break;
        }
        Console.Error.WriteLine($"{((Globals.LandingSegment.x2 + Globals.LandingSegment.x1)/2, Globals.LandingSegment.y1)}");
    }
}