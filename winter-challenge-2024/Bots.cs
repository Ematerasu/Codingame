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

    public Action[] Evaluate(NewGameState gameState)
    {
        var actionsList = gameState.GenerateActions(PlayerId);
        var rndIdx = rng.Next(actionsList.Count);
        return actionsList[rndIdx];
    }
}
struct Weights
{
    public int[] Params; // Trzymaj wszystkie wagi w tablicy dla łatwiejszej manipulacji.
    public Weights(int[] weightParams)
    {
        Params = weightParams;
    }
}

class BeamSearchBot
{
    int PlayerId;
    int Depth;
    int BeamWidth;
    Random rng;
    public Weights Weights { get; set; }
    public BeamSearchBot(int playerId, int depth, int beamWidth) 
    { 
        rng = new Random();
        PlayerId = playerId;
        Depth = depth;
        BeamWidth = beamWidth;
        Weights = new Weights 
        { 
            Params = [
                1, // Pointless Dir penalty
                1, // Farming harvesters value
                1, // Aggresive tentacles value
                1, // Sporer value
                1, // Basic value
                1, // Root value
                1, // PROTEIN_A value
                1, // PROTEIN_B value
                1, // PROTEIN_C value
                1, // PROTEIN_D value
            ]
        };
    }

    public Action[] Evaluate(NewGameState gameState)
    {
        Utils.watch.Restart();
        var bestMove = Search(gameState);
        Utils.watch.Stop();

        return bestMove;
    }

    private Action[] Search(NewGameState gameState)
    {
        var frontier = new List<(NewGameState state, Action[] moves)>
        {
            (gameState, [])
        };

        for (int depth = 0; depth < Depth; depth++)
        {
            Debug.Log($"Depth: {depth}\n");
            // if (Utils.watch.ElapsedMilliseconds >= Utils.MAX_TURN_TIME - 3)
            //     break;
            var nextStates = new List<(NewGameState state, Action[] moves)>();

            foreach (var (state, moves) in frontier)
            {
                // if (Utils.watch.ElapsedMilliseconds >= Utils.MAX_TURN_TIME - 3)
                //     break;
                // Pobierz możliwe ruchy z aktualnego stanu
                //state.Print();
                var possibleMoves = state.GenerateActions(PlayerId);
                var enemyMoves = state.GenerateActions(1 - PlayerId);
                var rndIdx = rng.Next(enemyMoves.Count);
                var randomEnemyMove = enemyMoves[rndIdx];
                Debug.Log($"States at depth {depth}: {possibleMoves.Count}\n");
                foreach (var move in possibleMoves)
                {
                    if (move.All(act => act.Command == CommandEnum.WAIT))
                        continue;
                    var newState = state.Clone();
                    if (PlayerId == 0)
                        newState.ProcessTurn(randomEnemyMove, move);
                    else
                        newState.ProcessTurn(move, randomEnemyMove);

                    if (moves.Length == 0)
                    {
                        nextStates.Add((newState, move));
                    }
                    else
                    {
                        nextStates.Add((newState, moves));
                    }
                    // if (Utils.watch.ElapsedMilliseconds >= Utils.MAX_TURN_TIME - 3)
                    //     break;
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
            .OrderByDescending(pair => EstimateState(pair.state))
            .FirstOrDefault();
        //Console.Error.WriteLine($"Did move in {Utils.watch.ElapsedMilliseconds}ms");
        // Zwróć pierwszy ruch z najlepszej ścieżki
        return bestPath.moves;
    }

    protected float EstimateState(NewGameState gameState)
    {
        // var myEntities = gameState.Player1Entities;
        // var enemyEntities = gameState.Player0Entities;
        // var myProteins = gameState.Player1Proteins;
        // var enemyProteins = gameState.Player0Proteins;
        return gameState.GenerateActions(PlayerId).Count - (float)gameState.GenerateActions(1 - PlayerId).Count;
        // if (gameState.IsGameOver)
        // {
        //     return (float)100000*(myEntities.Count - enemyEntities.Count);
        // }

        // float score = 0f;

        // foreach (var entity in myEntities.Values)
        // {
        //     var dir = Utils.DirToVector(entity.Dir);
        //     (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
        //     if (!gameState.IsPositionValid(neighborPos))
        //     {
        //         score -= Weights.Params[0];
        //     }
        //     switch (entity.Type)
        //     {
        //         case CellType.HARVESTER:
        //             if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
        //             {
        //                 score += Weights.Params[1];
        //             }
        //             else
        //             {
        //                 score -= Weights.Params[1];
        //             }
        //             break;
        //         case CellType.TENTACLE:
        //             if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
        //             {
        //                 score += Weights.Params[2];
        //             }
        //             else
        //             {
        //                 score -= Weights.Params[2];
        //             }
        //             break;

        //         case CellType.SPORER:
        //             score += Weights.Params[3];
        //             break;
        //         case CellType.BASIC:
        //             score += Weights.Params[4];
        //             break;
        //         case CellType.ROOT:
        //             score += Weights.Params[5];
        //             break;
        //     }
        // }

        // foreach (var entity in enemyEntities.Values)
        // {
        //     var dir = Utils.DirToVector(entity.Dir);
        //     (int x, int y) neighborPos = (entity.Position.x + dir.nx, entity.Position.y + dir.ny);
        //     switch (entity.Type)
        //     {
        //         case CellType.HARVESTER:
        //             if (gameState.IsProtein(gameState.Grid[neighborPos.x, neighborPos.y].Type))
        //             {
        //                 score -= Weights.Params[1];
        //             }
        //             else
        //             {
        //                 score += Weights.Params[1];
        //             }
        //             break;
        //         case CellType.TENTACLE:
        //             if (gameState.Grid[neighborPos.x, neighborPos.y].OwnerId == 1 - PlayerId)
        //             {
        //                 score -= Weights.Params[2];
        //             }
        //             else
        //             {
        //                 score += Weights.Params[2];
        //             }
        //             break;

        //         case CellType.SPORER:
        //             score -= Weights.Params[3];
        //             break;
        //         case CellType.BASIC:
        //             score -= Weights.Params[4];
        //             break;
        //         case CellType.ROOT:
        //             score -= Weights.Params[5];
        //             break;
        //     }
        // }

        // score += myProteins.A * Weights.Params[6];
        // score += myProteins.B * Weights.Params[7];
        // score += myProteins.C * Weights.Params[8];
        // score += myProteins.D * Weights.Params[9];

        // score -= enemyProteins.A * Weights.Params[6];
        // score -= enemyProteins.B * Weights.Params[7];
        // score -= enemyProteins.C * Weights.Params[8];
        // score -= enemyProteins.D * Weights.Params[9];

        //return 0f;
    }
}