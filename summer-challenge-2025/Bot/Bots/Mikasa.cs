
namespace SummerChallenge2025.Bot;

public interface IGamePhase
{
    void Enter(GameState st, int myPlayerId);
    TurnCommand GetMove(GameState st);
    bool ShouldExit(GameState st);
    IGamePhase? GetNextPhase(GameState st);
}

public sealed class GamePhaseController
{
    private readonly int _myId;
    private IGamePhase _current = new OpeningPhase();
    private bool _isFirst = true;

    public GamePhaseController(int myId) => _myId = myId;

    public TurnCommand GetMove(GameState st)
    {
        Console.Error.WriteLine($"[GamePhaseController] Current phase: {_current.GetType().Name} in turn {st.Turn}");
        if (_isFirst)
        {
            _current.Enter(st, _myId);
            _isFirst = false;
        }
        else if (_current.ShouldExit(st))
        {
            Console.Error.WriteLine($"[GamePhaseController] Exiting phase: {_current.GetType().Name} in turn {st.Turn}. Next phase: {_current.GetNextPhase(st)?.GetType().Name ?? "null"}");
            var next = _current.GetNextPhase(st);
            if (next != null)
            {
                _current = next;
                _current.Enter(st, _myId);
            }
        }
        return _current.GetMove(st);
    }
}

public sealed class Mikasa : AI
{
    private GamePhaseController? _ctrl;

    public override void Initialize(int playerId)
    {
        PlayerId = playerId;
        _ctrl = new GamePhaseController(playerId);
    }

    public override TurnCommand GetMove(GameState st)
    {
        if (_ctrl == null)
            throw new InvalidOperationException("Bot not initialized with playerId");

        return _ctrl.GetMove(st);
    }
}