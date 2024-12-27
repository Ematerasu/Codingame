using System;
using System.Diagnostics;
using System.Dynamic;

namespace winter_challenge_2024;

class RandomBot
{
    Random rng;
    int PlayerId;
    public RandomBot(int playerId) 
    { 
        rng = new Random();
        PlayerId = playerId;
    }

    public List<Action> Evaluate(GameState gameState)
    {
        var actionsList = gameState.GetPossibleActions(PlayerId);
        var rndIdx = rng.Next(actionsList.Count);
        return actionsList[rndIdx];
    }
}
struct Weights
{
    public float[] Params; // Trzymaj wszystkie wagi w tablicy dla łatwiejszej manipulacji.
    public Weights(float[] weightParams)
    {
        Params = weightParams;
    }
}

class HeuristicBot
{
    Random rng;
    int PlayerId;
    public Weights Weights { get; set; }
    public HeuristicBot(int playerId) 
    { 
        PlayerId = playerId;
        rng = new Random();
        Weights = new Weights 
        { 
            Params = [
                -0.5971339f,  // Pointless Dir penalty
                -0.31787202f, // Farming harvesters value
                0.73444957f, // Aggresive tentacles value
                0.59324366f, // Sporer value
                0.54558486f, // Basic value
                0.5661302f,  // Root value
                -0.07786243f, // PROTEIN_A value
                -0.767483f,  // PROTEIN_B value
                -0.3995268f, // PROTEIN_C value
                0.5912162f // PROTEIN_D value
            ]
        };
    }

    public List<Action> Evaluate(GameState gameState)
    {
        var actionsList = gameState.GetPossibleActions(PlayerId);
        var enemyActionsList = gameState.GetPossibleActions(1 - PlayerId);
        var bestActions = new List<Action>();
        float bestScore = float.MinValue;
        int simulations = 0;
        //ameState.Print();
        //Debug.Log("Got this state\n");
        while(Utils.watch.ElapsedMilliseconds < Utils.MAX_TURN_TIME - 2)
        //while(simulations < 10)
        {
            var randomActions = GetRandomActions(actionsList);
            var enemyRandomActions = GetRandomActions(enemyActionsList);
            var simulatedState = gameState.Clone();
            simulatedState.ProcessTurn(enemyRandomActions, randomActions);
            float score = EstimateState(simulatedState);
            //Debug.Log($"Simulated game state with action: {randomActions[0].ToString()}\n");
            //simulatedState.Print();
            //Debug.Log($"Estimated {score} score\n\n");
            //string title = $"TURN {simulatedState.Turn:D3} - {randomActions[0].ToString()}";
            //Helpers.VisualizeDebugState(simulatedState, title, $"Score: {score}");
            simulations++;
            if (score > bestScore)
            {
                bestScore = score;
                bestActions = new List<Action>(randomActions);
            }
        }
        Console.Error.WriteLine($"Did {simulations} sims in {Utils.watch.ElapsedMilliseconds}ms");
        if (bestActions.Count == 0)
        {
            foreach (var rootActions in actionsList)
            {
                bestActions.Add(Action.Wait());
            }
        }

        return bestActions;
    }

    protected List<Action> GetRandomActions(List<List<Action>> actionsList)
    {
        var rndIdx = rng.Next(actionsList.Count);
        return actionsList[rndIdx];
    }

