namespace SummerChallenge2025.Bot;

public abstract class AI
{
    protected int PlayerId { get; private set; }

    public virtual void Initialize(int playerId) => PlayerId = playerId;

    public abstract TurnCommand GetMove(GameState state);
}