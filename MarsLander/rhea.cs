using System;

public class RHEA
{
    private readonly int _populationSize;
    private readonly int _horizonLength;
    private readonly int _mutationRate;
    private readonly int _maxGenerations;
    private readonly Random _random;
    private (int Rotation, int ThrustPower)[][]? _previousPopulation;

    public RHEA(int populationSize, int horizonLength, int mutationRate, int maxGenerations)
    {
        _populationSize = populationSize;
        _horizonLength = horizonLength;
        _mutationRate = mutationRate;
        _maxGenerations = maxGenerations;
        _random = new Random();
    }

    public (int Rotation, int ThrustPower) FindBestMove(GameState initialState)
    {
        var population = _previousPopulation == null
            ? InitializePopulation(initialState)
            : ReusePopulation(initialState);

        for (int generation = 0; generation < _maxGenerations; generation++)
        {
            var fitnessScores = EvaluateFitness(population, initialState);

            population = CreateNextGeneration(population, fitnessScores);
        }
        _previousPopulation = population;
        var bestSequence = population[0];
        return bestSequence[0];
    }

    private (int Rotation, int ThrustPower)[][] ReusePopulation(GameState initialState)
    {
        var newPopulation = new (int Rotation, int ThrustPower)[_populationSize][];
        var availableMoves = initialState.AvailableMoves();

        for (int i = 0; i < _populationSize; i++)
        {
            var individual = _previousPopulation![i];

            var newIndividual = new (int Rotation, int ThrustPower)[_horizonLength];
            Array.Copy(individual, 1, newIndividual, 0, _horizonLength - 1);

            newIndividual[_horizonLength - 1] = availableMoves[_random.Next(availableMoves.Count)];

            newPopulation[i] = newIndividual;
        }

        return newPopulation;
    }

    private (int Rotation, int ThrustPower)[][] InitializePopulation(GameState initialState)
    {
        var population = new (int Rotation, int ThrustPower)[_populationSize][];
        var availableMoves = initialState.AvailableMoves();
        int[] validRotations = [-15, 0, 15];
        int[] validThrusts = [-1, 0, 1];
        for (int i = 0; i < _populationSize; i++)
        {
            var individual = new (int Rotation, int ThrustPower)[_horizonLength];
            individual[0] = availableMoves[_random.Next(availableMoves.Count)];
            for (int j = 1; j < _horizonLength; j++)
            {
                var previousMove = individual[j - 1];
                int previousRotation = previousMove.Rotation;
                int previousThrust = previousMove.ThrustPower;

                int newRotation = Clamp(
                    previousRotation + validRotations[_random.Next(validRotations.Length)],
                    -90, 90
                );

                int newThrust = Clamp(
                    previousThrust + validThrusts[_random.Next(validThrusts.Length)],
                    0, 4
                );

                individual[j] = (newRotation, newThrust);
            }

            population[i] = individual;
        }

        return population;
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

    private double[] EvaluateFitness((int Rotation, int ThrustPower)[][] population, GameState initialState)
    {
        var fitnessScores = new double[_populationSize];

        for (int i = 0; i < _populationSize; i++)
        {
            var individual = population[i];
            var state = initialState;
            state = state.ForwardModel(individual);
            fitnessScores[i] = ComputeFitness(state);
        }

        return fitnessScores;
    }

    private double ComputeFitness(GameState state)
    {
        if (state.Success) return 1e6;
        if (state.Crashed) return -1e6 - 100 * (Math.Abs(state.Rotation) + Math.Abs(state.HorizontalSpeed) + Math.Abs(state.VerticalSpeed));

        
        double distancePenalty = FastSqrt((state.X - Globals.middleOfLandingSpot.x)*(state.X - Globals.middleOfLandingSpot.x) + (state.Y - Globals.middleOfLandingSpot.y)*(state.Y - Globals.middleOfLandingSpot.y));

        double anglePenalty = Math.Abs(state.Rotation);
        double velocityPenalty = 40 * Math.Abs(state.VerticalSpeed) + 20 * Math.Abs(state.HorizontalSpeed);

        return -(100*distancePenalty + 100 * anglePenalty + velocityPenalty - state.Fuel);
    }

    private (int Rotation, int ThrustPower)[][] CreateNextGeneration(
        (int Rotation, int ThrustPower)[][] population,
        double[] fitnessScores)
    {
        var nextGeneration = new (int Rotation, int ThrustPower)[_populationSize][];

        int eliteCount = _populationSize / 20;
        for (int i = 0; i < eliteCount; i++)
        {
            nextGeneration[i] = population[i];
        }

        var child1Buffer = new (int Rotation, int ThrustPower)[_horizonLength];
        var child2Buffer = new (int Rotation, int ThrustPower)[_horizonLength];

        for (int i = eliteCount; i < _populationSize; i += 2)
        {
            var parent1 = population[SelectByTournament(population, fitnessScores, 3)];
            var parent2 = population[SelectByTournament(population, fitnessScores, 3)];

            Crossover(parent1, parent2, child1Buffer, child2Buffer);

            nextGeneration[i] = Mutate(child1Buffer);

            if (i + 1 < _populationSize)
            {
                nextGeneration[i + 1] = Mutate(child2Buffer);
            }
        }

        return nextGeneration;
    }

    private int SelectByTournament((int Rotation, int ThrustPower)[][] population, double[] fitnessScores, int k)
    {
        int bestIndex = -1;
        double bestFitness = double.MinValue;

        for (int i = 0; i < k; i++)
        {
            int candidateIndex = _random.Next(population.Length);
            if (fitnessScores[candidateIndex] > bestFitness)
            {
                bestFitness = fitnessScores[candidateIndex];
                bestIndex = candidateIndex;
            }
        }

        return bestIndex;
    }

    private ((int Rotation, int ThrustPower)[], (int Rotation, int ThrustPower)[]) Crossover(
        (int Rotation, int ThrustPower)[] parent1,
        (int Rotation, int ThrustPower)[] parent2,
        (int Rotation, int ThrustPower)[] child1,
        (int Rotation, int ThrustPower)[] child2
    )
    {
        int point = _random.Next(1, _horizonLength - 1);

        for(int i = 0; i < point; i++)
        {
            child1[i] = parent1[i];
            child2[i] = parent2[i];
        }

        for(int i = point; i < _horizonLength; i++)
        {
            child1[i] = parent2[i];
            child2[i] = parent1[i];
        }

        return (child1, child2);
    }

    private (int Rotation, int ThrustPower)[] Mutate((int Rotation, int ThrustPower)[] individual)
    {
        if (_random.Next(100) >= _mutationRate) return individual;
        int[] validRotations = [-15, 0, 15];
        int[] validThrusts = [-1, 0, 1];
        int mutations = 2; 
        for (int m = 0; m < mutations; m++)
        {
            int index = _random.Next(0, _horizonLength);

            individual[index].Rotation = Clamp(
                individual[index].Rotation + validRotations[_random.Next(3)],
                -90, 
                90
            );

            individual[index].ThrustPower = Clamp(
                individual[index].ThrustPower + validThrusts[_random.Next(3)],
                0, 
                4
            );

        }

        return individual;
    }

    public static double FastSqrt(double x)
    {
        if (x < 0)
        {
            throw new ArgumentException("Cannot compute square root of negative number");
        }

        double guess = x * 0.5;

        for (int i = 0; i < 3; i++)
        {
            guess = 0.5 * (guess + x / guess);
        }

        return guess;
    }
}