    protected float EstimateState(GameState gameState)
    {
        var myEntities = gameState.Player1Entities;
        var enemyEntities = gameState.Player0Entities;
        var myProteins = gameState.Player1Proteins;
        var enemyProteins = gameState.Player0Proteins;

        float score = 0f;

        foreach (var entity in myEntities.Values)
        {
            var dir = Utils.DirToVector(entity.Dir);
            (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
            if (!gameState.IsPositionValid(neighborPos))
            {
                score -= Weights.Params[0];
            }
            switch (entity.Type)
            {
                case CellType.HARVESTER:
                    if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
                    {
                        score += Weights.Params[1];
                    }
                    else
                    {
                        score -= Weights.Params[1];
                    }
                    break;
                case CellType.TENTACLE:
                    if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
                    {
                        score += Weights.Params[2];
                    }
                    else
                    {
                        score -= Weights.Params[2];
                    }
                    break;

                case CellType.SPORER:
                    score += Weights.Params[3];
                    break;
                case CellType.BASIC:
                    score += Weights.Params[4];
                    break;
                case CellType.ROOT:
                    score += Weights.Params[5];
                    break;
            }
        }

        foreach (var entity in enemyEntities.Values)
        {
            var dir = Utils.DirToVector(entity.Dir);
            (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
            switch (entity.Type)
            {
                case CellType.HARVESTER:
                    if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
                    {
                        score -= Weights.Params[1];
                    }
                    else
                    {
                        score += Weights.Params[1];
                    }
                    break;
                case CellType.TENTACLE:
                    if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
                    {
                        score -= Weights.Params[2];
                    }
                    else
                    {
                        score += Weights.Params[2];
                    }
                    break;

                case CellType.SPORER:
                    score -= Weights.Params[3];
                    break;
                case CellType.BASIC:
                    score -= Weights.Params[4];
                    break;
                case CellType.ROOT:
                    score -= Weights.Params[5];
                    break;
            }
        }

        score += myProteins.A * Weights.Params[6];
        score += myProteins.B * Weights.Params[7];
        score += myProteins.C * Weights.Params[8];
        score += myProteins.D * Weights.Params[9];

        score -= enemyProteins.A * Weights.Params[6];
        score -= enemyProteins.B * Weights.Params[7];
        score -= enemyProteins.C * Weights.Params[8];
        score -= enemyProteins.D * Weights.Params[9];

        return score;
    }
}

class SimplerHeuristicBot
{
    Random rng;
    int PlayerId;
    public SimplerHeuristicBot(int playerId) 
    { 
        PlayerId = playerId;
        rng = new Random();
    }

    public List<Action> Evaluate(GameState gameState)
    {
        var actionsList = gameState.GetPossibleActions(PlayerId);
        var enemyActionsList = gameState.GetPossibleActions(1 - PlayerId);
        var bestActions = new List<Action>();
        float bestScore = float.MinValue;
        //int simulations = 0;
        //ameState.Print();
        //Debug.Log("Got this state\n");
        while(Utils.watch.ElapsedMilliseconds < Utils.MAX_TURN_TIME - 2)
        //while(simulations < 10)
        {
            var randomActions = GetRandomActions(actionsList);
            var enemyRandomActions = GetRandomActions(enemyActionsList);
            var simulatedState = gameState.Clone();
            simulatedState.ProcessTurn(enemyRandomActions, randomActions);
            float score = EstimateState(simulatedState);
            //Debug.Log($"Simulated game state with action: {randomActions[0].ToString()}\n");
            //simulatedState.Print();
            //Debug.Log($"Estimated {score} score\n\n");
            //string title = $"TURN {simulatedState.Turn:D3} - {randomActions[0].ToString()}";
            //Helpers.VisualizeDebugState(simulatedState, title, $"Score: {score}");
            //simulations++;
            if (score > bestScore)
            {
                bestScore = score;
                bestActions = new List<Action>(randomActions);
            }
        }
        //Console.Error.WriteLine($"Did {simulations} sims in {Utils.watch.ElapsedMilliseconds}ms");
        if (bestActions.Count == 0)
        {
            foreach (var rootActions in actionsList)
            {
                bestActions.Add(Action.Wait());
            }
        }

        return bestActions;
    }

    protected List<Action> GetRandomActions(List<List<Action>> actionsList)
    {
        var rndIdx = rng.Next(actionsList.Count);
        return actionsList[rndIdx];
    }

    protected float EstimateState(GameState gameState)
    {
        var myEntities = PlayerId == 0 ? gameState.Player0Entities : gameState.Player1Entities;
        var enemyEntities = PlayerId == 0 ? gameState.Player1Entities : gameState.Player0Entities;
        
        float score = myEntities.Count - enemyEntities.Count;

        return score;
    }
}
class BeamSearchBot
{
    int PlayerId;
    int Depth;
    int BeamWidth;
    public Weights Weights { get; set; }
    public BeamSearchBot(int playerId, int depth, int beamWidth) 
    { 
        PlayerId = playerId;
        Depth = depth;
        BeamWidth = beamWidth;
        Weights = new Weights 
        { 
            Params = [
                -0.5971339f,  // Pointless Dir penalty
                -0.31787202f, // Farming harvesters value
                0.73444957f, // Aggresive tentacles value
                0.59324366f, // Sporer value
                0.54558486f, // Basic value
                0.5661302f,  // Root value
                -0.07786243f, // PROTEIN_A value
                -0.767483f,  // PROTEIN_B value
                -0.3995268f, // PROTEIN_C value
                0.5912162f // PROTEIN_D value
            ]
        };
    }

    public List<Action> Evaluate(GameState gameState)
    {
        var bestMove = Search(gameState);

        return bestMove;
    }

    private List<Action> Search(GameState gameState)
    {
         var frontier = new List<(GameState state, List<List<Action>> moves)>
        {
            (gameState, new List<List<Action>>())
        };

        for (int depth = 0; depth < Depth; depth++)
        {
            // Generuj wszystkie możliwe następne stany
            var nextStates = new List<(GameState state, List<List<Action>> moves)>();

            foreach (var (state, moves) in frontier)
            {
                // Pobierz możliwe ruchy z aktualnego stanu
                var possibleMoves = state.GetPossibleActions(PlayerId);

                foreach (var move in possibleMoves)
                {
                    // Zastosuj ruch i utwórz nowy stan
                    var newState = state.Clone();
                    newState.ProcessTurn(move, []);

                    // Zachowaj nową ścieżkę (stan + ruchy)
                    var newMoves = new List<List<Action>>(moves) { move };
                    nextStates.Add((newState, newMoves));
                }
            }

            // Oceń każdy stan za pomocą funkcji heurystycznej
            nextStates = nextStates
                .OrderByDescending(pair => EstimateState(pair.state))
                .Take(BeamWidth)
                .ToList();

            // Jeśli osiągnęliśmy maksymalną głębokość lub nie ma więcej stanów do przetwarzania
            if (nextStates.Count == 0)
                break;

            // Przejdź do kolejnego poziomu drzewa
            frontier = nextStates;
        }

        // Wybierz najlepszy stan i ruchy
        var bestPath = frontier
            .OrderByDescending(pair => Evaluate(pair.state))
            .FirstOrDefault();

        // Zwróć pierwszy ruch z najlepszej ścieżki
        return bestPath.moves.FirstOrDefault();
    }

    protected float EstimateState(GameState gameState)
    {
        var myEntities = gameState.Player1Entities;
        var enemyEntities = gameState.Player0Entities;
        var myProteins = gameState.Player1Proteins;
        var enemyProteins = gameState.Player0Proteins;

        if (gameState.IsGameOver)
        {
            return (float)100000*(myEntities.Count - enemyEntities.Count);
        }

        float score = 0f;

        foreach (var entity in myEntities.Values)
        {
            var dir = Utils.DirToVector(entity.Dir);
            (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
            if (!gameState.IsPositionValid(neighborPos))
            {
                score -= Weights.Params[0];
            }
            switch (entity.Type)
            {
                case CellType.HARVESTER:
                    if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
                    {
                        score += Weights.Params[1];
                    }
                    else
                    {
                        score -= Weights.Params[1];
                    }
                    break;
                case CellType.TENTACLE:
                    if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
                    {
                        score += Weights.Params[2];
                    }
                    else
                    {
                        score -= Weights.Params[2];
                    }
                    break;

                case CellType.SPORER:
                    score += Weights.Params[3];
                    break;
                case CellType.BASIC:
                    score += Weights.Params[4];
                    break;
                case CellType.ROOT:
                    score += Weights.Params[5];
                    break;
            }
        }

        foreach (var entity in enemyEntities.Values)
        {
            var dir = Utils.DirToVector(entity.Dir);
            (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
            switch (entity.Type)
            {
                case CellType.HARVESTER:
                    if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
                    {
                        score -= Weights.Params[1];
                    }
                    else
                    {
                        score += Weights.Params[1];
                    }
                    break;
                case CellType.TENTACLE:
                    if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
                    {
                        score -= Weights.Params[2];
                    }
                    else
                    {
                        score += Weights.Params[2];
                    }
                    break;

                case CellType.SPORER:
                    score -= Weights.Params[3];
                    break;
                case CellType.BASIC:
                    score -= Weights.Params[4];
                    break;
                case CellType.ROOT:
                    score -= Weights.Params[5];
                    break;
            }
        }

        score += myProteins.A * Weights.Params[6];
        score += myProteins.B * Weights.Params[7];
        score += myProteins.C * Weights.Params[8];
        score += myProteins.D * Weights.Params[9];

        score -= enemyProteins.A * Weights.Params[6];
        score -= enemyProteins.B * Weights.Params[7];
        score -= enemyProteins.C * Weights.Params[8];
        score -= enemyProteins.D * Weights.Params[9];

        return score;
    }
}