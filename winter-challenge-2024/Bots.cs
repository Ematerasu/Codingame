using System;


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
        var myEntities = PlayerId == 0 ? gameState.Player0Entities : gameState.Player1Entities;
        List<Action> chosen = new();
        foreach(var actions in actionsList)
        {
            chosen.Add(actions.OrderBy(act => rng.Next()).First());
        }

        return chosen;
    }
}

class BeamSearchBot
{

}