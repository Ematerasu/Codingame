namespace SummerChallenge2025.Bot;

public class RandomBot : AI
{
    private readonly Random rng;
    public RandomBot()
    {
        rng = new Random();
    }
    public override TurnCommand GetMove(GameState state)
    {
        var cmd = new TurnCommand(GameState.MaxAgents);
        Span<AgentOrder> buffer = stackalloc AgentOrder[512];

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            ref readonly var ag = ref state.Agents[id];
            if (!ag.Alive || ag.playerId != PlayerId) continue;

            int n = state.GetLegalOrders(id, buffer);
            if (n == 0) continue;

            var pick = buffer[rng.Next(n)];
            cmd.SetMove(id, pick.Move);
            cmd.SetCombat(id, pick.Combat);
        }
        return cmd;
    }
}
