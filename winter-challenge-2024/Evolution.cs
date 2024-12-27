using System;

namespace winter_challenge_2024;

class Evolution
{
    public static Weights RandomWeights(Random rng)
    {
        var weights = new float[10]; // Liczba wag zależy od liczby Twoich parametrów.
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = (float)(rng.NextDouble() * 2 - 1); // Wartości w zakresie [-1, 1].
        }
        return new Weights(weights);
    }

    public static void TrainBots(int generations, int populationSize, Random rng)
    {
        var population = new List<Weights>();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add(RandomWeights(rng));
        }

        for (int gen = 0; gen < generations; gen++)
        {
            Console.WriteLine($"Generation {gen + 1}");
            var scores = new List<(Weights, int)>();

            for (int i = 0; i < population.Count; i++)
            {
                var bot1 = new HeuristicBot(0) { Weights = population[i] };
                int score = 0;
                for (int j = 0; j < population.Count; j++)
                {
                    if (i == j) continue;
                    var bot2 = new HeuristicBot(1) { Weights = population[j] };
                    var result = PlayGame(bot1, bot2, rng, 5);
                    if (result > 0) score++; // Wygrana.
                    else if (result < 0) score--; // Przegrana.
                }
                scores.Add((population[i], score));
            }

            population = SelectAndBreed(scores, rng);
            Console.WriteLine($"Best weights for Gen {gen+1}:");
            foreach (var w in population.First().Params)
            {
                Console.Write($"{w}, ");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Best weights:");
        foreach (var w in population.First().Params)
        {
            Console.Write($"{w}, ");
        }
    }
    public static int PlayGame(HeuristicBot bot1, HeuristicBot bot2, Random rng, int gamesAmount)
    {
        int winner = 0;
        for(int i = 0; i < gamesAmount; i++)
        {
            var gameState = GameStateGenerator.GenerateGameState(rng);
            while (!gameState.IsGameOver)
            {
                var actions1 = bot1.Evaluate(gameState);
                var actions2 = bot2.Evaluate(gameState);
                gameState.ProcessTurn(actions1, actions2);
            }
            var result = gameState.GetWinner();
            if (result == 0) winner++;
            if (result == 1) winner--;
        }
        
        return winner;
    }

    static List<Weights> SelectAndBreed(List<(Weights weights, int score)> population, Random rng)
    {
        // Posortuj po wyniku.
        population.OrderByDescending(item => item.score).ToList();

        // Wybierz najlepszą połowę.
        var survivors = population.Take(population.Count / 2).ToList();

        // Twórz nowe osobniki przez mutację i rekombinację.
        var newPopulation = new List<Weights>();
        foreach (var parent in survivors)
        {
            // Dodaj mutację.
            var mutated = MutateWeights(parent.weights, rng);
            newPopulation.Add(mutated);

            // Dodaj rekombinację (krzyżowanie dwóch najlepszych).
            if (newPopulation.Count < population.Count)
            {
                var partner = survivors[rng.Next(survivors.Count)].weights;
                var child = CrossOverWeights(parent.weights, partner, rng);
                newPopulation.Add(child);
            }
        }
        return newPopulation;
    }

    static Weights MutateWeights(Weights original, Random rng)
    {
        var mutated = original.Params.ToArray();
        for (int i = 0; i < mutated.Length; i++)
        {
            if (rng.NextDouble() < 0.2) // 20% szansy na mutację.
            {
                mutated[i] += (float)(rng.NextDouble() * 0.2 - 0.1); // Perturbacja [-0.1, 0.1].
            }
        }
        return new Weights(mutated);
    }

    static Weights CrossOverWeights(Weights parent1, Weights parent2, Random rng)
    {
        var childParams = new float[parent1.Params.Length];
        for (int i = 0; i < childParams.Length; i++)
        {
            childParams[i] = rng.NextDouble() < 0.5 ? parent1.Params[i] : parent2.Params[i];
        }
        return new Weights(childParams);
    }
}