namespace SummerChallenge2025.Bot;

public abstract class AI
{
    public int PlayerId { get; protected set; }

    public virtual void Initialize(int playerId) => PlayerId = playerId;

    public abstract TurnCommand GetMove(GameState state);
}